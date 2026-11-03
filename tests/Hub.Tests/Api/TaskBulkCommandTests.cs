using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Hub.Api.Data;
using Hub.Api.Features;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

/// S12-A provider evidence: one transaction/receipt, seven operations, current visibility and coalesced notice ACLs.
public sealed class TaskBulkCommandTests : IDisposable
{
    readonly HubFactory f = new(); readonly TestData d;
    public TaskBulkCommandTests() { d = new(f); }
    public void Dispose() => f.Dispose();
    const string Preview = "/api/v1/tasks/bulk/preview", Command = "/api/v1/tasks/bulk/commands";
    async Task Flag(bool on) => await (await f.As(TestData.Admin).Put("/api/v1/admin/settings/flag.work_screens", new { value = on })).Json();
    object Item(Guid project, JsonNode task, int? version = null) => new { taskId = task.G("id"), projectId = project, rowVersion = version ?? f.Db(db => db.Tasks.Single(t => t.Id == task.G("id")).RowVersion) };
    static object Intent(object[] items, string operation, object parameters, string? reason = "Synthetic bulk correction") => new { schemaVersion = 1, items, operation, @params = parameters, reason };
    string State() => f.Db(db => JsonSerializer.Serialize(new {
        Tasks = db.Tasks.OrderBy(t => t.Id).Select(t => new { t.Id, t.RowVersion, t.AssigneeId, t.Priority, t.StartDate, t.DueDate, t.Status, t.MilestoneId, t.DeliverableId, t.LastActivityAt }).ToArray(),
        Members = db.ProjectMembers.Count(), Follows = db.Follows.Count(), Activity = db.ActivityLog.Count(), Notices = db.Notifications.Count(), Emails = db.Emails.Count(),
        Receipts = db.TaskBulkCommandReceipts.Count(), GenericReceipts = db.Idempotency.Count(),
        Outbox = db.Outbox.OrderBy(row => row.Id).Select(row => new { row.Id, row.EventType, row.ProjectId, row.Payload, row.ProcessedAt }).ToArray(),
        Assignments = db.WorkAssignmentOccurrences.OrderBy(row => row.Token).Select(row => new { row.Token, row.ItemId, row.RecipientId, row.Role, row.SourceVersion, row.SourceActivityId, row.RecordedAt }).ToArray(),
        Offers = db.Set<TaskPickupOffer>().OrderBy(row => row.Id).Select(row => new { row.Id, row.TaskId, row.ClosedAt, row.CloseCause }).ToArray() }));

    async Task<Guid> ReviewedTarget(Project p)
    {
        var civil = d.ProjectDiscipline(p.Id, "Civil"); var jill = d.User(TestData.Jill);
        await f.DbAsync(async db => { db.ProjectMembers.Add(new ProjectMember { ProjectId = p.Id, UserId = jill, Roles = [ProjectRole.Reviewer], AddedAt = f.Clock.Now }); return await db.SaveChangesAsync(); });
        var deliverable = (await (await f.As(TestData.Marc).Post($"/api/v1/projects/{p.Id}/deliverables", new { name = "Synthetic reviewed target", projectDisciplineId = civil,
            deliverableTypeId = await d.DeliverableType(), ownerId = d.User(TestData.Alex), revision = "A", requiresReview = false })).Json(201)).G("id");
        var version = f.Db(db => db.Deliverables.Single(row => row.Id == deliverable).RowVersion);
        var source = (await (await f.As(TestData.Alex).Post($"/api/v1/projects/{p.Id}/source-revisions", new ChangeEndpoints.RegisterBody(Guid.NewGuid(), deliverable, version, civil,
            d.User(TestData.Alex), "Deliverable", "synthetic-bulk", "Synthetic reviewed target", "A", "https://example.test/bulk-A.pdf", "Synthetic author", "Synthetic scope", null, null, null, null, null, null))).Json()).G("id");
        var package = (await (await f.As(TestData.Marc).Post($"/api/v1/projects/{p.Id}/reviews", new ReviewEndpoints.CreateBody(Guid.NewGuid(), "Synthetic current review", "Check the synthetic target", civil,
            d.User(TestData.Marc), [source], [new(civil, jill, new(2026, 9, 18))], true, null))).Json()).G("id");
        await (await f.As(TestData.Marc).Post($"/api/v1/projects/{p.Id}/reviews/{package}/action", new ReviewEndpoints.ActionBody(Guid.NewGuid(),
            f.Db(db => db.ReviewPackages.Single(row => row.Id == package).RowVersion), "start", "Start the synthetic current review"))).Json();
        return deliverable;
    }

