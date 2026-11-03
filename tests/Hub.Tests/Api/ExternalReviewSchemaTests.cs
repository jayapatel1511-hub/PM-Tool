using System.Net;
using System.Text.Json.Nodes;
using Hub.Api.Data;
using Hub.Api.Features;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace Hub.Tests.Api;

public sealed partial class ExternalReviewTests
{
    [Fact]
    public async Task Legacy_internal_review_downgrades_to_required_columns_without_defaults_and_reupgrades_without_data_loss()
    {
        // This test's HubFactory owns a fresh database. All legacy records come from actual internal-review commands.
        const string previous = "20261102234729_NoticeDeliveryPolicyWitness";
        const string migration = "20261103042711_ExternalIssuedReviewSources";
        await Flag(false); var control = await t.New(); var finding = await t.Finding(control);
        Assert.False(F.Db(db => db.Set<ExternalReview>().Any()));
        Assert.False(F.Db(db => db.DeliverableIssues.Any(row => row.IssuedAuthorIds != null)));
        Assert.False(F.Db(db => db.ReviewManifestItems.Any(row => row.DeliverableIssueId != null)));
        Assert.False(F.Db(db => db.ReviewFindings.Any(row => row.DeliverableIssueId != null || row.ExternalReviewId != null)));
        var identity = F.Db(db => db.ReviewFindings.AsNoTracking().Where(row => row.Id == finding)
            .Select(row => new { row.SourceRevisionId, row.OriginatorId, row.VerifierId }).Single());
        Assert.Equal(control.Source, identity.SourceRevisionId); Assert.NotNull(identity.OriginatorId); Assert.NotNull(identity.VerifierId);
        var engine = t.State(control.Project.Id);
        Task<string[]> History() => F.DbAsync(async db => (await db.Database.GetAppliedMigrationsAsync()).ToArray());
        Task<string[]> Columns() => F.DbAsync(db => db.Database.SqlQueryRaw<string>("""
            SELECT table_name || '.' || column_name || ' ' || udt_name || ' ' || is_nullable || ' ' ||
                COALESCE(column_default, '<no-default>') AS "Value"
            FROM information_schema.columns
            WHERE table_schema = 'hub' AND
                ((table_name = 'review_manifest_item' AND column_name = 'source_revision_id') OR
                 (table_name = 'review_finding' AND column_name IN ('source_revision_id', 'originator_id', 'verifier_id')))
            ORDER BY "Value"
            """).ToArrayAsync());
        Task<string> Rows() => F.DbAsync(db => db.Database.SqlQueryRaw<string>("""
            SELECT jsonb_build_object(
                'manifest', (SELECT jsonb_agg(to_jsonb(m) - 'deliverable_issue_id' ORDER BY id) FROM hub.review_manifest_item m),
                'findings', (SELECT jsonb_agg(to_jsonb(f) - 'deliverable_issue_id' - 'external_review_id' ORDER BY id) FROM hub.review_finding f),
                'sources', (SELECT jsonb_agg(to_jsonb(s) ORDER BY id) FROM hub.source_revision s),
                'issues', (SELECT jsonb_agg(to_jsonb(i) - 'issued_author_ids' ORDER BY id) FROM hub.deliverable_issue i)
            )::text AS "Value"
            """).SingleAsync());
        var history = await History(); Assert.Contains(previous, history); Assert.Contains(migration, history);
        var columns = await Columns(); var rows = await Rows();

        await F.DbAsync(async db => { await db.GetService<IMigrator>().MigrateAsync(previous); return 0; });
        Assert.Equal(new[] {
            "review_finding.originator_id uuid NO <no-default>",
            "review_finding.source_revision_id uuid NO <no-default>",
            "review_finding.verifier_id uuid NO <no-default>",
            "review_manifest_item.source_revision_id uuid NO <no-default>",
        }, await Columns());
        Assert.True(await F.DbAsync(db => db.Database.SqlQueryRaw<bool>(
            "SELECT to_regclass('hub.external_review') IS NULL AS \"Value\"").SingleAsync()));
        Assert.Equal(history.TakeWhile(id => string.CompareOrdinal(id, previous) <= 0), await History());
        Assert.Equal(rows, await Rows());

        // No host/API work while downgraded: the same provider migrator restores the actual latest applied schema.
        await F.DbAsync(async db => { await db.GetService<IMigrator>().MigrateAsync(history[^1]); return 0; });
        Assert.Equal(history, await History()); Assert.Equal(columns, await Columns()); Assert.Equal(rows, await Rows());
        Assert.Equal(engine, t.State(control.Project.Id)); Assert.False(F.Db(db => db.Set<ExternalReview>().Any()));
        Assert.Equal(identity, F.Db(db => db.ReviewFindings.AsNoTracking().Where(row => row.Id == finding)
            .Select(row => new { row.SourceRevisionId, row.OriginatorId, row.VerifierId }).Single()));
    }

