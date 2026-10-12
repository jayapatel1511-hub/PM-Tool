using Hub.Api.Data;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace Hub.Tests.Api;

/// Packet 036 T007/T008: the StaffingSignals schema, its StartedAt backfill (D4) and its rollback.
[Collection("api")]
public sealed class StaffingSignalsSchemaTests(HubFactory f)
{
    readonly TestData d = new(f);
    static readonly DateOnly Week = new(2026, 11, 2);

    [Theory]
    [InlineData("reversed", "ck_open_role_dates")]
    [InlineData("zeroHours", "ck_open_role_hours")]
    [InlineData("negativeHours", "ck_open_role_hours")]
    [InlineData("status", "ck_open_role_status")]
    [InlineData("labelEmpty", "ck_open_role_label")]
    [InlineData("labelLong", "ck_open_role_label")]
    public async Task PostgreSQL_refuses_each_invalid_open_role(string change, string constraint) // AC-ROLE-01
    {
        var project = await d.Project();
        var row = new OpenRole { ProjectId = project.Id, RoleLabel = "Civil technologist", FromDate = Week, ThroughDate = Week.AddDays(25), PlannedHours = 80 };
        switch (change)
        {
            case "reversed": row.ThroughDate = Week.AddDays(-1); break;
            case "zeroHours": row.PlannedHours = 0; break;
            case "negativeHours": row.PlannedHours = -5; break;
            case "status": row.Status = "Proposed"; break;
            case "labelEmpty": row.RoleLabel = "  "; break;
            case "labelLong": row.RoleLabel = new string('x', 81); break;
        }
        var e = await Assert.ThrowsAsync<DbUpdateException>(() => f.DbAsync(async db => { db.OpenRoles.Add(row); return await db.SaveChangesAsync(); }));
        Assert.Equal(constraint, Assert.IsType<PostgresException>(e.InnerException).ConstraintName);
    }

    [Fact]
    public async Task Open_roles_are_soft_deleted_audited_and_fill_at_most_once()
    {
        var project = await d.Project();
        var allocation = new ResourceAllocation { ProjectId = project.Id, PersonId = d.User(TestData.Alex), FromDate = Week, ThroughDate = Week.AddDays(4), PlannedHours = 20 };
        var role = new OpenRole { ProjectId = project.Id, RoleLabel = "Civil technologist", FromDate = Week, ThroughDate = Week.AddDays(4), PlannedHours = 20,
            ProjectDisciplineId = d.ProjectDiscipline(project.Id, "Civil"), Status = OpenRoleStatus.Filled, FilledAllocationId = allocation.Id };
        await f.DbAsync(async db => { db.Allocations.Add(allocation); db.OpenRoles.Add(role); return await db.SaveChangesAsync(); });

        var created = f.Db(db => db.ActivityLog.AsNoTracking().Single(a => a.ItemId == role.Id));
        Assert.Equal(("OpenRole", "Created", (Guid?)project.Id, "Civil technologist"), (created.ItemType, created.Action, created.ProjectId, created.ItemName));
        var fields = System.Text.Json.JsonDocument.Parse(created.Changes).RootElement.EnumerateArray().Select(c => c.GetProperty("field").GetString()).ToList();
        Assert.Subset(fields.ToHashSet(), new HashSet<string?> { "RoleLabel", "FromDate", "ThroughDate", "PlannedHours", "Status", "FilledAllocationId", "ProjectDisciplineId" });

        var twice = new OpenRole { ProjectId = project.Id, RoleLabel = "Second", FromDate = Week, ThroughDate = Week, PlannedHours = 8, Status = OpenRoleStatus.Filled, FilledAllocationId = allocation.Id };
        var e = await Assert.ThrowsAsync<DbUpdateException>(() => f.DbAsync(async db => { db.OpenRoles.Add(twice); return await db.SaveChangesAsync(); }));
        Assert.Equal("ix_open_role_filled_allocation_id", Assert.IsType<PostgresException>(e.InnerException).ConstraintName);

        await f.DbAsync(async db => { (await db.OpenRoles.SingleAsync(x => x.Id == role.Id)).DeletedAt = f.Clock.GetUtcNow(); return await db.SaveChangesAsync(); });
        Assert.False(f.Db(db => db.OpenRoles.Any(x => x.Id == role.Id)));
        Assert.True(f.Db(db => db.OpenRoles.IgnoreQueryFilters().Any(x => x.Id == role.Id && x.RowVersion == 1)));
        var indexes = await f.DbAsync(db => db.Database.SqlQueryRaw<string>("SELECT indexdef AS \"Value\" FROM pg_indexes WHERE schemaname='hub' AND tablename='open_role'").ToListAsync());
        Assert.Contains(indexes, x => x.Contains("(project_id, status) WHERE (deleted_at IS NULL)"));
    }

