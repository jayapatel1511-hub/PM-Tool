using System.Net;
using System.Text.Json.Nodes;
using Hub.Domain;

namespace Hub.Tests.Api;

[Collection("api")]
public sealed class PlanningPrivacyTests(HubFactory f)
{
    readonly TestData d = new(f);
    static readonly DateOnly Week = new(2026, 9, 14);

    [Fact]
    public async Task Private_draft_is_visible_to_creator_but_not_subject_or_read_only_user()
    {
        var alex = d.User(TestData.Alex);
        var created = await (await f.As(TestData.Sam).Post("/api/v1/planning/entries", new
        {
            personId = alex, hoursPerWeek = 6m, startWeek = Week, endWeek = Week, label = "Private supervisor note", sourceCategory = PlanningSource.Other,
        })).Json(201);
        var id = created.G("id");
        var creator = await (await f.As(TestData.Sam).GetAsync($"/api/v1/planning/entries/{id}")).Json();
        Assert.Equal(id, creator.G("id"));
        await (await f.As(TestData.Alex).GetAsync($"/api/v1/planning/entries/{id}")).Json(404);
        await (await f.As(TestData.Rita).GetAsync($"/api/v1/planning/entries/{id}")).Json(404);
    }

    [Fact]
    public async Task Read_only_cannot_create_or_record_time_away_and_admin_correction_is_logged()
    {
        var alex = d.User(TestData.Alex);
        Assert.Equal(HttpStatusCode.Forbidden, (await f.As(TestData.Rita).Post("/api/v1/planning/entries", new
        {
            personId = alex, hoursPerWeek = 4m, startWeek = Week, endWeek = Week, label = "Nope", sourceCategory = PlanningSource.Other,
        })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await f.As(TestData.Rita).Post("/api/v1/planning/time-away", new
        {
            personId = alex, from = Week, through = Week, action = "Record", availableHours = 0m, category = AvailabilityCategory.Unavailable,
            versions = Array.Empty<object>(),
        })).StatusCode);
        var correction = await (await f.As(TestData.Admin).GetAsync($"/api/v1/planning/grid?personId={alex}&from={Week:yyyy-MM-dd}&weeks=1&draftAccessReason=privacy%20review")).Json();
        Assert.True(correction["people"]!.AsArray().Count <= 1);
        Assert.True(f.Db(db => db.ActivityLog.Any(x => x.ItemType == "PlanningDraftAccess" && x.Reason == "privacy review")));
    }

    [Fact]
    public async Task Person_outside_supervisor_scope_is_not_enumerable()
    {
        var diane = d.User(TestData.Diane);
        await (await f.As(TestData.Sam).GetAsync($"/api/v1/planning/grid?personId={diane}&from={Week:yyyy-MM-dd}&weeks=1")).Json(404);
        await (await f.As(TestData.Sam).GetAsync($"/api/v1/planning/cells/{diane}/{Week:yyyy-MM-dd}")).Json(404);
    }

    [Fact]
    public async Task Private_draft_is_absent_from_every_read_surface_for_person_and_read_only_roles()
    {
        var alex = d.User(TestData.Alex);
        var created = await (await f.As(TestData.Sam).Post("/api/v1/planning/entries", new
        {
            personId = alex, hoursPerWeek = 8m, startWeek = Week, endWeek = Week,
            label = "UNIQUE PRIVATE SYNTHETIC DRAFT", sourceCategory = PlanningSource.Other
        })).Json(201);
        var id = created.G("id");
        var routes = new[]
        {
            $"/api/v1/planning/grid?personId={alex}&from={Week:yyyy-MM-dd}&weeks=1",
            $"/api/v1/planning/cells/{alex}/{Week:yyyy-MM-dd}",
            $"/api/v1/planning/entries?personId={alex}&from={Week:yyyy-MM-dd}&to={Week:yyyy-MM-dd}",
            $"/api/v1/planning/entries/{id}",
            $"/api/v1/planning/entries/{id}/activity",
            "/api/v1/planning/entries/export?format=csv",
            "/api/v1/planning/grid/export?format=csv",
            "/api/v1/search?q=UNIQUE%20PRIVATE%20SYNTHETIC&type=planning",
            "/api/v1/me/notifications",
            "/api/v1/admin/activity?itemType=PlanningEntry"
        };
        foreach (var role in new[] { TestData.Alex, TestData.Rita, TestData.Marc, TestData.Pm })
        {
            foreach (var route in routes)
            {
                var response = await f.As(role).GetAsync(route);
                var body = await response.Content.ReadAsStringAsync();
                Assert.DoesNotContain("UNIQUE PRIVATE SYNTHETIC DRAFT", body, StringComparison.Ordinal);
                Assert.DoesNotContain(id.ToString(), body, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    [Fact]
    public async Task Admin_data_correction_requires_reason_logs_access_and_excludes_private_draft_from_normal_activity()
    {
        var alex = d.User(TestData.Alex);
        var created = await (await f.As(TestData.Sam).Post("/api/v1/planning/entries", new
        {
            personId = alex, hoursPerWeek = 6m, startWeek = Week, endWeek = Week,
            label = "Correction-only synthetic draft", sourceCategory = PlanningSource.Other
        })).Json(201);
        var id = created.G("id");
        await (await f.As(TestData.Admin).GetAsync($"/api/v1/planning/grid?personId={alex}&from={Week:yyyy-MM-dd}&weeks=1&draftAccessReason=no")).Json(400);
        var corrected = await (await f.As(TestData.Admin).GetAsync($"/api/v1/planning/grid?personId={alex}&from={Week:yyyy-MM-dd}&weeks=1&draftAccessReason=privacy%20review")).Json();
        Assert.Contains(id, corrected["people"]!.AsArray().SelectMany(p => p!["entries"]!.AsArray()).Select(e => e!.G("id")));
        Assert.True(f.Db(db => db.ActivityLog.Any(x => x.ItemType == "PlanningDraftAccess" && x.Reason == "privacy review")));
        var normalActivity = await (await f.As(TestData.Admin).GetAsync("/api/v1/admin/activity?itemType=PlanningDraftAccess")).Json();
        Assert.DoesNotContain("Correction-only synthetic draft", normalActivity.ToJsonString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Read_only_and_subject_cannot_create_change_or_record_time_away_for_another_person()
    {
        var alex = d.User(TestData.Alex);
        var body = new { personId = d.User(TestData.Jill), hoursPerWeek = 4m, startWeek = Week, endWeek = Week, label = "Forbidden synthetic plan", sourceCategory = PlanningSource.Other };
        Assert.Equal(HttpStatusCode.Forbidden, (await f.As(TestData.Rita).Post("/api/v1/planning/entries", body)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await f.As(TestData.Alex).Post("/api/v1/planning/entries", body)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await f.As(TestData.Rita).Post("/api/v1/planning/time-away", new
        {
            personId = alex, from = Week, through = Week, action = "Record", availableHours = 0m, category = "Unavailable", versions = Array.Empty<object>()
        })).StatusCode);
    }
}
