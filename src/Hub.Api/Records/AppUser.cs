using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
namespace Hub.Api.Records;
public static class AppUserRecord { public static RegistryEntry Entry { get; } = new(typeof(Hub.Api.Data.AppUser), "User", ["DisplayName", "Email", "JobTitle", "OfficeId", "SupervisorId", "HomeDisciplineId", "IsActive", "WeeklyCapacityHours", "IsTemplateEditor", "HrSourceSystem", "HrPersonKey", "UnitId"], false, "no item-history surface today; child and organisation history retain existing queries")
    {
        NameOrder = 0,
        Names = async (db, list) => (await db.Users.Where(x => list.Contains(x.Id)).Select(x => new { x.Id, x.DisplayName, x.IsActive }).ToListAsync())
            .Select(x => (x.Id, x.IsActive ? x.DisplayName : x.DisplayName + " (Inactive)")),
        ExportNameOrder = 0,
        ExportNames = async (db, access, list) => { var visible = access.VisibleProjectIds(); return (await db.Users.Where(u => list.Contains(u.Id) &&
                (db.Projects.Any(p => visible.Contains(p.Id) && p.ProjectManagerId == u.Id) ||
                 db.ProjectMembers.Any(m => visible.Contains(m.ProjectId) && m.UserId == u.Id && m.RemovedAt == null) ||
                 db.ProjectDisciplines.Any(d => visible.Contains(d.ProjectId) && d.LeadUserId == u.Id) ||
                 db.Tasks.IgnoreQueryFilters().Any(t => visible.Contains(t.ProjectId) && (t.AssigneeId == u.Id || t.ReviewerId == u.Id)) ||
                 db.Deliverables.IgnoreQueryFilters().Any(d => visible.Contains(d.ProjectId) && (d.OwnerId == u.Id || d.ReviewerId == u.Id)) ||
                 db.Decisions.IgnoreQueryFilters().Any(d => visible.Contains(d.ProjectId) && (d.OwnerUserId == u.Id || d.RequestedById == u.Id)) ||
                 db.Risks.IgnoreQueryFilters().Any(r => visible.Contains(r.ProjectId) && r.OwnerId == u.Id) ||
                 db.Issues.IgnoreQueryFilters().Any(i => visible.Contains(i.ProjectId) && i.OwnerId == u.Id) ||
                 db.Actions.IgnoreQueryFilters().Any(a => visible.Contains(a.ProjectId) && a.OwnerUserId == u.Id) ||
                 db.ActivityLog.Any(a => a.ProjectId != null && visible.Contains(a.ProjectId.Value) && a.ActorUserId == u.Id)))
            .Select(u => new { u.Id, u.DisplayName, u.IsActive }).ToListAsync()).Select(u => (u.Id, u.IsActive ? u.DisplayName : u.DisplayName + " (Inactive)")); }
    };
}