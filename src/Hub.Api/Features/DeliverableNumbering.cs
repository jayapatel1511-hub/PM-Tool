using System.Text.Json.Serialization;
using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Hub.Api.Features;

/// Optional drawing-number convention. A mismatch is evidence to show, never a workflow guard.
public static class DeliverableNumberingEndpoints
{
    public sealed record NumberingRead(string? Pattern, int RowVersion, bool CanEdit);
    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    public sealed record NumberingBody([property: JsonRequired] string? Pattern, int? RowVersion, string? Reason);
    public sealed record NumberingSaved(Guid Id, int RowVersion, string? Pattern);

    public static string? Number(string? value) => Plain(value, "clientDrawingNumber");
    public static string? Pattern(string? value) => Plain(value, "pattern");
    static string? Plain(string? value, string field)
    {
        Check.That(value is null || !value.Any(char.IsControl), field, "error.invalid");
        return Check.Optional(value, field, 120);
    }

    /// Entire trimmed string, ordinal literals and one ASCII digit for each #; no executable regex syntax.
    public static bool? Matches(string? number, string? pattern)
    {
        if (string.IsNullOrWhiteSpace(number) || string.IsNullOrWhiteSpace(pattern)) return null;
        number = number.Trim(); pattern = pattern.Trim();
        if (number.Length != pattern.Length) return false;
        for (var index = 0; index < pattern.Length; index++)
            if (pattern[index] == '#' ? number[index] is < '0' or > '9' : pattern[index] != number[index]) return false;
        return true;
    }

    public static void Map(RouteGroupBuilder api)
    {
        var group = api.MapGroup("/projects/{id:guid}/deliverable-numbering").RequireFlag(DeliverableEndpoints.RegisterFlag);
        group.MapGet("", async Task<Ok<NumberingRead>> (Guid id, Access access, HubDb db) =>
        {
            var (project, context) = await access.Project(id, false);
            return TypedResults.Ok(new NumberingRead(project.ClientDrawingNumberPattern, project.RowVersion, Permissions.Writable(access.Actor, context, pmOnly: true).Ok));
        });
        group.MapPut("", async Task<Ok<NumberingSaved>> (Guid id, NumberingBody body, HttpContext http, Access access, HubDb db) => await Tx.Run(db, async () =>
        {
            await Coordination.Lock(db, id);
            var (project, context) = await access.Project(id);
            Access.Demand(Permissions.Writable(access.Actor, context, pmOnly: true));
            await Http.CheckVersion(db, http, project, body.RowVersion);
            var reason = Check.Optional(body.Reason, "reason", 1000);
            ProjectEndpoints.CorrectionReason(project, reason);
            var pattern = Pattern(body.Pattern);
            if (pattern != project.ClientDrawingNumberPattern)
            {
                project.ClientDrawingNumberPattern = pattern;
                db.Audit.Note(project, action: "NumberingPatternChanged", reason: reason);
                await db.SaveChangesAsync();
            }
            return TypedResults.Ok(new NumberingSaved(project.Id, project.RowVersion, project.ClientDrawingNumberPattern));
        }));
    }
}
