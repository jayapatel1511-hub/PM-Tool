using System.Text.Json.Nodes;
using Hub.Api.Data;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

[Collection("api")]
public sealed class DateContextTests(HubFactory f)
{
    readonly TestData data = new(f);
    HttpClient Admin => f.As(TestData.Admin);
    static string Url(string type, Guid id) => $"/api/v1/items/{type}/{id}/date-context";

    async Task Flag(bool on, Func<Task> check)
    {
        var previous = (await (await Admin.GetAsync("/api/v1/me")).Json())["flags"]!["date_context"]!.GetValue<bool>();
        try { await (await Admin.Put("/api/v1/admin/settings/flag.date_context", new { value = on })).Json(); await check(); }
        finally { await (await Admin.Put("/api/v1/admin/settings/flag.date_context", new { value = previous })).Json(); }
    }

    async Task<Deliverable> Deliverable(Project project, string key, DateOnly? due, Guid? milestone = null)
    {
        var item = new Deliverable { ProjectId = project.Id, ProjectDisciplineId = data.ProjectDiscipline(project.Id, "Civil"),
            DeliverableTypeId = await data.DeliverableType(), Key = key, Name = key, DueDate = due, MilestoneId = milestone, LastActivityAt = f.Clock.Now };
        await f.DbAsync(async db => { item.Seq = await db.Deliverables.CountAsync(d => d.ProjectId == project.Id) + 1; db.Deliverables.Add(item); return await db.SaveChangesAsync(); }); return item;
    }

    [Fact, Trait("Spec", "AC-INT-01"), Trait("flag-off", "date_context")]
    public async Task Off_returns_not_found_and_existing_dependency_read_is_unchanged()
    {
        var p = await data.Project(); var t = await data.NewTask(p.Id); var url = $"/api/v1/tasks/{t.G("id")}/dependencies";
        var before = await (await Admin.GetAsync(url)).Json();
        await Flag(false, async () => {
            foreach (var type in new[] { ItemType.Task, ItemType.Deliverable }) await (await Admin.GetAsync(Url(type, t.G("id")))).Json(404);
            Assert.True(JsonNode.DeepEquals(before, await (await Admin.GetAsync(url)).Json()));
        });
    }

    [Fact, Trait("Spec", "AC-INT-09"), Trait("Spec", "AC-INT-10"), Trait("flag-on", "date_context")]
    public async Task Task_bounds_own_flags_and_new_successors_match_evaluation_without_a_write()
    {
        var p = await data.Project(); var due = new DateOnly(2026, 10, 15); var parent = await Deliverable(p, "PARENT", due.AddDays(4));
        var a = await data.NewTask(p.Id, extra: new { startDate = due, dueDate = due, deliverableId = parent.Id });
        var b = await data.NewTask(p.Id, extra: new { startDate = due.AddDays(2), dueDate = due.AddDays(8) });
        var c = await data.NewTask(p.Id, extra: new { startDate = due.AddDays(-1), dueDate = due.AddDays(8) });
        foreach (var child in new[] { b, c }) await (await Admin.Post($"/api/v1/tasks/{a.G("id")}/dependencies", new { successorTaskId = child.G("id"), lagDays = 2 })).Json(201);
        var version = await data.TaskVersion(a); var logs = f.Db(db => db.ActivityLog.Count(l => l.ItemId == a.G("id")));
        await Flag(true, async () => {
            var response = await (await Admin.GetAsync(Url(ItemType.Task, b.G("id")))).Json(); var dates = response["dates"]!;
            Assert.Equal("2026-10-17", dates["earliestStart"]!.S("date")); Assert.Equal("2026-10-17", dates["earliestDue"]!.S("date"));
            Assert.Contains(a.S("key"), dates["earliestStart"]!["reasons"]![0]!.S("text"));
            var proposed = await (await Admin.GetAsync(Url(ItemType.Task, a.G("id")) + "?dueDate=2026-10-21")).Json();
            Assert.Equal("2026-10-19", proposed["dates"]!["latestDue"]!.S("date"));
            Assert.Equal("T-04", proposed["dates"]!["inconsistencies"]![0]!.S("rule"));
            var successors = proposed["dates"]!["successors"]!.AsArray(); Assert.Equal(b.G("id"), successors.Single()!.G("id"));
            Assert.Contains(a.S("key"), successors[0]!["reason"]!.S("text"));
            var emptyDate = await (await Admin.GetAsync(Url(ItemType.Task, a.G("id")) + "?dueDate=")).Json();
            Assert.Empty(emptyDate["dates"]!["inconsistencies"]!.AsArray()); Assert.Empty(emptyDate["dates"]!["successors"]!.AsArray());
            await (await Admin.GetAsync(Url(ItemType.Task, a.G("id")) + "?dueDate=definitely-not-a-date")).Json(400);
            Assert.Equal(version, await data.TaskVersion(a)); Assert.Equal(logs, f.Db(db => db.ActivityLog.Count(l => l.ItemId == a.G("id"))));
            await f.Evaluate(p.Id);
            Assert.False(f.Db(db => db.TaskStates.Single(s => s.TaskId == a.G("id")).IsDateInconsistent));
            Assert.False(f.Db(db => db.TaskStates.Single(s => s.TaskId == b.G("id")).IsDateInconsistent));
            Assert.True(f.Db(db => db.TaskStates.Single(s => s.TaskId == c.G("id")).IsDateInconsistent));
        });
    }

