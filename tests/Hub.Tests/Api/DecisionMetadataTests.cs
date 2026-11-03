using System.Text.Json;
using System.Text.Json.Nodes;
using Hub.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

public sealed class DecisionMetadataTests : IDisposable
{
    readonly HubFactory f = new(); readonly TestData d;
    public DecisionMetadataTests() { d = new(f); f.Clock.Now = new(2026, 11, 14, 14, 0, 0, TimeSpan.Zero); }
    public void Dispose() => f.Dispose();
    async Task Flag(bool on) => await (await f.As(TestData.Admin).Put("/api/v1/admin/settings/flag.register_screens", new { value = on })).Json();
    async Task<Guid> Category() => (await (await f.As(TestData.Admin).Post("/api/v1/admin/decisionCategories", new { name = "Synthetic choice", sortOrder = 1 })).Json(201)).G("id");
    Dictionary<string, object?> Body() => new() { ["subject"] = "Synthetic decision", ["description"] = "Synthetic context", ["ownerUserId"] = d.User(TestData.Alex), ["requestedById"] = d.User(TestData.Marc),
        ["dateRequested"] = "2026-11-01", ["requiredByDate"] = "2026-11-20", ["impactLevel"] = "Medium", ["impactDescription"] = "Synthetic impact" };
    int Version(Guid id) => f.Db(db => db.Decisions.Where(row => row.Id == id).Select(row => row.RowVersion).Single());
    string State() => f.Db(db => JsonSerializer.Serialize(new { Rows = db.Decisions.AsNoTracking().OrderBy(row => row.Id).Select(row => new { row.Id, row.Subject, row.CategoryId, row.OptionsConsidered, row.Rationale, row.Status, row.RowVersion, row.LastActivityAt }).ToArray(), Activity = db.ActivityLog.Count(), Notifications = db.Notifications.Count(), Receipts = db.Idempotency.Count() }));
    async Task<Guid> Raise(Guid project, Dictionary<string, object?>? body = null) => (await (await f.As(TestData.Pm).Post($"/api/v1/projects/{project}/decisions", body ?? Body())).Json(201)).G("id");

    [Fact]
    public async Task Optional_ordered_metadata_uses_actual_categories_and_rejects_invalid_mixed_bodies_atomically()
    {
        await Flag(true); var p = await d.Project(); var category = await Category();
        var body = Body(); body["categoryId"] = category; body["optionsConsidered"] = new[] { "  Option A  ", "Option B", "Option A" };
        var id = await Raise(p.Id, body);
        var detail = await (await f.As(TestData.Pm).GetAsync($"/api/v1/decisions/{id}")).Json();
        Assert.Equal(category, detail["metadata"]!.G("categoryId")); Assert.Equal(new[] { "Option A", "Option B", "Option A" }, detail["metadata"]!["optionsConsidered"]!.AsArray().Select(value => value!.GetValue<string>()));
        Assert.Null(detail["metadata"]!["rationale"]); Assert.False(detail["metadata"]!["canEditRationale"]!.GetValue<bool>());
        await (await f.As(TestData.Pm).Patch($"/api/v1/decisions/{id}", new { optionsConsidered = new[] { "One", "Two", "Three", "Four", "Five" } }, Version(id))).Json();
        Assert.Equal(5, f.Db(db => db.Decisions.Single(row => row.Id == id).OptionsConsidered)!.Length);
        foreach (var invalid in new object[] { new { categoryId = Guid.NewGuid() }, new { optionsConsidered = Enumerable.Repeat("Option", 6).ToArray() }, new { optionsConsidered = new[] { " " } }, new { optionsConsidered = new[] { new string('x', 501) } }, new { optionsConsidered = new object[] { "valid", 3 } }, new { subject = "Must not change", rationale = (string?)null } })
        {
            var before = State(); await (await f.As(TestData.Pm).Patch($"/api/v1/decisions/{id}", invalid, Version(id))).Json(400); Assert.Equal(before, State());
        }
        await f.DbAsync(async db => { var c = await db.DecisionCategories.SingleAsync(row => row.Id == category); c.IsActive = false; return await db.SaveChangesAsync(); });
        await (await f.As(TestData.Pm).Patch($"/api/v1/decisions/{id}", new { categoryId = category, optionsConsidered = Array.Empty<string>() }, Version(id))).Json();
        Assert.Empty(f.Db(db => db.Decisions.Single(row => row.Id == id).OptionsConsidered)!);
        var other = await Raise(p.Id); var previous = State();
        await (await f.As(TestData.Pm).Patch($"/api/v1/decisions/{other}", new { categoryId = category }, Version(other))).Json(400); Assert.Equal(previous, State());
        await (await f.As(TestData.Pm).Patch($"/api/v1/decisions/{id}", new { categoryId = (Guid?)null, optionsConsidered = (string[]?)null }, Version(id))).Json();
        Assert.Null(f.Db(db => db.Decisions.Single(row => row.Id == id).OptionsConsidered));
    }