    [Fact]
    public async Task PostgreSQL_refuses_unknown_dependency_types_and_new_links_default_to_finish_to_start() // AC-DEPT-01
    {
        var project = await d.Project();
        var a = (await d.NewTask(project.Id)).G("id");
        var b = (await d.NewTask(project.Id)).G("id");
        var bad = await Assert.ThrowsAsync<DbUpdateException>(() => f.DbAsync(async db =>
        {
            db.Dependencies.Add(new TaskDependency { ProjectId = project.Id, PredecessorTaskId = a, SuccessorTaskId = b, DependencyType = "StartToFinish" });
            return await db.SaveChangesAsync();
        }));
        Assert.Equal("ck_task_dependency_type", Assert.IsType<PostgresException>(bad.InnerException).ConstraintName);

        var (pred, succ) = (await Deliverable(project.Id), await Deliverable(project.Id));
        var badDl = await Assert.ThrowsAsync<DbUpdateException>(() => f.DbAsync(async db =>
        {
            db.DeliverableDependencies.Add(new DeliverableDependency { ProjectId = project.Id, PredecessorDeliverableId = pred, SuccessorDeliverableId = succ, DependencyType = "xyz" });
            return await db.SaveChangesAsync();
        }));
        Assert.Equal("ck_deliverable_dependency_type", Assert.IsType<PostgresException>(badDl.InnerException).ConstraintName);

        var dflt = await f.DbAsync(db => db.Database.SqlQueryRaw<string>(
            "SELECT column_default AS \"Value\" FROM information_schema.columns WHERE table_schema='hub' AND table_name='deliverable_dependency' AND column_name='dependency_type'").SingleAsync());
        Assert.Equal("'FinishToStart'::text", dflt);
    }

