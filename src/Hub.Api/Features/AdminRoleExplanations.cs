using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Hub.Api.Features;

public sealed record AdminRoleChoice(string Key, string Label);
public sealed record AdminPermissionExample(string ActionKey, string ActionLabel, string ResultKind, string ScenarioKey, string ScenarioLabel,
    IReadOnlyList<string> Facts, bool Allowed, string? ReasonCode, string? ReasonText, bool? RequiresReason, string DecisionKind);
public sealed record AdminRoleExplanation(string Key, string Label, string ScopeKind, IReadOnlyList<AdminPermissionExample> Examples);
public sealed record AdminOperationChoice(string Key, string RowId, string Label);
public sealed record AdminPermissionFactor(string ActionKey, string ActionLabel, string ResultKind, bool Eligible,
    string? ReasonCode, string? ReasonText, bool? RequiresReason);
public sealed record AdminPermissionRequirement(string Key, string State, string Text);
public sealed record AdminPermissionOperation(string Key, string Label, string DecisionKind, IReadOnlyList<string> Facts,
    IReadOnlyList<AdminPermissionFactor> Factors, IReadOnlyList<AdminPermissionRequirement> Requirements);
public sealed record AdminPermissionRow(string RowId, string MatrixRowKey, string Label, IReadOnlyList<AdminPermissionOperation> Operations);
public sealed record AdminRoleExamplesRead(string Basis, bool CoverageComplete, IReadOnlyList<string> Gaps,
    IReadOnlyList<AdminRoleChoice> RoleChoices, IReadOnlyList<AdminRoleChoice> ScenarioChoices, IReadOnlyList<AdminRoleExplanation> Roles,
    IReadOnlyList<AdminRoleChoice> SystemRoleChoices, IReadOnlyList<AdminRoleChoice> MembershipChoices,
    IReadOnlyList<AdminOperationChoice> OperationChoices, string SelectedMembershipKey, IReadOnlyList<AdminPermissionRow> Rows);

/// Read-only labelled examples. No caller-supplied person/project/record identity or live authority lookup is accepted.
public static class AdminRoleExplanations
{
    const string RequirementKeyPrefix = "adminRole.requirement.";
    static string RequirementId(string key) => key.StartsWith(RequirementKeyPrefix, StringComparison.Ordinal)
        ? key[RequirementKeyPrefix.Length..] : key;
    static string? Reason(PermissionExampleOutcome outcome) => outcome.ReasonCode is { } why
        ? Text.Get(why, outcome.ReasonArgument ?? "") : null;
    static AdminPermissionFactor Factor(PermissionRowFactor factor) => new(factor.ActionKey, Text.Get(factor.ActionLabelKey),
        factor.ResultKind, factor.Outcome.Allowed, factor.Outcome.ReasonCode, Reason(factor.Outcome), factor.Outcome.RequiresReason);

    public static void Map(RouteGroupBuilder admin)
    {
        admin.MapGet("/role-explanations", async Task<Ok<AdminRoleExamplesRead>> (string? role, string? scenario,
            string? membership, string? operation, HttpContext http, Access access, HubDb db, SettingsStore store) =>
        {
            Access.Demand(Permissions.Administer(access.Actor));
            Http.RequireFlag(await store.Get(db), "people_screens");
            Check.That(http.Request.Query.All(pair => (pair.Key is "role" or "scenario" or "membership" or "operation")
                && pair.Value.Count == 1), "query", "error.invalid");
            var roleKey = role ?? "baseline-Standard";
            var scenarioKey = scenario ?? "same-discipline";
            var membershipKey = membership ?? "none";
            var operationKey = operation ?? "all";
            Check.That(PermissionExplanationCatalog.RoleChoices.Any(choice => choice.Key == roleKey), "role", "error.invalid");
            Check.That(PermissionExplanationCatalog.RowScenarioChoices.Any(choice => choice.Key == scenarioKey), "scenario", "error.invalid");
            Check.That(PermissionExplanationCatalog.MembershipChoices.Any(choice => choice.Key == membershipKey), "membership", "error.invalid");
            Check.That(PermissionExplanationCatalog.OperationChoices.Any(choice => choice.Key == operationKey), "operation", "error.invalid");
            // Preserve legacy discriminants for already-open clients; DecisionKind adds whole-command context.
            var roles = PermissionExplanationCatalog.Read(roleKey, scenarioKey).Select(item => new AdminRoleExplanation(item.Key,
                Text.Get(item.LabelKey), item.ScopeKind, item.Examples.Select(example => new AdminPermissionExample(example.ActionKey,
                    Text.Get(example.ActionLabelKey), example.ResultKind,
                    example.ScenarioKey, Text.Get(example.ScenarioLabelKey), example.FactKeys.Select(key => Text.Get(key)).ToArray(),
                    example.Outcome.Allowed, example.Outcome.ReasonCode, Reason(example.Outcome), example.Outcome.RequiresReason,
                    PermissionExplanationCatalog.LegacyDecision(example))).ToArray())).ToArray();
            var rows = PermissionExplanationCatalog.ReadRows(roleKey, scenarioKey, membershipKey, operationKey)
                .Select(row => new AdminPermissionRow(row.RowId, row.MatrixRowKey, Text.Get(row.LabelKey),
                    row.Operations.Select(item => new AdminPermissionOperation(item.Key, Text.Get(item.LabelKey), item.DecisionKind,
                        item.FactKeys.Select(key => Text.Get(key)).ToArray(), item.Factors.Select(Factor).ToArray(),
                        item.Requirements.Select(requirement => new AdminPermissionRequirement(RequirementId(requirement.Key), requirement.State,
                            Text.Get(requirement.Key))).ToArray())).ToArray())).ToArray();
            return TypedResults.Ok(new AdminRoleExamplesRead("CurrentCanonicalConditionalExamples", false, PermissionExplanationCatalog.CompletionGaps,
                PermissionExplanationCatalog.RoleChoices.Select(choice => new AdminRoleChoice(choice.Key, Text.Get(choice.LabelKey))).ToArray(),
                PermissionExplanationCatalog.RowScenarioChoices.Select(choice => new AdminRoleChoice(choice.Key, Text.Get(choice.LabelKey))).ToArray(), roles,
                PermissionExplanationCatalog.SystemRoleChoices.Select(choice => new AdminRoleChoice(choice.Key, Text.Get(choice.LabelKey))).ToArray(),
                PermissionExplanationCatalog.MembershipChoices.Select(choice => new AdminRoleChoice(choice.Key, Text.Get(choice.LabelKey))).ToArray(),
                PermissionExplanationCatalog.OperationChoices.Select(choice => new AdminOperationChoice(choice.Key, choice.RowId, Text.Get(choice.LabelKey))).ToArray(),
                membershipKey, rows));
        }).RequireFlag("people_screens");
    }
}
