using System.Text.Json;
using System.Text.Json.Nodes;
using Hub.Api.Data;
using Hub.Api.Features;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

/// Normal implemented producer paths, exercised with work_screens OFF. No private-table fixture
/// writes, private locks, or direct canonical assignments: every assignment goes through its API.
/// Drawing-list import (S34) is not yet an implemented producer; project CSV import below is.
public sealed class WorkAssignmentProducerPathsTests
{
    static string PersonalState(WorkSeenTestContext s) => s.Factory.Db(db => JsonSerializer.Serialize(new {
        Acks = db.WorkSeenAcknowledgements.AsNoTracking().OrderBy(row => row.RecipientId).ThenBy(row => row.Token).ToArray(),
        Receipts = db.WorkSeenCommandReceipts.AsNoTracking().OrderBy(row => row.UserId).ThenBy(row => row.RequestId).ToArray(),
        Settings = db.UserSettings.OrderBy(row => row.UserId).Select(row => new { row.UserId, row.MyWorkFirstVisitAt,
            row.MyWorkPreviousVisitAt, row.MyWorkLastVisitAt, row.MyWorkLastMarkAllAt, row.MyWorkSeenRevision }).ToArray()
    }));

    static async Task PreparePersonalStateThenGoDark(WorkSeenTestContext s)
    {
        await s.Enable(); await s.Visit(); await s.Visit(TestData.Jill); await s.Flag("work_screens", false);
    }

    static int Version(HubDb db, WorkSeenKey key) => key.ItemType switch {
        "Task" => db.Tasks.Single(row => row.Id == key.ItemId).RowVersion,
        "Deliverable" => db.Deliverables.Single(row => row.Id == key.ItemId).RowVersion,
        "Decision" => db.Decisions.Single(row => row.Id == key.ItemId).RowVersion,
        "Action" => db.Actions.Single(row => row.Id == key.ItemId).RowVersion,
        "Risk" => db.Risks.Single(row => row.Id == key.ItemId).RowVersion,
        "Issue" => db.Issues.Single(row => row.Id == key.ItemId).RowVersion,
        "Handoff" => db.Handoffs.Single(row => row.Id == key.ItemId).RowVersion,
        "Project" => db.Projects.Single(row => row.Id == key.ItemId).RowVersion,
        _ => throw new ArgumentException("Not a producer fixture kind", nameof(key))
    };

    static Guid Published(WorkSeenTestContext s, WorkSeenKey key, string role, Guid recipient, string actor, int? sourceVersion = null)
    {
        var actorId = s.Data.User(actor);
        return s.Factory.Db(db => {
            var version = sourceVersion ?? Version(db, key);
            Assert.InRange(version, 0, Version(db, key));
            var occurrence = db.WorkAssignmentOccurrences.Single(row => row.ItemType == key.ItemType && row.ItemId == key.ItemId
                && row.Role == role && row.RecipientId == recipient && row.SourceVersion == version);
            var source = db.ActivityLog.Single(row => row.Id == occurrence.SourceActivityId);
            Assert.Equal(key.ItemType, source.ItemType); Assert.Equal(key.ItemId, source.ItemId);
            Assert.Equal(source.ProjectId, occurrence.ProjectId);
            Assert.Equal(actorId, source.ActorUserId); Assert.Equal(s.Factory.Clock.Now, source.OccurredAt);
            Assert.Equal(source.OccurredAt, occurrence.SourceOccurredAt); Assert.Equal("Published", occurrence.Provenance);
            Assert.False(db.WorkSeenAcknowledgements.Any(row => row.Token == occurrence.Token));
            return occurrence.Token;
        });
    }

    static int Occurrences(WorkSeenTestContext s, WorkSeenKey key) => s.Factory.Db(db => db.WorkAssignmentOccurrences
        .Count(row => row.ItemType == key.ItemType && row.ItemId == key.ItemId));

