using System.Net;
using System.Text.Json.Nodes;
using Hub.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Hub.Tests.Api;

[Collection("api")]
public sealed class LocationIssueTests(HubFactory f)
{
    readonly TestData d = new(f);

    async Task<JsonNode> Issue(Guid projectId, string owner = TestData.Alex) =>
        await f.As(TestData.Alex).Post($"/api/v1/projects/{projectId}/issues", new
        {
            title = "Synthetic utility clash", description = "Coordination issue for location workflow", severity = "High",
            ownerId = d.User(owner), projectDisciplineId = d.ProjectDiscipline(projectId, "Civil"),
            issueType = "Coordination", locations = new[] { new { kind = "SiteArea", siteArea = "Synthetic yard" } }
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
        Assert.Equal(2, (await f.As(TestData.Alex).GetAsync($"/api/v1/issues/{id}/locations").Result.Json()).AsArray().Count);
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
    public async Task Valid_coordinate_preserves_declared_values_and_reference_metadata_on_readback()
    {
        var p = await d.Project();
        var issue = await (await f.As(TestData.Alex).Post($"/api/v1/projects/{p.Id}/issues", new
        {
            title = "Coordinate preservation", severity = "Low", issueType = "General",
            projectDisciplineId = d.ProjectDiscipline(p.Id, "Civil")
        })).Json(201);
        var id = issue.G("id");
        const decimal x = 123.456m, y = -7.25m, z = 0.5m;

        await (await f.As(TestData.Alex).Post($"/api/v1/issues/{id}/locations", new
        {
            kind = "Coordinate", coordinateX = x, coordinateY = y, coordinateZ = z,
            coordinateReferenceSystem = "EPSG:26920", coordinateUnits = "m", rowVersion = await IssueVersion(id)
        })).Json(201);

        var saved = Assert.Single((await (await f.As(TestData.Alex).GetAsync($"/api/v1/issues/{id}/locations")).Json()).AsArray());
        Assert.Equal("Coordinate", saved!.S("kind"));
        Assert.Equal(x, saved["coordinateX"]!.GetValue<decimal>());
        Assert.Equal(y, saved["coordinateY"]!.GetValue<decimal>());
        Assert.Equal(z, saved["coordinateZ"]!.GetValue<decimal>());
        Assert.Equal("EPSG:26920", saved.S("coordinateReferenceSystem"));
        Assert.Equal("m", saved.S("coordinateUnits"));
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

    [Fact]
    public async Task Coordination_issue_needs_a_reference_at_creation_and_verification_to_resolve()
    {
        var p = await d.Project();
        var alex = f.As(TestData.Alex);
        var root = $"/api/v1/projects/{p.Id}/issues";
        // FR-LOC-01: refused without a reference, with an invalid one or with an unknown type; nothing is partly created.
        var missing = await alex.Post(root, new { title = "Clash without reference", severity = "High", issueType = "Coordination" });
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        Assert.Contains("\"reference\"", await missing.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.BadRequest, (await alex.Post(root, new
        {
            title = "Clash with reversed stations", severity = "High", issueType = "Coordination",
            locations = new[] { new { kind = "Alignment", alignment = "Road-B", startStation = 30, endStation = 10, stationUnits = "m" } }
        })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await alex.Post(root, new { title = "Unknown type", severity = "High", issueType = "Clash" })).StatusCode);
        Assert.False(f.Db(db => db.Issues.Any(i => i.ProjectId == p.Id)));

        var coordination = await alex.Post(root, new
        {
            title = "Duct clashes at gridline C", severity = "High", issueType = "Coordination",
            documents = new[] { new { kind = "Drawing", identifier = "C-300", revision = "A", sourceUrl = "https://review.example.test/c-300", isAvailable = true } }
        }).Result.Json(201);
        var cid = coordination.G("id");
        var general = await alex.Post(root, new { title = "Site office printer offline", severity = "Low" }).Result.Json(201);
        var gid = general.G("id");
        Assert.Equal("Coordination", (await alex.GetAsync($"/api/v1/issues/{cid}").Result.Json())["issue"]!.S("issueType"));
        Assert.Equal("General", (await alex.GetAsync($"/api/v1/issues/{gid}").Result.Json())["issue"]!.S("issueType"));
        Assert.Equal("C-300", Assert.Single((await alex.GetAsync($"/api/v1/issues/{cid}/documents").Result.Json()).AsArray())!.S("identifier"));
        Assert.Equal(cid, Assert.Single((await alex.GetAsync($"{root}?issueType=Coordination").Result.Json()).AsArray())!.G("id"));
        Assert.Equal(gid, Assert.Single((await alex.GetAsync($"{root}?issueType=General").Result.Json()).AsArray())!.G("id"));
        var csv = System.Text.Encoding.UTF8.GetString(await (await f.As(TestData.Pm).GetAsync($"{root}/export?format=csv&issueType=Coordination")).Content.ReadAsByteArrayAsync());
        Assert.Contains("Issue type", csv);
        Assert.Contains(coordination.S("key"), csv);
        Assert.DoesNotContain(general.S("key"), csv);

        // AC-LOC-02: only the Coordination issue waits for independent verification; a General issue may carry references and still resolve.
        Assert.Equal(HttpStatusCode.BadRequest, (await alex.Post($"/api/v1/issues/{cid}/transition", new
        {
            toStatus = "Resolved", resolution = "Duct lowered", rowVersion = await IssueVersion(cid)
        })).StatusCode);
        await alex.Post($"/api/v1/issues/{gid}/locations", new { kind = "SiteArea", siteArea = "Site office", rowVersion = await IssueVersion(gid) }).Result.Json(201);
        (await alex.Post($"/api/v1/issues/{gid}/transition", new { toStatus = "Resolved", resolution = "Printer replaced", rowVersion = await IssueVersion(gid) })).EnsureSuccessStatusCode();
        await f.As(TestData.Pm).Post($"/api/v1/issues/{cid}/verification", new
        {
            verifierId = d.User(TestData.Marc), status = "Proposed", note = "Independent clash check", rowVersion = await IssueVersion(cid)
        }).Result.Json(201);
        await f.As(TestData.Marc).Post($"/api/v1/issues/{cid}/verification", new
        {
            verifierId = d.User(TestData.Marc), status = "Verified", evidenceUrl = "https://review.example.test/verify/c-300", rowVersion = await IssueVersion(cid)
        }).Result.Json(201);
        (await alex.Post($"/api/v1/issues/{cid}/transition", new { toStatus = "Resolved", resolution = "Duct lowered", rowVersion = await IssueVersion(cid) })).EnsureSuccessStatusCode();

        // A realised risk follows the same creation rule.
        var risk = await alex.Post($"/api/v1/projects/{p.Id}/risks", new { title = "Crossing may clash", probability = 2, impact = 2 }).Result.Json(201);
        var riskId = risk.G("id");
        Assert.Equal(HttpStatusCode.BadRequest, (await alex.Post($"/api/v1/risks/{riskId}/transition", new
        {
            toStatus = "Realised", issue = new { title = "Realised crossing clash", severity = "High", issueType = "Coordination" }, rowVersion = risk.I("rowVersion")
        })).StatusCode);
        var realised = await alex.Post($"/api/v1/risks/{riskId}/transition", new
        {
            toStatus = "Realised", rowVersion = risk.I("rowVersion"),
            issue = new { title = "Realised crossing clash", severity = "High", issueType = "Coordination", locations = new[] { new { kind = "Building", building = "Pump house" } } }
        }).Result.Json();
        Assert.Equal("Coordination", f.Db(db => db.Issues.Single(i => i.Id == realised.G("issueId")).IssueType));
    }

    [Fact]
    public async Task Issue_type_change_is_pm_or_owner_only_and_keeps_an_appointed_verification()
    {
        var p = await d.Project();
        var issue = await f.As(TestData.Alex).Post($"/api/v1/projects/{p.Id}/issues", new
        {
            title = "Conduit crossing", severity = "Medium", ownerId = d.User(TestData.Omar), projectDisciplineId = d.ProjectDiscipline(p.Id, "Civil")
        }).Result.Json(201);
        var id = issue.G("id");
        var toCoordination = new { issueType = "Coordination" };
        var version = await IssueVersion(id);
        // The raiser and the discipline lead may edit the issue, but only the PM or the owner changes its type; Read Only edits nothing.
        foreach (var who in new[] { TestData.Alex, TestData.Marc, TestData.Rita })
            Assert.Equal(HttpStatusCode.Forbidden, (await f.As(who).Patch($"/api/v1/issues/{id}", toCoordination, version)).StatusCode);
        Assert.False((await f.As(TestData.Alex).GetAsync($"/api/v1/issues/{id}").Result.Json())["permissions"]!["changeType"]!["ok"]!.GetValue<bool>());
        Assert.True((await f.As(TestData.Omar).GetAsync($"/api/v1/issues/{id}").Result.Json())["permissions"]!["changeType"]!["ok"]!.GetValue<bool>());
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await f.As(TestData.Omar).Patch($"/api/v1/issues/{id}", toCoordination, version)).StatusCode);
        await f.As(TestData.Alex).Post($"/api/v1/issues/{id}/locations", new { kind = "SiteArea", siteArea = "Substation yard", rowVersion = version }).Result.Json(201);
        Assert.Equal(HttpStatusCode.Conflict, (await f.As(TestData.Omar).Patch($"/api/v1/issues/{id}", toCoordination, version)).StatusCode);
        version = await IssueVersion(id);
        (await f.As(TestData.Omar).Patch($"/api/v1/issues/{id}", toCoordination, version)).EnsureSuccessStatusCode();
        Assert.Equal(version + 1, await IssueVersion(id));
        Assert.Equal("Coordination", f.Db(db => db.Issues.Single(i => i.Id == id).IssueType));
        Assert.Single(f.Db(db => db.ActivityLog.Where(a => a.ItemId == id && a.ItemType == "Issue" && a.Action != "Created").ToList()),
            a => a.Changes.Contains("\"IssueType\"") && a.Changes.Contains("\"Coordination\""));

        // Without verification records the PM may set it back; once a verifier is appointed it stays Coordination.
        (await f.As(TestData.Pm).Patch($"/api/v1/issues/{id}", new { issueType = "General" }, await IssueVersion(id))).EnsureSuccessStatusCode();
        (await f.As(TestData.Pm).Patch($"/api/v1/issues/{id}", toCoordination, await IssueVersion(id))).EnsureSuccessStatusCode();
        await f.As(TestData.Alex).Post($"/api/v1/issues/{id}/verification", new
        {
            verifierId = d.User(TestData.Marc), status = "Proposed", note = "Appoint independent verifier", rowVersion = await IssueVersion(id)
        }).Result.Json(201);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await f.As(TestData.Pm).Patch($"/api/v1/issues/{id}", new { issueType = "General" }, await IssueVersion(id))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await f.As(TestData.Omar).Post($"/api/v1/issues/{id}/transition", new
        {
            toStatus = "Resolved", resolution = "Cannot bypass verification", rowVersion = await IssueVersion(id)
        })).StatusCode);

