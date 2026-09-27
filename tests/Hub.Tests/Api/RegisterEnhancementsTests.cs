using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

/// Packet 013: the client's open-decision export (US1), linking a decision to many items (US2), the decision log (US3)
/// and every issue of a deliverable (US4).
[Collection("api")]
public sealed class RegisterEnhancementsTests(HubFactory f)
{
    readonly TestData d = new(f);
    Guid U(string email) => d.User(email);

    Task<JsonNode> Party(Guid projectId, string name, bool client, string? notes = null) =>
        f.As(TestData.Pm).Post($"/api/v1/projects/{projectId}/external-parties", new { name, isClient = client, notes }).Result.Json(201);

    Task<JsonNode> Raise(Guid projectId, string subject, string requiredBy, Guid? owner = null, Guid? external = null) =>
        f.As(TestData.Pm).Post($"/api/v1/projects/{projectId}/decisions", new
        {
            subject, description = $"Choose for {subject}", ownerUserId = owner, ownerExternalPartyId = external, requiredByDate = requiredBy,
            impactLevel = "High", impactDescription = "Drawings wait",
        }).Result.Json(201);

    async Task<JsonNode> Move(JsonNode dec, object body, int expect = 200)
    {
        var dict = new Dictionary<string, object?> { ["rowVersion"] = await f.DbAsync(db => db.Decisions.Where(x => x.Id == dec.G("id")).Select(x => x.RowVersion).FirstAsync()) };
        foreach (var p in body.GetType().GetProperties()) dict[p.Name] = p.GetValue(body);
        return await f.As(TestData.Pm).Post($"/api/v1/decisions/{dec.S("id")}/transition", dict).Result.Json(expect);
    }

    [Fact]
    public async Task The_client_export_lists_only_their_open_decisions_overdue_first_and_nothing_internal() // US1, FR-001
    {
        var p = await d.Project();
        var client = await Party(p.Id, "City of Hamilton", client: true, notes: "Internal: slow to answer");
        var utility = await Party(p.Id, "Hydro One", client: false);
        await Raise(p.Id, "Pavement structure", "2026-09-30", external: client.G("id"));
        await Raise(p.Id, "Lane closures", "2026-09-10", external: client.G("id")); // overdue on 2026-09-14
        await Raise(p.Id, "Tree removals", "2026-09-20", external: client.G("id"));
        await Raise(p.Id, "Pole relocation", "2026-09-25", external: utility.G("id"));
        await Raise(p.Id, "Drawing standard", "2026-09-18", owner: U(TestData.Marc));
        var decided = await Raise(p.Id, "Survey limits", "2026-09-15", external: client.G("id"));
        await Move(decided, new { toStatus = "Decided", decisionText = "Limits as shown", decisionDate = "2026-09-14" });

        var res = await f.As(TestData.Alex).GetAsync($"/api/v1/projects/{p.Id}/decisions/client-export?format=csv");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var text = Encoding.UTF8.GetString((await res.Content.ReadAsByteArrayAsync())[3..]);
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(l => l.TrimEnd('\r')).ToList();
        Assert.StartsWith("Key,Subject,What must be decided,Required by,Days until or overdue,Impact,Status,Owner", lines[0]);
        var rows = lines.Skip(1).Take(3).ToList();
        Assert.Contains("Lane closures", rows[0]); // overdue first
        Assert.Contains("4 days overdue", rows[0]);
        Assert.Contains("Tree removals", rows[1]);
        Assert.Contains("In 6 days", rows[1]);
        Assert.Contains("Pavement structure", rows[2]);
        Assert.Contains("High: Drawings wait", rows[2]);
        Assert.DoesNotContain("Pole relocation", text);
        Assert.DoesNotContain("Drawing standard", text);
        Assert.DoesNotContain("Survey limits", text); // decided: not open
        Assert.DoesNotContain("slow to answer", text); // no internal notes

        var one = await f.As(TestData.Pm).GetAsync($"/api/v1/projects/{p.Id}/decisions/client-export?format=csv&partyId={utility.S("id")}");
        Assert.Contains("Pole relocation", Encoding.UTF8.GetString(await one.Content.ReadAsByteArrayAsync()));
        var empty = await d.Project();
        var none = await f.As(TestData.Pm).GetAsync($"/api/v1/projects/{empty.Id}/decisions/client-export?format=csv");
        Assert.Contains("nothing to export", (await none.Json(422)).S("detail")); // says so instead of an empty file
    }

    [Fact]
    public async Task One_action_links_a_decision_to_many_tasks_and_reports_what_it_skipped() // US2, FR-002, SC-002
    {
        var p = await d.Project();
        var dec = await Raise(p.Id, "Approve 85% package", "2026-10-15", owner: U(TestData.Marc));
        var civil = new List<Guid>();
        for (var i = 0; i < 12; i++) civil.Add((await d.NewTask(p.Id, TestData.Marc, new { name = $"Civil sheet {i + 1}" })).G("id"));
        var electrical = (await d.NewTask(p.Id, TestData.Omar, new { name = "Lighting layout" }, discipline: "Electrical")).G("id");
        var other = await d.Project();
        var elsewhere = (await d.NewTask(other.Id, TestData.Marc)).G("id");

        var r = await f.As(TestData.Marc).Post($"/api/v1/decisions/{dec.S("id")}/links/bulk", new
        {
            targetType = "Task", targetIds = civil.Append(electrical).Append(elsewhere).ToArray(), relation = "blocked_by_decision",
        }).Result.Json();
        Assert.Equal(12, r["linked"]!.AsArray().Count);
        var skipped = r["skipped"]!.AsArray().Select(x => x!.G("id")).ToHashSet();
        Assert.Equal(new[] { electrical, elsewhere }.ToHashSet(), skipped); // Marc leads Civil only; the other project's task is not found
        Assert.Equal(12, await f.DbAsync(db => db.ActivityLog.CountAsync(a => a.ProjectId == p.Id && a.Action == "Linked")));
        await f.Evaluate(p.Id);
        var row = (await f.As(TestData.Pm).GetAsync($"/api/v1/projects/{p.Id}/decisions").Result.Json()).AsArray().Single()!;
        Assert.Equal(12, row["blockingTaskIds"]!.AsArray().Count);

        var again = await f.As(TestData.Marc).Post($"/api/v1/decisions/{dec.S("id")}/links/bulk", new { targetType = "Task", targetIds = civil.Take(2).ToArray() }).Result.Json();
        Assert.Equal((0, 2), (again["linked"]!.AsArray().Count, again["skipped"]!.AsArray().Count)); // already linked
        Assert.Equal(HttpStatusCode.Forbidden, (await f.As(TestData.Alex).Post($"/api/v1/decisions/{dec.S("id")}/links/bulk", new { targetType = "Task", targetIds = civil.ToArray() })).StatusCode);
    }

