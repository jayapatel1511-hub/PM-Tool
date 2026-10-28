using System.Text.Json;
using System.Text.Json.Nodes;
using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Features;

/// Show deleted and Restore (§49.2, FR-RCV-01 to FR-RCV-06), behind `flag.show_deleted`: a project's deleted tasks,
/// deliverables or milestones, newest deletion first, for those who may restore them; and the checks every restore shares.
public static class RecoveryEndpoints
{
    public const string Flag = "show_deleted";
    static readonly string[] Kinds = ["tasks", "deliverables", "milestones"];

    public sealed class Row
    {
        public Guid Id { get; init; }
        public string Key { get; init; } = "";
        public string Name { get; init; } = "";
        public string? Discipline { get; init; }
        public string? ParentKey { get; init; }
        public bool ParentDeleted { get; init; }
        public string? Status { get; init; }
        public DateTimeOffset DeletedAt { get; init; }
        public string? DeletedBy { get; init; }
        public int RowVersion { get; init; }
    }

    /// One deleted task, deliverable or milestone as the Show deleted list and its export carry it; RestoreBlocked names the deleted parent to restore first.
    public sealed record DeletedItem(Guid Id, string Key, string Name, string? Discipline, string? ParentKey, string? Status, DateTimeOffset DeletedAt, string? DeletedBy,
        int RowVersion, string? RestoreBlocked);

    static readonly Col[] Cols =
    [
        new("key", "key", "key"), new("name", "name"), new("discipline", "discipline"), new("parentKey", "parent"), new("status", "status"),
        new("deletedAt", "deletedAt", "datetime"), new("deletedBy", "deletedBy"), new("restoreBlocked", "restoreBlocked"),
    ];

    public static void Map(RouteGroupBuilder api)
    {
        foreach (var kind in Kinds)
        {
            api.MapGet($"/projects/{{id:guid}}/{kind}/deleted", async Task<Ok<Page<DeletedItem>>> (Guid id, string? q, int? page, int? pageSize, Access access, HubDb db) =>
            {
                await Demand(access, id);
                var (pg, size) = Http.Paging(page, pageSize ?? 200);
                var rows = Query(db, kind, id, q);
                var total = await rows.CountAsync();
                var items = await rows.Skip((pg - 1) * size).Take(size).ToListAsync();
                return TypedResults.Ok(new Page<DeletedItem>(items.Select(Shape).ToList(), pg, size, total));
            }).RequireFlag(Flag);
            api.MapGet($"/projects/{{id:guid}}/{kind}/deleted/export", async (Guid id, string? q, string? format, HttpContext http, Access access, HubDb db, SettingsStore store, TimeProvider clock) =>
            {
                var p = await Demand(access, id);
                var rows = await Query(db, kind, id, q).Take(Export.MaxRows + 1).ToListAsync();
                return await ExportFile.Send(db, store, format, Text.Get($"export.deleted_{kind}", p.ProjectNumber), Cols,
                    JsonSerializer.SerializeToNode(rows.Select(Shape), JsonOpts.Web)!.AsArray(), await ListExportEndpoints.Filters(db, http, access), p.Id,
                    $"{p.ProjectNumber}-deleted-{kind}", clock);
            }).RequireFlag(Flag);
        }
    }

    /// FR-RCV-02: only those who may restore read the list; anyone else gets 403 (or 404 for a project they cannot see).
    static async Task<Project> Demand(Access access, Guid projectId)
    {
        var (p, ctx) = await access.Project(projectId, track: false);
        Access.Demand(Permissions.RestoreItem(access.Actor, ctx));
        return p;
    }

    static DeletedItem Shape(Row r) => new(r.Id, r.Key, r.Name, r.Discipline, r.ParentKey, r.Status, r.DeletedAt, r.DeletedBy, r.RowVersion,
        r.ParentDeleted ? Text.Get("restore.parent_first", r.ParentKey!) : null);

