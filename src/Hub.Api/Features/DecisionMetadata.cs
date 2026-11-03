using System.Text.Json;
using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Features;

public sealed record DecisionCategoryChoice(Guid Id, string Name, bool IsActive);
public sealed record DecisionMetadataSummary(DecisionCategoryChoice? Category, int? OptionCount, bool RationaleRecorded);
public sealed record DecisionMetadataDetail(Guid? CategoryId, string[]? OptionsConsidered, string? Rationale, bool CanEditRationale, bool CorrectionReasonRequired);

/// S39: optional context, never a status, permission or historical ownership inference.
public static class DecisionMetadata
{
    public const string Flag = "register_screens";
    public sealed record Prepared(Guid? CategoryId, string[]? OptionsConsidered, string? Rationale);
    public static DecisionMetadataDetail Detail(Decision decision, Actor actor, ProjectContext context) => new(decision.CategoryId, decision.OptionsConsidered, decision.Rationale,
        decision.Status == DecisionStatus.Decided && Permissions.EditRegisterItem(actor, context, DecisionEndpoints.Facts(decision)).Ok && Permissions.DecideOrDefer(actor, context, DecisionEndpoints.Facts(decision)).Ok,
        ProjectStatus.NeedsCorrectionReason(context.Status));
    public static string? Rationale(string? value) => Check.Optional(value, "rationale", 2000);
    static void Require(bool enabled) { if (!enabled) throw ApiException.NotFound(); }

    public static string[]? Options(string[]? values)
    {
        if (values is null) return null;
        Check.That(values.Length <= 5, "optionsConsidered", "error.invalid");
        return values.Select(value => Check.Required(value, "optionsConsidered", 500)).ToArray();
    }

    public static async Task Category(HubDb db, Guid? value, Guid? previous = null)
    {
        if (value is null) return;
        Check.That(value != Guid.Empty && await db.DecisionCategories.AnyAsync(category => category.Id == value && (category.IsActive || category.Id == previous)), "categoryId", "error.invalid");
    }

    public static async Task<Prepared> Create(HubDb db, Guid? categoryId, string[]? options, bool enabled)
    {
        if (categoryId is not null || options is not null) Require(enabled);
        if (!enabled) return new(null, null, null);
        await Category(db, categoryId);
        return new(categoryId, Options(options), null);
    }

    public static async Task<Prepared> Patch(HubDb db, Decision decision, JsonElement body, bool enabled, Actor actor, ProjectContext context)
    {
        Check.That(body.ValueKind == JsonValueKind.Object, "body", "error.invalid");
        var hasCategory = body.TryGetProperty("categoryId", out var category);
        var hasOptions = body.TryGetProperty("optionsConsidered", out var options);
        var hasRationale = body.TryGetProperty("rationale", out var rationale);
        if (hasCategory || hasOptions || hasRationale) Require(enabled);
        Guid? categoryId = decision.CategoryId;
        if (hasCategory)
        {
            if (category.ValueKind == JsonValueKind.Null) categoryId = null;
            else
            {
                Check.That(category.ValueKind == JsonValueKind.String && Guid.TryParse(category.GetString(), out _), "categoryId", "error.invalid");
                categoryId = Guid.Parse(category.GetString()!);
            }
            await Category(db, categoryId, decision.CategoryId);
        }
        var considered = decision.OptionsConsidered;
        if (hasOptions)
        {
            if (options.ValueKind == JsonValueKind.Null) considered = null;
            else
            {
                Check.That(options.ValueKind == JsonValueKind.Array && options.GetArrayLength() <= 5, "optionsConsidered", "error.invalid");
                var values = new List<string>();
                foreach (var value in options.EnumerateArray())
                {
                    Check.That(value.ValueKind == JsonValueKind.String, "optionsConsidered", "error.invalid");
                    values.Add(value.GetString()!);
                }
                considered = Options(values.ToArray());
            }
        }
        var why = decision.Rationale;
        if (hasRationale)
        {
            Check.That(decision.Status == DecisionStatus.Decided, "rationale", "error.invalid");
            Access.Demand(Permissions.DecideOrDefer(actor, context, DecisionEndpoints.Facts(decision)));
            Check.That(rationale.ValueKind is JsonValueKind.Null or JsonValueKind.String, "rationale", "error.invalid");
            why = rationale.ValueKind == JsonValueKind.Null ? null : Rationale(rationale.GetString());
        }
        return new(categoryId, considered, why);
    }

    public static void Apply(Decision decision, Prepared value)
        => (decision.CategoryId, decision.OptionsConsidered, decision.Rationale) = (value.CategoryId, value.OptionsConsidered, value.Rationale);

    public static string? Outcome(string to, string? rationale, bool enabled)
    {
        if (rationale is not null)
        {
            Require(enabled);
            Check.That(to == DecisionStatus.Decided, "rationale", "error.invalid");
        }
        return to == DecisionStatus.Decided && enabled ? Rationale(rationale) : null;
    }
}