    [Fact]
    public async Task Requester_cannot_rewrite_or_clear_outcome_rationale_and_mixed_refusal_changes_nothing()
    {
        await Flag(true); var p = await d.Project(); var id = await Raise(p.Id);
        await (await f.As(TestData.Alex).Post($"/api/v1/decisions/{id}/transition", new { toStatus = "Decided", decisionText = "Choose alignment A", decisionDate = "2026-11-11", rationale = "Preserves access", rowVersion = Version(id) })).Json();
        foreach (var rationale in new string?[] { "Requester must not rewrite", null })
        {
            var before = State(); await (await f.As(TestData.Marc).Patch($"/api/v1/decisions/{id}", new { subject = "Forbidden mixed change", optionsConsidered = new[] { "Changed" }, rationale }, Version(id))).Json(403); Assert.Equal(before, State());
        }
        await (await f.As(TestData.Marc).Patch($"/api/v1/decisions/{id}", new { optionsConsidered = new[] { "Requester can update context" } }, Version(id))).Json();
        await (await f.As(TestData.Alex).Patch($"/api/v1/decisions/{id}", new { rationale = "Owner correction" }, Version(id))).Json();
        var stale = Version(id); await (await f.As(TestData.Pm).Patch($"/api/v1/decisions/{id}", new { rationale = (string?)null }, stale)).Json();
        var unchanged = State(); await (await f.As(TestData.Pm).Patch($"/api/v1/decisions/{id}", new { rationale = "Stale" }, stale)).Json(409); Assert.Equal(unchanged, State());
        await (await f.As(TestData.Pm).Patch($"/api/v1/decisions/{id}", new { rationale = "Retained before reopen" }, Version(id))).Json();
        await Flag(false);
        await (await f.As(TestData.Pm).Post($"/api/v1/decisions/{id}/transition", new { toStatus = "Pending", reason = "Synthetic reconsideration", rowVersion = Version(id) })).Json();
        Assert.Null(f.Db(db => db.Decisions.Single(row => row.Id == id).Rationale)); Assert.Single(f.Db(db => db.Decisions.Single(row => row.Id == id).OptionsConsidered)!);
    }

    [Fact]
    public async Task Off_omits_stored_metadata_and_blocks_new_fields_without_changing_legacy_outcomes()
    {
        await Flag(false); var p = await d.Project(); var id = await Raise(p.Id);
        var dark = await (await f.As(TestData.Pm).GetAsync($"/api/v1/decisions/{id}")).Json();
        Assert.False(dark.AsObject().ContainsKey("metadata")); Assert.False(dark["decision"]!.AsObject().ContainsKey("metadata"));
        await (await f.As(TestData.Pm).GetAsync($"/api/v1/projects/{p.Id}/decisions/screen")).Json(404);
        var before = State(); var invalid = Body(); invalid["optionsConsidered"] = Array.Empty<string>();
        await (await f.As(TestData.Pm).Post($"/api/v1/projects/{p.Id}/decisions", invalid)).Json(404);
        await (await f.As(TestData.Pm).Patch($"/api/v1/decisions/{id}", new { categoryId = (Guid?)null }, Version(id))).Json(404); Assert.Equal(before, State());
        await Flag(true); await (await f.As(TestData.Pm).Patch($"/api/v1/decisions/{id}", new { optionsConsidered = new[] { "Synthetic A" } }, Version(id))).Json(); await Flag(false);
        var off = await (await f.As(TestData.Pm).GetAsync($"/api/v1/decisions/{id}")).Json(); Assert.False(off.AsObject().ContainsKey("metadata"));
        await (await f.As(TestData.Pm).Post($"/api/v1/decisions/{id}/transition", new { toStatus = "Decided", decisionText = "Legacy outcome", rowVersion = Version(id) })).Json();
        Assert.Equal("Legacy outcome", f.Db(db => db.Decisions.Single(row => row.Id == id).DecisionText)); Assert.Null(f.Db(db => db.Decisions.Single(row => row.Id == id).Rationale));
    }

