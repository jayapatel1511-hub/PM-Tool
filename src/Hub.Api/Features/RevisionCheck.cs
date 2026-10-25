using System.Text.Json;
using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Hub.Api.Features;

public static class RevisionCheckEndpoints
{
    public sealed record ConventionBody(int RowVersion, string? Preset, string[]? Codes = null);
    public sealed record IssueMarks(Guid Id, string? Revision, string[] Marks);
    public sealed record DeliverableMarks(Guid Id, string[] Marks, IssueMarks[] Issues);
    public sealed record ReadResponse(RevisionConvention? Convention, DeliverableMarks[] Deliverables);
    public sealed record WriteResponse(Guid Id, int RowVersion, RevisionConvention? Convention);
    public static void Map(RouteGroupBuilder api)
    {
        var group = api.MapGroup("/projects/{projectId:guid}/revision-check").RequireFlag("revision_check");
        group.MapGet("", async (Guid projectId, Access access, HubDb db) =>
        {
            var (p, _) = await access.Project(projectId, false);
            return TypedResults.Ok(new ReadResponse(Parse(p.RevisionConvention), await Marks(db, p)));
        });
        group.MapPut("", async (Guid projectId, ConventionBody body, HttpContext http, Access access, HubDb db) => await Tx.Run(db, async () =>
        {
            await Coordination.Lock(db, projectId);
            var (p, ctx) = await access.Project(projectId);
            Access.Demand(Permissions.Writable(access.Actor, ctx, pmOnly: true));
            await Http.CheckVersion(db, http, p, body.RowVersion);
            var convention = body.Preset == null ? null : new RevisionConvention(body.Preset, body.Codes);
            Check.That(convention == null || convention.Valid, "preset", "error.invalid");
            p.RevisionConvention = convention == null ? null : JsonSerializer.Serialize(convention, JsonOpts.Web);
            db.Audit.Note(p, action: "RevisionConventionChanged");
            await db.SaveChangesAsync();
            return TypedResults.Ok(new WriteResponse(p.Id, p.RowVersion, convention));
        }));
    }
    static string[] Text(RevisionConvention convention, string[] codes) => [.. codes.Select(code => code == RevisionConvention.NonStandard
        ? Hub.Api.Text.Get("revision.non_standard", convention.Pattern) : Hub.Api.Text.Get("revision.out_of_sequence"))];
    public static RevisionConvention? Parse(string? text) => text == null ? null : JsonSerializer.Deserialize<RevisionConvention>(text, JsonOpts.Web);
    public static async Task<DeliverableMarks[]> Marks(HubDb db, Project project)
    {
        var convention = Parse(project.RevisionConvention);
        if (convention == null) return [];
        var deliverables = await db.Deliverables.AsNoTracking().Where(d => d.ProjectId == project.Id).Select(d => new { d.Id, d.Revision }).ToListAsync();
        var issues = await db.DeliverableIssues.AsNoTracking().Where(i => i.ProjectId == project.Id).OrderBy(i => i.CreatedAt).ThenBy(i => i.Id)
            .Select(i => new { i.Id, i.DeliverableId, i.Revision }).ToListAsync();
        return deliverables.Select(d =>
        {
            string? prior = null;
            var history = issues.Where(i => i.DeliverableId == d.Id).Select(i => { var mark = new IssueMarks(i.Id, i.Revision, Text(convention, convention.Marks(i.Revision, prior))); prior = i.Revision; return mark; }).ToArray();
            var current = history.LastOrDefault();
            var marks = current is not null && current.Revision == d.Revision ? current.Marks
                : current is null && string.IsNullOrWhiteSpace(d.Revision) ? []
                : Text(convention, convention.Marks(d.Revision, prior));
            return new DeliverableMarks(d.Id, marks, history);
        }).ToArray();
    }
}

public sealed class RevisionRecheckJob : IJob
{
    public string Name => "revision-convention-recheck";
    public bool IsDue(DateTimeOffset now, DateTimeOffset? last, OrgSettings s) => s.IsFlagOn("revision_check") && Schedule.Every(TimeSpan.FromMinutes(1), now, last);
    public async Task<object?> Run(IServiceProvider sp, CancellationToken ct)
    {
        var db = sp.GetRequiredService<HubDb>(); var count = 0;
        var projects = await db.Projects.AsNoTracking().ToListAsync(ct);
        foreach (var project in projects)
        {
            var changed = await db.ActivityLog.Where(a => a.ProjectId == project.Id && a.ItemType == ItemType.Project && a.Action == "RevisionConventionChanged")
                .MaxAsync(a => (DateTimeOffset?)a.OccurredAt, ct);
            var checkedAt = await db.ActivityLog.Where(a => a.ProjectId == project.Id && a.Action == "RevisionConventionRechecked")
                .MaxAsync(a => (DateTimeOffset?)a.OccurredAt, ct);
            if (changed == null || checkedAt >= changed) continue;
            var marks = await RevisionCheckEndpoints.Marks(db, project);
            db.LogEvent(ItemType.Project, project.Id, "RevisionConventionRechecked", "admin", project.Id, project.ProjectNumber, project.Name,
                new[] { new { field = "RevisionWarnings", old = (object?)null, @new = marks.Sum(d => d.Marks.Length) + marks.Sum(d => d.Issues.Sum(i => i.Marks.Length)) } });
            await db.SaveChangesAsync(ct); count++;
        }
        return new { Projects = count };
    }
}
