using System.Data.Common;
using System.Net;
using System.Text.Json;
using Hub.Api.Data;
using Hub.Api.Features;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace Hub.Tests.Api;

/// AC-SCR-77 / AC-SCR-84: seven honest comparison states, actual targets, scoped batching and dark wire parity.
public sealed class DocumentLinkReferenceReadTests
{
    [Theory] // AC-SCR-77: revisions are known trimmed labels, never an inferred ordering or RowVersion.
    [InlineData(null, "D", "NotRecorded")]
    [InlineData("", "D", "NotRecorded")]
    [InlineData(" C ", " D ", "Different")]
    [InlineData("D", "C", "Different")]
    [InlineData("d", "D", "Different")]
    [InlineData("10", "2", "Different")]
    [InlineData("é/β", "é/β", "Matches")]
    [InlineData(" D ", "D", "Matches")]
    [InlineData("C", null, "CurrentNotRecorded")]
    [InlineData("C", "   ", "CurrentNotRecorded")]
    public async Task Known_label_comparison_is_ordinal_and_missing_values_do_not_match(string? declared, string? current, string state)
    {
        using var t = new DocumentLinkReferenceFixture(); await t.Flag(true); var s = await t.New();
        await t.F.DbAsync(async db => { (await db.Deliverables.SingleAsync(row => row.Id == s.Deliverable)).Revision = current; return await db.SaveChangesAsync(); });
        var id = (await t.Add(s, reference: new("Manual register", "001", declared))).G("id"); var before = t.State(s.Project.Id);
        var row = t.Row(await t.List(s), id); var comparison = t.Context(row);
        Assert.Equal(state, comparison.S("state"));
        Assert.Equal(string.IsNullOrWhiteSpace(declared) ? null : declared.Trim(), comparison["declaredRevision"]?.GetValue<string>());
        Assert.Equal(state is "Matches" or "Different" ? current!.Trim() : null, comparison["currentRevision"]?.GetValue<string>());
        Assert.Equal(before, t.State(s.Project.Id)); Assert.Equal("Manual", row["referenceContext"]!["reference"]!.S("registrationKind"));
    }

    [Theory] // AC-SCR-77: Project and genuinely standalone Task links may declare a revision without claiming comparison.
    [InlineData(ItemType.Project)]
    [InlineData(ItemType.Task)]
    public async Task No_deliverable_is_distinct_from_no_declaration(string kind)
    {
        using var t = new DocumentLinkReferenceFixture(); await t.Flag(true); var s = await t.New(parent: false);
        var item = kind == ItemType.Project ? s.Project.Id : s.Task; var who = kind == ItemType.Project ? TestData.Pm : TestData.Alex;
        var id = (await t.Add(s, reference: new("External register", null, "C"), type: kind, item: item, who: who)).G("id");
        Assert.Null(t.Stored(id).ReferenceDeliverableId); var before = t.State(s.Project.Id);
        var row = t.Row(await t.List(s, kind, item), id); Assert.Equal("NoDeliverable", t.Context(row).S("state")); Assert.Null(row["referenceContext"]!["target"]);
        Assert.Equal(before, t.State(s.Project.Id));
    }

    [Theory] // AC-SCR-77: Task target changes precede NoDeliverable, including NULL→ID and ID→NULL.
    [InlineData("replace")]
    [InlineData("gain")]
    [InlineData("remove")]
    public async Task Changed_task_parent_never_becomes_a_new_match_until_explicit_reregistration(string change)
    {
        using var t = new DocumentLinkReferenceFixture(); await t.Flag(true); var s = await t.New(parent: change != "gain");
        var id = (await t.Add(s, reference: new("Register", "001", "C"))).G("id"); var original = t.Stored(id);
        var replacement = await t.Drawing(s.Project, "A new parent must not supply comparison facts", "C");
        Guid? next = change == "remove" ? null : replacement;
        await (await t.F.As(TestData.Marc).Patch($"/api/v1/tasks/{s.Task}", new { deliverableId = next }, t.Version<WorkTask>(s.Task))).Json();
        var before = t.State(s.Project.Id); var row = t.Row(await t.List(s), id); var context = row["referenceContext"]!;
        Assert.Equal("TargetChanged", t.Context(row).S("state")); Assert.Null(context["target"]); Assert.Null(t.Context(row)["currentRevision"]);
        Assert.DoesNotContain(replacement.ToString(), context.ToJsonString()); Assert.DoesNotContain("A new parent", context.ToJsonString());
        Assert.Equal(original.ReferenceDeliverableId, t.Stored(id).ReferenceDeliverableId); Assert.Equal(before, t.State(s.Project.Id));
        t.F.Clock.Now = t.F.Clock.Now.AddMinutes(1);
        await t.Patch(id, new { reference = new DocumentLinkReferenceInput("Register", "001", "C") });
        row = t.Row(await t.List(s), id); Assert.Equal(change == "remove" ? "NoDeliverable" : "Matches", t.Context(row).S("state"));
        Assert.Equal(next, t.Stored(id).ReferenceDeliverableId); Assert.Equal(original.AddedAt, t.Stored(id).AddedAt);
    }

