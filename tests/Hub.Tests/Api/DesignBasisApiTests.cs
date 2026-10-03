using System.Text.Json.Nodes;
using Hub.Api.Data;
using Hub.Api.Features;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

[Collection("api")]
public sealed class DesignBasisApiTests(HubFactory f)
{
    readonly TestData data = new(f);
    async Task<JsonNode> Post(string who, string path, object body, int status = 200) => await (await f.As(who).Post(path, body)).Json(status);
    int Version<T>(Guid id) where T : Audited => f.Db(db => db.Set<T>().AsNoTracking().Single(x => x.Id == id).RowVersion);

    [Fact]
    public async Task New_proposed_basis_requires_a_confirmation_due_date()
    {
        var project = await data.Project();
        var civil = data.ProjectDiscipline(project.Id, "Civil");
        var body = new DesignBasisEndpoints.CreateBody(Guid.NewGuid(), BasisKind.Criterion, "Dated basis", data.User(TestData.Alex), civil,
            data.User(TestData.Marc), new DesignBasisEndpoints.VersionInput("Area A", "Proposed value", null, null,
                "Manual", null, "https://example.test/source", "A", null, null), null);

        await Post(TestData.Pm, $"/api/v1/projects/{project.Id}/design-basis", body, 400);
        var dated = body with { RequestId = Guid.NewGuid(), Version = body.Version with { ConfirmationDueDate = new DateOnly(2026, 10, 5) } };
        var root = $"/api/v1/projects/{project.Id}/design-basis";
        var entry = await Post(TestData.Pm, root, dated);
        var entryId = entry.G("id");
        var versionId = f.Db(db => db.DesignBasisVersions.Single(v => v.EntryId == entryId).Id);
        await Post(TestData.Marc, $"{root}/{entryId}/versions/{versionId}/confirm", new DesignBasisEndpoints.ConfirmBody(
            Guid.NewGuid(), Version<DesignBasisEntry>(entryId), Version<DesignBasisVersion>(versionId), "Confirm dated basis"));
        await Post(TestData.Marc, $"{root}/{entryId}/propose", new DesignBasisEndpoints.ProposeBody(Guid.NewGuid(),
            Version<DesignBasisEntry>(entryId), Version<DesignBasisVersion>(versionId), body.Version, "Propose without date"), 400);
        var proposed = await Post(TestData.Marc, $"{root}/{entryId}/propose", new DesignBasisEndpoints.ProposeBody(Guid.NewGuid(),
            Version<DesignBasisEntry>(entryId), Version<DesignBasisVersion>(versionId), dated.Version with { ConfirmationDueDate = new DateOnly(2026, 11, 5) }, "Propose dated replacement"));
        await Post(TestData.Alex, $"{root}/{entryId}/versions/{proposed.G("id")}/edit", new DesignBasisEndpoints.EditProposedBody(
            Guid.NewGuid(), Version<DesignBasisEntry>(entryId), Version<DesignBasisVersion>(proposed.G("id")), body.Version, "Edit without date"), 400);
    }

    [Fact]
    public async Task Scope_edit_and_replacement_require_inspection_of_another_duplicate_entry()
    {
        var project = await data.Project();
        var civil = data.ProjectDiscipline(project.Id, "Civil");
        var root = $"/api/v1/projects/{project.Id}/design-basis";
        var input = new DesignBasisEndpoints.VersionInput("Area A", "Narrative requirement", null, null,
            "Synthetic report", "SYN-DUP", "https://example.test/source", "A", new DateOnly(2026, 10, 5), null);
        var create = new DesignBasisEndpoints.CreateBody(Guid.NewGuid(), BasisKind.Criterion, "Shared criterion",
            data.User(TestData.Alex), civil, data.User(TestData.Marc), input, null);
        var original = await Post(TestData.Pm, root, create);
        var other = await Post(TestData.Pm, root, create with { RequestId = Guid.NewGuid(), Version = input with { Scope = "Area B" } });
        var id = other.G("id");
        var versionId = f.Db(db => db.DesignBasisVersions.Single(v => v.EntryId == id).Id);
        var editPath = $"{root}/{id}/versions/{versionId}/edit";
        var edit = new DesignBasisEndpoints.EditProposedBody(Guid.NewGuid(), Version<DesignBasisEntry>(id),
            Version<DesignBasisVersion>(versionId), input, "Correct the proposed scope");
        var refusal = await Post(TestData.Alex, editPath, edit, 409);
        Assert.Equal(original.G("id"), refusal.G("existingId"));
        Assert.Equal("Area B", f.Db(db => db.DesignBasisVersions.Single(v => v.Id == versionId).Scope));
        await Post(TestData.Alex, editPath, edit with { RequestId = Guid.NewGuid(), InspectedDuplicateId = original.G("id") });
        await Post(TestData.Marc, $"{root}/{id}/versions/{versionId}/confirm", new DesignBasisEndpoints.ConfirmBody(
            Guid.NewGuid(), Version<DesignBasisEntry>(id), Version<DesignBasisVersion>(versionId), "Narrative source checked"));
        var propose = new DesignBasisEndpoints.ProposeBody(Guid.NewGuid(), Version<DesignBasisEntry>(id),
            Version<DesignBasisVersion>(versionId), input with { DeclaredRevision = "B" }, "Updated source revision");
        await Post(TestData.Marc, $"{root}/{id}/propose", propose, 409);
        Assert.Equal(1, f.Db(db => db.DesignBasisVersions.Count(v => v.EntryId == id)));
        await Post(TestData.Marc, $"{root}/{id}/propose", propose with { RequestId = Guid.NewGuid(), InspectedDuplicateId = original.G("id") });
        Assert.Equal(2, f.Db(db => db.DesignBasisVersions.Count(v => v.EntryId == id)));
    }