    [Fact]
    public async Task Migration_backfills_started_dates_reads_every_link_as_finish_to_start_and_rolls_back() // T008, D4, AC-DEPT-01a
    {
        using var isolated = new HubFactory();
        var data = new TestData(isolated);
        var project = await data.Project();
        async Task<Guid> NewTask() => (await data.NewTask(project.Id)).G("id");
        var logged = await NewTask();    // leaves Not Started (logged), is reopened, then restarts later
        var fallback = await NewTask();  // started with no logged change: StatusChangedAt
        var noDates = await NewTask();   // started with no logged change and no StatusChangedAt: CreatedAt
        var reopened = await NewTask();  // started, then back to Not Started: none
        var untouched = await NewTask(); // never started: none
        var started = await Deliverable(project.Id, isolated, data);
        var notStarted = await Deliverable(project.Id, isolated, data);
        var t1 = new DateTimeOffset(2026, 9, 15, 13, 0, 0, TimeSpan.Zero);
        async Task Move(DateTimeOffset at, Action<HubDb> change)
        {
            isolated.Clock.Now = at;
            await isolated.DbAsync(async db => { change(db); return await db.SaveChangesAsync(); });
        }
        void SetTask(HubDb db, Guid id, string status) { var t = db.Tasks.Single(x => x.Id == id); (t.Status, t.StatusChangedAt) = (status, isolated.Clock.Now); }
        await Move(t1, db =>
        {
            SetTask(db, logged, TaskStatuses.InProgress); SetTask(db, reopened, TaskStatuses.InProgress);
            var dl = db.Deliverables.Single(x => x.Id == started); (dl.Status, dl.StatusChangedAt) = (DeliverableStatus.InProgress, t1);
        });
        await Move(t1.AddDays(1), db => { SetTask(db, logged, TaskStatuses.NotStarted); SetTask(db, reopened, TaskStatuses.NotStarted); });
        await Move(t1.AddDays(2), db => SetTask(db, logged, TaskStatuses.InProgress));
        var taskLink = new TaskDependency { ProjectId = project.Id, PredecessorTaskId = logged, SuccessorTaskId = untouched };
        var deliverableLink = new DeliverableDependency { ProjectId = project.Id, PredecessorDeliverableId = started, SuccessorDeliverableId = notStarted };
        await isolated.DbAsync(async db => { db.Dependencies.Add(taskLink); db.DeliverableDependencies.Add(deliverableLink); return await db.SaveChangesAsync(); });

        string[] counted = ["task", "deliverable", "task_dependency", "deliverable_dependency", "activity_log", "app_user", "project"];
        var countSql = $"SELECT {string.Join(" || ' ' || ", counted.Select(t => $"(SELECT count(*) FROM hub.{t})::text"))} AS \"Value\"";
        Task<string> Counts() => isolated.DbAsync(db => db.Database.SqlQueryRaw<string>(countSql).SingleAsync());
        var latest = await Schema(isolated);
        var rows = await Counts();

        // Down: the previous schema exactly, with existing rows kept.
        await isolated.DbAsync(async db =>
        {
            var applied = (await db.Database.GetAppliedMigrationsAsync()).ToArray();
            Assert.EndsWith("_StaffingSignals", applied[^1]);
            await db.GetService<IMigrator>().MigrateAsync(applied[^2]);
            return 0;
        });
        var previous = await Schema(isolated);
        Assert.Empty(previous.Except(latest)); // down leaves nothing behind and alters nothing else
        var added = latest.Except(previous).ToList();
        Assert.Equal(
        [
            "col app_user.home_discipline_id uuid YES ",
            "col deliverable.started_at timestamp with time zone YES ",
            "col deliverable_dependency.dependency_type text NO 'FinishToStart'::text",
            "col task.started_at timestamp with time zone YES ",
            "con app_user.fk_app_user_disciplines_home_discipline_id FOREIGN KEY (home_discipline_id) REFERENCES discipline(id) ON DELETE RESTRICT",
            "con deliverable_dependency.ck_deliverable_dependency_type CHECK ((dependency_type = ANY (ARRAY['FinishToStart'::text, 'StartToStart'::text, 'FinishToFinish'::text])))",
            "con task_dependency.ck_task_dependency_type CHECK ((dependency_type = ANY (ARRAY['FinishToStart'::text, 'StartToStart'::text, 'FinishToFinish'::text])))",
            "idx ix_app_user_home_discipline_id CREATE INDEX ix_app_user_home_discipline_id ON hub.app_user USING btree (home_discipline_id)",
        ], added.Where(x => !x.Contains("open_role")).Order(StringComparer.Ordinal));
        Assert.Equal(17 + 8 + 4, added.Count(x => x.Contains("open_role"))); // 17 columns; pk, 4 checks, 3 foreign keys; 4 indexes
        Assert.Equal(rows, await Counts());

        // Legacy data the backfill has to read: rows changed without a logged status change, and a link type nobody read.
        await isolated.DbAsync(async db =>
        {
            await db.Database.ExecuteSqlAsync($"UPDATE hub.task SET status = 'In Progress', status_changed_at = '2026-09-10T08:00:00Z' WHERE id = {fallback}");
            await db.Database.ExecuteSqlAsync($"UPDATE hub.task SET status = 'Complete', status_changed_at = NULL WHERE id = {noDates}");
            return await db.Database.ExecuteSqlAsync($"UPDATE hub.task_dependency SET dependency_type = 'Legacy' WHERE id = {taskLink.Id}");
        });

        // Up again: the same schema as a fresh migration, and the backfill.
        await isolated.DbAsync(async db => { await db.Database.MigrateAsync(); return 0; });
        var again = await Schema(isolated);
        Assert.Empty(latest.Except(again));
        Assert.Empty(again.Except(latest));
        Assert.Equal(rows, await Counts());
        await isolated.DbAsync(async db =>
        {
            var tasks = await db.Tasks.AsNoTracking().Where(t => t.ProjectId == project.Id).ToDictionaryAsync(t => t.Id);
            Assert.Equal(t1, tasks[logged].StartedAt);           // the first change away from Not Started, not the later restart
            Assert.Equal(t1.AddDays(2), tasks[logged].StatusChangedAt);
            Assert.Equal(new DateTimeOffset(2026, 9, 10, 8, 0, 0, TimeSpan.Zero), tasks[fallback].StartedAt);
            Assert.Equal(tasks[noDates].CreatedAt, tasks[noDates].StartedAt);
            Assert.Null(tasks[reopened].StartedAt);
            Assert.Null(tasks[untouched].StartedAt);
            var dls = await db.Deliverables.AsNoTracking().Where(x => x.ProjectId == project.Id).ToDictionaryAsync(x => x.Id);
            Assert.Equal(t1, dls[started].StartedAt);
            Assert.Null(dls[notStarted].StartedAt);
            Assert.Equal(DependencyTypes.FinishToStart, (await db.Dependencies.AsNoTracking().SingleAsync(x => x.Id == taskLink.Id)).DependencyType);
            Assert.Equal(DependencyTypes.FinishToStart, (await db.DeliverableDependencies.AsNoTracking().SingleAsync(x => x.Id == deliverableLink.Id)).DependencyType);
            Assert.False(await db.Dependencies.IgnoreQueryFilters().AnyAsync(x => x.DependencyType != DependencyTypes.FinishToStart));
            Assert.False(await db.DeliverableDependencies.IgnoreQueryFilters().AnyAsync(x => x.DependencyType != DependencyTypes.FinishToStart));
            return 0;
        });
    }

