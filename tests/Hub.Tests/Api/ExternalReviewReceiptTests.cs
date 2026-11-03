using System.Net;
using System.Data.Common;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Hub.Api.Data;
using Hub.Api.Features;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Hub.Tests.Api;

public sealed partial class ExternalReviewTests
{
    [Fact]
    public async Task External_batch_receipt_is_exactly_once_under_concurrency_payload_mismatch_and_current_authority()
    {
        var s = await New(); var body = Body(s);
        var results = await Task.WhenAll(Record(s, body), Record(s, body));
        Assert.True(JsonNode.DeepEquals(results[0], results[1]));
        Assert.Equal(2, Findings(s).Length);
        Assert.Equal(1, F.Db(db => db.Set<ExternalReview>().Count(row => row.PackageId == s.Package)));
        Assert.Equal(1, F.Db(db => db.CoordinationCommands.Count(row => row.ProjectId == s.Project.Id && row.RequestId == body.RequestId)));
        var before = State(s); var again = await Record(s, body);
        Assert.True(JsonNode.DeepEquals(results[0], again)); Assert.Equal(before, State(s));
        var mismatch = await Record(s, body with { Reason = body.Reason + " changed" }, status: 422);
        Assert.Equal("idempotency_key_reused", mismatch.S("code")); Assert.Equal(before, State(s));
        mismatch = await Record(s, body with { Comments = [body.Comments[0] with { Text = "Changed incoming text" }, body.Comments[1]] }, status: 422);
        Assert.Equal("idempotency_key_reused", mismatch.S("code")); Assert.Equal(before, State(s));
        var member = F.Db(db => db.ProjectMembers.AsNoTracking().Single(row => row.ProjectId == s.Project.Id && row.UserId == Data.User(TestData.Jill)));
        (await F.As(TestData.Pm).Delete(Root(s) + $"/members/{member.Id}?reason=Remove%20recording%20authority", member.RowVersion)).EnsureSuccessStatusCode();
        before = State(s); await Record(s, body, status: 403); Assert.Equal(before, State(s));
    }

    [Fact]
    public async Task Stale_nested_baselines_and_late_batch_errors_leave_no_partial_incoming_finding_receipt_sequence_or_notice()
    {
        var s = await New(); var body = Body(s);
        ExternalReviewRecordBody[] stale = [
            body with { RowVersion = body.RowVersion - 1 }, body with { IssueRowVersion = body.IssueRowVersion - 1 },
            body with { PartyRowVersion = body.PartyRowVersion - 1 }, body with { ReturnCodeRowVersion = body.ReturnCodeRowVersion - 1 },
        ];
        foreach (var candidate in stale) {
            var before = State(s); await Record(s, candidate with { RequestId = Guid.NewGuid() }, status: 409); Assert.Equal(before, State(s));
        }
        foreach (var candidate in new[] {
            body with { RowVersion = null }, body with { IssueRowVersion = null }, body with { PartyRowVersion = null }, body with { ReturnCodeRowVersion = null },
        }) {
            var before = State(s); await Record(s, candidate with { RequestId = Guid.NewGuid() }, status: 428); Assert.Equal(before, State(s));
        }
        ExternalReviewRecordBody[] invalid = [
            body with { Comments = [body.Comments[0], body.Comments[1] with { Text = "" }] },
            body with { Comments = [body.Comments[0], body.Comments[1] with { Severity = "Unspecified" }] },
            body with { ReceivedDate = new(2026, 10, 29) }, body with { ReceivedDate = new(2026, 11, 3) },
            body with { Comments = [] },
        ];
        foreach (var candidate in invalid) {
            var before = State(s); await Record(s, candidate with { RequestId = Guid.NewGuid() }, status: 400); Assert.Equal(before, State(s));
        }
        var other = await Data.Project();
        var foreignParty = (await Post(TestData.Pm, $"/api/v1/projects/{other.Id}/external-parties", new { name = "Actual other-project party" }, 201)).G("id");
        var foreignDeliverable = (await Post(TestData.Marc, $"/api/v1/projects/{other.Id}/deliverables", new {
            name = "Actual other-project issued scope", projectDisciplineId = Data.ProjectDiscipline(other.Id, "Civil"),
            deliverableTypeId = await Data.DeliverableType(), ownerId = Data.User(TestData.Alex), requiresReview = false,
        }, 201)).G("id");
        var foreignIssue = await RealIssue(foreignDeliverable, true);
        var unchanged = State(s);
        await Record(s, Body(s) with { ExternalPartyId = foreignParty, PartyRowVersion = V<ExternalParty>(foreignParty) }, status: 400);
        Assert.Equal(unchanged, State(s));
        await Record(s, Body(s) with { DeliverableIssueId = foreignIssue, IssueRowVersion = V<DeliverableIssue>(foreignIssue) }, status: 404);
        Assert.Equal(unchanged, State(s));
        Assert.Equal(HttpStatusCode.NotFound, (await F.As(TestData.Pm).Post($"/api/v1/projects/{other.Id}/reviews/{s.Package}/external-reviews", Body(s))).StatusCode);
        Assert.Equal(unchanged, State(s));
        Assert.Empty(Findings(s)); Assert.False(F.Db(db => db.Set<ExternalReview>().Any(row => row.PackageId == s.Package)));
    }

