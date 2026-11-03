using System.Text.Json.Serialization;
using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Features;

/// S43: a membership note never widens the PM-only role/primary-discipline command.
public static class MemberResponsibilityEndpoints
{
    public sealed record ResponsibilityRead(string? Note, bool CanEdit, string? EditReason);
    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    public sealed record SetResponsibilityBody([property: JsonRequired] string? ResponsibilityNote, int? RowVersion, string? Reason);
    public sealed record ResponsibilitySaved(Guid Id, int RowVersion, string? ResponsibilityNote);

    public static ResponsibilityRead Describe(Actor actor, ProjectContext context, string? note, bool leadsActivePrimaryDiscipline)
    {
        var permission = Permissions.SetMemberResponsibility(actor, context, leadsActivePrimaryDiscipline);
        return new(note, permission.Ok, permission.Why is { } why ? Text.Get(why, permission.Arg ?? "") : null);
    }

    public static void Map(RouteGroupBuilder api)
    {
        api.MapPatch("/projects/{projectId:guid}/members/{memberId:guid}/responsibility", Set).RequireFlag("people_screens");
    }

    static async Task<Ok<ResponsibilitySaved>> Set(Guid projectId, Guid memberId, SetResponsibilityBody body, HttpContext http,
        Access access, HubDb db, SettingsStore store)
    {
        Http.RequireFlag(await store.Get(db), "people_screens");
        return await Tx.Run(db, async () =>
        {
            var (project, context) = await access.Project(projectId, track: false);
            var member = await db.ProjectMembers.SingleOrDefaultAsync(row => row.Id == memberId && row.ProjectId == projectId && row.RemovedAt == null) ?? throw ApiException.NotFound();
            // Never substitute home discipline, another project, a former/inactive lead or the caller's own primary.
            var ownActivePrimary = member.PrimaryDisciplineId is { } primary && await db.ProjectDisciplines.AsNoTracking()
                .AnyAsync(discipline => discipline.Id == primary && discipline.ProjectId == projectId && discipline.IsActive && discipline.LeadUserId == access.Me.Id);
            Access.Demand(Permissions.SetMemberResponsibility(access.Actor, context, ownActivePrimary));
            await Http.CheckVersion(db, http, member, body.RowVersion);
            var note = Check.Optional(body.ResponsibilityNote, "responsibilityNote", 80);
            Check.That(note == null || !note.Any(char.IsControl), "responsibilityNote", "error.invalid");
            var reason = Check.Optional(body.Reason, "reason", 1000);
            ProjectEndpoints.CorrectionReason(project, reason);
            member.ResponsibilityNote = note;
            db.Audit.Note(member, reason: reason);
            await db.SaveChangesAsync();
            return TypedResults.Ok(new ResponsibilitySaved(member.Id, member.RowVersion, member.ResponsibilityNote));
        });
    }
}
