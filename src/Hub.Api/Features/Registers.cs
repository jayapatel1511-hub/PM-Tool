using System.Text.Json;
using System.Text.Json.Serialization;
using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Features;

/// Risk and Issue Registers (§12.10, §13.13, RSK-01..03, ISS-01..03, packet 014): risks scored on a 3 × 3 grid, issues with
/// a severity and a target date, and a realised risk that becomes a linked issue. A-07 and the issue input to health are
/// in the rules engine; the dashboard counts and Weekly Coordination section 10 read these rows.
public static partial class RegisterEndpoints
{
    public sealed record RiskBody(string Title, string? Description, Guid? OwnerId, int? Probability, int? Impact, string? Mitigation, string? TriggerIndicator,
        DateOnly? ReviewDate, Guid? ProjectDisciplineId, DecisionEndpoints.LinkInput[]? Links);
    public sealed record IssueBody(string Title, string? Description, Guid? RaisedById, Guid? OwnerId, string? Severity, DateOnly? DateRaised,
        DateOnly? TargetResolutionDate, Guid? ProjectDisciplineId, DecisionEndpoints.LinkInput[]? Links, Guid[]? AffectedDisciplineIds = null,
        string? IssueType = null, IssueLocationBody[]? Locations = null, IssueDocumentBody[]? Documents = null);
    public sealed record RiskMove(string ToStatus, string? Reason, Guid? IssueId, IssueBody? Issue, int? RowVersion);
    public sealed record IssueMove(string ToStatus, string? Reason, string? Resolution, DateOnly? ResolvedDate, int? RowVersion,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Guid? RootCauseCategoryId = null);
    public sealed record RegisterQuery(string? Status, string? Severity, Guid? OwnerId, Guid? DisciplineId, string? Indicator, string? Q,
        string? Location = null, string? Document = null, string? Revision = null, string? Verification = null,
        string? Alignment = null, decimal? StationFrom = null, decimal? StationTo = null, string? StationUnits = null, string? IssueType = null, string? RootCause = null);
    public sealed record IssueLocationBody(string Kind, string? SiteArea, string? Building, string? Level, string? Room, string? AssetSystem,
        string? Alignment, decimal? StartStation, decimal? EndStation, string? StationUnits, decimal? CoordinateX, decimal? CoordinateY,
        decimal? CoordinateZ, string? CoordinateReferenceSystem, string? CoordinateUnits, int RowVersion);
    public sealed record IssueDocumentBody(string Kind, string Identifier, string Revision, string SourceUrl, string? ExternalTopicId,
        string? ModelElementGuid, string? ViewpointUrl, bool IsAvailable, int RowVersion, string? SourceSystem = null, string? StableSourceId = null);
    public sealed record ReplaceIssueDocumentBody(Guid RequestId, int RowVersion, int DocumentRowVersion,
        IssueDocumentBody Document, string Reason);
    public sealed record IssueVerificationBody(Guid VerifierId, string Status, string? EvidenceUrl, string? Note, int RowVersion);