    [Theory]
    [InlineData("assign")][InlineData("setDeliverable")]
    public async Task Current_discipline_reviewer_conflicts_are_refused_per_row_in_preview_and_mixed_apply(string operation)
    {
        await Flag(true); var p = await d.Project(); var target = await ReviewedTarget(p);
        var conflicting = operation == "assign" ? await d.NewTask(p.Id, extra: new { assigneeId = d.User(TestData.Alex), deliverableId = target })
            : await d.NewTask(p.Id, extra: new { assigneeId = d.User(TestData.Jill) });
        var valid = await d.NewTask(p.Id, extra: new { assigneeId = d.User(TestData.Alex) });
        object parameters = operation == "assign" ? new { kind = operation, assigneeId = d.User(TestData.Jill) }
            : new { kind = operation, targets = new[] { new { projectId = p.Id, deliverableId = target } } };
        var intent = Intent([Item(p.Id, conflicting), Item(p.Id, valid)], operation, parameters);
        var before = State(); var preview = await (await f.As(TestData.Marc).Post(Preview, intent)).Json(); Assert.Equal(before, State());
        Assert.Equal(1, preview["counts"]!.I("eligible")); Assert.Equal(1, preview["counts"]!.I("refused"));
        var initialVersion = await d.TaskVersion(conflicting);
        var result = await (await f.As(TestData.Marc).Post(Command, new { requestId = Guid.NewGuid(), intent })).Json();
        Assert.Equal(1, result["counts"]!.I("changed")); Assert.Equal(1, result["counts"]!.I("refused")); Assert.Equal(initialVersion, await d.TaskVersion(conflicting));
        if (operation == "assign")
        {
            await (await f.As(TestData.Admin).Put("/api/v1/admin/settings/allow_self_review", new { value = true })).Json();
            await (await f.As(TestData.Marc).Patch($"/api/v1/tasks/{conflicting.G("id")}", new { assigneeId = d.User(TestData.Jill) }, await d.TaskVersion(conflicting))).Json();
            await (await f.As(TestData.Admin).Put("/api/v1/admin/settings/allow_self_review", new { value = false })).Json();
            var noop = await (await f.As(TestData.Marc).Post(Command, new { requestId = Guid.NewGuid(), intent = Intent([Item(p.Id, conflicting)], operation, parameters) })).Json();
            Assert.Equal(1, noop["counts"]!.I("unchanged")); Assert.Equal(0, noop["counts"]!.I("refused"));
        }
    }

    [Theory]
    [InlineData("assign")][InlineData("cancel")]
    public async Task Savehook_pickup_offer_closures_join_the_same_bulk_capture_and_receipt_without_duplicate_retry(string operation)
    {
        await Flag(true); await (await f.As(TestData.Admin).Put("/api/v1/admin/settings/flag.task_pickup", new { value = true })).Json();
        var p = await d.Project(); var alex = d.User(TestData.Alex);
        await f.DbAsync(async db => { db.NotificationPreferences.Add(new NotificationPreference { UserId = alex, EventType = NotificationEvents.TaskPickupClosed, InApp = true, Email = true }); return await db.SaveChangesAsync(); });
        var tasks = new[] { await d.NewTask(p.Id, extra: new { assigneeId = alex }), await d.NewTask(p.Id, extra: new { assigneeId = alex }) };
        foreach (var task in tasks) await (await f.As(TestData.Alex).Post($"/api/v1/tasks/{task.G("id")}/pickup-offer", new { reason = "Synthetic offer needs cover" })).Json(201);
        var beforeEmails = f.Db(db => db.Emails.Select(email => email.Id).ToArray()); var beforeNotices = f.Db(db => db.Notifications.Select(notice => notice.Id).ToArray());
        object parameters = operation == "assign" ? new { kind = operation, assigneeId = d.User(TestData.Jill) } : new { kind = operation };
        var body = new { requestId = Guid.NewGuid(), intent = Intent(tasks.Select(task => Item(p.Id, task)).ToArray(), operation, parameters) };
        var result = await (await f.As(TestData.Marc).Post(Command, body)).Json(); var command = result.G("commandId");
        Assert.Equal(2, result["counts"]!.I("changed"));
        var offers = f.Db(db => db.Set<TaskPickupOffer>().ToArray()); Assert.Equal(2, offers.Length);
        Assert.All(offers, offer => { Assert.NotNull(offer.ClosedAt); Assert.Equal(operation == "assign" ? TaskPickupOffer.ReassignedElsewhere : TaskPickupOffer.TaskClosed, offer.CloseCause); });
        var notices = f.Db(db => db.Notifications.Where(notice => !beforeNotices.Contains(notice.Id)).ToArray());
        var emails = f.Db(db => db.Emails.Where(email => !beforeEmails.Contains(email.Id)).ToArray());
        Assert.Single(notices.Where(notice => notice.UserId == alex)); Assert.Single(emails.Where(email => email.UserId == alex));
        Assert.All(notices.GroupBy(notice => notice.UserId), group => Assert.Single(group)); Assert.All(emails.GroupBy(email => email.UserId), group => Assert.Single(group));
        Assert.All(notices, notice => Assert.Equal("TaskBulkCommand", notice.ItemType));
        var receipt = JsonNode.Parse(f.Db(db => db.TaskBulkCommandReceipts.Single(row => row.Id == command).ResponseJson))!;
        Assert.Equal(notices.Select(n => n.Id).Order(), receipt["notificationIds"]!.AsArray().Select(id => id!.GetValue<Guid>()).Order());
        Assert.Equal(emails.Select(n => n.Id).Order(), receipt["emailIds"]!.AsArray().Select(id => id!.GetValue<Guid>()).Order());
        var taskActivity = receipt["rows"]!.AsArray().SelectMany(row => row!["activityIds"]!.AsArray().Select(id => id!.GetValue<Guid>()));
        var sideEffects = receipt["sideEffectActivityIds"]!.AsArray().Select(id => id!.GetValue<Guid>());
        Assert.Equal(f.Db(db => db.ActivityLog.Where(a => a.CorrelationId == command).Select(a => a.Id).OrderBy(id => id).ToArray()), taskActivity.Concat(sideEffects).Order());
        Assert.Equal(2, f.Db(db => db.ActivityLog.Count(a => a.CorrelationId == command && a.Action == "PickupClosed")));
        var state = State(); await (await f.As(TestData.Marc).Post(Command, body)).Json(); Assert.Equal(state, State());
    }

