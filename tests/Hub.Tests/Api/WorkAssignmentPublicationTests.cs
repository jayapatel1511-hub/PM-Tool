using System.Text.Json.Nodes;
using Hub.Api.Data;
using Hub.Api.Features;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

public sealed class WorkAssignmentPublicationTests
{
    [Theory]
    [InlineData("Task", 2)] [InlineData("Deliverable", 2)] [InlineData("Decision", 1)] [InlineData("Action", 1)]
    [InlineData("Risk", 1)] [InlineData("Issue", 1)] [InlineData("Handoff", 2)] [InlineData("ChangeNotice", 1)] [InlineData("Project", 1)]
    public async Task Closed_map_binds_every_role_to_exact_create_version_zero_and_source_event(string kind, int expected)
    {
        using var s = new WorkSeenTestContext(); var p = await s.Data.Project(); var recipient = s.Data.User(TestData.Alex);
        await s.Factory.DbAsync(db => {
            Entity entity = kind switch {
                "Task" => new WorkTask { ProjectId = p.Id, AssigneeId = recipient, ReviewerId = recipient },
                "Deliverable" => new Deliverable { ProjectId = p.Id, OwnerId = recipient, ReviewerId = recipient },
                "Decision" => new Decision { ProjectId = p.Id, OwnerUserId = recipient },
                "Action" => new MeetingAction { ProjectId = p.Id, OwnerType = ActionOwnerType.User, OwnerUserId = recipient },
                "Risk" => new Risk { ProjectId = p.Id, OwnerId = recipient },
                "Issue" => new Issue { ProjectId = p.Id, OwnerId = recipient },
                "Handoff" => new Handoff { ProjectId = p.Id, SendingOwnerId = recipient, ReceivingOwnerId = recipient },
                "ChangeNotice" => new ChangeNotice { ProjectId = p.Id, OwnerId = recipient },
                _ => new Project { ProjectManagerId = recipient }
            };
            // Isolated publication contract probe: no synthetic incomplete canonical entity is saved.
            var entry = db.Add(entity); var audit = (IAuditable)entity;
            var source = new ActivityLog { ItemType = kind, ItemId = entity.Id, ProjectId = audit.AuditProjectId, OccurredAt = s.Factory.Clock.Now };
            WorkAssignmentPublication.Publish(db, entry, source, s.Factory.Clock.Now);
            var rows = db.WorkAssignmentOccurrences.Local.Where(row => row.ItemId == entity.Id && row.ItemType == kind).ToArray();
            Assert.Equal(expected, rows.Length);
            Assert.All(rows, row => { Assert.Equal(0, row.SourceVersion); Assert.Equal(recipient, row.RecipientId);
                Assert.Equal(source.Id, row.SourceActivityId); Assert.Equal(source.OccurredAt, row.SourceOccurredAt); Assert.Equal("Published", row.Provenance); });
            WorkAssignmentPublication.Publish(db, entry, source, s.Factory.Clock.Now);
            Assert.Equal(expected, db.WorkAssignmentOccurrences.Local.Count(row => row.ItemId == entity.Id && row.ItemType == kind));
            return Task.FromResult(0);
        });
    }