    [Fact]
    public async Task Actual_future_issue_authors_refuse_downgrade_with_55000_before_schema_data_or_history_changes()
    {
        const string previous = "20261102234729_NoticeDeliveryPolicyWitness";
        // Actual issue command with presentation OFF; no receipt, issued manifest, or finding can mask the authors-only guard.
        var s = await New(presentationOn: false, package: false); var authors = Authors(s.Issue);
        Assert.Equal(new[] { Data.User(TestData.Alex), Data.User(TestData.Marc) }.OrderBy(id => id), authors.OrderBy(id => id));
        Assert.False(F.Db(db => db.Set<ExternalReview>().Any()));
        Assert.False(F.Db(db => db.ReviewManifestItems.Any(row => row.DeliverableIssueId != null)));
        Assert.False(F.Db(db => db.ReviewFindings.Any(row => row.DeliverableIssueId != null || row.ExternalReviewId != null)));
        Task<string[]> History() => F.DbAsync(async db => (await db.Database.GetAppliedMigrationsAsync()).ToArray());
        Task<string[]> Schema() => F.DbAsync(db => db.Database.SqlQueryRaw<string>("""
            SELECT 'column ' || table_name || '.' || column_name || ' ' || udt_name || ' ' || is_nullable || ' ' ||
                COALESCE(column_default, '<no-default>') AS "Value"
            FROM information_schema.columns WHERE table_schema = 'hub'
            UNION ALL
            SELECT 'constraint ' || t.relname || '.' || c.conname || ' ' || pg_get_constraintdef(c.oid)
            FROM pg_constraint c JOIN pg_class t ON t.oid = c.conrelid JOIN pg_namespace n ON n.oid = t.relnamespace
            WHERE n.nspname = 'hub'
            UNION ALL
            SELECT 'index ' || tablename || '.' || indexname || ' ' || indexdef FROM pg_indexes WHERE schemaname = 'hub'
            ORDER BY "Value"
            """).ToArrayAsync());
        Task<string> Rows() => F.DbAsync(db => db.Database.SqlQueryRaw<string>("""
            SELECT jsonb_build_object(
                'issues', (SELECT jsonb_agg(to_jsonb(i) ORDER BY id) FROM hub.deliverable_issue i),
                'manifest', (SELECT jsonb_agg(to_jsonb(m) ORDER BY id) FROM hub.review_manifest_item m),
                'findings', (SELECT jsonb_agg(to_jsonb(f) ORDER BY id) FROM hub.review_finding f),
                'receipts', (SELECT jsonb_agg(to_jsonb(r) ORDER BY id) FROM hub.external_review r)
            )::text AS "Value"
            """).SingleAsync());
        var history = await History(); Assert.Contains("20261103042711_ExternalIssuedReviewSources", history);
        var schema = await Schema(); var rows = await Rows(); var state = State(s);

        var thrown = await Assert.ThrowsAnyAsync<Exception>(() => F.DbAsync(async db => {
            await db.GetService<IMigrator>().MigrateAsync(previous); return 0;
        }));
        // Npgsql's execution strategy may wrap the server refusal; its exact cause must still be 55000.
        var error = Assert.IsType<PostgresException>(thrown.GetBaseException());
        Assert.Equal("55000", error.SqlState);
        Assert.Equal(history, await History()); Assert.Equal(schema, await Schema()); Assert.Equal(rows, await Rows());
        Assert.Equal(state, State(s)); Assert.Equal(authors, Authors(s.Issue));
        Assert.Equal(s.Issue, F.Db(db => db.Deliverables.Single(row => row.Id == s.Deliverable).CurrentIssueId));
    }