    [Fact, Trait("Spec", "AC-INT-10")]
    public async Task Deliverable_milestone_limit_and_explicit_links_use_the_evaluators_rules()
    {
        var p = await data.Project(); var date = new DateOnly(2026, 10, 15);
        var milestone = new Milestone { ProjectId = p.Id, Key = "M-LIMIT", Name = "Deadline", Date = date.AddDays(4) };
        await f.DbAsync(async db => { db.Milestones.Add(milestone); return await db.SaveChangesAsync(); });
        var a = await Deliverable(p, "DL-A", date); var b = await Deliverable(p, "DL-B", date.AddDays(1), milestone.Id);
        await f.DbAsync(async db => { (await db.Deliverables.SingleAsync(d => d.Id == b.Id)).StartDate = date; return await db.SaveChangesAsync(); });
        await (await Admin.Post($"/api/v1/deliverables/{a.Id}/dependencies", new { successorDeliverableId = b.Id, lagDays = 2 })).Json(201);
        await Flag(true, async () => {
            var body = await (await Admin.GetAsync(Url(ItemType.Deliverable, b.Id) + "?dueDate=2026-10-21")).Json(); var dates = body["dates"]!;
            Assert.Equal("2026-10-17", dates["earliestStart"]!.S("date")); Assert.Null(dates["earliestDue"]);
            Assert.Equal("2026-10-19", dates["latestDue"]!.S("date"));
            Assert.Equal(new[] { "DL-03", "D-12" }, dates["inconsistencies"]!.AsArray().Select(r => r!.S("rule")));
            await f.Evaluate(p.Id);
            var evaluated = JsonNode.Parse(f.Db(db => db.DeliverableStates.Single(s => s.DeliverableId == b.Id).InconsistencyDetail))!;
            Assert.True(JsonNode.DeepEquals(evaluated[0], dates["inconsistencies"]![1]));
            await f.DbAsync(async db => { (await db.Milestones.SingleAsync(m => m.Id == milestone.Id)).IsCancelled = true; return await db.SaveChangesAsync(); });
            var cancelled = await (await Admin.GetAsync(Url(ItemType.Deliverable, b.Id))).Json(); Assert.Null(cancelled["dates"]!["latestDue"]);
        });
    }

