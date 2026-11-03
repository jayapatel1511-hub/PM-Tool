using Hub.Api.Data;
using Hub.Api.Features;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

public sealed class WorkSeenEligibilityTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Created_query_with_current_reviewer_role_is_eligible_without_mine_or_review_stage_membership(bool complete)
    {
        using var s = new WorkSeenTestContext(); await s.Enable(); var p = await s.Data.Project(); await s.Visit();
        var task = await s.Data.NewTask(p.Id, TestData.Alex, new { assigneeId = s.Data.User(TestData.Jill),
            reviewerId = s.Data.User(TestData.Alex), requiresReview = false, dueDate = "2026-09-15" });
        if (complete) { await s.Data.Move(TestData.Pm, task, "In Progress"); await s.Data.Move(TestData.Pm, task, "Complete"); }
        var query = $"/api/v1/tasks?createdBy={s.Data.User(TestData.Alex)}&status={(complete ? "Complete" : "Not%20Started")}";
        var created = await (await s.Factory.As(TestData.Alex).GetAsync(query)).Json();
        Assert.Contains(created["items"]!.AsArray(), row => row!.G("id") == task.G("id"));
        Assert.False(s.Factory.Db(db => db.Collaborators.Any(row => row.TaskId == task.G("id") && row.UserId == s.Data.User(TestData.Alex))));
        var key = new WorkSeenKey("Task", task.G("id"));
        Assert.Equal("New", (await s.Read([key.ItemId]))[key].State);
        await s.MarkAll();
        Assert.Equal("Seen", (await s.Read([key.ItemId]))[key].State);
    }

    [Theory]
    [InlineData("expiry")] [InlineData("escalation")] [InlineData("ended")] [InlineData("wrong-kind")] [InlineData("wrong-rule")]
    public async Task Sole_attention_arm_obeys_actual_snooze_visibility_until_expiry_escalation_or_typed_mismatch(string branch)
    {
        using var s = new WorkSeenTestContext(); await s.Enable(); var p = await s.Data.Project(); await s.Visit();
        var issue = await (await s.Factory.As(TestData.Pm).Post($"/api/v1/projects/{p.Id}/issues",
            new { title = "Sole attention eligibility", description = "Synthetic", severity = Impact.High,
                ownerId = s.Data.User(TestData.Alex) })).Json(201);
        var id = issue.G("id"); await s.Factory.Evaluate(p.Id);
        var attention = s.Factory.Db(db => db.Attention.Single(row => row.ItemType == "Issue" && row.ItemId == id && row.RuleId == "A-07").Id);
        var snooze = await (await s.Factory.As(TestData.Pm).Post($"/api/v1/attention/{attention}/snooze",
            new { days = 1, note = "Synthetic snooze eligibility check" })).Json();
        var token = s.Factory.Db(db => db.WorkAssignmentOccurrences.Single(row => row.ItemType == "Issue" && row.ItemId == id).Token);
        Assert.False(s.Factory.Db(db => db.Watchers.Any(row => row.ItemType == "Issue" && row.ItemId == id && row.UserId == s.Data.User(TestData.Alex))));
        var hidden = await (await s.Factory.As(TestData.Alex).GetAsync("/api/v1/me/work")).Json();
        Assert.DoesNotContain(hidden["attention"]!["items"]!.AsArray(), row => row!.G("itemId") == id);
        Assert.Empty(await s.Read([id], type: "Issue")); await s.MarkAll();
        Assert.False(s.Factory.Db(db => db.WorkSeenAcknowledgements.Any(row => row.Token == token)));
        if (branch == "expiry") s.Factory.Clock.Now = s.Factory.Clock.Now.AddDays(2);
        else await s.Factory.DbAsync(async db => {
            // Explicit historical/typed snooze fixtures exercise the reader's exact matching branches.
            // Canonical assignment, evaluator attention and snooze creation above used normal commands.
            var row = await db.Snoozes.SingleAsync(row => row.Id == snooze.G("id"));
            if (branch == "escalation") row.SeverityAtSnooze = Severity.Warning;
            if (branch == "ended") row.EndedAt = s.Factory.Clock.Now;
            if (branch == "wrong-kind") row.ItemType = "Risk";
            if (branch == "wrong-rule") row.RuleId = "A-08";
            return await db.SaveChangesAsync();
        });
        var shown = await (await s.Factory.As(TestData.Alex).GetAsync("/api/v1/me/work")).Json();
        Assert.Contains(shown["attention"]!["items"]!.AsArray(), row => row!.G("itemId") == id);
        Assert.Equal("New", (await s.Read([id], type: "Issue"))[new("Issue", id)].State);
        await s.MarkAll();
        Assert.True(s.Factory.Db(db => db.WorkSeenAcknowledgements.Any(row => row.Token == token)));
    }
    [Theory]
    [InlineData(2, true)] [InlineData(25, false)]
    public async Task NonTask_requested_closed_age_keeps_its_real_fourteen_day_rule(int daysOld, bool eligible)
    {
        using var s = new WorkSeenTestContext(); await s.Enable(); await s.Flag("my_work_follow_up", true);
        var now = s.Factory.Clock.Now; s.Factory.Clock.SetDate(2026, 8, 1);
        var p = await s.Data.Project(); await s.Visit(); s.Factory.Clock.SetDate(2026, 8, 2);
        var decision = await (await s.Factory.As(TestData.Alex).Post($"/api/v1/projects/{p.Id}/decisions",
            new { subject = "Requested age fixture", description = "Synthetic", requiredByDate = "2026-10-01",
                impactLevel = Impact.Low, impactDescription = "Synthetic impact for the requested-item eligibility fixture.", ownerUserId = s.Data.User(TestData.Alex) })).Json(201);
        s.Factory.Clock.Now = now.AddDays(-daysOld);
        await s.Factory.DbAsync(async db => {
            // Temporal status fixture uses the real audited-save producer; assignment was created normally.
            var row = await db.Decisions.SingleAsync(row => row.Id == decision.G("id"));
            row.Status = "Decided"; row.StatusChangedAt = s.Factory.Clock.Now;
            return await db.SaveChangesAsync();
        });
        s.Factory.Clock.Now = now;
        var key = new WorkSeenKey("Decision", decision.G("id"));
        Assert.Equal(eligible, (await s.Read([key.ItemId], type: "Decision")).ContainsKey(key));
        var token = s.Factory.Db(db => db.WorkAssignmentOccurrences.Single(row => row.ItemType == "Decision" && row.ItemId == key.ItemId).Token);
        await s.MarkAll();
        Assert.Equal(eligible, s.Factory.Db(db => db.WorkSeenAcknowledgements.Any(row => row.Token == token)));
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Arbitrary_age_Completed_mine_rows_with_a_direct_role_clear_in_shared_MarkAll(bool collaboratorReviewer)
    {
        using var s = new WorkSeenTestContext(); await s.Enable(); var current = s.Factory.Clock.Now;
        s.Factory.Clock.SetDate(2026, 5, 1); var p = await s.Data.Project(); await s.Visit(); s.Factory.Clock.Now = s.Factory.Clock.Now.AddDays(1);
        var task = await s.Data.NewTask(p.Id, extra: new { assigneeId = s.Data.User(collaboratorReviewer ? TestData.Jill : TestData.Alex),
            reviewerId = collaboratorReviewer ? s.Data.User(TestData.Alex) : (Guid?)null, requiresReview = false });
        if (collaboratorReviewer)
            await (await s.Factory.As(TestData.Pm).Post($"/api/v1/tasks/{task.G("id")}/collaborators", new { userId = s.Data.User(TestData.Alex) })).Json(204);
        await s.Data.Move(TestData.Pm, task, "In Progress"); await s.Data.Move(TestData.Pm, task, "Complete");
        s.Factory.Clock.Now = current;
        var rows = await (await s.Factory.As(TestData.Alex).GetAsync("/api/v1/tasks?mine=true&status=Complete&completedFrom=2026-01-01&completedTo=2026-12-31")).Json();
        Assert.Contains(rows["items"]!.AsArray(), row => row!.G("id") == task.G("id"));
        var key = new WorkSeenKey("Task", task.G("id"));
        Assert.Equal("New", (await s.Read([key.ItemId]))[key].State);
        await s.MarkAll();
        Assert.Equal("Seen", (await s.Read([key.ItemId]))[key].State);
    }

    [Fact]
    public async Task Collaborator_creator_and_readable_unwatched_Risk_do_not_become_assignments_or_MarkAll_targets()
    {
        using var s = new WorkSeenTestContext(); await s.Enable(); var p = await s.Data.Project(); await s.Visit();
        var task = await s.Data.NewTask(p.Id, TestData.Alex, new { assigneeId = s.Data.User(TestData.Jill) });
        await (await s.Factory.As(TestData.Pm).Post($"/api/v1/tasks/{task.G("id")}/collaborators", new { userId = s.Data.User(TestData.Alex) })).Json(204);
        Assert.Empty(await s.Read([task.G("id")]));
        var risk = await (await s.Factory.As(TestData.Pm).Post($"/api/v1/projects/{p.Id}/risks",
            new { title = "Unreadable-by-stream is not New", description = "Synthetic", probability = 2, impact = 2, ownerId = s.Data.User(TestData.Alex) })).Json(201);
        Assert.Empty(await s.Read([risk.G("id")], type: "Risk"));
        var token = s.Factory.Db(db => db.WorkAssignmentOccurrences.Single(row => row.ItemType == "Risk" && row.ItemId == risk.G("id")).Token);
        await s.MarkAll();
        Assert.False(s.Factory.Db(db => db.WorkSeenAcknowledgements.Any(row => row.Token == token)));
        await s.Flag("my_work_follow_up", true);
        await (await s.Factory.As(TestData.Alex).Post($"/api/v1/items/Risk/{risk.G("id")}/watchers", new { userId = s.Data.User(TestData.Alex) })).Json(204);
        Assert.Equal("New", (await s.Read([risk.G("id")], type: "Risk"))[new("Risk", risk.G("id"))].State);
        await s.MarkAll();
        Assert.Equal("Seen", (await s.Read([risk.G("id")], type: "Risk"))[new("Risk", risk.G("id"))].State);
    }

    [Fact]
    public async Task Action_discriminator_switch_back_creates_new_occurrence_but_discipline_fallback_is_not_person_assignment()
    {
        using var s = new WorkSeenTestContext(); await s.Enable(); var p = await s.Data.Project(); await s.Visit();
        var meeting = await (await s.Factory.As(TestData.Pm).Post($"/api/v1/projects/{p.Id}/meetings/current", new { })).Json();
        var action = await (await s.Factory.As(TestData.Pm).Post($"/api/v1/meetings/{meeting.G("id")}/actions",
            new { text = "Discriminated assignment", ownerType = "User", ownerUserId = s.Data.User(TestData.Alex) })).Json(201);
        var id = action.G("id");
        int Version() => s.Factory.Db(db => db.Actions.Single(row => row.Id == id).RowVersion);
        var old = s.Factory.Db(db => db.WorkAssignmentOccurrences.Single(row => row.ItemType == "Action" && row.ItemId == id).Token);
        await (await s.Factory.As(TestData.Pm).Patch("/api/v1/actions/" + id, new { ownerType = ActionOwnerType.Discipline,
            ownerUserId = (Guid?)null, ownerDisciplineId = s.Data.ProjectDiscipline(p.Id, "Civil") }, Version())).Json();
        Assert.Empty(await s.Read([id], type: "Action"));
        await (await s.Factory.As(TestData.Pm).Patch("/api/v1/actions/" + id, new { ownerType = ActionOwnerType.User,
            ownerUserId = s.Data.User(TestData.Alex), ownerDisciplineId = (Guid?)null }, Version())).Json();
        var state = (await s.Read([id], type: "Action"))[new("Action", id)];
        Assert.Equal("New", state.State); Assert.DoesNotContain(state.ObservedTokens, row => row.Token == old);
    }

    [Fact]
    public async Task Detail_bound_at_old_version_never_receives_a_future_away_back_token()
    {
        using var s = new WorkSeenTestContext(); await s.Enable(); var p = await s.Data.Project(); await s.Visit();
        var task = await s.Data.NewTask(p.Id, extra: new { assigneeId = s.Data.User(TestData.Alex) }); var id = task.G("id");
        var old = s.Latest(id);
        var bound = await s.Factory.DbAsync(db => Task.FromResult(Hub.Api.Infrastructure.WorkAssignmentPublication.Bound(db, db.Tasks.AsNoTracking().Single(row => row.Id == id))));
        await s.Assign(id, s.Data.User(TestData.Jill)); await s.Assign(id, s.Data.User(TestData.Alex));
        var later = s.Latest(id);
        var read = await s.Factory.DbAsync(async db => {
            var me = new Hub.Api.Infrastructure.CurrentUser { Id = s.Data.User(TestData.Alex), IsActive = true, Resolved = true };
            var org = await db.CurrentSettings();
            return await WorkSeenEligibility.ReadCurrentAsync(db, new(db, me), org,
                new(me.Id, Hub.Api.Infrastructure.Clock.LocalDate(s.Factory.Clock.Now, org), s.Factory.Clock.Now), [bound]);
        });
        Assert.Contains(read[new("Task", id)], token => token.Token == old);
        Assert.DoesNotContain(read[new("Task", id)], token => token.Token == later);
    }
}