    // An independent copy of the pre-extension wire, not a serialization of the new DTO itself.
    sealed record LegacyReviewCreate(Guid RequestId, string Title, string Purpose, Guid ProjectDisciplineId, Guid CoordinatorId,
        Guid[] SourceRevisionIds, ReviewEndpoints.Assignment[] Assignments, bool RequiredForIssue, string? Reason);
    sealed record LegacyReviewRound(Guid RequestId, int RowVersion, string Purpose, Guid[] SourceRevisionIds,
        ReviewEndpoints.Assignment[] Assignments, string Reason, string? RemovalImpact);

    [Fact]
    public async Task Internal_omitted_and_null_extension_keep_exact_wire_hash_and_genuine_receipt_parity()
    {
        await Flag(false); var control = await t.New(); var request = Guid.NewGuid();
        var old = new LegacyReviewCreate(request, "Legacy second package", "Independent old internal scope", control.Civil,
            Data.User(TestData.Marc), [control.Source], t.Assignments(control), false, null);
        var current = new ReviewEndpoints.CreateBody(old.RequestId, old.Title, old.Purpose, old.ProjectDisciplineId, old.CoordinatorId,
            old.SourceRevisionIds, old.Assignments, old.RequiredForIssue, old.Reason);
        Assert.Equal(JsonSerializer.Serialize(old, JsonOpts.Web), JsonSerializer.Serialize(current, JsonOpts.Web));
        Assert.Equal(Coordination.Hash(new { operation = "review.create", body = old }), Coordination.Hash(new { operation = "review.create", body = current }));
        var round = new LegacyReviewRound(Guid.NewGuid(), 3, "Legacy next scope", [control.Source], t.Assignments(control), "Real next round reason", null);
        Assert.Equal(JsonSerializer.Serialize(round, JsonOpts.Web), JsonSerializer.Serialize(new ReviewEndpoints.RoundBody(round.RequestId,
            round.RowVersion, round.Purpose, round.SourceRevisionIds, round.Assignments, round.Reason, round.RemovalImpact), JsonOpts.Web));
        var first = await Post(TestData.Marc, t.Root(control) + "/reviews", old);
        var id = first.G("id"); var before = t.State(control.Project.Id);
        var raw = JsonSerializer.SerializeToNode(old, JsonOpts.Web)!.AsObject(); raw["issuedRevisionIds"] = null;
        using var content = new StringContent(raw.ToJsonString(), Encoding.UTF8, "application/json");
        var replay = await (await F.As(TestData.Marc).PostAsync(t.Root(control) + "/reviews", content)).Json();
        Assert.True(JsonNode.DeepEquals(first, replay)); Assert.Equal(before, t.State(control.Project.Id));
        await Flag(); before = t.State(control.Project.Id);
        var again = await Post(TestData.Marc, t.Root(control) + "/reviews", current);
        Assert.True(JsonNode.DeepEquals(first, again)); Assert.Equal(before, t.State(control.Project.Id));
        var detail = await (await F.As(TestData.Marc).GetAsync(t.Root(control) + $"/reviews/{id}")).Json();
        Assert.False(detail.AsObject().ContainsKey("externalReviews")); Assert.False(detail.AsObject().ContainsKey("canRecordExternalReview"));
        Assert.All(detail["manifest"]!.AsArray(), row => Assert.False(row!.AsObject().ContainsKey("issuedSource")));
    }

