using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Hub.Api.Data;
using Hub.Api.Features;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

[Collection("api")]
public sealed class HandoffsTests(HubFactory f)
{
    readonly TestData data = new(f);
    sealed record Setup(Project Project, JsonNode Source, JsonNode Target, HandoffEndpoints.DraftBody Body);
    string Path(Guid project, Guid? id = null) => $"/api/v1/projects/{project}/handoffs" + (id is null ? "" : $"/{id}");

    async Task<Setup> New(bool restricted = false)
    {
        var p = await data.Project(tweak: b => b["visibility"] = restricted ? Visibility.Restricted : Visibility.Open);
        var source = await (await f.As(TestData.Marc).Post($"/api/v1/projects/{p.Id}/deliverables", new {
            name = "Survey basis", projectDisciplineId = data.ProjectDiscipline(p.Id, "Civil"), deliverableTypeId = await data.DeliverableType(),
            ownerId = data.User(TestData.Alex), revision = "A", transmittalUrl = "https://example.test/survey-A.pdf" })).Json(201);
        var target = await data.NewTask(p.Id, TestData.Omar, new { assigneeId = data.User(TestData.Omar), dueDate = "2026-09-16" }, "Electrical");
        var version = f.Db(db => db.Deliverables.First(d => d.Id == source.G("id")).RowVersion);
        var b = new HandoffEndpoints.DraftBody(Guid.NewGuid(), "Survey to Electrical", source.G("id"), version, "A", "https://example.test/survey-A.pdf",
            data.ProjectDiscipline(p.Id, "Electrical"), data.User(TestData.Alex), data.User(TestData.Omar), target.G("id"), null,
            "Set the electrical service alignment", "Survey covers the entire service corridor", new DateOnly(2026, 9, 16), new DateOnly(2026, 9, 18), null, null);
        return new Setup(p, source, target, b);
    }
    async Task<JsonNode> Create(Setup s, HandoffEndpoints.DraftBody? body = null) =>
        await (await f.As(TestData.Marc).Post(Path(s.Project.Id), body ?? s.Body)).Json(201);
    async Task<JsonNode> Detail(Setup s, Guid id, string who = TestData.Alex) => await (await f.As(who).GetAsync(Path(s.Project.Id, id))).Json();
    async Task<JsonNode> Move(Setup s, Guid id, string who, string to, string? reason = null, string? outcome = null, int expect = 200)
    {
        var v = f.Db(db => db.Handoffs.First(h => h.Id == id).RowVersion);
        return await (await f.As(who).Post(Path(s.Project.Id, id) + "/transition", new HandoffEndpoints.MoveBody(Guid.NewGuid(), to, v, reason, outcome))).Json(expect);
    }

    [Fact]
    public async Task AC_HND_01_02_usable_acceptance_and_incorporation_preserve_dates_tasks_and_evidence()
    {
        var s = await New(); var h = await Create(s); var id = h.G("id");
        Assert.True((await Detail(s, id))["row"]!["dateMismatch"]!.GetValue<bool>());
        await Move(s, id, TestData.Pm, HandoffStatus.Submitted, expect: 403); // no proxy signatures
        await Move(s, id, TestData.Alex, HandoffStatus.Submitted);
        await Move(s, id, TestData.Omar, HandoffStatus.Accepted, outcome: "Entire corridor is covered; checked against criteria");
        var accepted = await Detail(s, id);
        Assert.Equal(HandoffStatus.Accepted, accepted["row"]!.S("status"));
        Assert.Null(accepted["row"]!["incorporatedRevisionId"]);
        var task = f.Db(db => db.Tasks.First(t => t.Id == s.Target.G("id")));
        Assert.Equal(TaskStatuses.NotStarted, task.Status);
        Assert.Equal(new DateOnly(2026, 9, 16), task.DueDate);
        await Move(s, id, TestData.Omar, HandoffStatus.Incorporated, outcome: "Revision A used in service alignment E-101");
        var incorporated = await Detail(s, id);
        Assert.Equal(incorporated["row"]!.S("currentRevisionId"), incorporated["row"]!.S("incorporatedRevisionId"));
        Assert.Equal(3, incorporated["history"]!.AsArray().Count);
        Assert.Single(incorporated["revisions"]!.AsArray());
        Assert.Equal("A", incorporated["revisions"]![0]!["source"]!.S("revision"));
        Assert.Equal(data.User(TestData.Omar), incorporated["history"]![1]!.G("createdBy"));
        Assert.Equal("2026-09-16", incorporated["row"]!.S("neededBy"));
        Assert.Equal("2026-09-18", incorporated["row"]!.S("promisedBy"));
        Assert.Equal(3, f.Db(db => db.ActivityLog.Count(a => a.ItemId == id && a.Action == "StatusChanged")));
    }