    [Fact]
    public async Task Member_can_propose_only_own_discipline_and_cannot_assign_another_owner_or_approver()
    {
        var project = await data.Project();
        var civil = data.ProjectDiscipline(project.Id, "Civil");
        var electrical = data.ProjectDiscipline(project.Id, "Electrical");
        var alex = data.User(TestData.Alex);
        var root = $"/api/v1/projects/{project.Id}/design-basis";
        var body = new DesignBasisEndpoints.CreateBody(Guid.NewGuid(), BasisKind.Assumption,
            "Member proposed level", alex, civil, null,
            new DesignBasisEndpoints.VersionInput("Level 1", "Proposed level", null, null,
                null, null, null, null, new DateOnly(2026, 10, 5), null), null);
        var otherDiscipline = await Post(TestData.Alex, root, body with { RequestId = Guid.NewGuid(), ProjectDisciplineId = electrical }, 403);
        Assert.Contains("Electrical lead", (string)otherDiscipline["detail"]!);
        await Post(TestData.Alex, root, body with { RequestId = Guid.NewGuid(), OwnerId = data.User(TestData.Marc) }, 403);
        await Post(TestData.Alex, root, body with { RequestId = Guid.NewGuid(), IndependentApproverId = data.User(TestData.Marc) }, 403);
        var entry = await Post(TestData.Alex, root, body);
        Assert.Equal(alex, f.Db(db => db.DesignBasisEntries.Single(x => x.Id == entry.G("id")).OwnerId));
        var versionId = f.Db(db => db.DesignBasisVersions.Single(v => v.EntryId == entry.G("id")).Id);
        // Neither the responsible lead nor an appointed approver: a permission refusal (§25.6), before field checks.
        foreach (var who in new[] { TestData.Alex, TestData.Pm })
            await Post(who, $"{root}/{entry.G("id")}/versions/{versionId}/confirm",
                new DesignBasisEndpoints.ConfirmBody(Guid.NewGuid(), Version<DesignBasisEntry>(entry.G("id")),
                    Version<DesignBasisVersion>(versionId), "Unauthorised approval"), 403);
        Assert.Equal(BasisStatus.Proposed, f.Db(db => db.DesignBasisVersions.Single(v => v.Id == versionId).Status));
        var assignPath = $"{root}/{entry.G("id")}/assign";
        var assignment = new DesignBasisEndpoints.AssignBody(Guid.NewGuid(), Version<DesignBasisEntry>(entry.G("id")),
            data.User(TestData.Pm), data.User(TestData.Marc), "Lead assigned a new accountable owner");
        await Post(TestData.Alex, assignPath, assignment, 403);
        await Post(TestData.Omar, assignPath, assignment with { RequestId = Guid.NewGuid() }, 403);
        await Post(TestData.Marc, assignPath, assignment);
        Assert.Equal(data.User(TestData.Pm), f.Db(db => db.DesignBasisEntries.Single(x => x.Id == entry.G("id")).OwnerId));
        await Post(TestData.Marc, assignPath, assignment with { RequestId = Guid.NewGuid() }, 409);
    }