    static async Task<JsonNode> Deliverable(WorkSeenTestContext s, Project p, Guid owner, Guid? reviewer = null, Guid? fromTask = null) =>
        await (await s.Factory.As(TestData.Pm).Post($"/api/v1/projects/{p.Id}/deliverables", new { name = "Synthetic producer deliverable",
            projectDisciplineId = s.Data.ProjectDiscipline(p.Id, "Civil"), deliverableTypeId = await s.Data.DeliverableType(),
            ownerId = owner, reviewerId = reviewer, requiresReview = false, fromTaskId = fromTask })).Json(201);

    static async Task<JsonNode> Decision(WorkSeenTestContext s, Project p, Guid owner) =>
        await (await s.Factory.As(TestData.Pm).Post($"/api/v1/projects/{p.Id}/decisions", new { subject = "Synthetic producer decision",
            description = "Synthetic", requiredByDate = "2026-10-01", impactLevel = Impact.Low,
            impactDescription = "Synthetic producer impact", ownerUserId = owner })).Json(201);

    [Fact]
    public async Task Ordinary_edits_and_handoff_assign_publish_every_changed_recipient_role_while_dark()
    {
        using var s = new WorkSeenTestContext(); var (p, cases) = await WorkScreenReadEnvelopeTests.NineDetails(s);
        await PreparePersonalStateThenGoDark(s); var personal = PersonalState(s);
        var jill = s.Data.User(TestData.Jill); var sam = s.Data.User(TestData.Sam); var client = s.Factory.As(TestData.Pm);
        foreach (var item in cases.Where(item => item.Kind is not ("Project" or "ChangeNotice")))
        {
            var key = new WorkSeenKey(item.Kind, item.Id); var count = Occurrences(s, key); var version = s.Factory.Db(db => Version(db, key));
            if (item.Kind == "Handoff")
                await (await client.Post($"/api/v1/projects/{p.Id}/handoffs/{item.Id}/assign", new HandoffEndpoints.AssignBody(
                    Guid.NewGuid(), jill, sam, version, "Synthetic assignment coverage"))).Json();
            else
            {
                object body = item.Kind switch {
                    "Task" => new { assigneeId = jill, reviewerId = sam },
                    "Deliverable" => new { ownerId = jill, reviewerId = sam },
                    "Decision" => new { ownerUserId = jill },
                    "Action" => new { ownerType = "User", ownerUserId = jill },
                    _ => new { ownerId = jill }
                };
                await (await client.Patch(item.Path, body, version)).Json();
            }
            var roles = item.Kind switch {
                "Task" => new[] { ("Assignee", jill), ("Reviewer", sam) },
                "Deliverable" => new[] { ("Owner", jill), ("Reviewer", sam) },
                "Handoff" => new[] { ("SendingOwner", jill), ("ReceivingOwner", sam) },
                "Decision" or "Action" => new[] { ("InternalOwner", jill) },
                _ => new[] { ("Owner", jill) }
            };
            foreach (var (role, recipient) in roles) Published(s, key, role, recipient, TestData.Pm);
            Assert.Equal(count + roles.Length, Occurrences(s, key));
            if (item.Kind == "Handoff")
            {
                // Draft editing has a separate assignment-writing route from /assign.
                var h = s.Factory.Db(db => db.Handoffs.AsNoTracking().Single(row => row.Id == item.Id));
                var sourceVersion = s.Factory.Db(db => db.Deliverables.Single(row => row.Id == h.SourceDeliverableId).RowVersion);
                var alex = s.Data.User(TestData.Alex); var omar = s.Data.User(TestData.Omar);
                await (await client.Post($"/api/v1/projects/{p.Id}/handoffs/{item.Id}/draft", new HandoffEndpoints.DraftBody(
                    Guid.NewGuid(), h.Title, h.SourceDeliverableId, sourceVersion, h.DeclaredRevision, h.SourceUrl,
                    h.ReceivingDisciplineId, alex, omar, h.TargetTaskId, h.TargetDeliverableId, h.IntendedUse, h.AcceptanceCriteria,
                    h.NeededBy, h.PromisedBy, h.RowVersion, "Synthetic draft assignment coverage"))).Json();
                Published(s, key, "SendingOwner", alex, TestData.Pm); Published(s, key, "ReceivingOwner", omar, TestData.Pm);
                Assert.Equal(count + 4, Occurrences(s, key));
            }
        }
        Assert.Equal(personal, PersonalState(s));
    }