    [Fact]
    public async Task Metadata_preserves_readonly_archived_and_outcome_date_boundaries_with_zero_partial_effects()
    {
        await Flag(true); var p = await d.Project(); var id = await Raise(p.Id);
        var before = State(); await (await f.As(TestData.Rita).Patch($"/api/v1/decisions/{id}", new { optionsConsidered = new[] { "Readonly" } }, Version(id))).Json(403); Assert.Equal(before, State());
        foreach (var invalid in new object[] { new { toStatus = "Decided", decisionText = "Bad date", decisionDate = "2026-10-31", rowVersion = Version(id) },
            new { toStatus = "Decided", decisionText = "Future", decisionDate = "2026-11-15", rowVersion = Version(id) },
            new { toStatus = "Decided", decisionText = "Too long why", rationale = new string('x', 2001), rowVersion = Version(id) },
            new { toStatus = "Under Review", rationale = "Not an outcome", rowVersion = Version(id) } })
        { before = State(); await (await f.As(TestData.Pm).Post($"/api/v1/decisions/{id}/transition", invalid)).Json(400); Assert.Equal(before, State()); }
        await f.DbAsync(async db => { var project = await db.Projects.SingleAsync(row => row.Id == p.Id); project.Status = "Archived"; return await db.SaveChangesAsync(); });
        before = State(); await (await f.As(TestData.Pm).Patch($"/api/v1/decisions/{id}", new { subject = "Must not change", optionsConsidered = new[] { "Archived" } }, Version(id))).Json(403); Assert.Equal(before, State());
    }

    [Fact]
    public async Task Complete_project_metadata_and_outcome_require_the_canonical_reason_without_a_new_upper_cap()
    {
        await Flag(true); var p = await d.Project(); var id = await Raise(p.Id);
        await f.DbAsync(async db => { var project = await db.Projects.SingleAsync(row => row.Id == p.Id); project.Status = "Complete"; return await db.SaveChangesAsync(); });
        var detail = await (await f.As(TestData.Pm).GetAsync($"/api/v1/decisions/{id}")).Json();
        Assert.Equal("Complete", detail["project"]!.S("status")); Assert.True(detail["metadata"]!["correctionReasonRequired"]!.GetValue<bool>());
        var observed = Version(id); var before = State();
        await (await f.As(TestData.Pm).Patch($"/api/v1/decisions/{id}", new { optionsConsidered = new[] { "Complete correction" } }, observed)).Json(400);
        await (await f.As(TestData.Pm).Patch($"/api/v1/decisions/{id}", new { optionsConsidered = new[] { "Complete correction" }, reason = "four" }, observed)).Json(400);
        Assert.Equal(before, State()); Assert.Equal(observed, Version(id));
        var reason = "Synthetic correction: " + new string('x', 3000); // Check.Reason has minimum5 and no application-level maximum.
        await (await f.As(TestData.Pm).Patch($"/api/v1/decisions/{id}", new { optionsConsidered = new[] { "Complete correction" }, reason }, observed)).Json();
        Assert.Equal("Complete correction", Assert.Single(f.Db(db => db.Decisions.Single(row => row.Id == id).OptionsConsidered)!));
        Assert.True(f.Db(db => db.ActivityLog.Any(log => log.ItemId == id && log.Reason == reason)));
        observed = Version(id); before = State();
        await (await f.As(TestData.Pm).Post($"/api/v1/decisions/{id}/transition", new { toStatus = "Decided", decisionText = "Complete outcome", rationale = "Why this outcome", rowVersion = observed })).Json(400);
        await (await f.As(TestData.Pm).Post($"/api/v1/decisions/{id}/transition", new { toStatus = "Decided", decisionText = "Complete outcome", reason = "four", rowVersion = observed })).Json(400);
        Assert.Equal(before, State()); Assert.Equal(observed, Version(id));
        await (await f.As(TestData.Pm).Post($"/api/v1/decisions/{id}/transition", new { toStatus = "Decided", decisionText = "Complete outcome", rationale = "Why this outcome", reason, rowVersion = observed })).Json();
        Assert.Equal("Decided", f.Db(db => db.Decisions.Single(row => row.Id == id).Status)); Assert.Equal("Why this outcome", f.Db(db => db.Decisions.Single(row => row.Id == id).Rationale));
    }
}