    [Fact] // AC-SCR-77: absence of a declaration remains NotRecorded even when a parent changed.
    public async Task Undeclared_or_legacy_link_never_claims_a_revision_match()
    {
        using var t = new DocumentLinkReferenceFixture(); await t.Flag(true); var s = await t.New(parent: false);
        var registered = (await t.Add(s, reference: new("Manual register"))).G("id"); var legacy = (await t.Add(s)).G("id");
        await (await t.F.As(TestData.Marc).Patch($"/api/v1/tasks/{s.Task}", new { deliverableId = s.Deliverable }, t.Version<WorkTask>(s.Task))).Json();
        var before = t.State(s.Project.Id); var list = await t.List(s);
        Assert.Equal("NotRecorded", t.Context(t.Row(list, registered)).S("state")); Assert.Null(t.Row(list, registered)["referenceContext"]!["target"]);
        Assert.Equal("NotRecorded", t.Context(t.Row(list, legacy)).S("state")); Assert.Null(t.Row(list, legacy)["referenceContext"]!["reference"]);
        Assert.Equal(before, t.State(s.Project.Id));
    }

    [Fact] // AC-SCR-77: direct deliverable target is separate from the unchanged inheritance Owner tuple.
    public async Task Direct_deliverable_link_is_not_self_inherited_and_task_inheritance_keeps_real_id_version_and_readonly()
    {
        using var t = new DocumentLinkReferenceFixture(); await t.Flag(true); var s = await t.New();
        var id = (await t.Add(s, reference: new("Register", "D-001", "C"), type: ItemType.Deliverable, item: s.Deliverable)).G("id");
        var owner = await t.F.DbAsync(db => DocumentLinkEndpoints.Owner(db, ItemType.Deliverable, s.Deliverable)); Assert.NotNull(owner); Assert.Null(owner.Value.DeliverableId);
        var before = t.State(s.Project.Id); var direct = await t.List(s, ItemType.Deliverable, s.Deliverable);
        Assert.Single(direct["links"]!.AsArray()); Assert.Empty(direct["inherited"]!.AsArray());
        Assert.Equal(s.Deliverable, t.Row(direct, id)["referenceContext"]!["target"]!.G("id"));
        var inherited = t.Row(await t.List(s), id, "inherited"); Assert.False(inherited["canChange"]!.GetValue<bool>());
        Assert.Equal(t.Stored(id).RowVersion, inherited.I("rowVersion"));
        Assert.True(System.Text.Json.Nodes.JsonNode.DeepEquals(t.Row(direct, id)["referenceContext"], inherited["referenceContext"]));
        Assert.Equal(before, t.State(s.Project.Id));
    }

    [Theory] // AC-SCR-77: deleted/foreign targets expose neither IDs nor labels/revisions; no read-side repair.
    [InlineData(false)]
    [InlineData(true)]
    public async Task Unavailable_captured_target_does_not_leak_foreign_or_deleted_metadata(bool foreign)
    {
        using var t = new DocumentLinkReferenceFixture(); await t.Flag(true); var s = await t.New();
        var id = (await t.Add(s, reference: new("Register", "001", "C"))).G("id"); var other = await t.New();
        await t.F.DbAsync(async db => {
            if (foreign)
            {
                // Deliberate coherent GUIDs with invalid cross-project ownership, bypassing normal task validation.
                (await db.Tasks.SingleAsync(task => task.Id == s.Task)).DeliverableId = other.Deliverable;
                (await db.DocumentLinks.SingleAsync(link => link.Id == id)).ReferenceDeliverableId = other.Deliverable;
                (await db.Deliverables.SingleAsync(work => work.Id == other.Deliverable)).Name = "PRIVATE FOREIGN DRAWING";
            }
            else (await db.Deliverables.SingleAsync(work => work.Id == s.Deliverable)).DeletedAt = t.F.Clock.Now;
            return await db.SaveChangesAsync();
        });
        var before = t.State(s.Project.Id); var context = t.Row(await t.List(s), id)["referenceContext"]!;
        Assert.Equal("TargetUnavailable", context["comparison"]!.S("state")); Assert.Null(context["target"]); Assert.Null(context["comparison"]!["currentRevision"]);
        Assert.DoesNotContain(other.Deliverable.ToString(), context.ToJsonString()); Assert.DoesNotContain("PRIVATE", context.ToJsonString());
        Assert.Equal(before, t.State(s.Project.Id));
        if (foreign)
        {
            // Even if the caller can read both projects, a different project's target is not this link's target.
            var both = await t.F.DbAsync(db => DocumentLinkReferences.ReadContexts(db, [t.Stored(id)], [s.Project.Id, other.Project.Id]));
            Assert.Equal("TargetUnavailable", both[id].Comparison.State); Assert.Null(both[id].Target);
        }
    }

