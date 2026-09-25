using System.Text.Json;
using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Features;

/// Risk and Issue Registers (§12.10, §13.13, RSK-01..03, ISS-01..03, packet 014): risks scored on a 3 × 3 grid, issues with
/// a severity and a target date, and a realised risk that becomes a linked issue. A-07 and the issue input to health are
/// in the rules engine; the dashboard counts and Weekly Coordination section 10 read these rows.
public static class RegisterEndpoints
{
    public sealed record RiskBody(string Title, string? Description, Guid? OwnerId, int? Probability, int? Impact, string? Mitigation, string? TriggerIndicator,
        DateOnly? ReviewDate, Guid? ProjectDisciplineId, DecisionEndpoints.LinkInput[]? Links);
    public sealed record IssueBody(string Title, string? Description, Guid? RaisedById, Guid? OwnerId, string? Severity, DateOnly? DateRaised,
        DateOnly? TargetResolutionDate, Guid? ProjectDisciplineId, DecisionEndpoints.LinkInput[]? Links);
    public sealed record RiskMove(string ToStatus, string? Reason, Guid? IssueId, IssueBody? Issue, int? RowVersion);
    public sealed record IssueMove(string ToStatus, string? Reason, string? Resolution, DateOnly? ResolvedDate, int? RowVersion);
    public sealed record RegisterQuery(string? Status, string? Severity, Guid? OwnerId, Guid? DisciplineId, string? Indicator, string? Q);

    static readonly Col[] RiskCols =
    [
        new("key", "key", "key"), new("title", "title"), new("status", "status"), new("band", "severity"), new("score", "score", "number"),
        new("probability", "probability", "number"), new("impact", "impactScore", "number"), new("ownerName", "owner"), new("disciplineName", "discipline"),
        new("reviewDate", "reviewDate", "date"), new("mitigation", "mitigation"), new("triggerIndicator", "trigger"), new("realisedIssueKey", "realisedAs"),
    ];
    static readonly Col[] IssueCols =
    [
        new("key", "key", "key"), new("title", "title"), new("status", "status"), new("severity", "severity"), new("ownerName", "owner"), new("raisedByName", "raisedBy"),
        new("disciplineName", "discipline"), new("dateRaised", "dateRaised", "date"), new("targetResolutionDate", "targetDate", "date"),
        new("daysOverdue", "daysOverdue", "number"), new("resolvedDate", "resolvedDate", "date"), new("resolution", "resolution"), new("originRiskKey", "originRisk"),
    ];

    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/projects/{id:guid}/risks", RiskList);
        api.MapPost("/projects/{id:guid}/risks", CreateRisk);
        api.MapGet("/risks/{id}", GetRisk);
        api.MapPatch("/risks/{id:guid}", EditRisk);
        api.MapPost("/risks/{id:guid}/transition", MoveRisk);
        api.MapPost("/risks/{id:guid}/links", async (Guid id, DecisionEndpoints.LinkInput body, Access access, HubDb db) =>
        {
            var (r, p, ctx) = await LoadRisk(db, access, id);
            Access.Demand(Permissions.EditRegisterItem(access.Actor, ctx, Facts(r)));
            var link = await DecisionEndpoints.NewLink(db, access, p, ItemType.Risk, r.Id, r.Key, body.TargetType, body.TargetId, ItemRelation.Related);
            await db.SaveChangesAsync();
            return Results.Created($"/api/v1/item-links/{link.Id}", new { link.Id });
        });
        api.MapGet("/projects/{id:guid}/risks/export", async (Guid id, string? format, [AsParameters] RegisterQuery f, HttpContext http, Access access, HubDb db, SettingsStore store, TimeProvider clock) =>
        {
            var (p, _) = await access.Project(id, track: false);
            var rows = JsonSerializer.SerializeToNode(await RiskList(id, f, access, db, store, clock), JsonOpts.Web)!.AsArray();
            return await ExportFile.Send(db, store, format, Text.Get("export.risks", p.ProjectNumber), RiskCols, rows, await ListExportEndpoints.Filters(db, http), p.Id, $"{p.ProjectNumber}-risks", clock);
        });