    [Fact]
    public async Task AC_HND_04_return_requires_reason_and_resubmission_keeps_each_snapshot()
    {
        var s = await New(); var id = (await Create(s)).G("id");
        await Move(s, id, TestData.Alex, HandoffStatus.Submitted);
        await Move(s, id, TestData.Alex, HandoffStatus.Accepted, outcome: "Attempting my own receipt", expect: 403);
        await Move(s, id, TestData.Omar, HandoffStatus.Returned, expect: 400);
        await Move(s, id, TestData.Omar, HandoffStatus.Returned, reason: "Missing the west corridor tie-in");
        var v = f.Db(db => db.Handoffs.First(h => h.Id == id).RowVersion);
        var changed = s.Body with { RequestId = Guid.NewGuid(), RowVersion = v, DeclaredRevision = "A1", SourceUrl = "https://example.test/survey-A1.pdf" };
        await (await f.As(TestData.Alex).Post(Path(s.Project.Id, id) + "/draft", changed)).Json();
        await Move(s, id, TestData.Alex, HandoffStatus.Submitted, expect: 400);
        await Move(s, id, TestData.Alex, HandoffStatus.Submitted, reason: "Added the missing west corridor tie-in");
        var detail = await Detail(s, id);
        Assert.Equal(new[] { "A", "A1" }, detail["revisions"]!.AsArray().Select(r => r!["source"]!.S("revision")));
        Assert.Equal("Missing the west corridor tie-in", detail["history"]![1]!.S("reason"));
        await Move(s, id, TestData.Omar, HandoffStatus.Accepted, expect: 400);
        await Move(s, id, TestData.Omar, HandoffStatus.Accepted, outcome: "Both corridor ends verified");
        var before = f.Db(db => db.HandoffRevisions.Count(r => r.HandoffId == id));
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.DbAsync(async db => {
            var revision = await db.SourceRevisions.FirstAsync(r => r.DeliverableId == s.Source.G("id")); revision.Revision = "Overwritten";
            return await db.SaveChangesAsync();
        }));
        Assert.Equal(before, f.Db(db => db.HandoffRevisions.Count(r => r.HandoffId == id)));
    }

    [Fact]
    public async Task AC_HND_05_receipts_are_independent_and_restricted_search_lists_and_exports_do_not_leak()
    {
        var s = await New(restricted: true);
        var first = (await Create(s)).G("id");
        var secondBody = s.Body with { RequestId = Guid.NewGuid(), ReceivingDisciplineId = data.ProjectDiscipline(s.Project.Id, "Civil"),
            ReceivingOwnerId = data.User(TestData.Marc), TargetTaskId = null, TargetDeliverableId = s.Source.G("id"), IntendedUse = "Check grading tie-ins" };
        var second = (await Create(s, secondBody)).G("id");
        await Move(s, first, TestData.Alex, HandoffStatus.Submitted);
        await Move(s, second, TestData.Alex, HandoffStatus.Submitted);
        await Move(s, first, TestData.Omar, HandoffStatus.Accepted, outcome: "Input usable for this purpose");
        Assert.Equal(HandoffStatus.Submitted, (await Detail(s, second))["row"]!.S("status"));
        var outsider = f.As(TestData.Diane);
        foreach (var path in new[] { Path(s.Project.Id), Path(s.Project.Id, first), Path(s.Project.Id) + "/export?format=csv", Path(s.Project.Id) + "/options" })
            Assert.Equal(HttpStatusCode.NotFound, (await outsider.GetAsync(path)).StatusCode);
        var key = (await Detail(s, first))["row"]!.S("key");
        var hidden = await (await outsider.GetAsync("/api/v1/search?type=handoffs&q=" + key)).Json();
        Assert.Empty(hidden["groups"]!["handoffs"]!.AsArray()); Assert.Null(hidden["exact"]);
        var visible = await (await f.As(TestData.Omar).GetAsync("/api/v1/search?type=handoffs&q=" + key)).Json();
        Assert.Equal(first, visible["exact"]!.G("id"));
        var list = await (await f.As(TestData.Omar).GetAsync(Path(s.Project.Id) + "?direction=incoming&status=Accepted")).Json();
        Assert.Equal(1, list.I("totalCount"));
        var exported = await f.As(TestData.Omar).GetAsync(Path(s.Project.Id) + "/export?direction=incoming&status=Accepted&format=csv");
        Assert.Equal(HttpStatusCode.OK, exported.StatusCode);
        Assert.Contains(key, await exported.Content.ReadAsStringAsync());
        Assert.DoesNotContain((await Detail(s, second))["row"]!.S("key"), await exported.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Duplicate_commands_racing_or_retried_commit_one_handoff_one_receipt_and_one_notification()
    {
        var s = await New();
        var creates = await Task.WhenAll(Enumerable.Range(0, 2).Select(async _ => await (await f.As(TestData.Marc).Post(Path(s.Project.Id), s.Body)).Json(201)));
        Assert.Equal(creates[0].G("id"), creates[1].G("id")); var id = creates[0].G("id");
        Assert.Equal(1, f.Db(db => db.Handoffs.Count(h => h.ProjectId == s.Project.Id)));
        var b = new HandoffEndpoints.MoveBody(Guid.NewGuid(), HandoffStatus.Submitted, creates[0].I("rowVersion"), null, null);
        var submits = await Task.WhenAll(Enumerable.Range(0, 2).Select(async _ => await (await f.As(TestData.Alex).Post(Path(s.Project.Id, id) + "/transition", b)).Json()));
        Assert.Equal(submits[0].I("rowVersion"), submits[1].I("rowVersion"));
        Assert.Equal(1, f.Db(db => db.HandoffReceiptEvents.Count(h => h.HandoffId == id)));
        Assert.Equal(1, f.Db(db => db.Notifications.Where(n => n.ItemId == id && n.EventType == NotificationEvents.HandoffChanged).Sum(n => n.Count)));
        await (await f.As(TestData.Alex).Post(Path(s.Project.Id, id) + "/transition", b with { Reason = "Reused key with a different payload" })).Json(422);
        await (await f.As(TestData.Alex).Post(Path(s.Project.Id, id) + "/transition", b with { RequestId = Guid.NewGuid() })).Json(409);
    }

    [Fact]
    public async Task Cross_project_targets_missing_promises_stale_sources_and_unavailable_owners_are_refused_atomically()
    {
        var s = await New(); var other = await New();
        await (await f.As(TestData.Marc).Post(Path(s.Project.Id), s.Body with { RequestId = Guid.NewGuid(), TargetTaskId = other.Target.G("id") })).Json(400);
        await (await f.As(TestData.Marc).Post(Path(s.Project.Id), s.Body with { RequestId = Guid.NewGuid(), ReceivingOwnerId = data.User(TestData.Rita) })).Json(400);
        await (await f.As(TestData.Marc).Post(Path(s.Project.Id), s.Body with { RequestId = Guid.NewGuid(), SourceRowVersion = s.Body.SourceRowVersion + 1 })).Json(422);
        Assert.Equal(0, f.Db(db => db.Handoffs.Count(h => h.ProjectId == s.Project.Id)));
        var id = (await Create(s, s.Body with { PromisedBy = null })).G("id");
        await Move(s, id, TestData.Alex, HandoffStatus.Submitted, expect: 400);
        Assert.Equal(0, f.Db(db => db.HandoffRevisions.Count(r => r.HandoffId == id)));
        Assert.Equal(0, f.Db(db => db.HandoffReceiptEvents.Count(r => r.HandoffId == id)));
        await Move(s, id, TestData.Rita, HandoffStatus.Cancelled, reason: "Read-only attempt", expect: 403);
        await Move(s, id, TestData.Pm, HandoffStatus.Cancelled, reason: "No longer required");
    }

    [Fact]
    public async Task Reassignment_preserves_the_submitter_and_cannot_bypass_independence()
    {
        var s = await New(); var id = (await Create(s)).G("id");
        await Move(s, id, TestData.Alex, HandoffStatus.Submitted);
        var version = f.Db(db => db.Handoffs.First(h => h.Id == id).RowVersion);
        var illegal = new HandoffEndpoints.AssignBody(Guid.NewGuid(), data.User(TestData.Marc), data.User(TestData.Alex), version, "Replacing both owners");
        await (await f.As(TestData.Pm).Post(Path(s.Project.Id, id) + "/assign", illegal)).Json(400);
        var allowed = illegal with { RequestId = Guid.NewGuid(), ReceivingOwnerId = data.User(TestData.Omar) };
        await (await f.As(TestData.Pm).Post(Path(s.Project.Id, id) + "/assign", allowed)).Json();
        Assert.Equal(data.User(TestData.Alex), (await Detail(s, id))["revisions"]![0]!.G("createdBy"));
        Assert.Contains("Replacing both owners", f.Db(db => db.ActivityLog.Where(a => a.ItemId == id).Select(a => a.Reason).ToList()));
    }

    [Theory]
    [InlineData(ProjectStatus.Archived)] [InlineData(ProjectStatus.Cancelled)] [InlineData(ProjectStatus.Complete)]
    public async Task Lifecycle_guards_veto_receiver_commands(string status)
    {
        var s = await New(); var id = (await Create(s)).G("id");
        await Move(s, id, TestData.Alex, HandoffStatus.Submitted);
        await f.DbAsync(async db => { var p = await db.Projects.FirstAsync(p => p.Id == s.Project.Id); p.Status = status; return await db.SaveChangesAsync(); });
        await Move(s, id, TestData.Omar, HandoffStatus.Accepted, outcome: "Should not be accepted", expect: 403);
        Assert.Equal(HandoffStatus.Submitted, (await Detail(s, id))["row"]!.S("status"));
    }

    [Fact]
    public async Task Scoped_delivery_rechecks_access_and_preserves_source_snapshots_after_a_later_edit()
    {
        var s = await New(restricted: true); var id = (await Create(s)).G("id");
        await Move(s, id, TestData.Alex, HandoffStatus.Submitted);
        var owner = data.User(TestData.Alex);
        Assert.True(await f.DbAsync(db => EmailProjectAccess.Allowed(db, owner, [s.Project.Id])));
        await f.DbAsync(async db => {
            var member = await db.ProjectMembers.FirstAsync(m => m.ProjectId == s.Project.Id && m.UserId == owner); member.RemovedAt = f.Clock.GetUtcNow();
            var source = await db.Deliverables.FirstAsync(d => d.Id == s.Source.G("id")); source.Revision = "B";
            db.Emails.Add(new EmailMessage { UserId = owner, ToAddress = "alex@hub.test", Subject = "Handoff", RequiredProjectIds = [s.Project.Id], CreatedAt = f.Clock.GetUtcNow() });
            return await db.SaveChangesAsync();
        });
        Assert.False(await f.DbAsync(db => EmailProjectAccess.Allowed(db, owner, [s.Project.Id])));
        await f.RunJob<EmailJob>();
        var email = f.Db(db => db.Emails.Single(e => e.UserId == owner && e.Subject == "Handoff"));
        Assert.Null(email.SentAt); Assert.NotNull(email.SuppressedAt);
        var detail = await Detail(s, id, TestData.Omar);
        Assert.True(detail["row"]!["sourceChanged"]!.GetValue<bool>());
        Assert.Equal("A", detail["revisions"]![0]!["source"]!.S("revision"));
        Assert.False(detail["row"]!["ownersAvailable"]!.GetValue<bool>());
        await Move(s, id, TestData.Omar, HandoffStatus.Accepted, outcome: "Blocked by unavailable sender", expect: 400);
        // The later packet 027 creates the change-impact assignment; this test does not claim AC-HND-03.
    }
}
