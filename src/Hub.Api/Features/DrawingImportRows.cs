using System.Globalization;
using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Features;

internal sealed record DrawingImportPreparedRow(DeliverableEndpoints.CreatePlan? Plan, DrawingImportPreviewRow Preview);
internal sealed record DrawingImportPreparedBatch(DrawingImportPreview Preview, DrawingImportPreparedRow[] Rows, string? Reason);

/// Read-only mapping/validation. All source rows are checked before any caller may apply a canonical create plan.
internal static class DrawingImportRows
{
    static readonly HashSet<string> Headers = new(StringComparer.Ordinal) {
        "title", "discipline", "type", "client drawing number", "owner email", "reviewer email", "milestone",
        "start date", "due date", "revision", "requires review", "priority", "description", "source row key"
    };
    sealed class DrawingImportRowWork(DrawingImportRawRecord source)
    {
        public DrawingImportRawRecord Source = source;
        public string? SourceKey;
        public DeliverableEndpoints.CreatePlan? Plan;
        public DrawingImportValues? Values;
        public readonly List<DrawingImportMessage> Errors = [];
        public readonly List<DrawingImportMessage> Warnings = [];
    }

    internal static async Task<DrawingImportPreparedBatch> Prepare(Guid projectId, DrawingImportPreviewBody body, Access access, HubDb db, SettingsStore store)
    {
        Http.RequireFlag(await store.Get(db), DeliverableEndpoints.RegisterFlag);
        var (project, context) = await access.Project(projectId, false);
        if (!access.Actor.IsActive) throw ApiException.Forbidden("perm.inactive");
        Access.Demand(Permissions.Writable(access.Actor, context));
        var disciplines = await db.ProjectDisciplines.AsNoTracking().Include(row => row.Discipline).Where(row => row.ProjectId == projectId).ToArrayAsync();
        if (!disciplines.Any(row => row.IsActive && Permissions.CreateDeliverable(access.Actor, context, row.Id).Ok))
            throw ApiException.Forbidden("perm.pm_or_dl");
        var types = await db.DeliverableTypes.AsNoTracking().Where(row => row.IsActive).ToArrayAsync();
        var people = await db.Users.AsNoTracking().Where(row => row.IsActive).ToArrayAsync();
        var milestones = await db.Milestones.AsNoTracking().Where(row => row.ProjectId == projectId).ToArrayAsync();
        var members = await db.ProjectMembers.AsNoTracking().Where(row => row.ProjectId == projectId).ToArrayAsync();

        var sourceReference = string.IsNullOrWhiteSpace(body.SourceReference) ? null : body.SourceReference;
        Check.That(sourceReference is null || sourceReference.Length <= 200 && !sourceReference.Any(char.IsControl) && ValidText(sourceReference), "sourceReference", "error.invalid");
        var reason = Check.Optional(body.Reason, "reason", 1000);
        Check.That(body.Worksheet is null || body.Worksheet.Length <= 128 && !body.Worksheet.Any(char.IsControl), "worksheet", "error.invalid");
        var format = body.Format switch { "csv" => DrawingImportFormatKind.Csv, "xlsx" => DrawingImportFormatKind.Xlsx,
            _ => throw ApiException.Invalid("format", "drawingImport.format") };
        var byteLimit = format == DrawingImportFormatKind.Csv ? 256 * 1024 : 2 * 1024 * 1024;
        Check.That(body.ContentBase64 is { Length: > 0 } && body.ContentBase64.Length <= ((byteLimit + 2) / 3) * 4,
            "contentBase64", "drawingImport.fileLimit");
        Check.That(!body.ContentBase64.Any(char.IsWhiteSpace), "contentBase64", "drawingImport.base64");
        var buffer = new byte[byteLimit];
        Check.That(Convert.TryFromBase64String(body.ContentBase64, buffer, out var written), "contentBase64", "drawingImport.base64");
        var bytes = buffer[..written];
        var zipSignature = bytes.Length >= 4 && bytes[0] == 'P' && bytes[1] == 'K' && bytes[2] == 3 && bytes[3] == 4;
        var oleSignature = bytes.Length >= 4 && bytes[0] == 0xD0 && bytes[1] == 0xCF && bytes[2] == 0x11 && bytes[3] == 0xE0;
        Check.That(format == DrawingImportFormatKind.Xlsx ? zipSignature : !zipSignature && !oleSignature, "format", "drawingImport.format");
        var parsed = DrawingImportFormat.Parse(new(format, bytes, body.Worksheet));
        var diagnostics = parsed.Diagnostics.ToList();
        void FileError(string code, string field, int? row = null, int? column = null) => diagnostics.Add(new(code, DrawingImportDiagnosticSeverity.Error,
            Text.Get($"drawingImport.{code}", field), parsed.SelectedSheet, row, column));
        foreach (var header in parsed.Headers.Where(header => !Headers.Contains(header.Normalized)))
        {
            var sourceRow = new string(header.Cell.Reference.Where(char.IsDigit).ToArray());
            FileError("unknownColumn", header.Raw, int.TryParse(sourceRow, out var at) ? at : null, header.Column);
        }
        if (parsed.Headers.All(header => header.Normalized != "title")) FileError("titleColumn", "title");
        try { ProjectEndpoints.CorrectionReason(project, reason); }
        catch (ApiException exception) { diagnostics.Add(new("reason", DrawingImportDiagnosticSeverity.Error,
            exception.Errors?.Values.SelectMany(value => value).FirstOrDefault() ?? exception.Message, parsed.SelectedSheet)); }
        // Duplicate headers remain file errors; choosing the first here only permits a complete diagnostic preview, never commit.
        var columns = parsed.Headers.GroupBy(header => header.Normalized, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.First().Column, StringComparer.Ordinal);
        var seenSourceKeys = new HashSet<string>(StringComparer.Ordinal); var work = new List<DrawingImportRowWork>();
        foreach (var record in parsed.Records)
        {
            var row = new DrawingImportRowWork(record); work.Add(row);
            foreach (var diagnostic in parsed.Diagnostics.Where(value => value.SourceRow == record.SourceRow))
                (diagnostic.Severity == DrawingImportDiagnosticSeverity.Error ? row.Errors : row.Warnings)
                    .Add(new(diagnostic.Code, diagnostic.Column?.ToString(CultureInfo.InvariantCulture) ?? "row", diagnostic.Message));
            DrawingImportRawCell? Cell(string header) => columns.TryGetValue(header, out var column) ? record.Cells.FirstOrDefault(cell => cell.Column == column) : null;
            void Error(string code, string field) => row.Errors.Add(new(code, field, Text.Get($"drawingImport.{code}", field)));
            string? Literal(string header, int max = 200, bool plain = false)
            {
                var cell = Cell(header);
                if (cell is null || cell.Kind is DrawingImportCellKind.Missing or DrawingImportCellKind.Blank) return null;
                if (cell.Kind != DrawingImportCellKind.Text) { Error("literalText", header); return null; }
                var value = string.IsNullOrWhiteSpace(cell.Text) ? null : cell.Text.Trim();
                if (value is null) return null;
                if (value.Length > max) Error("textLimit", header);
                if (!ValidText(value) || plain && value.Any(char.IsControl)) Error("textEncoding", header);
                return value;
            }
            DateOnly? Date(string header)
            {
                var cell = Cell(header);
                if (cell is null || cell.Kind is DrawingImportCellKind.Missing or DrawingImportCellKind.Blank || cell.Kind == DrawingImportCellKind.Text && string.IsNullOrWhiteSpace(cell.Text)) return null;
                if (cell.Kind == DrawingImportCellKind.Date && cell.CalendarValue is { } date && date.TimeOfDay == TimeSpan.Zero) return DateOnly.FromDateTime(date);
                if (cell.Kind == DrawingImportCellKind.Text && DateOnly.TryParseExact(cell.Text?.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var iso)) return iso;
                Error("dateOnly", header); return null;
            }
            bool? Boolean(string header)
            {
                var cell = Cell(header);
                if (cell is null || cell.Kind is DrawingImportCellKind.Missing or DrawingImportCellKind.Blank || cell.Kind == DrawingImportCellKind.Text && string.IsNullOrWhiteSpace(cell.Text)) return null;
                if (cell.Kind == DrawingImportCellKind.Boolean) return cell.Boolean;
                if (cell.Kind == DrawingImportCellKind.Text && bool.TryParse(cell.Text?.Trim(), out var value)) return value;
                Error("boolean", header); return null;
            }
            var title = Literal("title") ?? "";
            var disciplineName = Literal("discipline", plain: true); var typeName = Literal("type", plain: true);
            var disciplineMatches = disciplines.Where(value => value.IsActive && (disciplineName is null ? value.Id == body.Defaults?.ProjectDisciplineId
                : Eq(value.Discipline?.Name, disciplineName) || Eq(value.Discipline?.Code, disciplineName))).ToArray();
            var typeMatches = types.Where(value => typeName is null ? value.Id == body.Defaults?.DeliverableTypeId : Eq(value.Name, typeName)).ToArray();
            var discipline = disciplineMatches.Length == 1 ? disciplineMatches[0] : null;
            var type = typeMatches.Length == 1 ? typeMatches[0] : null;
            if (discipline is null) Error("discipline", "discipline");
            if (type is null) Error("type", "type");
            Guid? Person(string field)
            {
                var email = Literal(field, 320, plain: true);
                if (email is null) return null;
                var matches = people.Where(person => Eq(person.Email, email)).ToArray();
                if (matches.Length == 1) return matches[0].Id;
                Error("person", field); return null;
            }
            var owner = Person("owner email"); var reviewer = Person("reviewer email");
            var milestoneKey = Literal("milestone", plain: true);
            Milestone[] milestoneMatches = milestoneKey is null ? [] : milestones.Where(value => string.Equals(value.Key, milestoneKey, StringComparison.OrdinalIgnoreCase)).ToArray();
            var milestone = milestoneMatches.Length == 1 ? milestoneMatches[0] : null;
            if (milestoneKey is not null && milestone is null) Error("milestone", "milestone");
            row.SourceKey = Literal("source row key", 120, plain: true);
            if (row.SourceKey is not null && !seenSourceKeys.Add(row.SourceKey)) Error("sourceKeyDuplicate", "source row key");
            var bodyRow = new DeliverableEndpoints.CreateBody(title, discipline?.Id ?? Guid.Empty, type?.Id ?? Guid.Empty,
                Literal("description", 8000), owner, reviewer, milestone?.Id, Date("start date"), Date("due date"),
                Literal("priority", 30, plain: true), Literal("revision", 50), Boolean("requires review"), FromTaskId: null,
                ClientDrawingNumber: Literal("client drawing number", 120, plain: true));
            try
            {
                row.Plan = await DeliverableEndpoints.PrepareCreate(projectId, bodyRow, access, db, store);
                // EnsureMember would refuse these assignments. Check both before CanCommit, without making memberships.
                foreach (var assignment in new[] { (Field: "owner email", Id: (Guid?)row.Plan.OwnerId), (Field: "reviewer email", Id: bodyRow.ReviewerId) })
                    if (assignment.Id is { } user && project.Visibility == Visibility.Restricted && !Permissions.ManageTeam(access.Actor, context).Ok
                        && !members.Any(member => member.UserId == user && member.RemovedAt == null)) Error("restrictedMembership", assignment.Field);
                var plan = row.Plan; var ownerPerson = people.Single(person => person.Id == plan.OwnerId);
                row.Values = new(plan.Body.Name, plan.Discipline.Id, plan.Discipline.Discipline?.Name ?? discipline?.Discipline?.Name ?? "",
                    plan.Body.DeliverableTypeId, type!.Name, plan.OwnerId, ownerPerson.DisplayName, plan.Body.ReviewerId,
                    plan.Body.ReviewerId is { } reviewId ? people.Single(person => person.Id == reviewId).DisplayName : null,
                    plan.Body.MilestoneId, plan.Milestone?.Key, plan.Body.StartDate, plan.DueDate, plan.Body.Priority ?? Priority.Medium,
                    plan.Body.Revision, plan.Body.RequiresReview ?? true, plan.ClientDrawingNumber, plan.Body.Description,
                    bodyRow.OwnerId is null, bodyRow.DueDate is null && plan.Milestone?.Date is not null);
                if (DeliverableNumberingEndpoints.Matches(plan.ClientDrawingNumber, project.ClientDrawingNumberPattern) == false)
                    row.Warnings.Add(new("numberPattern", "client drawing number", Text.Get("drawingImport.numberPattern")));
                if (plan.Milestone?.Date is { } target && plan.DueDate is { } due && due > target)
                    row.Warnings.Add(new("afterMilestone", "due date", Text.Get("drawingImport.afterMilestone")));
            }
            catch (ApiException exception) when (exception.Status is 400 or 403 or 404 or 422)
            {
                if (exception.Errors is { } fields)
                    foreach (var (field, messages) in fields) foreach (var message in messages) row.Errors.Add(new(exception.Code, field, message));
                else row.Errors.Add(new(exception.Code, "row", exception.Message));
            }
        }

        var names = work.Where(row => row.Plan is not null).Select(row => row.Plan!.Body.Name.ToLowerInvariant()).Distinct().ToArray();
        var numbers = work.Where(row => row.Plan?.ClientDrawingNumber is not null).Select(row => row.Plan!.ClientDrawingNumber!.ToLowerInvariant()).Distinct().ToArray();
        var matchingNames = (await db.Deliverables.AsNoTracking().Where(row => row.ProjectId == projectId && names.Contains(row.Name.ToLower()))
            .Select(row => row.Name.ToLower()).Distinct().ToArrayAsync()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var matchingNumbers = (await db.Deliverables.AsNoTracking().Where(row => row.ProjectId == projectId && row.ClientDrawingNumber != null && numbers.Contains(row.ClientDrawingNumber.ToLower()))
            .Select(row => row.ClientDrawingNumber!.ToLower()).Distinct().ToArrayAsync()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var repeatedNames = work.Where(row => row.Plan is not null).GroupBy(row => row.Plan!.Body.Name, StringComparer.OrdinalIgnoreCase).Where(group => group.Count() > 1).Select(group => group.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var repeatedNumbers = work.Where(row => row.Plan?.ClientDrawingNumber is not null).GroupBy(row => row.Plan!.ClientDrawingNumber!, StringComparer.OrdinalIgnoreCase).Where(group => group.Count() > 1).Select(group => group.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var row in work)
            if (row.Plan is { } plan && (matchingNames.Contains(plan.Body.Name) || repeatedNames.Contains(plan.Body.Name)
                || plan.ClientDrawingNumber is { } number && (matchingNumbers.Contains(number) || repeatedNumbers.Contains(number))))
                row.Warnings.Add(new("possibleDuplicate", "row", Text.Get("drawingImport.possibleDuplicate")));

        var rows = work.Select(row => new DrawingImportPreparedRow(row.Plan,
            new(row.Source.SourceRow, row.Source.SourceRowInferred, row.Source.Hidden, row.Errors.Count == 0 && row.Plan is not null ? "Create" : "Error",
                row.SourceKey, row.Errors.Count == 0 ? row.Values : null, row.Errors.Distinct().ToArray(), row.Warnings.Distinct().ToArray()))).ToArray();
        var sourceRows = rows.Select(row => row.Preview.SourceRow).ToHashSet();
        var errors = rows.Count(row => row.Preview.Classification == "Error")
            + diagnostics.Count(value => value.Severity == DrawingImportDiagnosticSeverity.Error && (value.SourceRow is null || !sourceRows.Contains(value.SourceRow.Value)));
        var creates = rows.Count(row => row.Preview.Classification == "Create");
        var canCommit = parsed.Complete && errors == 0 && creates > 0;
        object? ReferenceFacts(DrawingImportPreparedRow row)
        {
            if (row.Plan is not { } plan) return null;
            var pd = disciplines.Single(value => value.Id == plan.Discipline.Id);
            var type = types.Single(value => value.Id == plan.Body.DeliverableTypeId);
            var selectedPeople = new[] { (Guid?)plan.OwnerId, plan.Body.ReviewerId }.Where(value => value is not null).Select(value => value!.Value).Distinct().Order().ToArray();
            return new {
                discipline = new { pd.Id, pd.RowVersion, pd.IsActive, pd.LeadUserId, pd.DisciplineId, pd.Discipline!.Name, pd.Discipline.Code, referenceVersion = pd.Discipline.RowVersion },
                type = new { type.Id, type.RowVersion, type.IsActive, type.Name },
                people = people.Where(person => selectedPeople.Contains(person.Id)).OrderBy(person => person.Id).Select(person => new { person.Id, person.RowVersion, person.IsActive, person.Email, person.DisplayName }).ToArray(),
                membership = members.Where(member => selectedPeople.Contains(member.UserId)).OrderBy(member => member.Id).Select(member => new { member.Id, member.UserId, member.RowVersion, member.RemovedAt, roles = member.Roles.Order().ToArray() }).ToArray(),
                milestone = plan.Milestone is { } milestone ? new { milestone.Id, milestone.RowVersion, milestone.Key, milestone.Date } : null,
                row.Preview.Values, warnings = row.Preview.Warnings.Select(warning => warning.Code).Order().ToArray()
            };
        }
        var fingerprint = canCommit ? Coordination.Hash(new {
            contentHash = parsed.ContentSha256, body.Format, sheet = parsed.SelectedSheet, body.Worksheet, sourceReference, body.Defaults, reason,
            project = new { project.Id, project.RowVersion, project.ProjectNumber, project.Status, project.Visibility, project.ProjectManagerId, project.ClientDrawingNumberPattern, project.RevisionConvention },
            actor = new { access.Actor.Id, access.Actor.IsActive, roles = access.Actor.Roles.Order().ToArray(), memberRoles = context.MemberRoles.Order().ToArray(), leads = context.LeadDisciplineIds.Order().ToArray(), context.PrimaryDisciplineId },
            rows = rows.Select(ReferenceFacts).ToArray()
        }) : null;
        var preview = new DrawingImportPreview(body.Format, sourceReference, parsed.SelectedSheet, parsed.ContentSha256, fingerprint,
            project.RowVersion, ProjectStatus.NeedsCorrectionReason(project.Status), parsed.Complete, parsed.Sheets.ToArray(), diagnostics.ToArray(), rows.Select(row => row.Preview).ToArray(), creates, errors, canCommit);
        return new(preview, rows, reason);
    }

    static bool Eq(string? left, string right) => string.Equals(left?.Trim(), right, StringComparison.OrdinalIgnoreCase);
    static bool ValidText(string value)
    {
        for (var index = 0; index < value.Length; index++)
        {
            if (value[index] == '\0' || char.IsLowSurrogate(value[index])) return false;
            if (char.IsHighSurrogate(value[index]) && (++index >= value.Length || !char.IsLowSurrogate(value[index]))) return false;
        }
        return true;
    }
}