        // A closed issue keeps the type it was resolved under.
        var closed = await f.As(TestData.Pm).Post($"/api/v1/projects/{p.Id}/issues", new { title = "Closed general issue", severity = "Low" }).Result.Json(201);
        var closedId = closed.G("id");
        await f.As(TestData.Pm).Post($"/api/v1/issues/{closedId}/locations", new { kind = "SiteArea", siteArea = "Laydown", rowVersion = await IssueVersion(closedId) }).Result.Json(201);
        (await f.As(TestData.Pm).Post($"/api/v1/issues/{closedId}/transition", new { toStatus = "Resolved", resolution = "Moved", rowVersion = await IssueVersion(closedId) })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await f.As(TestData.Pm).Patch($"/api/v1/issues/{closedId}", toCoordination, await IssueVersion(closedId))).StatusCode);
    }

    [Fact]
    public async Task Location_reference_and_verification_writes_reach_the_issue_history()
    {
        var p = await d.Project();
        var issue = await Issue(p.Id);
        var id = issue.G("id");
        await f.As(TestData.Alex).Post($"/api/v1/issues/{id}/documents", new
        {
            kind = "Model", identifier = "M-01", revision = "3", sourceUrl = "https://review.example.test/m-01", isAvailable = true, rowVersion = await IssueVersion(id)
        }).Result.Json(201);
        await f.As(TestData.Pm).Post($"/api/v1/issues/{id}/verification", new
        {
            verifierId = d.User(TestData.Marc), status = "Proposed", note = "Independent model check", rowVersion = await IssueVersion(id)
        }).Result.Json(201);
        var rows = f.Db(db => db.ActivityLog.Where(a => a.ProjectId == p.Id && a.ItemKey == issue.S("key") && a.ItemType != "Issue")
            .Select(a => new { a.ItemType, a.Action, a.ActorUserId, a.Reason }).ToList());
        Assert.Contains(rows, r => r.ItemType == "IssueLocation" && r.Action == "Created" && r.ActorUserId == d.User(TestData.Alex));
        Assert.Contains(rows, r => r.ItemType == "IssueDocumentReference" && r.Action == "Created" && r.ActorUserId == d.User(TestData.Alex));
        Assert.Contains(rows, r => r.ItemType == "IssueVerification" && r.Action == "Created" && r.ActorUserId == d.User(TestData.Pm) && r.Reason == "Independent model check");
        var history = await f.As(TestData.Rita).GetAsync($"/api/v1/items/Issue/{id}/activity").Result.Json();
        Assert.Superset(new HashSet<string> { "Issue", "IssueLocation", "IssueDocumentReference", "IssueVerification" },
            history["items"]!.AsArray().Select(x => x!.S("itemType")).ToHashSet());
    }

    [Fact]
    public async Task Issue_type_migration_marks_issues_that_already_have_references_as_coordination()
    {
        var p = await d.Project();
        async Task<Guid> Legacy(string title) =>
            (await f.As(TestData.Alex).Post($"/api/v1/projects/{p.Id}/issues", new { title, severity = "Low" }).Result.Json(201)).G("id");
        var located = await Legacy("Legacy located issue");
        var referenced = await Legacy("Legacy markup issue");
        var plain = await Legacy("Legacy plain issue");
        await f.As(TestData.Alex).Post($"/api/v1/issues/{located}/locations", new { kind = "SiteArea", siteArea = "Legacy yard", rowVersion = 0 }).Result.Json(201);
        await f.As(TestData.Alex).Post($"/api/v1/issues/{referenced}/documents", new
        {
            kind = "Markup", identifier = "MK-1", revision = "1", sourceUrl = "https://review.example.test/mk-1", isAvailable = true, rowVersion = 0
        }).Result.Json(201);
        var migration = new Hub.Api.Data.Migrations.CoordinationIssueType();
        var types = await f.DbAsync(async db =>
        {
            // Re-run the migration's own Down and Up operations over these rows inside a rolled-back transaction,
            // so the shared test database keeps its schema and data.
            var generator = db.GetService<IMigrationsSqlGenerator>();
            await using var tx = await db.Database.BeginTransactionAsync();
            foreach (var command in generator.Generate(migration.DownOperations).Concat(generator.Generate(migration.UpOperations)))
                await db.Database.ExecuteSqlRawAsync(command.CommandText);
            var result = new Dictionary<Guid, string>();
            foreach (var issueId in new[] { located, referenced, plain })
                result[issueId] = await db.Database.SqlQueryRaw<string>("SELECT issue_type AS \"Value\" FROM hub.issue WHERE id = {0}", issueId).SingleAsync();
            await tx.RollbackAsync();
            return result;
        });
        Assert.Equal("Coordination", types[located]);
        Assert.Equal("Coordination", types[referenced]);
        Assert.Equal("General", types[plain]);
        Assert.Equal("General", f.Db(db => db.Issues.Single(i => i.Id == located).IssueType)); // rolled back
    }

    [Fact]
    public async Task Affected_discipline_leads_get_one_project_scoped_notice_per_command()
    {
        var p = await d.Project();
        var civil = d.ProjectDiscipline(p.Id, "Civil");
        var electrical = d.ProjectDiscipline(p.Id, "Electrical");
        var (marc, omar) = (d.User(TestData.Marc), d.User(TestData.Omar));
        var code = Hub.Domain.NotificationEvents.IssueAffectedDiscipline;
        Assert.Contains(code, Hub.Domain.NotificationEvents.ProjectScoped);
        (await f.As(TestData.Omar).Put($"/api/v1/me/preferences/events/{code}", new { app = true, email = true })).EnsureSuccessStatusCode();
        int Notices(Guid user, Guid issueId) => f.Db(db => db.Notifications.Where(n => n.UserId == user && n.ItemId == issueId && n.EventType == code).Sum(n => n.Count));

        // Raised with Electrical affected: its lead hears once, in app and by project-scoped email; Civil's lead and the actor do not.
        var issue = await f.As(TestData.Alex).Post($"/api/v1/projects/{p.Id}/issues", new
        {
            title = "Feeder crosses the culvert", severity = "High", ownerId = marc, affectedDisciplineIds = new[] { electrical }
        }).Result.Json(201);
        var id = issue.G("id");
        Assert.Equal(1, Notices(omar, id));
        Assert.Equal(0, Notices(marc, id));
        Assert.Equal(0, Notices(d.User(TestData.Alex), id));
        Assert.Equal($"/projects/{p.ProjectNumber}/issues?panel=Issue:{id}", f.Db(db => db.Notifications.Single(n => n.UserId == omar && n.ItemId == id).LinkPath));
        Assert.Equal(new[] { p.Id }, f.Db(db => db.Emails.Single(e => e.UserId == omar && e.DedupKey == $"{code}:{id}:{omar}").RequiredProjectIds));

        // The owner adds the discipline he leads: no self-notice. A stale, refused or repeated command adds nothing.
        var version = await IssueVersion(id);
        (await f.As(TestData.Marc).Patch($"/api/v1/issues/{id}", new { affectedDisciplineIds = new[] { electrical, civil } }, version)).EnsureSuccessStatusCode();
        Assert.Equal(0, Notices(marc, id));
        Assert.Equal(HttpStatusCode.Conflict, (await f.As(TestData.Marc).Patch($"/api/v1/issues/{id}", new { affectedDisciplineIds = new[] { civil } }, version)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await f.As(TestData.Rita).Patch($"/api/v1/issues/{id}", new { affectedDisciplineIds = Array.Empty<Guid>() }, await IssueVersion(id))).StatusCode);
        (await f.As(TestData.Marc).Patch($"/api/v1/issues/{id}", new { affectedDisciplineIds = new[] { electrical, civil } }, await IssueVersion(id))).EnsureSuccessStatusCode();
        Assert.Equal(1, Notices(omar, id));

        // One notice per recipient per command, even when one person leads both newly affected disciplines.
        (await f.As(TestData.Pm).Patch($"/api/v1/issues/{id}", new { affectedDisciplineIds = Array.Empty<Guid>() }, await IssueVersion(id))).EnsureSuccessStatusCode();
        await f.DbAsync(async db => { (await db.ProjectDisciplines.SingleAsync(x => x.Id == civil)).LeadUserId = omar; return await db.SaveChangesAsync(); });
        (await f.As(TestData.Pm).Patch($"/api/v1/issues/{id}", new { affectedDisciplineIds = new[] { electrical, civil } }, await IssueVersion(id))).EnsureSuccessStatusCode();
        Assert.Equal(2, Notices(omar, id));
        Assert.Equal(2, f.Db(db => db.Notifications.Count(n => n.UserId == omar && n.ItemId == id && n.EventType == code)));

        // A lead who no longer has access to the restricted project hears nothing.
        (await f.As(TestData.Pm).Patch($"/api/v1/issues/{id}", new { affectedDisciplineIds = Array.Empty<Guid>() }, await IssueVersion(id))).EnsureSuccessStatusCode();
        await f.DbAsync(async db =>
        {
            (await db.Projects.SingleAsync(x => x.Id == p.Id)).Visibility = Hub.Domain.Visibility.Restricted;
            foreach (var m in await db.ProjectMembers.Where(m => m.ProjectId == p.Id && m.UserId == omar).ToListAsync()) m.RemovedAt = f.Clock.GetUtcNow();
            return await db.SaveChangesAsync();
        });
        Assert.False(await f.DbAsync(db => EmailProjectAccess.Allowed(db, omar, [p.Id])));
        (await f.As(TestData.Pm).Patch($"/api/v1/issues/{id}", new { affectedDisciplineIds = new[] { electrical } }, await IssueVersion(id))).EnsureSuccessStatusCode();
        Assert.Equal(2, Notices(omar, id));
    }

    [Fact]
    public async Task Coordination_dashboard_lists_an_issue_once_under_its_primary_and_each_affected_discipline()
    {
        var p = await d.Project();
        var civil = d.ProjectDiscipline(p.Id, "Civil");
        var electrical = d.ProjectDiscipline(p.Id, "Electrical");
        var root = $"/api/v1/projects/{p.Id}/issues";
        var shared = (await f.As(TestData.Alex).Post(root, new
        {
            title = "Shared corridor clash", severity = "High", projectDisciplineId = civil, affectedDisciplineIds = new[] { electrical, civil }
        }).Result.Json(201)).G("id");
        var civilOnly = (await f.As(TestData.Alex).Post(root, new { title = "Civil kerb issue", severity = "Low", projectDisciplineId = civil }).Result.Json(201)).G("id");
        async Task<Guid[]> Listed(Guid? discipline) =>
            [.. (await f.As(TestData.Pm).GetAsync($"/api/v1/projects/{p.Id}/coordination{(discipline is { } x ? $"?disciplineId={x}" : "")}").Result.Json())["issues"]!
                .AsArray().Select(row => row!.G("id"))];
        Assert.Equal(new[] { shared, civilOnly }.Order(), (await Listed(civil)).Order());
        Assert.Equal(new[] { shared }, await Listed(electrical));
        Assert.Equal(new[] { shared, civilOnly }.Order(), (await Listed(null)).Order());
    }
}