    [Fact]
    public async Task The_decision_log_shows_each_outcome_newest_first_with_text_or_reason() // US3, FR-003
    {
        var p = await d.Project();
        var client = await Party(p.Id, "City", client: true);
        var a = await Raise(p.Id, "Pavement structure", "2026-09-30", external: client.G("id"));
        var b = await Raise(p.Id, "Lane closures", "2026-09-20", owner: U(TestData.Marc));
        var c = await Raise(p.Id, "Night works", "2026-09-22", owner: U(TestData.Marc));
        var saved = f.Clock.Now;
        try
        {
            await Move(b, new { toStatus = "Deferred", newRequiredBy = "2026-10-05", reason = "Waiting for the traffic study" });
            f.Clock.Now = saved.AddMinutes(1);
            await Move(a, new { toStatus = "Decided", decisionText = "450 mm granular base", decisionDate = "2026-09-14" });
            f.Clock.Now = saved.AddMinutes(2);
            await Move(c, new { toStatus = "Cancelled", reason = "Scope removed by the client" });
        }
        finally { f.Clock.Now = saved; }

        var log = (await f.As(TestData.Alex).GetAsync($"/api/v1/projects/{p.Id}/decision-log").Result.Json()).AsArray();
        Assert.Equal(new[] { "Cancelled", "Decided", "Deferred" }, log.Select(x => x!.S("kind")));
        Assert.Equal(("Night works", "Scope removed by the client"), (log[0]!.S("subject"), log[0]!.S("text")));
        Assert.Equal(("450 mm granular base", "2026-09-14", "City", "Priya Nair"), (log[1]!.S("text"), log[1]!.S("decisionDate"), log[1]!.S("owner"), log[1]!.S("recordedBy")));
        Assert.Equal(("Waiting for the traffic study", "2026-10-05"), (log[2]!.S("text"), log[2]!.S("newRequiredBy")));
    }

    [Fact]
    public async Task Every_issue_of_a_deliverable_is_kept_and_the_latest_shows_on_it() // US4, FR-004, SC-003
    {
        var p = await d.Project();
        var marc = f.As(TestData.Marc);
        var del = await f.As(TestData.Pm).Post($"/api/v1/projects/{p.Id}/deliverables", new
        {
            name = "85% Civil Drawing Package", projectDisciplineId = d.ProjectDiscipline(p.Id, "Civil"), deliverableTypeId = await d.DeliverableType(), requiresReview = false, ownerId = U(TestData.Marc),
        }).Result.Json(201);
        var id = del.S("id");
        Task<int> V() => f.DbAsync(db => db.Deliverables.Where(x => x.Id == del.G("id")).Select(x => x.RowVersion).FirstAsync());
        async Task Step(string to, object? extra = null)
        {
            var body = new Dictionary<string, object?> { ["toStatus"] = to, ["rowVersion"] = await V() };
            if (extra is not null) foreach (var pp in extra.GetType().GetProperties()) body[pp.Name] = pp.GetValue(extra);
            (await marc.Post($"/api/v1/deliverables/{id}/transition", body)).EnsureSuccessStatusCode();
        }
        await Step("In Progress");
        await Step("Ready to Issue");
        await marc.Post($"/api/v1/deliverables/{id}/issue", new { issuedDate = "2026-09-10", revision = "Rev A", issuedTo = "City of Hamilton", transmittalUrl = "https://contoso.sharepoint.com/t/001", note = "For comment", rowVersion = await V() }).Result.Json();
        await Step("Revision Required", new { comment = "Client comments received" });
        await Step("In Progress");
        await Step("Ready to Issue");
        await marc.Post($"/api/v1/deliverables/{id}/issue", new { issuedDate = "2026-09-14", revision = "Rev B", issuedTo = "City of Hamilton", transmittalUrl = "https://contoso.sharepoint.com/t/002", rowVersion = await V() }).Result.Json();

        var detail = await f.As(TestData.Alex).GetAsync($"/api/v1/deliverables/{id}").Result.Json();
        var issues = detail["issues"]!.AsArray();
        Assert.Equal(new[] { "Rev B", "Rev A" }, issues.Select(x => x!.S("revision")));
        Assert.Equal(("2026-09-10", "City of Hamilton", "https://contoso.sharepoint.com/t/001", "For comment", "Marc Dubois"),
            (issues[1]!.S("issuedDate"), issues[1]!.S("issuedTo"), issues[1]!.S("transmittalUrl"), issues[1]!.S("note"), issues[1]!.S("issuedBy")));
        Assert.Equal(("Rev B", "2026-09-14"), (detail["deliverable"]!.S("revision"), detail["deliverable"]!.S("issuedDate"))); // the latest still shows
    }
}