    [Fact]
    public async Task Confirmation_preserves_exact_uses_and_creates_assessments_for_replacement()
    {
        var project = await data.Project();
        await f.DbAsync(async db => { (await db.Projects.SingleAsync(p => p.Id == project.Id)).Visibility = Visibility.Restricted;
            await db.SaveChangesAsync(); return 0; });
        var civil = data.ProjectDiscipline(project.Id, "Civil");
        var owner = data.User(TestData.Alex);
        var root = $"/api/v1/projects/{project.Id}/design-basis";
        var a = new DesignBasisEndpoints.VersionInput("Bridge / Pier 1", "Allowable bearing pressure", 100, null,
            "Geotechnical report", "GEO-1", "https://example.test/geotech", "A", new DateOnly(2026, 10, 5), null);
        var create = new DesignBasisEndpoints.CreateBody(Guid.NewGuid(), BasisKind.Criterion, "Bearing pressure", owner,
            civil, data.User(TestData.Marc), a, null);
        await Post(TestData.Rita, root, create, 404);
        var entry = await Post(TestData.Pm, root, create with { RequestId = Guid.NewGuid() });
        var id = entry.G("id");
        var versionId = f.Db(db => db.DesignBasisVersions.Single(v => v.EntryId == id).Id);
        var confirmPath = $"{root}/{id}/versions/{versionId}/confirm";
        await Post(TestData.Marc, confirmPath, new DesignBasisEndpoints.ConfirmBody(Guid.NewGuid(), Version<DesignBasisEntry>(id),
            Version<DesignBasisVersion>(versionId), "Geotechnical source checked"), 400);
        var editPath = $"{root}/{id}/versions/{versionId}/edit";
        var edit = new DesignBasisEndpoints.EditProposedBody(Guid.NewGuid(), Version<DesignBasisEntry>(id),
            Version<DesignBasisVersion>(versionId), a with { Units = "kPa" }, "Add units from the source report");
        await Post(TestData.Rita, editPath, edit with { RequestId = Guid.NewGuid() }, 404);
        await Post(TestData.Omar, editPath, edit with { RequestId = Guid.NewGuid() }, 403);
        await Post(TestData.Alex, editPath, edit);
        await Post(TestData.Alex, editPath, edit);
        await Post(TestData.Alex, editPath, edit with { RequestId = Guid.NewGuid() }, 409);
        Assert.Equal("kPa", f.Db(db => db.DesignBasisVersions.Single(v => v.Id == versionId).Units));
        var confirm = new DesignBasisEndpoints.ConfirmBody(Guid.NewGuid(),
            Version<DesignBasisEntry>(id), Version<DesignBasisVersion>(versionId), "Geotechnical source checked");
        var confirmed = await Post(TestData.Marc, confirmPath, confirm);
        await Post(TestData.Alex, editPath, edit with { RequestId = Guid.NewGuid(),
            EntryRowVersion = Version<DesignBasisEntry>(id), VersionRowVersion = Version<DesignBasisVersion>(versionId) }, 400);
        Assert.Equal(versionId, confirmed.G("id"));
        Assert.Equal(versionId, (await Post(TestData.Marc, confirmPath, confirm)).G("id"));
        await Post(TestData.Marc, confirmPath, confirm with { RequestId = Guid.NewGuid() }, 409);
        Assert.Equal(versionId, f.Db(db => db.DesignBasisEntries.Single(e => e.Id == id).CurrentVersionId));

        var task = await data.NewTask(project.Id, extra: new { assigneeId = owner });
        await Post(TestData.Pm, $"{root}/{id}/uses", new DesignBasisEndpoints.UseBody(Guid.NewGuid(), versionId,
            "Task", task.G("id"), "Foundation sizing"), 403);
        var use = await Post(TestData.Alex, $"{root}/{id}/uses", new DesignBasisEndpoints.UseBody(Guid.NewGuid(), versionId,
            "Task", task.G("id"), "Foundation sizing"));
        var secondTask = await data.NewTask(project.Id, extra: new { assigneeId = owner });
        var secondUse = await Post(TestData.Alex, $"{root}/{id}/uses", new DesignBasisEndpoints.UseBody(Guid.NewGuid(), versionId,
            "Task", secondTask.G("id"), "Check adjacent foundation"));
        var b = a with { NumericValue = 125, Units = "kPa", DeclaredRevision = "B" };
        var proposed = await Post(TestData.Marc, $"{root}/{id}/propose", new DesignBasisEndpoints.ProposeBody(Guid.NewGuid(),
            Version<DesignBasisEntry>(id), Version<DesignBasisVersion>(versionId), b, "New geotechnical report"));
        var bId = proposed.G("id");
        await Post(TestData.Marc, $"{root}/{id}/versions/{bId}/confirm", new DesignBasisEndpoints.ConfirmBody(Guid.NewGuid(),
            Version<DesignBasisEntry>(id), Version<DesignBasisVersion>(bId), "Revised report checked"));
        Assert.Equal(BasisStatus.Superseded, f.Db(db => db.DesignBasisVersions.Single(v => v.Id == versionId).Status));
        Assert.Equal(versionId, f.Db(db => db.BasisUses.Single(u => u.Id == use.G("id")).VersionId));
        Assert.Single(f.Db(db => db.BasisImpactAssessments.Where(i => i.BasisUseId == use.G("id") &&
            i.OldVersionId == versionId && i.NewVersionId == bId && i.Status == AssessmentStatus.Pending).ToList()));
        await Post(TestData.Marc, $"{root}/{id}/propose", new DesignBasisEndpoints.ProposeBody(Guid.NewGuid(),
            Version<DesignBasisEntry>(id), Version<DesignBasisVersion>(bId), b with { NumericValue = 130, DeclaredRevision = "C" },
            "Further revised report"), 400);
        var impact = f.Db(db => db.BasisImpactAssessments.Single(i => i.BasisUseId == use.G("id")));
        var taskVersion = f.Db(db => db.Tasks.Single(t => t.Id == task.G("id")).RowVersion);
        var decidePath = $"{root}/{id}/impacts/{impact.Id}/decide";
        var adopt = new DesignBasisEndpoints.ImpactBody(Guid.NewGuid(), Version<BasisImpactAssessment>(impact.Id),
            Version<BasisUse>(use.G("id")), Version<DesignBasisVersion>(bId), taskVersion,
            "Adopt", "Foundation sizing revised to report B", "https://example.test/review-B");
        await Post(TestData.Pm, decidePath, adopt, 403);
        await Post(TestData.Alex, decidePath, adopt with { RequestId = Guid.NewGuid(), TargetRowVersion = taskVersion - 1 }, 409);
        await Post(TestData.Alex, decidePath, adopt);
        await Post(TestData.Alex, decidePath, adopt);
        Assert.Equal(AssessmentStatus.Resolved, f.Db(db => db.BasisImpactAssessments.Single(i => i.Id == impact.Id).Status));
        Assert.Equal(2, f.Db(db => db.BasisUses.Count(u => u.TargetId == task.G("id") &&
            (u.VersionId == versionId || u.VersionId == bId))));
        var detail = await (await f.As(TestData.Alex).GetAsync($"{root}/{id}")).Json();
        Assert.Equal(bId, detail["uses"]!.AsArray().Single(u => u!.G("targetId") == task.G("id") &&
            u["isCurrent"]!.GetValue<bool>())!.G("versionId"));
        var secondImpact = f.Db(db => db.BasisImpactAssessments.Single(i => i.BasisUseId == secondUse.G("id")));
        var unaffected = new DesignBasisEndpoints.ImpactBody(Guid.NewGuid(), Version<BasisImpactAssessment>(secondImpact.Id),
            Version<BasisUse>(secondUse.G("id")), Version<DesignBasisVersion>(bId),
            f.Db(db => db.Tasks.Single(t => t.Id == secondTask.G("id")).RowVersion), "Unaffected",
            "Adjacent foundation remains outside revised area", "https://example.test/area-check");
        await Post(TestData.Alex, $"{root}/{id}/impacts/{secondImpact.Id}/decide", unaffected, 403);
        await Post(TestData.Marc, $"{root}/{id}/impacts/{secondImpact.Id}/decide", unaffected with { RequestId = Guid.NewGuid() });
        Assert.Equal(AssessmentStatus.Unaffected, f.Db(db => db.BasisImpactAssessments.Single(i => i.Id == secondImpact.Id).Status));
        Assert.Equal(versionId, f.Db(db => db.BasisUses.Single(u => u.Id == secondUse.G("id")).VersionId));
        var supersededFilter = await (await f.As(TestData.Pm).GetAsync(root + "?status=Superseded")).Json();
        Assert.DoesNotContain(supersededFilter["items"]!.AsArray(), row => row!.G("id") == id);
        var confirmedFilter = await (await f.As(TestData.Pm).GetAsync(root + "?status=Confirmed")).Json();
        Assert.Contains(confirmedFilter["items"]!.AsArray(), row => row!.G("id") == id);
        var scoped = await (await f.As(TestData.Pm).GetAsync(root + $"?kind=Criterion&scope=Pier&affectedWorkId={task.G("id")}")).Json();
        Assert.Contains(scoped["items"]!.AsArray(), row => row!.G("id") == id);
        // A malformed filter value is a field error, not the binder's raw message (list and export alike).
        foreach (var path in new[] { root, root + "/export" })
        {
            var malformed = await (await f.As(TestData.Pm).GetAsync(path + $"?affectedWorkId=Task:{task.G("id")}")).Json(400);
            Assert.Equal("validation", (string)malformed["code"]!);
            Assert.NotNull(malformed["errors"]!["affectedWorkId"]);
            Assert.DoesNotContain("Failed to bind", malformed.ToJsonString());
        }
        var notOverdue = await (await f.As(TestData.Pm).GetAsync(root + "?overdue=true")).Json();
        Assert.DoesNotContain(notOverdue["items"]!.AsArray(), row => row!.G("id") == id);
        var export = await f.As(TestData.Pm).GetAsync(root + "/export?kind=Criterion&scope=Pier&format=csv");
        export.EnsureSuccessStatusCode();
        var csv = await export.Content.ReadAsStringAsync();
        Assert.Contains("kPa", csv); Assert.Contains("GEO-1", csv); Assert.Contains("Geotechnical report", csv);
        await (await f.As(TestData.Rita).GetAsync(root + "/export?format=csv")).Json(404);
        var competing = create with { RequestId = Guid.NewGuid(), Version = b with { NumericValue = 150 } };
        await Post(TestData.Pm, root, competing, 409);
        var second = await Post(TestData.Pm, root, competing with { RequestId = Guid.NewGuid(), InspectedDuplicateId = id });
        var otherId = second.G("id");
        var otherVersionId = f.Db(db => db.DesignBasisVersions.Single(v => v.EntryId == otherId).Id);
        await Post(TestData.Marc, $"{root}/{otherId}/versions/{otherVersionId}/confirm", new DesignBasisEndpoints.ConfirmBody(Guid.NewGuid(),
            Version<DesignBasisEntry>(otherId), Version<DesignBasisVersion>(otherVersionId), "Conflicting report reviewed"));
        Assert.Single(f.Db(db => db.BasisConflicts.Where(c => !c.Resolved &&
            (c.LeftVersionId == bId || c.RightVersionId == bId) &&
            (c.LeftVersionId == otherVersionId || c.RightVersionId == otherVersionId)).ToList()));
        var conflict = f.Db(db => db.BasisConflicts.Single(c => !c.Resolved &&
            (c.LeftVersionId == bId || c.RightVersionId == bId) &&
            (c.LeftVersionId == otherVersionId || c.RightVersionId == otherVersionId)));
        var resolvePath = $"{root}/conflicts/{conflict.Id}/resolve";
        await Post(TestData.Rita, resolvePath, new DesignBasisEndpoints.ResolveConflictBody(Guid.NewGuid(),
            conflict.RowVersion, otherVersionId, Version<DesignBasisVersion>(otherVersionId), "Marc selected the reviewed peer basis"), 404);
        await Post(TestData.Marc, resolvePath, new DesignBasisEndpoints.ResolveConflictBody(Guid.NewGuid(),
            conflict.RowVersion, otherVersionId, Version<DesignBasisVersion>(otherVersionId), "Peer basis is not a replacement"), 400);
        await (await f.As(TestData.Rita).GetAsync($"{root}/{id}")).Json(404);
        var c = await Post(TestData.Marc, $"{root}/{id}/propose", new DesignBasisEndpoints.ProposeBody(Guid.NewGuid(),
            Version<DesignBasisEntry>(id), Version<DesignBasisVersion>(bId), b with { NumericValue = 130, DeclaredRevision = "C" },
            "Further revised report", otherId));
        var cId = c.G("id");
        await Post(TestData.Marc, $"{root}/{id}/versions/{cId}/confirm", new DesignBasisEndpoints.ConfirmBody(Guid.NewGuid(),
            Version<DesignBasisEntry>(id), Version<DesignBasisVersion>(cId), "Revision C reviewed"));
        Assert.Single(f.Db(db => db.BasisImpactAssessments.Where(i => i.BasisUseId == secondUse.G("id") &&
            i.OldVersionId == versionId && i.NewVersionId == cId && i.Status == AssessmentStatus.Pending).ToList()));
        Assert.Single(f.Db(db => db.BasisImpactAssessments.Where(i => i.OldVersionId == bId &&
            i.NewVersionId == cId && i.Status == AssessmentStatus.Pending).ToList()));
        var resolved = await Post(TestData.Marc, resolvePath, new DesignBasisEndpoints.ResolveConflictBody(Guid.NewGuid(),
            conflict.RowVersion, cId, Version<DesignBasisVersion>(cId), "Revision C is the authorised replacement"));
        Assert.Equal(conflict.Id, resolved.G("id"));
        var savedConflict = f.Db(db => db.BasisConflicts.Single(c => c.Id == conflict.Id));
        Assert.True(savedConflict.Resolved);
        Assert.Equal(cId, savedConflict.ResolutionVersionId);
        Assert.Equal(data.User(TestData.Marc), savedConflict.ResolvedBy);
        await Post(TestData.Marc, resolvePath, new DesignBasisEndpoints.ResolveConflictBody(Guid.NewGuid(),
            conflict.RowVersion, cId, Version<DesignBasisVersion>(cId), "Duplicate resolution"), 409);
    }

