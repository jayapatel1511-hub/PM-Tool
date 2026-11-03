using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Hub.Api.Data;
using Hub.Api.Features;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

/// FR-SCR-84 / FR-DLV-08: read-side projection only; immutable source audit strings stay intact.
public sealed class ActivityScreenFlagProjectionTests
{
    sealed record AuditBytes(Guid Id, string Changes, string? Snapshot, string? ItemKey, string? ItemName, Guid? ChainId, long? ChainSeq, string? PrevHash, string? RowHash);
    sealed record ProjectionCase(string Type, string Flag, string LegacyField, Dictionary<string, object?> Fields);
    static readonly (bool Register, bool People)[] States = [(true, true), (false, true), (true, false), (false, false), (true, true)];
    static async Task Flag(HubFactory f, string flag, bool on) =>
        (await f.As(TestData.Admin).Put($"/api/v1/admin/settings/flag.{flag}", new { value = on })).EnsureSuccessStatusCode();
    static async Task Flags(HubFactory f, bool register, bool people)
    {
        await Flag(f, "register_screens", register); await Flag(f, "people_screens", people);
    }
    static ActivityLog Log(HubFactory f, string type, Dictionary<string, object?> fields, string action = "Updated", Guid? project = null, Guid? item = null) => new()
    {
        OccurredAt = f.Clock.GetUtcNow(), ActorType = "System", ProjectId = project, ItemType = type,
        ItemId = item ?? Guid.NewGuid(), ItemKey = "SYN-AUDIT", ItemName = "Synthetic audit event", Action = action, Categories = ["decision"],
        Changes = JsonSerializer.Serialize(fields.Select(pair => new { field = pair.Key, old = (object?)null, @new = pair.Value })),
        Snapshot = action == "Deleted" ? JsonSerializer.Serialize(fields) : null,
    };
    static AuditBytes[] Raw(HubFactory f, Guid[] ids) => f.Db(db => db.ActivityLog.AsNoTracking().Where(row => ids.Contains(row.Id)).OrderBy(row => row.Id)
        .Select(row => new AuditBytes(row.Id, row.Changes, row.Snapshot, row.ItemKey, row.ItemName, row.ChainId, row.ChainSeq, row.PrevHash, row.RowHash)).ToArray());
    static Task<List<ActivityEndpoints.ActivityEntry>> Render(HubFactory f, Guid[] ids) => f.DbAsync(async db =>
        await ActivityEndpoints.Render(db, await db.ActivityLog.AsNoTracking().Where(row => ids.Contains(row.Id)).OrderBy(row => row.Id).ToListAsync()));
    static async Task Store(HubFactory f, ActivityLog[] rows) =>
        await f.DbAsync(async db => { db.ActivityLog.AddRange(rows); return await db.SaveChangesAsync(); });
    static string[] Ordered(IEnumerable<string> fields) => fields.OrderBy(field => field, StringComparer.Ordinal).ToArray();