    [Fact]
    public async Task Zero_shift_is_a_terminal_noop_without_offer_outbox_or_assignment_publication_side_effects()
    {
        await Flag(true); await (await f.As(TestData.Admin).Put("/api/v1/admin/settings/flag.task_pickup", new { value = true })).Json();
        var p = await d.Project(); var task = await d.NewTask(p.Id, extra: new { assigneeId = d.User(TestData.Alex), startDate = "2026-09-20", dueDate = "2026-09-25" });
        await (await f.As(TestData.Alex).Post($"/api/v1/tasks/{task.G("id")}/pickup-offer", new { reason = "Synthetic cover" })).Json(201);
        var before = JsonNode.Parse(State())!.AsObject(); before.Remove("Receipts");
        var result = await (await f.As(TestData.Marc).Post(Command, new { requestId = Guid.NewGuid(), intent = Intent([Item(p.Id, task)], "shiftDueDates", new { kind = "shiftDueDates", days = 0 }) })).Json();
        Assert.Equal(1, result["counts"]!.I("unchanged")); Assert.Equal(0, result["counts"]!.I("changed"));
        var after = JsonNode.Parse(State())!.AsObject(); after.Remove("Receipts"); Assert.Equal(before.ToJsonString(), after.ToJsonString());
    }

    [Fact]
    public async Task Real_new_task_zero_version_is_valid_but_missing_negative_and_stale_versions_are_not_promoted()
    {
        await Flag(true); var p = await d.Project(); var task = await d.NewTask(p.Id);
        Assert.Equal(0, await d.TaskVersion(task));
        var result = await (await f.As(TestData.Marc).Post(Command, new { requestId = Guid.NewGuid(), intent = Intent([Item(p.Id, task)], "setPriority", new { kind = "setPriority", priority = "High" }) })).Json();
        Assert.Equal(1, result["counts"]!.I("changed")); Assert.Equal(1, result["rows"]![0]!.I("rowVersion"));
        var before = State();
        foreach (var bad in new[] { (object)new { taskId = task.G("id"), projectId = p.Id }, new { taskId = task.G("id"), projectId = p.Id, rowVersion = -1 } })
            await (await f.As(TestData.Marc).Post(Command, new { requestId = Guid.NewGuid(), intent = Intent([bad], "setPriority", new { kind = "setPriority", priority = "Low" }) })).Json(400);
        Assert.Equal(before, State());
        var stale = await (await f.As(TestData.Marc).Post(Command, new { requestId = Guid.NewGuid(), intent = Intent([Item(p.Id, task, 0)], "setPriority", new { kind = "setPriority", priority = "Low" }) })).Json();
        Assert.Equal(0, stale["counts"]!.I("changed")); Assert.Equal(1, stale["counts"]!.I("refused"));
        Assert.Equal("High", f.Db(db => db.Tasks.Single(row => row.Id == task.G("id")).Priority)); Assert.Equal(1, await d.TaskVersion(task));
    }