    [Theory, Trait("Spec", "AC-INT-11")]
    [InlineData("Leave", true)]
    [InlineData("PartialLeave", false)]
    [InlineData("Holiday", true)]
    [InlineData("Schedule", true)]
    [InlineData("Override", true)]
    public async Task Availability_uses_the_planner_day_rule_and_is_absent_outside_scope(string cause, bool unavailable)
    {
        var monday = new DateOnly(2026, 9, 14); var office = new Office { Name = "Synthetic date context " + Guid.NewGuid() };
        var person = new AppUser { Email = $"synthetic-date-{Guid.NewGuid():N}@hub.test", DisplayName = "Synthetic date person", OfficeId = office.Id, SupervisorId = data.User(TestData.Sam) };
        await f.DbAsync(async db => {
            db.AddRange(office, person);
            if (cause is "Leave" or "PartialLeave") db.ApprovedLeaves.Add(new() { PersonId = person.Id, SourceSystem = "Private synthetic HR", ExternalId = $"private-hr-reference-{person.Id}", FromDate = monday, ThroughDate = monday, Status = "Approved", HoursPerDay = cause == "PartialLeave" ? 4 : null });
            if (cause == "Holiday") db.Holidays.Add(new() { OfficeId = office.Id, Date = monday, Name = "Private synthetic holiday" });
            if (cause == "Schedule") db.PersonSchedules.Add(new() { PersonId = person.Id, FromDate = monday, WeeklyHours = 32, WorkingDays = [2, 3, 4, 5] });
            if (cause == "Override") db.AvailabilityOverrides.Add(new() { PersonId = person.Id, WorkDate = monday, AvailableHours = 0, Category = "Unavailable" });
            return await db.SaveChangesAsync();
        });
        var p = await data.Project(); var predecessor = await data.NewTask(p.Id, extra: new { dueDate = monday }); var t = await data.NewTask(p.Id, extra: new { startDate = monday, dueDate = monday.AddDays(1) });
        await f.DbAsync(async db => { (await db.Tasks.SingleAsync(task => task.Id == t.G("id"))).AssigneeId = person.Id; return await db.SaveChangesAsync(); });
        await (await Admin.Post($"/api/v1/tasks/{predecessor.G("id")}/dependencies", new { successorTaskId = t.G("id") })).Json(201);
        await Flag(true, async () => {
            foreach (var client in new[] { Admin, f.As(TestData.Sam), f.As(person.Email) })
            {
                var body = await (await client.GetAsync(Url(ItemType.Task, t.G("id")))).Json();
                Assert.Equal("2026-09-14", body["dates"]!["earliestStart"]!.S("date"));
                var warnings = body["availability"]!.AsArray(); Assert.Equal(unavailable ? 1 : 0, warnings.Count);
                if (unavailable) { Assert.Equal("2026-09-14", warnings[0]!.S("date")); Assert.Equal("2026-09-15", warnings[0]!.S("nextAvailableDate")); Assert.Equal("not available on that date", warnings[0]!.S("message")); }
                Assert.DoesNotContain("private", body.ToJsonString(), StringComparison.OrdinalIgnoreCase);
                if (unavailable) Assert.Equal(new[] { "date", "message", "nextAvailableDate" }, warnings[0]!.AsObject().Select(pair => pair.Key).Order());
            }
            foreach (var email in new[] { TestData.Rita, TestData.Alex })
            {
                var body = await (await f.As(email).GetAsync(Url(ItemType.Task, t.G("id")))).Json(); Assert.False(body.AsObject().ContainsKey("availability"));
                Assert.DoesNotContain("leave", body.ToJsonString(), StringComparison.OrdinalIgnoreCase);
            }
        });
    }

    [Fact, Trait("Spec", "AC-INT-11")]
    public async Task Availability_has_no_next_date_beyond_the_horizon_and_also_uses_deliverable_owner()
    {
        var p = await data.Project(); var owner = data.User(TestData.Alex); var d = await Deliverable(p, "D-OWNER", new DateOnly(2027, 9, 18));
        await f.DbAsync(async db => { (await db.Deliverables.SingleAsync(row => row.Id == d.Id)).OwnerId = owner; return await db.SaveChangesAsync(); });
        await Flag(true, async () => {
            var response = await (await Admin.GetAsync(Url(ItemType.Deliverable, d.Id))).Json(); var warning = response["availability"]!.AsArray().Single()!;
            Assert.Equal("2027-09-18", warning.S("date")); Assert.Null(warning["nextAvailableDate"]);
        });
    }

