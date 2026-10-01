using System.Net;
using System.Text.Json.Nodes;
using Hub.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

[Collection("api")]
public sealed class LocationIssueTests(HubFactory f)
{
    readonly TestData d = new(f);

    async Task<JsonNode> Issue(Guid projectId, string owner = TestData.Alex) =>
        await f.As(TestData.Alex).Post($"/api/v1/projects/{projectId}/issues", new
        {
            title = "Synthetic utility clash", description = "Coordination issue for location workflow", severity = "High",
            ownerId = d.User(owner), projectDisciplineId = d.ProjectDiscipline(projectId, "Civil")
        }).Result.Json(201);
    Task<int> IssueVersion(Guid id) => f.DbAsync(db => db.Issues.Where(x => x.Id == id).Select(x => x.RowVersion).FirstAsync());

    [Fact]
    public async Task Verifier_notifications_recheck_restricted_project_access_at_enqueue_and_delivery()
    {
        var project = await d.Project();
        var issue = await Issue(project.Id);
        var issueId = issue.G("id");
        var verifierId = d.User(TestData.Marc);
        var ownerId = d.User(TestData.Alex);
        (await f.As(TestData.Marc).Put($"/api/v1/me/preferences/events/{Hub.Domain.NotificationEvents.IssueVerifierAssigned}",
            new { app = true, email = true })).EnsureSuccessStatusCode();
        await f.As(TestData.Pm).Post($"/api/v1/issues/{issueId}/verification", new
        {
            verifierId, status = "Proposed", note = "Appoint independent verifier", rowVersion = await IssueVersion(issueId)
        }).Result.Json(201);
        var queuedId = f.Db(db => db.Emails.Single(e => e.UserId == verifierId &&
            e.DedupKey == $"{Hub.Domain.NotificationEvents.IssueVerifierAssigned}:{issueId}:{verifierId}").Id);
        Assert.Equal(new[] { project.Id }, f.Db(db => db.Emails.Single(e => e.Id == queuedId).RequiredProjectIds));
        await f.DbAsync(async db =>
        {
            var p = await db.Projects.SingleAsync(p => p.Id == project.Id);
            p.Visibility = Hub.Domain.Visibility.Restricted;
            var owner = await db.ProjectMembers.SingleAsync(m => m.ProjectId == project.Id && m.UserId == ownerId);
            owner.RemovedAt = f.Clock.GetUtcNow();
            return await db.SaveChangesAsync();
        });
        Assert.False(await f.DbAsync(db => EmailProjectAccess.Allowed(db, ownerId, [project.Id])));
        await f.As(TestData.Marc).Post($"/api/v1/issues/{issueId}/verification", new
        {
            verifierId, status = "Verified", evidenceUrl = "https://review.example.test/verify/access", rowVersion = await IssueVersion(issueId)
        }).Result.Json(201);
        Assert.False(f.Db(db => db.Notifications.Any(n => n.ItemId == issueId && n.UserId == ownerId &&
            n.EventType == Hub.Domain.NotificationEvents.IssueVerificationOutcome)));
        await f.DbAsync(async db =>
        {
            var member = await db.ProjectMembers.SingleAsync(m => m.ProjectId == project.Id && m.UserId == verifierId);
            member.RemovedAt = f.Clock.GetUtcNow();
            var queued = await db.Emails.SingleAsync(e => e.Id == queuedId);
            queued.CreatedAt = DateTimeOffset.MinValue; // put this fixture first in the shared worker batch
            return await db.SaveChangesAsync();
        });
        Assert.False(await f.DbAsync(db => EmailProjectAccess.Allowed(db, verifierId, [project.Id])));
        await f.RunJob<EmailJob>();
        var email = f.Db(db => db.Emails.Single(e => e.Id == queuedId));
        Assert.Null(email.SentAt);
        Assert.NotNull(email.SuppressedAt);
    }