    async Task<Guid> TrustedLegacyUnknown(Scenario s)
    {
        // Explicit pre-feature storage fixture, NOT a future issue command or evidence of a technical signature.
        // Current owners/IssuedBy are deliberately insufficient to reconstruct this historical NULL snapshot.
        var row = new DeliverableIssue { ProjectId = s.Project.Id, DeliverableId = s.Deliverable, Revision = "Legacy unknown",
            IssuedDate = new(2026, 9, 1), CreatedAt = new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero), IssuedBy = Data.User(TestData.Marc), IssuedAuthorIds = null };
        await F.DbAsync(async db => { db.DeliverableIssues.Add(row); return await db.SaveChangesAsync(); });
        return row.Id;
    }
    async Task GuardRefuses<T>(Guid id, Action<T> change) where T : Entity
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => F.DbAsync(async db => {
            var row = await db.Set<T>().SingleAsync(value => value.Id == id); change(row); return await db.SaveChangesAsync();
        }));
    }
    async Task ProviderRefuses(Func<HubDb, Task<int>> command, string code = PostgresErrorCodes.CheckViolation)
    {
        var error = await Assert.ThrowsAsync<PostgresException>(() => F.DbAsync(command)); Assert.Equal(code, error.SqlState);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Trusted_legacy_NULL_is_unknown_before_self_review_or_admin_and_cannot_be_backfilled(bool allowSelf)
    {
        var s = await New(package: false); var legacy = await TrustedLegacyUnknown(s); await SelfReview(allowSelf);
        var before = State(s);
        var options = await (await F.As(TestData.Pm).GetAsync(Root(s) + "/reviews/issued-sources?page=1&pageSize=1")).Json();
        Assert.Equal(2, options.I("totalCount")); Assert.Single(options["items"]!.AsArray());
        var all = await (await F.As(TestData.Pm).GetAsync(Root(s) + "/reviews/issued-sources?page=1&pageSize=200")).Json();
        var unknown = all["items"]!.AsArray().Single(row => row!.G("deliverableIssueId") == legacy)!;
        Assert.Equal("Unknown", unknown.S("authorsState")); Assert.False(unknown["selectable"]!.GetValue<bool>()); Assert.NotNull(unknown["reason"]);
        var guidance = await (await F.As(TestData.Pm).GetAsync(Root(s) + $"/review-issued-assignment-guidance?issuedRevisionIds={legacy}")).Json();
        Assert.Equal(allowSelf, guidance["allowSelfReview"]!.GetValue<bool>()); Assert.False(guidance["sourcesAvailable"]!.GetValue<bool>());
        foreach (var who in new[] { TestData.Marc, TestData.Admin }) {
            var error = await Post(who, Root(s) + "/reviews", CreateBody(s, [legacy]), 400);
            Assert.NotNull(error["errors"]?["deliverableIssueId"]); Assert.Equal(before, State(s));
        }
        await GuardRefuses<DeliverableIssue>(legacy, row => row.IssuedAuthorIds = Authors(s.Issue));
        await GuardRefuses<DeliverableIssue>(s.Issue, row => row.IssuedAuthorIds = null);
        await GuardRefuses<DeliverableIssue>(s.Issue, row => row.IssuedAuthorIds = [Data.User(TestData.Jill)]);
        Assert.Null(F.Db(db => db.DeliverableIssues.Single(row => row.Id == legacy).IssuedAuthorIds));
        Assert.Equal(before, State(s));
    }

    async Task Decide(Scenario s, string who, Guid assignment, int status = 200) => await Post(who,
        PackagePath(s) + $"/assignments/{assignment}/decision", new ReviewEndpoints.DecisionBody(Guid.NewGuid(),
            V<DisciplineReview>(assignment), DisciplineReviewStatus.Approved, "Independently checked the actual issued scope"), status);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Deliberately_corrupt_stored_snapshot_blocks_real_start_record_designation_closure_and_final_approval(bool allowSelf)
    {
        foreach (var phase in new[] { "start", "record", "designation", "closure", "approval" })
        {
            await SelfReview(false); var s = await New(start: phase != "start"); Guid finding = Guid.Empty; Guid assignment = Guid.Empty;
            if (phase is "designation" or "closure" or "approval") {
                await Record(s, Body(s) with { Comments = [new("Actual finding before storage corruption", s.Civil, Data.User(TestData.Alex), "Blocking")] });
                finding = Assert.Single(Findings(s)).Id;
            }
            if (phase is "closure" or "approval") {
                await MakeReviewer(s, TestData.Admin);
                var verifier = phase == "closure" ? TestData.Admin : TestData.Jill;
                await Act(s, finding, "assignVerifier", TestData.Pm, owner: Data.User(verifier));
                await Act(s, finding, FindingStatus.Responded, TestData.Alex, outcome: "Clarified");
                if (phase == "approval") {
                    await Act(s, finding, FindingStatus.VerifiedClosed, TestData.Jill, evidence: VerifierEvidence);
                    assignment = F.Db(db => db.DisciplineReviews.Single(row => row.RoundId == s.Round && row.ProjectDisciplineId == s.Civil).Id);
                    await Post(TestData.Pm, PackagePath(s) + $"/assignments/{assignment}/assign", new ReviewEndpoints.ReassignBody(
                        Guid.NewGuid(), V<DisciplineReview>(assignment), Data.User(TestData.Admin), "Explicitly appoint the independent administrator reviewer"));
                    var electrical = F.Db(db => db.DisciplineReviews.Single(row => row.RoundId == s.Round && row.ProjectDisciplineId == s.Electrical).Id);
                    await Decide(s, TestData.Omar, electrical);
                }
            }
            // ROOT-APPROVED STORAGE-CORRUPTION injection: bypass application immutability ONLY on the actual
            // issue's private authors. Every ID, manifest, receipt, event and prior signature above came from real APIs.
            // This is not a legacy issue, a supported edit, a repaired source, or fabricated approval evidence.
            await F.DbAsync(db => db.DeliverableIssues.Where(row => row.Id == s.Issue)
                .ExecuteUpdateAsync(set => set.SetProperty(row => row.IssuedAuthorIds, (Guid[]?)null)));
            await SelfReview(allowSelf); var before = State(s); JsonNode error;
            if (phase == "start") error = await Post(TestData.Admin, PackagePath(s) + "/action",
                new ReviewEndpoints.ActionBody(Guid.NewGuid(), V<ReviewPackage>(s.Package), "start", "Attempt actual start against unknown stored provenance"), 400);
            else if (phase == "record") error = await Record(s, who: TestData.Admin, status: 400);
            else if (phase == "designation") error = await Act(s, finding, "assignVerifier", TestData.Admin, owner: Data.User(TestData.Jill), status: 400);
            else if (phase == "closure") error = await Act(s, finding, FindingStatus.VerifiedClosed, TestData.Admin, evidence: VerifierEvidence, status: 400);
            else {
                error = await Post(TestData.Admin, PackagePath(s) + $"/assignments/{assignment}/decision",
                    new ReviewEndpoints.DecisionBody(Guid.NewGuid(), V<DisciplineReview>(assignment), DisciplineReviewStatus.Approved, "Attempt the last actual approval"), 400);
            }
            Assert.NotNull(error["errors"]?["deliverableIssueId"]); Assert.Equal(before, State(s));
            Assert.NotEqual(ReviewStatus.Approved, F.Db(db => db.ReviewPackages.Single(row => row.Id == s.Package).Status));
        }
    }

    [Fact]
    public async Task Same_GUID_party_and_user_stay_typed_snapshot_identity_survives_rename_and_scalar_FK_targets_party()
    {
        var s = await New(); var user = Data.User(TestData.Jill);
        // Approved metadata-only collision fixture. Jill is the existing real user, not a fabricated account or author.
        var party = new ExternalParty { Id = user, ProjectId = s.Project.Id, Name = "Synthetic colliding party", Organisation = "Captured organisation",
            Email = PartyEmail, Notes = PartyNotes };
        await F.DbAsync(async db => { db.ExternalParties.Add(party); return await db.SaveChangesAsync(); });
        s = s with { Party = party.Id }; var count = F.Db(db => db.Users.Count()); var receiptId = (await Record(s)).G("id");
        Assert.Equal(count, F.Db(db => db.Users.Count()));
        var detail = await Detail(s); var row = detail["findings"]!.AsArray().First()!;
        Assert.Equal("ExternalParty", row["originator"]!.S("kind")); Assert.Null(row["originator"]!["userId"]);
        Assert.Equal(user, row["originator"]!.G("externalPartyId")); Assert.Equal("Synthetic colliding party", row["originator"]!.S("name"));
        Assert.Null(row["originatorId"]); Assert.Null(row["verifierId"]);
        (await F.As(TestData.Pm).Patch($"/api/v1/external-parties/{party.Id}", new { name = "Renamed live party", organisation = "Renamed organisation", isActive = false }, V<ExternalParty>(party.Id))).EnsureSuccessStatusCode();
        var receipt = await (await F.As(TestData.Pm).GetAsync(RecordPath(s) + $"/{receiptId}")).Json();
        Assert.Equal("Synthetic colliding party", receipt.S("partyName")); Assert.Equal("Captured organisation", receipt.S("partyOrganisation"));
        Assert.Equal("Inactive", receipt.S("partyState"));
        var register = await (await F.As(TestData.Pm).GetAsync(Root(s) + $"/review-findings?originatorKind=ExternalParty&originatorId={user}&allRounds=true")).Json();
        Assert.Equal(2, register.I("totalCount")); Assert.All(register["items"]!.AsArray(), item => Assert.Equal("Synthetic colliding party", item!["originator"]!.S("name")));
        var internalOnly = await (await F.As(TestData.Pm).GetAsync(Root(s) + $"/review-findings?originatorKind=User&originatorId={user}&allRounds=true")).Json();
        Assert.Empty(internalOnly["items"]!.AsArray());
        Assert.False(F.Db(db => db.ExternalParties.Any(value => value.Id == Data.User(TestData.Alex))));
        await ProviderRefuses(db => db.Set<ExternalReview>().Where(value => value.Id == receiptId)
            .ExecuteUpdateAsync(set => set.SetProperty(value => value.ExternalPartyId, Data.User(TestData.Alex))), PostgresErrorCodes.ForeignKeyViolation);
    }

    [Fact]
    public async Task Real_rows_enforce_source_origin_XOR_internal_verifier_and_immutable_capture_and_receipt()
    {
        var s = await New(); var receipt = (await Record(s)).G("id"); var finding = Findings(s)[0].Id;
        var control = await t.New(s.Project, "Actual internal source control"); var internalFinding = await t.Finding(control);
        var manifest = F.Db(db => db.ReviewManifestItems.Single(row => row.RoundId == s.Round).Id); var before = State(s);
        await ProviderRefuses(db => db.ReviewFindings.Where(row => row.Id == finding)
            .ExecuteUpdateAsync(set => set.SetProperty(row => row.SourceRevisionId, (Guid?)control.Source)));
        await ProviderRefuses(db => db.ReviewFindings.Where(row => row.Id == finding)
            .ExecuteUpdateAsync(set => set.SetProperty(row => row.DeliverableIssueId, (Guid?)null)));
        await ProviderRefuses(db => db.ReviewFindings.Where(row => row.Id == finding)
            .ExecuteUpdateAsync(set => set.SetProperty(row => row.OriginatorId, (Guid?)Data.User(TestData.Jill))));
        await ProviderRefuses(db => db.ReviewFindings.Where(row => row.Id == finding)
            .ExecuteUpdateAsync(set => set.SetProperty(row => row.ExternalReviewId, (Guid?)null)));
        await ProviderRefuses(db => db.ReviewFindings.Where(row => row.Id == internalFinding)
            .ExecuteUpdateAsync(set => set.SetProperty(row => row.VerifierId, (Guid?)null)));
        await ProviderRefuses(db => db.ReviewManifestItems.Where(row => row.Id == manifest)
            .ExecuteUpdateAsync(set => set.SetProperty(row => row.SourceRevisionId, (Guid?)control.Source)));
        await ProviderRefuses(db => db.ReviewManifestItems.Where(row => row.Id == manifest)
            .ExecuteUpdateAsync(set => set.SetProperty(row => row.DeliverableIssueId, (Guid?)null)));
        await ProviderRefuses(db => db.DeliverableIssues.Where(row => row.Id == s.Issue)
            .ExecuteUpdateAsync(set => set.SetProperty(row => row.IssuedAuthorIds, Array.Empty<Guid>())));
        await ProviderRefuses(db => db.DeliverableIssues.Where(row => row.Id == s.Issue)
            .ExecuteUpdateAsync(set => set.SetProperty(row => row.IssuedAuthorIds, new[] { Guid.Empty })));
        await GuardRefuses<ExternalReview>(receipt, row => row.PartyNameSnapshot = "Rewritten receipt");
        await GuardRefuses<ExternalReview>(receipt, row => row.ReceivedDate = new(2026, 11, 2));
        await GuardRefuses<ExternalReview>(receipt, row => row.ReturnCodeSnapshot = "A");
        await GuardRefuses<ReviewFinding>(finding, row => row.SourceRevisionId = control.Source);
        Assert.Equal(before, State(s));
    }

    [Theory]
    [InlineData("anotherReceiptAndDeliverable")]
    [InlineData("sameRound")]
    [InlineData("futureRound")]
    public async Task Deliberately_corrupt_carry_is_not_actionable_and_cannot_pass_command_or_save_guards(string corruption)
    {
        var s = await New(package: false); var otherIssue = await AnotherIssue(s);
        var package = (await Post(TestData.Marc, Root(s) + "/reviews", CreateBody(s, [s.Issue, otherIssue]))).G("id");
        s = s with { Package = package, Round = F.Db(db => db.ReviewPackages.Single(row => row.Id == package).CurrentRoundId!.Value) }; await Start(s);
        var a = Body(s); var receipt = (await Record(s, a)).G("id");
        var original = Findings(s).Single(row => row.ExternalReviewId == receipt && row.Text == a.Comments[0].Text);
        var sibling = Findings(s).Single(row => row.ExternalReviewId == receipt && row.Text == a.Comments[1].Text);
        var b = Body(s) with { DeliverableIssueId = otherIssue, IssueRowVersion = V<DeliverableIssue>(otherIssue),
            Comments = [new("Different real receipt and deliverable", s.Civil, Data.User(TestData.Alex), "Blocking")] };
        var otherReceipt = (await Record(s, b)).G("id"); var otherOriginal = Findings(s).Single(row => row.ExternalReviewId == otherReceipt);
        async Task Next() => await Post(TestData.Marc, PackagePath(s) + "/rounds", new ReviewEndpoints.RoundBody(Guid.NewGuid(),
            V<ReviewPackage>(s.Package), "Actual next round for carry integrity", [], Assignments(s), "Carry both real issued scopes", null, [s.Issue, otherIssue]));
        await Next();
        var current = Findings(s).Single(row => row.CarriedFromId == original.Id);
        var currentSibling = Findings(s).Single(row => row.CarriedFromId == sibling.Id);
        var corruptId = current.Id; var badPrevious = corruption == "sameRound" ? currentSibling.Id : otherOriginal.Id;
        if (corruption == "futureRound") {
            var second = current; await Next();
            current = Findings(s).Single(row => row.CarriedFromId == second.Id);
            badPrevious = Findings(s).Single(row => row.CarriedFromId == currentSibling.Id).Id; corruptId = second.Id;
        }
        (await F.As(TestData.Admin).Put("/api/v1/admin/settings/flag.screen_patterns", new { value = true })).EnsureSuccessStatusCode();
        // Approved STORAGE-CORRUPTION fixture: only an ancestry edge is changed. All users/issues/receipts/findings/
        // rounds came from the real commands above; no approval or signature is fabricated or claimed by this write.
        await F.DbAsync(db => db.ReviewFindings.Where(row => row.Id == corruptId)
            .ExecuteUpdateAsync(set => set.SetProperty(row => row.CarriedFromId, (Guid?)badPrevious)));
        var before = State(s);
        var child = await (await F.As(TestData.Alex).GetAsync(Root(s) + $"/review-findings/{current.Id}")).Json();
        Assert.Equal(RecordContextAction.NotAssessed, child["context"]!.S("nextAction")); Assert.Empty(child["context"]!["nextActors"]!.AsArray());
        await Act(s, current.Id, FindingStatus.Responded, TestData.Alex, outcome: "Noted", status: 400);
        await GuardRefuses<ReviewFinding>(current.Id, row => row.Text += " Rejected corrupt edit");
        Assert.Equal(before, State(s));
    }
}
