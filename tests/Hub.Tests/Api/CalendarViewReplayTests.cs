using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Data.Common;
using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace Hub.Tests.Api;

public sealed class CalendarViewReplayTests : IDisposable
{
    readonly HubFactory f = new();
    public void Dispose() => f.Dispose();
    async Task Flag(bool on) => await (await f.As(TestData.Admin).Put("/api/v1/admin/settings/flag.work_screens", new { value = on })).Json();
    async Task<HttpResponseMessage> Send(string actor, string key, object body, Encoding? encoding = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/views")
        { Content = new StringContent(body is string raw ? raw : JsonSerializer.Serialize(body, JsonOpts.Web), encoding ?? Encoding.UTF8, "application/json") };
        request.Headers.Add("Idempotency-Key", key);
        return await f.As(actor).SendAsync(request);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Calendar_receipt_rechecks_flag_even_after_deletion_or_changed_body(bool utf16)
    {
        await Flag(true);
        var key = Guid.NewGuid().ToString(); var encoding = utf16 ? Encoding.Unicode : Encoding.UTF8;
        var body = new { name = "Synthetic café Calendar", listType = "calendar", @params = new { mode = "week" } };
        var first = await (await Send(TestData.Alex, key, body, encoding)).Json(201);
        var replay = await Send(TestData.Alex, key, body, encoding);
        Assert.True(replay.Headers.Contains("Idempotent-Replayed"));
        Assert.Equal(first.ToJsonString(), (await replay.Json(201)).ToJsonString());
        Assert.Equal("Personal", f.Db(db => db.Idempotency.Single(row => row.Key == key).CalendarViewScope));
        await (await f.As(TestData.Alex).DeleteAsync($"/api/v1/views/{first.G("id")}")).Json(204);
        await Flag(false);
        await (await Send(TestData.Alex, key, body, encoding)).Json(404);
        await (await Send(TestData.Alex, key, new { name = "Synthetic changed list", listType = "tasks" }, encoding)).Json(404);
        Assert.Empty(f.Db(db => db.SavedViews.ToList()));
        await Flag(true);
        var restored = await Send(TestData.Alex, key, body, encoding);
        Assert.True(restored.Headers.Contains("Idempotent-Replayed"));
        Assert.Equal(first.ToJsonString(), (await restored.Json(201)).ToJsonString());
        Assert.Empty(f.Db(db => db.SavedViews.ToList()));
    }

    [Fact]
    public async Task Shared_receipt_rechecks_current_project_management_without_requiring_old_view_version()
    {
        var data = new TestData(f); var project = await data.Project(); await Flag(true);
        var key = Guid.NewGuid().ToString();
        var body = new { name = "Synthetic shared Calendar", listType = "calendar", scope = "Project", projectId = project.Id,
            @params = new Dictionary<string, string> { ["projects"] = project.Id.ToString(), ["mode"] = "month" } };
        var first = await (await Send(TestData.Pm, key, body)).Json(201);
        // Synthetic current-role change models another authorized operator taking over the project.
        await f.DbAsync(async db =>
        {
            var pm = data.User(TestData.Pm); var replacement = data.User(TestData.Marc);
            (await db.Projects.SingleAsync(row => row.Id == project.Id)).ProjectManagerId = replacement;
            foreach (var membership in await db.ProjectMembers.Where(row => row.ProjectId == project.Id && row.UserId == pm && row.RemovedAt == null).ToListAsync())
                membership.Roles = [ProjectRole.TeamMember];
            foreach (var discipline in await db.ProjectDisciplines.Where(row => row.ProjectId == project.Id && row.LeadUserId == pm).ToListAsync())
                discipline.LeadUserId = replacement;
            return await db.SaveChangesAsync();
        });
        await (await Send(TestData.Pm, key, body)).Json(403);
        await (await Send(TestData.Pm, key, new { name = "Synthetic changed list", listType = "tasks" })).Json(403);
        Assert.Equal(first.G("id"), f.Db(db => db.SavedViews.Single(row => row.ListType == "calendar").Id));
    }

    [Fact]
    public async Task Non_calendar_receipts_keep_legacy_behavior_and_expired_calendar_context_is_cleared()
    {
        await Flag(false); var key = Guid.NewGuid().ToString();
        var oldBody = new { name = "Synthetic legacy tasks", listType = "tasks" };
        var first = await (await Send(TestData.Alex, key, oldBody)).Json(201);
        var replay = await Send(TestData.Alex, key, "{");
        Assert.True(replay.Headers.Contains("Idempotent-Replayed"));
        Assert.Equal(first.ToJsonString(), (await replay.Json(201)).ToJsonString());
        Assert.Null(f.Db(db => db.Idempotency.Single(row => row.Key == key).CalendarViewScope));

        await Flag(true); var calendarKey = Guid.NewGuid().ToString();
        var calendar = await (await Send(TestData.Alex, calendarKey, new { name = "Synthetic expired Calendar", listType = "calendar" })).Json(201);
        f.Clock.Now = f.Clock.Now.AddDays(1).AddMinutes(1); await Flag(false);
        var fresh = await (await Send(TestData.Alex, calendarKey, oldBody)).Json(201);
        Assert.NotEqual(calendar.G("id"), fresh.G("id"));
        Assert.Null(f.Db(db => db.Idempotency.Single(row => row.Key == calendarKey).CalendarViewScope));
        Assert.Null(f.Db(db => db.Idempotency.Single(row => row.Key == calendarKey).CalendarViewProjectId));
    }

    [Fact]
    public async Task Calendar_receipt_completed_after_a_non_calendar_retry_starts_is_authorized_at_the_actual_replay_boundary()
    {
        await Flag(true); var race = new PendingReceiptRace();
        using var app = f.WithWebHostBuilder(builder => builder.ConfigureServices(services => services.AddDbContext<HubDb>(options => options.AddInterceptors(race))));
        using var actor = app.CreateClient(); actor.DefaultRequestHeaders.Add("X-Dev-User", TestData.Alex);
        using var admin = app.CreateClient(); admin.DefaultRequestHeaders.Add("X-Dev-User", TestData.Admin);
        var key = Guid.NewGuid().ToString();
        async Task<HttpResponseMessage> Request(string list)
        {
            using var message = new HttpRequestMessage(HttpMethod.Post, "/api/v1/views")
            { Content = new StringContent(JsonSerializer.Serialize(new { name = "Synthetic controlled receipt race", listType = list }, JsonOpts.Web), Encoding.UTF8, "application/json") };
            message.Headers.Add("Idempotency-Key", key); return await actor.SendAsync(message);
        }
        var first = Request("calendar");
        try
        {
            await race.FillReached.Task.WaitAsync(TimeSpan.FromSeconds(10));
            // The original command has succeeded; its generic receipt is still Pending until we release its fill.
            await (await admin.Put("/api/v1/admin/settings/flag.work_screens", new { value = false })).Json();
            var retry = Request("tasks");
            await race.LookupReached.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await (await first).Json(201);
            await (await retry).Json(404);
            Assert.Equal(1, f.Db(db => db.SavedViews.Count(row => row.ListType == "calendar")));
            Assert.Equal(0, f.Db(db => db.SavedViews.Count(row => row.ListType == "tasks")));
            Assert.Equal("Personal", f.Db(db => db.Idempotency.Single(row => row.Key == key).CalendarViewScope));
        }
        finally { race.ReleaseFill.TrySetResult(); }
    }

    sealed class PendingReceiptRace : DbCommandInterceptor
    {
        internal readonly TaskCompletionSource FillReached = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource LookupReached = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource ReleaseFill = new(TaskCreationOptions.RunContinuationsAsynchronously);
        readonly TaskCompletionSource fillCompleted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int filling, lookup; DbCommand? firstFill;
        public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("idempotency_record", StringComparison.OrdinalIgnoreCase)
                && command.CommandText.TrimStart().StartsWith("UPDATE", StringComparison.OrdinalIgnoreCase)
                && Interlocked.CompareExchange(ref filling, 1, 0) == 0)
            {
                firstFill = command; FillReached.TrySetResult();
                await ReleaseFill.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
            }
            return result;
        }
        public override ValueTask<int> NonQueryExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
            int result, CancellationToken cancellationToken = default)
        {
            if (ReferenceEquals(firstFill, command)) fillCompleted.TrySetResult();
            return ValueTask.FromResult(result);
        }
        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            var fullReceipt = command.CommandText.Contains(".body", StringComparison.OrdinalIgnoreCase)
                || command.CommandText.Contains(".\"body\"", StringComparison.OrdinalIgnoreCase);
            if (Volatile.Read(ref filling) == 1 && fullReceipt && command.CommandText.Contains("idempotency_record", StringComparison.OrdinalIgnoreCase)
                && Interlocked.CompareExchange(ref lookup, 1, 0) == 0)
            {
                LookupReached.TrySetResult(); ReleaseFill.TrySetResult();
                // A former scope-only pre-query must finish while Pending; release only at the full generic lookup.
                await fillCompleted.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
            }
            return result;
        }
    }
}