    [Fact] // AC-SCR-77: descriptors require actual matching users; inactive registrar identity is retained without private fields.
    public async Task Actual_inactive_and_unknown_registrars_are_not_guessed_or_replaced_by_original_author()
    {
        using var t = new DocumentLinkReferenceFixture(); await t.Flag(true); var s = await t.New(); var id = (await t.Add(s, reference: new("Register", null, "C"))).G("id");
        var alex = t.Data.User(TestData.Alex);
        await t.F.DbAsync(async db => { var user = await db.Users.SingleAsync(row => row.Id == alex); user.IsActive = false; user.JobTitle = "PRIVATE PERSON FACT"; return await db.SaveChangesAsync(); });
        var before = t.State(s.Project.Id); var context = t.Row(await t.List(s), id)["referenceContext"]!;
        Assert.Equal(alex, context["reference"]!["registeredByUser"]!.G("id")); Assert.False(context["reference"]!["registeredByUser"]!["isActive"]!.GetValue<bool>());
        Assert.DoesNotContain("@hub.test", context.ToJsonString()); Assert.DoesNotContain("PRIVATE", context.ToJsonString()); Assert.Equal(before, t.State(s.Project.Id));
        var staleInput = t.Stored(id); var unknown = Guid.NewGuid(); staleInput.ReferenceRegisteredBy = unknown; // In-memory historical input, never written around the FK.
        var read = await t.F.DbAsync(db => DocumentLinkReferences.ReadContexts(db, [staleInput], [s.Project.Id]));
        Assert.Equal(unknown, read[id].Reference!.RegisteredBy); Assert.Null(read[id].Reference!.RegisteredByUser);
        Assert.False(t.F.Db(db => db.Users.Any(user => user.Id == unknown))); Assert.Equal(before, t.State(s.Project.Id));
    }

    [Fact] // AC-SCR-77 / AC-SCR-84: caller-authorized project IDs are an upper bound, not a directory expansion.
    public async Task Batch_reader_excludes_unreadable_links_and_hides_mismatched_owner_metadata()
    {
        using var t = new DocumentLinkReferenceFixture(); await t.Flag(true); var visible = await t.New(); var hidden = await t.New();
        var a = (await t.Add(visible, reference: new("Visible register", null, "C"))).G("id");
        var b = (await t.Add(hidden, reference: new("PRIVATE HIDDEN REGISTER", null, "C"))).G("id");
        var before = t.State(visible.Project.Id);
        var links = new[] { t.Stored(a), t.Stored(b) };
        var result = await t.F.DbAsync(db => DocumentLinkReferences.ReadContexts(db, links, [visible.Project.Id]));
        Assert.Equal(new[] { a }, result.Keys); Assert.DoesNotContain("PRIVATE", JsonSerializer.Serialize(result, JsonOpts.Web));
        var malformed = t.Stored(a); malformed.ItemId = hidden.Task;
        var bad = await t.F.DbAsync(db => DocumentLinkReferences.ReadContexts(db, [malformed], [visible.Project.Id]));
        Assert.Equal("TargetUnavailable", bad[a].Comparison.State); Assert.Null(bad[a].Reference); Assert.Null(bad[a].Target);
        Assert.DoesNotContain(hidden.Task.ToString(), JsonSerializer.Serialize(bad, JsonOpts.Web)); Assert.Equal(before, t.State(visible.Project.Id));
        Assert.Empty(await t.F.DbAsync(db => DocumentLinkReferences.ReadContexts(db, links, [])));
    }