    [Fact]
    public void Complete_inventory_preserves_exact_field_and_whole_event_ownership_for_all_independent_flag_combinations()
    {
        // Expected ownership comes from the reviewed registry-diff inventory, not the implementation's private rules.
        (string Type, string Flag, string[] Fields)[] expected =
        [
            ("User", "capacity_screens", ["PersonType", "PersonStartDate", "PersonEndDate"]),
            ("Decision", "register_screens", ["CategoryId", "OptionsConsidered", "Rationale"]),
            ("Deliverable", "register_screens", ["ClientDrawingNumber"]),
            ("DocumentLink", "people_screens", ["SourceSystem", "StableSourceId", "DeclaredRevision", "ReferenceRegisteredBy", "ReferenceRegisteredAt", "ReferenceDeliverableId"]),
            ("FindingEvent", "register_screens", ["ResponseOutcome"]), ("Issue", "register_screens", ["RootCauseCategoryId", "RootCauseLabelSnapshot"]),
            ("Project", "register_screens", ["ClientDrawingNumberPattern"]), ("Project", "visual_system", ["IdentityColour"]),
            ("Member", "people_screens", ["ResponsibilityNote"]), ("Template", "people_screens", ["CopiedFromTemplateId"]),
            ("ReviewFinding", "register_screens", ["ResponseOutcome"]), ("Risk", "register_screens", ["TemplateRiskId"]),
        ];
        (string Type, string? Key, string Flag)[] wholeEvents =
        [
            ("ReferenceData", "DecisionCategory", "register_screens"), ("ReferenceData", "IssueRootCauseCategory", "register_screens"),
            ("ReferenceData", "IssuePurpose", "register_screens"), ("ReferenceData", "IssueReturnCode", "register_screens"),
            ("ReferenceData", "CertificationTag", "capacity_screens"), ("UserCertification", null, "capacity_screens"),
        ];
        Assert.Equal(22, expected.Sum(group => group.Fields.Length)); Assert.Equal(11, expected.Select(group => group.Type).Distinct().Count());
        var flags = new[] { "register_screens", "people_screens", "capacity_screens", "visual_system" };
        for (var bits = 0; bits < 16; bits++)
        {
            var state = flags.Select((key, index) => (Key: key, On: (bits & (1 << index)) != 0)).ToDictionary(pair => pair.Key, pair => pair.On);
            var projection = new ActivityScreenFields(new OrgSettings { Flags = state });
            foreach (var type in expected.GroupBy(group => group.Type))
            {
                var fields = type.SelectMany(group => group.Fields.Select(field => (Field: field, On: state[group.Flag]))).ToArray();
                var snapshot = JsonSerializer.SerializeToElement(fields.ToDictionary(field => field.Field, _ => (object?)null));
                var masked = projection.MaskSnapshot(type.Key, snapshot);
                Assert.Equal(Ordered(fields.Where(field => field.On).Select(field => field.Field)), Ordered(masked.EnumerateObject().Select(field => field.Name)));
                foreach (var field in fields)
                {
                    Assert.Equal(!field.On, projection.Hidden(type.Key, field.Field));
                    Assert.Equal(!field.On, projection.Hidden(type.Key, field.Field.ToLowerInvariant()));
                }
            }
            foreach (var (type, fields) in new[] { ("Project", new[] { "StatusNote", "StatusNoteBy", "StatusNoteAt" }),
                ("User", new[] { "HomeDisciplineId", "WeeklyCapacityHours", "IsTemplateEditor" }), ("ReferenceData", new[] { "Name", "IsActive", "SortOrder" }) })
            {
                foreach (var field in fields) Assert.False(projection.Hidden(type, field));
                var snapshot = JsonSerializer.SerializeToElement(fields.ToDictionary(field => field, _ => (object?)null));
                Assert.Equal(Ordered(fields), Ordered(projection.MaskSnapshot(type, snapshot).EnumerateObject().Select(field => field.Name)));
            }
            foreach (var entry in wholeEvents)
                foreach (var key in entry.Key is null ? new[] { Guid.NewGuid().ToString(), Guid.NewGuid().ToString() } : new[] { entry.Key })
                {
                    var row = new ActivityLog { ItemType = entry.Type, ItemKey = key, ItemName = "Synthetic whole-event name" };
                    Assert.Equal(state[entry.Flag], projection.Visible(row));
                    Assert.Equal(state[entry.Flag] ? 1 : 0, projection.Visible(new[] { row }.AsQueryable()).Count());
                }
            Assert.True(projection.Visible(new ActivityLog { ItemType = "ReferenceData", ItemKey = "ProjectType", ItemName = "DecisionCategory" }));
            Assert.True(projection.Visible(new ActivityLog { ItemType = "ReferenceData", ItemKey = null, ItemName = "CertificationTag" }));
        }
    }