    [Fact]
    public async Task Reviewer_selected_by_normal_review_transition_publishes_only_the_new_reviewer_role()
    {
        using var s = new WorkSeenTestContext(); var p = await s.Data.Project();
        var alex = s.Data.User(TestData.Alex); var marc = s.Data.User(TestData.Marc);
        var task = await s.Data.NewTask(p.Id, TestData.Pm, new { assigneeId = alex, requiresReview = true });
        await s.Data.Move(TestData.Alex, task, TaskStatuses.InProgress);
        var key = new WorkSeenKey("Task", task.G("id")); var count = Occurrences(s, key);
        await PreparePersonalStateThenGoDark(s); var personal = PersonalState(s);
        await s.Data.Move(TestData.Alex, task, TaskStatuses.ReadyForReview, new { reviewerId = marc });
        Published(s, key, "Reviewer", marc, TestData.Alex); Assert.Equal(count + 1, Occurrences(s, key));
        Assert.Equal(1, s.Factory.Db(db => db.WorkAssignmentOccurrences.Count(row => row.ItemType == "Task" && row.ItemId == key.ItemId && row.Role == "Assignee")));
        Assert.Equal(personal, PersonalState(s));
    }

    [Fact]
    public async Task Bulk_task_assignment_and_deliverable_owner_changes_publish_each_changed_row_while_dark()
    {
        using var s = new WorkSeenTestContext(); var p = await s.Data.Project();
        var alex = s.Data.User(TestData.Alex); var jill = s.Data.User(TestData.Jill);
        var tasks = new[] { await s.Data.NewTask(p.Id, extra: new { assigneeId = alex }), await s.Data.NewTask(p.Id, extra: new { assigneeId = alex }) };
        var del = await Deliverable(s, p, alex);
        await PreparePersonalStateThenGoDark(s); var personal = PersonalState(s);
        var keys = tasks.Select(row => new WorkSeenKey("Task", row.G("id"))).Append(new("Deliverable", del.G("id"))).ToArray();
        var counts = keys.ToDictionary(key => key, key => Occurrences(s, key));
        await (await s.Factory.As(TestData.Pm).Post($"/api/v1/projects/{p.Id}/tasks/bulk", new {
            taskIds = tasks.Select(row => row.G("id")).ToArray(), operation = "assign", @params = new { assigneeId = jill }
        })).Json();
        await (await s.Factory.As(TestData.Pm).Post($"/api/v1/projects/{p.Id}/deliverables/bulk", new {
            ids = new[] { del.G("id") }, operation = "setOwner", @params = new { ownerId = jill }
        })).Json();
        foreach (var key in keys)
        {
            Published(s, key, key.ItemType == "Task" ? "Assignee" : "Owner", jill, TestData.Pm);
            Assert.Equal(counts[key] + 1, Occurrences(s, key));
        }
        Assert.Equal(personal, PersonalState(s));
        var detail = await (await s.Factory.As(TestData.Jill).GetAsync($"/api/v1/tasks/{keys[0].ItemId}")).Json();
        Assert.False(detail.AsObject().ContainsKey("seen"));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Pickup_claim_and_manager_offer_assignment_publish_canonical_recipient_not_offer_snapshot(bool managerAssigns)
    {
        using var s = new WorkSeenTestContext(); await s.Flag("task_pickup", true); await s.Flag("org_structure", false);
        var p = await s.Data.Project(); var alex = s.Data.User(TestData.Alex); var jill = s.Data.User(TestData.Jill);
        await (await s.Factory.As(TestData.Pm).Post($"/api/v1/projects/{p.Id}/members", new { userId = jill, roles = new[] { "TeamMember" },
            primaryDisciplineId = (Guid?)null })).Json(201);
        var task = await s.Data.NewTask(p.Id, extra: new { assigneeId = alex }); var key = new WorkSeenKey("Task", task.G("id"));
        await PreparePersonalStateThenGoDark(s); var personal = PersonalState(s);
        var originalVersion = s.Factory.Db(db => Version(db, key)); var count = Occurrences(s, key);
        await (await s.Factory.As(TestData.Alex).Post($"/api/v1/tasks/{key.ItemId}/pickup-offer", new {
            reason = "Synthetic cover request", keepWatching = true
        })).Json(201);
        Assert.Equal(count, Occurrences(s, key)); Assert.Equal(originalVersion, s.Factory.Db(db => Version(db, key)));
        var actor = managerAssigns ? TestData.Pm : TestData.Jill;
        if (managerAssigns)
            await (await s.Factory.As(actor).Post($"/api/v1/tasks/{key.ItemId}/pickup-offer/assign", new { userId = jill, rowVersion = originalVersion })).Json();
        else
            await (await s.Factory.As(actor).Post($"/api/v1/tasks/{key.ItemId}/pickup", new { rowVersion = originalVersion })).Json();
        Published(s, key, "Assignee", jill, actor); Assert.Equal(count + 1, Occurrences(s, key));
        Assert.Equal(personal, PersonalState(s));
    }

    [Fact]
    public async Task Supervisor_reassigns_all_five_supported_canonical_roles_with_one_source_version_per_item()
    {
        using var s = new WorkSeenTestContext();
        await (await s.Factory.As(TestData.Admin).Put("/api/v1/admin/settings/allow_self_review", new { value = true })).Json();
        var p = await s.Data.Project(); var alex = s.Data.User(TestData.Alex); var jill = s.Data.User(TestData.Jill);
        var task = await s.Data.NewTask(p.Id, TestData.Pm, new { assigneeId = alex, reviewerId = alex });
        var del = await Deliverable(s, p, alex, alex); var decision = await Decision(s, p, alex);
        var taskKey = new WorkSeenKey("Task", task.G("id")); var delKey = new WorkSeenKey("Deliverable", del.G("id"));
        var decisionKey = new WorkSeenKey("Decision", decision.G("id"));
        await PreparePersonalStateThenGoDark(s); var personal = PersonalState(s);
        var counts = new[] { taskKey, delKey, decisionKey }.ToDictionary(key => key, key => Occurrences(s, key));
        var result = await (await s.Factory.As(TestData.Sam).Post($"/api/v1/users/{alex}/reassign", new ReassignEndpoints.ReassignBody([
            new("TaskAssignee", taskKey.ItemId), new("TaskReviewer", taskKey.ItemId), new("DeliverableOwner", delKey.ItemId),
            new("DeliverableReviewer", delKey.ItemId), new("DecisionOwner", decisionKey.ItemId)
        ], jill))).Json();
        Assert.Equal(5, result.I("reassigned")); Assert.Empty(result["skipped"]!.AsArray());
        foreach (var role in new[] { "Assignee", "Reviewer" }) Published(s, taskKey, role, jill, TestData.Sam);
        foreach (var role in new[] { "Owner", "Reviewer" }) Published(s, delKey, role, jill, TestData.Sam);
        Published(s, decisionKey, "InternalOwner", jill, TestData.Sam);
        Assert.Equal(counts[taskKey] + 2, Occurrences(s, taskKey)); Assert.Equal(counts[delKey] + 2, Occurrences(s, delKey));
        Assert.Equal(counts[decisionKey] + 1, Occurrences(s, decisionKey)); Assert.Equal(personal, PersonalState(s));
    }

    [Fact]
    public async Task Member_removal_publishes_only_the_task_assignee_reviewer_and_deliverable_owner_it_reassigns()
    {
        using var s = new WorkSeenTestContext(); var p = await s.Data.Project();
        var alex = s.Data.User(TestData.Alex); var marc = s.Data.User(TestData.Marc); var jill = s.Data.User(TestData.Jill);
        var assigned = await s.Data.NewTask(p.Id, TestData.Pm, new { assigneeId = alex, reviewerId = marc });
        var reviewed = await s.Data.NewTask(p.Id, TestData.Pm, new { assigneeId = marc, reviewerId = alex });
        var owned = await Deliverable(s, p, alex, marc);
        var unchangedReviewer = await Deliverable(s, p, marc, alex);
        var a = new WorkSeenKey("Task", assigned.G("id")); var r = new WorkSeenKey("Task", reviewed.G("id"));
        var d = new WorkSeenKey("Deliverable", owned.G("id")); var notMoved = new WorkSeenKey("Deliverable", unchangedReviewer.G("id"));
        await PreparePersonalStateThenGoDark(s); var personal = PersonalState(s);
        var counts = new[] { a, r, d, notMoved }.ToDictionary(key => key, key => Occurrences(s, key));
        var unchangedVersion = s.Factory.Db(db => Version(db, notMoved));
        var member = s.Factory.Db(db => db.ProjectMembers.AsNoTracking().Single(row => row.ProjectId == p.Id && row.UserId == alex && row.RemovedAt == null));
        await (await s.Factory.As(TestData.Pm).Delete($"/api/v1/projects/{p.Id}/members/{member.Id}?reassignTo={jill}&reason=Synthetic%20coverage", member.RowVersion)).Json();
        Published(s, a, "Assignee", jill, TestData.Pm); Published(s, r, "Reviewer", jill, TestData.Pm); Published(s, d, "Owner", jill, TestData.Pm);
        foreach (var key in new[] { a, r, d }) Assert.Equal(counts[key] + 1, Occurrences(s, key));
        // TeamService.Remove does not change Deliverable.ReviewerId. Membership loss alone is not a new assignment.
        Assert.Equal(alex, s.Factory.Db(db => db.Deliverables.Single(row => row.Id == notMoved.ItemId).ReviewerId));
        Assert.Equal(unchangedVersion, s.Factory.Db(db => Version(db, notMoved))); Assert.Equal(counts[notMoved], Occurrences(s, notMoved));
        Assert.Equal(personal, PersonalState(s));
    }

    [Fact]
    public async Task Primary_project_manager_away_and_back_publishes_new_occurrences_while_membership_roles_do_not()
    {
        using var s = new WorkSeenTestContext(); var p = await s.Data.Project(); var key = new WorkSeenKey("Project", p.Id);
        await PreparePersonalStateThenGoDark(s); var personal = PersonalState(s);
        var old = s.Factory.Db(db => db.WorkAssignmentOccurrences.Single(row => row.ItemType == "Project" && row.ItemId == p.Id).Token);
        var marc = s.Data.User(TestData.Marc); var pm = s.Data.User(TestData.Pm);
        await (await s.Factory.As(TestData.Admin).Patch($"/api/v1/projects/{p.Id}", new { projectManagerId = marc }, s.Factory.Db(db => Version(db, key)))).Json();
        var away = Published(s, key, "ProjectManager", marc, TestData.Admin);
        await (await s.Factory.As(TestData.Admin).Patch($"/api/v1/projects/{p.Id}", new { projectManagerId = pm }, s.Factory.Db(db => Version(db, key)))).Json();
        var back = Published(s, key, "ProjectManager", pm, TestData.Admin);
        Assert.NotEqual(old, back); Assert.NotEqual(away, back); Assert.Equal(3, Occurrences(s, key));
        // Assigning a secondary PM role is not changing the project's canonical primary manager.
        var member = s.Factory.Db(db => db.ProjectMembers.AsNoTracking().Single(row => row.ProjectId == p.Id && row.UserId == marc && row.RemovedAt == null));
        await (await s.Factory.As(TestData.Admin).Patch($"/api/v1/projects/{p.Id}/members/{member.Id}", new { roles = new[] { "PM", "TeamMember" } }, member.RowVersion)).Json(204);
        Assert.Equal(3, Occurrences(s, key)); Assert.Equal(personal, PersonalState(s));
    }

    static async Task<Guid> PublishedTemplate(WorkSeenTestContext s)
    {
        var client = s.Factory.As(TestData.Admin); var civil = await s.Data.Discipline("Civil");
        var created = await (await client.Post("/api/v1/templates", new { name = "Synthetic assignment producer template" })).Json(201);
        var id = created.G("id");
        var structure = new TemplateEndpoints.StructureBody(null, [new(civil, true)], [],
            [new("d", civil, "Synthetic template deliverable", await s.Data.DeliverableType(), null, null, false, null)],
            [new("lead", civil, "d", "Synthetic lead task", null, false, "Medium", null, null, "DisciplineLead"),
             new("pm", civil, null, "Synthetic PM task", null, false, "Medium", null, null, "PM"),
             new("none", civil, null, "Synthetic unassigned task", null, false, "Medium", null, null, "Unassigned")],
            [], created.I("rowVersion"));
        var saved = await (await client.Put($"/api/v1/templates/{id}/structure", structure)).Json();
        await (await client.Post($"/api/v1/templates/{id}/publish", new { rowVersion = saved.I("rowVersion") })).Json();
        return id;
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Template_project_and_discipline_pack_publish_only_the_copied_items_actual_assignees(bool pack)
    {
        using var s = new WorkSeenTestContext(); var template = await PublishedTemplate(s);
        var civil = await s.Data.Discipline("Civil"); var marc = s.Data.User(TestData.Marc); var pm = s.Data.User(TestData.Pm);
        await PreparePersonalStateThenGoDark(s); var personal = PersonalState(s);
        Guid projectId;
        if (pack)
        {
            var p = await s.Data.Project(); projectId = p.Id;
            await (await s.Factory.As(TestData.Pm).Post($"/api/v1/projects/{projectId}/template-packs", new {
                templateId = template, disciplineId = civil, leadUserId = marc
            })).Json();
        }
        else
        {
            var project = await (await s.Factory.As(TestData.Pm).Post("/api/v1/projects", new {
                projectNumber = TestData.Number(), name = "Synthetic instantiated project", clientId = await s.Data.Client(), officeId = await s.Data.Office(),
                startDate = "2026-10-01", templateId = template, disciplines = new[] { new { disciplineId = civil, leadUserId = marc } }
            })).Json(201); projectId = project.G("id");
            // The template hook bumps project metadata after its initial create save, without
            // changing its PM. Its assignment still belongs to the exact original version zero.
            Published(s, new("Project", projectId), "ProjectManager", pm, TestData.Pm, sourceVersion: 0);
            Assert.Equal(1, Occurrences(s, new("Project", projectId)));
        }
        var tasks = s.Factory.Db(db => db.Tasks.AsNoTracking().Where(row => row.ProjectId == projectId && row.TemplateTaskId != null).ToArray());
        Assert.Equal(3, tasks.Length);
        foreach (var task in tasks)
        {
            var key = new WorkSeenKey("Task", task.Id);
            if (task.Name == "Synthetic unassigned task") { Assert.Null(task.AssigneeId); Assert.Equal(0, Occurrences(s, key)); }
            else
            {
                var expected = task.Name == "Synthetic lead task" ? marc : pm;
                Assert.Equal(expected, task.AssigneeId); Published(s, key, "Assignee", expected, TestData.Pm); Assert.Equal(1, Occurrences(s, key));
            }
        }
        var del = s.Factory.Db(db => db.Deliverables.AsNoTracking().Single(row => row.ProjectId == projectId && row.TemplateDeliverableId != null));
        Assert.Equal(marc, del.OwnerId); Published(s, new("Deliverable", del.Id), "Owner", marc, TestData.Pm);
        Assert.Equal(personal, PersonalState(s));
    }

    [Fact]
    public async Task Implemented_project_csv_import_publishes_primary_pm_in_the_import_transaction_without_private_writes()
    {
        using var s = new WorkSeenTestContext(); await s.Flag("project_import", true);
        var clientId = await s.Data.Client(); var officeId = await s.Data.Office(); var number = TestData.Number();
        var client = s.Factory.Db(db => db.Clients.Single(row => row.Id == clientId).Name);
        var office = s.Factory.Db(db => db.Offices.Single(row => row.Id == officeId).Name);
        static string Csv(string text) => "\"" + text.Replace("\"", "\"\"") + "\"";
        var csv = "project number,name,client,office,project manager email\n" +
            string.Join(',', new[] { number, "Synthetic imported assignment", client, office, TestData.Pm }.Select(Csv));
        await PreparePersonalStateThenGoDark(s); var personal = PersonalState(s);
        var admin = s.Factory.As(TestData.Admin);
        var before = s.Factory.Db(db => db.WorkAssignmentOccurrences.Count());
        await (await admin.Post("/api/v1/projects/import-preview", new { csv })).Json();
        Assert.Equal(before, s.Factory.Db(db => db.WorkAssignmentOccurrences.Count()));
        await (await admin.Post("/api/v1/projects/import", new { csv })).Json();
        var p = s.Factory.Db(db => db.Projects.AsNoTracking().Single(row => row.ProjectNumber == number));
        var key = new WorkSeenKey("Project", p.Id); var token = Published(s, key, "ProjectManager", s.Data.User(TestData.Pm), TestData.Admin);
        s.Factory.Db(db => {
            var occurrence = db.WorkAssignmentOccurrences.Single(row => row.Token == token);
            Assert.Equal("Import", db.ActivityLog.Single(row => row.Id == occurrence.SourceActivityId).Source); return 0;
        });
        var again = await (await admin.Post("/api/v1/projects/import", new { csv })).Json();
        Assert.Equal(0, again.I("creates")); Assert.Equal(1, again.I("exists")); Assert.Equal(1, Occurrences(s, key));
        Assert.Equal(personal, PersonalState(s));
    }

    [Fact]
    public async Task Action_conversion_risk_realisation_and_deliverable_from_task_publish_new_canonical_assignments()
    {
        using var s = new WorkSeenTestContext(); await s.Flag("small_ux", true);
        var p = await s.Data.Project(); var alex = s.Data.User(TestData.Alex);
        var pm = s.Factory.As(TestData.Pm); var civil = s.Data.ProjectDiscipline(p.Id, "Civil");
        var meeting = await (await pm.Post($"/api/v1/projects/{p.Id}/meetings/current", new { })).Json();
        var action = await (await pm.Post($"/api/v1/meetings/{meeting.G("id")}/actions", new { text = "Synthetic convert action", ownerType = "User", ownerUserId = alex })).Json(201);
        var risk = await (await pm.Post($"/api/v1/projects/{p.Id}/risks", new { title = "Synthetic realised risk", description = "Synthetic", probability = 2, impact = 2, ownerId = alex })).Json(201);
        await PreparePersonalStateThenGoDark(s); var personal = PersonalState(s);
        var actionKey = new WorkSeenKey("Action", action.G("id")); var riskKey = new WorkSeenKey("Risk", risk.G("id"));
        var actionOccurrences = Occurrences(s, actionKey); var riskOccurrences = Occurrences(s, riskKey);
        var converted = await (await pm.Post($"/api/v1/actions/{actionKey.ItemId}/convert", new {
            projectDisciplineId = civil, assigneeId = alex, rowVersion = s.Factory.Db(db => Version(db, actionKey))
        })).Json();
        var taskKey = new WorkSeenKey("Task", converted.G("taskId")); Published(s, taskKey, "Assignee", alex, TestData.Pm);
        var del = await Deliverable(s, p, alex, fromTask: taskKey.ItemId); Published(s, new("Deliverable", del.G("id")), "Owner", alex, TestData.Pm);
        Assert.True(s.Factory.Db(db => db.ItemLinks.Any(row => row.SourceType == "Task" && row.SourceId == taskKey.ItemId
            && row.TargetType == "Deliverable" && row.TargetId == del.G("id") && row.Relation == ItemRelation.Related)));
        var realised = await (await pm.Post($"/api/v1/risks/{riskKey.ItemId}/transition", new {
            toStatus = "Realised", rowVersion = s.Factory.Db(db => Version(db, riskKey)), issue = new {
                title = "Synthetic issue from risk", description = "Synthetic", ownerId = alex, severity = Impact.Low, projectDisciplineId = civil
            }
        })).Json();
        Published(s, new("Issue", realised.G("issueId")), "Owner", alex, TestData.Pm);
        Assert.Equal(actionOccurrences, Occurrences(s, actionKey)); Assert.Equal(riskOccurrences, Occurrences(s, riskKey));
        Assert.Equal(personal, PersonalState(s));
    }
}
