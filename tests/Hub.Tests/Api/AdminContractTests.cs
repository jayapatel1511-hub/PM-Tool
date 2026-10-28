using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Hub.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

/// 040-S15-Admin: the Admin routes' typed records must serialise to the bytes the Administration screens already read
/// (names, order, nulls written, status codes and Location headers). Written against the anonymous objects and the
/// entities they replace, so each assertion passed before the conversion.
[Collection("api")]
public sealed class AdminContractTests(HubFactory f)
{
    static string[] Keys(JsonNode? n) => [.. n!.AsObject().Select(x => x.Key)];

    /// What every reference entry carries after its own columns, in the order the entity has always serialised them.
    static readonly string[] Shared = ["name", "isActive", "sortOrder", "auditType", "auditProjectId", "auditKey", "auditName", "createdAt", "createdBy", "updatedAt", "updatedBy", "rowVersion", "id"];

    [Fact]
    public async Task Reference_lists_and_commands_keep_their_names_order_nulls_and_answers()
    {
        var admin = f.As(TestData.Admin);
        var civil = await new TestData(f).Discipline("Civil");
        (await admin.Put("/api/v1/admin/settings/flag.commitment_reliability", new { value = true })).EnsureSuccessStatusCode();
        try
        {
            // kind, the entity's type name (its audit key), the kind's own columns, a create body, a patch body and what the patch does to those columns
            var kinds = new (string Kind, string Entity, string[] Own, object Create, object Patch)[]
            {
                ("disciplines", "Discipline", ["code", "colour"], new { name = "Pin discipline", code = "PIN", colour = "#112233", sortOrder = 90 }, new { name = "Pin discipline 2", code = "PI2", colour = "#445566", isActive = false, sortOrder = 91 }),
                ("clients", "Client", ["shortName"], new { name = "Pin client", shortName = "PC", sortOrder = 90 }, new { name = "Pin client 2", shortName = "P2", isActive = false, sortOrder = 91 }),
                ("offices", "Office", ["code", "timeZone"], new { name = "Pin office", code = "PNO", timeZone = "America/Toronto", sortOrder = 90 }, new { name = "Pin office 2", code = "PN2", timeZone = "America/Halifax", isActive = false, sortOrder = 91 }),
                ("deliverableTypes", "DeliverableType", ["defaultDisciplineId"], new { name = "Pin type", defaultDisciplineId = civil, sortOrder = 90 }, new { name = "Pin type 2", defaultDisciplineId = (Guid?)null, isActive = false, sortOrder = 91 }),
                ("phases", "Phase", [], new { name = "Pin phase", sortOrder = 90 }, new { name = "Pin phase 2", isActive = false, sortOrder = 91 }),
                ("projectTypes", "ProjectType", [], new { name = "Pin project type", sortOrder = 90 }, new { name = "Pin project type 2", isActive = false, sortOrder = 91 }),
                ("commitmentMissCategories", "CommitmentMissCategory", [], new { name = "Pin miss category", sortOrder = 90 }, new { name = "Pin miss category 2", isActive = false, sortOrder = 91 }),
            };
            foreach (var (kind, entity, own, create, patch) in kinds)
            {
                string[] expected = [.. own, .. Shared];
                var url = $"/api/v1/admin/{kind}";

                // The list: the kind's columns first and the shared ones after, every null written, in sort order.
                var seeded = (await admin.GetAsync(url).Result.Json()).AsArray();
                Assert.NotEmpty(seeded);
                Assert.All(seeded, row => Assert.Equal(expected, Keys(row)));
                Assert.Equal(seeded.OrderBy(r => r!.I("sortOrder")).ThenBy(r => r!.S("name"), StringComparer.Ordinal).Select(r => r!.G("id")), seeded.Select(r => r!.G("id")));
                Assert.All(seeded, row => { Assert.Equal("ReferenceData", row!.S("auditType")); Assert.Equal(entity, row.S("auditKey")); Assert.Equal(row.S("name"), row.S("auditName")); Assert.Null(row["auditProjectId"]); });

                // Create: 201 with the new entry's location, the same record as the list, and the kind's own values.
                var made = await admin.Post(url, create);
                var row1 = await made.Json(201);
                Assert.Equal(expected, Keys(row1));
                Assert.Equal($"{url}/{row1.S("id")}", made.Headers.Location!.OriginalString);
                Assert.Equal((90, true, 0, "ReferenceData", entity), (row1.I("sortOrder"), row1["isActive"]!.GetValue<bool>(), row1.I("rowVersion"), row1.S("auditType"), row1.S("auditKey")));
                Assert.Equal(row1.S("name"), row1.S("auditName"));
                Assert.Matches(@"^\d{4}-\d{2}-\d{2}T", row1.S("createdAt"));
                Assert.Equal(row1.S("createdAt"), row1.S("updatedAt"));
                Assert.NotNull(row1["createdBy"]);
                Assert.Contains(row1.G("id"), (await admin.GetAsync(url).Result.Json()).AsArray().Select(r => r!.G("id")));
                foreach (var column in own) Assert.True(row1.AsObject().ContainsKey(column), column);
                Assert.Equal(HttpStatusCode.BadRequest, (await admin.Post(url, new { name = " " })).StatusCode);
                Assert.Equal(HttpStatusCode.Forbidden, (await f.As(TestData.Pm).Post(url, create)).StatusCode);

                // Usage: the number of projects that use it, unknown ids included.
                var usage = await admin.GetAsync($"{url}/{row1.S("id")}/usage").Result.Json();
                Assert.Equal(["projects"], Keys(usage)); Assert.Equal(0, usage.I("projects"));
                Assert.Equal(0, (await admin.GetAsync($"{url}/{Guid.NewGuid()}/usage").Result.Json()).I("projects"));
                Assert.Equal(HttpStatusCode.Forbidden, (await f.As(TestData.Pm).GetAsync($"{url}/{row1.S("id")}/usage")).StatusCode);

                // Patch: 200 with the whole record again at the next row version; a stale version and an unknown entry are refused.
                var patched = await admin.Patch($"{url}/{row1.S("id")}", patch, row1.I("rowVersion")).Result.Json();
                Assert.Equal(expected, Keys(patched));
                Assert.Equal((row1.G("id"), 91, false, row1.I("rowVersion") + 1), (patched.G("id"), patched.I("sortOrder"), patched["isActive"]!.GetValue<bool>(), patched.I("rowVersion")));
                Assert.Equal(patched.S("name"), patched.S("auditName")); Assert.StartsWith("Pin ", patched.S("name")); Assert.EndsWith(" 2", patched.S("name"));
                Assert.Equal(row1.S("createdAt"), patched.S("createdAt"));
                Assert.Equal("concurrency_conflict", (await (await admin.Patch($"{url}/{row1.S("id")}", new { name = "Stale" }, row1.I("rowVersion"))).Json(409)).S("code"));
                Assert.Equal(HttpStatusCode.NotFound, (await admin.Patch($"{url}/{Guid.NewGuid()}", new { name = "Nobody" }, 1)).StatusCode);
                Assert.Equal(HttpStatusCode.Forbidden, (await f.As(TestData.Pm).Patch($"{url}/{row1.S("id")}", new { name = "Not allowed" }, patched.I("rowVersion"))).StatusCode);
            }

            // The kind's own values land where the page reads them.
            var offices = (await admin.GetAsync("/api/v1/admin/offices").Result.Json()).AsArray();
            var toronto = offices.Single(r => r!.S("name") == "Pin office 2")!;
            Assert.Equal(("PN2", "America/Halifax"), (toronto.S("code"), toronto.S("timeZone")));
            var type = (await admin.GetAsync("/api/v1/admin/deliverableTypes").Result.Json()).AsArray().Single(r => r!.S("name") == "Pin type 2")!;
            Assert.True(type.AsObject().ContainsKey("defaultDisciplineId")); Assert.Null(type["defaultDisciplineId"]);
            var shortName = (await admin.Post("/api/v1/admin/clients", new { name = "Pin client no short" }).Result.Json(201));
            Assert.True(shortName.AsObject().ContainsKey("shortName")); Assert.Null(shortName["shortName"]);
            Assert.Equal("timeZone", (await admin.Post("/api/v1/admin/offices", new { name = "Pin bad zone", code = "PBZ", timeZone = "Mars/Olympus" }).Result.Json(400))["errors"]!.AsObject().Single().Key);
        }
        finally { (await admin.Put("/api/v1/admin/settings/flag.commitment_reliability", new { value = false })).EnsureSuccessStatusCode(); }
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync("/api/v1/admin/commitmentMissCategories")).StatusCode); // the flag is off again
    }

    [Fact]
    public async Task Holidays_keep_their_row_names_order_nulls_and_empty_answers()
    {
        var admin = f.As(TestData.Admin);
        var office = await new TestData(f).Office();

        // Create: 201 with a Location and only the id; an organisation-wide day has no office.
        var org = await admin.Post("/api/v1/admin/holidays", new { date = "2037-07-01", name = "Pin org day" });
        var orgRow = await org.Json(201);
        Assert.Equal(["id"], Keys(orgRow));
        Assert.Equal($"/api/v1/admin/holidays/{orgRow.S("id")}", org.Headers.Location!.OriginalString);
        var local = await admin.Post("/api/v1/admin/holidays", new { officeId = office, date = "2037-07-02", name = "Pin office day" }).Result.Json(201);
        Assert.Equal(["id"], Keys(local));

        // The list: id, office id, office name, date and name; nulls written for the organisation's own day.
        var all = (await admin.GetAsync("/api/v1/admin/holidays?year=2037").Result.Json()).AsArray();
        Assert.Equal([orgRow.G("id"), local.G("id")], all.Select(r => r!.G("id"))); // by date
        Assert.All(all, r => Assert.Equal(["id", "officeId", "office", "date", "name"], Keys(r)));
        Assert.Equal((null, null, "2037-07-01", "Pin org day"), (all[0]!["officeId"]?.ToString(), all[0]!["office"]?.ToString(), all[0]!.S("date"), all[0]!.S("name")));
        Assert.True(all[0]!.AsObject().ContainsKey("officeId") && all[0]!.AsObject().ContainsKey("office"));
        Assert.Equal((office, "2037-07-02"), (all[1]!.G("officeId"), all[1]!.S("date")));
        Assert.Equal(f.Db(db => db.Offices.Single(o => o.Id == office).Name), all[1]!.S("office"));
        Assert.Equal([local.G("id")], (await admin.GetAsync($"/api/v1/admin/holidays?officeId={office}&year=2037").Result.Json()).AsArray().Select(r => r!.G("id")));
        Assert.Empty((await admin.GetAsync("/api/v1/admin/holidays?year=1999").Result.Json()).AsArray());

        // Refusals.
        Assert.Equal("holiday_exists", (await (await admin.Post("/api/v1/admin/holidays", new { officeId = office, date = "2037-07-02", name = "Again" })).Json(409)).S("code"));
        Assert.NotNull((await (await admin.Post("/api/v1/admin/holidays", new { name = "No date" })).Json(400))["errors"]!["date"]);
        Assert.NotNull((await (await admin.Post("/api/v1/admin/holidays", new { officeId = Guid.NewGuid(), date = "2037-07-03", name = "No office" })).Json(400))["errors"]!["officeId"]);
        Assert.NotNull((await (await admin.Post("/api/v1/admin/holidays", new { date = "2037-07-04", name = " " })).Json(400))["errors"]!["name"]);
        Assert.Equal(HttpStatusCode.Forbidden, (await f.As(TestData.Pm).Post("/api/v1/admin/holidays", new { date = "2037-07-05", name = "Not allowed" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await f.As(TestData.Pm).DeleteAsync($"/api/v1/admin/holidays/{orgRow.S("id")}")).StatusCode);

        // Delete: 204 with an empty body; again is a 404.
        foreach (var id in new[] { orgRow.S("id"), local.S("id") })
        {
            var gone = await admin.DeleteAsync($"/api/v1/admin/holidays/{id}");
            Assert.Equal(HttpStatusCode.NoContent, gone.StatusCode); Assert.Empty(await gone.Content.ReadAsStringAsync());
        }
        Assert.Equal(HttpStatusCode.NotFound, (await admin.DeleteAsync($"/api/v1/admin/holidays/{orgRow.S("id")}")).StatusCode);
        Assert.Empty((await admin.GetAsync("/api/v1/admin/holidays?year=2037").Result.Json()).AsArray());
    }

    [Fact]
    public async Task Settings_keep_their_row_names_order_and_the_flag_packet_and_save_answers()
    {
        var admin = f.As(TestData.Admin);
        var rows = (await admin.GetAsync("/api/v1/admin/settings").Result.Json()).AsArray();

        // An ordinary row has five properties; a flag row also names its packet, last. The default and the value are whatever the setting holds.
        var due = rows.Single(r => r!.S("key") == "task_due_soon_days")!;
        Assert.Equal(["key", "kind", "group", "default", "value"], Keys(due));
        Assert.Equal(("Int", "thresholds", 5), (due.S("kind"), due.S("group"), due["default"]!.GetValue<int>()));
        var flag = rows.Single(r => r!.S("key") == "flag.verifiable_log")!;
        Assert.Equal(["key", "kind", "group", "default", "value", "packet"], Keys(flag));
        Assert.Equal(("Bool", "flags", "048"), (flag.S("kind"), flag.S("group"), flag.S("packet")));
        Assert.All(rows.Where(r => !r!.S("key").StartsWith("flag.")), r => Assert.False(r!.AsObject().ContainsKey("packet")));
        Assert.All(rows.Where(r => r!.S("key").StartsWith("flag.")), r => Assert.Matches(@"^\d{3}$", r!.S("packet")));
        var channels = rows.First(r => r!.S("kind") == "Channels")!;
        Assert.Equal(["app", "email"], Keys(channels["default"])); Assert.Equal(["app", "email"], Keys(channels["value"]));
        Assert.Equal(HttpStatusCode.Forbidden, (await f.As(TestData.Pm).GetAsync("/api/v1/admin/settings")).StatusCode);

        // Saving answers with the key and the stored value; the body may be {"value": x} or the bare value.
        var was = due["value"]!.GetValue<int>();
        try
        {
            var saved = await admin.Put("/api/v1/admin/settings/task_due_soon_days", new { value = 6 }).Result.Json();
            Assert.Equal(["key", "value"], Keys(saved)); Assert.Equal(("task_due_soon_days", 6), (saved.S("key"), saved["value"]!.GetValue<int>()));
            Assert.Equal(6, (await admin.Put("/api/v1/admin/settings/task_due_soon_days", new { value = 6 }).Result.Json())["value"]!.GetValue<int>()); // unchanged still answers
            Assert.Equal(7, (await admin.PutAsync("/api/v1/admin/settings/task_due_soon_days", new StringContent("7", Encoding.UTF8, "application/json")).Result.Json())["value"]!.GetValue<int>());
            Assert.Equal(7, (await admin.GetAsync("/api/v1/admin/settings").Result.Json()).AsArray().Single(r => r!.S("key") == "task_due_soon_days")!["value"]!.GetValue<int>());
            var channel = await admin.Put("/api/v1/admin/settings/notify_default.TaskAssigned", new { value = new { app = true, email = false } }).Result.Json();
            Assert.Equal(["key", "value"], Keys(channel)); Assert.Equal(["app", "email"], Keys(channel["value"]));
            Assert.False(channel["value"]!["email"]!.GetValue<bool>());
        }
        finally
        {
            await admin.Put("/api/v1/admin/settings/task_due_soon_days", new { value = was });
            await admin.Put("/api/v1/admin/settings/notify_default.TaskAssigned", new { value = new { app = true, email = true } });
        }
        Assert.NotNull((await (await admin.Put("/api/v1/admin/settings/no_such_key", new { value = 1 })).Json(400))["errors"]!["key"]);
        Assert.NotNull((await (await admin.Put("/api/v1/admin/settings/task_due_soon_days", new { value = "abc" })).Json(400))["errors"]!["value"]);
    }

    [Fact]
    public async Task Operations_keep_their_names_order_and_row_shapes_and_retry_answers()
    {
        var at = f.Clock.GetUtcNow();
        var type = "AdminPin" + Guid.NewGuid().ToString("N")[..6];
        var job = "admin-pin-" + Guid.NewGuid().ToString("N")[..6];
        var running = job + "-running";
        var ids = await f.DbAsync(async db =>
        {
            var a = new OutboxEvent { EventType = type, Payload = "{}", Attempts = 10, DeadLetteredAt = at, LastError = "boom", CreatedAt = at.AddMinutes(-30) };
            var b = new OutboxEvent { EventType = type, Payload = "{}", Attempts = 10, DeadLetteredAt = at.AddMinutes(-1), CreatedAt = at.AddMinutes(-40) };
            db.Outbox.AddRange(a, b);
            db.JobRuns.AddRange(new JobRun { JobName = job, StartedAt = at.AddMinutes(-5), FinishedAt = at.AddMinutes(-4), Status = "Failed" },
                new JobRun { JobName = job, StartedAt = at.AddMinutes(-50), FinishedAt = at.AddMinutes(-49), Status = "Succeeded" },
                new JobRun { JobName = running, StartedAt = at.AddMinutes(-2), Status = "Running" });
            await db.SaveChangesAsync();
            return new[] { a.Id, b.Id };
        });
        try
        {
            Assert.Equal(HttpStatusCode.Forbidden, (await f.As(TestData.Pm).GetAsync("/api/v1/admin/operations")).StatusCode);
            var ops = await f.As(TestData.Admin).GetAsync("/api/v1/admin/operations").Result.Json();
            Assert.Equal(["asOf", "problems", "jobs", "pendingChanges", "deadLetters", "deadLetterCount"], Keys(ops));
            Assert.Equal(at.ToString("yyyy-MM-ddTHH:mm:ss"), ops.S("asOf")[..19]);
            Assert.All(ops["problems"]!.AsArray(), p => Assert.Equal(["kind", "detail"], Keys(p)));
            Assert.Contains("OutboxDeadLettered", ops["problems"]!.AsArray().Select(p => p!.S("kind")));

            // A job: its last run, the last success (null written when it never succeeded) and the failures in 24 hours.
            Assert.All(ops["jobs"]!.AsArray(), j => Assert.Equal(["job", "startedAt", "finishedAt", "status", "lastSuccess", "failed24h"], Keys(j)));
            var failed = ops["jobs"]!.AsArray().Single(j => j!.S("job") == job)!;
            Assert.Equal(("Failed", 1), (failed.S("status"), failed.I("failed24h")));
            Assert.Equal(at.AddMinutes(-5).ToString("yyyy-MM-ddTHH:mm"), failed.S("startedAt")[..16]);
            Assert.Equal(at.AddMinutes(-4).ToString("yyyy-MM-ddTHH:mm"), failed.S("finishedAt")[..16]);
            Assert.Equal(at.AddMinutes(-50).ToString("yyyy-MM-ddTHH:mm"), failed.S("lastSuccess")[..16]);
            var live = ops["jobs"]!.AsArray().Single(j => j!.S("job") == running)!;
            Assert.Equal(("Running", 0), (live.S("status"), live.I("failed24h")));
            Assert.True(live.AsObject().ContainsKey("finishedAt") && live.AsObject().ContainsKey("lastSuccess"));
            Assert.Null(live["finishedAt"]); Assert.Null(live["lastSuccess"]);

            // A dead letter: newest dead-lettering first, the project number null when there is no project.
            Assert.True(ops.I("deadLetterCount") >= 2);
            Assert.All(ops["deadLetters"]!.AsArray(), d => Assert.Equal(["id", "eventType", "projectNumber", "attempts", "lastError", "createdAt", "deadLetteredAt"], Keys(d)));
            var mine = ops["deadLetters"]!.AsArray().Where(d => d!.S("eventType") == type).ToList();
            Assert.Equal(ids, mine.Select(d => d!.G("id")));
            Assert.Equal((10, "boom"), (mine[0]!.I("attempts"), mine[0]!.S("lastError")));
            Assert.True(mine[0]!.AsObject().ContainsKey("projectNumber")); Assert.Null(mine[0]!["projectNumber"]);
            Assert.Null(mine[1]!["lastError"]);
            Assert.Equal(at.ToString("yyyy-MM-ddTHH:mm"), mine[0]!.S("deadLetteredAt")[..16]);

            // Retry: 200 with how many went back to the worker. One id only: retrying every dead letter would also revive other tests' rows.
            Assert.Equal(HttpStatusCode.Forbidden, (await f.As(TestData.Pm).Post("/api/v1/admin/operations/outbox/retry", new { })).StatusCode);
            var one = await f.As(TestData.Admin).Post($"/api/v1/admin/operations/outbox/retry?id={ids[0]}", new { }).Result.Json();
            Assert.Equal(["retried"], Keys(one)); Assert.Equal(1, one.I("retried"));
            Assert.Equal(0, (await f.As(TestData.Admin).Post($"/api/v1/admin/operations/outbox/retry?id={ids[0]}", new { }).Result.Json()).I("retried")); // no longer dead-lettered
            Assert.Equal(0, (await f.As(TestData.Admin).Post($"/api/v1/admin/operations/outbox/retry?id={Guid.NewGuid()}", new { }).Result.Json()).I("retried"));
        }
        finally { await f.DbAsync(async db => { await db.Outbox.Where(o => ids.Contains(o.Id)).ExecuteDeleteAsync(); await db.JobRuns.Where(r => r.JobName == job || r.JobName == running).ExecuteDeleteAsync(); return 0; }); }
    }

    [Fact]
    public async Task Users_and_roles_keep_their_row_names_order_nulls_and_empty_answers()
    {
        var admin = f.As(TestData.Admin);
        var civil = await new TestData(f).Discipline("Civil");
        var office = await new TestData(f).Office();
        var email = $"pin{Guid.NewGuid():N}"[..10] + "@hub.test";
        await f.As(email).GetAsync("/api/v1/me").Result.Json(); // first sign-in creates the person
        var row = (await admin.GetAsync($"/api/v1/admin/users?q={email}").Result.Json()).AsArray().Single()!;

        // The row: the person's facts with nulls written, the row version, then the roles as role and source.
        Assert.Equal(["id", "displayName", "email", "jobTitle", "officeId", "supervisorId", "homeDisciplineId", "isActive", "weeklyCapacityHours", "isTemplateEditor", "lastSignInAt", "rowVersion", "roles"], Keys(row));
        Assert.Equal((email, true, false, 0), (row.S("email"), row["isActive"]!.GetValue<bool>(), row["isTemplateEditor"]!.GetValue<bool>(), row.I("rowVersion")));
        Assert.All(new[] { "jobTitle", "officeId", "supervisorId", "homeDisciplineId", "weeklyCapacityHours" }, k => { Assert.True(row.AsObject().ContainsKey(k), k); Assert.Null(row[k]); });
        Assert.Empty(row["roles"]!.AsArray());
        var jordan = (await admin.GetAsync("/api/v1/admin/users?q=jordan").Result.Json()).AsArray().Single(u => u!.S("email") == TestData.Admin)!;
        var role = Assert.Single(jordan["roles"]!.AsArray())!;
        Assert.Equal(["role", "source"], Keys(role)); Assert.Equal(("Admin", "Manual"), (role.S("role"), role.S("source")));
        Assert.Equal(HttpStatusCode.Forbidden, (await f.As(TestData.Pm).GetAsync("/api/v1/admin/users")).StatusCode);

        // The filters keep the rows they kept: inactive people, people with no supervisor, a role.
        Assert.Contains(row.G("id"), (await admin.GetAsync("/api/v1/admin/users?noSupervisor=true").Result.Json()).AsArray().Select(u => u!.G("id")));
        Assert.Contains(jordan.G("id"), (await admin.GetAsync("/api/v1/admin/users?role=Admin").Result.Json()).AsArray().Select(u => u!.G("id")));
        Assert.DoesNotContain(row.G("id"), (await admin.GetAsync("/api/v1/admin/users?role=Admin").Result.Json()).AsArray().Select(u => u!.G("id")));

        // Patch: 200 with only the id and the next row version; the person then carries the new values.
        var patched = await admin.Patch($"/api/v1/admin/users/{row.S("id")}", new { weeklyCapacityHours = 36, isTemplateEditor = true, homeDisciplineId = civil, officeId = office }, row.I("rowVersion")).Result.Json();
        Assert.Equal(["id", "rowVersion"], Keys(patched)); Assert.Equal((row.G("id"), 1), (patched.G("id"), patched.I("rowVersion")));
        var after = (await admin.GetAsync($"/api/v1/admin/users?q={email}").Result.Json()).AsArray().Single()!;
        Assert.Equal((36m, true, civil, office, 1), (after["weeklyCapacityHours"]!.GetValue<decimal>(), after["isTemplateEditor"]!.GetValue<bool>(), after.G("homeDisciplineId"), after.G("officeId"), after.I("rowVersion")));
        Assert.Equal("concurrency_conflict", (await (await admin.Patch($"/api/v1/admin/users/{row.S("id")}", new { weeklyCapacityHours = 30 }, row.I("rowVersion"))).Json(409)).S("code"));
        Assert.Equal(HttpStatusCode.NotFound, (await admin.Patch($"/api/v1/admin/users/{Guid.NewGuid()}", new { isActive = true }, 1)).StatusCode);
        Assert.NotNull((await (await admin.Patch($"/api/v1/admin/users/{row.S("id")}", new { weeklyCapacityHours = 99 }, 1)).Json(400))["errors"]!["weeklyCapacityHours"]);
        Assert.NotNull((await (await admin.Patch($"/api/v1/admin/users/{row.S("id")}", new { supervisorId = row.G("id") }, 1)).Json(400))["errors"]!["supervisorId"]);
        Assert.Equal(HttpStatusCode.Forbidden, (await f.As(TestData.Pm).Patch($"/api/v1/admin/users/{row.S("id")}", new { isActive = false }, 1)).StatusCode);

        // Roles: 204 with an empty body to add and to remove, an unknown role is a 400 and a role the person lacks a 404.
        var added = await admin.Post($"/api/v1/admin/users/{row.S("id")}/roles", new { role = "Supervisor" });
        Assert.Equal(HttpStatusCode.NoContent, added.StatusCode); Assert.Empty(await added.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.NoContent, (await admin.Post($"/api/v1/admin/users/{row.S("id")}/roles", new { role = "Supervisor" })).StatusCode); // again changes nothing
        var granted = (await admin.GetAsync($"/api/v1/admin/users?q={email}").Result.Json()).AsArray().Single()!["roles"]!.AsArray();
        Assert.Equal([("Supervisor", "Manual")], granted.Select(r => (r!.S("role"), r.S("source"))));
        Assert.NotNull((await (await admin.Post($"/api/v1/admin/users/{row.S("id")}/roles", new { role = "Nope" })).Json(400))["errors"]!["role"]);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.Post($"/api/v1/admin/users/{Guid.NewGuid()}/roles", new { role = "Supervisor" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await f.As(TestData.Pm).Post($"/api/v1/admin/users/{row.S("id")}/roles", new { role = "Supervisor" })).StatusCode);
        var removed = await admin.DeleteAsync($"/api/v1/admin/users/{row.S("id")}/roles/Supervisor");
        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode); Assert.Empty(await removed.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.NotFound, (await admin.DeleteAsync($"/api/v1/admin/users/{row.S("id")}/roles/Supervisor")).StatusCode);
        Assert.Empty((await admin.GetAsync($"/api/v1/admin/users?q={email}").Result.Json()).AsArray().Single()!["roles"]!.AsArray());

        // Deactivating hides the person from the default list and keeps them under the inactive filter.
        (await admin.Patch($"/api/v1/admin/users/{row.S("id")}", new { isActive = false }, after.I("rowVersion"))).EnsureSuccessStatusCode();
        Assert.Empty((await admin.GetAsync($"/api/v1/admin/users?q={email}").Result.Json()).AsArray());
        Assert.False((await admin.GetAsync($"/api/v1/admin/users?q={email}&inactive=true").Result.Json()).AsArray().Single()!["isActive"]!.GetValue<bool>());
    }

    [Fact]
    public async Task Organisation_activity_keeps_its_page_envelope_and_row_names()
    {
        var admin = f.As(TestData.Admin);
        (await admin.Put("/api/v1/admin/settings/review_stale_days", new { value = 5 })).EnsureSuccessStatusCode(); // a setting edit is an organisation-level entry
        var page = await admin.GetAsync("/api/v1/admin/activity?pageSize=5").Result.Json();
        Assert.Equal(["items", "page", "pageSize", "totalCount", "sort", "filters"], Keys(page));
        Assert.Equal((1, 5), (page.I("page"), page.I("pageSize")));
        Assert.Null(page["sort"]); Assert.Null(page["filters"]);
        Assert.True(page.I("totalCount") >= 1);
        var rows = page["items"]!.AsArray();
        Assert.InRange(rows.Count, 1, 5);
        Assert.All(rows, r => { Assert.Equal(["id", "occurredAt", "actorId", "actorName", "actorType", "projectId", "itemType", "itemId"], Keys(r)[..8]); Assert.Null(r!["projectId"]); });
        Assert.Equal(rows.Select(r => r!.S("occurredAt")).OrderDescending(StringComparer.Ordinal), rows.Select(r => r!.S("occurredAt"))); // newest first
        var users = await admin.GetAsync("/api/v1/admin/activity?itemType=User&excludeAction=SignedIn&pageSize=3&page=2").Result.Json();
        Assert.Equal((2, 3), (users.I("page"), users.I("pageSize")));
        Assert.All(users["items"]!.AsArray(), r => Assert.Equal("User", r!.S("itemType")));
        Assert.Equal(HttpStatusCode.Forbidden, (await f.As(TestData.Pm).GetAsync("/api/v1/admin/activity")).StatusCode);
    }
}