    [Fact]
    public async Task Stored_mixed_and_new_only_updates_and_null_deletion_keys_follow_independent_flags_and_restore_without_rewriting_audit()
    {
        using var f = new HubFactory(); await Flags(f, true, true);
        var adminId = new TestData(f).User(TestData.Admin);
        var category = new DecisionCategory { Name = "SYNTHETIC hidden decision category" };
        var cause = new IssueRootCauseCategory { Name = "SYNTHETIC hidden cause category" };
        var template = new ProjectTemplate { FamilyId = Guid.NewGuid(), Name = "SYNTHETIC hidden source template" };
        await f.DbAsync(async db => { db.AddRange(category, cause, template); return await db.SaveChangesAsync(); });
        ProjectionCase[] cases =
        [
            new("Decision", "register_screens", "Subject", new() { ["CategoryId"] = category.Id, ["OptionsConsidered"] = new[] { "SYNTHETIC hidden option" }, ["Rationale"] = "SYNTHETIC hidden rationale" }),
            new("Issue", "register_screens", "Title", new() { ["RootCauseCategoryId"] = cause.Id, ["RootCauseLabelSnapshot"] = "SYNTHETIC hidden cause snapshot" }),
            new("Risk", "register_screens", "Title", new() { ["TemplateRiskId"] = Guid.NewGuid() }),
            new("Template", "people_screens", "Name", new() { ["CopiedFromTemplateId"] = template.Id }),
            new("DocumentLink", "people_screens", "Url", new() { ["SourceSystem"] = "SYNTHETIC hidden source", ["StableSourceId"] = "SYNTHETIC hidden stable ID",
                ["DeclaredRevision"] = "SYNTHETIC hidden revision", ["ReferenceRegisteredBy"] = adminId, ["ReferenceRegisteredAt"] = f.Clock.GetUtcNow(), ["ReferenceDeliverableId"] = Guid.NewGuid() }),
        ];
        var rows = cases.SelectMany(c =>
        {
            var mixed = new Dictionary<string, object?>(c.Fields) { [c.LegacyField] = "SYNTHETIC retained legacy value" };
            var deleted = c.Fields.ToDictionary(pair => pair.Key, _ => (object?)null);
            deleted[c.LegacyField] = "SYNTHETIC retained deletion value";
            return new[] { Log(f, c.Type, mixed), Log(f, c.Type, c.Fields), Log(f, c.Type, deleted, "Deleted") };
        }).ToArray();
        await Store(f, rows); var ids = rows.Select(row => row.Id).ToArray(); var original = Raw(f, ids);
        var originalVisible = JsonSerializer.Serialize(await Render(f, ids));
        foreach (var state in States)
        {
            await Flags(f, state.Register, state.People); var rendered = await Render(f, ids);
            Assert.Equal(rows.Length, rendered.Count); // A new-only update of a legacy type remains an event when changes are hidden.
            for (var i = 0; i < cases.Length; i++)
            {
                var c = cases[i]; var visible = c.Flag == "register_screens" ? state.Register : state.People;
                for (var kind = 0; kind < 3; kind++)
                {
                    var source = rows[i * 3 + kind]; var entry = rendered.Single(row => row.Id == source.Id);
                    var expected = (kind == 1 ? Array.Empty<string>() : new[] { c.LegacyField }).Concat(visible ? c.Fields.Keys : Enumerable.Empty<string>());
                    Assert.Equal(Ordered(expected), Ordered(entry.Changes.Select(change => change.Field)));
                    if (kind == 2)
                    {
                        Assert.True(entry.Snapshot.HasValue); var snapshot = entry.Snapshot.Value;
                        Assert.Equal(Ordered(expected), Ordered(snapshot.EnumerateObject().Select(field => field.Name)));
                        Assert.NotNull(entry.SnapshotLabels); Assert.Equal(Ordered(expected), Ordered(entry.SnapshotLabels.Keys));
                        foreach (var field in c.Fields.Keys) Assert.Equal(visible, snapshot.TryGetProperty(field, out _));
                        Assert.Contains("SYNTHETIC retained deletion value", entry.Summary);
                    }
                    else if (kind == 0) Assert.Contains("SYNTHETIC retained legacy value", entry.Summary);
                    else if (!visible) { Assert.Empty(entry.Changes); Assert.Empty(entry.Summary); }
                    if (!visible)
                    {
                        var json = JsonSerializer.Serialize(entry);
                        foreach (var field in c.Fields.Keys) Assert.DoesNotContain(field, json);
                        Assert.DoesNotContain("SYNTHETIC hidden", json);
                        foreach (var value in c.Fields.Values.OfType<Guid>()) Assert.DoesNotContain(value.ToString(), json);
                    }
                }
            }
            Assert.Equal(original, Raw(f, ids));
        }
        Assert.Equal(originalVisible, JsonSerializer.Serialize(await Render(f, ids)));
    }