    [Fact]
    public async Task Nine_normal_create_commands_publish_the_twelve_actual_roles()
    {
        using var s = new WorkSeenTestContext(); var f = s.Factory; var data = s.Data; var p = await data.Project();
        var civil = data.ProjectDiscipline(p.Id, "Civil"); var electrical = data.ProjectDiscipline(p.Id, "Electrical");
        var alex = data.User(TestData.Alex); var marc = data.User(TestData.Marc); var omar = data.User(TestData.Omar);
        async Task<JsonNode> Post(string path, object body, int status = 201, string who = TestData.Pm) =>
            await (await f.As(who).Post(path, body)).Json(status);
        var task = await data.NewTask(p.Id, extra: new { assigneeId = alex, reviewerId = marc });
        var deliverable = await Post($"/api/v1/projects/{p.Id}/deliverables", new { name = "Assignment matrix source", projectDisciplineId = civil,
            deliverableTypeId = await data.DeliverableType(), ownerId = alex, reviewerId = marc, revision = "A", requiresReview = false });
        var decision = await Post($"/api/v1/projects/{p.Id}/decisions", new { subject = "Matrix decision", description = "Synthetic",
            requiredByDate = "2026-10-01", impactLevel = Impact.All[0], impactDescription = "Synthetic impact for the assignment publication matrix.", ownerUserId = alex });
        var meeting = await Post($"/api/v1/projects/{p.Id}/meetings/current", new { }, 200);
        var action = await Post($"/api/v1/meetings/{meeting.G("id")}/actions", new { text = "Matrix action", ownerType = "User", ownerUserId = alex });
        var risk = await Post($"/api/v1/projects/{p.Id}/risks", new { title = "Matrix risk", description = "Synthetic", probability = 2, impact = 2, ownerId = alex });
        var issue = await Post($"/api/v1/projects/{p.Id}/issues", new { title = "Matrix issue", description = "Synthetic", severity = Impact.All[0], ownerId = alex });
        var target = await data.NewTask(p.Id, TestData.Omar, new { assigneeId = omar }, "Electrical");
        var dv = f.Db(db => db.Deliverables.Single(row => row.Id == deliverable.G("id")).RowVersion);
        var handoff = await Post($"/api/v1/projects/{p.Id}/handoffs", new HandoffEndpoints.DraftBody(Guid.NewGuid(), "Matrix handoff",
            deliverable.G("id"), dv, "A", "https://example.test/matrix-A.pdf", electrical, alex, omar, target.G("id"), null,
            "Use the survey", "Check extents", new(2026, 10, 1), new(2026, 10, 2), null, null));
        ChangeEndpoints.RegisterBody Registration(string revision, Guid? old = null) => new(Guid.NewGuid(), deliverable.G("id"), dv,
            civil, alex, "Deliverable", "matrix", "Matrix source", revision, "https://example.test/" + revision + ".pdf",
            "Synthetic", "Corridor", null, old, old is null ? null : f.Db(db => db.SourceHeads.Single(head => head.ProjectId == p.Id).RowVersion),
            old is null ? null : "Synthetic revision change", old is null ? null : new DateOnly(2026, 9, 14), old is null ? null : new DateOnly(2026, 10, 1));
        var revision = await Post($"/api/v1/projects/{p.Id}/source-revisions", Registration("A"), 200, TestData.Alex);
        var change = await Post($"/api/v1/projects/{p.Id}/source-revisions", Registration("B", revision.G("id")), 200, TestData.Alex);
        var expected = new Dictionary<WorkSeenKey, string[]> {
            [new("Project", p.Id)] = ["ProjectManager"], [new("Task", task.G("id"))] = ["Assignee", "Reviewer"],
            [new("Deliverable", deliverable.G("id"))] = ["Owner", "Reviewer"], [new("Decision", decision.G("id"))] = ["InternalOwner"],
            [new("Action", action.G("id"))] = ["InternalOwner"], [new("Risk", risk.G("id"))] = ["Owner"],
            [new("Issue", issue.G("id"))] = ["Owner"], [new("Handoff", handoff.G("id"))] = ["ReceivingOwner", "SendingOwner"],
            [new("ChangeNotice", change.G("id"))] = ["Owner"]
        };
        f.Db(db => {
            foreach (var (key, roles) in expected)
            {
                var rows = db.WorkAssignmentOccurrences.Where(row => row.ItemType == key.ItemType && row.ItemId == key.ItemId).ToArray();
                Assert.Equal(roles.Order(), rows.Select(row => row.Role).Order());
                foreach (var occurrence in rows)
                {
                    var source = db.ActivityLog.Single(log => log.Id == occurrence.SourceActivityId);
                    Assert.Equal(key.ItemType, source.ItemType); Assert.Equal(key.ItemId, source.ItemId);
                    Assert.Equal(source.OccurredAt, occurrence.SourceOccurredAt); Assert.Equal("Published", occurrence.Provenance);
                }
            }
            return 0;
        });
    }

    [Fact]
    public async Task Noop_comment_stage_and_refused_stale_commands_publish_no_assignment_and_unrelated_versions_still_resolve()
    {
        using var s = new WorkSeenTestContext(); await s.Enable(); var p = await s.Data.Project(); await s.Visit();
        var task = await s.Data.NewTask(p.Id, extra: new { assigneeId = s.Data.User(TestData.Alex), reviewerId = s.Data.User(TestData.Marc), requiresReview = true });
        var id = task.G("id"); var token = s.Latest(id);
        var count = s.Factory.Db(db => db.WorkAssignmentOccurrences.Count(row => row.ItemType == "Task" && row.ItemId == id));
        await s.Assign(id, s.Data.User(TestData.Alex));
        await (await s.Factory.As(TestData.Alex).Post($"/api/v1/items/Task/{id}/comments", new { body = "Synthetic comment, not an assignment" })).Json(201);
        await s.Data.Move(TestData.Alex, task, "In Progress");
        await s.Data.Move(TestData.Alex, task, "Ready for Review", new { reviewerId = s.Data.User(TestData.Marc) });
        await s.Data.Move(TestData.Marc, task, "In Review");
        await (await s.Factory.As(TestData.Pm).Patch("/api/v1/tasks/" + id, new { assigneeId = s.Data.User(TestData.Jill) }, 99999)).Json(409);
        Assert.Equal(count, s.Factory.Db(db => db.WorkAssignmentOccurrences.Count(row => row.ItemType == "Task" && row.ItemId == id)));
        var state = (await s.Read([id]))[new("Task", id)];
        Assert.Equal("New", state.State); Assert.Contains(state.ObservedTokens, observed => observed.Token == token);
    }
}