    [Fact]
    public async Task Eight_rows_three_projects_commit_six_changes_and_two_terminal_refusals_with_one_receipt_and_recipient_summaries()
    {
        await Flag(true); var projects = new[] { await d.Project(), await d.Project(), await d.Project() };
        var items = new List<object>(); var taskIds = new List<Guid>();
        foreach (var p in projects) for (var i = 0; i < 2; i++) { var task = await d.NewTask(p.Id, extra: new { assigneeId = d.User(TestData.Alex) }); items.Add(Item(p.Id, task)); taskIds.Add(task.G("id")); }
        foreach (var p in projects.Take(2)) { var task = await d.NewTask(p.Id, TestData.Pm, new { assigneeId = d.User(TestData.Alex) }, "Electrical"); items.Add(Item(p.Id, task)); }
        var recipients = new[] { d.User(TestData.Jill), d.User(TestData.Sam), d.User(TestData.Pm) };
        await f.DbAsync(async db => { foreach (var user in recipients) foreach (var e in new[] { NotificationEvents.TaskAssigned, NotificationEvents.AddedToProject, NotificationEvents.StaffAssignment, NotificationEvents.MemberAutoAdded })
            db.NotificationPreferences.Add(new NotificationPreference { UserId = user, EventType = e, InApp = true, Email = true }); return await db.SaveChangesAsync(); });
        var intent = Intent(items.ToArray(), "assign", new { kind = "assign", assigneeId = d.User(TestData.Jill) });
        var beforePreview = State(); var preview = await (await f.As(TestData.Marc).Post(Preview, intent)).Json(); Assert.Equal(beforePreview, State());
        Assert.Equal(6, preview["counts"]!.I("eligible")); Assert.Equal(2, preview["counts"]!.I("refused"));
        var body = new { requestId = Guid.NewGuid(), intent }; var answer = await (await f.As(TestData.Marc).Post(Command, body)).Json(); var command = answer.G("commandId");
        Assert.Equal(6, answer["counts"]!.I("changed")); Assert.Equal(2, answer["counts"]!.I("refused"));
        Assert.Equal(8, answer["rows"]!.AsArray().Count); Assert.Equal(1, f.Db(db => db.TaskBulkCommandReceipts.Count()));
        Assert.Equal(6, f.Db(db => db.ActivityLog.Count(a => a.CorrelationId == command && a.ItemType == ItemType.Task)));
        Assert.Equal(6, f.Db(db => db.Tasks.Count(t => taskIds.Contains(t.Id) && t.AssigneeId == d.User(TestData.Jill))));
        var notices = f.Db(db => db.Notifications.Where(n => n.CorrelationId == command).ToArray());
        var emails = f.Db(db => db.Emails.Where(e => e.DedupKey != null && e.DedupKey.Contains(command.ToString())).ToArray());
        foreach (var recipient in recipients)
        {
            var notice = Assert.Single(notices.Where(n => n.UserId == recipient)); var email = Assert.Single(emails.Where(n => n.UserId == recipient));
            Assert.Equal(3, notice.RequiredProjectIds!.Length); Assert.Equal(3, email.RequiredProjectIds.Length); Assert.Equal("/tasks", notice.LinkPath);
            foreach (var project in projects) { Assert.DoesNotContain(project.ProjectNumber, notice.Title); Assert.DoesNotContain(project.ProjectNumber, email.BodyText); }
        }
        var final = State(); var retry = await (await f.As(TestData.Marc).Post(Command, body)).Json(); Assert.Equal(answer.ToJsonString(), retry.ToJsonString()); Assert.Equal(final, State());
        await (await f.As(TestData.Marc).Post(Command, new { body.requestId, intent = Intent(items.ToArray(), "assign", new { kind = "assign", assigneeId = d.User(TestData.Alex) }) })).Json(422);
        Assert.Equal(final, State());
    }

