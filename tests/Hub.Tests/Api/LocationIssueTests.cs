using System.Net;
using System.Text.Json.Nodes;
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
        var verify = await f.As(TestData.Marc).Post($"/api/v1/issues/{id}/verification", new
        {
            verifierId = d.User(TestData.Marc), status = "Verified", evidenceUrl = "https://review.example.test/verify/1", note = "Independent synthetic review", rowVersion = await IssueVersion(id)
        }).Result.Json(201);
        Assert.NotEqual(Guid.Empty, verify.G("id"));
        Assert.Equal(HttpStatusCode.BadRequest, (await f.As(TestData.Marc).Post($"/api/v1/issues/{id}/verification", new
        {
            verifierId = d.User(TestData.Marc), status = "Verified", evidenceUrl = "https://review.example.test/verify/repeat", rowVersion = await IssueVersion(id)
        })).StatusCode);

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
}
