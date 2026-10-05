using System.Text.Json;
using Hub.Api.Data;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace Hub.Tests.Api;

[Collection("api")]
public sealed class PlanningSchemaTests(HubFactory f)
{
    readonly TestData d = new(f);
    static readonly DateOnly Week = new(2026, 10, 5);

    [Theory]
    [InlineData("hours", "ck_planning_hours")]
    [InlineData("zero", "ck_planning_hours")]
    [InlineData("tooManyHours", "ck_planning_hours")]
    [InlineData("startTuesday", "ck_planning_weeks")]
    [InlineData("endTuesday", "ck_planning_weeks")]
    [InlineData("reversed", "ck_planning_weeks")]
    [InlineData("span", "ck_planning_weeks")]
    [InlineData("labelEmpty", "ck_planning_label")]
    [InlineData("labelLong", "ck_planning_label")]
    [InlineData("notesLong", "ck_planning_notes")]
    [InlineData("confidence", "ck_planning_confidence")]
    [InlineData("visibility", "ck_planning_visibility")]
    [InlineData("source", "ck_planning_source")]
    [InlineData("selfDraft", "ck_planning_self_visible")]
    [InlineData("majorWithoutProject", "ck_planning_project_source")]
    [InlineData("otherWithProject", "ck_planning_project_source")]
    [InlineData("disciplineWithoutProject", "ck_planning_discipline")]
    [InlineData("owner", "ck_planning_owner")]
    public async Task PostgreSQL_refuses_each_invalid_record(string change, string constraint)
    {
        var project = await d.Project();
        var row = new PlanningEntry { PersonId = d.User(TestData.Alex), CreatedBy = d.User(TestData.Sam),
            HoursPerWeek = 8, StartWeek = Week, EndWeek = Week, Label = "Synthetic constraint check", LastValidatedAt = f.Clock.GetUtcNow() };
        switch (change)
        {
            case "hours": row.HoursPerWeek = 8.1m; break;
            case "zero": row.HoursPerWeek = 0; break;
            case "tooManyHours": row.HoursPerWeek = 168.5m; break;
            case "startTuesday": row.StartWeek = Week.AddDays(1); break;
            case "endTuesday": row.EndWeek = Week.AddDays(1); break;
            case "reversed": row.EndWeek = Week.AddDays(-7); break;
            case "span": row.EndWeek = Week.AddDays(728); break;
            case "labelEmpty": row.Label = " "; break;
            case "labelLong": row.Label = new string('x', 121); break;
            case "notesLong": row.Notes = new string('x', 2001); break;
            case "confidence": row.Confidence = "Bogus"; break;
            case "visibility": row.Visibility = "Bogus"; break;
            case "source": row.SourceCategory = "Leave"; break;
            case "selfDraft": row.CreatedBy = row.PersonId; break;
            case "majorWithoutProject": row.SourceCategory = PlanningSource.MajorProject; break;
            case "otherWithProject": row.ProjectId = project.Id; break;
            case "disciplineWithoutProject": row.ProjectDisciplineId = d.ProjectDiscipline(project.Id, "Civil"); break;
            case "owner": row.CreatedBy = null; break;
        }
        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => f.DbAsync(async db =>
        {
            db.PlanningEntries.Add(row); await db.SaveChangesAsync(); return 0;
        }));
        Assert.Equal(constraint, Assert.IsType<PostgresException>(exception.InnerException).ConstraintName);
    }

    [Fact]
    public async Task Indexes_foreign_keys_and_soft_delete_filter_are_present_in_database()
    {
        await (await f.As(TestData.Admin).GetAsync("/api/v1/me")).Json();
        await f.DbAsync(async db =>
        {
            var indexes = await db.Database.SqlQueryRaw<string>("SELECT indexname AS \"Value\" FROM pg_indexes WHERE schemaname='hub' AND tablename='planning_entry'").ToListAsync();
            Assert.Contains("ix_planning_entry_person_id_start_week_end_week", indexes);
            Assert.Contains("ix_planning_entry_created_by_end_week", indexes);
            Assert.Contains("ix_planning_entry_project_id", indexes);
            var foreignKeys = await db.Database.SqlQueryRaw<string>("SELECT conname AS \"Value\" FROM pg_constraint WHERE conrelid='hub.planning_entry'::regclass AND contype='f'").ToListAsync();
            Assert.Equal(6, foreignKeys.Count);
            Assert.NotEmpty(db.Model.FindEntityType(typeof(PlanningEntry))!.GetDeclaredQueryFilters());
            var row = new PlanningEntry { PersonId = d.User(TestData.Alex), CreatedBy = d.User(TestData.Sam),
                HoursPerWeek = 8, StartWeek = Week, EndWeek = Week, Label = "Synthetic deleted plan", DeletedAt = f.Clock.GetUtcNow(), LastValidatedAt = f.Clock.GetUtcNow() };
            db.PlanningEntries.Add(row); await db.SaveChangesAsync();
            Assert.False(await db.PlanningEntries.AnyAsync(x => x.Id == row.Id));
            Assert.True(await db.PlanningEntries.IgnoreQueryFilters().AnyAsync(x => x.Id == row.Id)); return 0;
        });
    }

    [Fact]
    public async Task Migration_down_and_up_on_fresh_database_preserve_existing_allocation_data()
    {
        using var isolated = new HubFactory();
        var data = new TestData(isolated); var project = await data.Project();
        var person = data.User(TestData.Alex);
        await isolated.DbAsync(async db =>
        {
            db.Allocations.Add(new ResourceAllocation { ProjectId = project.Id, PersonId = person, FromDate = Week,
                ThroughDate = Week.AddDays(4), PlannedHours = 12, Status = AllocationStatus.Confirmed });
            db.AvailabilityOverrides.Add(new PersonAvailabilityOverride { PersonId = person, WorkDate = Week,
                AvailableHours = 4, Category = AvailabilityCategory.Reduced });
            await db.SaveChangesAsync(); return 0;
        });
        async Task<string> Snapshot() => await isolated.DbAsync(async db => JsonSerializer.Serialize(new
        {
            Allocations = await db.Allocations.AsNoTracking().OrderBy(x => x.Id).ToListAsync(),
            Overrides = await db.AvailabilityOverrides.AsNoTracking().OrderBy(x => x.Id).ToListAsync(),
            Versions = await db.PersonDateVersions.AsNoTracking().OrderBy(x => x.Id).ToListAsync(),
        }));
        var before = await Snapshot();
        await isolated.DbAsync(async db =>
        {
            var migrations = (await db.Database.GetAppliedMigrationsAsync()).ToArray();
            var planningIndex = Array.FindIndex(migrations, m => m.EndsWith("_PlanningEntries"));
            Assert.True(planningIndex > 0);
            await db.GetService<IMigrator>().MigrateAsync(migrations[planningIndex - 1]);
            Assert.Null(await db.Database.SqlQueryRaw<string>("SELECT to_regclass('hub.planning_entry')::text AS \"Value\"").SingleAsync());
            await db.Database.MigrateAsync();
            Assert.NotNull(await db.Database.SqlQueryRaw<string>("SELECT to_regclass('hub.planning_entry')::text AS \"Value\"").SingleAsync());
            return 0;
        });
        Assert.Equal(before, await Snapshot());
    }
}
