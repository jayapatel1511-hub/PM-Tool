using System.Text.Json;
using System.Text.Json.Nodes;
using Hub.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

/// The displayed owner is polymorphic; an external-party identifier must never authorize a User link.
public sealed class MyWorkPersonOwnerTests : IDisposable
{
    readonly HubFactory f = new();
    readonly TestData d;
    public MyWorkPersonOwnerTests() { d = new(f); f.Clock.SetDate(2026, 11, 1); }
    public void Dispose() => f.Dispose();

    async Task Flag(string name, bool value) =>
        (await f.As(TestData.Admin).Put($"/api/v1/admin/settings/flag.{name}", new { value })).EnsureSuccessStatusCode();

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task Owner_kind_uses_the_canonical_discriminator_and_either_consuming_flag_without_writes(bool people, bool work)
    {
        await Flag("my_work_follow_up", true);
        await Flag("people_screens", people);
        await Flag("work_screens", work);
        var project = await d.Project();
        var userId = d.User(TestData.Alex);
        const string externalName = "Synthetic external recipient";
        // Independent tables can contain the same GUID. This fixture deliberately exposes unsafe GUID/name inference.
        await f.DbAsync(async db =>
        {
            db.ExternalParties.Add(new ExternalParty { Id = userId, ProjectId = project.Id, Name = externalName });
            return await db.SaveChangesAsync();
        });
        var pm = f.As(TestData.Pm);
        async Task<JsonNode> Create(bool external) => await pm.Post($"/api/v1/projects/{project.Id}/decisions", new
        {
            subject = external ? "Synthetic external decision" : "Synthetic internal decision",
            description = "Typed owner proof", ownerUserId = external ? (Guid?)null : userId,
            ownerExternalPartyId = external ? userId : (Guid?)null,
            requiredByDate = "2026-11-03", impactLevel = "Medium", impactDescription = "Synthetic test"
        }).Result.Json(201);
        var internalDecision = await Create(false);
        var externalDecision = await Create(true);
        foreach (var decision in new[] { internalDecision, externalDecision })
            (await pm.Post($"/api/v1/items/Decision/{decision.G("id")}/watchers", new { userId = d.User(TestData.Pm) })).EnsureSuccessStatusCode();

        async Task<string> Snapshot() => await f.DbAsync(async db => JsonSerializer.Serialize(new
        {
            Decisions = await db.Decisions.AsNoTracking().Where(x => x.ProjectId == project.Id).OrderBy(x => x.Id)
                .Select(x => new { x.Id, x.OwnerUserId, x.OwnerExternalPartyId, x.RowVersion, x.Status }).ToArrayAsync(),
            Activity = await db.ActivityLog.CountAsync(),
            Occurrences = await db.WorkAssignmentOccurrences.CountAsync(),
            Acknowledgements = await db.WorkSeenAcknowledgements.CountAsync(),
            Commands = await db.WorkSeenCommandReceipts.CountAsync(),
            Visits = await db.UserSettings.AsNoTracking().OrderBy(x => x.UserId).Select(x => new
                { x.UserId, x.MyWorkFirstVisitAt, x.MyWorkPreviousVisitAt, x.MyWorkLastVisitAt, x.MyWorkLastMarkAllAt, x.MyWorkSeenRevision }).ToArrayAsync()
        }));
        var before = await Snapshot();
        foreach (var route in new[] { "watching", "requested" })
        {
            var result = await pm.GetAsync($"/api/v1/me/work/{route}").Result.Json();
            JsonNode Row(Guid id) => result["items"]!.AsArray().Single(x => x!.G("id") == id)!;
            var internalRow = Row(internalDecision.G("id"));
            var externalRow = Row(externalDecision.G("id"));
            Assert.Equal(userId, internalRow.G("ownerId"));
            Assert.Equal(userId, externalRow.G("ownerId")); // Existing OwnerId values and canonical decisions stay unchanged.
            Assert.Equal("Alex Chen", internalRow.S("ownerName"));
            if (people || work)
            {
                Assert.Equal("User", internalRow.S("ownerKind"));
                Assert.Equal("ExternalParty", externalRow.S("ownerKind"));
                Assert.Equal(externalName, externalRow.S("ownerName"));
            }
            else
            {
                Assert.False(internalRow.AsObject().ContainsKey("ownerKind"));
                Assert.False(externalRow.AsObject().ContainsKey("ownerKind"));
            }
        }
        Assert.Equal(before, await Snapshot());
    }
}