    [Fact, Trait("Spec", "AC-INT-09")]
    public async Task Missing_unreadable_deleted_and_unsupported_items_are_not_found()
    {
        var p = await data.Project(); var t = await data.NewTask(p.Id); var d = await Deliverable(p, "D-PRIVATE", null);
        await f.DbAsync(async db => { (await db.Projects.SingleAsync(row => row.Id == p.Id)).Visibility = Visibility.Restricted; return await db.SaveChangesAsync(); });
        await Flag(true, async () => {
            var client = f.As(TestData.Rita); var missing = await (await client.GetAsync(Url(ItemType.Task, Guid.NewGuid()))).Json(404);
            foreach (var (type, id) in new[] { (ItemType.Task, t.G("id")), (ItemType.Deliverable, d.Id), (ItemType.Milestone, d.Id) })
            {
                var response = await (await client.GetAsync(Url(type, id))).Json(404); Assert.Equal(missing.S("code"), response.S("code"));
            }
            await f.DbAsync(async db => { (await db.Tasks.SingleAsync(row => row.Id == t.G("id"))).DeletedAt = f.Clock.Now; return await db.SaveChangesAsync(); });
            await (await Admin.GetAsync(Url(ItemType.Task, t.G("id")))).Json(404);
        });
    }

    [Fact, Trait("Spec", "AC-INT-13")]
    public async Task Deliverable_drag_lists_open_children_after_the_new_due_with_T04_reasons_without_writing()
    {
        var p = await data.Project(); var due = new DateOnly(2026, 10, 15); var deliverable = await Deliverable(p, "D-DRAG", due);
        var before = await data.NewTask(p.Id, extra: new { deliverableId = deliverable.Id, dueDate = due.AddDays(-2) });
        var at = await data.NewTask(p.Id, extra: new { deliverableId = deliverable.Id, dueDate = due });
        var after = await data.NewTask(p.Id, extra: new { deliverableId = deliverable.Id, dueDate = due.AddDays(2) });
        var complete = await data.NewTask(p.Id, extra: new { deliverableId = deliverable.Id, dueDate = due.AddDays(3) });
        await f.DbAsync(async db => { (await db.Tasks.SingleAsync(t => t.Id == complete.G("id"))).Status = TaskStatuses.Complete; return await db.SaveChangesAsync(); });
        await Flag(true, async () => {
            var version = f.Db(db => db.Deliverables.Single(d => d.Id == deliverable.Id).RowVersion);
            var response = await (await Admin.GetAsync(Url(ItemType.Deliverable, deliverable.Id) + "?dueDate=2026-10-14")).Json();
            var tasks = response["tasks"]!.AsArray(); Assert.Equal(new[] { at.G("id"), after.G("id") }, tasks.Select(t => t!.G("id")));
            Assert.All(tasks, t => { Assert.Equal("T-04", t!["reason"]!.S("rule")); Assert.Contains("D-DRAG", t["reason"]!.S("text")); Assert.Contains("2026-10-14", t["reason"]!.S("text")); });
            var cleared = await (await Admin.GetAsync(Url(ItemType.Deliverable, deliverable.Id) + "?dueDate=")).Json(); Assert.Empty(cleared["tasks"]!.AsArray());
            var taskRead = await (await Admin.GetAsync(Url(ItemType.Task, before.G("id")) + "?dueDate=2026-10-16")).Json();
            Assert.False(taskRead.AsObject().ContainsKey("tasks")); Assert.Equal("T-04", taskRead["dates"]!["newInconsistencies"]!.AsArray().Single()!.S("rule"));
            Assert.Equal(version, f.Db(db => db.Deliverables.Single(d => d.Id == deliverable.Id).RowVersion));
            Assert.Equal(due, f.Db(db => db.Deliverables.Single(d => d.Id == deliverable.Id).DueDate));
        });
    }
}