    [Fact]
    public async Task Stored_external_work_disappears_when_dark_while_internal_routes_and_wire_remain_unchanged()
    {
        var s = await New(); var receipt = await Record(s);
        var internalControl = await t.New(s.Project, "Genuine internal dark control");
        var internalOn = await t.Detail(internalControl); await Flag(false);
        var before = State(s);
        Assert.Equal(HttpStatusCode.NotFound, (await F.As(TestData.Pm).GetAsync(PackagePath(s))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await F.As(TestData.Pm).GetAsync(RecordPath(s))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await F.As(TestData.Pm).GetAsync(RecordPath(s) + "/options")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await F.As(TestData.Pm).GetAsync(RecordPath(s) + $"/{receipt.G("id")}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await F.As(TestData.Pm).GetAsync(Root(s) + "/reviews/issued-sources")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await F.As(TestData.Pm).Post(RecordPath(s), Body(s))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await F.As(TestData.Marc).Post(Root(s) + "/reviews", CreateBody(s))).StatusCode);
        var list = await (await F.As(TestData.Pm).GetAsync(Root(s) + "/reviews")).Json();
        Assert.DoesNotContain(list["items"]!.AsArray(), row => row!.G("id") == s.Package);
        Assert.Contains(list["items"]!.AsArray(), row => row!.G("id") == internalControl.Package);
        var internalOff = await t.Detail(internalControl);
        Assert.False(internalOff.AsObject().ContainsKey("externalReviews")); Assert.False(internalOff.AsObject().ContainsKey("canRecordExternalReview"));
        Assert.Equal(internalOn["package"]!.S("key"), internalOff["package"]!.S("key"));
        Assert.Equal(before, State(s));
        await Flag(); Assert.Equal(receipt.G("id"), (await (await F.As(TestData.Pm).GetAsync(RecordPath(s) + $"/{receipt.G("id")}")).Json()).G("id"));
    }

    // Bound only to this actor's actual project FOR UPDATE. BeforeLock proves preflight has finished;
    // Acquired holds the real request after the queued change commits but before callback/receipt lookup.
    sealed class ExternalProjectLockGate(Guid projectId, Guid actorId) : DbCommandInterceptor
    {
        internal readonly TaskCompletionSource<int> BeforeLock = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource EnterDatabase = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource Acquired = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource Continue = new(TaskCreationOptions.RunContinuationsAsynchronously);
        DbCommand? captured; int armed = 1;
        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData data,
            InterceptionResult<DbDataReader> result, CancellationToken ct = default)
        {
            var sql = command.CommandText.Replace("\"", "");
            if (data.Context is HubDb db && db.Audit.ActorId == actorId
                && sql.Contains("FROM hub.project", StringComparison.OrdinalIgnoreCase) && sql.Contains("FOR UPDATE", StringComparison.OrdinalIgnoreCase)
                && command.Parameters.Cast<DbParameter>().Any(parameter => parameter.Value is Guid id && id == projectId)
                && Interlocked.CompareExchange(ref armed, 0, 1) == 1)
            {
                captured = command; BeforeLock.TrySetResult(((NpgsqlConnection)command.Connection!).ProcessID);
                await EnterDatabase.Task.WaitAsync(TimeSpan.FromSeconds(30), ct);
            }
            return result;
        }
        public override async ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData data,
            DbDataReader result, CancellationToken ct = default)
        {
            if (ReferenceEquals(command, captured)) {
                Acquired.TrySetResult(); await Continue.Task.WaitAsync(TimeSpan.FromSeconds(30), ct);
            }
            return result;
        }
        internal void Release() { EnterDatabase.TrySetResult(); Continue.TrySetResult(); }
    }

    static async Task<int> WaitForProjectWaiter(NpgsqlConnection observer, int blocker)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        while (true)
        {
            await using var query = new NpgsqlCommand("""
                SELECT COALESCE((SELECT pid FROM pg_stat_activity WHERE datname=current_database()
                  AND wait_event_type='Lock' AND @blocker=ANY(pg_blocking_pids(pid))
                  AND replace(query,chr(34),'') ILIKE '%FROM hub.project%FOR UPDATE%' LIMIT 1),0)
                """, observer);
            query.Parameters.AddWithValue("blocker", blocker);
            var pid = Convert.ToInt32(await query.ExecuteScalarAsync(timeout.Token));
            if (pid != 0) return pid;
            await Task.Delay(10, timeout.Token); // Pacing only: success requires the observed PostgreSQL blocker predicate.
        }
    }
    static async Task WaitForBlocker(NpgsqlConnection observer, int waiter, int blocker)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        while (true)
        {
            await using var query = new NpgsqlCommand("SELECT @blocker=ANY(pg_blocking_pids(@waiter))", observer);
            query.Parameters.AddWithValue("blocker", blocker); query.Parameters.AddWithValue("waiter", waiter);
            if (await query.ExecuteScalarAsync(timeout.Token) is true) return;
            await Task.Delay(10, timeout.Token);
        }
    }

    async Task<(HttpResponseMessage Response, string AfterChange)> BehindProjectChange(Scenario s, string who,
        Func<Task<HttpResponseMessage>> change, Func<HttpClient, Task<HttpResponseMessage>> request)
    {
        var gate = new ExternalProjectLockGate(s.Project.Id, Data.User(who));
        using var app = F.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.AddDbContext<HubDb>(options => options.AddInterceptors(gate))));
        using var actor = app.CreateClient(); actor.DefaultRequestHeaders.Add("X-Dev-User", who);
        var cs = $"Host=localhost;Port=55432;Database={F.Database};Username=hub";
        await using var holder = new NpgsqlConnection(cs); await holder.OpenAsync();
        await using var observer = new NpgsqlConnection(cs); await observer.OpenAsync();
        await using var transaction = await holder.BeginTransactionAsync();
        await using (var hold = new NpgsqlCommand("SELECT id FROM hub.project WHERE id=@id FOR UPDATE", holder, transaction)) {
            hold.Parameters.AddWithValue("id", s.Project.Id); Assert.Equal(s.Project.Id, (Guid)(await hold.ExecuteScalarAsync())!);
        }
        var pendingChange = change(); Task<HttpResponseMessage>? waiting = null; var released = false;
        try
        {
            var changePid = await WaitForProjectWaiter(observer, holder.ProcessID); // Mutation was queued first.
            Assert.False(pendingChange.IsCompleted);
            waiting = request(actor);
            var waitingPid = await gate.BeforeLock.Task.WaitAsync(TimeSpan.FromSeconds(20));
            gate.EnterDatabase.TrySetResult();
            await WaitForBlocker(observer, waitingPid, changePid); // Prove actual queue ordering, not assumed FIFO timing.
            Assert.False(waiting.IsCompleted); Assert.False(pendingChange.IsCompleted);
            await transaction.CommitAsync(); released = true;
            (await pendingChange.WaitAsync(TimeSpan.FromSeconds(20))).EnsureSuccessStatusCode();
            await gate.Acquired.Task.WaitAsync(TimeSpan.FromSeconds(20));
            var afterChange = State(s); // The waiting command owns the lock but has not applied anything or replayed.
            gate.Continue.TrySetResult();
            return (await waiting.WaitAsync(TimeSpan.FromSeconds(20)), afterChange);
        }
        finally
        {
            gate.Release();
            if (!released) await transaction.RollbackAsync();
            await pendingChange.WaitAsync(TimeSpan.FromSeconds(20));
            if (waiting is not null) await waiting.WaitAsync(TimeSpan.FromSeconds(20));
        }
    }

    [Fact]
    public async Task Advisory_record_queued_after_coordinator_change_must_recheck_its_package_baseline_under_lock()
    {
        var s = await New(); var body = Body(s) with { Comments = [new("Advisory must still honor its original package version", s.Civil, Data.User(TestData.Alex), "Advisory")] };
        var originalVersion = body.RowVersion!.Value;
        var change = new ReviewEndpoints.ReassignBody(Guid.NewGuid(), originalVersion, Data.User(TestData.Pm), "Commit a real coordinator change ahead of the waiting record");
        var race = await BehindProjectChange(s, TestData.Jill,
            () => F.As(TestData.Pm).Post(PackagePath(s) + "/assign", change), actor => actor.Post(RecordPath(s), body));
        var problem = await race.Response.Json(409); Assert.Equal("concurrency_conflict", problem.S("code"));
        Assert.True(V<ReviewPackage>(s.Package) > originalVersion);
        Assert.Equal(Data.User(TestData.Pm), F.Db(db => db.ReviewPackages.Single(row => row.Id == s.Package).CoordinatorId));
        Assert.Equal(race.AfterChange, State(s)); Assert.Empty(Findings(s));
        Assert.False(F.Db(db => db.Set<ExternalReview>().Any(row => row.PackageId == s.Package)));
        Assert.False(F.Db(db => db.CoordinationCommands.Any(row => row.ProjectId == s.Project.Id && row.RequestId == body.RequestId)));
    }

    [Fact]
    public async Task Exact_named_verifier_retry_waiting_behind_real_reassignment_is_refused_before_receipt_replay()
    {
        var s = await New(); await Record(s, Body(s) with { Comments = [new("Actual verifier return to be retried", s.Civil, Data.User(TestData.Alex), "Blocking")] });
        var finding = Assert.Single(Findings(s)).Id;
        await Act(s, finding, "assignVerifier", TestData.Pm, owner: Data.User(TestData.Jill));
        await Act(s, finding, FindingStatus.Responded, TestData.Alex, outcome: "Clarified");
        // Return-to-Open is a real named-verifier action with a committed receipt, and leaves reassignment lawful.
        var original = new ReviewEndpoints.FindingAction(Guid.NewGuid(), V<ReviewFinding>(finding), FindingStatus.Open,
            "Return this actual response for additional work", VerifierEvidence, null);
        var first = await Post(TestData.Jill, PackagePath(s) + $"/findings/{finding}/action", original);
        Assert.Equal(FindingStatus.Open, Findings(s).Single(row => row.Id == finding).Status);
        var beforeReceipt = F.Db(db => db.CoordinationCommands.AsNoTracking().Single(row => row.ProjectId == s.Project.Id && row.RequestId == original.RequestId));
        var reassignment = new ReviewEndpoints.FindingAction(Guid.NewGuid(), V<ReviewFinding>(finding), "assignVerifier",
            "Explicitly replace the verifier before the pending retry", null, Data.User(TestData.Omar));
        var race = await BehindProjectChange(s, TestData.Jill,
            () => F.As(TestData.Pm).Post(PackagePath(s) + $"/findings/{finding}/action", reassignment),
            actor => actor.Post(PackagePath(s) + $"/findings/{finding}/action", original));
        await race.Response.Json(403); Assert.Equal(race.AfterChange, State(s));
        Assert.Equal(Data.User(TestData.Omar), Findings(s).Single(row => row.Id == finding).VerifierId);
        var retained = F.Db(db => db.CoordinationCommands.AsNoTracking().Single(row => row.ProjectId == s.Project.Id && row.RequestId == original.RequestId));
        Assert.Equal(beforeReceipt.PayloadHash, retained.PayloadHash); Assert.Equal(first.G("id"), retained.ResultId);
        Assert.Equal(beforeReceipt.ResultVersion, retained.ResultVersion);
        Assert.Equal(1, F.Db(db => db.FindingEvents.Count(row => row.FindingId == finding && row.Action == FindingStatus.Open && row.Reason == original.Reason)));
    }
}