    [Theory]
    [InlineData("assign")][InlineData("setDueDate")][InlineData("shiftDueDates")][InlineData("setPriority")][InlineData("setDeliverable")][InlineData("hold")][InlineData("cancel")]
    public async Task Every_toolbar_operation_uses_a_versioned_canonical_command(string operation)
    {
        await Flag(true); var p = await d.Project();
        var task = await d.NewTask(p.Id, extra: new { assigneeId = d.User(TestData.Alex), startDate = "2026-09-20", dueDate = "2026-09-25" });
        var deliverable = await (await f.As(TestData.Marc).Post($"/api/v1/projects/{p.Id}/deliverables", new { name = "Synthetic target", projectDisciplineId = d.ProjectDiscipline(p.Id, "Civil"), deliverableTypeId = await d.DeliverableType() })).Json(201);
        if (operation == "setDeliverable") await f.DbAsync(async db => { var milestone = new Milestone { ProjectId = p.Id, Seq = 801, Key = "SYN-M801", Name = "Synthetic previous milestone" }; db.Milestones.Add(milestone);
            (await db.Tasks.SingleAsync(t => t.Id == task.G("id"))).MilestoneId = milestone.Id; return await db.SaveChangesAsync(); });
        var originalVersion = await d.TaskVersion(task);
        object parameters = operation switch {
            "assign" => new { kind = operation, assigneeId = (Guid?)null }, "setDueDate" => new { kind = operation, dueDate = (string?)null },
            "shiftDueDates" => new { kind = operation, days = 2 }, "setPriority" => new { kind = operation, priority = "High" },
            "setDeliverable" => new { kind = operation, targets = new[] { new { projectId = p.Id, deliverableId = (Guid?)deliverable.G("id") } } },
            _ => new { kind = operation } };
        var body = new { requestId = Guid.NewGuid(), intent = Intent([Item(p.Id, task)], operation, parameters) };
        var reply = await (await f.As(TestData.Marc).Post(Command, body)).Json(); Assert.Equal(1, reply["counts"]!.I("changed"));
        var row = f.Db(db => db.Tasks.Single(t => t.Id == task.G("id")));
        Assert.Equal(originalVersion + 1, row.RowVersion);
        switch (operation)
        {
            case "assign": Assert.Null(row.AssigneeId); break;
            case "setDueDate": Assert.Null(row.DueDate); Assert.Equal(1, row.DueDateChangeCount); break;
            case "shiftDueDates": Assert.Equal(new DateOnly(2026, 9, 22), row.StartDate); Assert.Equal(new DateOnly(2026, 9, 27), row.DueDate); break;
            case "setPriority": Assert.Equal("High", row.Priority); break;
            case "setDeliverable": Assert.Equal(deliverable.G("id"), row.DeliverableId); Assert.Null(row.MilestoneId); break;
            case "hold": Assert.Equal(TaskStatuses.OnHold, row.Status); Assert.Equal(TaskStatuses.NotStarted, row.PreviousStatus); break;
            case "cancel": Assert.Equal(TaskStatuses.Cancelled, row.Status); Assert.Equal(TaskStatuses.NotStarted, row.PreviousStatus); break;
        }
        Assert.Null(reply["undo"]); Assert.Single(reply["rows"]![0]!["activityIds"]!.AsArray());
    }

    [Fact]
    public async Task Deliverable_mapping_order_is_canonical_but_input_row_order_is_significant()
    {
        await Flag(true); var first = await d.Project(); var second = await d.Project();
        var a = await d.NewTask(first.Id); var b = await d.NewTask(second.Id); var items = new[] { Item(first.Id, a), Item(second.Id, b) };
        var targets = new[] { new { projectId = first.Id, deliverableId = (Guid?)null }, new { projectId = second.Id, deliverableId = (Guid?)null } };
        var requestId = Guid.NewGuid(); var client = f.As(TestData.Marc);
        var original = await (await client.Post(Command, new { requestId, intent = Intent(items, "setDeliverable", new { kind = "setDeliverable", targets }) })).Json();
        var before = State();
        var reordered = await (await client.Post(Command, new { requestId, intent = Intent(items, "setDeliverable", new { kind = "setDeliverable", targets = targets.Reverse().ToArray() }) })).Json();
        Assert.Equal(original.ToJsonString(), reordered.ToJsonString()); Assert.Equal(before, State());
        Assert.Matches("^[0-9a-f]{64}$", f.Db(db => db.TaskBulkCommandReceipts.Single().IntentHash));
        await (await client.Post(Command, new { requestId, intent = Intent(items.Reverse().ToArray(), "setDeliverable", new { kind = "setDeliverable", targets }) })).Json(422);
        Assert.Equal(before, State());
    }

    [Fact]
    public async Task Keyed_preview_is_write_free_and_stale_noop_and_refused_results_never_revive()
    {
        await Flag(true); var p = await d.Project(); var a = await d.NewTask(p.Id); var b = await d.NewTask(p.Id, TestData.Pm, discipline: "Electrical");
        var intent = Intent([Item(p.Id, a), Item(p.Id, b)], "setPriority", new { kind = "setPriority", priority = "Medium" });
        var client = f.As(TestData.Marc); client.DefaultRequestHeaders.Add("Idempotency-Key", "Synthetic preview key"); var before = State();
        var preview = await (await client.Post(Preview, intent)).Json(); Assert.Equal(before, State()); Assert.Equal(1, preview["counts"]!.I("unchanged")); Assert.Equal(1, preview["counts"]!.I("refused"));
        var body = new { requestId = Guid.NewGuid(), intent }; var result = await (await client.Post(Command, body)).Json();
        await f.DbAsync(async db => { (await db.Tasks.SingleAsync(t => t.Id == a.G("id"))).Priority = "High";
            (await db.ProjectDisciplines.SingleAsync(pd => pd.Id == d.ProjectDiscipline(p.Id, "Electrical"))).LeadUserId = d.User(TestData.Marc); return await db.SaveChangesAsync(); });
        var state = State(); var repeated = await (await client.Post(Command, body)).Json(); Assert.Equal(result.ToJsonString(), repeated.ToJsonString()); Assert.Equal(state, State());
        var freshPreview = await (await client.Post(Preview, intent)).Json(); Assert.Equal(1, freshPreview["counts"]!.I("refused")); Assert.Equal(1, freshPreview["counts"]!.I("unchanged"));
        Assert.Equal(state, State());
    }