    [Fact]
    public async Task Location_document_and_independent_verification_are_scoped_and_gate_resolution()
    {
        var p = await d.Project();
        var issue = await Issue(p.Id);
        var id = issue.G("id");

        var badStation = await f.As(TestData.Alex).Post($"/api/v1/issues/{id}/locations", new
        {
            kind = "Alignment", alignment = "Road-A", startStation = 20, endStation = 10, stationUnits = "m"
        });
        Assert.Equal(HttpStatusCode.BadRequest, badStation.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await f.As(TestData.Alex).Post($"/api/v1/issues/{id}/locations", new
        {
            kind = "Building", rowVersion = await IssueVersion(id)
        })).StatusCode);

        var location = await f.As(TestData.Alex).Post($"/api/v1/issues/{id}/locations", new
        {
            kind = "Alignment", alignment = "Road-A", startStation = 10, endStation = 20, stationUnits = "m", rowVersion = await IssueVersion(id)
        }).Result.Json(201);
        Assert.NotEqual(Guid.Empty, location.G("id"));
        var overlap = await (await f.As(TestData.Pm).GetAsync($"/api/v1/projects/{p.Id}/issues?alignment=Road-A&stationFrom=15&stationTo=25&stationUnits=m")).Json();
        Assert.Contains(overlap.AsArray(), row => row!.G("id") == id);
        var outside = await (await f.As(TestData.Pm).GetAsync($"/api/v1/projects/{p.Id}/issues?alignment=Road-A&stationFrom=21&stationTo=30&stationUnits=m")).Json();
        Assert.DoesNotContain(outside.AsArray(), row => row!.G("id") == id);
        Assert.Equal(HttpStatusCode.BadRequest, (await f.As(TestData.Pm).GetAsync($"/api/v1/projects/{p.Id}/issues?stationFrom=30&stationTo=20")).StatusCode);
        var stationExport = System.Text.Encoding.UTF8.GetString(await (await f.As(TestData.Pm).GetAsync($"/api/v1/projects/{p.Id}/issues/export?format=csv&alignment=Road-A&stationFrom=15&stationTo=25&stationUnits=m")).Content.ReadAsByteArrayAsync());
        Assert.Contains(issue.S("key"), stationExport);
        var staleLocation = await f.As(TestData.Alex).Post($"/api/v1/issues/{id}/locations", new
        {
            kind = "SiteArea", siteArea = "North", rowVersion = 0
        });
        Assert.Equal(HttpStatusCode.Conflict, staleLocation.StatusCode);

        var missingCrs = await f.As(TestData.Alex).Post($"/api/v1/issues/{id}/locations", new
        {
            kind = "Coordinate", coordinateX = 1, coordinateY = 2, coordinateUnits = "m", rowVersion = await IssueVersion(id)
        });
        Assert.Equal(HttpStatusCode.BadRequest, missingCrs.StatusCode);

        var document = await f.As(TestData.Alex).Post($"/api/v1/issues/{id}/documents", new
        {
            kind = "Drawing", identifier = "C-101", revision = "A", sourceUrl = "https://review.example.test/c-101", isAvailable = true, rowVersion = await IssueVersion(id)
        }).Result.Json(201);
        Assert.NotEqual(Guid.Empty, document.G("id"));
        Assert.Equal(HttpStatusCode.Conflict, (await f.As(TestData.Alex).Post($"/api/v1/issues/{id}/documents", new
        {
            kind = "Drawing", identifier = "C-101", revision = "A", sourceUrl = "https://review.example.test/c-101", isAvailable = true, rowVersion = await IssueVersion(id)
        })).StatusCode);

        var unresolved = await f.As(TestData.Alex).Post($"/api/v1/issues/{id}/transition", new
        {
            toStatus = "Resolved", resolution = "Utility alignment coordinated", rowVersion = await f.DbAsync(db => db.Issues.Where(x => x.Id == id).Select(x => x.RowVersion).FirstAsync())
        });
        Assert.Equal(HttpStatusCode.BadRequest, unresolved.StatusCode);