    [Fact]
    public async Task Proposed_assumption_requires_approved_unexpired_scope_for_its_consumer()
    {
        var project = await data.Project();
        var civil = data.ProjectDiscipline(project.Id, "Civil");
        var owner = data.User(TestData.Alex);
        var root = $"/api/v1/projects/{project.Id}/design-basis";
        var input = new DesignBasisEndpoints.VersionInput("Site grading", "Assume existing utility depth", null, null,
            null, null, null, null, new DateOnly(2020, 1, 1), null);
        var entry = await Post(TestData.Pm, root, new DesignBasisEndpoints.CreateBody(Guid.NewGuid(), BasisKind.Assumption,
            "Utility depth", owner, civil, data.User(TestData.Marc), input, null));
        var id = entry.G("id");
        var overdue = await (await f.As(TestData.Pm).GetAsync(root + "?overdue=true")).Json();
        Assert.Contains(overdue["items"]!.AsArray(), row => row!.G("id") == id);
        var versionId = f.Db(db => db.DesignBasisVersions.Single(v => v.EntryId == id).Id);
        var task = await data.NewTask(project.Id, extra: new { assigneeId = owner });
        var use = new DesignBasisEndpoints.UseBody(Guid.NewGuid(), versionId, "Task", task.G("id"), "Preliminary layout");
        await Post(TestData.Alex, $"{root}/{id}/uses", use, 400);
        var proceedPath = $"{root}/{id}/versions/{versionId}/proceed";
        await Post(TestData.Marc, proceedPath, new DesignBasisEndpoints.DispositionBody(Guid.NewGuid(),
            Version<DesignBasisVersion>(versionId), "Site grading", owner, new DateOnly(2020, 1, 1), "Confirm before issue"), 400);
        await Post(TestData.Marc, proceedPath, new DesignBasisEndpoints.DispositionBody(Guid.NewGuid(),
            Version<DesignBasisVersion>(versionId), "Site grading", owner, new DateOnly(2027, 1, 1), "Confirm before issue"));
        await Post(TestData.Alex, $"{root}/{id}/uses", use with { RequestId = Guid.NewGuid() });
        await Post(TestData.Alex, $"{root}/{id}/versions/{versionId}/edit",
            new DesignBasisEndpoints.EditProposedBody(Guid.NewGuid(), Version<DesignBasisEntry>(id),
                Version<DesignBasisVersion>(versionId), input with { Statement = "Changed after approval" },
                "Try to alter an approved assumption"), 400);
        Assert.Equal(input.Statement, f.Db(db => db.DesignBasisVersions.Single(v => v.Id == versionId).Statement));
        Assert.Equal(BasisStatus.Proposed, f.Db(db => db.DesignBasisVersions.Single(v => v.Id == versionId).Status));
        Assert.Single(f.Db(db => db.BasisAssumptionDispositions.Where(d => d.VersionId == versionId && d.OwnerId == owner).ToList()));
    }