    [Fact]
    public async Task Replay_uses_current_visibility_without_requiring_lost_assignment_rights_and_hidden_rows_have_no_true_outcome()
    {
        await Flag(true); var p = await d.Project(); var task = await d.NewTask(p.Id, extra: new { assigneeId = d.User(TestData.Alex) });
        var body = new { requestId = Guid.NewGuid(), intent = Intent([Item(p.Id, task)], "assign", new { kind = "assign", assigneeId = d.User(TestData.Diane) }) };
        var sam = f.As(TestData.Sam); var result = await (await sam.Post(Command, body)).Json(); Assert.Equal(1, result["counts"]!.I("changed"));
        var after = State(); Assert.Equal(result.ToJsonString(), (await (await sam.Post(Command, body)).Json()).ToJsonString()); Assert.Equal(after, State());
        await f.DbAsync(async db => { (await db.Projects.SingleAsync(x => x.Id == p.Id)).Visibility = Visibility.Restricted;
            foreach (var m in await db.ProjectMembers.Where(m => m.ProjectId == p.Id && m.UserId == d.User(TestData.Sam)).ToListAsync()) m.RemovedAt = f.Clock.GetUtcNow(); return await db.SaveChangesAsync(); });
        var hidden = await (await sam.Post(Command, body)).Json(); Assert.Equal(0, hidden["counts"]!.I("changed")); Assert.Equal(1, hidden["counts"]!.I("unavailable"));
        Assert.Equal(new[] { "index", "outcome" }, hidden["rows"]![0]!.AsObject().Select(property => property.Key)); Assert.Equal("unavailable", hidden["rows"]![0]!.S("outcome"));
        // Another actor's same request ID is its own new command; it cannot recover Sam's opaque receipt.
        var foreign = await (await f.As(TestData.Pm).Post(Command, body)).Json(); Assert.NotEqual(result.G("commandId"), foreign.G("commandId"));
        await Flag(false); await (await sam.Post(Command, body)).Json(404);
    }

    [Fact]
    public async Task Provider_failure_after_changes_rolls_back_the_whole_attempt_and_identical_retry_commits_once()
    {
        await Flag(true); var p = await d.Project(); var task = await d.NewTask(p.Id, extra: new { assigneeId = d.User(TestData.Alex) });
        var body = new { requestId = Guid.NewGuid(), intent = Intent([Item(p.Id, task)], "assign", new { kind = "assign", assigneeId = d.User(TestData.Jill) }) };
        await f.DbAsync(db => db.Database.ExecuteSqlRawAsync("CREATE FUNCTION hub.fail_bulk_receipt() RETURNS trigger LANGUAGE plpgsql AS $$BEGIN RAISE EXCEPTION 'Synthetic bulk failure'; END$$; CREATE TRIGGER fail_bulk_receipt BEFORE INSERT ON hub.task_bulk_command_receipt FOR EACH ROW EXECUTE FUNCTION hub.fail_bulk_receipt()"));
        var before = State(); await (await f.As(TestData.Marc).Post(Command, body)).Json(500); Assert.Equal(before, State());
        await f.DbAsync(db => db.Database.ExecuteSqlRawAsync("DROP TRIGGER fail_bulk_receipt ON hub.task_bulk_command_receipt; DROP FUNCTION hub.fail_bulk_receipt()"));
        var client = f.As(TestData.Marc); var replies = await Task.WhenAll(client.Post(Command, body), client.Post(Command, body));
        var first = await replies[0].Json(); var second = await replies[1].Json(); Assert.Equal(first.ToJsonString(), second.ToJsonString());
        Assert.Equal(1, f.Db(db => db.TaskBulkCommandReceipts.Count())); Assert.Equal(1, first["counts"]!.I("changed"));
    }

