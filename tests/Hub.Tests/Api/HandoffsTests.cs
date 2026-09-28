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
        var p = await data.Project();
        if (restricted)
        {
            // Visibility is set after creation; CreateBody deliberately has no visibility field.
            await f.DbAsync(async db => {
                var project = await db.Projects.FirstAsync(x => x.Id == p.Id);
                project.Visibility = Visibility.Restricted;
                return await db.SaveChangesAsync();
            });
            p.Visibility = Visibility.Restricted;
            Assert.Equal(Visibility.Restricted, f.Db(db => db.Projects.First(x => x.Id == p.Id).Visibility));
        }
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

    [Fact]
    public async Task Discipline_coordination_reconciles_source_rows_and_counts_at_one_evaluation()
    {
        var s = await New(restricted: true);
        var handoff = await Create(s);
        var second = await data.NewTask(s.Project.Id, TestData.Omar, new { assigneeId = data.User(TestData.Omar) }, "Electrical");
        var third = await data.NewTask(s.Project.Id, TestData.Omar, new { assigneeId = data.User(TestData.Omar) }, "Electrical");
        await (await f.As(TestData.Pm).Post($"/api/v1/tasks/{second.G("id")}/dependencies",
            new { predecessorTaskId = s.Target.G("id") })).Json(201);
        await (await f.As(TestData.Pm).Post($"/api/v1/tasks/{third.G("id")}/dependencies",
            new { predecessorTaskId = second.G("id") })).Json(201);
        var url = $"/api/v1/projects/{s.Project.Id}/discipline-coordination?disciplineId={data.ProjectDiscipline(s.Project.Id, "Electrical")}";
        await (await f.As(TestData.Rita).GetAsync(url)).Json(404);
        var result = await (await f.As(TestData.Omar).GetAsync(url)).Json();
        Assert.Equal(1, result!["handoffsTotal"]!.GetValue<int>());
        Assert.Equal(1, result["handoffs"]!.AsArray().Count);
        Assert.Equal(handoff.G("id"), result["handoffs"]!.AsArray()[0]!.G("id"));
        Assert.NotNull(result["evaluatedAt"]);
        var group = Assert.Single(result["blockerGroups"]!.AsArray());
        Assert.Equal(handoff.G("id"), group!.G("handoffId"));
        Assert.Equal(3, group["taskIds"]!.AsArray().Count);
        Assert.Equal(new[] { s.Target.G("id"), second.G("id"), third.G("id") }.OrderBy(id => id),
            group["taskIds"]!.AsArray().Select(id => Guid.Parse(id!.GetValue<string>())).OrderBy(id => id));
        var meeting = await (await f.As(TestData.Pm).Post($"/api/v1/projects/{s.Project.Id}/meetings/current", new { })).Json();
        var action = await (await f.As(TestData.Pm).Post($"/api/v1/meetings/{meeting.G("id")}/actions", new {
            text = "Resolve the survey input for Electrical", ownerType = "User", ownerUserId = data.User(TestData.Omar),
            dueDate = "2026-09-18", relatedTaskId = s.Target.G("id"),
            links = new[] { new { targetType = ItemType.Handoff, targetId = handoff.G("id") },
                new { targetType = ItemType.Task, targetId = second.G("id") }, new { targetType = ItemType.Task, targetId = third.G("id") } }
        })).Json(201);
        await (await f.As(TestData.Pm).Post($"/api/v1/meetings/{meeting.G("id")}/actions", new {
            text = "Separate task follow-up", ownerType = "User", ownerUserId = data.User(TestData.Omar),
            relatedTaskId = s.Target.G("id")
        })).Json(201); // sharing a task does not make an action belong to this handoff
        var withAction = await (await f.As(TestData.Omar).GetAsync(url)).Json();
        var linked = Assert.Single(withAction["linkedActions"]!.AsArray());
        Assert.Equal(action.G("id"), linked!.G("id"));
        Assert.Equal(handoff.G("id"), linked.G("sourceId"));
        var actionDetail = await (await f.As(TestData.Omar).GetAsync($"/api/v1/actions/{action.G("id")}")).Json();
        Assert.Contains(actionDetail["links"]!.AsArray(), row => row!.S("targetType") == ItemType.Handoff && row.G("targetId") == handoff.G("id"));
        Assert.Equal(3, actionDetail["links"]!.AsArray().Count);
        await (await f.As(TestData.Rita).GetAsync(url)).Json(404);
        await Move(s, handoff.G("id"), TestData.Alex, HandoffStatus.Submitted);
        var electrical = await (await f.As(TestData.Omar).GetAsync(url)).Json();
        Assert.Empty(electrical["outgoing"]!.AsArray());
        Assert.Single(electrical["incoming"]!.AsArray());
        var civil = await (await f.As(TestData.Alex).GetAsync($"/api/v1/projects/{s.Project.Id}/discipline-coordination?disciplineId={data.ProjectDiscipline(s.Project.Id, "Civil")}")).Json();
        Assert.Single(civil["outgoing"]!.AsArray());
        Assert.Empty(civil["incoming"]!.AsArray());
        await Move(s, handoff.G("id"), TestData.Omar, HandoffStatus.Accepted, outcome: "Input reviewed for use");
        var accepted = await (await f.As(TestData.Omar).GetAsync(url)).Json();
        Assert.Empty(accepted["blockerGroups"]!.AsArray());
        Assert.Empty(accepted["linkedActions"]!.AsArray());
    }

    [Fact]
    public async Task Workspace_coordination_omits_restricted_projects_without_membership()
    {
        var visible = await New(restricted: true);
        var hidden = await New(restricted: true);
        var shownHandoff = await Create(visible);
        await Create(hidden);
        await f.DbAsync(async db => {
            var membership = await db.ProjectMembers.SingleAsync(m => m.ProjectId == hidden.Project.Id &&
                m.UserId == data.User(TestData.Alex));
            membership.RemovedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
            return 0;
        });
        var result = await (await f.As(TestData.Alex).GetAsync("/api/v1/discipline-coordination")).Json();
        var projects = result["projects"]!.AsArray();
        Assert.Contains(projects, p => p!.G("id") == visible.Project.Id &&
            p["data"]!["handoffs"]!.AsArray().Any(h => h!.G("id") == shownHandoff.G("id")));
        Assert.DoesNotContain(projects, p => p!.G("id") == hidden.Project.Id);
        Assert.DoesNotContain(result["projectChoices"]!.AsArray(), p => p!.G("id") == hidden.Project.Id);
        await (await f.As(TestData.Alex).GetAsync($"/api/v1/discipline-coordination?projectId={hidden.Project.Id}"))
            .Json(404);
        var selectedUrl = $"/api/v1/discipline-coordination?scopeKind=set&scopeProjectIds={visible.Project.Id}";
        var selected = await (await f.As(TestData.Alex).GetAsync(selectedUrl)).Json();
        Assert.Single(selected["projects"]!.AsArray());
        Assert.Single(selected["projectChoices"]!.AsArray());
        var workspace = await (await f.As(TestData.Alex).Post("/api/v1/workspaces",
            new { name = "Coordination test", projectIds = new[] { visible.Project.Id } })).Json(201);
        var named = await (await f.As(TestData.Alex).GetAsync(
            $"/api/v1/discipline-coordination?scopeKind=workspace&scopeWorkspaceId={workspace.G("id")}&scopeProjectIds={hidden.Project.Id}"))
            .Json();
        Assert.Single(named["projects"]!.AsArray());
        Assert.Equal(visible.Project.Id, named["projects"]![0]!.G("id"));
        await (await f.As(TestData.Pm).GetAsync(
            $"/api/v1/discipline-coordination?scopeKind=workspace&scopeWorkspaceId={workspace.G("id")}"))
            .Json(404);
    }
    async Task<JsonNode> Move(Setup s, Guid id, string who, string to, string? reason = null, string? outcome = null, int expect = 200)
    {
        var v = f.Db(db => db.Handoffs.First(h => h.Id == id).RowVersion);
        return await (await f.As(who).Post(Path(s.Project.Id, id) + "/transition", new HandoffEndpoints.MoveBody(Guid.NewGuid(), to, v, reason, outcome))).Json(expect);
    }

    [Fact]
    public async Task Submitted_handoff_keeps_source_derived_readiness_not_ready_until_accepted()
    {
        var s = await New(); var handoffId = (await Create(s)).G("id");
        var targetId = s.Target.G("id");
        var targetVersion = f.Db(db => db.Tasks.Single(t => t.Id == targetId).RowVersion);
        var path = $"/api/v1/projects/{s.Project.Id}/readiness/Task/{targetId}";
        var assessment = await (await f.As(TestData.Omar).Post(path,
            new ReadinessEndpoints.CreateBody(Guid.NewGuid(), targetVersion, "Electrical service alignment",
                "Alignment checked against the accepted survey"))).Json();
        await f.DbAsync(async db =>
        {
            var checks = await db.ReadinessChecks.Where(c => c.AssessmentId == assessment.G("id")).ToListAsync();
            foreach (var check in checks)
            {
                check.Applies = check.Code != ReadinessCheckCode.ProductionCapacity;
                check.Satisfied = check.Applies == true ? true : null;
            }
            await db.SaveChangesAsync(); return 0;
        });
        await Move(s, handoffId, TestData.Alex, HandoffStatus.Submitted);
        var submitted = await (await f.As(TestData.Omar).GetAsync(path)).Json();
        Assert.Equal(ReadinessState.NotReady, submitted["assessment"]!.S("state"));
        Assert.Contains(ReadinessCheckCode.Handoff, submitted["blocked"]!.AsArray().Select(x => x!.GetValue<string>()));
        await data.NewTask(s.Project.Id, TestData.Omar, new { assigneeId = data.User(TestData.Omar), dueDate = "2026-09-20" }, "Electrical"); // unassessed work is not Ready
        var coordinationUrl = $"/api/v1/projects/{s.Project.Id}/discipline-coordination?disciplineId={data.ProjectDiscipline(s.Project.Id, "Electrical")}&ownerId={data.User(TestData.Omar)}&from=2026-09-14&to=2026-09-20";
        var blocked = await (await f.As(TestData.Omar).GetAsync(coordinationUrl)).Json();
        var blockedRow = Assert.Single(blocked["startability"]!.AsArray());
        Assert.Equal(targetId, blockedRow!.G("targetId"));
        Assert.Equal(ReadinessState.NotReady, blockedRow.S("state"));
        Assert.Contains(ReadinessCheckCode.Handoff, blockedRow["blocked"]!.AsArray().Select(x => x!.GetValue<string>()));
        Assert.Equal(0, blocked["startabilityReadyTotal"]!.GetValue<int>());
        await Move(s, handoffId, TestData.Omar, HandoffStatus.Accepted, outcome: "Survey criteria met");
        var accepted = await (await f.As(TestData.Omar).GetAsync(path)).Json();
        Assert.Equal(ReadinessState.Ready, accepted["assessment"]!.S("state"));
        var ready = await (await f.As(TestData.Omar).GetAsync(coordinationUrl)).Json();
        Assert.Equal(ReadinessState.Ready, Assert.Single(ready["startability"]!.AsArray())!.S("state"));
        Assert.Equal(1, ready["startabilityReadyTotal"]!.GetValue<int>());
        await (await f.As(TestData.Omar).GetAsync($"/api/v1/projects/{s.Project.Id}/discipline-coordination?to=2026-09-10")).Json();
        await (await f.As(TestData.Omar).GetAsync($"/api/v1/projects/{s.Project.Id}/discipline-coordination?from=2026-09-20&to=2026-09-14")).Json(400);
    }

    [Fact]
    public async Task AC_HND_03_publishing_B_keeps_incorporated_A_and_creates_one_pending_assessment()
    {
        var s = await New(); var hid = (await Create(s)).G("id");
        await Move(s, hid, TestData.Alex, HandoffStatus.Submitted);
        await Move(s, hid, TestData.Omar, HandoffStatus.Accepted, outcome: "Survey criteria checked");
        await Move(s, hid, TestData.Omar, HandoffStatus.Incorporated, outcome: "Revision A incorporated into receiving design");
        var head = f.Db(db => db.SourceHeads.AsNoTracking().Single(h => h.ProjectId == s.Project.Id));
        var notice = await (await f.As(TestData.Alex).Post($"/api/v1/projects/{s.Project.Id}/source-revisions", new ChangeEndpoints.RegisterBody(
            Guid.NewGuid(), s.Source.G("id"), f.Db(db => db.Deliverables.Single(d => d.Id == s.Source.G("id")).RowVersion),
            data.ProjectDiscipline(s.Project.Id, "Civil"), data.User(TestData.Alex), "Deliverable", "survey", "Survey basis", "B", "https://example.test/survey-B.pdf", "Survey team", "Service corridor", null,
            head.CurrentRevisionId, head.RowVersion, "Updated corridor tie-in", new DateOnly(2026,9,14), new DateOnly(2026,9,18)))).Json();
        await (await f.As(TestData.Alex).Post($"/api/v1/projects/{s.Project.Id}/changes/{notice.G("id")}/publish", new ChangeEndpoints.PublishBody(Guid.NewGuid(), notice.I("rowVersion"), head.RowVersion, null))).Json();
        var detail = await Detail(s, hid);
        Assert.Equal(HandoffStatus.Incorporated, detail["row"]!.S("status"));
        Assert.Equal("A", detail["revisions"]![0]!["source"]!.S("revision"));
        Assert.Equal(3, detail["history"]!.AsArray().Count);
        Assert.Single(detail["changeAssessments"]!.AsArray());
        Assert.Equal(AssessmentStatus.Pending, detail["changeAssessments"]![0]!.S("status"));
        var a = f.Db(db => db.ChangeAssessments.Single(a => a.ChangeNoticeId == notice.G("id")));
        Assert.Equal(hid, a.HandoffId); Assert.NotNull(a.InputUseId);
        Assert.Equal(head.CurrentRevisionId, f.Db(db => db.InputUses.Single(u => u.Id == a.InputUseId).SourceRevisionId));
        Assert.Equal(TaskStatuses.NotStarted, f.Db(db => db.Tasks.Single(t => t.Id == s.Target.G("id")).Status));
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
        await Move(s, id, TestData.Omar, HandoffStatus.Returned, reason: "Please verify the replacement input");
        await Move(s, id, TestData.Marc, HandoffStatus.Submitted, reason: "Replacement input independently checked");
        var latest = f.Db(db => db.Handoffs.First(h => h.Id == id).RowVersion);
        // Changing the latest submitter must not erase the earlier sender's involvement.
        await (await f.As(TestData.Pm).Post(Path(s.Project.Id, id) + "/assign",
            illegal with { RequestId = Guid.NewGuid(), RowVersion = latest })).Json(400);
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
            // The shared test database can already contain over one batch of queued emails.
            // Put this fixture first so one worker run actually exercises its delivery guard.
            db.Emails.Add(new EmailMessage { UserId = owner, ToAddress = "alex@hub.test", Subject = "Handoff", RequiredProjectIds = [s.Project.Id], CreatedAt = DateTimeOffset.MinValue });
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