    public sealed record RiskListRow(Guid Id, Guid ProjectId, string Key, string Title, string Status, Guid OwnerId, string? OwnerName,
        int Probability, int Impact, int Score, string Band, string? Mitigation, string? TriggerIndicator, DateOnly? ReviewDate,
        bool IsReviewOverdue, int ReviewOverdueDays, Guid? ProjectDisciplineId, string? DisciplineName, Guid? RealisedIssueId,
        string? RealisedIssueKey, int RowVersion, DateTimeOffset LastActivityAt);
    public sealed record RegisterProject(Guid Id, string ProjectNumber, string Name, string Status);
    public sealed record RealisedRiskIssue(Guid Id, string Key, string Title, string Status);
    public sealed record RegisterPermission(bool Ok, string? Reason);
    public sealed record RegisterTransition(string To, bool Ok, string? Reason);
    public sealed record RiskPermissions(RegisterPermission Edit, IEnumerable<RegisterTransition> Transitions, bool Comment);
    public sealed partial record RiskDetailData(RiskListRow Risk, string? Description, RegisterProject Project, RealisedRiskIssue? RealisedIssue,
        List<DecisionEndpoints.ItemLinkRow> Links, RiskPermissions Permissions,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] RecordContextView? Context = null);

    /// A raised risk or issue: its id, key and first version.
    public sealed record RegisterCreated(Guid Id, string Key, int RowVersion);
    /// What an edit, or a location, document or verification added to an issue, answers: the id and the row's new version.
    public sealed record RegisterSaved(Guid Id, int RowVersion);
    /// The risk after a move, with the issue it became when it was realised.
    public sealed record RiskMoved(Guid Id, string Status, int RowVersion, Guid? IssueId, string? IssueKey);
    public sealed record IssueMoved(Guid Id, string Status, int RowVersion);
    /// One issue in the register, with its locations, references, affected disciplines and verification worked out for the list.
    public sealed record IssueRow(Guid Id, Guid ProjectId, string Key, string Title, string Status, string IssueType, Guid RaisedById, string? RaisedByName, Guid OwnerId, string? OwnerName,
        string Severity, DateOnly DateRaised, DateOnly? TargetResolutionDate, bool IsOverdue, int DaysOverdue, string? Resolution, DateOnly? ResolvedDate, Guid? OriginRiskId,
        string? OriginRiskKey, Guid? ProjectDisciplineId, string? DisciplineName, string[] LocationLabels, string LocationSummary, Guid[] AffectedDisciplineIds,
        string[] AffectedDisciplineNames, string AffectedDisciplineSummary, string DocumentSummary, string[] DocumentIdentifiers, string[] DocumentRevisions,
        string VerificationStatus, int RowVersion, DateTimeOffset LastActivityAt,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IssueRootCauseValue? RootCause = null);
    public sealed record IssueOriginRisk(Guid Id, string Key, string Title, string Status);
    public sealed record IssuePermissions(RegisterPermission Edit, RegisterPermission ChangeType, IEnumerable<RegisterTransition> Transitions, bool Comment);
    public sealed partial record IssueDetailData(IssueRow Issue, string? Description, RegisterProject Project, IssueOriginRisk? OriginRisk,
        List<DecisionEndpoints.ItemLinkRow> Links, IssuePermissions Permissions,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] RecordContextView? Context = null);
    public sealed record IssueLocationRow(Guid Id, string Kind, string? SiteArea, string? Building, string? Level, string? Room, string? AssetSystem, string? Alignment,
        decimal? StartStation, decimal? EndStation, string? StationUnits, decimal? CoordinateX, decimal? CoordinateY, decimal? CoordinateZ, string? CoordinateReferenceSystem,
        string? CoordinateUnits, int RowVersion);
    public sealed record IssueDocumentRow(Guid Id, string Kind, string Identifier, string Revision, string SourceUrl, string? SourceSystem, string? StableSourceId, Guid? RegisteredBy,
        DateTimeOffset RegisteredAt, string RegistrationMethod, string? ExternalTopicId, string? ModelElementGuid, string? ViewpointUrl, bool IsAvailable, Guid? ReplacedById, int RowVersion);
    public sealed record IssueVerificationRow(Guid Id, Guid VerifierId, string Status, string? EvidenceUrl, string? Note, DateTimeOffset? VerifiedAt, int RowVersion);

    /// What the risk register's query reads for each risk, before the names are put beside it.
    sealed record RiskJoin(Guid Id, Guid ProjectId, string Key, int Seq, string Title, string Status, Guid OwnerId, int Probability, int Impact, string? Mitigation, string? TriggerIndicator,
        DateOnly? ReviewDate, Guid? ProjectDisciplineId, Guid? RealisedIssueId, int RowVersion, DateTimeOffset LastActivityAt, string? OwnerName, string? DisciplineName, string? RealisedIssueKey);
    /// The same for each issue.
    sealed record IssueJoin(Guid Id, Guid ProjectId, string Key, int Seq, string Title, string Status, string IssueType, Guid RaisedById, Guid OwnerId, string Severity, DateOnly DateRaised,
        DateOnly? TargetResolutionDate, string? Resolution, DateOnly? ResolvedDate, Guid? OriginRiskId, Guid? ProjectDisciplineId, int RowVersion, DateTimeOffset LastActivityAt,
        string? RaisedByName, string? OwnerName, string? DisciplineName, string? OriginRiskKey, Guid? RootCauseCategoryId, string? RootCauseLabelSnapshot);
    sealed record AffectedName(Guid IssueId, Guid ProjectDisciplineId, string Name);
    sealed record LatestVerification(string Status, Guid VerifierId);

    static readonly Col[] RiskCols = Hub.Api.Records.RiskRecord.RegistersRiskCols;
    static readonly Col[] IssueCols = Hub.Api.Records.IssueRecord.RegistersIssueCols;

    public static void Map(RouteGroupBuilder api)
    {
        IssueRootCauses.Map(api);
        api.MapGet("/projects/{id:guid}/risks", async (Guid id, [AsParameters] RegisterQuery f, Access access, HubDb db, SettingsStore store, TimeProvider clock) =>
            TypedResults.Ok(await RiskList(id, f, access, db, store, clock)));
        api.MapPost("/projects/{id:guid}/risks", CreateRisk);
        api.MapGet("/risks/{id}", async (string id, Access access, HubDb db, SettingsStore store, TimeProvider clock) =>
            TypedResults.Ok(await GetRisk(id, access, db, store, clock)));
        api.MapPatch("/risks/{id:guid}", EditRisk);
        api.MapPost("/risks/{id:guid}/transition", MoveRisk);
        api.MapPost("/risks/{id:guid}/links", async Task<Created<DecisionEndpoints.DecisionLinkMutation>> (Guid id, DecisionEndpoints.LinkInput body, Access access, HubDb db) =>
        {
            var (r, p, ctx) = await LoadRisk(db, access, id);
            Access.Demand(Permissions.EditRegisterItem(access.Actor, ctx, Facts(r)));
            var link = await DecisionEndpoints.NewLink(db, access, p, ItemType.Risk, r.Id, r.Key, body.TargetType, body.TargetId, ItemRelation.Related);
            await db.SaveChangesAsync();
            return TypedResults.Created($"/api/v1/item-links/{link.Id}", new DecisionEndpoints.DecisionLinkMutation(link.Id));
        });
        api.MapGet("/projects/{id:guid}/risks/export", async (Guid id, string? format, [AsParameters] RegisterQuery f, HttpContext http, Access access, HubDb db, SettingsStore store, TimeProvider clock) =>
        {
            var (p, _) = await access.Project(id, track: false);
            var rows = JsonSerializer.SerializeToNode(await RiskList(id, f, access, db, store, clock), JsonOpts.Web)!.AsArray();
            return await ExportFile.Send(db, store, format, Text.Get("export.risks", p.ProjectNumber), RiskCols, rows, await ListExportEndpoints.Filters(db, http, access), p.Id, $"{p.ProjectNumber}-risks", clock);
        });

        api.MapGet("/projects/{id:guid}/issues", async (Guid id, [AsParameters] RegisterQuery f, Access access, HubDb db, SettingsStore store, TimeProvider clock) =>
            TypedResults.Ok(await IssueList(id, f, access, db, store, clock)));
        api.MapPost("/projects/{id:guid}/issues", CreateIssue);
        api.MapGet("/issues/{id}", GetIssue);
        api.MapPatch("/issues/{id:guid}", EditIssue);
        api.MapPost("/issues/{id:guid}/transition", MoveIssue).WithMetadata(new IssueRootCauseReplay.Guard());
        api.MapGet("/issues/{id:guid}/locations", ListIssueLocations);
        api.MapPost("/issues/{id:guid}/locations", AddIssueLocation);
        api.MapGet("/issues/{id:guid}/documents", ListIssueDocuments);
        api.MapPost("/issues/{id:guid}/documents", AddIssueDocument);
        api.MapPost("/issues/{id:guid}/documents/{documentId:guid}/replace", ReplaceIssueDocument).WithMetadata(new Coordination.AtomicCommand());
        api.MapGet("/issues/{id:guid}/verification", ListIssueVerification);
        api.MapPost("/issues/{id:guid}/verification", AddIssueVerification);
        api.MapPost("/issues/{id:guid}/links", async Task<Created<DecisionEndpoints.DecisionLinkMutation>> (Guid id, DecisionEndpoints.LinkInput body, Access access, HubDb db) =>
        {
            var (i, p, ctx) = await LoadIssue(db, access, id);
            Access.Demand(Permissions.EditRegisterItem(access.Actor, ctx, Facts(i)));
            var link = await DecisionEndpoints.NewLink(db, access, p, ItemType.Issue, i.Id, i.Key, body.TargetType, body.TargetId, ItemRelation.Related);
            await db.SaveChangesAsync();
            return TypedResults.Created($"/api/v1/item-links/{link.Id}", new DecisionEndpoints.DecisionLinkMutation(link.Id));
        });
        api.MapGet("/projects/{id:guid}/issues/export", async (Guid id, string? format, [AsParameters] RegisterQuery f, HttpContext http, Access access, HubDb db, SettingsStore store, TimeProvider clock) =>
        {
            var (p, _) = await access.Project(id, track: false);
            var rows = JsonSerializer.SerializeToNode(await IssueList(id, f, access, db, store, clock), JsonOpts.Web)!.AsArray();
            return await ExportFile.Send(db, store, format, Text.Get("export.issues", p.ProjectNumber), IssueCols, rows, await ListExportEndpoints.Filters(db, http, access), p.Id, $"{p.ProjectNumber}-issues", clock);
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

    static async Task<Guid?> Discipline(HubDb db, Guid projectId, Guid? id, string field = "projectDisciplineId")
    {
        if (id is { } d) Check.That(await db.ProjectDisciplines.AnyAsync(x => x.Id == d && x.ProjectId == projectId && x.IsActive), field, "error.not_found");
        return id;
    }

    static RegisterPermission Perm(Allow x, Project p) => new(x.Ok, x.Ok ? null : Text.Get(x.Why!, x.Arg ?? p.Status));

    // ---------- Risks ----------

    public static async Task<List<RiskListRow>> RiskList(Guid id, [AsParameters] RegisterQuery f, Access access, HubDb db, SettingsStore store, TimeProvider clock)
    {
        await access.Project(id, track: false);
        var today = clock.Today(await store.Get(db));
        return await RiskRows(db, RiskQuery(db, id, f, today), today);
    }

    /// The risk register's filters, shared with the dashboard counts so a number always equals the list it opens (FR-RCV-08).
    public static IQueryable<Risk> RiskQuery(HubDb db, Guid id, RegisterQuery f, DateOnly today)
    {
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
        return q;
    }

    /// Default sort (§13.13): severity descending, then review date, then key.
    public static async Task<List<RiskListRow>> RiskRows(HubDb db, IQueryable<Risk> q, DateOnly today)
    {
        var rows = await q.Select(r => new RiskJoin(
            r.Id, r.ProjectId, r.Key, r.Seq, r.Title, r.Status, r.OwnerId, r.Probability, r.Impact, r.Mitigation, r.TriggerIndicator, r.ReviewDate,
            r.ProjectDisciplineId, r.RealisedIssueId, r.RowVersion, r.LastActivityAt,
            db.Users.Where(u => u.Id == r.OwnerId).Select(u => u.IsActive ? u.DisplayName : u.DisplayName + " (Inactive)").FirstOrDefault(),
            db.ProjectDisciplines.Where(x => x.Id == r.ProjectDisciplineId).Select(x => x.Discipline!.Name).FirstOrDefault(),
            db.Issues.Where(x => x.Id == r.RealisedIssueId && x.ProjectId == r.ProjectId).Select(x => x.Key).FirstOrDefault())).ToListAsync();
        return [.. rows.Select(r => (r, score: Registers.Score(r.Probability, r.Impact))).OrderByDescending(x => x.score).ThenBy(x => x.r.ReviewDate ?? DateOnly.MaxValue).ThenBy(x => x.r.Seq)
            .Select(x =>
            {
                var (r, score) = x;
                var late = Registers.ReviewOverdueDays(r.Status, r.ReviewDate, today);
                return new RiskListRow(r.Id, r.ProjectId, r.Key, r.Title, r.Status, r.OwnerId, r.OwnerName, r.Probability, r.Impact,
                    score, Registers.Band(score), r.Mitigation, r.TriggerIndicator, r.ReviewDate, late > 0, late,
                    r.ProjectDisciplineId, r.DisciplineName, r.RealisedIssueId, r.RealisedIssueKey, r.RowVersion, r.LastActivityAt);
            })];
    }

    static async Task<Created<RegisterCreated>> CreateRisk(Guid id, RiskBody body, Access access, HubDb db, TeamService team, TimeProvider clock, CurrentUser me)
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
        return TypedResults.Created($"/api/v1/risks/{r.Id}", new RegisterCreated(r.Id, r.Key, r.RowVersion));
    }

    internal static async Task<RiskDetailData> GetRisk(string id, Access access, HubDb db, SettingsStore store, TimeProvider clock)
    {
        var settings = await store.Get(db);
        await using var snapshot = settings.IsFlagOn("work_screens")
            ? await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead) : null;
        var r = (Guid.TryParse(id, out var g) ? await db.Risks.AsNoTracking().FirstOrDefaultAsync(x => x.Id == g)
            : await db.Risks.AsNoTracking().FirstOrDefaultAsync(x => x.Key == id)) ?? throw ApiException.NotFound();
        var (p, ctx) = await access.Project(r.ProjectId, track: false);
        var row = (await RiskRows(db, db.Risks.AsNoTracking().Where(x => x.Id == r.Id), clock.Today(settings))).Single();
        var edit = Permissions.EditRegisterItem(access.Actor, ctx, Facts(r));
        var reason = edit.Ok ? null : Text.Get(edit.Why!, edit.Arg ?? p.Status);
        var context = await new RecordContextReader(db, p, settings).Read(RecordContextKind.Risk, r.Id);
        return await WorkScreenResponseMetadata.AttachDetailAsync(new RiskDetailData(row, r.Description, new(p.Id, p.ProjectNumber, p.Name, p.Status),
            r.RealisedIssueId is { } ri ? await db.Issues.Where(x => x.Id == ri && x.ProjectId == p.Id).Select(x => new RealisedRiskIssue(x.Id, x.Key, x.Title, x.Status)).FirstOrDefaultAsync() : null,
            await DecisionEndpoints.LinkRows(db, p.Id, ItemType.Risk, r.Id, settings.IsFlagOn("screen_patterns")),
            new(new(edit.Ok, reason), RiskStatus.All.Where(to => Workflow.RiskStep(r.Status, to)).Select(to => new RegisterTransition(to, edit.Ok, reason)),
                Permissions.Comment(access.Actor, ctx).Ok), context), r, access, db, settings, clock);
    }

    static async Task<Ok<RegisterSaved>> EditRisk(Guid id, JsonElement body, HttpContext http, Access access, HubDb db, TeamService team, TimeProvider clock, SettingsStore store)
        => await Tx.Notice<Ok<RegisterSaved>>(db, store, async () =>
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
        return TypedResults.Ok(new RegisterSaved(r.Id, r.RowVersion));
    });

    /// RSK-03: Realised needs the issue it became, new or existing; the link is kept on both (realised issue, origin risk).
    static async Task<Ok<RiskMoved>> MoveRisk(Guid id, RiskMove body, HttpContext http, Access access, HubDb db, TeamService team, SettingsStore store, TimeProvider clock, CurrentUser me, Notifier notify)
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
                    : body.Issue is { } b ? await NewIssue(db, access, team, notify, p, b, me.Id, today, clock.GetUtcNow()) : throw ApiException.Invalid("issueId", "risk.realised_needs_issue");
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
        return TypedResults.Ok(new RiskMoved(r.Id, r.Status, r.RowVersion, issue?.Id, issue?.Key));
    }

    // ---------- Issues ----------

    public static async Task<List<IssueRow>> IssueList(Guid id, [AsParameters] RegisterQuery f, Access access, HubDb db, SettingsStore store, TimeProvider clock)
    {
        await access.Project(id, track: false);
        var settings = await store.Get(db); var today = clock.Today(settings);
        var enabled = settings.IsFlagOn(IssueRootCauses.Flag);
        return await IssueRows(db, IssueQuery(db, id, f, today, enabled), today, Http.List(f.Verification), enabled);
    }

    /// The issue register's filters, shared with the dashboard counts so a number always equals the list it opens (FR-RCV-08).
    public static IQueryable<Issue> IssueQuery(HubDb db, Guid id, RegisterQuery f, DateOnly today, bool rootCauseEnabled = false)
    {
        var q = db.Issues.AsNoTracking().Where(i => i.ProjectId == id);
        var st = Http.List(f.Status); if (st.Length > 0) q = q.Where(i => st.Contains(i.Status));
        var sev = Http.List(f.Severity); if (sev.Length > 0) q = q.Where(i => sev.Contains(i.Severity));
        var types = Http.List(f.IssueType); if (types.Length > 0) q = q.Where(i => types.Contains(i.IssueType));
        if (f.OwnerId is { } o) q = q.Where(i => i.OwnerId == o);
        // AC-LOC-01: each affected discipline sees the same issue as its primary discipline.
        if (f.DisciplineId is { } d) q = q.Where(i => i.ProjectDisciplineId == d || db.IssueAffectedDisciplines.Any(x => x.IssueId == i.Id && x.ProjectDisciplineId == d));
        if (!string.IsNullOrWhiteSpace(f.Q)) { var term = $"%{f.Q.Trim()}%"; q = q.Where(i => EF.Functions.ILike(i.Title, term) || EF.Functions.ILike(i.Key, term)); }
        if (!string.IsNullOrWhiteSpace(f.Location))
        {
            var term = $"%{f.Location.Trim()}%";
            q = q.Where(i => db.IssueLocations.Any(x => x.IssueId == i.Id &&
                (EF.Functions.ILike(x.Kind, term) || EF.Functions.ILike(x.SiteArea ?? "", term) || EF.Functions.ILike(x.Building ?? "", term) ||
                 EF.Functions.ILike(x.Level ?? "", term) || EF.Functions.ILike(x.Room ?? "", term) || EF.Functions.ILike(x.AssetSystem ?? "", term) ||
                 EF.Functions.ILike(x.Alignment ?? "", term) || EF.Functions.ILike(x.CoordinateReferenceSystem ?? "", term))));
        }
        Check.That(f.StationFrom is null || f.StationTo is null || f.StationFrom <= f.StationTo, "stationTo", "issue.stationRangeInvalid");
        if (!string.IsNullOrWhiteSpace(f.Alignment) || f.StationFrom is not null || f.StationTo is not null || !string.IsNullOrWhiteSpace(f.StationUnits))
        {
            var alignment = f.Alignment?.Trim();
            q = q.Where(i => db.IssueLocations.Any(x => x.IssueId == i.Id &&
                (string.IsNullOrWhiteSpace(alignment) || EF.Functions.ILike(x.Alignment ?? "", alignment)) &&
                (string.IsNullOrWhiteSpace(f.StationUnits) || x.StationUnits == f.StationUnits) &&
                (!f.StationFrom.HasValue || (x.EndStation.HasValue && x.EndStation.Value >= f.StationFrom.Value)) &&
                (!f.StationTo.HasValue || (x.StartStation.HasValue && x.StartStation.Value <= f.StationTo.Value))));
        }
        if (!string.IsNullOrWhiteSpace(f.Document))
        {
            var term = $"%{f.Document.Trim()}%";
            q = q.Where(i => db.IssueDocumentReferences.Any(x => x.IssueId == i.Id &&
                (EF.Functions.ILike(x.Kind, term) || EF.Functions.ILike(x.Identifier, term) || EF.Functions.ILike(x.ExternalTopicId ?? "", term) ||
                 EF.Functions.ILike(x.ModelElementGuid ?? "", term))));
        }
        if (!string.IsNullOrWhiteSpace(f.Revision))
        {
            var term = $"%{f.Revision.Trim()}%";
            q = q.Where(i => db.IssueDocumentReferences.Any(x => x.IssueId == i.Id && EF.Functions.ILike(x.Revision, term)));
        }
        foreach (var ind in Http.List(f.Indicator))
            q = ind switch
            {
                "open" => q.Where(i => i.Status == IssueStatus.Open || i.Status == IssueStatus.InProgress),
                "overdue" => q.Where(i => (i.Status == IssueStatus.Open || i.Status == IssueStatus.InProgress) && i.TargetResolutionDate < today),
                _ => q,
            };
        return IssueRootCauses.Filter(q, f.RootCause, rootCauseEnabled);
    }

    /// Default sort (§13.13): severity descending, then target resolution date, then key.
    public static async Task<List<IssueRow>> IssueRows(HubDb db, IQueryable<Issue> q, DateOnly today, string[]? verification = null, bool rootCauseEnabled = false)
    {
        var rows = await q.Select(i => new IssueJoin(
            i.Id, i.ProjectId, i.Key, i.Seq, i.Title, i.Status, i.IssueType, i.RaisedById, i.OwnerId, i.Severity, i.DateRaised, i.TargetResolutionDate, i.Resolution, i.ResolvedDate,
            i.OriginRiskId, i.ProjectDisciplineId, i.RowVersion, i.LastActivityAt,
            db.Users.Where(u => u.Id == i.RaisedById).Select(u => u.DisplayName).FirstOrDefault(),
            db.Users.Where(u => u.Id == i.OwnerId).Select(u => u.IsActive ? u.DisplayName : u.DisplayName + " (Inactive)").FirstOrDefault(),
            db.ProjectDisciplines.Where(x => x.Id == i.ProjectDisciplineId).Select(x => x.Discipline!.Name).FirstOrDefault(),
            db.Risks.Where(x => x.Id == i.OriginRiskId && x.ProjectId == i.ProjectId).Select(x => x.Key).FirstOrDefault(), i.RootCauseCategoryId, i.RootCauseLabelSnapshot)).ToListAsync();
        var issueIds = rows.Select(i => i.Id).ToArray();
        var locations = await db.IssueLocations.AsNoTracking().Where(x => issueIds.Contains(x.IssueId)).ToListAsync();
        var documents = await db.IssueDocumentReferences.AsNoTracking().Where(x => issueIds.Contains(x.IssueId)).ToListAsync();
        var verifications = await db.IssueVerifications.AsNoTracking().Where(x => issueIds.Contains(x.IssueId)).ToListAsync();
        var affectedByIssue = (await db.IssueAffectedDisciplines.AsNoTracking().Where(x => issueIds.Contains(x.IssueId))
            .Select(x => new AffectedName(x.IssueId, x.ProjectDisciplineId, db.ProjectDisciplines.Where(pd => pd.Id == x.ProjectDisciplineId).Select(pd => pd.Discipline!.Name).FirstOrDefault() ?? ""))
            .ToListAsync()).OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToLookup(x => x.IssueId);
        var locationsByIssue = locations.ToLookup(x => x.IssueId);
        var documentsByIssue = documents.ToLookup(x => x.IssueId);
        var verificationsByIssue = verifications.ToLookup(x => x.IssueId);
        var results = rows.OrderBy(i => Registers.Weight(i.Severity)).ThenBy(i => i.TargetResolutionDate ?? DateOnly.MaxValue).ThenBy(i => i.Seq)
            .Select(i =>
            {
                var late = Registers.IssueOverdueDays(i.Status, i.TargetResolutionDate, today);
                var issueLocations = locationsByIssue[i.Id].OrderBy(x => x.CreatedAt).ToList();
                var issueDocuments = documentsByIssue[i.Id].OrderBy(x => x.CreatedAt).ToList();
                var issueVerification = verificationsByIssue[i.Id].OrderByDescending(x => x.IssueRowVersion)
                    .ThenByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).FirstOrDefault();
                var latestReferenceVersion = issueLocations.Select(x => x.IssueRowVersion)
                    .Concat(issueDocuments.Select(x => x.IssueRowVersion)).DefaultIfEmpty(0).Max();
                var verificationStatus = issueVerification is null ? "None" :
                    issueVerification.IssueRowVersion <= latestReferenceVersion ? "Stale" : issueVerification.Status;
                var locationLabels = issueLocations.Select(x => string.Join(" · ", new[] { x.Kind, x.SiteArea, x.Building, x.Level, x.Room, x.AssetSystem, x.Alignment,
                    x.StartStation is { } start ? $"{start}-{x.EndStation} {x.StationUnits}" : null, x.CoordinateX is { } coordinateX ? $"({coordinateX}, {x.CoordinateY}{(x.CoordinateZ is { } z ? $", {z}" : "")}) {x.CoordinateReferenceSystem} {x.CoordinateUnits}" : null }.Where(v => !string.IsNullOrWhiteSpace(v)))).ToArray();
                var affected = affectedByIssue[i.Id].ToList();
                return new IssueRow(i.Id, i.ProjectId, i.Key, i.Title, i.Status, i.IssueType, i.RaisedById, i.RaisedByName, i.OwnerId, i.OwnerName, i.Severity, i.DateRaised, i.TargetResolutionDate,
                    late > 0, late, i.Resolution, i.ResolvedDate, i.OriginRiskId, i.OriginRiskKey, i.ProjectDisciplineId, i.DisciplineName,
                    locationLabels, string.Join("; ", locationLabels),
                    affected.Select(x => x.ProjectDisciplineId).ToArray(), affected.Select(x => x.Name).ToArray(), string.Join("; ", affected.Select(x => x.Name)),
                    string.Join("; ", issueDocuments.Select(x => $"{x.Kind} {x.Identifier} rev {x.Revision} · {(x.ReplacedById is null ? "current reference" : "historical replaced reference")} · manually registered · source {x.SourceSystem ?? "[unknown]"} · ID {x.StableSourceId ?? "[unknown]"} · {(x.IsAvailable ? x.SourceUrl : "[unavailable]")}")),
                    issueDocuments.Select(x => x.Identifier).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray(),
                    issueDocuments.Select(x => x.Revision).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray(),
                    verificationStatus, i.RowVersion, i.LastActivityAt, rootCauseEnabled && i.RootCauseCategoryId is { } cause && i.RootCauseLabelSnapshot is { } label ? new(cause, label) : null);
            });
        return [.. results.Where(x => verification is null || verification.Length == 0 ||
            verification.Contains(x.VerificationStatus, StringComparer.OrdinalIgnoreCase))];
    }

    static async Task<Created<RegisterCreated>> CreateIssue(Guid id, IssueBody body, Access access, HubDb db, TeamService team, SettingsStore store, TimeProvider clock, CurrentUser me, Notifier notify)
    {
        var (p, ctx) = await access.Project(id);
        Access.Demand(Permissions.RaiseRegisterItem(access.Actor, ctx));
        var today = clock.Today(await store.Get(db));
        var i = await Tx.Run(db, async () =>
        {
            var i = await NewIssue(db, access, team, notify, p, body, me.Id, today, clock.GetUtcNow());
            await db.SaveChangesAsync();
            return i;
        });
        return TypedResults.Created($"/api/v1/issues/{i.Id}", new RegisterCreated(i.Id, i.Key, i.RowVersion));
    }

    static async Task<Issue> NewIssue(HubDb db, Access access, TeamService team, Notifier notify, Project p, IssueBody b, Guid me, DateOnly today, DateTimeOffset now)
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
        var type = b.IssueType ?? IssueType.General;
        Check.OneOf(type, IssueType.All, "issueType");
        var locations = (b.Locations ?? []).OfType<IssueLocationBody>().ToArray();
        var documents = (b.Documents ?? []).OfType<IssueDocumentBody>().ToArray();
        // FR-LOC-01: a Coordination issue never exists without a location or drawing/model reference, so it is required at creation.
        Check.That(type == IssueType.General || locations.Length + documents.Length > 0, "reference", "issue.coordination_reference_required");
        var (seq, key) = await Keys.Next(db, p.Id, p.ProjectNumber, "issue");
        var i = new Issue
        {
            ProjectId = p.Id, Seq = seq, Key = key, Title = title, Description = Check.Optional(b.Description, "description", 8000), RaisedById = b.RaisedById ?? me,
            OwnerId = owner, Severity = severity, DateRaised = raised, TargetResolutionDate = b.TargetResolutionDate, ProjectDisciplineId = discipline,
            IssueType = type, StatusChangedAt = now, LastActivityAt = now,
        };
        db.Issues.Add(i);
        // References saved with the new issue carry its first version (0), so any later verification is newer than them.
        foreach (var l in locations) db.Audit.Note(db.IssueLocations.Add(Location(i, l, 0)).Entity, key: i.Key);
        foreach (var d in documents) db.Audit.Note(db.IssueDocumentReferences.Add(Document(i, d, 0)).Entity, key: i.Key);
        await SetAffectedDisciplines(db, notify, p, i, b.AffectedDisciplineIds ?? []);
        foreach (var l in b.Links ?? []) await DecisionEndpoints.NewLink(db, access, p, ItemType.Issue, i.Id, i.Key, l.TargetType, l.TargetId, ItemRelation.Related);
        await team.EnsureMember(p, owner, ProjectRole.TeamMember);
        return i;
    }

    /// FR-LOC-03: replace the issue's affected disciplines; newly added ones must be active disciplines of the same project,
    /// while an already-linked discipline that was later deactivated may stay.
    static async Task SetAffectedDisciplines(HubDb db, Notifier notify, Project p, Issue i, IReadOnlyCollection<Guid> ids)
    {
        var wanted = ids.Distinct().ToHashSet();
        var existing = db.Entry(i).State == EntityState.Added ? [] : await db.IssueAffectedDisciplines.Where(x => x.IssueId == i.Id).ToListAsync();
        var removed = existing.Where(x => !wanted.Contains(x.ProjectDisciplineId)).ToList();
        var added = wanted.Except(existing.Select(x => x.ProjectDisciplineId)).ToList();
        // A relation-only edit still bumps the issue version so concurrent edits conflict.
        if (db.Entry(i).State != EntityState.Added && (removed.Count > 0 || added.Count > 0)) db.Entry(i).Property(x => x.LastActivityAt).IsModified = true;
        foreach (var r in removed) db.Audit.Note(r, key: i.Key);
        db.IssueAffectedDisciplines.RemoveRange(removed);
        foreach (var d in added)
        {
            await Discipline(db, i.ProjectId, d, "affectedDisciplineIds");
            db.Audit.Note(db.IssueAffectedDisciplines.Add(new IssueAffectedDiscipline { ProjectId = i.ProjectId, IssueId = i.Id, ProjectDisciplineId = d }).Entity, key: i.Key);
        }
        if (added.Count == 0) return;
        // FR-LOC-03, FR-MDC-06: each newly affected discipline's lead gets one notice per command, queued in this transaction;
        // the queue applies preferences, current project access and no self-notification.
        var leads = await db.ProjectDisciplines.Where(x => added.Contains(x.Id)).Select(x => x.LeadUserId).ToListAsync();
        await notify.Send(NotificationEvents.IssueAffectedDiscipline, leads, new NotifyItem(p.Id, ItemType.Issue, i.Id, i.Key,
            $"/projects/{p.ProjectNumber}/issues?panel=Issue:{i.Id}", p.ProjectNumber), Text.Get("notify.issue_affected_discipline", i.Key, i.Title));
    }

    /// FR-LOC-01: within the register edit gate, only the PM or the issue owner changes an issue's type.
    static Allow TypeChange(Access access, ProjectContext ctx, Issue i)
    {
        var edit = Permissions.EditRegisterItem(access.Actor, ctx, Facts(i));
        return !edit ? edit : Permissions.IsPM(access.Actor, ctx) || i.OwnerId == access.Me.Id ? Allow.Yes : Allow.No("perm.owner");
    }

    static async Task<bool> HasReference(HubDb db, Guid issueId) =>
        await db.IssueLocations.AnyAsync(x => x.IssueId == issueId) || await db.IssueDocumentReferences.AnyAsync(x => x.IssueId == issueId);

    static async Task<Ok<IssueDetailData>> GetIssue(string id, Access access, HubDb db, SettingsStore store, TimeProvider clock)
    {
        var settings = await store.Get(db);
        await using var snapshot = settings.IsFlagOn("work_screens")
            ? await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead) : null;
        var i = (Guid.TryParse(id, out var g) ? await db.Issues.AsNoTracking().FirstOrDefaultAsync(x => x.Id == g)
            : await db.Issues.AsNoTracking().FirstOrDefaultAsync(x => x.Key == id)) ?? throw ApiException.NotFound();
        var (p, ctx) = await access.Project(i.ProjectId, track: false);
        var row = (await IssueRows(db, db.Issues.AsNoTracking().Where(x => x.Id == i.Id), clock.Today(settings), rootCauseEnabled: settings.IsFlagOn(IssueRootCauses.Flag))).Single();
        var edit = Permissions.EditRegisterItem(access.Actor, ctx, Facts(i));
        var context = await new RecordContextReader(db, p, settings).Read(RecordContextKind.Issue, i.Id);
        return TypedResults.Ok(await WorkScreenResponseMetadata.AttachDetailAsync(new IssueDetailData(row, i.Description, new(p.Id, p.ProjectNumber, p.Name, p.Status),
            i.OriginRiskId is { } ri ? await db.Risks.Where(x => x.Id == ri && x.ProjectId == p.Id).Select(x => new IssueOriginRisk(x.Id, x.Key, x.Title, x.Status)).FirstOrDefaultAsync() : null,
            await DecisionEndpoints.LinkRows(db, p.Id, ItemType.Issue, i.Id, settings.IsFlagOn("screen_patterns")),
            new(Perm(edit, p), Perm(TypeChange(access, ctx, i), p),
                IssueStatus.All.Where(to => Workflow.IssueStep(i.Status, to)).Select(to => new RegisterTransition(to, edit.Ok, edit.Ok ? null : Text.Get(edit.Why!, edit.Arg ?? p.Status))),
                Permissions.Comment(access.Actor, ctx).Ok), context), i, access, db, settings, clock));
    }

    static async Task<Ok<RegisterSaved>> EditIssue(Guid id, JsonElement body, HttpContext http, Access access, HubDb db, TeamService team, SettingsStore store, TimeProvider clock, Notifier notify)
        => await Tx.Notice<Ok<RegisterSaved>>(db, store, async () =>
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
        if (patch.Has("affectedDisciplineIds"))
        {
            var raw = patch.Raw.GetProperty("affectedDisciplineIds");
            Check.That(raw.ValueKind is JsonValueKind.Array or JsonValueKind.Null, "affectedDisciplineIds", "error.validation");
            var ids = raw.ValueKind == JsonValueKind.Null ? [] : raw.EnumerateArray()
                .Select(x => x.ValueKind == JsonValueKind.String && Guid.TryParse(x.GetString(), out var g) ? g : throw ApiException.Invalid("affectedDisciplineIds", "error.not_found")).ToArray();
            await SetAffectedDisciplines(db, notify, p, i, ids);
        }
        if (patch.Has("issueType") && patch.Str("issueType") is var type && type != i.IssueType)
        {
            Check.OneOf(type, IssueType.All, "issueType");
            Access.Demand(TypeChange(access, ctx, i));
            if (!IssueStatus.IsOpen(i.Status)) throw ApiException.Rule("issue_type_closed", "issue.type_closed");
            if (type == IssueType.Coordination && !await HasReference(db, i.Id))
                throw ApiException.Rule("coordination_reference_required", "issue.coordination_reference_required");
            // Dropping the type would let an appointed or rejected verification be bypassed at resolution.
            if (type == IssueType.General && await db.IssueVerifications.AnyAsync(x => x.IssueId == i.Id))
                throw ApiException.Rule("issue_type_verified", "issue.type_has_verification");
            i.IssueType = type!;
        }
        if (patch.Has("raisedById")) { var rb = patch.Id("raisedById") ?? throw ApiException.Invalid("raisedById", "error.required"); await DeliverableEndpoints.ActivePerson(db, rb, "raisedById"); i.RaisedById = rb; }
        if (patch.Has("ownerId"))
        {
            var o = patch.Id("ownerId") ?? throw ApiException.Invalid("ownerId", "error.required");
            await DeliverableEndpoints.ActivePerson(db, o, "ownerId");
            if (i.OwnerId != o)
            {
                var allowSelfReview = (await store.Get(db)).AllowSelfReview;
                var latestVerification = await db.IssueVerifications.Where(x => x.IssueId == i.Id).OrderByDescending(x => x.IssueRowVersion).FirstOrDefaultAsync();
                Check.That(allowSelfReview || latestVerification is null || latestVerification.VerifierId != o,
                    "ownerId", "issue.verifier_independent");
                Check.That(!await db.IssueReferenceImpactAssessments.AnyAsync(a => a.IssueId == i.Id && a.Status == IssueReferenceImpactStatus.Pending && a.VerifierId == o),
                    "ownerId", "issue.verifier_independent");
                foreach (var impact in await db.IssueReferenceImpactAssessments.Where(a => a.IssueId == i.Id && a.Status == IssueReferenceImpactStatus.Pending).ToListAsync())
                {
                    impact.OwnerId = o;
                    impact.OwnerDisposition = null;
                    impact.OwnerReason = null;
                    impact.OwnerDecidedBy = null;
                    impact.OwnerDecidedAt = null;
                    impact.UpdatedAt = clock.GetUtcNow();
                    impact.UpdatedBy = access.Me.Id;
                    db.Audit.Note(impact, reason: "Issue owner reassigned");
                }
            }
            i.OwnerId = o;
            await team.EnsureMember(p, o, ProjectRole.TeamMember);
        }
        i.LastActivityAt = clock.GetUtcNow();
        await db.SaveChangesAsync();
        Http.ETag(http, i);
        return TypedResults.Ok(new RegisterSaved(i.Id, i.RowVersion));
    });

    /// ISS-02: Resolved needs the resolution and records the resolved date; cancelling and reopening need a reason.
    static async Task<Ok<IssueMoved>> MoveIssue(Guid id, IssueMove body, HttpContext http, Access access, HubDb db, SettingsStore store, TimeProvider clock)
    {
        var rootCauseEnabled = (await store.Get(db)).IsFlagOn(IssueRootCauses.Flag);
        if (!rootCauseEnabled && body.RootCauseCategoryId is not null) throw ApiException.NotFound();
        if (rootCauseEnabled && body.ToStatus != IssueStatus.Resolved && body.RootCauseCategoryId is not null)
            throw ApiException.Invalid("rootCauseCategoryId", "issueRootCause.onlyResolution");
        await using var transaction = rootCauseEnabled && body.ToStatus == IssueStatus.Resolved && db.Database.CurrentTransaction is null
            ? await db.Database.BeginTransactionAsync() : null;
        var (i, p, ctx) = await LoadIssue(db, access, id);
        await Http.CheckVersion(db, http, i, body.RowVersion);
        Check.OneOf(body.ToStatus, IssueStatus.All, "toStatus");
        var (from, to) = (i.Status, body.ToStatus);
        if (!Workflow.IssueStep(from, to)) throw ApiException.Rule("illegal_transition", "issue.illegal_transition", null, from, to);
        Access.Demand(Permissions.EditRegisterItem(access.Actor, ctx, Facts(i)));
        if (from == IssueStatus.Resolved && to == IssueStatus.InProgress)
            Check.That(!await db.IssueReferenceImpactAssessments.AnyAsync(a => a.IssueId == i.Id && a.Status == IssueReferenceImpactStatus.Pending), "impact", "issue.reference_impact_pending");
        if (to == IssueStatus.Resolved && i.IssueType == IssueType.Coordination)
        {
            Check.That(await HasReference(db, i.Id), "reference", "issue.coordination_reference_required");
            Check.That(await db.IssueDocumentReferences.Where(x => x.IssueId == i.Id && x.ReplacedById == null).AllAsync(x => x.IsAvailable), "document", "issue.document_unavailable");
            var latestReferenceVersion = await db.IssueLocations.Where(x => x.IssueId == i.Id).Select(x => (int?)x.IssueRowVersion)
                .Concat(db.IssueDocumentReferences.Where(x => x.IssueId == i.Id).Select(x => (int?)x.IssueRowVersion)).MaxAsync() ?? 0;
            var latestVerification = await db.IssueVerifications.Where(x => x.IssueId == i.Id).OrderByDescending(x => x.IssueRowVersion).FirstOrDefaultAsync();
            var current = latestVerification?.Status == IssueVerificationStatus.Verified && latestVerification.IssueRowVersion > latestReferenceVersion &&
                ((await store.Get(db)).AllowSelfReview || latestVerification.VerifierId != i.OwnerId);
            Check.That(current, "verification", "issue.verification_required");
        }
        var today = clock.Today(await store.Get(db));
        var reason = string.IsNullOrWhiteSpace(body.Reason) ? null : body.Reason.Trim();
        switch (to)
        {
            case IssueStatus.Resolved:
                i.Resolution = Check.Required(body.Resolution, "resolution", 8000);
                i.ResolvedDate = body.ResolvedDate ?? today;
                Check.That(i.ResolvedDate <= today, "resolvedDate", "issue.future_resolved");
                var cause = rootCauseEnabled ? await IssueRootCauses.LockSelection(db, body.RootCauseCategoryId) : null;
                i.RootCauseCategoryId = cause?.CategoryId; i.RootCauseLabelSnapshot = cause?.RecordedName;
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
        if (transaction is not null) await transaction.CommitAsync();
        return TypedResults.Ok(new IssueMoved(i.Id, i.Status, i.RowVersion));
    }
    static async Task<Ok<List<IssueLocationRow>>> ListIssueLocations(Guid id, Access access, HubDb db)
    {
        await LoadIssue(db, access, id);
        return TypedResults.Ok(await db.IssueLocations.AsNoTracking().Where(x => x.IssueId == id).OrderBy(x => x.CreatedAt)
            .Select(x => new IssueLocationRow(x.Id, x.Kind, x.SiteArea, x.Building, x.Level, x.Room, x.AssetSystem, x.Alignment, x.StartStation, x.EndStation,
                x.StationUnits, x.CoordinateX, x.CoordinateY, x.CoordinateZ, x.CoordinateReferenceSystem, x.CoordinateUnits, x.RowVersion)).ToListAsync());
    }

    static async Task<Created<RegisterSaved>> AddIssueLocation(Guid id, IssueLocationBody body, HttpContext http, Access access, HubDb db, TimeProvider clock)
    {
        var (issue, _, ctx) = await LoadIssue(db, access, id);
        Access.Demand(Permissions.EditRegisterItem(access.Actor, ctx, Facts(issue)));
        await Http.CheckVersion(db, http, issue, body.RowVersion);
        var row = Location(issue, body, issue.RowVersion + 1);
        issue.LastActivityAt = clock.GetUtcNow(); db.Entry(issue).Property(x => x.LastActivityAt).IsModified = true;
        db.IssueLocations.Add(row); db.Audit.Note(row, key: issue.Key); await db.SaveChangesAsync();
        return TypedResults.Created($"/api/v1/issue-locations/{row.Id}", new RegisterSaved(row.Id, row.RowVersion));
    }

    /// FR-LOC-01/02: a validated location in the project's own station and coordinate convention; nothing is converted or inferred.
    static IssueLocation Location(Issue issue, IssueLocationBody body, int issueRowVersion)
    {
        try { Registers.ValidateIssueLocation(body.Kind, body.Alignment, body.StartStation, body.EndStation, body.StationUnits, body.CoordinateX, body.CoordinateY, body.CoordinateReferenceSystem, body.CoordinateUnits); }
        catch (ArgumentException ex) { throw ApiException.Invalid("location", "issue.location_invalid", ex.Message); }
        if (body.Kind == "SiteArea") Check.That(!string.IsNullOrWhiteSpace(body.SiteArea), "siteArea", "error.required");
        if (body.Kind == "Building") Check.That(!string.IsNullOrWhiteSpace(body.Building), "building", "error.required");
        return new IssueLocation { ProjectId = issue.ProjectId, IssueId = issue.Id, IssueRowVersion = issueRowVersion, Kind = body.Kind, SiteArea = Check.Optional(body.SiteArea, "siteArea", 500),
            Building = Check.Optional(body.Building, "building", 200), Level = Check.Optional(body.Level, "level", 100), Room = Check.Optional(body.Room, "room", 100),
            AssetSystem = Check.Optional(body.AssetSystem, "assetSystem", 300), Alignment = Check.Optional(body.Alignment, "alignment", 300), StartStation = body.StartStation,
            EndStation = body.EndStation, StationUnits = Check.Optional(body.StationUnits, "stationUnits", 40), CoordinateX = body.CoordinateX, CoordinateY = body.CoordinateY,
            CoordinateZ = body.CoordinateZ, CoordinateReferenceSystem = Check.Optional(body.CoordinateReferenceSystem, "coordinateReferenceSystem", 100),
            CoordinateUnits = Check.Optional(body.CoordinateUnits, "coordinateUnits", 40) };
    }

    static async Task<Ok<List<IssueDocumentRow>>> ListIssueDocuments(Guid id, Access access, HubDb db)
    {
        await LoadIssue(db, access, id);
        return TypedResults.Ok(await db.IssueDocumentReferences.AsNoTracking().Where(x => x.IssueId == id).OrderBy(x => x.CreatedAt)
            .Select(x => new IssueDocumentRow(x.Id, x.Kind, x.Identifier, x.Revision, x.SourceUrl, x.SourceSystem, x.StableSourceId, x.CreatedBy, x.CreatedAt, "Manual", x.ExternalTopicId, x.ModelElementGuid, x.ViewpointUrl, x.IsAvailable, x.ReplacedById, x.RowVersion)).ToListAsync());
    }

    static async Task<Created<RegisterSaved>> AddIssueDocument(Guid id, IssueDocumentBody body, HttpContext http, Access access, HubDb db, TimeProvider clock)
    {
        var (issue, _, ctx) = await LoadIssue(db, access, id);
        Access.Demand(Permissions.EditRegisterItem(access.Actor, ctx, Facts(issue)));
        await Http.CheckVersion(db, http, issue, body.RowVersion);
        var row = Document(issue, body, issue.RowVersion + 1);
        if (await db.IssueDocumentReferences.AnyAsync(x => x.IssueId == id && x.Identifier == row.Identifier && x.Revision == row.Revision && x.ReplacedById == null)) throw ApiException.Conflict("duplicate_reference", "error.duplicate");
        issue.LastActivityAt = clock.GetUtcNow(); db.Entry(issue).Property(x => x.LastActivityAt).IsModified = true;
        db.IssueDocumentReferences.Add(row); db.Audit.Note(row, key: issue.Key); await db.SaveChangesAsync();
        return TypedResults.Created($"/api/v1/issue-document-references/{row.Id}", new RegisterSaved(row.Id, row.RowVersion));
    }

    // Recovery retains the old declared reference and invalidates its earlier verification (FR-MDC-03/05).
    static async Task<Coordination.Result> ReplaceIssueDocument(Guid id, Guid documentId, ReplaceIssueDocumentBody body,
        Access access, HubDb db, TimeProvider clock)
    {
        var projectId = await db.Issues.AsNoTracking().Where(i => i.Id == id).Select(i => (Guid?)i.ProjectId).SingleOrDefaultAsync()
            ?? throw ApiException.NotFound();
        return await Coordination.Run(projectId, body.RequestId, new { operation = "issue.document.replace", id, documentId, body },
            access, db, clock, async (project, ctx) =>
            {
                var issue = await db.Issues.SingleAsync(i => i.Id == id && i.ProjectId == project.Id);
                Access.Demand(Permissions.EditRegisterItem(access.Actor, ctx, Facts(issue)));
                Coordination.Version(issue, body.RowVersion);
                Check.That(IssueStatus.IsOpen(issue.Status), "status", "issue.document_replace_open");
                var previous = await db.IssueDocumentReferences.SingleOrDefaultAsync(r => r.Id == documentId && r.IssueId == id)
                    ?? throw ApiException.NotFound();
                Coordination.Version(previous, body.DocumentRowVersion);
                Check.That(previous.ReplacedById is null, "documentId", "issue.document_replaced");
                var reason = Check.Reason(body.Reason);
                var replacement = Document(issue, body.Document, issue.RowVersion + 1);
                Check.That(replacement.IsAvailable, "isAvailable", "issue.document_unavailable");
                Check.That(!await db.IssueDocumentReferences.AnyAsync(r => r.IssueId == id && r.Id != documentId && r.ReplacedById == null &&
                    r.Identifier == replacement.Identifier && r.Revision == replacement.Revision), "document", "error.duplicate");
                db.IssueDocumentReferences.Add(replacement);
                previous.ReplacedById = replacement.Id;
                issue.LastActivityAt = clock.GetUtcNow();
                db.Entry(issue).Property(i => i.LastActivityAt).IsModified = true;
                db.Audit.Note(previous, reason: reason, key: issue.Key);
                db.Audit.Note(replacement, reason: reason, key: issue.Key);
                db.Audit.Note(issue, reason: reason);
                return replacement;
            });
    }

    /// FR-LOC-01: every drawing/model reference names its identifier, declared revision and external source link.
    static IssueDocumentReference Document(Issue issue, IssueDocumentBody body, int issueRowVersion)
    {
        try { Registers.ValidateIssueDocument(body.Kind, body.Identifier, body.Revision, body.SourceUrl); }
        catch (ArgumentException ex) { throw ApiException.Invalid("document", "issue.document_invalid", ex.Message); }
        var sourceUrl = Coordination.Url(body.SourceUrl);
        return new IssueDocumentReference { ProjectId = issue.ProjectId, IssueId = issue.Id, IssueRowVersion = issueRowVersion, Kind = body.Kind,
            Identifier = Check.Required(body.Identifier, "identifier", 300), Revision = Check.Required(body.Revision, "revision", 100), SourceUrl = sourceUrl,
            SourceSystem = Check.Optional(body.SourceSystem, "sourceSystem", 200) ?? new Uri(sourceUrl).Host,
            StableSourceId = Check.Optional(body.StableSourceId, "stableSourceId", 300),
            ExternalTopicId = Check.Optional(body.ExternalTopicId, "externalTopicId", 300), ModelElementGuid = Check.Optional(body.ModelElementGuid, "modelElementGuid", 300),
            ViewpointUrl = string.IsNullOrWhiteSpace(body.ViewpointUrl) ? null : Coordination.Url(body.ViewpointUrl), IsAvailable = body.IsAvailable };
    }

    static async Task<Ok<List<IssueVerificationRow>>> ListIssueVerification(Guid id, Access access, HubDb db)
    {
        await LoadIssue(db, access, id);
        return TypedResults.Ok(await db.IssueVerifications.AsNoTracking().Where(x => x.IssueId == id).OrderByDescending(x => x.IssueRowVersion)
            .Select(x => new IssueVerificationRow(x.Id, x.VerifierId, x.Status, x.EvidenceUrl, x.Note, x.VerifiedAt, x.RowVersion)).ToListAsync());
    }

    static async Task<Created<RegisterSaved>> AddIssueVerification(Guid id, IssueVerificationBody body, HttpContext http, Access access, HubDb db, TimeProvider clock, Notifier notify, SettingsStore store)
    {
        var (issue, project, ctx) = await LoadIssue(db, access, id);
        await Http.CheckVersion(db, http, issue, body.RowVersion);
        Check.OneOf(body.Status, IssueVerificationStatus.All, "status");
        Check.That((await store.Get(db)).AllowSelfReview || body.VerifierId != issue.OwnerId && body.VerifierId != issue.CreatedBy && body.VerifierId != issue.RaisedById,
            "verifierId", "issue.verifier_independent");
        await Coordination.Person(db, project, body.VerifierId, "verifierId");
        if (body.Status == IssueVerificationStatus.Proposed)
        {
            Access.Demand(Permissions.Writable(access.Actor, ctx));
            Access.Demand(Permissions.IsPM(access.Actor, ctx) || issue.CreatedBy == access.Me.Id && ctx.IsMember // the creator, while on the team
                ? Allow.Yes : Allow.No("perm.owner"));
            Check.Reason(body.Note);
        }
        else
        {
            Access.Demand(Permissions.Writable(access.Actor, ctx));
            Check.That(body.VerifierId == access.Me.Id, "verifierId", "issue.verifier_must_submit");
            var latest = await db.IssueVerifications.Where(x => x.IssueId == id)
                .OrderByDescending(x => x.IssueRowVersion).Select(x => new LatestVerification(x.Status, x.VerifierId)).FirstOrDefaultAsync();
            Check.That(latest?.Status == IssueVerificationStatus.Proposed && latest.VerifierId == access.Me.Id,
                "verifierId", "issue.verifier_not_appointed");
        }
        var evidence = string.IsNullOrWhiteSpace(body.EvidenceUrl) ? null : Coordination.Url(body.EvidenceUrl);
        if (body.Status == IssueVerificationStatus.Verified) Check.That(evidence is not null, "evidenceUrl", "error.required");
        var row = await Tx.Run(db, async () =>
        {
            var now = clock.GetUtcNow();
            issue.LastActivityAt = now; db.Entry(issue).Property(x => x.LastActivityAt).IsModified = true;
            var created = new IssueVerification { ProjectId = project.Id, IssueId = id, IssueRowVersion = issue.RowVersion + 1, VerifierId = body.VerifierId, Status = body.Status, EvidenceUrl = evidence,
                Note = Check.Optional(body.Note, "note", 4000), VerifiedAt = body.Status == IssueVerificationStatus.Verified ? now : null,
                CreatedAt = now, UpdatedAt = now, CreatedBy = access.Me.Id, UpdatedBy = access.Me.Id };
            db.IssueVerifications.Add(created); db.Audit.Note(created, reason: created.Note, key: issue.Key);
            var item = new NotifyItem(project.Id, ItemType.Issue, issue.Id, issue.Key,
                $"/projects/{project.ProjectNumber}/issues?panel=Issue:{issue.Id}", project.ProjectNumber);
            if (body.Status == IssueVerificationStatus.Proposed)
                await notify.Send(NotificationEvents.IssueVerifierAssigned, body.VerifierId, item,
                    Text.Get("notify.issue_verification_requested", issue.Key));
            else
                await notify.Send(NotificationEvents.IssueVerificationOutcome,
                    new Guid?[] { issue.OwnerId, issue.CreatedBy, project.ProjectManagerId }, item,
                    Text.Get("notify.issue_verification_outcome", issue.Key, body.Status));
            await db.SaveChangesAsync();
            return created;
        });
        return TypedResults.Created($"/api/v1/issue-verifications/{row.Id}", new RegisterSaved(row.Id, row.RowVersion));
    }
}