    [Fact]
    public async Task Complete_reasons_foreign_references_stale_versions_and_envelope_bounds_store_only_permitted_rows()
    {
        await Flag(true); var p = await d.Project(); var other = await d.Project();
        var a = await d.NewTask(p.Id); var b = await d.NewTask(p.Id);
        var intent = Intent([Item(p.Id, a, 999), Item(p.Id, b)], "setPriority", new { kind = "setPriority", priority = "High" });
        var stale = await (await f.As(TestData.Marc).Post(Command, new { requestId = Guid.NewGuid(), intent })).Json(); Assert.Equal(1, stale["counts"]!.I("changed")); Assert.Equal(1, stale["counts"]!.I("refused"));
        var foreign = await (await f.As(TestData.Marc).Post(Command, new { requestId = Guid.NewGuid(), intent = Intent([Item(p.Id, a)], "setDeliverable",
            new { kind = "setDeliverable", targets = new[] { new { projectId = p.Id, deliverableId = Guid.NewGuid() } } }) })).Json(); Assert.Equal(1, foreign["counts"]!.I("refused"));
        var wrongProject = await (await f.As(TestData.Marc).Post(Command, new { requestId = Guid.NewGuid(), intent = Intent([Item(other.Id, a)], "hold", new { kind = "hold" }) })).Json();
        Assert.Equal(new[] { "index", "outcome" }, wrongProject["rows"]![0]!.AsObject().Select(property => property.Key));
        await f.DbAsync(async db => { (await db.Projects.SingleAsync(x => x.Id == p.Id)).Status = ProjectStatus.Complete; return await db.SaveChangesAsync(); });
        var refused = await (await f.As(TestData.Pm).Post(Command, new { requestId = Guid.NewGuid(), intent = Intent([Item(p.Id, a)], "setPriority", new { kind = "setPriority", priority = "High" }, null) })).Json(); Assert.Equal(1, refused["counts"]!.I("refused"));
        var allowed = await (await f.As(TestData.Pm).Post(Command, new { requestId = Guid.NewGuid(), intent = Intent([Item(p.Id, a)], "setPriority", new { kind = "setPriority", priority = "High" }, new string('r', 3001)) })).Json(); Assert.Equal(1, allowed["counts"]!.I("changed"));
        var before = State();
        foreach (var invalid in new[] { Intent([Item(p.Id, a), Item(p.Id, a)], "hold", new { kind = "hold" }),
            Intent(Enumerable.Range(0, 201).Select(_ => (object)new { taskId = Guid.NewGuid(), projectId = p.Id, rowVersion = 1 }).ToArray(), "hold", new { kind = "hold" }),
            Intent([Item(p.Id, a)], "transition", new { kind = "hold" }) })
            await (await f.As(TestData.Pm).Post(Command, new { requestId = Guid.NewGuid(), intent = invalid })).Json(400);
        Assert.Equal(before, State());
    }

    [Fact]
    public async Task New_summary_requires_all_projects_before_paging_count_pulse_and_mark_read_while_null_legacy_rows_remain_visible()
    {
        var p = await d.Project(); var other = await d.Project(); var recipient = d.User(TestData.Diane); var summary = Guid.NewGuid(); var legacy = Guid.NewGuid();
        await f.DbAsync(async db => { db.Notifications.AddRange(new Notification { Id = summary, UserId = recipient, EventType = NotificationEvents.TaskAssigned,
            RequiredProjectIds = [p.Id, other.Id], Title = "Synthetic summary", CreatedAt = f.Clock.GetUtcNow(), UpdatedAt = f.Clock.GetUtcNow() },
            new Notification { Id = legacy, UserId = recipient, EventType = NotificationEvents.TaskAssigned, Title = "Synthetic legacy", CreatedAt = f.Clock.GetUtcNow(), UpdatedAt = f.Clock.GetUtcNow() }); return await db.SaveChangesAsync(); });
        var client = f.As(TestData.Diane); var beforeCount = (await (await client.GetAsync("/api/v1/me/notifications/unread-count")).Json()).I("notifications");
        await f.DbAsync(async db => { (await db.Projects.SingleAsync(x => x.Id == other.Id)).Visibility = Visibility.Restricted; return await db.SaveChangesAsync(); });
        var page = await (await client.GetAsync("/api/v1/me/notifications?pageSize=1")).Json(); Assert.Equal(legacy, page["items"]![0]!.G("id")); Assert.Equal(1, page.I("totalCount"));
        Assert.Equal(beforeCount - 1, (await (await client.GetAsync("/api/v1/me/notifications/unread-count")).Json()).I("notifications"));
        var pulse = await (await client.GetAsync("/api/v1/me/notifications/pulse")).Json(); Assert.StartsWith("1:", pulse.S("stamp"));
        var marked = await (await client.Post("/api/v1/me/notifications/read", new { ids = new[] { summary, legacy } })).Json(); Assert.Equal(1, marked.I("marked"));
        Assert.Null(f.Db(db => db.Notifications.Single(n => n.Id == summary).ReadAt)); Assert.NotNull(f.Db(db => db.Notifications.Single(n => n.Id == legacy).ReadAt));
    }