        var namedByPm = await f.As(TestData.Pm).Post($"/api/v1/issues/{id}/verification", new
        {
            verifierId = d.User(TestData.Marc), status = "Verified", evidenceUrl = "https://review.example.test/verify/1", note = "Independent synthetic review", rowVersion = await IssueVersion(id)
        });
        Assert.Equal(HttpStatusCode.BadRequest, namedByPm.StatusCode);
        await f.As(TestData.Pm).Post($"/api/v1/issues/{id}/verification", new
        {
            verifierId = d.User(TestData.Marc), status = "Proposed", note = "Appoint Marc as independent verifier", rowVersion = await IssueVersion(id)
        }).Result.Json(201);
        Assert.Equal(1, f.Db(db => db.Notifications.Where(n => n.ItemId == id &&
            n.EventType == Hub.Domain.NotificationEvents.IssueVerifierAssigned && n.UserId == d.User(TestData.Marc)).Sum(n => n.Count)));
        var verifyVersion = await IssueVersion(id);
        var attempts = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => f.As(TestData.Marc).Post($"/api/v1/issues/{id}/verification", new
        {
            verifierId = d.User(TestData.Marc), status = "Verified", evidenceUrl = "https://review.example.test/verify/1", note = "Independent synthetic review", rowVersion = verifyVersion
        })));
        Assert.Equal(new[] { HttpStatusCode.Created, HttpStatusCode.Conflict }, attempts.Select(a => a.StatusCode).OrderBy(x => x));
        var verify = await attempts.Single(a => a.StatusCode == HttpStatusCode.Created).Json(201);
        Assert.NotEqual(Guid.Empty, verify.G("id"));
        Assert.Equal(1, f.Db(db => db.Notifications.Where(n => n.ItemId == id &&
            n.EventType == Hub.Domain.NotificationEvents.IssueVerificationOutcome && n.UserId == d.User(TestData.Alex)).Sum(n => n.Count)));
        Assert.Equal(HttpStatusCode.BadRequest, (await f.As(TestData.Marc).Post($"/api/v1/issues/{id}/verification", new
        {
            verifierId = d.User(TestData.Marc), status = "Verified", evidenceUrl = "https://review.example.test/verify/repeat", rowVersion = await IssueVersion(id)
        })).StatusCode);
        Assert.Equal(1, f.Db(db => db.Notifications.Where(n => n.ItemId == id &&
            n.EventType == Hub.Domain.NotificationEvents.IssueVerificationOutcome && n.UserId == d.User(TestData.Alex)).Sum(n => n.Count)));

        await f.As(TestData.Alex).Post($"/api/v1/issues/{id}/documents", new
        {
            kind = "Drawing", identifier = "C-101", revision = "B", sourceUrl = "https://review.example.test/c-101-b", isAvailable = true, rowVersion = await IssueVersion(id)
        }).Result.Json(201);
        var staleVerification = await f.As(TestData.Alex).Post($"/api/v1/issues/{id}/transition", new
        {
            toStatus = "Resolved", resolution = "Should require a fresh verification", rowVersion = await f.DbAsync(db => db.Issues.Where(x => x.Id == id).Select(x => x.RowVersion).FirstAsync())
        });
        Assert.Equal(HttpStatusCode.BadRequest, staleVerification.StatusCode);
        var staleRows = await (await f.As(TestData.Pm).GetAsync($"/api/v1/projects/{p.Id}/issues?verification=Stale")).Json();
        Assert.Contains(staleRows.AsArray(), row => row!.G("id") == id && row.S("verificationStatus") == "Stale");
        var groupedSource = Assert.Single(staleRows.AsArray().Where(row => row!.G("id") == id))!;
        Assert.Equal(new[] { "C-101" }, groupedSource["documentIdentifiers"]!.AsArray().Select(value => value!.GetValue<string>()));
        Assert.Equal(new[] { "A", "B" }, groupedSource["documentRevisions"]!.AsArray().Select(value => value!.GetValue<string>()));
        var verifiedRows = await (await f.As(TestData.Pm).GetAsync($"/api/v1/projects/{p.Id}/issues?verification=Verified")).Json();
        Assert.DoesNotContain(verifiedRows.AsArray(), row => row!.G("id") == id);
        var staleExport = System.Text.Encoding.UTF8.GetString(await (await f.As(TestData.Pm).GetAsync($"/api/v1/projects/{p.Id}/issues/export?format=csv&verification=Stale")).Content.ReadAsByteArrayAsync());
        Assert.Contains(issue.S("key"), staleExport);
        var verifiedExport = System.Text.Encoding.UTF8.GetString(await (await f.As(TestData.Pm).GetAsync($"/api/v1/projects/{p.Id}/issues/export?format=csv&verification=Verified")).Content.ReadAsByteArrayAsync());
        Assert.DoesNotContain(issue.S("key"), verifiedExport);
        await f.As(TestData.Pm).Post($"/api/v1/issues/{id}/verification", new
        {
            verifierId = d.User(TestData.Marc), status = "Proposed", note = "Recheck drawing revision B", rowVersion = await IssueVersion(id)
        }).Result.Json(201);
        await f.As(TestData.Marc).Post($"/api/v1/issues/{id}/verification", new
        {
            verifierId = d.User(TestData.Marc), status = "Verified", evidenceUrl = "https://review.example.test/verify/1b", rowVersion = await IssueVersion(id)
        }).Result.Json(201);

        var resolved = await f.As(TestData.Alex).Post($"/api/v1/issues/{id}/transition", new
        {
            toStatus = "Resolved", resolution = "Utility alignment coordinated", rowVersion = await f.DbAsync(db => db.Issues.Where(x => x.Id == id).Select(x => x.RowVersion).FirstAsync())
        });
        Assert.Equal(HttpStatusCode.OK, resolved.StatusCode);
        Assert.Single((await f.As(TestData.Alex).GetAsync($"/api/v1/issues/{id}/locations").Result.Json()).AsArray());
        Assert.Equal(2, (await f.As(TestData.Rita).GetAsync($"/api/v1/issues/{id}/documents").Result.Json()).AsArray().Count);
        var export = System.Text.Encoding.UTF8.GetString(await (await f.As(TestData.Pm).GetAsync($"/api/v1/projects/{p.Id}/issues/export?format=csv")).Content.ReadAsByteArrayAsync());
        Assert.Contains("Location", export);
        Assert.Contains("Road-A", export);
        Assert.Contains("C-101 rev B", export);
        Assert.Contains("Verified", export);
        foreach (var filter in new[] { "location=Road-A", "document=C-101", "revision=B", "verification=Verified" })
        {
            var filtered = await (await f.As(TestData.Pm).GetAsync($"/api/v1/projects/{p.Id}/issues?{filter}")).Json();
            Assert.Single(filtered.AsArray());
            Assert.Equal(id, filtered.AsArray()[0]!.G("id"));
        }
        var revisionExport = System.Text.Encoding.UTF8.GetString(await (await f.As(TestData.Pm).GetAsync($"/api/v1/projects/{p.Id}/issues/export?format=csv&revision=B")).Content.ReadAsByteArrayAsync());
        Assert.Contains("C-101 rev B", revisionExport);
        Assert.Contains("C-101 rev A", revisionExport); // historical references remain inspectable on the filtered issue

        var unavailable = await Issue(p.Id);
        var unavailableId = unavailable.G("id");
        var noneVerification = await (await f.As(TestData.Pm).GetAsync($"/api/v1/projects/{p.Id}/issues?verification=None")).Json();
        Assert.Contains(noneVerification.AsArray(), row => row!.G("id") == unavailableId);
        await f.As(TestData.Alex).Post($"/api/v1/issues/{unavailableId}/documents", new
        {
            kind = "Drawing", identifier = "C-202", revision = "B", sourceUrl = "https://review.example.test/c-202", isAvailable = false, rowVersion = await IssueVersion(unavailableId)
        }).Result.Json(201);
        await f.As(TestData.Pm).Post($"/api/v1/issues/{unavailableId}/verification", new
        {
            verifierId = d.User(TestData.Marc), status = "Proposed", note = "Appoint Marc for unavailable evidence", rowVersion = await IssueVersion(unavailableId)
        }).Result.Json(201);
        await f.As(TestData.Marc).Post($"/api/v1/issues/{unavailableId}/verification", new
        {
            verifierId = d.User(TestData.Marc), status = "Verified", evidenceUrl = "https://review.example.test/verify/2", rowVersion = await IssueVersion(unavailableId)
        }).Result.Json(201);
        var unavailableResolution = await f.As(TestData.Alex).Post($"/api/v1/issues/{unavailableId}/transition", new
        {
            toStatus = "Resolved", resolution = "Should remain blocked", rowVersion = await f.DbAsync(db => db.Issues.Where(x => x.Id == unavailableId).Select(x => x.RowVersion).FirstAsync())
        });
        Assert.Equal(HttpStatusCode.BadRequest, unavailableResolution.StatusCode);
    }

    [Fact]
    public async Task Appointed_non_owner_verifier_decides_and_latest_rejection_blocks_resolution()
    {
        var p = await d.Project();
        var issue = await Issue(p.Id);
        var id = issue.G("id");
        async Task<int> Version() => await f.DbAsync(db => db.Issues.Where(x => x.Id == id).Select(x => x.RowVersion).FirstAsync());
        await f.As(TestData.Alex).Post($"/api/v1/issues/{id}/locations", new
        {
            kind = "SiteArea", siteArea = "Synthetic test area", rowVersion = await Version()
        }).Result.Json(201);
        await f.As(TestData.Alex).Post($"/api/v1/issues/{id}/verification", new
        {
            verifierId = d.User(TestData.Omar), status = "Proposed", note = "Appoint other discipline lead", rowVersion = await Version()
        }).Result.Json(201);
        await f.As(TestData.Omar).Post($"/api/v1/issues/{id}/verification", new
        {
            verifierId = d.User(TestData.Omar), status = "Verified", evidenceUrl = "https://review.example.test/verify/om-1", rowVersion = await Version()
        }).Result.Json(201);
        await f.As(TestData.Alex).Post($"/api/v1/issues/{id}/verification", new
        {
            verifierId = d.User(TestData.Omar), status = "Proposed", note = "Recheck proposed resolution", rowVersion = await Version()
        }).Result.Json(201);
        await f.As(TestData.Omar).Post($"/api/v1/issues/{id}/verification", new
        {
            verifierId = d.User(TestData.Omar), status = "Rejected", note = "Evidence no longer sufficient", rowVersion = await Version()
        }).Result.Json(201);
        var blocked = await f.As(TestData.Alex).Post($"/api/v1/issues/{id}/transition", new
        {
            toStatus = "Resolved", resolution = "Must not resolve after rejection", rowVersion = await Version()
        });
        Assert.Equal(HttpStatusCode.BadRequest, blocked.StatusCode);
    }

    [Fact]
    public async Task Only_creator_or_pm_can_propose_an_independent_verifier()
    {
        var p = await d.Project();
        var creatorIssue = await f.As(TestData.Alex).Post($"/api/v1/projects/{p.Id}/issues", new
        {
            title = "Creator appointment authorization", severity = "Medium", ownerId = d.User(TestData.Omar),
            projectDisciplineId = d.ProjectDiscipline(p.Id, "Civil")
        }).Result.Json(201);
        var creatorId = creatorIssue.G("id");

        var ownerAttempt = await f.As(TestData.Omar).Post($"/api/v1/issues/{creatorId}/verification", new
        {
            verifierId = d.User(TestData.Marc), status = "Proposed", note = "Owner cannot appoint", rowVersion = await IssueVersion(creatorId)
        });
        Assert.Equal(HttpStatusCode.Forbidden, ownerAttempt.StatusCode);
        var leadAttempt = await f.As(TestData.Marc).Post($"/api/v1/issues/{creatorId}/verification", new
        {
            verifierId = d.User(TestData.Marc), status = "Proposed", note = "Discipline lead cannot appoint", rowVersion = await IssueVersion(creatorId)
        });
        Assert.Equal(HttpStatusCode.Forbidden, leadAttempt.StatusCode);
        await f.As(TestData.Alex).Post($"/api/v1/issues/{creatorId}/verification", new
        {
            verifierId = d.User(TestData.Marc), status = "Proposed", note = "Creator appoints independent verifier", rowVersion = await IssueVersion(creatorId)
        }).Result.Json(201);

        var pmIssue = await f.As(TestData.Pm).Post($"/api/v1/projects/{p.Id}/issues", new
        {
            title = "PM appointment authorization", severity = "Medium", ownerId = d.User(TestData.Alex),
            projectDisciplineId = d.ProjectDiscipline(p.Id, "Civil")
        }).Result.Json(201);
        var pmId = pmIssue.G("id");
        var nonCreatorOwnerAttempt = await f.As(TestData.Alex).Post($"/api/v1/issues/{pmId}/verification", new
        {
            verifierId = d.User(TestData.Marc), status = "Proposed", note = "Owner cannot replace PM appointment", rowVersion = await IssueVersion(pmId)
        });
        Assert.Equal(HttpStatusCode.Forbidden, nonCreatorOwnerAttempt.StatusCode);
        await f.As(TestData.Pm).Post($"/api/v1/issues/{pmId}/verification", new
        {
            verifierId = d.User(TestData.Marc), status = "Proposed", note = "PM appoints independent verifier", rowVersion = await IssueVersion(pmId)
        }).Result.Json(201);
        var replacementAttempt = await f.As(TestData.Alex).Post($"/api/v1/issues/{pmId}/verification", new
        {
            verifierId = d.User(TestData.Omar), status = "Proposed", note = "Owner cannot replace an appointment", rowVersion = await IssueVersion(pmId)
        });
        Assert.Equal(HttpStatusCode.Forbidden, replacementAttempt.StatusCode);
        await f.As(TestData.Pm).Post($"/api/v1/issues/{pmId}/verification", new
        {
            verifierId = d.User(TestData.Omar), status = "Proposed", note = "PM replaces the independent verifier", rowVersion = await IssueVersion(pmId)
        }).Result.Json(201);
    }

    [Fact]
    public async Task Affected_disciplines_share_one_issue_across_discipline_views_and_exports()
    {
        var p = await d.Project();
        var civil = d.ProjectDiscipline(p.Id, "Civil");
        var electrical = d.ProjectDiscipline(p.Id, "Electrical");
        var other = await d.Project();
        var foreign = d.ProjectDiscipline(other.Id, "Electrical");
        Assert.Equal(HttpStatusCode.BadRequest, (await f.As(TestData.Alex).Post($"/api/v1/projects/{p.Id}/issues", new
        {
            title = "Foreign discipline", severity = "High", projectDisciplineId = civil, affectedDisciplineIds = new[] { foreign }
        })).StatusCode);
        var created = await f.As(TestData.Alex).Post($"/api/v1/projects/{p.Id}/issues", new
        {
            title = "Duct bank crosses feeder", severity = "High", projectDisciplineId = civil, affectedDisciplineIds = new[] { electrical, electrical }
        }).Result.Json(201);
        var id = created.G("id");
        var key = created.S("key");
        foreach (var location in new object[]
        {
            new { kind = "Alignment", alignment = "Road-A", startStation = 10, endStation = 20, stationUnits = "m", rowVersion = await IssueVersion(id) },
            new { kind = "SiteArea", siteArea = "North yard", rowVersion = await IssueVersion(id) + 1 },
        })
            await f.As(TestData.Alex).Post($"/api/v1/issues/{id}/locations", location).Result.Json(201);

        // AC-LOC-01: the Civil lead and the Electrical lead both see the same issue ID with its exact range and both locations.
        foreach (var (who, discipline) in new[] { (TestData.Marc, civil), (TestData.Omar, electrical) })
        {
            var rows = (await f.As(who).GetAsync($"/api/v1/projects/{p.Id}/issues?disciplineId={discipline}").Result.Json()).AsArray();
            var row = Assert.Single(rows)!;
            Assert.Equal(key, row.S("key"));
            Assert.Equal(new[] { "Electrical" }, row["affectedDisciplineNames"]!.AsArray().Select(x => x!.GetValue<string>()));
            Assert.Equal(2, row["locationLabels"]!.AsArray().Count);
            Assert.Contains("Road-A", row.S("locationSummary"));
        }
        var detail = await f.As(TestData.Rita).GetAsync($"/api/v1/issues/{id}").Result.Json();
        Assert.Equal(electrical, detail["issue"]!["affectedDisciplineIds"]!.AsArray().Single()!.GetValue<Guid>());
        var csv = System.Text.Encoding.UTF8.GetString(await (await f.As(TestData.Pm).GetAsync($"/api/v1/projects/{p.Id}/issues/export?format=csv&disciplineId={electrical}")).Content.ReadAsByteArrayAsync());
        Assert.Contains("Affected disciplines", csv);
        Assert.Contains(key, csv);

        // Existing issue edit permission, version check and audit apply to the relation.
        var version = await IssueVersion(id);
        Assert.Equal(HttpStatusCode.Forbidden, (await f.As(TestData.Rita).Patch($"/api/v1/issues/{id}", new { affectedDisciplineIds = Array.Empty<Guid>() }, version)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await f.As(TestData.Omar).Patch($"/api/v1/issues/{id}", new { affectedDisciplineIds = Array.Empty<Guid>() }, version)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await f.As(TestData.Alex).Patch($"/api/v1/issues/{id}", new { affectedDisciplineIds = Array.Empty<Guid>() }, version - 1)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await f.As(TestData.Alex).Patch($"/api/v1/issues/{id}", new { affectedDisciplineIds = new[] { foreign } }, version)).StatusCode);
        (await f.As(TestData.Alex).Patch($"/api/v1/issues/{id}", new { affectedDisciplineIds = Array.Empty<Guid>() }, version)).EnsureSuccessStatusCode();
        Assert.Equal(version + 1, await IssueVersion(id));
        Assert.Empty((await f.As(TestData.Omar).GetAsync($"/api/v1/projects/{p.Id}/issues?disciplineId={electrical}").Result.Json()).AsArray());
        Assert.Equal(1, f.Db(db => db.ActivityLog.Count(a => a.ItemType == "IssueAffectedDiscipline" && a.Action == "Removed" && a.ProjectId == p.Id)));
        (await f.As(TestData.Pm).Patch($"/api/v1/issues/{id}", new { affectedDisciplineIds = new[] { electrical } }, version + 1)).EnsureSuccessStatusCode();
        Assert.Single((await f.As(TestData.Omar).GetAsync($"/api/v1/projects/{p.Id}/issues?disciplineId={electrical}").Result.Json()).AsArray());

        // A restricted project hides the issue from non-members regardless of discipline.
        await f.DbAsync(async db => { (await db.Projects.SingleAsync(x => x.Id == p.Id)).Visibility = Hub.Domain.Visibility.Restricted; return await db.SaveChangesAsync(); });
        Assert.False((await f.As(TestData.Rita).GetAsync($"/api/v1/projects/{p.Id}/issues?disciplineId={electrical}")).IsSuccessStatusCode);
        Assert.False((await f.As(TestData.Rita).GetAsync($"/api/v1/issues/{id}")).IsSuccessStatusCode);
        Assert.Single((await f.As(TestData.Omar).GetAsync($"/api/v1/projects/{p.Id}/issues?disciplineId={electrical}").Result.Json()).AsArray());
    }
}