    /// Columns, constraints and indexes of the hub schema, one line each.
    static async Task<HashSet<string>> Schema(HubFactory factory) => [.. await factory.DbAsync(db => db.Database.SqlQueryRaw<string>("""
        SELECT 'col ' || table_name || '.' || column_name || ' ' || data_type || ' ' || is_nullable || ' ' || coalesce(column_default, '') AS "Value"
          FROM information_schema.columns WHERE table_schema = 'hub'
        UNION ALL
        SELECT 'con ' || c.relname || '.' || k.conname || ' ' || pg_get_constraintdef(k.oid)
          FROM pg_constraint k JOIN pg_class c ON c.oid = k.conrelid JOIN pg_namespace n ON n.oid = c.relnamespace WHERE n.nspname = 'hub'
        UNION ALL
        SELECT 'idx ' || indexname || ' ' || indexdef FROM pg_indexes WHERE schemaname = 'hub'
        """).ToListAsync())];

    Task<Guid> Deliverable(Guid projectId) => Deliverable(projectId, f, d);

    static async Task<Guid> Deliverable(Guid projectId, HubFactory factory, TestData data) =>
        (await factory.As(TestData.Pm).Post($"/api/v1/projects/{projectId}/deliverables",
            new { name = "Deliverable " + Guid.NewGuid().ToString("N")[..4], projectDisciplineId = data.ProjectDiscipline(projectId, "Civil"), deliverableTypeId = await data.DeliverableType() })
            .Result.Json(201)).G("id");
}