    [Fact] // AC-SCR-77: actual provider query count stays fixed across a growing cohort; GET/helper never saves state.
    public async Task Reference_reads_batch_owner_target_and_registrar_queries_instead_of_querying_each_link()
    {
        using var t = new DocumentLinkReferenceFixture(); await t.Flag(true); var s = await t.New(); var ids = new List<Guid>();
        for (var index = 0; index < 12; index++) ids.Add((await t.Add(s, reference: new("Register", index.ToString(), "C"))).G("id"));
        var links = t.F.Db(db => db.DocumentLinks.AsNoTracking().Where(link => ids.Contains(link.Id)).ToArray()); var before = t.State(s.Project.Id);
        async Task<int> Count(IReadOnlyCollection<DocumentLink> cohort)
        {
            using var scope = t.F.Services.CreateScope(); var services = scope.ServiceProvider; var capture = new ReadCommands();
            var options = new DbContextOptionsBuilder<HubDb>(services.GetRequiredService<DbContextOptions<HubDb>>()).AddInterceptors(capture).Options;
            await using var db = new HubDb(options, services.GetRequiredService<AuditContext>(), services.GetRequiredService<TimeProvider>(),
                services.GetRequiredService<EvaluationSignal>(), services.GetRequiredService<SettingsStore>(), services.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>());
            await using var snapshot = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead);
            var contexts = await DocumentLinkReferences.ReadContexts(db, cohort, [s.Project.Id]); Assert.Equal(cohort.Count, contexts.Count);
            Assert.All(contexts.Values, context => Assert.Equal("Different", context.Comparison.State));
            Assert.All(capture.Sql, sql => Assert.StartsWith("SELECT", sql.TrimStart(), StringComparison.OrdinalIgnoreCase));
            return capture.Sql.Count;
        }
        var one = await Count([links[0]]); var many = await Count(links);
        Assert.InRange(one, 1, 4); Assert.Equal(one, many); Assert.Equal(before, t.State(s.Project.Id));
    }

    [Fact] // AC-SCR-84: own/inherited legacy shape is restored after stored-on metadata, while original path actions survive.
    public async Task Off_reads_omit_context_and_preserve_legacy_unc_sharepoint_and_inheritance_shapes()
    {
        using var t = new DocumentLinkReferenceFixture(); var s = await t.New();
        var unc = (await t.Add(s, url: @"\\fileserver\project\drawing" )).G("id");
        var inherited = (await t.Add(s, type: ItemType.Deliverable, item: s.Deliverable, url: "https://contoso.sharepoint.com/sites/p/Shared%20Documents/drawing")).G("id");
        var off = await t.List(s); Assert.True(t.Row(off, unc)["isNetworkPath"]!.GetValue<bool>());
        Assert.Equal(LinkType.SharePoint, t.Row(off, inherited, "inherited").S("linkType"));
        await t.Flag(true); await t.Patch(unc, new { reference = new DocumentLinkReferenceInput("Manual register", null, "C") });
        await t.Flag(false); var before = t.State(s.Project.Id); var dark = await t.List(s);
        Assert.All(dark["links"]!.AsArray().Concat(dark["inherited"]!.AsArray()), row => Assert.False(row!.AsObject().ContainsKey("referenceContext")));
        foreach (var collection in new[] { "links", "inherited" })
            Assert.Equal(off[collection]!.AsArray().Single()!.AsObject().Select(property => property.Key), dark[collection]!.AsArray().Single()!.AsObject().Select(property => property.Key));
        Assert.Equal(t.Row(off, unc).S("url"), t.Row(dark, unc).S("url")); Assert.Equal(t.Row(off, unc).S("linkType"), t.Row(dark, unc).S("linkType"));
        Assert.Equal(t.Row(off, inherited, "inherited").ToJsonString(), t.Row(dark, inherited, "inherited").ToJsonString()); Assert.Equal(before, t.State(s.Project.Id));
    }

    sealed class ReadCommands : DbCommandInterceptor
    {
        internal readonly List<string> Sql = [];
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        { Sql.Add(command.CommandText); return ValueTask.FromResult(result); }
        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        { Sql.Add(command.CommandText); return ValueTask.FromResult(result); }
        public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<object> result, CancellationToken cancellationToken = default)
        { Sql.Add(command.CommandText); return ValueTask.FromResult(result); }
    }
}