    [Fact]
    public async Task Real_project_item_admin_report_and_csv_reads_mask_then_restore_the_same_stored_event()
    {
        using var f = new HubFactory(); var d = new TestData(f); await Flags(f, true, true); var project = await d.Project();
        var decision = new Decision { ProjectId = project.Id, Key = "D-SYN-AUDIT", Seq = 99, Subject = "Synthetic history anchor", OwnerUserId = d.User(TestData.Pm), RequestedById = d.User(TestData.Pm),
            DateRequested = new(2026, 9, 14), RequiredByDate = new(2026, 10, 1), OriginalRequiredByDate = new(2026, 10, 1) };
        await f.DbAsync(async db => { db.Decisions.Add(decision); return await db.SaveChangesAsync(); });
        const string legacy = "SYNTHETIC retained HTTP value", secret = "SYNTHETIC hidden HTTP rationale";
        var row = Log(f, "Decision", new() { ["Subject"] = legacy, ["Rationale"] = secret }, project: project.Id, item: decision.Id);
        row.ItemKey = decision.Key; await Store(f, [row]); var original = Raw(f, [row.Id]);
        foreach (var on in new[] { true, false, true })
        {
            await Flag(f, "register_screens", on);
            var paths = new[] { $"/api/v1/projects/{project.Id}/activity?itemType=Decision&pageSize=200", $"/api/v1/items/Decision/{decision.Id}/activity",
                "/api/v1/admin/activity?itemType=Decision&category=decision&pageSize=200", $"/api/v1/reports/project-activity-log?projectId={project.Id}&category=decision" };
            foreach (var path in paths)
            {
                var client = f.As(path.Contains("/admin/") ? TestData.Admin : TestData.Pm); var page = await (await client.GetAsync(path)).Json();
                var items = (page[path.Contains("/reports/") ? "rows" : "items"]!).AsArray(); var entry = items.Single(value => value!.G("id") == row.Id)!;
                Assert.Equal(Ordered(on ? new[] { "Subject", "Rationale" } : new[] { "Subject" }), Ordered(entry["changes"]!.AsArray().Select(value => value!.S("field"))));
                Assert.Contains(legacy, entry.S("summary")); Assert.Equal(on, entry.ToJsonString().Contains(secret));
                Assert.Equal(row.Id, entry.G("id")); Assert.Equal("Updated", entry.S("action"));
            }
            foreach (var path in new[] { $"/api/v1/projects/{project.Id}/activity/export?format=csv&itemType=Decision",
                $"/api/v1/reports/project-activity-log/export?projectId={project.Id}&category=decision&format=csv" })
            {
                var response = await f.As(TestData.Pm).GetAsync(path); response.EnsureSuccessStatusCode();
                Assert.Equal("text/csv", response.Content.Headers.ContentType!.MediaType);
                var bytes = await response.Content.ReadAsByteArrayAsync(); Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes[..3]);
                var csv = Encoding.UTF8.GetString(bytes[3..]); Assert.Contains(legacy, csv); Assert.Equal(on, csv.Contains(secret));
                if (path.Contains("/activity/export")) Assert.StartsWith("Timestamp,Actor,Action,Item,Change,Reason,Source", csv);
            }
            Assert.Equal(original, Raw(f, [row.Id])); // Export events and flag-setting audits are outside this immutable target cohort.
        }
        Assert.Equal(HttpStatusCode.Forbidden, (await f.As(TestData.Alex).GetAsync("/api/v1/admin/activity")).StatusCode);
    }

    [Fact]
    public async Task Admin_catalogue_events_use_actual_trusted_keys_and_are_excluded_before_count_and_page_while_ordinary_reference_names_remain()
    {
        using var f = new HubFactory(); f.Clock.SetDate(2026, 11, 2); await Flags(f, true, true); var admin = f.As(TestData.Admin);
        var ordinary = await (await admin.Post("/api/v1/admin/projectTypes", new { name = "DecisionCategory" })).Json(201);
        f.Clock.Now = f.Clock.Now.AddMinutes(1);
        var category = await (await admin.Post("/api/v1/admin/decisionCategories", new { name = "SYNTHETIC hidden catalogue decision" })).Json(201);
        f.Clock.Now = f.Clock.Now.AddMinutes(1);
        var cause = await (await admin.Post("/api/v1/admin/issueRootCauses", new { name = "SYNTHETIC hidden catalogue cause" })).Json(201);
        var itemIds = new[] { ordinary.G("id"), category.G("id"), cause.G("id") };
        var audit = f.Db(db => db.ActivityLog.AsNoTracking().Where(row => row.ItemId != null && itemIds.Contains(row.ItemId.Value) && row.Action == "Created").ToArray());
        Assert.Equal(3, audit.Length); Assert.All(audit, row => Assert.Equal("ReferenceData", row.ItemType));
        Assert.Equal(nameof(ProjectType), audit.Single(row => row.ItemId == ordinary.G("id")).ItemKey);
        Assert.Equal(nameof(DecisionCategory), audit.Single(row => row.ItemId == category.G("id")).ItemKey);
        Assert.Equal(nameof(IssueRootCauseCategory), audit.Single(row => row.ItemId == cause.G("id")).ItemKey);
        var ids = audit.Select(row => row.Id).ToArray(); var original = Raw(f, ids); var actorId = new TestData(f).User(TestData.Admin);
        var query = $"/api/v1/admin/activity?itemType=ReferenceData&actorId={actorId}&from=2026-11-02&to=2026-11-02&pageSize=1";
        foreach (var state in States)
        {
            await Flags(f, state.Register, state.People);
            var expected = state.Register ? new[] { cause.G("id"), category.G("id"), ordinary.G("id") } : new[] { ordinary.G("id") };
            for (var page = 1; page <= expected.Length + 1; page++)
            {
                var result = await (await admin.GetAsync(query + $"&page={page}")).Json(); Assert.Equal(expected.Length, result.I("totalCount"));
                var items = result["items"]!.AsArray(); Assert.Equal(page <= expected.Length ? 1 : 0, items.Count);
                if (page <= expected.Length) Assert.Equal(expected[page - 1], items.Single()!.G("itemId"));
                if (!state.Register) Assert.DoesNotContain("SYNTHETIC hidden catalogue", result.ToJsonString());
            }
            var rendered = await Render(f, ids); Assert.Equal(expected.Length, rendered.Count);
            Assert.Equal(Ordered(expected.Select(id => id.ToString())), Ordered(rendered.Select(row => row.ItemId!.Value.ToString())));
            Assert.Contains(rendered, row => row.ItemId == ordinary.G("id") && row.ItemName == "DecisionCategory");
            Assert.Equal(original, Raw(f, ids));
        }
    }

    [Fact]
    public async Task Existing_sensitive_notes_stay_redacted_and_legacy_template_editor_and_capacity_fields_are_not_gated()
    {
        using var f = new HubFactory(); await Flags(f, true, true);
        var project = Log(f, "Project", new() { ["Name"] = "SYNTHETIC legacy project", ["InternalNotes"] = "SYNTHETIC private note" }, "Deleted");
        var user = Log(f, "User", new() { ["IsTemplateEditor"] = true, ["WeeklyCapacityHours"] = 0 });
        await Store(f, [project, user]); var ids = new[] { project.Id, user.Id }; var original = Raw(f, ids);
        foreach (var on in new[] { true, false, true })
        {
            await Flags(f, on, on); var rendered = await Render(f, ids); var entry = rendered.Single(row => row.Id == project.Id);
            Assert.DoesNotContain("SYNTHETIC private note", JsonSerializer.Serialize(entry));
            Assert.Contains(entry.Changes, change => change.Field == "InternalNotes" && change.Old is null && change.New is null);
            Assert.False(entry.Snapshot!.Value.TryGetProperty("InternalNotes", out _)); Assert.False(entry.SnapshotLabels!.ContainsKey("InternalNotes"));
            var userEntry = rendered.Single(row => row.Id == user.Id);
            Assert.Equal(new[] { "IsTemplateEditor", "WeeklyCapacityHours" }, Ordered(userEntry.Changes.Select(change => change.Field)));
            Assert.True(userEntry.Changes.Single(change => change.Field == "IsTemplateEditor").New!.Value.GetBoolean());
            Assert.Equal(0, userEntry.Changes.Single(change => change.Field == "WeeklyCapacityHours").New!.Value.GetInt32());
            Assert.Equal(original, Raw(f, ids));
        }
    }
}
