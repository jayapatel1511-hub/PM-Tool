using System.Net;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

/// Actual HTTP adapter checks in fresh HubFactory databases.
public sealed class AdminRoleOperationExplanationTests
{
    const string Path = "/api/v1/admin/role-explanations";
    static readonly Regex Identifier = new("[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}", RegexOptions.IgnoreCase);

    static async Task Enable(HttpClient admin) =>
        (await admin.Put("/api/v1/admin/settings/flag.people_screens", new { value = true })).EnsureSuccessStatusCode();

    static JsonNode SingleOperation(JsonNode read, string rowId, string operation)
    {
        var row = Assert.Single(read["rows"]!.AsArray())!;
        Assert.Equal(rowId, row.S("rowId"));
        var selected = Assert.Single(row["operations"]!.AsArray())!;
        Assert.Equal(operation, selected.S("key"));
        return selected;
    }

    [Fact]
    public async Task Selected_operation_echoes_explicit_membership_without_silently_adding_a_team_role()
    {
        using var f = new HubFactory(); using var admin = f.As(TestData.Admin); await Enable(admin);
        var noMember = await (await admin.GetAsync(Path + "?operation=W15.operation-1")).Json();
        var legacyExamples = noMember["roles"]!.AsArray().SelectMany(role => role!["examples"]!.AsArray()).ToArray();
        Assert.Contains(legacyExamples, example => example!.S("resultKind") == "Condition");
        Assert.All(legacyExamples, example => Assert.Contains(example!.S("resultKind"), new[] { "Permission", "Condition" }));
        Assert.Equal("none", noMember.S("selectedMembershipKey"));
        Assert.Equal("NotEligible", SingleOperation(noMember, "W15", "W15.operation-1").S("decisionKind"));

        var member = await (await admin.GetAsync(Path + "?role=baseline-Standard&scenario=same-discipline&membership=TeamMember&operation=W15.operation-1")).Json();
        Assert.Equal(ProjectRole.TeamMember, member.S("selectedMembershipKey"));
        var operation = SingleOperation(member, "W15", "W15.operation-1");
        Assert.Equal("8.5.2:Create task", member["rows"]![0]!.S("matrixRowKey"));
        Assert.Equal("Conditional", operation.S("decisionKind"));
        var create = Assert.Single(operation["factors"]!.AsArray(), factor => factor!.S("actionKey") == nameof(Permissions.CreateTask))!;
        Assert.True(create["eligible"]!.GetValue<bool>());
    }

    [Fact]
    public async Task Query_accepts_only_closed_single_choices_and_never_accepts_live_identity_inputs()
    {
        using var f = new HubFactory(); using var admin = f.As(TestData.Admin); await Enable(admin);
        foreach (var query in new[]
        {
            "role=forged", "scenario=forged", "membership=forged", "operation=forged",
            "role=baseline-Standard&role=system-Admin", "scenario=same-discipline&scenario=own-item",
            "membership=none&membership=PM", "operation=C06.respond&operation=C06.withdraw",
            "actorId=00000000-0000-0000-0000-000000005601", "personId=00000000-0000-0000-0000-000000005601",
            "projectId=00000000-0000-0000-0000-000000005603", "recordId=00000000-0000-0000-0000-000000005604",
            "role=system-Admin&membership=PM&operation=C06.respond&userId=00000000-0000-0000-0000-000000005601"
        })
        {
            using var response = await admin.GetAsync(Path + "?" + query);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
    }

    sealed record ReadState(int Activities, int Outbox, int Notifications, int NotificationReads, int NoticeIntents,
        int Idempotency, int CoordinationCommands, int HandoffCommands, int Occurrences, int SeenCommands, int SeenAcknowledgements,
        int Settings, long SeenRevision, int Preferences, int Follows, long UserVersions, int Projects, long ProjectVersions,
        int Tasks, long TaskVersions, int Deliverables, long DeliverableVersions, int Findings, long FindingVersions);

    static ReadState State(HubFactory f) => f.Db(db => new ReadState(
        db.ActivityLog.Count(), db.Outbox.Count(), db.Notifications.Count(), db.Notifications.Count(row => row.ReadAt != null), db.NoticeDeliveryIntents.Count(),
        db.Idempotency.Count(), db.CoordinationCommands.Count(), db.HandoffCommands.Count(), db.WorkAssignmentOccurrences.Count(),
        db.WorkSeenCommandReceipts.Count(), db.WorkSeenAcknowledgements.Count(), db.UserSettings.Count(), db.UserSettings.Sum(row => (long?)row.MyWorkSeenRevision) ?? 0,
        db.NotificationPreferences.Count(), db.Follows.Count(), db.Users.Sum(row => (long?)row.RowVersion) ?? 0,
        db.Projects.IgnoreQueryFilters().Count(), db.Projects.IgnoreQueryFilters().Sum(row => (long?)row.RowVersion) ?? 0,
        db.Tasks.IgnoreQueryFilters().Count(), db.Tasks.IgnoreQueryFilters().Sum(row => (long?)row.RowVersion) ?? 0,
        db.Deliverables.IgnoreQueryFilters().Count(), db.Deliverables.IgnoreQueryFilters().Sum(row => (long?)row.RowVersion) ?? 0,
        db.ReviewFindings.Count(), db.ReviewFindings.Sum(row => (long?)row.RowVersion) ?? 0));

    [Fact]
    public async Task Valid_rows_are_translated_identifier_free_and_readonly_even_when_examples_are_ineligible_or_policy_pending()
    {
        using var f = new HubFactory(); using var admin = f.As(TestData.Admin); await Enable(admin);
        await new TestData(f).Project(); // A real synthetic business record makes accidental updates observable too.
        var before = State(f);
        foreach (var query in new[]
        {
            "role=system-Admin&scenario=private-draft&membership=none&operation=P02.capacity",
            "role=baseline-Standard&scenario=designated-verifier&membership=TeamMember&operation=C06.withdraw",
            "role=system-Admin&scenario=inactive-person&membership=none&operation=W11.operation-1",
            "role=system-Admin&scenario=same-discipline&membership=none&operation=S21.export"
        })
        {
            var read = await (await admin.GetAsync(Path + "?" + query)).Json();
            Assert.False(read["coverageComplete"]!.GetValue<bool>());
            var operation = Assert.Single(Assert.Single(read["rows"]!.AsArray())!["operations"]!.AsArray())!;
            Assert.Contains(operation.S("decisionKind"), new[] { "Conditional", "NotEligible", "PolicyPending" });
            Assert.NotEmpty(operation["requirements"]!.AsArray());
            if (query.EndsWith("S21.export", StringComparison.Ordinal)) Assert.Equal("Conditional", operation.S("decisionKind"));
            if (query.Contains("scenario=inactive-person", StringComparison.Ordinal)) Assert.Equal("NotEligible", operation.S("decisionKind"));
            var text = read.ToJsonString();
            Assert.DoesNotMatch(Identifier, text);
            Assert.DoesNotContain("adminRole.", text);
            Assert.Equal(before, State(f));
        }
    }
}