    [Fact]
    public async Task Withdrawal_marks_confirmed_basis_with_consumers_and_requires_assessment()
    {
        var project = await data.Project();
        var civil = data.ProjectDiscipline(project.Id, "Civil");
        var owner = data.User(TestData.Alex);
        var root = $"/api/v1/projects/{project.Id}/design-basis";
        var input = new DesignBasisEndpoints.VersionInput("Bridge / Pier 1", "Allowable bearing pressure", 100, "kPa",
            "Geotechnical report", "GEO-W-1", "https://example.test/geotech", "A", new DateOnly(2026, 10, 5), null);
        var created = await Post(TestData.Pm, root, new DesignBasisEndpoints.CreateBody(Guid.NewGuid(), BasisKind.Criterion,
            "Withdrawal criterion", owner, civil, data.User(TestData.Marc), input, null));
        var entryId = created.G("id");
        var versionId = f.Db(db => db.DesignBasisVersions.Single(v => v.EntryId == entryId).Id);
        await Post(TestData.Marc, $"{root}/{entryId}/versions/{versionId}/confirm",
            new DesignBasisEndpoints.ConfirmBody(Guid.NewGuid(), Version<DesignBasisEntry>(entryId),
                Version<DesignBasisVersion>(versionId), "Source checked"));
        var proposed = await Post(TestData.Marc, $"{root}/{entryId}/propose", new DesignBasisEndpoints.ProposeBody(Guid.NewGuid(),
            Version<DesignBasisEntry>(entryId), Version<DesignBasisVersion>(versionId), input with { DeclaredRevision = "B", NumericValue = 110 },
            "Candidate replacement"));
        var proposedId = proposed.G("id");
        await Post(TestData.Marc, $"{root}/{entryId}/versions/{versionId}/withdraw",
            new DesignBasisEndpoints.WithdrawBody(Guid.NewGuid(), Version<DesignBasisEntry>(entryId),
                Version<DesignBasisVersion>(versionId), "Must resolve proposed replacement first"), 400);
        await Post(TestData.Marc, $"{root}/{entryId}/versions/{proposedId}/withdraw",
            new DesignBasisEndpoints.WithdrawBody(Guid.NewGuid(), Version<DesignBasisEntry>(entryId),
                Version<DesignBasisVersion>(proposedId), "Candidate replacement withdrawn"));
        var task = await data.NewTask(project.Id, extra: new { assigneeId = owner });
        var use = await Post(TestData.Alex, $"{root}/{entryId}/uses", new DesignBasisEndpoints.UseBody(Guid.NewGuid(), versionId,
            "Task", task.G("id"), "Foundation sizing"));
        await Post(TestData.Marc, $"{root}/{entryId}/versions/{versionId}/withdraw",
            new DesignBasisEndpoints.WithdrawBody(Guid.NewGuid(), Version<DesignBasisEntry>(entryId),
                Version<DesignBasisVersion>(versionId), "Geotechnical basis withdrawn pending replacement"));
        var saved = f.Db(db => new
        {
            Entry = db.DesignBasisEntries.Single(e => e.Id == entryId),
            Version = db.DesignBasisVersions.Single(v => v.Id == versionId),
            Assessments = db.BasisImpactAssessments.Where(a => a.BasisUseId == use.G("id")).ToList()
        });
        Assert.Null(saved.Entry.CurrentVersionId);
        Assert.Equal(BasisStatus.Withdrawn, saved.Version.Status);
        var assessment = Assert.Single(saved.Assessments);
        Assert.Null(assessment.NewVersionId);
        Assert.Equal(AssessmentStatus.Pending, assessment.Status);
        await Post(TestData.Alex, $"{root}/{entryId}/uses", new DesignBasisEndpoints.UseBody(Guid.NewGuid(), versionId,
            "Task", task.G("id"), "Should be blocked"), 400);
    }

