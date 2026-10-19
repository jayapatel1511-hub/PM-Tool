using System.Text;
using System.Text.RegularExpressions;
using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualBasic.FileIO;

namespace Hub.Api.Features;

public static class ProjectImportEndpoints
{
    public sealed record Body(string Csv);
    public sealed record ImportRow(int Row, string ProjectNumber, string Name, string Client, string Office, string ProjectManagerEmail,
        string Classification, string? Error, Guid? ClientId = null, Guid? OfficeId = null, Guid? ProjectManagerId = null);

    public static void Map(RouteGroupBuilder api)
    {
        api.MapPost("/projects/import-preview", async (Body body, Access access, HubDb db, SettingsStore store) => Summary(await Parse(body, access, db, store)));
        api.MapPost("/projects/import", async (Body body, Access access, HubDb db, SettingsStore store, TimeProvider clock, TeamService team) =>
            await Tx.Run(db, async () => {
                Access.Demand(Permissions.Administer(access.Actor));
                await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(470013)");
                var rows = await Parse(body, access, db, store);
                Check.That(rows.All(r => r.Classification != "Error"), "csv", "projectImport.errors");
                var settings = await store.Get(db);
                db.Audit.Source = "Import";
                foreach (var row in rows.Where(r => r.Classification == "Create")) {
                    var project = new Project { ProjectNumber = row.ProjectNumber, Name = row.Name, ClientId = row.ClientId!.Value,
                        OfficeId = row.OfficeId!.Value, ProjectManagerId = row.ProjectManagerId!.Value, Status = ProjectStatus.Setup,
                        AllowViewerComments = settings.ViewerCommentsDefault, StatusChangedAt = clock.GetUtcNow() };
                    db.Projects.Add(project);
                    await team.Add(project, project.ProjectManagerId, [ProjectRole.PM], null, notifyPerson: false);
                }
                db.LogEvent("ProjectImport", null, "Imported", "creation", changes: new[] { new { Field = "Created", Old = (int?)null, New = rows.Count(r => r.Classification == "Create") }, new { Field = "Exists", Old = (int?)null, New = rows.Count(r => r.Classification == "Exists") } });
                await db.SaveChangesAsync();
                return Summary(rows);
            }));
    }

    static object Summary(ImportRow[] rows) => new { Rows = rows, Creates = rows.Count(r => r.Classification == "Create"),
        Exists = rows.Count(r => r.Classification == "Exists"), Errors = rows.Count(r => r.Classification == "Error"), CanApply = rows.All(r => r.Classification != "Error") };

    static async Task<ImportRow[]> Parse(Body body, Access access, HubDb db, SettingsStore store)
    {
        Access.Demand(Permissions.Administer(access.Actor));
        var settings = await store.Get(db);
        if (!settings.IsFlagOn("project_import")) throw ApiException.NotFound();
        Check.That(body.Csv != null && Encoding.UTF8.GetByteCount(body.Csv) <= 256 * 1024, "csv", "error.too_long", 262144);
        using var parser = new TextFieldParser(new StringReader(body.Csv!.TrimStart('\uFEFF'))) { TextFieldType = FieldType.Delimited, HasFieldsEnclosedInQuotes = true, TrimWhiteSpace = true };
        parser.SetDelimiters(",");
        string[]? Fields() { try { return parser.ReadFields(); } catch (MalformedLineException) { throw ApiException.Invalid("csv", "projectImport.invalidCsv"); } }
        var headers = Fields();
        string[] expected = ["project number", "name", "client", "office", "project manager email"];
        Check.That(headers != null && headers.Select(h => h.ToLowerInvariant()).SequenceEqual(expected), "csv", "projectImport.headers");
        var clients = await db.Clients.AsNoTracking().Where(c => c.IsActive).ToListAsync();
        var offices = await db.Offices.AsNoTracking().Where(o => o.IsActive).ToListAsync();
        var pms = await db.Users.AsNoTracking().Where(u => u.IsActive && db.UserRoles.Any(r => r.UserId == u.Id && (r.Role == SystemRole.Admin || r.Role == SystemRole.ProjectManager))).ToListAsync();
        var existing = (await db.Projects.AsNoTracking().Select(p => p.ProjectNumber).ToListAsync()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase); var rows = new List<ImportRow>();
        while (!parser.EndOfData) {
            Check.That(rows.Count < 1000, "csv", "projectImport.rows");
            var cells = Fields() ?? []; var row = rows.Count + 2;
            if (cells.Length != 5) { rows.Add(new(row, "", "", "", "", "", "Error", Text.Get("projectImport.columns"))); continue; }
            var (number, name, clientName, officeName, email) = (cells[0], cells[1], cells[2], cells[3], cells[4]);
            bool Eq(string? a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
            var client = clients.Where(c => Eq(c.Name, clientName) || Eq(c.ShortName, clientName)).ToArray();
            var office = offices.Where(o => Eq(o.Name, officeName) || Eq(o.Code, officeName)).ToArray();
            var pm = pms.SingleOrDefault(u => Eq(u.Email, email));
            var error = !seen.Add(number) ? "projectImport.duplicate" : number.Length > 32 || !Regex.IsMatch(number, settings.ProjectNumberFormat) ? "project.number_format"
                : name.Length is < 1 or > 200 ? "projectImport.invalidName" : client.Length != 1 ? "projectImport.client" : office.Length != 1 ? "projectImport.office" : pm == null ? "projectImport.pm" : null;
            rows.Add(new(row, number, name, clientName, officeName, email, error != null ? "Error" : existing.Contains(number) ? "Exists" : "Create", error == null ? null : Text.Get(error),
                client.Length == 1 ? client[0].Id : null, office.Length == 1 ? office[0].Id : null, pm?.Id));
        }
        return rows.ToArray();
    }
}
