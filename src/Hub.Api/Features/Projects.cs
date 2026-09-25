using System.Text.Json;
using System.Text.RegularExpressions;
using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Features;

/// Structure sources applied right after a project is created: copy another project (packet 010) or a template (packet 012).
public interface IProjectCreateHook { Task AfterCreate(Project p, ProjectEndpoints.CreateBody body); }

/// Projects: creation, fields, lifecycle, links, stars, list and header (§12.1, §13.2, §13.16, §15.2).
public static class ProjectEndpoints
{
    public sealed record DisciplineInput(Guid DisciplineId, Guid? LeadUserId);
    public sealed record MemberInput(Guid UserId, string[] Roles, Guid? DisciplineId);
    public sealed record CreateBody(string ProjectNumber, string Name, Guid ClientId, string? ClientReference, Guid OfficeId, Guid? ProjectTypeId,
        string? Description, string? Location, DateOnly? StartDate, DateOnly? TargetCompletionDate, string? Priority, string? CoordinationDay,
        DisciplineInput[]? Disciplines, MemberInput[]? Members, Guid? CopyFromProjectId, Guid? TemplateId, JsonElement? Template);
    public sealed record TransitionBody(string ToStatus, string? Reason, Closeout? Closeout, int? RowVersion);
    public sealed record Closeout(string? Tasks, string? Deliverables, string? Decisions, string? Registers);
    public sealed record LinkBody(string Title, string Url, string? LinkType);

    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/projects", List);
        api.MapPost("/projects", Create);
        api.MapGet("/projects/{idOrNumber}", Get);
        api.MapPatch("/projects/{id:guid}", Edit);
        api.MapGet("/projects/{id:guid}/closeout", CloseoutPreview);
        api.MapPost("/projects/{id:guid}/transition", Transition);
        api.MapGet("/projects/{id:guid}/date-review", DateReview);
        api.MapPost("/projects/{id:guid}/star", async (Guid id, Access access, HubDb db, CurrentUser me, TimeProvider clock) =>
        {
            await access.Project(id, track: false);
            if (!await db.ProjectStars.AnyAsync(s => s.ProjectId == id && s.UserId == me.Id))
                db.ProjectStars.Add(new ProjectStar { ProjectId = id, UserId = me.Id, CreatedAt = clock.GetUtcNow() });
            await db.SaveChangesAsync();
            return Results.NoContent();
        });
        api.MapDelete("/projects/{id:guid}/star", async (Guid id, HubDb db, CurrentUser me) =>
        {
            await db.ProjectStars.Where(s => s.ProjectId == id && s.UserId == me.Id).ExecuteDeleteAsync();
            return Results.NoContent();
        });
        // Header links are document links on the project (§12.1, §12.7); the PM maintains them.
        api.MapPost("/projects/{id:guid}/links", async (Guid id, LinkBody body, Access access, HubDb db, TimeProvider clock) =>
        {
            var (p, ctx) = await access.Project(id);
            Access.Demand(Permissions.EditProject(access.Actor, ctx));
            var link = await DocumentLinkEndpoints.Add(db, id, ItemType.Project, id, p.ProjectNumber, new(body.Url, body.Title, body.LinkType), access.Me.Id, clock.GetUtcNow());
            return Results.Created($"/api/v1/links/{link.Id}", new { link.Id, link.Title, link.Url, link.LinkType });
        });
        api.MapDelete("/projects/{id:guid}/links/{linkId:guid}", async (Guid id, Guid linkId, Access access, HubDb db, TimeProvider clock) =>
        {
            var (p, ctx) = await access.Project(id);
            Access.Demand(Permissions.EditProject(access.Actor, ctx));
            var link = await db.DocumentLinks.FirstOrDefaultAsync(l => l.Id == linkId && l.ItemType == ItemType.Project && l.ItemId == id) ?? throw ApiException.NotFound();
            link.DeletedAt = clock.GetUtcNow(); // DOC-03: soft delete, logged
            link.DeletedBy = access.Me.Id;
            link.AuditKey = p.ProjectNumber;
            await db.SaveChangesAsync();
            return Results.NoContent();
        });
    }

    // ---------- List (§13.2, FR-021) ----------

    public sealed record ProjectQuery(string? Q, string? Status, Guid? PmId, Guid? ClientId, Guid? OfficeId, Guid? PhaseId, Guid? DisciplineId, string? Health,
        Guid? ProjectTypeId, bool? IncludeArchived, bool? Mine, bool? Starred, string? Priority, string? Ids, string? Sort, int? Page, int? PageSize, int? SubmissionWithinDays,
        string? ComputedHealth = null);

    static async Task<object> List([AsParameters] ProjectQuery f, Access access, HubDb db, CurrentUser me, SettingsStore store, TimeProvider clock)
    {
        var items = await Sorted(f, access, db, me, store, clock);
        var (pg, size) = Http.Paging(f.Page, f.PageSize ?? 200);
        return new Page<object>(items.Skip((pg - 1) * size).Take(size).ToList(), pg, size, items.Count, f.Sort);
    }

    /// The project list's rows in list order, unpaged (the list pages them; exports take them all).
    public static async Task<List<object>> Sorted(ProjectQuery f, Access access, HubDb db, CurrentUser me, SettingsStore store, TimeProvider clock)
    {
        var (q, status, pmId, clientId, officeId, phaseId, disciplineId, health, projectTypeId, includeArchived, mine, starred, priority, ids, sort, _, _, submissionWithinDays, computedHealth) = f;
        var s = await store.Get(db);
        var today = clock.Today(s);
        var query = access.VisibleProjects().AsNoTracking();
        var statuses = Http.List(status);
        if (statuses.Length > 0) query = query.Where(p => statuses.Contains(p.Status));
        else if (includeArchived != true) query = query.Where(p => p.Status == ProjectStatus.Active || p.Status == ProjectStatus.Setup || p.Status == ProjectStatus.OnHold);
        var idList = Http.Ids(ids);
        if (idList.Length > 0) query = query.Where(p => idList.Contains(p.Id));
        if (!string.IsNullOrWhiteSpace(q)) query = query.Where(p => EF.Functions.ILike(p.Name, $"%{q.Trim()}%") || EF.Functions.ILike(p.ProjectNumber, $"{q.Trim()}%")
            || db.Clients.Any(c => c.Id == p.ClientId && EF.Functions.ILike(c.Name, $"%{q.Trim()}%")));
        if (pmId is { } pm) query = query.Where(p => p.ProjectManagerId == pm);
        if (clientId is { } c) query = query.Where(p => p.ClientId == c);
        if (officeId is { } o) query = query.Where(p => p.OfficeId == o);
        if (phaseId is { } ph) query = query.Where(p => p.PhaseId == ph);
        if (projectTypeId is { } pt) query = query.Where(p => p.ProjectTypeId == pt);
        var prios = Http.List(priority);
        if (prios.Length > 0) query = query.Where(p => prios.Contains(p.Priority));
        if (disciplineId is { } d) query = query.Where(p => db.ProjectDisciplines.Any(x => x.ProjectId == p.Id && x.DisciplineId == d && x.IsActive));
        if (mine == true) query = query.Where(p => p.ProjectManagerId == me.Id || db.ProjectMembers.Any(m => m.ProjectId == p.Id && m.UserId == me.Id && m.RemovedAt == null));
        if (starred == true) query = query.Where(p => db.ProjectStars.Any(x => x.ProjectId == p.Id && x.UserId == me.Id));

        var rows = await query.Select(p => new
        {
            p.Id, p.ProjectNumber, p.Name, p.Status, p.Priority, p.Visibility, p.StartDate, p.TargetCompletionDate, p.ClientId, p.OfficeId, p.PhaseId,
            p.ProjectTypeId, p.ProjectManagerId, p.HealthOverride, p.HealthOverrideNote, p.HealthOverrideAt, p.HealthOverrideExpiresAt, p.CreatedAt,
            Client = db.Clients.Where(x => x.Id == p.ClientId).Select(x => x.Name).FirstOrDefault(),
            Office = db.Offices.Where(x => x.Id == p.OfficeId).Select(x => x.Name).FirstOrDefault(),
            Phase = db.Phases.Where(x => x.Id == p.PhaseId).Select(x => x.Name).FirstOrDefault(),
            Pm = db.Users.Where(x => x.Id == p.ProjectManagerId).Select(x => x.DisplayName).FirstOrDefault(),
            OverrideBy = db.Users.Where(x => x.Id == p.HealthOverrideBy).Select(x => x.DisplayName).FirstOrDefault(),
            State = db.ProjectStates.Where(x => x.ProjectId == p.Id).Select(x => new { x.ComputedHealth, x.HealthReasons, x.ProgressPct, x.OverdueTasks, x.BlockedTasks,
                x.OverdueDecisions, x.HighIssues, x.AttentionCritical, x.AttentionWarning, x.NextMilestoneId, x.NextSubmissionMilestoneId }).FirstOrDefault(),
            MyRoles = db.ProjectMembers.Where(m => m.ProjectId == p.Id && m.UserId == me.Id && m.RemovedAt == null).Select(m => m.Roles).FirstOrDefault(),
            Leads = db.ProjectDisciplines.Count(x => x.ProjectId == p.Id && x.LeadUserId == me.Id),
            Starred = db.ProjectStars.Any(x => x.ProjectId == p.Id && x.UserId == me.Id),
            LastActivityAt = db.ActivityLog.Where(a => a.ProjectId == p.Id).Max(a => (DateTimeOffset?)a.OccurredAt),
        }).Take(5000).ToListAsync();

        var msIds = rows.SelectMany(r => new[] { r.State?.NextMilestoneId, r.State?.NextSubmissionMilestoneId }).OfType<Guid>().Distinct().ToList();
        var ms = await db.Milestones.AsNoTracking().Where(m => msIds.Contains(m.Id))
            .Select(m => new { m.Id, m.Key, m.Name, m.Date, Status = db.MilestoneStates.Where(x => x.MilestoneId == m.Id).Select(x => x.Status).FirstOrDefault() })
            .ToDictionaryAsync(m => m.Id);
        var now = clock.GetUtcNow();
        var list = rows.Select(r =>
        {
            var computed = r.Status == ProjectStatus.Active ? r.State?.ComputedHealth ?? Health.Grey : Health.Grey;
            var reported = r.HealthOverride is { } ho && r.HealthOverrideExpiresAt > now && r.Status == ProjectStatus.Active ? ho : computed;
            var next = r.State?.NextMilestoneId is { } nm && ms.TryGetValue(nm, out var m1) ? m1 : null;
            var sub = r.State?.NextSubmissionMilestoneId is { } ns && ms.TryGetValue(ns, out var m2) ? m2 : null;
            var roles = (r.MyRoles ?? []).ToList();
            if (r.Leads > 0) roles.Add("DisciplineLead");
            return new
            {
                r.Id, r.ProjectNumber, r.Name, r.Status, r.Priority, r.Visibility, r.StartDate, r.TargetCompletionDate,
                Client = new { Id = r.ClientId, Name = r.Client }, Pm = new { Id = r.ProjectManagerId, DisplayName = r.Pm },
                r.OfficeId, r.Office, r.PhaseId, r.Phase, r.ProjectTypeId,
                ComputedHealth = computed, ReportedHealth = reported, HealthReasons = J.El(r.State?.HealthReasons),
                r.HealthOverrideNote, r.HealthOverrideAt, r.OverrideBy,
                OverrideActive = r.HealthOverride is not null && r.HealthOverrideExpiresAt > now && r.Status == ProjectStatus.Active,
                ProgressPct = r.State?.ProgressPct, OverdueTasks = r.State?.OverdueTasks ?? 0, BlockedTasks = r.State?.BlockedTasks ?? 0,
                OverdueDecisions = r.State?.OverdueDecisions ?? 0, HighIssues = r.State?.HighIssues ?? 0,
                AttentionCritical = r.State?.AttentionCritical ?? 0, AttentionWarning = r.State?.AttentionWarning ?? 0,
                NextMilestone = next, NextSubmission = sub, MyRoles = roles, r.Starred, r.LastActivityAt,
            };
        }).ToList();
        var healths = Http.List(health);
        if (healths.Length > 0) list = list.Where(r => healths.Contains(r.ReportedHealth) || healths.Contains(r.ComputedHealth)).ToList();
        // Computed health alone, for the overview's On Track figure (§36.6): an override never counts as Green there.
        var computedOnly = Http.List(computedHealth);
        if (computedOnly.Length > 0) list = list.Where(r => computedOnly.Contains(r.ComputedHealth)).ToList();
        if (submissionWithinDays is { } within) list = list.Where(r => r.NextSubmission?.Date is { } sd && sd >= today && sd.DayNumber - today.DayNumber <= within).ToList();

        // Default sort: starred first, then health severity (Red, Yellow, Green, Grey), then next milestone date (§13.2).
        IEnumerable<dynamic> sorted = (sort ?? "").Split(':') switch
        {
            ["number", var dir] => dir == "desc" ? list.OrderByDescending(r => r.ProjectNumber) : list.OrderBy(r => r.ProjectNumber),
            ["name", var dir] => dir == "desc" ? list.OrderByDescending(r => r.Name) : list.OrderBy(r => r.Name),
            ["due", var dir] => dir == "desc" ? list.OrderByDescending(r => r.TargetCompletionDate) : list.OrderBy(r => r.TargetCompletionDate ?? DateOnly.MaxValue),
            ["priority", var dir] => dir == "desc" ? list.OrderByDescending(r => Priority.Rank(r.Priority)) : list.OrderBy(r => Priority.Rank(r.Priority)),
            ["progress", var dir] => dir == "desc" ? list.OrderByDescending(r => r.ProgressPct ?? -1) : list.OrderBy(r => r.ProgressPct ?? 101),
            ["status", var dir] => dir == "desc" ? list.OrderByDescending(r => r.Status) : list.OrderBy(r => r.Status),
            ["pm", var dir] => dir == "desc" ? list.OrderByDescending(r => r.Pm.DisplayName) : list.OrderBy(r => r.Pm.DisplayName),
            ["client", var dir] => dir == "desc" ? list.OrderByDescending(r => r.Client.Name) : list.OrderBy(r => r.Client.Name),
            ["overdue", var dir] => dir == "desc" ? list.OrderByDescending(r => r.OverdueTasks) : list.OrderBy(r => r.OverdueTasks),
            ["activity", var dir] => dir == "desc" ? list.OrderByDescending(r => r.LastActivityAt) : list.OrderBy(r => r.LastActivityAt),
            ["submission", _] => list.OrderBy(r => Health.Severity(r.ReportedHealth)).ThenBy(r => r.NextSubmission?.Date ?? DateOnly.MaxValue),
            _ => list.OrderByDescending(r => r.Starred).ThenBy(r => Health.Severity(r.ReportedHealth)).ThenBy(r => r.NextMilestone?.Date ?? DateOnly.MaxValue).ThenBy(r => r.ProjectNumber),
        };
        return sorted.Cast<object>().ToList();
    }

    // ---------- Create (FR-001, FR-002, AC-PRJ-01, AC-PRJ-02, Workflow 1) ----------

    static async Task<IResult> Create(CreateBody body, Access access, HubDb db, SettingsStore store, TeamService team, CurrentUser me, TimeProvider clock, IServiceProvider sp)
    {
        Access.Demand(Permissions.CreateProject(access.Actor));
        var s = await store.Get(db);
        var number = Check.Required(body.ProjectNumber, "projectNumber", 32);
        Check.That(Regex.IsMatch(number, s.ProjectNumberFormat), "projectNumber", "project.number_format");
        if (await db.Projects.Where(p => p.ProjectNumber == number).Select(p => new { p.Id, p.ProjectNumber, p.Name }).FirstOrDefaultAsync() is { } dup)
            throw new ApiException(409, "duplicate_project_number", Text.Get("project.duplicate", dup.ProjectNumber, dup.Name),
                new Dictionary<string, string[]> { ["projectNumber"] = [Text.Get("project.duplicate", dup.ProjectNumber, dup.Name)] }, new { existingProjectId = dup.Id, existingProjectNumber = dup.ProjectNumber });
        Check.That(await db.Clients.AnyAsync(c => c.Id == body.ClientId && c.IsActive), "clientId", "error.required");
        Check.That(await db.Offices.AnyAsync(o => o.Id == body.OfficeId && o.IsActive), "officeId", "error.required");
        if (body.ProjectTypeId is { } pt) Check.That(await db.ProjectTypes.AnyAsync(x => x.Id == pt), "projectTypeId", "error.not_found");
        if (body.Priority is not null) Check.OneOf(body.Priority, Priority.All, "priority");
        if (body.CoordinationDay is not null) Check.OneOf(body.CoordinationDay, Weekday.All, "coordinationDay");
        var p = new Project
        {
            ProjectNumber = number, Name = Check.Required(body.Name, "name", 200), ClientId = body.ClientId, ClientReference = Check.Optional(body.ClientReference, "clientReference", 200),
            OfficeId = body.OfficeId, ProjectTypeId = body.ProjectTypeId, Description = Check.Optional(body.Description, "description", 8000),
            Location = Check.Optional(body.Location, "location", 300), StartDate = body.StartDate, TargetCompletionDate = body.TargetCompletionDate,
            Priority = body.Priority ?? Priority.Medium, CoordinationDay = body.CoordinationDay, ProjectManagerId = me.Id, Status = ProjectStatus.Setup,
            AllowViewerComments = s.ViewerCommentsDefault, StatusChangedAt = clock.GetUtcNow(),
        };
        await Tx.Run(db, async () =>
        {
            db.Projects.Add(p);
            await team.Add(p, me.Id, [ProjectRole.PM], null, notifyPerson: false);
            var pds = new Dictionary<Guid, ProjectDiscipline>();
            foreach (var (d, i) in (body.Disciplines ?? []).DistinctBy(x => x.DisciplineId).Select((d, i) => (d, i)))
            {
                var disc = await db.Disciplines.FirstOrDefaultAsync(x => x.Id == d.DisciplineId && x.IsActive) ?? throw ApiException.Invalid("disciplines", "error.not_found");
                var pd = new ProjectDiscipline { ProjectId = p.Id, DisciplineId = disc.Id, Discipline = disc, SortOrder = i };
                db.ProjectDisciplines.Add(pd);
                pds[disc.Id] = pd;
            }
            foreach (var m in (body.Members ?? []).Where(m => m.UserId != me.Id))
                await team.Add(p, m.UserId, m.Roles, m.DisciplineId is { } dd && pds.TryGetValue(dd, out var mpd) ? mpd.Id : null);
            foreach (var d in body.Disciplines ?? [])
                if (d.LeadUserId is { } lead && pds.TryGetValue(d.DisciplineId, out var pd)) await team.SetLead(p, pd, lead);
            await db.SaveChangesAsync();
            foreach (var hook in sp.GetServices<IProjectCreateHook>()) await hook.AfterCreate(p, body);
            return 0;
        });
        return Results.Created($"/api/v1/projects/{p.Id}", new { p.Id, p.ProjectNumber });
    }

    // ---------- Read (project header §12.1, Setup checklist P-09) ----------

    static async Task<object> Get(string idOrNumber, Access access, HubDb db, CurrentUser me, SettingsStore store, TimeProvider clock, EvaluationService eval)
    {
        var (p, ctx) = await access.ProjectByKeyOrId(idOrNumber, track: false);
        await eval.EnsureFresh(p.Id);
        var s = await store.Get(db);
        var a = access.Actor;
        var state = await db.ProjectStates.AsNoTracking().FirstOrDefaultAsync(x => x.ProjectId == p.Id);
        var now = clock.GetUtcNow();
        var computed = p.Status == ProjectStatus.Active ? state?.ComputedHealth ?? Health.Grey : Health.Grey;
        var overrideActive = p.HealthOverride is not null && p.HealthOverrideExpiresAt > now && p.Status == ProjectStatus.Active;
        var names = await db.Users.Where(u => u.Id == p.ProjectManagerId || u.Id == p.HealthOverrideBy || u.Id == p.LastCoordinationReviewedBy)
            .ToDictionaryAsync(u => u.Id, u => u.IsActive ? u.DisplayName : u.DisplayName + " (Inactive)");
        var disciplines = await db.ProjectDisciplines.AsNoTracking().Where(d => d.ProjectId == p.Id).OrderBy(d => d.SortOrder)
            .Select(d => new { d.Id, d.DisciplineId, d.Discipline!.Name, d.Discipline.Code, d.Discipline.Colour, d.LeadUserId, d.IsActive,
                LeadName = db.Users.Where(u => u.Id == d.LeadUserId).Select(u => u.DisplayName).FirstOrDefault() }).ToListAsync();
        var milestones = await db.Milestones.AsNoTracking().Where(m => m.ProjectId == p.Id && !m.IsCancelled)
            .Select(m => new { m.Id, m.Key, m.Name, m.Date, m.MilestoneType, m.IsComplete,
                Deliverables = db.Deliverables.Count(d => d.MilestoneId == m.Id && d.Status != DeliverableStatus.Cancelled),
                Status = db.MilestoneStates.Where(x => x.MilestoneId == m.Id).Select(x => x.Status).FirstOrDefault() }).ToListAsync();
        var next = milestones.Where(m => !m.IsComplete && m.Date != null).OrderBy(m => m.Date).FirstOrDefault();
        var nextSub = milestones.Where(m => !m.IsComplete && m.Date != null && MilestoneType.IsSubmission(m.MilestoneType)).OrderBy(m => m.Date).FirstOrDefault();
        var follow = await db.Follows.AsNoTracking().Where(f => f.ProjectId == p.Id && f.UserId == me.Id).Select(f => new { f.Level, f.Source }).FirstOrDefaultAsync();
        var myRoles = ctx.MemberRoles.ToList();
        if (ctx.LeadDisciplineIds.Count > 0) myRoles.Add("DisciplineLead");
        var today = clock.Today(s);
        var completeFor = p.CompletedAt is { } ca ? today.DayNumber - Clock.LocalDate(ca, s).DayNumber : 0;
        return new
        {
            p.Id, p.ProjectNumber, p.Name, p.ClientId, p.ClientReference, p.ProjectManagerId, PmName = names.GetValueOrDefault(p.ProjectManagerId),
            p.OfficeId, p.ProjectTypeId, p.Description, p.Location, p.Status, p.PhaseId, p.StartDate, p.TargetCompletionDate, p.Priority, p.Visibility,
            InternalNotes = ctx.IsMember || a.Admin || a.Executive || a.Supervisor ? p.InternalNotes : null,
            p.CoordinationDay, p.AllowViewerComments, p.LastCoordinationReviewedAt,
            LastCoordinationReviewedBy = p.LastCoordinationReviewedBy is { } rb ? names.GetValueOrDefault(rb) : null,
            p.StatusChangedAt, p.ActivatedAt, p.CompletedAt, p.ArchivedAt, p.CreatedFromTemplateId, p.TemplateVersion, p.RowVersion,
            TemplateName = p.CreatedFromTemplateId is { } tid ? await db.Templates.Where(x => x.Id == tid).Select(x => x.Name).FirstOrDefaultAsync() : null, // FR-008
            Client = await db.Clients.Where(c => c.Id == p.ClientId).Select(c => c.Name).FirstOrDefaultAsync(),
            Office = await db.Offices.Where(o => o.Id == p.OfficeId).Select(o => o.Name).FirstOrDefaultAsync(),
            Phase = await db.Phases.Where(x => x.Id == p.PhaseId).Select(x => x.Name).FirstOrDefaultAsync(),
            Links = await DocumentLinkEndpoints.Of(db, ItemType.Project, p.Id).Select(l => new { l.Id, l.Title, l.Url, l.LinkType }).ToListAsync(),
            Disciplines = disciplines,
            Health = new
            {
                Computed = computed, Reported = overrideActive ? p.HealthOverride : computed, OverrideActive = overrideActive,
                p.HealthOverride, p.HealthOverrideNote, OverrideBy = p.HealthOverrideBy is { } hb ? names.GetValueOrDefault(hb) : null,
                p.HealthOverrideAt, p.HealthOverrideExpiresAt,
                Reasons = J.El(state?.HealthReasons),
                Inputs = J.El(state?.Inputs),
                EvaluatedAt = state?.EvaluatedAt,
            },
            ProgressPct = state?.ProgressPct,
            NextMilestone = next, NextSubmission = nextSub,
            MyRoles = myRoles, Follow = follow,
            Starred = await db.ProjectStars.AnyAsync(x => x.ProjectId == p.Id && x.UserId == me.Id),
            SetupChecklist = p.Status == ProjectStatus.Setup ? new
            {
                HasMilestone = milestones.Count > 0,
                EveryDisciplineHasLead = disciplines.Where(d => d.IsActive).All(d => d.LeadUserId != null),
                EverySubmissionHasDeliverable = milestones.Where(m => MilestoneType.IsSubmission(m.MilestoneType)).All(m => m.Deliverables > 0),
            } : null,
            SuggestArchive = p.Status == ProjectStatus.Complete && completeFor > s.CompleteProjectEditWindowDays,
            EditWindowDaysLeft = p.Status == ProjectStatus.Complete ? Math.Max(0, s.CompleteProjectEditWindowDays - completeFor) : (int?)null,
            Permissions = new
            {
                Edit = Perm(Permissions.EditProject(a, ctx), p.Status),
                ManageTeam = Perm(Permissions.ManageTeam(a, ctx), p.Status),
                ManageMilestones = Perm(Permissions.ManageMilestones(a, ctx), p.Status),
                HealthOverride = Perm(Permissions.HealthOverride(a, ctx), p.Status),
                Comment = Perm(Permissions.Comment(a, ctx), p.Status),
                RaiseRegister = Perm(Permissions.RaiseRegisterItem(a, ctx), p.Status),
                RunCoordination = Perm(Permissions.RunCoordination(a, ctx), p.Status),
                CreateEvent = Perm(Permissions.CreateProjectEvent(a, ctx), p.Status),
                EnterTime = Perm(Permissions.EnterTime(a, ctx), p.Status),
                CreateTaskIn = disciplines.Where(d => d.IsActive && Permissions.CreateTask(a, ctx, d.Id)).Select(d => d.Id),
                CreateDeliverableIn = disciplines.Where(d => d.IsActive && Permissions.CreateDeliverable(a, ctx, d.Id)).Select(d => d.Id),
                LeadOf = ctx.LeadDisciplineIds,
                IsPm = Permissions.IsPM(a, ctx),
                Transitions = Workflow.ProjectNext(p.Status).Where(to => Permissions.ChangeProjectStatus(a, ctx, to).Ok),
                ChangeNumber = a.Admin,
                SetVisibility = s.RestrictedProjectsEnabled && Permissions.EditProject(a, ctx).Ok,
                NeedsReason = p.Status == ProjectStatus.Complete,
            },
        };
    }

    static object Perm(Allow a, string status) => new { a.Ok, Reason = a.Why is null ? null : Text.Get(a.Why, a.Arg ?? status) };

    /// P-05 / AC-PRJ-06: edits to a Complete project are corrections that need a reason.
    public static void CorrectionReason(Project p, string? reason) { if (p.Status == ProjectStatus.Complete) Check.Reason(reason); }

    // ---------- Edit (FR-003, P-07, FR-016) ----------

    static async Task<IResult> Edit(Guid id, JsonElement body, HttpContext http, Access access, HubDb db, TeamService team, SettingsStore store)
    {
        var (p, ctx) = await access.Project(id);
        var patch = new Patch(body);
        Access.Demand(Permissions.EditProject(access.Actor, ctx));
        await Http.CheckVersion(db, http, p, patch.RowVersion);
        var reason = patch.Str("reason");
        CorrectionReason(p, reason);
        db.Audit.Reason = reason;
        var s = await store.Get(db);
        if (patch.Has("projectNumber"))
        {
            Access.Demand(Permissions.Administer(access.Actor)); // P-07
            var number = Check.Required(patch.Str("projectNumber"), "projectNumber", 32);
            Check.That(Regex.IsMatch(number, s.ProjectNumberFormat), "projectNumber", "project.number_format");
            if (number != p.ProjectNumber && await db.Projects.AnyAsync(x => x.ProjectNumber == number && x.Id != p.Id)) throw ApiException.Conflict("duplicate_project_number", "project.duplicate_simple");
            p.ProjectNumber = number;
        }
        if (patch.Has("name")) p.Name = Check.Required(patch.Str("name"), "name", 200);
        if (patch.Has("clientId")) { var c = patch.Id("clientId") ?? throw ApiException.Invalid("clientId", "error.required"); Check.That(await db.Clients.AnyAsync(x => x.Id == c), "clientId", "error.not_found"); p.ClientId = c; }
        if (patch.Has("clientReference")) p.ClientReference = Check.Optional(patch.Str("clientReference"), "clientReference", 200);
        if (patch.Has("officeId")) p.OfficeId = patch.Id("officeId") ?? throw ApiException.Invalid("officeId", "error.required");
        if (patch.Has("projectTypeId")) p.ProjectTypeId = patch.Id("projectTypeId");
        if (patch.Has("description")) p.Description = Check.Optional(patch.Str("description"), "description", 8000);
        if (patch.Has("location")) p.Location = Check.Optional(patch.Str("location"), "location", 300);
        if (patch.Has("phaseId")) p.PhaseId = patch.Id("phaseId");
        if (patch.Has("startDate")) p.StartDate = patch.Date("startDate");
        if (patch.Has("targetCompletionDate")) p.TargetCompletionDate = patch.Date("targetCompletionDate");
        if (patch.Has("priority")) { var pr = patch.Str("priority"); Check.OneOf(pr, Priority.All, "priority"); p.Priority = pr!; }
        if (patch.Has("internalNotes")) p.InternalNotes = Check.Optional(patch.Str("internalNotes"), "internalNotes", 8000);
        if (patch.Has("coordinationDay")) { var d = patch.Str("coordinationDay"); if (d is not null) Check.OneOf(d, Weekday.All, "coordinationDay"); p.CoordinationDay = d; }
        if (patch.Has("allowViewerComments")) p.AllowViewerComments = patch.Bool("allowViewerComments") ?? true;
        if (patch.Has("visibility"))
        {
            Check.That(s.RestrictedProjectsEnabled, "visibility", "project.restricted_disabled");
            var v = patch.Str("visibility");
            Check.OneOf(v, [Visibility.Open, Visibility.Restricted], "visibility");
            p.Visibility = v!;
            if (v == Visibility.Restricted)
            {
                // E-28: followers who can no longer view the project lose the follow.
                var members = await db.ProjectMembers.Where(m => m.ProjectId == p.Id && m.RemovedAt == null).Select(m => m.UserId).ToListAsync();
                var privileged = await db.UserRoles.Where(r => r.Role == SystemRole.Admin || r.Role == SystemRole.Executive).Select(r => r.UserId).ToListAsync();
                db.Follows.RemoveRange(await db.Follows.Where(f => f.ProjectId == p.Id && !members.Contains(f.UserId) && !privileged.Contains(f.UserId)).ToListAsync());
            }
        }
        if (patch.Has("projectManagerId"))
        {
            var pm = patch.Id("projectManagerId") ?? throw ApiException.Invalid("projectManagerId", "error.required");
            Check.That(await db.Users.AnyAsync(u => u.Id == pm && u.IsActive), "projectManagerId", "team.inactive_user");
            await team.ChangePrimaryPm(p, pm);
        }
        await db.SaveChangesAsync();
        Http.ETag(http, p);
        return Results.Ok(new { p.Id, p.RowVersion });
    }

    // ---------- Lifecycle (P-02..P-06, AC-PRJ-03..06, ASG-07, AC-NOT-07) ----------

    static async Task<object> CloseoutPreview(Guid id, Access access, HubDb db)
    {
        var (p, _) = await access.Project(id, track: false);
        var tasks = await db.Tasks.AsNoTracking().Where(t => t.ProjectId == id && t.Status != TaskStatuses.Complete && t.Status != TaskStatuses.Cancelled)
            .OrderBy(t => t.Seq).Select(t => new { t.Id, t.Key, t.Name, t.Status }).ToListAsync();
        var deliverables = await db.Deliverables.AsNoTracking().Where(d => d.ProjectId == id && d.Status != DeliverableStatus.Issued && d.Status != DeliverableStatus.Accepted && d.Status != DeliverableStatus.Cancelled)
            .OrderBy(d => d.Seq).Select(d => new { d.Id, d.Key, d.Name, d.Status }).ToListAsync();
        var decisions = await db.Decisions.AsNoTracking().Where(d => d.ProjectId == id && (d.Status == DecisionStatus.Pending || d.Status == DecisionStatus.UnderReview || d.Status == DecisionStatus.Deferred))
            .OrderBy(d => d.Seq).Select(d => new { d.Id, d.Key, Name = d.Subject, d.Status }).ToListAsync();
        var registers = await db.Risks.CountAsync(r => r.ProjectId == id && (r.Status == RiskStatus.Open || r.Status == RiskStatus.Monitoring))
            + await db.Issues.CountAsync(i => i.ProjectId == id && (i.Status == IssueStatus.Open || i.Status == IssueStatus.InProgress))
            + await db.Actions.CountAsync(a => a.ProjectId == id && (a.Status == ActionStatus.Open || a.Status == ActionStatus.InProgress));
        return new { p.Status, openTasks = tasks.Count, tasks, deliverables, decisions, openRegisterItems = registers };
    }

    /// E-05 (Rec): once an On Hold project is Active again, its open tasks and deliverables whose due dates fell while it was
    /// on hold, so the PM can shift them together with the bulk actions. Read from the logged status changes; nothing is
    /// listed once they have moved, or 30 days after the resume.
    static async Task<object> DateReview(Guid id, Access access, HubDb db, SettingsStore store, TimeProvider clock)
    {
        var (p, _) = await access.Project(id, track: false);
        var s = await store.Get(db);
        var today = clock.Today(s);
        const string resumed = """[{"field": "Status", "old": "On Hold", "new": "Active"}]""", held = """[{"field": "Status", "new": "On Hold"}]""";
        var log = db.ActivityLog.AsNoTracking().Where(a => a.ProjectId == id && a.ItemType == ItemType.Project && a.ItemId == id && a.Action == "StatusChanged");
        var back = p.Status == ProjectStatus.Active
            ? await log.Where(a => EF.Functions.JsonContains(a.Changes, resumed)).OrderByDescending(a => a.OccurredAt).Select(a => (DateTimeOffset?)a.OccurredAt).FirstOrDefaultAsync() : null;
        var from = back is { } b ? await log.Where(a => a.OccurredAt < b && EF.Functions.JsonContains(a.Changes, held)).OrderByDescending(a => a.OccurredAt)
            .Select(a => (DateTimeOffset?)a.OccurredAt).FirstOrDefaultAsync() : null;
        if (back is not { } resumedAt || from is not { } heldAt || Clock.LocalDate(resumedAt, s) < today.AddDays(-30))
            return new { Window = (object?)null, Tasks = new List<object>(), Deliverables = new List<object>() };
        var (start, end) = (Clock.LocalDate(heldAt, s), Clock.LocalDate(resumedAt, s));
        var tasks = await TaskQueries.Rows(db, db.Tasks.AsNoTracking().Where(t => t.ProjectId == id && t.Status != TaskStatuses.Complete && t.Status != TaskStatuses.Cancelled
            && t.DueDate >= start && t.DueDate <= end));
        var deliverables = await DeliverableEndpoints.Rows(db, db.Deliverables.AsNoTracking().Where(d => d.ProjectId == id && d.Status != DeliverableStatus.Issued
            && d.Status != DeliverableStatus.Accepted && d.Status != DeliverableStatus.Cancelled && d.DueDate >= start && d.DueDate <= end));
        return new { Window = new { HeldFrom = start, ResumedOn = end, Days = end.DayNumber - start.DayNumber }, Tasks = tasks, Deliverables = deliverables };
    }

    static async Task<IResult> Transition(Guid id, TransitionBody body, HttpContext http, Access access, HubDb db, Notifier notify, TimeProvider clock, CurrentUser me)
    {
        var (p, ctx) = await access.Project(id);
        await Http.CheckVersion(db, http, p, body.RowVersion);
        var from = p.Status;
        var to = body.ToStatus;
        Check.OneOf(to, ProjectStatus.All, "toStatus");
        if (!Workflow.ProjectAllowed(from, to)) throw ApiException.Rule("illegal_transition", "project.illegal_transition", null, from, to);
        Access.Demand(Permissions.ChangeProjectStatus(access.Actor, ctx, to));
        var reason = Workflow.ProjectNeedsReason(from, to) ? Check.Reason(body.Reason) : body.Reason?.Trim();
        var now = clock.GetUtcNow();
        db.Audit.Reason = reason;
        await Tx.Run(db, async () =>
        {
            if (to == ProjectStatus.Complete)
            {
                var c = body.Closeout;
                if (c?.Tasks == "cancel")
                    foreach (var t in await db.Tasks.Where(t => t.ProjectId == id && t.Status != TaskStatuses.Complete && t.Status != TaskStatuses.Cancelled).ToListAsync())
                    { t.PreviousStatus = t.Status; t.Status = TaskStatuses.Cancelled; t.CancelledReason = reason; t.StatusChangedAt = now; }
                if (c?.Deliverables == "cancel")
                    foreach (var d in await db.Deliverables.Where(d => d.ProjectId == id && d.Status != DeliverableStatus.Issued && d.Status != DeliverableStatus.Accepted && d.Status != DeliverableStatus.Cancelled).ToListAsync())
                    { d.PreviousStatus = d.Status; d.Status = DeliverableStatus.Cancelled; d.CancelledReason = reason; d.StatusChangedAt = now; }
                if (c?.Decisions == "cancel")
                    foreach (var d in await db.Decisions.Where(d => d.ProjectId == id && (d.Status == DecisionStatus.Pending || d.Status == DecisionStatus.UnderReview || d.Status == DecisionStatus.Deferred)).ToListAsync())
                    { d.Status = DecisionStatus.Cancelled; d.CancelledReason = reason; d.StatusChangedAt = now; }
                if (c?.Registers == "cancel")
                {
                    foreach (var r in await db.Risks.Where(r => r.ProjectId == id && (r.Status == RiskStatus.Open || r.Status == RiskStatus.Monitoring)).ToListAsync()) { r.Status = RiskStatus.Closed; r.StatusChangedAt = now; }
                    foreach (var i in await db.Issues.Where(i => i.ProjectId == id && (i.Status == IssueStatus.Open || i.Status == IssueStatus.InProgress)).ToListAsync()) { i.Status = IssueStatus.Cancelled; i.StatusChangedAt = now; }
                    foreach (var a in await db.Actions.Where(a => a.ProjectId == id && (a.Status == ActionStatus.Open || a.Status == ActionStatus.InProgress)).ToListAsync()) { a.Status = ActionStatus.Cancelled; a.StatusChangedAt = now; }
                }
                db.LogEvent(ItemType.Project, p.Id, "Closeout", "status", p.Id, p.ProjectNumber, p.Name, new
                {
                    tasks = c?.Tasks ?? "leave", deliverables = c?.Deliverables ?? "leave", decisions = c?.Decisions ?? "leave", registers = c?.Registers ?? "leave",
                }, reason);
                p.CompletedAt = now;
            }
            if (to == ProjectStatus.Archived) p.ArchivedAt = now;
            if (from == ProjectStatus.Archived) p.ArchivedAt = null;
            if (from == ProjectStatus.Complete && to == ProjectStatus.Active) p.CompletedAt = null;
            if (from == ProjectStatus.Setup && to == ProjectStatus.Active)
            {
                p.ActivatedAt = now;
                // ASG-07: set-up work never arrives in followers' feeds as a flood.
                foreach (var f in await db.Follows.Where(f => f.ProjectId == id).ToListAsync()) f.LastSeenAt = now;
            }
            p.Status = to;
            p.StatusChangedAt = now;
            await db.SaveChangesAsync();

            var members = await db.ProjectMembers.Where(m => m.ProjectId == id && m.RemovedAt == null).Select(m => (Guid?)m.UserId).ToListAsync();
            await notify.Send(NotificationEvents.ProjectStatusChanged, members, TeamService.Item(p),
                Text.Get("notify.project_status", await notify.ActorName(), p.ProjectNumber, p.Name, Text.Get($"value.{to}")), reason);
            if (from == ProjectStatus.Setup && to == ProjectStatus.Active)
            {
                // AC-NOT-07: one batched assignment notice per person for work assigned during set-up.
                var counts = await db.Tasks.Where(t => t.ProjectId == id && t.AssigneeId != null && t.Status != TaskStatuses.Complete && t.Status != TaskStatuses.Cancelled)
                    .GroupBy(t => t.AssigneeId).Select(g => new { UserId = g.Key, N = g.Count() }).ToListAsync();
                foreach (var c in counts)
                    await notify.Send(NotificationEvents.TaskAssigned, c.UserId, TeamService.Item(p) with { Link = $"/my-work?project={p.Id}" },
                        Text.Get("notify.batched_assignments", c.N, p.ProjectNumber, p.Name));
            }
            await db.SaveChangesAsync();
            return 0;
        });
        return Results.Ok(new { p.Id, p.Status, p.RowVersion });
    }
}