    [Fact]
    public async Task Proposed_basis_can_be_withdrawn_without_consumer_assessment()
    {
        var project = await data.Project();
        var civil = data.ProjectDiscipline(project.Id, "Civil");
        var owner = data.User(TestData.Alex);
        var root = $"/api/v1/projects/{project.Id}/design-basis";
        var input = new DesignBasisEndpoints.VersionInput("Site grading", "Assume existing utility depth", null, null,
            null, null, null, null, new DateOnly(2026, 10, 5), null);
        var created = await Post(TestData.Pm, root, new DesignBasisEndpoints.CreateBody(Guid.NewGuid(), BasisKind.Assumption,
            "Withdrawn assumption", owner, civil, data.User(TestData.Marc), input, null));
        var entryId = created.G("id");
        var versionId = f.Db(db => db.DesignBasisVersions.Single(v => v.EntryId == entryId).Id);
        await Post(TestData.Marc, $"{root}/{entryId}/versions/{versionId}/withdraw",
            new DesignBasisEndpoints.WithdrawBody(Guid.NewGuid(), Version<DesignBasisEntry>(entryId),
                Version<DesignBasisVersion>(versionId), "Assumption no longer applicable"));
        Assert.Equal(BasisStatus.Withdrawn, f.Db(db => db.DesignBasisVersions.Single(v => v.Id == versionId).Status));
        Assert.Empty(f.Db(db => db.BasisImpactAssessments.Where(a => a.OldVersionId == versionId).ToList()));
    }

