using System.Text.Json.Nodes;
using Hub.Api.Data;
using Hub.Api.Features;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

/// Packet 031 notices: consumers whose impact assessment becomes pending and owners of conflicting entries.
/// Queued in the command transaction, never to the actor, never after a refused, stale or replayed command,
/// and only to recipients who still hold project access (FR-BAS-03, FR-BAS-05, FR-BAS-06, FR-MDC-02, FR-MDC-03, FR-MDC-06).
[Collection("api")]
public sealed class DesignBasisNotificationTests(HubFactory f)
{
    readonly TestData data = new(f);
    async Task<JsonNode> Post(string who, string path, object body, int status = 200) => await (await f.As(who).Post(path, body)).Json(status);
    int Version<T>(Guid id) where T : Audited => f.Db(db => db.Set<T>().AsNoTracking().Single(x => x.Id == id).RowVersion);
    int Notices(Guid itemId, string eventType, Guid userId) => f.Db(db => db.Notifications
        .Where(n => n.ItemId == itemId && n.EventType == eventType && n.UserId == userId).Sum(n => n.Count));

    static readonly DesignBasisEndpoints.VersionInput Input = new("Bridge / Pier 1", "Allowable bearing pressure", 100, "kPa",
        "Geotechnical report", "GEO-N-1", "https://example.test/geotech", "A", new DateOnly(2026, 10, 5), null);

    async Task AddMember(Guid projectId, Guid userId) => await f.DbAsync(async db =>
    {
        db.ProjectMembers.Add(new ProjectMember { ProjectId = projectId, UserId = userId, Roles = [ProjectRole.TeamMember],
            PrimaryDisciplineId = data.ProjectDiscipline(projectId, "Civil") });
        return await db.SaveChangesAsync();
    });

    async Task<(Guid Entry, Guid Version)> Confirmed(Project project, string title, Guid owner, DesignBasisEndpoints.VersionInput input,
        Guid? inspected = null)
    {
        var root = $"/api/v1/projects/{project.Id}/design-basis";
        var entry = (await Post(TestData.Pm, root, new DesignBasisEndpoints.CreateBody(Guid.NewGuid(), BasisKind.Criterion, title, owner,
            data.ProjectDiscipline(project.Id, "Civil"), data.User(TestData.Marc), input, null, inspected))).G("id");
        var version = f.Db(db => db.DesignBasisVersions.Single(v => v.EntryId == entry).Id);
        await Post(TestData.Marc, $"{root}/{entry}/versions/{version}/confirm", new DesignBasisEndpoints.ConfirmBody(Guid.NewGuid(),
            Version<DesignBasisEntry>(entry), Version<DesignBasisVersion>(version), "Source checked"));
        return (entry, version);
    }

    async Task Use(Project project, Guid entry, Guid version, string who, Guid assignee)
    {
        var task = await data.NewTask(project.Id, extra: new { assigneeId = assignee });
        await Post(who, $"/api/v1/projects/{project.Id}/design-basis/{entry}/uses",
            new DesignBasisEndpoints.UseBody(Guid.NewGuid(), version, "Task", task.G("id"), "Foundation sizing"));
    }

    [Fact]
    public async Task Replacement_notifies_each_consumer_once_and_skips_actor_stale_retry_and_removed_member()
    {
        var project = await data.Project();
        var alex = data.User(TestData.Alex);
        var jill = data.User(TestData.Jill);
        var marc = data.User(TestData.Marc);
        await AddMember(project.Id, jill);
        var root = $"/api/v1/projects/{project.Id}/design-basis";
        var (entry, a) = await Confirmed(project, "Bearing pressure", alex, Input);
        await Use(project, entry, a, TestData.Alex, alex);
        await Use(project, entry, a, TestData.Alex, alex);
        await Use(project, entry, a, TestData.Jill, jill);
        var b = (await Post(TestData.Marc, $"{root}/{entry}/propose", new DesignBasisEndpoints.ProposeBody(Guid.NewGuid(),
            Version<DesignBasisEntry>(entry), Version<DesignBasisVersion>(a), Input with { NumericValue = 125, DeclaredRevision = "B" },
            "New geotechnical report"))).G("id");
        var confirmPath = $"{root}/{entry}/versions/{b}/confirm";
        var confirm = new DesignBasisEndpoints.ConfirmBody(Guid.NewGuid(), Version<DesignBasisEntry>(entry),
            Version<DesignBasisVersion>(b), "Revised report checked");
        await Post(TestData.Marc, confirmPath, confirm with { RequestId = Guid.NewGuid(), VersionRowVersion = -1 }, 409);
        Assert.Equal(0, Notices(entry, NotificationEvents.BasisImpactPending, alex));

        await f.DbAsync(async db =>
        {
            (await db.Projects.SingleAsync(p => p.Id == project.Id)).Visibility = Visibility.Restricted;
            (await db.ProjectMembers.SingleAsync(m => m.ProjectId == project.Id && m.UserId == jill)).RemovedAt = f.Clock.GetUtcNow();
            return await db.SaveChangesAsync();
        });
        await Post(TestData.Marc, confirmPath, confirm);
        await Post(TestData.Marc, confirmPath, confirm);
        Assert.Equal(2, f.Db(db => db.BasisImpactAssessments.Count(i => i.NewVersionId == b && i.OwnerId == alex)));
        Assert.Equal(1, Notices(entry, NotificationEvents.BasisImpactPending, alex));
        Assert.Equal(0, Notices(entry, NotificationEvents.BasisImpactPending, jill));
        Assert.Equal(0, Notices(entry, NotificationEvents.BasisImpactPending, marc));
        Assert.Single(f.Db(db => db.BasisImpactAssessments.Where(i => i.NewVersionId == b && i.OwnerId == jill).ToList()));
    }