        api.MapGet("/projects/{id:guid}/issues", IssueList);
        api.MapPost("/projects/{id:guid}/issues", CreateIssue);
        api.MapGet("/issues/{id}", GetIssue);
        api.MapPatch("/issues/{id:guid}", EditIssue);
        api.MapPost("/issues/{id:guid}/transition", MoveIssue);
        api.MapPost("/issues/{id:guid}/links", async (Guid id, DecisionEndpoints.LinkInput body, Access access, HubDb db) =>
        {
            var (i, p, ctx) = await LoadIssue(db, access, id);
            Access.Demand(Permissions.EditRegisterItem(access.Actor, ctx, Facts(i)));
            var link = await DecisionEndpoints.NewLink(db, access, p, ItemType.Issue, i.Id, i.Key, body.TargetType, body.TargetId, ItemRelation.Related);
            await db.SaveChangesAsync();
            return Results.Created($"/api/v1/item-links/{link.Id}", new { link.Id });
        });
        api.MapGet("/projects/{id:guid}/issues/export", async (Guid id, string? format, [AsParameters] RegisterQuery f, HttpContext http, Access access, HubDb db, SettingsStore store, TimeProvider clock) =>
        {
            var (p, _) = await access.Project(id, track: false);
            var rows = JsonSerializer.SerializeToNode(await IssueList(id, f, access, db, store, clock), JsonOpts.Web)!.AsArray();
            return await ExportFile.Send(db, store, format, Text.Get("export.issues", p.ProjectNumber), IssueCols, rows, await ListExportEndpoints.Filters(db, http), p.Id, $"{p.ProjectNumber}-issues", clock);
        });
    }

    public static async Task<(Risk R, Project P, ProjectContext Ctx)> LoadRisk(HubDb db, Access access, Guid id)
    {
        var r = await db.Risks.FirstOrDefaultAsync(x => x.Id == id) ?? throw ApiException.NotFound();
        var (p, ctx) = await access.Project(r.ProjectId);
        return (r, p, ctx);
    }

    public static async Task<(Issue I, Project P, ProjectContext Ctx)> LoadIssue(HubDb db, Access access, Guid id)
    {
        var i = await db.Issues.FirstOrDefaultAsync(x => x.Id == id) ?? throw ApiException.NotFound();
        var (p, ctx) = await access.Project(i.ProjectId);
        return (i, p, ctx);
    }

    /// The PM, the owner, whoever raised it and the lead of its discipline edit it (§8.5.2).
    internal static OwnedFacts Facts(Risk r) => new(r.OwnerId, r.CreatedBy, r.ProjectDisciplineId);
    internal static OwnedFacts Facts(Issue i) => new(i.OwnerId, i.RaisedById, i.ProjectDisciplineId);

    /// The grid scores in the chosen bands, for filtering in the database.
    public static int[] Scores(IEnumerable<string> bands) =>
        [.. Registers.Levels.SelectMany(p => Registers.Levels.Select(i => Registers.Score(p, i))).Distinct().Where(s => bands.Contains(Registers.Band(s)))];

    static int Level(int? v, string field)
    {
        Check.That(v is >= 1 and <= 3, field, "risk.level");
        return v!.Value;
    }

    static async Task<Guid?> Discipline(HubDb db, Guid projectId, Guid? id)
    {
        if (id is { } d) Check.That(await db.ProjectDisciplines.AnyAsync(x => x.Id == d && x.ProjectId == projectId && x.IsActive), "projectDisciplineId", "error.not_found");
        return id;
    }

    static object Perm(Allow x, Project p) => new { x.Ok, Reason = x.Ok ? null : Text.Get(x.Why!, x.Arg ?? p.Status) };

    // ---------- Risks ----------

    public static async Task<List<object>> RiskList(Guid id, [AsParameters] RegisterQuery f, Access access, HubDb db, SettingsStore store, TimeProvider clock)
    {
        await access.Project(id, track: false);
        var today = clock.Today(await store.Get(db));
        var q = db.Risks.AsNoTracking().Where(r => r.ProjectId == id);
        var st = Http.List(f.Status); if (st.Length > 0) q = q.Where(r => st.Contains(r.Status));
        var bands = Http.List(f.Severity); if (bands.Length > 0) { var scores = Scores(bands); q = q.Where(r => scores.Contains(r.Probability * r.Impact)); }
        if (f.OwnerId is { } o) q = q.Where(r => r.OwnerId == o);
        if (f.DisciplineId is { } d) q = q.Where(r => r.ProjectDisciplineId == d);
        if (!string.IsNullOrWhiteSpace(f.Q)) { var term = $"%{f.Q.Trim()}%"; q = q.Where(r => EF.Functions.ILike(r.Title, term) || EF.Functions.ILike(r.Key, term)); }
        foreach (var ind in Http.List(f.Indicator))
            q = ind switch
            {
                "open" => q.Where(r => r.Status == RiskStatus.Open || r.Status == RiskStatus.Monitoring),
                "reviewOverdue" => q.Where(r => (r.Status == RiskStatus.Open || r.Status == RiskStatus.Monitoring) && r.ReviewDate < today),
                _ => q,
            };
        return await RiskRows(db, q, today);
    }

    /// Default sort (§13.13): severity descending, then review date, then key.
    public static async Task<List<object>> RiskRows(HubDb db, IQueryable<Risk> q, DateOnly today)
    {
        var rows = await q.Select(r => new
        {
            r.Id, r.ProjectId, r.Key, r.Seq, r.Title, r.Status, r.OwnerId, r.Probability, r.Impact, r.Mitigation, r.TriggerIndicator, r.ReviewDate,
            r.ProjectDisciplineId, r.RealisedIssueId, r.RowVersion, r.LastActivityAt,
            OwnerName = db.Users.Where(u => u.Id == r.OwnerId).Select(u => u.IsActive ? u.DisplayName : u.DisplayName + " (Inactive)").FirstOrDefault(),
            DisciplineName = db.ProjectDisciplines.Where(x => x.Id == r.ProjectDisciplineId).Select(x => x.Discipline!.Name).FirstOrDefault(),
            RealisedIssueKey = db.Issues.Where(x => x.Id == r.RealisedIssueId).Select(x => x.Key).FirstOrDefault(),
        }).ToListAsync();
        return [.. rows.Select(r => (r, score: Registers.Score(r.Probability, r.Impact))).OrderByDescending(x => x.score).ThenBy(x => x.r.ReviewDate ?? DateOnly.MaxValue).ThenBy(x => x.r.Seq)
            .Select(x =>
            {
                var (r, score) = x;
                var late = Registers.ReviewOverdueDays(r.Status, r.ReviewDate, today);
                return (object)new
                {
                    r.Id, r.ProjectId, r.Key, r.Title, r.Status, r.OwnerId, r.OwnerName, r.Probability, r.Impact, Score = score, Band = Registers.Band(score),
                    r.Mitigation, r.TriggerIndicator, r.ReviewDate, IsReviewOverdue = late > 0, ReviewOverdueDays = late, r.ProjectDisciplineId, r.DisciplineName,
                    r.RealisedIssueId, r.RealisedIssueKey, r.RowVersion, r.LastActivityAt,
                };
            })];
    }

    static async Task<IResult> CreateRisk(Guid id, RiskBody body, Access access, HubDb db, TeamService team, TimeProvider clock, CurrentUser me)
    {
        var (p, ctx) = await access.Project(id);
        Access.Demand(Permissions.RaiseRegisterItem(access.Actor, ctx));
        var owner = body.OwnerId ?? me.Id;
        await DeliverableEndpoints.ActivePerson(db, owner, "ownerId");
        var (title, probability, impact) = (Check.Required(body.Title, "title", 300), Level(body.Probability, "probability"), Level(body.Impact, "impact"));
        var discipline = await Discipline(db, id, body.ProjectDisciplineId);
        var r = await Tx.Run(db, async () =>
        {
            var (seq, key) = await Keys.Next(db, id, p.ProjectNumber, "risk");
            var now = clock.GetUtcNow();
            var r = new Risk
            {
                ProjectId = id, Seq = seq, Key = key, Title = title, Description = Check.Optional(body.Description, "description", 8000), OwnerId = owner,
                Probability = probability, Impact = impact, Mitigation = Check.Optional(body.Mitigation, "mitigation", 4000),
                TriggerIndicator = Check.Optional(body.TriggerIndicator, "triggerIndicator", 2000), ReviewDate = body.ReviewDate, ProjectDisciplineId = discipline,
                StatusChangedAt = now, LastActivityAt = now,
            };
            db.Risks.Add(r);
            foreach (var l in body.Links ?? []) await DecisionEndpoints.NewLink(db, access, p, ItemType.Risk, r.Id, r.Key, l.TargetType, l.TargetId, ItemRelation.Related);
            await team.EnsureMember(p, owner, ProjectRole.TeamMember); // the owner must be able to open what they are asked to watch
            await db.SaveChangesAsync();
            return r;
        });
        return Results.Created($"/api/v1/risks/{r.Id}", new { r.Id, r.Key, r.RowVersion });
    }

    static async Task<object> GetRisk(string id, Access access, HubDb db, SettingsStore store, TimeProvider clock)
    {
        var r = (Guid.TryParse(id, out var g) ? await db.Risks.AsNoTracking().FirstOrDefaultAsync(x => x.Id == g)
            : await db.Risks.AsNoTracking().FirstOrDefaultAsync(x => x.Key == id)) ?? throw ApiException.NotFound();
        var (p, ctx) = await access.Project(r.ProjectId, track: false);
        var row = (await RiskRows(db, db.Risks.AsNoTracking().Where(x => x.Id == r.Id), clock.Today(await store.Get(db)))).Single();
        var edit = Permissions.EditRegisterItem(access.Actor, ctx, Facts(r));
        return new
        {
            Risk = row, r.Description,
            Project = new { p.Id, p.ProjectNumber, p.Name, p.Status },
            RealisedIssue = r.RealisedIssueId is { } ri ? await db.Issues.Where(x => x.Id == ri).Select(x => new { x.Id, x.Key, x.Title, x.Status }).FirstOrDefaultAsync() : null,
            Links = await DecisionEndpoints.LinkRows(db, r.Id),
            Permissions = new
            {
                Edit = Perm(edit, p),
                Transitions = RiskStatus.All.Where(to => Workflow.RiskStep(r.Status, to)).Select(to => new { To = to, edit.Ok, Reason = edit.Ok ? null : Text.Get(edit.Why!, edit.Arg ?? p.Status) }),
                Comment = Permissions.Comment(access.Actor, ctx).Ok,
            },
        };
    }

    static async Task<IResult> EditRisk(Guid id, JsonElement body, HttpContext http, Access access, HubDb db, TeamService team, TimeProvider clock)
    {
        var (r, p, ctx) = await LoadRisk(db, access, id);
        Access.Demand(Permissions.EditRegisterItem(access.Actor, ctx, Facts(r)));
        var patch = new Patch(body);
        await Http.CheckVersion(db, http, r, patch.RowVersion);
        ProjectEndpoints.CorrectionReason(p, patch.Str("reason"));
        db.Audit.Reason = patch.Str("reason");
        if (patch.Has("title")) r.Title = Check.Required(patch.Str("title"), "title", 300);
        if (patch.Has("description")) r.Description = Check.Optional(patch.Str("description"), "description", 8000);
        if (patch.Has("probability")) r.Probability = Level(patch.Int("probability"), "probability");
        if (patch.Has("impact")) r.Impact = Level(patch.Int("impact"), "impact");
        if (patch.Has("mitigation")) r.Mitigation = Check.Optional(patch.Str("mitigation"), "mitigation", 4000);
        if (patch.Has("triggerIndicator")) r.TriggerIndicator = Check.Optional(patch.Str("triggerIndicator"), "triggerIndicator", 2000);
        if (patch.Has("reviewDate")) r.ReviewDate = patch.Date("reviewDate");
        if (patch.Has("projectDisciplineId")) r.ProjectDisciplineId = await Discipline(db, r.ProjectId, patch.Id("projectDisciplineId"));
        if (patch.Has("ownerId"))
        {
            var o = patch.Id("ownerId") ?? throw ApiException.Invalid("ownerId", "error.required");
            await DeliverableEndpoints.ActivePerson(db, o, "ownerId");
            r.OwnerId = o;
            await team.EnsureMember(p, o, ProjectRole.TeamMember);
        }
        r.LastActivityAt = clock.GetUtcNow();
        await db.SaveChangesAsync();
        Http.ETag(http, r);
        return Results.Ok(new { r.Id, r.RowVersion });
    }

    /// RSK-03: Realised needs the issue it became, new or existing; the link is kept on both (realised issue, origin risk).
    static async Task<IResult> MoveRisk(Guid id, RiskMove body, HttpContext http, Access access, HubDb db, TeamService team, SettingsStore store, TimeProvider clock, CurrentUser me)
    {
        var (r, p, ctx) = await LoadRisk(db, access, id);
        await Http.CheckVersion(db, http, r, body.RowVersion);
        Check.OneOf(body.ToStatus, RiskStatus.All, "toStatus");
        var (from, to) = (r.Status, body.ToStatus);
        if (!Workflow.RiskStep(from, to)) throw ApiException.Rule("illegal_transition", "risk.illegal_transition", null, from, to);
        Access.Demand(Permissions.EditRegisterItem(access.Actor, ctx, Facts(r)));
        var reason = string.IsNullOrWhiteSpace(body.Reason) ? null : body.Reason.Trim();
        ProjectEndpoints.CorrectionReason(p, reason);
        var today = clock.Today(await store.Get(db));
        var issue = await Tx.Run(db, async () =>
        {
            Issue? issue = null;
            if (to == RiskStatus.Realised)
            {
                issue = body.IssueId is { } iid ? await db.Issues.FirstOrDefaultAsync(x => x.Id == iid && x.ProjectId == p.Id) ?? throw ApiException.Invalid("issueId", "error.not_found")
                    : body.Issue is { } b ? await NewIssue(db, access, team, p, b, me.Id, today, clock.GetUtcNow()) : throw ApiException.Invalid("issueId", "risk.realised_needs_issue");
                Check.That(issue.OriginRiskId is null || issue.OriginRiskId == r.Id, "issueId", "risk.issue_has_origin");
                issue.OriginRiskId = r.Id;
                r.RealisedIssueId = issue.Id;
            }
            var now = clock.GetUtcNow();
            r.Status = to;
            r.StatusChangedAt = now;
            r.LastActivityAt = now;
            db.Audit.Note(r, action: to == RiskStatus.Realised ? "Realised" : null, reason: reason);
            await db.SaveChangesAsync();
            return issue;
        });
        return Results.Ok(new { r.Id, r.Status, r.RowVersion, IssueId = issue?.Id, IssueKey = issue?.Key });
    }

    // ---------- Issues ----------

    public static async Task<List<object>> IssueList(Guid id, [AsParameters] RegisterQuery f, Access access, HubDb db, SettingsStore store, TimeProvider clock)
    {
        await access.Project(id, track: false);
        var today = clock.Today(await store.Get(db));
        var q = db.Issues.AsNoTracking().Where(i => i.ProjectId == id);
        var st = Http.List(f.Status); if (st.Length > 0) q = q.Where(i => st.Contains(i.Status));
        var sev = Http.List(f.Severity); if (sev.Length > 0) q = q.Where(i => sev.Contains(i.Severity));
        if (f.OwnerId is { } o) q = q.Where(i => i.OwnerId == o);
        if (f.DisciplineId is { } d) q = q.Where(i => i.ProjectDisciplineId == d);
        if (!string.IsNullOrWhiteSpace(f.Q)) { var term = $"%{f.Q.Trim()}%"; q = q.Where(i => EF.Functions.ILike(i.Title, term) || EF.Functions.ILike(i.Key, term)); }
        foreach (var ind in Http.List(f.Indicator))
            q = ind switch
            {
                "open" => q.Where(i => i.Status == IssueStatus.Open || i.Status == IssueStatus.InProgress),
                "overdue" => q.Where(i => (i.Status == IssueStatus.Open || i.Status == IssueStatus.InProgress) && i.TargetResolutionDate < today),
                _ => q,
            };
        return await IssueRows(db, q, today);
    }

    /// Default sort (§13.13): severity descending, then target resolution date, then key.
    public static async Task<List<object>> IssueRows(HubDb db, IQueryable<Issue> q, DateOnly today)
    {
        var rows = await q.Select(i => new
        {
            i.Id, i.ProjectId, i.Key, i.Seq, i.Title, i.Status, i.RaisedById, i.OwnerId, i.Severity, i.DateRaised, i.TargetResolutionDate, i.Resolution, i.ResolvedDate,
            i.OriginRiskId, i.ProjectDisciplineId, i.RowVersion, i.LastActivityAt,
            RaisedByName = db.Users.Where(u => u.Id == i.RaisedById).Select(u => u.DisplayName).FirstOrDefault(),
            OwnerName = db.Users.Where(u => u.Id == i.OwnerId).Select(u => u.IsActive ? u.DisplayName : u.DisplayName + " (Inactive)").FirstOrDefault(),
            DisciplineName = db.ProjectDisciplines.Where(x => x.Id == i.ProjectDisciplineId).Select(x => x.Discipline!.Name).FirstOrDefault(),
            OriginRiskKey = db.Risks.Where(x => x.Id == i.OriginRiskId).Select(x => x.Key).FirstOrDefault(),
        }).ToListAsync();
        return [.. rows.OrderBy(i => Registers.Weight(i.Severity)).ThenBy(i => i.TargetResolutionDate ?? DateOnly.MaxValue).ThenBy(i => i.Seq)
            .Select(i =>
            {
                var late = Registers.IssueOverdueDays(i.Status, i.TargetResolutionDate, today);
                return (object)new
                {
                    i.Id, i.ProjectId, i.Key, i.Title, i.Status, i.RaisedById, i.RaisedByName, i.OwnerId, i.OwnerName, i.Severity, i.DateRaised, i.TargetResolutionDate,
                    IsOverdue = late > 0, DaysOverdue = late, i.Resolution, i.ResolvedDate, i.OriginRiskId, i.OriginRiskKey, i.ProjectDisciplineId, i.DisciplineName,
                    i.RowVersion, i.LastActivityAt,
                };
            })];
    }

    static async Task<IResult> CreateIssue(Guid id, IssueBody body, Access access, HubDb db, TeamService team, SettingsStore store, TimeProvider clock, CurrentUser me)
    {
        var (p, ctx) = await access.Project(id);
        Access.Demand(Permissions.RaiseRegisterItem(access.Actor, ctx));
        var today = clock.Today(await store.Get(db));
        var i = await Tx.Run(db, async () =>
        {
            var i = await NewIssue(db, access, team, p, body, me.Id, today, clock.GetUtcNow());
            await db.SaveChangesAsync();
            return i;
        });
        return Results.Created($"/api/v1/issues/{i.Id}", new { i.Id, i.Key, i.RowVersion });
    }

    static async Task<Issue> NewIssue(HubDb db, Access access, TeamService team, Project p, IssueBody b, Guid me, DateOnly today, DateTimeOffset now)
    {
        var owner = b.OwnerId ?? me;
        await DeliverableEndpoints.ActivePerson(db, owner, "ownerId");
        if (b.RaisedById is { } rb) await DeliverableEndpoints.ActivePerson(db, rb, "raisedById");
        var title = Check.Required(b.Title, "title", 300);
        var severity = b.Severity ?? throw ApiException.Invalid("severity", "error.required");
        Check.OneOf(severity, Impact.All, "severity");
        var raised = b.DateRaised ?? today;
        Check.That(raised <= today, "dateRaised", "issue.future_raised");
        Check.That(b.TargetResolutionDate is null || b.TargetResolutionDate >= raised, "targetResolutionDate", "issue.target_before_raised");
        var discipline = await Discipline(db, p.Id, b.ProjectDisciplineId);
        var (seq, key) = await Keys.Next(db, p.Id, p.ProjectNumber, "issue");
        var i = new Issue
        {
            ProjectId = p.Id, Seq = seq, Key = key, Title = title, Description = Check.Optional(b.Description, "description", 8000), RaisedById = b.RaisedById ?? me,
            OwnerId = owner, Severity = severity, DateRaised = raised, TargetResolutionDate = b.TargetResolutionDate, ProjectDisciplineId = discipline,
            StatusChangedAt = now, LastActivityAt = now,
        };
        db.Issues.Add(i);
        foreach (var l in b.Links ?? []) await DecisionEndpoints.NewLink(db, access, p, ItemType.Issue, i.Id, i.Key, l.TargetType, l.TargetId, ItemRelation.Related);
        await team.EnsureMember(p, owner, ProjectRole.TeamMember);
        return i;
    }

    static async Task<object> GetIssue(string id, Access access, HubDb db, SettingsStore store, TimeProvider clock)
    {
        var i = (Guid.TryParse(id, out var g) ? await db.Issues.AsNoTracking().FirstOrDefaultAsync(x => x.Id == g)
            : await db.Issues.AsNoTracking().FirstOrDefaultAsync(x => x.Key == id)) ?? throw ApiException.NotFound();
        var (p, ctx) = await access.Project(i.ProjectId, track: false);
        var row = (await IssueRows(db, db.Issues.AsNoTracking().Where(x => x.Id == i.Id), clock.Today(await store.Get(db)))).Single();
        var edit = Permissions.EditRegisterItem(access.Actor, ctx, Facts(i));
        return new
        {
            Issue = row, i.Description,
            Project = new { p.Id, p.ProjectNumber, p.Name, p.Status },
            OriginRisk = i.OriginRiskId is { } ri ? await db.Risks.Where(x => x.Id == ri).Select(x => new { x.Id, x.Key, x.Title, x.Status }).FirstOrDefaultAsync() : null,
            Links = await DecisionEndpoints.LinkRows(db, i.Id),
            Permissions = new
            {
                Edit = Perm(edit, p),
                Transitions = IssueStatus.All.Where(to => Workflow.IssueStep(i.Status, to)).Select(to => new { To = to, edit.Ok, Reason = edit.Ok ? null : Text.Get(edit.Why!, edit.Arg ?? p.Status) }),
                Comment = Permissions.Comment(access.Actor, ctx).Ok,
            },
        };
    }

    static async Task<IResult> EditIssue(Guid id, JsonElement body, HttpContext http, Access access, HubDb db, TeamService team, SettingsStore store, TimeProvider clock)
    {
        var (i, p, ctx) = await LoadIssue(db, access, id);
        Access.Demand(Permissions.EditRegisterItem(access.Actor, ctx, Facts(i)));
        var patch = new Patch(body);
        await Http.CheckVersion(db, http, i, patch.RowVersion);
        ProjectEndpoints.CorrectionReason(p, patch.Str("reason"));
        db.Audit.Reason = patch.Str("reason");
        var today = clock.Today(await store.Get(db));
        if (patch.Has("title")) i.Title = Check.Required(patch.Str("title"), "title", 300);
        if (patch.Has("description")) i.Description = Check.Optional(patch.Str("description"), "description", 8000);
        if (patch.Has("severity")) { var s = patch.Str("severity"); Check.OneOf(s, Impact.All, "severity"); i.Severity = s!; }
        if (patch.Has("dateRaised"))
        {
            i.DateRaised = patch.Date("dateRaised") ?? throw ApiException.Invalid("dateRaised", "error.required");
            Check.That(i.DateRaised <= today, "dateRaised", "issue.future_raised");
        }
        if (patch.Has("targetResolutionDate")) i.TargetResolutionDate = patch.Date("targetResolutionDate");
        if (patch.Has("dateRaised") || patch.Has("targetResolutionDate"))
            Check.That(i.TargetResolutionDate is null || i.TargetResolutionDate >= i.DateRaised, "targetResolutionDate", "issue.target_before_raised");
        if (patch.Has("resolution"))
        {
            Check.That(i.Status == IssueStatus.Resolved, "resolution", "issue.resolution_when_resolved");
            i.Resolution = Check.Required(patch.Str("resolution"), "resolution", 8000); // ISS-02 still holds
        }
        if (patch.Has("projectDisciplineId")) i.ProjectDisciplineId = await Discipline(db, i.ProjectId, patch.Id("projectDisciplineId"));
        if (patch.Has("raisedById")) { var rb = patch.Id("raisedById") ?? throw ApiException.Invalid("raisedById", "error.required"); await DeliverableEndpoints.ActivePerson(db, rb, "raisedById"); i.RaisedById = rb; }
        if (patch.Has("ownerId"))
        {
            var o = patch.Id("ownerId") ?? throw ApiException.Invalid("ownerId", "error.required");
            await DeliverableEndpoints.ActivePerson(db, o, "ownerId");
            i.OwnerId = o;
            await team.EnsureMember(p, o, ProjectRole.TeamMember);
        }
        i.LastActivityAt = clock.GetUtcNow();
        await db.SaveChangesAsync();
        Http.ETag(http, i);
        return Results.Ok(new { i.Id, i.RowVersion });
    }

    /// ISS-02: Resolved needs the resolution and records the resolved date; cancelling and reopening need a reason.
    static async Task<IResult> MoveIssue(Guid id, IssueMove body, HttpContext http, Access access, HubDb db, SettingsStore store, TimeProvider clock)
    {
        var (i, p, ctx) = await LoadIssue(db, access, id);
        await Http.CheckVersion(db, http, i, body.RowVersion);
        Check.OneOf(body.ToStatus, IssueStatus.All, "toStatus");
        var (from, to) = (i.Status, body.ToStatus);
        if (!Workflow.IssueStep(from, to)) throw ApiException.Rule("illegal_transition", "issue.illegal_transition", null, from, to);
        Access.Demand(Permissions.EditRegisterItem(access.Actor, ctx, Facts(i)));
        var today = clock.Today(await store.Get(db));
        var reason = string.IsNullOrWhiteSpace(body.Reason) ? null : body.Reason.Trim();
        switch (to)
        {
            case IssueStatus.Resolved:
                i.Resolution = Check.Required(body.Resolution, "resolution", 8000);
                i.ResolvedDate = body.ResolvedDate ?? today;
                Check.That(i.ResolvedDate <= today, "resolvedDate", "issue.future_resolved");
                break;
            case IssueStatus.Cancelled:
                reason = Check.Reason(body.Reason);
                break;
            case IssueStatus.InProgress when from == IssueStatus.Resolved:
                reason = Check.Reason(body.Reason);
                i.ResolvedDate = null;
                break;
        }
        ProjectEndpoints.CorrectionReason(p, reason);
        var now = clock.GetUtcNow();
        i.Status = to;
        i.StatusChangedAt = now;
        i.LastActivityAt = now;
        db.Audit.Note(i, action: to == IssueStatus.Resolved ? "Resolved" : from == IssueStatus.Resolved ? "Reopened" : null, reason: reason);
        await db.SaveChangesAsync();
        return Results.Ok(new { i.Id, i.Status, i.RowVersion });
    }
}
