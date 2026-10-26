using System.IO.Compression;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

/// AC-PLAT-02, AC-PLAT-03, AC-PLAT-04: a seeded fixture's activity rows (task edit, risk edit, open-role cancel), Activity
/// History, item histories, export Parameters sheets and private-record responses, compared with
/// Snapshots/registry/parity.json. That snapshot was written by this same test on 29e38084, the commit before the registry
/// derivation, so a match means the derived audit allow-list, history lookup and name lookups give the same bytes. Only
/// run-specific values are replaced: ids, project numbers and timestamps.
[Collection("api")]
public sealed class RegistryParityTests(HubFactory f)
{
    readonly TestData d = new(f);
    static readonly Regex Guids = new("[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}");
    static readonly Regex Times = new(@"\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(\.\d+)?(Z|[+-]\d{2}:\d{2})?");

    [Fact]
    public async Task Activity_history_and_export_parameters_match_the_pre_registry_snapshot()
    {
        var clock = f.Clock.Now;
        f.Clock.Now = new DateTimeOffset(2026, 9, 14, 13, 0, 0, TimeSpan.Zero);
        await f.As(TestData.Admin).Put("/api/v1/admin/settings/restricted_projects_enabled", new { value = true }).Result.Json();
        try
        {
            var actual = await Capture();
            var file = Path.Combine(Root, "tests/Hub.Tests/Snapshots/registry/parity.json");
            if (Environment.GetEnvironmentVariable("HUB_UPDATE_REGISTRY") == "1")
            {
                File.WriteAllText(file, actual);
                // An update run still fails, so a variable left set cannot hide a change.
                Assert.Fail("Updated tests/Hub.Tests/Snapshots/registry/parity.json; review the diff and rerun without HUB_UPDATE_REGISTRY");
            }
            Assert.Equal(File.ReadAllText(file), actual);
        }
        finally
        {
            f.Clock.Now = clock;
            await f.As(TestData.Admin).Put("/api/v1/admin/settings/restricted_projects_enabled", new { value = false }).Result.Json();
        }
    }