    /// A plain projection over deleted rows. IgnoreQueryFilters covers the whole query, so a parent or person shows even when deleted.
    static IQueryable<Row> Query(HubDb db, string kind, Guid projectId, string? q)
    {
        var rows = kind switch
        {
            "tasks" => db.Tasks.IgnoreQueryFilters().AsNoTracking().Where(t => t.ProjectId == projectId && t.DeletedAt != null).Select(t => new Row
            {
                Id = t.Id, Key = t.Key, Name = t.Name, Status = t.Status, DeletedAt = t.DeletedAt!.Value, RowVersion = t.RowVersion,
                Discipline = db.ProjectDisciplines.Where(pd => pd.Id == t.ProjectDisciplineId).Select(pd => pd.Discipline!.Name).FirstOrDefault(),
                DeletedBy = db.Users.Where(u => u.Id == t.DeletedBy).Select(u => u.DisplayName).FirstOrDefault(),
                ParentKey = db.Deliverables.Where(d => d.Id == t.DeliverableId).Select(d => d.Key).FirstOrDefault()
                    ?? db.Milestones.Where(m => m.Id == t.MilestoneId).Select(m => m.Key).FirstOrDefault(),
                ParentDeleted = db.Deliverables.Any(d => d.Id == t.DeliverableId && d.DeletedAt != null) || db.Milestones.Any(m => m.Id == t.MilestoneId && m.DeletedAt != null),
            }),
            "deliverables" => db.Deliverables.IgnoreQueryFilters().AsNoTracking().Where(x => x.ProjectId == projectId && x.DeletedAt != null).Select(x => new Row
            {
                Id = x.Id, Key = x.Key, Name = x.Name, Status = x.Status, DeletedAt = x.DeletedAt!.Value, RowVersion = x.RowVersion,
                Discipline = db.ProjectDisciplines.Where(pd => pd.Id == x.ProjectDisciplineId).Select(pd => pd.Discipline!.Name).FirstOrDefault(),
                DeletedBy = db.Users.Where(u => u.Id == x.DeletedBy).Select(u => u.DisplayName).FirstOrDefault(),
                ParentKey = db.Milestones.Where(m => m.Id == x.MilestoneId).Select(m => m.Key).FirstOrDefault(),
                ParentDeleted = db.Milestones.Any(m => m.Id == x.MilestoneId && m.DeletedAt != null),
            }),
            _ => db.Milestones.IgnoreQueryFilters().AsNoTracking().Where(m => m.ProjectId == projectId && m.DeletedAt != null).Select(m => new Row
            {
                Id = m.Id, Key = m.Key, Name = m.Name, DeletedAt = m.DeletedAt!.Value, RowVersion = m.RowVersion,
                Status = m.IsComplete ? MilestoneStatus.Complete : m.IsCancelled ? MilestoneStatus.Cancelled
                    : db.MilestoneStates.Where(s => s.MilestoneId == m.Id).Select(s => s.Status).FirstOrDefault(),
                Discipline = db.ProjectDisciplines.Where(pd => pd.Id == m.ProjectDisciplineId).Select(pd => pd.Discipline!.Name).FirstOrDefault(),
                DeletedBy = db.Users.Where(u => u.Id == m.DeletedBy).Select(u => u.DisplayName).FirstOrDefault(),
            }),
        };
        if (!string.IsNullOrWhiteSpace(q)) { var term = $"%{q.Trim()}%"; rows = rows.Where(r => EF.Functions.ILike(r.Name, term) || EF.Functions.ILike(r.Key, term)); }
        return rows.OrderByDescending(r => r.DeletedAt).ThenBy(r => r.Key);
    }

    /// FR-RCV-04, FR-RCV-05: a stale `If-Match`, when sent, is refused (409); a child whose deliverable or milestone is deleted is refused naming it.
    public static async Task Guard(HubDb db, HttpContext http, Audited item, Guid? deliverableId, Guid? milestoneId)
    {
        if (Http.IfMatch(http) is not null) await Http.CheckVersion(db, http, item);
        var parent = await db.Deliverables.IgnoreQueryFilters().Where(d => d.Id == deliverableId && d.DeletedAt != null).Select(d => d.Key).FirstOrDefaultAsync()
            ?? await db.Milestones.IgnoreQueryFilters().Where(m => m.Id == milestoneId && m.DeletedAt != null).Select(m => m.Key).FirstOrDefaultAsync();
        if (parent is not null) throw ApiException.Rule("parent_deleted", "restore.parent_first", new { parentKey = parent }, parent);
    }
}
