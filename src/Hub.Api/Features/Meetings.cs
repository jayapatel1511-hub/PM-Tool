using System.Text.Json;
using System.Text.Json.Serialization;
using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Features;

/// Meeting Action Register (§12.11, §13.13, MTG-01..04, packet 015): meetings and the actions agreed in them, each owned by
/// a person, a discipline (routed to its lead, or flagged to the PM when it has none) or an external party (nothing is sent
/// outside the organisation); an action can become a task and then follows the task's completion. Agendas, minutes,
/// attendance and recurring series are not built (§12.11).
public static partial class MeetingEndpoints
{
    public sealed record MeetingBody(string Title, DateOnly? MeetingDate, string? MeetingType, string? NotesLink, Guid? CalendarEventId);
    public sealed record ActionBody(string Text, string? OwnerType, Guid? OwnerUserId, Guid? OwnerDisciplineId, Guid? OwnerExternalPartyId, DateOnly? DueDate,
        Guid? RelatedTaskId, Guid? RelatedDecisionId, DecisionEndpoints.LinkInput[]? Links);
    public sealed record ActionMove(string ToStatus, string? Reason, int? RowVersion);
    public sealed record ConvertBody(Guid? ProjectDisciplineId, Guid? AssigneeId, Guid? DeliverableId, DateOnly? DueDate, int? RowVersion);
    public sealed record ReuseBody(Guid RequestId, int? RowVersion, DecisionEndpoints.LinkInput[]? Links, string? Reason);
    public sealed record ActionQuery(string? Status, string? OwnerType, Guid? OwnerId, Guid? MeetingId, string? Indicator, string? Q);
    /// One action as the register, My Work and the coordination view read it: the stored action with the meeting, owner, lead and linked item names worked out beside it.
    public sealed partial record MeetingActionRow(Guid Id, Guid ProjectId, string? ProjectNumber, string Key, Guid MeetingId, string? MeetingTitle, DateOnly MeetingDate, string Text, string OwnerType,
        string OwnerTypeLabel, Guid? OwnerUserId, Guid? OwnerDisciplineId, Guid? OwnerExternalPartyId, string? OwnerName, Guid? LeadId, string? LeadName, bool NoLead, bool PartyIsClient,
        DateOnly? DueDate, bool IsOverdue, int DaysOverdue, string Status, Guid? RelatedTaskId, string? TaskKey, Guid? RelatedDecisionId, string? DecisionKey, bool Converted, int RowVersion);