    [Fact]
    public async Task Actual_supervisor_summary_is_excluded_from_digest_build_and_queued_body_after_any_contributing_project_is_hidden()
    {
        await Flag(true); var p = await d.Project(); var other = await d.Project();
        var a = await d.NewTask(p.Id, extra: new { assigneeId = d.User(TestData.Alex) }); var b = await d.NewTask(other.Id, extra: new { assigneeId = d.User(TestData.Alex) });
        var sam = d.User(TestData.Sam);
        await f.DbAsync(async db => { foreach (var project in new[] { p.Id, other.Id }) db.Follows.Add(new ProjectFollow { ProjectId = project, UserId = sam,
            Level = FollowLevel.AllActivity, Source = FollowSource.Manual, CreatedAt = f.Clock.Now.AddDays(-1), UpdatedAt = f.Clock.Now, LastSeenAt = f.Clock.Now.AddDays(-1) }); return await db.SaveChangesAsync(); });
        var reply = await (await f.As(TestData.Marc).Post(Command, new { requestId = Guid.NewGuid(), intent = Intent([Item(p.Id, a), Item(other.Id, b)], "assign", new { kind = "assign", assigneeId = d.User(TestData.Jill) }) })).Json();
        var command = reply.G("commandId");
        var summary = f.Db(db => db.Notifications.Single(n => n.UserId == sam && n.CorrelationId == command && n.EventType == NotificationEvents.StaffAssignment));
        Assert.Equal(2, summary.RequiredProjectIds!.Length); Assert.Null(summary.ProjectId);
        var legacy = Guid.NewGuid(); const string legacyTitle = "Synthetic legacy staffing change";
        await f.DbAsync(async db => { db.Notifications.Add(new Notification { Id = legacy, UserId = sam, EventType = NotificationEvents.StaffAssignment,
            Title = legacyTitle, LinkPath = "/tasks", CreatedAt = f.Clock.GetUtcNow(), UpdatedAt = f.Clock.GetUtcNow() }); return await db.SaveChangesAsync(); });
        var off = Digest.SectionCodes.Where(code => code != "staff" && code != "updates").ToHashSet();
        Task<Digest.Result?> Build() => f.DbAsync(db => Digest.Build(db, sam, "Synthetic supervisor", new DateOnly(2026, 9, 14), f.Clock.Now.AddDays(-1), f.Clock.Now, new OrgSettings(), "https://example.test", off));
        var before = Assert.IsType<Digest.Result>(await Build()); Assert.Contains(before.Sections.Single(s => s.Code == "staff").Rows, row => row.ItemId == summary.Id);
        var followingBefore = await (await f.As(TestData.Sam).GetAsync($"/api/v1/me/following?projectId={p.Id}")).Json();
        var originalGroup = followingBefore["items"]!.AsArray().Single(item => item!["entry"]!["correlationId"]?.GetValue<Guid>() == command)!;
        Assert.True(originalGroup["notified"]!.GetValue<bool>()); Assert.False(originalGroup["unread"]!.GetValue<bool>());
        var originalUpdateCount = before.ProjectUpdates.SingleOrDefault(update => update.ProjectNumber == p.ProjectNumber)?.Count ?? 0;
        await f.DbAsync(async db => { (await db.Projects.SingleAsync(x => x.Id == other.Id)).Visibility = Visibility.Restricted;
            var pref = await db.UserSettings.SingleOrDefaultAsync(setting => setting.UserId == sam);
            if (pref is null) { pref = new UserSetting { UserId = sam }; db.UserSettings.Add(pref); }
            pref.DigestEnabled = true; pref.DigestTimeLocal = "00:00"; pref.DigestSectionsOff = JsonSerializer.Serialize(off); pref.LastDigestAt = null; return await db.SaveChangesAsync(); });
        var after = Assert.IsType<Digest.Result>(await Build()); var staff = after.Sections.Single(s => s.Code == "staff");
        Assert.DoesNotContain(staff.Rows, row => row.ItemId == summary.Id); Assert.Contains(staff.Rows, row => row.ItemId == legacy);
        Assert.DoesNotContain(summary.Title, after.Body); Assert.Contains(legacyTitle, after.Body); Assert.DoesNotContain(other.Id, after.RequiredProjectIds);
        var followingAfter = await (await f.As(TestData.Sam).GetAsync($"/api/v1/me/following?projectId={p.Id}")).Json();
        var visibleGroup = followingAfter["items"]!.AsArray().Single(item => item!["entry"]!["correlationId"]?.GetValue<Guid>() == command)!;
        Assert.False(visibleGroup["notified"]!.GetValue<bool>()); Assert.True(visibleGroup["unread"]!.GetValue<bool>());
        Assert.True(after.ProjectUpdates.Single(update => update.ProjectNumber == p.ProjectNumber).Count > originalUpdateCount, "Hidden summaries cannot suppress visible-project digest updates by correlation");
        await f.RunJob<DigestJob>();
        var queued = f.Db(db => db.Emails.Single(email => email.UserId == sam && email.Kind == "Digest"));
        Assert.DoesNotContain(summary.Title, queued.BodyText); Assert.Contains(legacyTitle, queued.BodyText); Assert.DoesNotContain(other.Id, queued.RequiredProjectIds);
        Assert.DoesNotContain(other.ProjectNumber, queued.BodyText);
    }
}