    [Fact]
    public async Task Reopening_linked_decision_creates_consumer_assessment()
    {
        var project = await data.Project();
        var civil = data.ProjectDiscipline(project.Id, "Civil");
        var owner = data.User(TestData.Alex);
        var root = $"/api/v1/projects/{project.Id}/design-basis";
        var decision = await (await f.As(TestData.Pm).Post($"/api/v1/projects/{project.Id}/decisions", new
        {
            subject = "Confirm linked bearing basis", description = "Basis source decision", ownerUserId = owner,
            requiredByDate = "2026-10-01", impactLevel = "Medium", impactDescription = "Design depends on this decision"
        })).Json(201);
        var decisionId = decision.G("id");
        var foreignProject = await data.Project();
        var foreignDecision = await (await f.As(TestData.Pm).Post($"/api/v1/projects/{foreignProject.Id}/decisions", new
        {
            subject = "Foreign project source", description = "Cannot link across projects", ownerUserId = owner,
            requiredByDate = "2026-10-01", impactLevel = "Medium", impactDescription = "Reference validation"
        })).Json(201);
        var input = new DesignBasisEndpoints.VersionInput("Bridge / Pier 1", "Allowable bearing pressure", 100, "kPa",
            "Geotechnical report", "GEO-D-1", "https://example.test/geotech", "A", new DateOnly(2026, 10, 5), decisionId);
        await Post(TestData.Pm, root, new DesignBasisEndpoints.CreateBody(Guid.NewGuid(), BasisKind.Criterion,
            "Invalid foreign source", owner, civil, data.User(TestData.Marc), input with { DecisionId = foreignDecision.G("id") }, null), 400);
        Assert.False(f.Db(db => db.DesignBasisEntries.Any(e => e.ProjectId == project.Id && e.Title == "Invalid foreign source")));
        var created = await Post(TestData.Pm, root, new DesignBasisEndpoints.CreateBody(Guid.NewGuid(), BasisKind.Criterion,
            "Decision linked criterion", owner, civil, data.User(TestData.Marc), input, null));
        var entryId = created.G("id");
        var versionId = f.Db(db => db.DesignBasisVersions.Single(v => v.EntryId == entryId).Id);
        await Post(TestData.Alex, $"{root}/{entryId}/versions/{versionId}/edit", new DesignBasisEndpoints.EditProposedBody(
            Guid.NewGuid(), Version<DesignBasisEntry>(entryId), Version<DesignBasisVersion>(versionId),
            input with { DecisionId = foreignDecision.G("id") }, "Try to link a foreign source decision"), 400);
        Assert.Equal(decisionId, f.Db(db => db.DesignBasisVersions.Single(v => v.Id == versionId).DecisionId));
        await Post(TestData.Marc, $"{root}/{entryId}/versions/{versionId}/confirm",
            new DesignBasisEndpoints.ConfirmBody(Guid.NewGuid(), Version<DesignBasisEntry>(entryId),
                Version<DesignBasisVersion>(versionId), "Source checked"));
        var task = await data.NewTask(project.Id, extra: new { assigneeId = owner });
        var use = await Post(TestData.Alex, $"{root}/{entryId}/uses", new DesignBasisEndpoints.UseBody(Guid.NewGuid(), versionId,
            "Task", task.G("id"), "Foundation sizing"));
        var transition = $"/api/v1/decisions/{decisionId}/transition";
        await (await f.As(TestData.Pm).Post(transition, new { toStatus = "Decided", decisionText = "Use the linked basis", decisionDate = "2026-09-14", rowVersion = f.Db(db => db.Decisions.Single(d => d.Id == decisionId).RowVersion) })).Json(200);
        await (await f.As(TestData.Pm).Post(transition, new { toStatus = "Pending", reason = "Source decision requires revalidation", rowVersion = f.Db(db => db.Decisions.Single(d => d.Id == decisionId).RowVersion) })).Json(200);
        var assessment = f.Db(db => db.BasisImpactAssessments.Single(a => a.BasisUseId == use.G("id")));
        Assert.Equal(versionId, assessment.OldVersionId);
        Assert.Null(assessment.NewVersionId);
        Assert.Equal(AssessmentStatus.Pending, assessment.Status);
        await (await f.As(TestData.Pm).Post(transition, new { toStatus = "Decided", decisionText = "Revalidated linked basis", decisionDate = "2026-09-14", rowVersion = f.Db(db => db.Decisions.Single(d => d.Id == decisionId).RowVersion) })).Json(200);
        Assert.Single(f.Db(db => db.BasisImpactAssessments.Where(a => a.BasisUseId == use.G("id") && a.NewVersionId == null).ToList()));
    }
}
