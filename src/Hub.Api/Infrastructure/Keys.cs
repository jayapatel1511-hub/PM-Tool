using Hub.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Infrastructure;

/// Per-project item keys (§9.5, G-08): taken from the project's counters under the row lock of an
/// UPDATE … RETURNING inside the caller's transaction, so keys are unique, gap-free and never reused.
public static class Keys
{
    static readonly Dictionary<string, (string Column, string Prefix, int Width)> Kinds = new()
    {
        ["task"] = ("next_task_seq", "T", 4), ["deliverable"] = ("next_deliverable_seq", "D", 3),
        ["milestone"] = ("next_milestone_seq", "M", 2), ["decision"] = ("next_decision_seq", "DEC", 2),
        ["risk"] = ("next_risk_seq", "R", 2), ["issue"] = ("next_issue_seq", "I", 2), ["action"] = ("next_action_seq", "A", 2),
        ["review"] = ("next_review_seq", "RV", 3), ["change"] = ("next_change_seq", "CH", 3),
        ["submission"] = ("next_submission_seq", "SUB", 3),
        ["handoff"] = ("next_handoff_seq", "H", 3),
    };

    public static async Task<(int Seq, string Key)> Next(HubDb db, Guid projectId, string projectNumber, string kind)
    {
        var seq = await Reserve(db, projectId, kind, 1);
        return (seq, Format(projectNumber, kind, seq));
    }

    /// One fixed statement per kind, built once from the column names above; the project and count are parameters.
    static readonly Dictionary<string, string> ReserveSql = Kinds.ToDictionary(k => k.Key, k =>
        "UPDATE hub.project SET " + k.Value.Column + " = " + k.Value.Column + " + {1} WHERE id = {0} RETURNING " + k.Value.Column + " - {1} AS \"Value\"");

    /// Takes `count` consecutive numbers at once (bulk copies); returns the first.
    public static async Task<int> Reserve(HubDb db, Guid projectId, string kind, int count) =>
        (await db.Database.SqlQueryRaw<int>(ReserveSql[kind], projectId, count).ToListAsync()).Single();

    public static string Format(string projectNumber, string kind, int seq)
    {
        var (_, prefix, width) = Kinds[kind];
        return $"{projectNumber}-{prefix}{seq.ToString().PadLeft(width, '0')}";
    }
}

public static class Tx
{
    /// Runs work in one database transaction (keys, the change and its log entries commit together).
    public static async Task<T> Run<T>(HubDb db, Func<Task<T>> work)
    {
        if (db.Database.CurrentTransaction is not null) return await work();
        await using var tx = await db.Database.BeginTransactionAsync();
        var r = await work();
        await tx.CommitAsync();
        return r;
    }
}
