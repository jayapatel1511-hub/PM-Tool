using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Features;

/// Only the register additions are validated here; legacy view parameters retain their existing policy.
internal static class DeliverableViewParameters
{
    static readonly string[] Keys = ["purposeOfIssueId", "returnCodeId", "noDueDate", "layout"];
    static readonly string[] Columns = ["clientDrawingNumber", "purposeOfIssue", "returnCode", "responseDate"];
    public static bool Owns(string key) => Keys.Contains(key) || key == "cols";

    public static async Task<Dictionary<string, string>> Write(Dictionary<string, string> parameters, HubDb db, OrgSettings settings)
    {
        foreach (var (key, value) in parameters)
        {
            if (string.IsNullOrWhiteSpace(value)) continue;
            if (Keys.Contains(key))
            {
                Http.RequireFlag(settings, "register_screens");
                Check.That(await Valid(key, value.Trim(), db), key, "error.invalid");
            }
            else if (key == "cols" && Http.List(value).Any(Columns.Contains)) Http.RequireFlag(settings, "register_screens");
        }
        return parameters;
    }

    public static async Task<(string? Value, bool Dropped)> Read(string key, string value, HubDb db, OrgSettings settings)
    {
        if (key == "cols")
        {
            if (settings.IsFlagOn("register_screens")) return (value, false);
            var columns = Http.List(value);
            if (!columns.Any(Columns.Contains)) return (value, false);
            var kept = string.Join(',', columns.Where(column => !Columns.Contains(column)));
            return (kept.Length > 0 ? kept : null, true);
        }
        return settings.IsFlagOn("register_screens") && await Valid(key, value, db) ? (value, false) : (null, true);
    }

    static Task<bool> Valid(string key, string value, HubDb db) => key switch
    {
        "layout" => Task.FromResult(value is "register" or "dates"),
        "noDueDate" => Task.FromResult(value is "true" or "false"),
        "purposeOfIssueId" => Guid.TryParse(value, out var purpose) && purpose != Guid.Empty ? db.IssuePurposes.AnyAsync(row => row.Id == purpose) : Task.FromResult(false),
        "returnCodeId" => Guid.TryParse(value, out var code) && code != Guid.Empty ? db.IssueReturnCodes.AnyAsync(row => row.Id == code) : Task.FromResult(false),
        _ => Task.FromResult(false),
    };
}