    /// The stored meeting, as the create, edit and current routes answer it.
    public sealed record MeetingRead(Guid Id, Guid ProjectId, string Title, DateOnly MeetingDate, string MeetingType, string? NotesLink, Guid? CalendarEventId, int RowVersion)
    {
        public static MeetingRead From(Meeting m) => new(m.Id, m.ProjectId, m.Title, m.MeetingDate, m.MeetingType, m.NotesLink, m.CalendarEventId, m.RowVersion);
    }
    /// One meeting in the register: the stored meeting with its linked calendar event's title and how many of its actions are open and in all.
    public sealed record MeetingListRow(Guid Id, Guid ProjectId, string Title, DateOnly MeetingDate, string MeetingType, string? NotesLink, Guid? CalendarEventId, int RowVersion,
        string? CalendarEventTitle, int Open, int Total);
    public sealed record ActionCreated(Guid Id, string Key, int RowVersion, IReadOnlyList<string> Warnings);
    public sealed record ActionMutation(Guid Id, int RowVersion);
    public sealed record ActionMoved(Guid Id, string Status, int RowVersion);
    public sealed record ActionConverted(Guid Id, string Status, int RowVersion, Guid TaskId, string TaskKey);
    public sealed record ActionMeetingRef(Guid Id, string Title, DateOnly MeetingDate, string MeetingType, string? NotesLink);
    public sealed record ActionProject(Guid Id, string ProjectNumber, string Name, string Status);
    public sealed record ActionTaskRef(Guid Id, string Key, string Name, string Status);
    public sealed record ActionDecisionRef(Guid Id, string Key, string Subject, string Status);
    public sealed record ActionDetailPermissions(RegisterEndpoints.RegisterPermission Edit, IReadOnlyList<RegisterEndpoints.RegisterTransition> Transitions, bool Convert, bool Comment);
    /// One action with its meeting, project, related task and decision, links and what the caller may do with it.
    public sealed partial record ActionDetail(MeetingActionRow Action, ActionMeetingRef Meeting, ActionProject Project, ActionTaskRef? Task, ActionDecisionRef? Decision,
        IReadOnlyList<DecisionEndpoints.ItemLinkRow> Links, Guid? TaskDisciplineId, ActionDetailPermissions Permissions,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] RecordContextView? Context = null);
    /// What the register's query reads for each action, before the names are put beside it.
    sealed record ActionJoin(Guid Id, Guid ProjectId, string Key, int Seq, Guid MeetingId, string Text, string OwnerType, Guid? OwnerUserId, Guid? OwnerDisciplineId, Guid? OwnerExternalPartyId,
        DateOnly? DueDate, string Status, Guid? RelatedTaskId, Guid? RelatedDecisionId, int RowVersion, string? ProjectNumber, string? MeetingTitle, DateOnly MeetingDate,
        string? UserName, string? DisciplineName, Guid? LeadId, string? PartyName, bool PartyIsClient, string? TaskKey, string? DecisionKey, bool Converted);
    sealed record DisciplineLead(Guid? LeadUserId, string Name);

    static readonly string[] OwnerTypes = [ActionOwnerType.User, ActionOwnerType.Discipline, ActionOwnerType.ExternalParty];
    static readonly Col[] ActionCols = Hub.Api.Records.MeetingActionRecord.MeetingsActionCols;

    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/projects/{id:guid}/meetings", Meetings);
        api.MapPost("/projects/{id:guid}/meetings", CreateMeeting);
        api.MapPost("/projects/{id:guid}/meetings/current", Current);
        api.MapPatch("/meetings/{id:guid}", EditMeeting);
        api.MapGet("/projects/{id:guid}/actions", List);
        api.MapPost("/meetings/{id:guid}/actions", CreateAction);
        api.MapGet("/actions/{id}", GetAction);
        api.MapPatch("/actions/{id:guid}", EditAction);
        api.MapPost("/actions/{id:guid}/transition", MoveAction);
        api.MapPost("/actions/{id:guid}/convert", Convert);
        api.MapPost("/projects/{projectId:guid}/actions/{id:guid}/reuse", Reuse).WithMetadata(new Coordination.AtomicCommand());
        api.MapGet("/projects/{id:guid}/actions/export", async (Guid id, string? format, [AsParameters] ActionQuery f, HttpContext http, Access access, HubDb db, SettingsStore store, TimeProvider clock) =>
        {
            var (p, _) = await access.Project(id, track: false);
            var rows = JsonSerializer.SerializeToNode(await List(id, f, access, db, store, clock), JsonOpts.Web)!.AsArray();
            return await ExportFile.Send(db, store, format, Text.Get("export.actions", p.ProjectNumber), ActionCols, rows, await ListExportEndpoints.Filters(db, http, access), p.Id, $"{p.ProjectNumber}-actions", clock);
        });
    }

    public static NotifyItem Item(Project p, MeetingAction a) => new(p.Id, ItemType.Action, a.Id, a.Key, $"/projects/{p.ProjectNumber}/meetings?panel=Action:{a.Id}", p.ProjectNumber);

    /// Owners, whoever raised it, the lead of the owning discipline and the PM edit an action (§8.5.2).
    internal static OwnedFacts Facts(MeetingAction a) => new(a.OwnerUserId, a.CreatedBy, a.OwnerDisciplineId);

    static async Task<(MeetingAction A, Project P, ProjectContext Ctx)> Load(HubDb db, Access access, Guid id)
    {
        var a = await db.Actions.FirstOrDefaultAsync(x => x.Id == id) ?? throw ApiException.NotFound();
        var (p, ctx) = await access.Project(a.ProjectId);
        return (a, p, ctx);
    }

    /// The discipline a converted task belongs to: the owning discipline, else the owner's primary or led discipline on the
    /// project, else the related task's.
    static async Task<Guid?> TaskDiscipline(HubDb db, MeetingAction a) =>
        a.OwnerDisciplineId
        ?? (a.OwnerUserId is { } u
            ? await db.ProjectMembers.Where(m => m.ProjectId == a.ProjectId && m.UserId == u && m.RemovedAt == null).Select(m => m.PrimaryDisciplineId).FirstOrDefaultAsync()
              ?? await db.ProjectDisciplines.Where(d => d.ProjectId == a.ProjectId && d.LeadUserId == u && d.IsActive).Select(d => (Guid?)d.Id).FirstOrDefaultAsync()
            : null)
        ?? (a.RelatedTaskId is { } t ? await db.Tasks.Where(x => x.Id == t && x.ProjectId == a.ProjectId).Select(x => (Guid?)x.ProjectDisciplineId).FirstOrDefaultAsync() : null);

    // ---------- Meetings (FR-001) ----------

    static async Task<Ok<List<MeetingListRow>>> Meetings(Guid id, Access access, HubDb db)
    {
        await access.Project(id, track: false);
        return TypedResults.Ok(await db.Meetings.AsNoTracking().Where(m => m.ProjectId == id).OrderByDescending(m => m.MeetingDate).ThenByDescending(m => m.CreatedAt).Select(m => new MeetingListRow(
            m.Id, m.ProjectId, m.Title, m.MeetingDate, m.MeetingType, m.NotesLink, m.CalendarEventId, m.RowVersion,
            db.CalendarEvents.Where(e => e.Id == m.CalendarEventId).Select(e => e.Title).FirstOrDefault(),
            db.Actions.Count(a => a.MeetingId == m.Id && (a.Status == ActionStatus.Open || a.Status == ActionStatus.InProgress)),
            db.Actions.Count(a => a.MeetingId == m.Id))).ToListAsync());
    }

    static async Task<Meeting> Fill(HubDb db, Guid projectId, Meeting m, MeetingBody b, DateOnly today)
    {
        m.Title = Check.Required(b.Title, "title", 200);
        m.MeetingDate = b.MeetingDate ?? today;
        var type = b.MeetingType ?? "Coordination";
        Check.OneOf(type, MeetingType.All, "meetingType");
        m.MeetingType = type;
        m.NotesLink = Check.Optional(b.NotesLink, "notesLink", 2000);
        if (m.NotesLink is { } link) Check.That(Uri.TryCreate(link, UriKind.Absolute, out var u) && u.Scheme is "https" or "http", "notesLink", "error.url");
        if (b.CalendarEventId is { } ev) Check.That(await db.CalendarEvents.AnyAsync(e => e.Id == ev && e.ProjectId == projectId), "calendarEventId", "error.not_found"); // §36.5
        m.CalendarEventId = b.CalendarEventId;
        return m;
    }

    static async Task<Created<MeetingRead>> CreateMeeting(Guid id, MeetingBody body, Access access, HubDb db, SettingsStore store, TimeProvider clock)
    {
        var (_, ctx) = await access.Project(id);
        Access.Demand(Permissions.RunCoordination(access.Actor, ctx)); // FR-001: the PM and leads record meetings
        var m = await Fill(db, id, new Meeting { ProjectId = id }, body, clock.Today(await store.Get(db)));
        db.Meetings.Add(m);
        await db.SaveChangesAsync();
        return TypedResults.Created($"/api/v1/meetings/{m.Id}", MeetingRead.From(m));
    }

    static async Task<Ok<MeetingRead>> EditMeeting(Guid id, MeetingBody body, HttpContext http, Access access, HubDb db, SettingsStore store, TimeProvider clock)
    {
        var m = await db.Meetings.FirstOrDefaultAsync(x => x.Id == id) ?? throw ApiException.NotFound();
        var (_, ctx) = await access.Project(m.ProjectId);
        Access.Demand(Permissions.RunCoordination(access.Actor, ctx));
        await Http.CheckVersion(db, http, m, null);
        await Fill(db, m.ProjectId, m, body, clock.Today(await store.Get(db)));
        await db.SaveChangesAsync();
        return TypedResults.Ok(MeetingRead.From(m));
    }

    /// MTG-04: meeting mode captures actions against today's coordination meeting, recorded the first time it is needed.
    static async Task<Ok<MeetingRead>> Current(Guid id, Access access, HubDb db, SettingsStore store, TimeProvider clock)
    {
        var (_, ctx) = await access.Project(id);
        Access.Demand(Permissions.RunCoordination(access.Actor, ctx));
        var today = clock.Today(await store.Get(db));
        // ponytail: two people starting at the same moment could each record one; the second is harmless and can be edited
        var m = await db.Meetings.OrderBy(x => x.CreatedAt).FirstOrDefaultAsync(x => x.ProjectId == id && x.MeetingType == "Coordination" && x.MeetingDate == today);
        if (m is null)
        {
            m = new Meeting { ProjectId = id, Title = Text.Get("meeting.coordination_title", today.ToString("yyyy-MM-dd")), MeetingDate = today, MeetingType = "Coordination" };
            db.Meetings.Add(m);
            await db.SaveChangesAsync();
        }
        return TypedResults.Ok(MeetingRead.From(m));
    }

    // ---------- Actions (FR-002, FR-007) ----------

    public static async Task<List<MeetingActionRow>> List(Guid id, [AsParameters] ActionQuery f, Access access, HubDb db, SettingsStore store, TimeProvider clock)
    {
        await access.Project(id, track: false);
        var today = clock.Today(await store.Get(db));
        var q = db.Actions.AsNoTracking().Where(a => a.ProjectId == id);
        var st = Http.List(f.Status); if (st.Length > 0) q = q.Where(a => st.Contains(a.Status));
        var types = Http.List(f.OwnerType); if (types.Length > 0) q = q.Where(a => types.Contains(a.OwnerType));
        if (f.OwnerId is { } o) q = q.Where(a => a.OwnerUserId == o || a.OwnerDisciplineId == o || a.OwnerExternalPartyId == o);
        if (f.MeetingId is { } m) q = q.Where(a => a.MeetingId == m);
        if (!string.IsNullOrWhiteSpace(f.Q)) { var term = $"%{f.Q.Trim()}%"; q = q.Where(a => EF.Functions.ILike(a.Text, term) || EF.Functions.ILike(a.Key, term)); }
        foreach (var ind in Http.List(f.Indicator))
            q = ind switch
            {
                "open" => q.Where(a => a.Status == ActionStatus.Open || a.Status == ActionStatus.InProgress),
                "overdue" => q.Where(a => (a.Status == ActionStatus.Open || a.Status == ActionStatus.InProgress) && a.DueDate < today),
                _ => q,
            };
        return await Rows(db, q, today);
    }

    /// Due date ascending (undated last), then key (§13.13); the screen groups them by meeting.
    public static async Task<List<MeetingActionRow>> Rows(HubDb db, IQueryable<MeetingAction> q, DateOnly today)
    {
        var rows = await q.Select(a => new ActionJoin(
            a.Id, a.ProjectId, a.Key, a.Seq, a.MeetingId, a.Text, a.OwnerType, a.OwnerUserId, a.OwnerDisciplineId, a.OwnerExternalPartyId, a.DueDate, a.Status,
            a.RelatedTaskId, a.RelatedDecisionId, a.RowVersion,
            db.Projects.Where(p => p.Id == a.ProjectId).Select(p => p.ProjectNumber).FirstOrDefault(),
            db.Meetings.Where(m => m.Id == a.MeetingId && m.ProjectId == a.ProjectId).Select(m => m.Title).FirstOrDefault(),
            db.Meetings.Where(m => m.Id == a.MeetingId && m.ProjectId == a.ProjectId).Select(m => m.MeetingDate).FirstOrDefault(),
            db.Users.Where(u => u.Id == a.OwnerUserId).Select(u => u.IsActive ? u.DisplayName : u.DisplayName + " (Inactive)").FirstOrDefault(),
            db.ProjectDisciplines.Where(d => d.Id == a.OwnerDisciplineId).Select(d => d.Discipline!.Name).FirstOrDefault(),
            db.ProjectDisciplines.Where(d => d.Id == a.OwnerDisciplineId).Select(d => d.LeadUserId).FirstOrDefault(),
            db.ExternalParties.Where(e => e.Id == a.OwnerExternalPartyId).Select(e => e.Name).FirstOrDefault(),
            db.ExternalParties.Where(e => e.Id == a.OwnerExternalPartyId).Select(e => e.IsClient).FirstOrDefault(),
            db.Tasks.Where(t => t.Id == a.RelatedTaskId && t.ProjectId == a.ProjectId).Select(t => t.Key).FirstOrDefault(),
            db.Decisions.Where(d => d.Id == a.RelatedDecisionId && d.ProjectId == a.ProjectId).Select(d => d.Key).FirstOrDefault(),
            db.ItemLinks.Any(l => l.ProjectId == a.ProjectId && l.SourceType == ItemType.Action && l.SourceId == a.Id && l.TargetType == ItemType.Task && l.Relation == ItemRelation.ConvertedToTask))).ToListAsync();
        var leads = rows.Select(r => r.LeadId).OfType<Guid>().Distinct().ToList();
        var leadNames = await db.Users.AsNoTracking().Where(u => leads.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.DisplayName);
        return [.. rows.OrderBy(r => r.DueDate ?? DateOnly.MaxValue).ThenBy(r => r.Seq).Select(r =>
        {
            var late = Registers.ActionOverdueDays(r.Status, r.DueDate, today);
            return new MeetingActionRow(r.Id, r.ProjectId, r.ProjectNumber, r.Key, r.MeetingId, r.MeetingTitle, r.MeetingDate, r.Text, r.OwnerType, Text.Get($"opt.{r.OwnerType}"),
                r.OwnerUserId, r.OwnerDisciplineId, r.OwnerExternalPartyId, r.UserName ?? r.DisciplineName ?? r.PartyName,
                r.LeadId, r.LeadId is { } l ? leadNames.GetValueOrDefault(l) : null, r.OwnerType == ActionOwnerType.Discipline && r.LeadId is null,
                r.PartyIsClient, r.DueDate, late > 0, late, r.Status, r.RelatedTaskId, r.TaskKey, r.RelatedDecisionId, r.DecisionKey,
                r.Converted, r.RowVersion);
        })];
    }

    /// FR-002: exactly one owner of the stated type — an active person, one of the project's active disciplines, or one of
    /// its active external parties.
    static async Task Owner(HubDb db, Guid projectId, string? type, Guid? user, Guid? discipline, Guid? party)
    {
        Check.OneOf(type, OwnerTypes, "ownerType");
        switch (type)
        {
            case ActionOwnerType.User:
                Check.That(user is not null && discipline is null && party is null, "ownerUserId", "action.one_owner");
                await DeliverableEndpoints.ActivePerson(db, user!.Value, "ownerUserId");
                break;
            case ActionOwnerType.Discipline:
                Check.That(discipline is not null && user is null && party is null, "ownerDisciplineId", "action.one_owner");
                Check.That(await db.ProjectDisciplines.AnyAsync(d => d.Id == discipline && d.ProjectId == projectId && d.IsActive), "ownerDisciplineId", "error.not_found");
                break;
            default:
                Check.That(party is not null && user is null && discipline is null, "ownerExternalPartyId", "action.one_owner");
                Check.That(await db.ExternalParties.AnyAsync(e => e.Id == party && e.ProjectId == projectId && e.IsActive), "ownerExternalPartyId", "error.not_found");
                break;
        }
    }

    static async Task Related(HubDb db, Guid projectId, Guid? task, Guid? decision)
    {
        if (task is { } t) Check.That(await db.Tasks.AnyAsync(x => x.Id == t && x.ProjectId == projectId), "relatedTaskId", "decision.other_project");
        if (decision is { } d) Check.That(await db.Decisions.AnyAsync(x => x.Id == d && x.ProjectId == projectId), "relatedDecisionId", "decision.other_project");
    }

    /// MTG-01, MTG-02: a person hears about their action, a discipline's lead about the discipline's (the PM when it has no
    /// lead), and nothing is sent for an external party's.
    static async Task Assigned(HubDb db, Notifier notify, TeamService team, Project p, MeetingAction a)
    {
        if (a.OwnerType == ActionOwnerType.ExternalParty) return;
        var actor = await notify.ActorName();
        if (a.OwnerType == ActionOwnerType.User)
        {
            await team.EnsureMember(p, a.OwnerUserId, ProjectRole.TeamMember);
            await notify.Send(NotificationEvents.ActionAssigned, a.OwnerUserId, Item(p, a), Text.Get("notify.action_assigned", actor, a.Key, a.Text));
            return;
        }
        var d = await db.ProjectDisciplines.Where(x => x.Id == a.OwnerDisciplineId).Select(x => new DisciplineLead(x.LeadUserId, x.Discipline!.Name)).FirstAsync();
        await notify.Send(NotificationEvents.ActionAssigned, d.LeadUserId ?? p.ProjectManagerId, Item(p, a),
            Text.Get(d.LeadUserId is null ? "notify.action_no_lead" : "notify.action_discipline", actor, a.Key, a.Text, d.Name));
    }

    static async Task<Created<ActionCreated>> CreateAction(Guid id, ActionBody body, Access access, HubDb db, Notifier notify, TeamService team, TimeProvider clock)
    {
        var m = await db.Meetings.FirstOrDefaultAsync(x => x.Id == id) ?? throw ApiException.NotFound();
        var (p, ctx) = await access.Project(m.ProjectId);
        Access.Demand(Permissions.RaiseRegisterItem(access.Actor, ctx));
        var text = Check.Required(body.Text, "text", 2000);
        await Owner(db, p.Id, body.OwnerType, body.OwnerUserId, body.OwnerDisciplineId, body.OwnerExternalPartyId);
        await Related(db, p.Id, body.RelatedTaskId, body.RelatedDecisionId);
        var a = await Tx.Run(db, async () =>
        {
            var (seq, key) = await Keys.Next(db, p.Id, p.ProjectNumber, "action");
            var now = clock.GetUtcNow();
            var a = new MeetingAction
            {
                ProjectId = p.Id, Seq = seq, Key = key, MeetingId = m.Id, Text = text, OwnerType = body.OwnerType!, OwnerUserId = body.OwnerUserId,
                OwnerDisciplineId = body.OwnerDisciplineId, OwnerExternalPartyId = body.OwnerExternalPartyId, DueDate = body.DueDate,
                RelatedTaskId = body.RelatedTaskId, RelatedDecisionId = body.RelatedDecisionId, StatusChangedAt = now, LastActivityAt = now,
            };
            db.Actions.Add(a);
            foreach (var l in body.Links ?? []) await DecisionEndpoints.NewLink(db, access, p, ItemType.Action, a.Id, a.Key, l.TargetType, l.TargetId, ItemRelation.Related);
            await Assigned(db, notify, team, p, a);
            await db.SaveChangesAsync();
            return a;
        });
        var noLead = a.OwnerType == ActionOwnerType.Discipline && await db.ProjectDisciplines.AnyAsync(d => d.Id == a.OwnerDisciplineId && d.LeadUserId == null);
        return TypedResults.Created($"/api/v1/actions/{a.Id}", new ActionCreated(a.Id, a.Key, a.RowVersion, noLead ? [Text.Get("action.no_lead_warning")] : []));
    }

    static async Task<Ok<ActionDetail>> GetAction(string id, Access access, HubDb db, SettingsStore store, TimeProvider clock)
    {
        var settings = await store.Get(db);
        await using var snapshot = settings.IsFlagOn("work_screens")
            ? await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead) : null;
        var a = (Guid.TryParse(id, out var g) ? await db.Actions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == g)
            : await db.Actions.AsNoTracking().FirstOrDefaultAsync(x => x.Key == id)) ?? throw ApiException.NotFound();
        var (p, ctx) = await access.Project(a.ProjectId, track: false);
        var row = (await Rows(db, db.Actions.AsNoTracking().Where(x => x.Id == a.Id), clock.Today(settings))).Single();
        var edit = Permissions.EditRegisterItem(access.Actor, ctx, Facts(a));
        var converted = await db.ItemLinks.AnyAsync(l => l.ProjectId == p.Id && l.SourceType == ItemType.Action && l.SourceId == a.Id && l.TargetType == ItemType.Task && l.Relation == ItemRelation.ConvertedToTask);
        var context = await new RecordContextReader(db, p, settings).Read(RecordContextKind.Action, a.Id);
        string? Why(Allow x) => x.Ok ? null : Text.Get(x.Why!, x.Arg ?? p.Status);
        return TypedResults.Ok(await WorkScreenResponseMetadata.AttachDetailAsync(new ActionDetail(
            row,
            await db.Meetings.AsNoTracking().Where(m => m.Id == a.MeetingId && m.ProjectId == p.Id).Select(m => new ActionMeetingRef(m.Id, m.Title, m.MeetingDate, m.MeetingType, m.NotesLink)).FirstOrDefaultAsync() ?? throw ApiException.NotFound(),
            new ActionProject(p.Id, p.ProjectNumber, p.Name, p.Status),
            a.RelatedTaskId is { } tid ? await db.Tasks.AsNoTracking().Where(t => t.Id == tid && t.ProjectId == p.Id).Select(t => new ActionTaskRef(t.Id, t.Key, t.Name, t.Status)).FirstOrDefaultAsync() : null,
            a.RelatedDecisionId is { } did ? await db.Decisions.AsNoTracking().Where(d => d.Id == did && d.ProjectId == p.Id).Select(d => new ActionDecisionRef(d.Id, d.Key, d.Subject, d.Status)).FirstOrDefaultAsync() : null,
            await DecisionEndpoints.LinkRows(db, p.Id, ItemType.Action, a.Id, settings.IsFlagOn("screen_patterns"), settings.IsFlagOn("people_screens")),
            converted ? null : await TaskDiscipline(db, a),
            new ActionDetailPermissions(
                new RegisterEndpoints.RegisterPermission(edit.Ok, Why(edit)),
                converted ? [] : [.. ActionStatus.All.Where(to => Workflow.ActionStep(a.Status, to)).Select(to => new RegisterEndpoints.RegisterTransition(to, edit.Ok, Why(edit)))],
                !converted && ActionStatus.IsOpen(a.Status) && edit.Ok,
                Permissions.Comment(access.Actor, ctx).Ok), context), a, access, db, settings, clock));
    }

    static async Task<Ok<ActionMutation>> EditAction(Guid id, JsonElement body, HttpContext http, Access access, HubDb db, Notifier notify, TeamService team, TimeProvider clock, SettingsStore store)
        => await Tx.Notice<Ok<ActionMutation>>(db, store, async () =>
    {
        var (a, p, ctx) = await Load(db, access, id);
        Access.Demand(Permissions.EditRegisterItem(access.Actor, ctx, Facts(a)));
        var patch = new Patch(body);
        await Http.CheckVersion(db, http, a, patch.RowVersion);
        ProjectEndpoints.CorrectionReason(p, patch.Str("reason"));
        db.Audit.Reason = patch.Str("reason");
        if (patch.Has("text")) a.Text = Check.Required(patch.Str("text"), "text", 2000);
        if (patch.Has("dueDate")) a.DueDate = patch.Date("dueDate");
        if (patch.Has("meetingId"))
        {
            var mid = patch.Id("meetingId") ?? throw ApiException.Invalid("meetingId", "error.required");
            Check.That(await db.Meetings.AnyAsync(m => m.Id == mid && m.ProjectId == a.ProjectId), "meetingId", "error.not_found");
            a.MeetingId = mid;
        }
        if (patch.Has("relatedTaskId") || patch.Has("relatedDecisionId"))
        {
            var (t, d) = (patch.Has("relatedTaskId") ? patch.Id("relatedTaskId") : a.RelatedTaskId, patch.Has("relatedDecisionId") ? patch.Id("relatedDecisionId") : a.RelatedDecisionId);
            Check.That(!patch.Has("relatedTaskId") || !await db.ItemLinks.AnyAsync(l => l.SourceId == a.Id && l.Relation == ItemRelation.ConvertedToTask), "relatedTaskId", "action.follows_task_edit");
            await Related(db, a.ProjectId, t, d);
            (a.RelatedTaskId, a.RelatedDecisionId) = (t, d);
        }
        if (patch.Has("ownerType") || patch.Has("ownerUserId") || patch.Has("ownerDisciplineId") || patch.Has("ownerExternalPartyId"))
        {
            var type = patch.Has("ownerType") ? patch.Str("ownerType") : a.OwnerType;
            var (u, d, x) = (patch.Id("ownerUserId"), patch.Id("ownerDisciplineId"), patch.Id("ownerExternalPartyId"));
            await Owner(db, a.ProjectId, type, u, d, x);
            var changed = type != a.OwnerType || u != a.OwnerUserId || d != a.OwnerDisciplineId || x != a.OwnerExternalPartyId;
            (a.OwnerType, a.OwnerUserId, a.OwnerDisciplineId, a.OwnerExternalPartyId) = (type!, u, d, x);
            if (changed) await Assigned(db, notify, team, p, a);
        }
        a.LastActivityAt = clock.GetUtcNow();
        await db.SaveChangesAsync();
        Http.ETag(http, a);
        return TypedResults.Ok(new ActionMutation(a.Id, a.RowVersion));
    });

    static async Task<Ok<ActionMoved>> MoveAction(Guid id, ActionMove body, HttpContext http, Access access, HubDb db, TimeProvider clock)
    {
        var (a, p, ctx) = await Load(db, access, id);
        await Http.CheckVersion(db, http, a, body.RowVersion);
        Check.OneOf(body.ToStatus, ActionStatus.All, "toStatus");
        var (from, to) = (a.Status, body.ToStatus);
        if (!Workflow.ActionStep(from, to)) throw ApiException.Rule("illegal_transition", "action.illegal_transition", null, from, to);
        Access.Demand(Permissions.EditRegisterItem(access.Actor, ctx, Facts(a)));
        if (await db.ItemLinks.Where(l => l.SourceId == a.Id && l.Relation == ItemRelation.ConvertedToTask).Join(db.Tasks, l => l.TargetId, t => t.Id, (l, t) => t.Key).FirstOrDefaultAsync() is { } key)
            throw ApiException.Rule("follows_task", "action.follows_task", null, key); // MTG-03: the task decides
        var reason = to == ActionStatus.Cancelled ? Check.Reason(body.Reason) : string.IsNullOrWhiteSpace(body.Reason) ? null : body.Reason.Trim();
        ProjectEndpoints.CorrectionReason(p, reason);
        var now = clock.GetUtcNow();
        (a.Status, a.StatusChangedAt, a.LastActivityAt) = (to, now, now);
        db.Audit.Note(a, reason: reason);
        await db.SaveChangesAsync();
        return TypedResults.Ok(new ActionMoved(a.Id, a.Status, a.RowVersion));
    }

    /// MTG-03: a task pre-filled from the action — its text, due date, the owner (or the discipline's lead) as assignee and
    /// the owning discipline — created with every task rule, linked, and the action then follows the task.
    static async Task<Ok<ActionConverted>> Convert(Guid id, ConvertBody body, HttpContext http, Access access, HubDb db, TeamService team, Notifier notify, SettingsStore store,
        CurrentUser me, TimeProvider clock)
    {
        var (a, p, ctx) = await Load(db, access, id);
        await Http.CheckVersion(db, http, a, body.RowVersion);
        Access.Demand(Permissions.EditRegisterItem(access.Actor, ctx, Facts(a)));
        Check.That(ActionStatus.IsOpen(a.Status), "status", "action.convert_open");
        if (await db.ItemLinks.AnyAsync(l => l.SourceId == a.Id && l.Relation == ItemRelation.ConvertedToTask)) throw ApiException.Conflict("already_converted", "action.already_converted");
        var meeting = await db.Meetings.AsNoTracking().FirstAsync(m => m.Id == a.MeetingId);
        var lead = a.OwnerDisciplineId is { } od ? await db.ProjectDisciplines.Where(d => d.Id == od).Select(d => d.LeadUserId).FirstOrDefaultAsync() : null;
        var discipline = body.ProjectDisciplineId ?? await TaskDiscipline(db, a);
        var name = a.Text.Length > 200 ? a.Text[..200] : a.Text;
        var note = Text.Get("action.converted_from", a.Key, meeting.Title, meeting.MeetingDate.ToString("yyyy-MM-dd"));
        var t = await Tx.Run(db, async () =>
        {
            var (t, _) = await TaskEndpoints.NewTask(p.Id, new TaskEndpoints.CreateBody(name, name == a.Text ? note : $"{note}\n\n{a.Text}",
                discipline, body.DeliverableId, null, body.AssigneeId ?? a.OwnerUserId ?? lead,
                null, null, null, null, body.DueDate ?? a.DueDate, null, null), access, db, team, notify, store, me, clock);
            var now = clock.GetUtcNow();
            db.ItemLinks.Add(new ItemLink { ProjectId = p.Id, SourceType = ItemType.Action, SourceId = a.Id, TargetType = ItemType.Task, TargetId = t.Id,
                Relation = ItemRelation.ConvertedToTask, CreatedBy = me.Id, CreatedAt = now, AuditKey = a.Key });
            (a.RelatedTaskId, a.Status, a.StatusChangedAt, a.LastActivityAt) = (t.Id, Workflow.ActionFollowing(t.Status), now, now);
            db.Audit.Note(a, action: "Converted");
            await db.SaveChangesAsync();
            return t;
        });
        return TypedResults.Ok(new ActionConverted(a.Id, a.Status, a.RowVersion, t.Id, t.Key));
    }

    /// AC-DCV-05, FR-DCV-06: meeting mode reuses an existing open action for a handoff or change instead of creating a
    /// duplicate. Only the missing source and affected-work links are added; the edit rule, version and lifecycle of the
    /// action apply, a retry with the same request returns the first result, and nobody is notified again.
    static Task<Coordination.Result> Reuse(Guid projectId, Guid id, ReuseBody body, Access access, HubDb db, TimeProvider clock) =>
        Coordination.Run(projectId, body.RequestId, new { operation = "action.reuse", id, body }, access, db, clock, async (p, ctx) =>
        {
            var a = await db.Actions.SingleOrDefaultAsync(x => x.Id == id && x.ProjectId == p.Id) ?? throw ApiException.NotFound();
            Access.Demand(Permissions.EditRegisterItem(access.Actor, ctx, Facts(a)));
            Coordination.Version(a, body.RowVersion);
            Check.That(ActionStatus.IsOpen(a.Status), "status", "action.reuse_open");
            var links = (body.Links ?? []).DistinctBy(l => l.TargetId).ToArray();
            Check.That(links.Any(l => l.TargetType is ItemType.Handoff or ItemType.ChangeNotice), "links", "action.reuse_source");
            var linked = await db.ItemLinks.Where(l => l.SourceId == a.Id).Select(l => l.TargetId).ToListAsync();
            foreach (var l in links.Where(l => !linked.Contains(l.TargetId)))
                await DecisionEndpoints.NewLink(db, access, p, ItemType.Action, a.Id, a.Key, l.TargetType, l.TargetId, ItemRelation.Related);
            a.LastActivityAt = clock.GetUtcNow();
            db.Entry(a).Property(x => x.LastActivityAt).IsModified = true; // the version moves even when every link already existed
            db.Audit.Note(a, action: "Reused", reason: Check.Optional(body.Reason, "reason", 2000));
            return a;
        });
}