    [Fact]
    public async Task Withdrawal_notifies_consumer_but_refused_withdrawal_does_not()
    {
        var project = await data.Project();
        var alex = data.User(TestData.Alex);
        var (entry, version) = await Confirmed(project, "Withdrawal notice criterion", alex, Input);
        await Use(project, entry, version, TestData.Alex, alex);
        var path = $"/api/v1/projects/{project.Id}/design-basis/{entry}/versions/{version}/withdraw";
        var withdraw = new DesignBasisEndpoints.WithdrawBody(Guid.NewGuid(), Version<DesignBasisEntry>(entry),
            Version<DesignBasisVersion>(version), "Geotechnical basis withdrawn");
        await Post(TestData.Alex, path, withdraw, 403);
        Assert.Equal(0, Notices(entry, NotificationEvents.BasisImpactPending, alex));
        await Post(TestData.Marc, path, withdraw with { RequestId = Guid.NewGuid() });
        Assert.Equal(1, Notices(entry, NotificationEvents.BasisImpactPending, alex));
    }

    [Fact]
    public async Task Reopened_source_decision_notifies_consumer_once()
    {
        var project = await data.Project();
        var alex = data.User(TestData.Alex);
        var decisionId = (await (await f.As(TestData.Pm).Post($"/api/v1/projects/{project.Id}/decisions", new
        {
            subject = "Confirm bearing basis", description = "Basis source decision", ownerUserId = alex,
            requiredByDate = "2026-10-01", impactLevel = "Medium", impactDescription = "Design depends on this decision"
        })).Json(201)).G("id");
        var (entry, version) = await Confirmed(project, "Decision notice criterion", alex, Input with { DecisionId = decisionId });
        await Use(project, entry, version, TestData.Alex, alex);
        var transition = $"/api/v1/decisions/{decisionId}/transition";
        int DecisionVersion() => f.Db(db => db.Decisions.Single(d => d.Id == decisionId).RowVersion);
        await (await f.As(TestData.Pm).Post(transition, new { toStatus = "Decided", decisionText = "Use the basis", decisionDate = "2026-09-14", rowVersion = DecisionVersion() })).Json(200);
        var decided = DecisionVersion();
        await (await f.As(TestData.Pm).Post(transition, new { toStatus = "Pending", reason = "Revalidate source", rowVersion = decided })).Json(200);
        Assert.Equal(1, Notices(decisionId, NotificationEvents.BasisImpactPending, alex));
        Assert.Equal(0, Notices(decisionId, NotificationEvents.BasisImpactPending, data.User(TestData.Pm)));
        Assert.False((await f.As(TestData.Pm).Post(transition, new { toStatus = "Pending", reason = "Revalidate source", rowVersion = decided })).IsSuccessStatusCode);
        Assert.Equal(1, Notices(decisionId, NotificationEvents.BasisImpactPending, alex));
    }

    [Fact]
    public async Task Conflicting_confirmation_notifies_both_entry_owners_except_actor()
    {
        var project = await data.Project();
        var alex = data.User(TestData.Alex);
        var jill = data.User(TestData.Jill);
        await AddMember(project.Id, jill);
        var (first, _) = await Confirmed(project, "Conflict criterion", alex, Input);
        var (second, _) = await Confirmed(project, "Conflict criterion", jill, Input with { NumericValue = 150, StableSourceId = "GEO-N-2" }, first);
        Assert.Single(f.Db(db => db.BasisConflicts.Where(c => c.ProjectId == project.Id).ToList()));
        Assert.Equal(1, Notices(second, NotificationEvents.BasisConflictRaised, alex));
        Assert.Equal(1, Notices(second, NotificationEvents.BasisConflictRaised, jill));
        Assert.Equal(0, Notices(second, NotificationEvents.BasisConflictRaised, data.User(TestData.Marc)));
        Assert.Equal(0, Notices(first, NotificationEvents.BasisConflictRaised, alex));
    }
}