    async Task<string> Capture()
    {
        // Named reference rows, so rows other tests add to the shared database cannot change the output.
        var reference = await d.Ref();
        Guid Named(string list, string name) => reference[list]!.AsArray().First(x => x!["name"]!.GetValue<string>() == name)!.G("id");
        var (client, office) = (Named("clients", "Internal / TBD"), Named("offices", "Head Office"));
        var hidden = await d.Project(tweak: b => { b["name"] = "Parity hidden project"; b["members"] = Array.Empty<object>(); b["clientId"] = client; b["officeId"] = office; });
        var secret = await f.As(TestData.Pm).Post($"/api/v1/projects/{hidden.Id}/deliverables", new { name = "Confidential parity package",
            projectDisciplineId = d.ProjectDiscipline(hidden.Id, "Civil"), deliverableTypeId = await d.DeliverableType() }).Result.Json(201);
        await (await f.As(TestData.Pm).Patch($"/api/v1/projects/{hidden.Id}", new { visibility = "Restricted" }, d.Version(hidden.Id))).Json();

        var p = await d.Project(tweak: b => { b["name"] = "Parity project"; b["clientId"] = client; b["officeId"] = office; });
        var civil = d.ProjectDiscipline(p.Id, "Civil");
        var milestone = await f.As(TestData.Pm).Post($"/api/v1/projects/{p.Id}/milestones", new { name = "Parity submission",
            milestoneType = "Design Submission", date = "2026-09-24" }).Result.Json(201);
        var deliverable = await f.As(TestData.Marc).Post($"/api/v1/projects/{p.Id}/deliverables", new { name = "Parity drawing package",
            projectDisciplineId = civil, deliverableTypeId = await d.DeliverableType() }).Result.Json(201);

        // The three edits AC-PLAT-02 names.
        var task = await d.NewTask(p.Id, TestData.Marc, new { name = "Parity grading task" });
        await (await f.As(TestData.Marc).Patch($"/api/v1/tasks/{task.S("id")}", new { assigneeId = d.User(TestData.Alex), deliverableId = deliverable.G("id"),
            dueDate = "2026-10-01", estimatedHours = 12, priority = "High" }, await d.TaskVersion(task))).Json();
        var risk = await f.As(TestData.Alex).Post($"/api/v1/projects/{p.Id}/risks", new { title = "Parity permit risk", description = "What could happen",
            probability = 2, impact = 3, reviewDate = "2026-09-28", mitigation = "Book the permit meeting early", triggerIndicator = "No reply within two weeks" }).Result.Json(201);
        var riskVersion = await f.DbAsync(db => db.Risks.Where(x => x.Id == risk.G("id")).Select(x => x.RowVersion).FirstAsync());
        await (await f.As(TestData.Pm).Patch($"/api/v1/risks/{risk.S("id")}", new { impact = 1, ownerId = d.User(TestData.Marc), reviewDate = "2026-10-05" }, riskVersion)).Json();
        var role = await f.As(TestData.Pm).Post($"/api/v1/projects/{p.Id}/open-roles", new { roleLabel = "Parity technologist", fromDate = "2026-10-05",
            throughDate = "2026-10-09", plannedHours = 20, projectDisciplineId = civil }).Result.Json(201);
        await f.As(TestData.Pm).Post($"/api/v1/open-roles/{role.S("id")}/cancel", new { rowVersion = role.I("rowVersion"), reason = "Scope dropped" }).Result.Json();

        // Records only their owner (and the PM) may see.
        var entry = await f.As(TestData.Alex).Post("/api/v1/time", new { taskId = task.G("id"), workDate = "2026-09-14", hours = 1m, note = "Parity private effort note" }).Result.Json(201);
        var calendar = await f.As(TestData.Alex).Post("/api/v1/calendar/events", new { projectId = p.Id, type = "Meeting", title = "Parity private calendar event",
            start = "2026-09-21T09:00", end = "2026-09-21T10:00", visibility = "Private" }).Result.Json(201);

        var labels = new Dictionary<string, string>
        {
            [p.Id.ToString()] = "{project}", [hidden.Id.ToString()] = "{hiddenProject}", [secret.S("id")] = "{hiddenDeliverable}", [civil.ToString()] = "{civil}",
            [d.ProjectDiscipline(p.Id, "Electrical").ToString()] = "{electrical}", [deliverable.S("id")] = "{deliverable}", [milestone.S("id")] = "{milestone}",
            [task.S("id")] = "{task}", [risk.S("id")] = "{risk}", [role.S("id")] = "{openRole}", [entry.S("id")] = "{timeEntry}", [calendar.S("id")] = "{calendarEvent}",
        };
        foreach (var u in f.Db(db => db.Users.Where(u => u.Email.EndsWith("@hub.test") && !u.Email.Contains("-")).Select(u => new { u.Id, u.Email }).ToList()))
            labels[u.Id.ToString()] = "{" + u.Email.Split('@')[0] + "}";
        string N(string text)
        {
            text = text.Replace(p.ProjectNumber, "{P}").Replace(hidden.ProjectNumber, "{Q}");
            text = Guids.Replace(text, m => labels.GetValueOrDefault(m.Value.ToLowerInvariant(), "{id}"));
            return Times.Replace(text, "{time}");
        }
        JsonArray Sorted(IEnumerable<JsonNode?> rows) => new([.. rows.Select(r => N(r!.ToJsonString(Json))).Order(StringComparer.Ordinal).Select(s => JsonNode.Parse(s))]);
        async Task<JsonNode> Get(string who, string url)
        {
            var response = await f.As(who).GetAsync(url);
            var body = await response.Content.ReadAsStringAsync();
            var node = new JsonObject { ["status"] = (int)response.StatusCode };
            if (body.Length > 0 && JsonNode.Parse(body) is JsonObject page && page["items"] is JsonArray items) { node["totalCount"] = page["totalCount"]?.DeepClone(); node["items"] = Sorted(items); }
            return node;
        }

        var ids = new Guid?[] { task.G("id"), risk.G("id"), role.G("id") };
        var rows = await f.DbAsync(db => db.ActivityLog.AsNoTracking().Where(a => ids.Contains(a.ItemId)).ToListAsync());
        var document = new JsonObject
        {
            ["activityRows"] = Sorted(rows.Select(a => (JsonNode)new JsonObject
            {
                ["itemType"] = a.ItemType, ["itemId"] = a.ItemId?.ToString(), ["projectId"] = a.ProjectId?.ToString(), ["projectDisciplineId"] = a.ProjectDisciplineId?.ToString(),
                ["action"] = a.Action, ["actorType"] = a.ActorType, ["actorUserId"] = a.ActorUserId?.ToString(), ["itemKey"] = a.ItemKey, ["itemName"] = a.ItemName,
                ["categories"] = new JsonArray([.. a.Categories.Select(c => (JsonNode)c)]), ["changes"] = JsonNode.Parse(a.Changes), ["reason"] = a.Reason, ["source"] = a.Source,
                ["snapshot"] = a.Snapshot is null ? null : JsonNode.Parse(a.Snapshot),
            })),
            ["projectHistory"] = await Get(TestData.Pm, $"/api/v1/projects/{p.Id}/activity?pageSize=200"),
            ["itemHistory"] = new JsonObject
            {
                ["Task"] = await Get(TestData.Pm, $"/api/v1/items/Task/{task.S("id")}/activity"),
                ["Risk"] = await Get(TestData.Pm, $"/api/v1/items/Risk/{risk.S("id")}/activity"),
                ["OpenRole"] = await Get(TestData.Pm, $"/api/v1/items/OpenRole/{role.S("id")}/activity"),
                ["Deliverable"] = await Get(TestData.Pm, $"/api/v1/items/Deliverable/{deliverable.S("id")}/activity"),
                ["Milestone"] = await Get(TestData.Pm, $"/api/v1/items/Milestone/{milestone.S("id")}/activity"),
                ["Project"] = await Get(TestData.Pm, $"/api/v1/items/Project/{p.Id}/activity"),
                ["TimeEntry"] = await Get(TestData.Pm, $"/api/v1/items/TimeEntry/{entry.S("id")}/activity"),
                ["CalendarEvent"] = await Get(TestData.Pm, $"/api/v1/items/CalendarEvent/{calendar.S("id")}/activity"),
            },
            ["privacy"] = new JsonObject
            {
                ["dianeTask"] = await Get(TestData.Diane, $"/api/v1/items/Task/{task.S("id")}/activity"),
                ["dianePrivate"] = await Get(TestData.Diane, $"/api/v1/projects/{p.Id}/activity?itemType=TimeEntry,CalendarEvent"),
                ["alexPrivate"] = await Get(TestData.Alex, $"/api/v1/projects/{p.Id}/activity?itemType=TimeEntry,CalendarEvent"),
                ["pmPrivate"] = await Get(TestData.Pm, $"/api/v1/projects/{p.Id}/activity?itemType=TimeEntry,CalendarEvent"),
                ["dianeExport"] = await Csv(TestData.Diane, $"/api/v1/projects/{p.Id}/activity/export?format=csv&itemType=TimeEntry,CalendarEvent", N),
            },
        };
        // Exports log themselves, so they run after the history reads.
        var filters = $"assigneeId={d.User(TestData.Alex)}&disciplineId={civil}&deliverableId={deliverable.S("id")},{secret.S("id")}&milestoneId={milestone.S("id")}&q=parity";
        document["parameters"] = new JsonObject
        {
            ["alexTasks"] = N(await Parameters(TestData.Alex, $"/api/v1/projects/{p.Id}/tasks/export?format=xlsx&{filters}")),
            ["pmTasks"] = N(await Parameters(TestData.Pm, $"/api/v1/projects/{p.Id}/tasks/export?format=xlsx&{filters}")),
            ["alexProjects"] = N(await Parameters(TestData.Alex, $"/api/v1/projects/export?format=xlsx&clientId={client}&officeId={office}&pmId={d.User(TestData.Pm)}")),
            ["alexActivity"] = N(await Parameters(TestData.Alex, $"/api/v1/projects/{p.Id}/activity/export?format=xlsx&actorId={d.User(TestData.Marc)}&disciplineId={civil}")),
        };
        return document.ToJsonString(new JsonSerializerOptions(Json) { WriteIndented = true }) + "\n";
    }

    static readonly JsonSerializerOptions Json = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    async Task<string> Parameters(string who, string url)
    {
        var response = await f.As(who).GetAsync(url);
        response.EnsureSuccessStatusCode();
        using var zip = new ZipArchive(new MemoryStream(await response.Content.ReadAsByteArrayAsync()));
        using var reader = new StreamReader(zip.GetEntry("xl/worksheets/sheet2.xml")!.Open());
        return await reader.ReadToEndAsync();
    }

    async Task<JsonNode> Csv(string who, string url, Func<string, string> normalise)
    {
        var response = await f.As(who).GetAsync(url);
        response.EnsureSuccessStatusCode();
        var lines = normalise(Encoding.UTF8.GetString(await response.Content.ReadAsByteArrayAsync())).Split("\r\n");
        return new JsonArray([lines[0], .. lines[1..].Order(StringComparer.Ordinal).Select(l => (JsonNode)l)]);
    }

    // Not ContractTests.Root: that class does not exist on 29e38084, where this file must also compile to rewrite the snapshot.
    static string Root
    {
        get
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Hub.slnx"))) dir = dir.Parent;
            return dir?.FullName ?? throw new InvalidOperationException("Repository root not found");
        }
    }
}
