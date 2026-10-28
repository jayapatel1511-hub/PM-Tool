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
    async Task<JsonNode> Get(string who, string path) => await (await f.As(who).GetAsync(path)).Json();
    int Version<T>(Guid id) where T : Audited => f.Db(db => db.Set<T>().AsNoTracking().Single(x => x.Id == id).RowVersion);

    [Fact]
    public async Task Basis_discipline_lists_match_create_basis_for_every_role()
    {
        var project = await data.Project();
        var disciplines = new[] { data.ProjectDiscipline(project.Id, "Civil"), data.ProjectDiscipline(project.Id, "Electrical") };
        var root = $"/api/v1/projects/{project.Id}/design-basis";

        foreach (var who in new[] { TestData.Admin, TestData.Pm, TestData.Marc, TestData.Alex, TestData.Omar })
        {
            var detail = await Get(who, $"/api/v1/projects/{project.Id}");
            var allowed = detail["permissions"]!["createBasisIn"]!.AsArray().Select(x => x!.GetValue<Guid>()).ToHashSet();
            var owner = data.User(who == TestData.Admin ? TestData.Pm : who);
            foreach (var discipline in disciplines)
            {
                var body = new DesignBasisEndpoints.CreateBody(Guid.NewGuid(), BasisKind.Assumption,
                    $"Parity probe {who} {discipline}", owner, discipline, null,
                    new DesignBasisEndpoints.VersionInput("Scope", "Parity probe", null, null, null, null, null, null,
                        new DateOnly(2026, 10, 5), null), null);
                await Post(who, root, body, allowed.Contains(discipline) ? 200 : 403);
            }
        }
    }

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
    public async Task Register_and_detail_keep_their_property_names_order_nulls_and_flags() // the typed records must serialise to the bytes the Design basis page already reads
    {
        var project = await data.Project(); var root = $"/api/v1/projects/{project.Id}/design-basis";
        var civil = data.ProjectDiscipline(project.Id, "Civil"); var alex = data.User(TestData.Alex);
        var input = new DesignBasisEndpoints.VersionInput("Bridge / Pier 1", "Allowable bearing pressure", 100, "kPa", "Geotechnical report", "GEO-S-1",
            "https://example.test/geotech", "A", new DateOnly(2026, 10, 5), null);
        async Task Confirm(Guid entryId, Guid versionId, string who = TestData.Marc) => await Post(who, $"{root}/{entryId}/versions/{versionId}/confirm",
            new DesignBasisEndpoints.ConfirmBody(Guid.NewGuid(), Version<DesignBasisEntry>(entryId), Version<DesignBasisVersion>(versionId), "Source checked"));
        async Task<(Guid Entry, Guid Version)> Entry(string kind, string title, DesignBasisEndpoints.VersionInput version, Guid? inspected = null)
        {
            var entry = await Post(TestData.Pm, root, new DesignBasisEndpoints.CreateBody(Guid.NewGuid(), kind, title, alex, civil, data.User(TestData.Marc), version, null, inspected));
            var id = entry.G("id"); return (id, f.Db(db => db.DesignBasisVersions.Single(v => v.EntryId == id).Id));
        }
        // E1: a confirmed criterion with a use, replaced by revision B, answered by the consumer, and in conflict with a peer.
        var (e1, v1) = await Entry(BasisKind.Criterion, "Bearing pressure", input);
        await Confirm(e1, v1);
        var task = await data.NewTask(project.Id, extra: new { assigneeId = alex });
        var use = await Post(TestData.Alex, $"{root}/{e1}/uses", new DesignBasisEndpoints.UseBody(Guid.NewGuid(), v1, "Task", task.G("id"), "Foundation sizing"));
        var b = input with { NumericValue = 125, DeclaredRevision = "B" };
        var proposed = await Post(TestData.Marc, $"{root}/{e1}/propose", new DesignBasisEndpoints.ProposeBody(Guid.NewGuid(), Version<DesignBasisEntry>(e1), Version<DesignBasisVersion>(v1), b, "New report"));
        var v2 = proposed.G("id");
        var pending = await Get(TestData.Pm, $"{root}/{e1}");
        await Confirm(e1, v2);
        var (peer, peerVersion) = await Entry(BasisKind.Criterion, "Bearing pressure", b with { NumericValue = 150 }, e1);
        await Confirm(peer, peerVersion);
        var impact = f.Db(db => db.BasisImpactAssessments.Single(i => i.BasisUseId == use.G("id")));
        var beforeDecision = await Get(TestData.Alex, $"{root}/{e1}");
        await Post(TestData.Alex, $"{root}/{e1}/impacts/{impact.Id}/decide", new DesignBasisEndpoints.ImpactBody(Guid.NewGuid(), impact.RowVersion, Version<BasisUse>(use.G("id")),
            Version<DesignBasisVersion>(v2), f.Db(db => db.Tasks.Single(t => t.Id == task.G("id")).RowVersion), "Adopt", "Foundation sizing revised to report B", "https://example.test/review-B"));
        // E2: an assumption approved to proceed; E3: a confirmed criterion withdrawn under a use; E4: a proposed criterion withdrawn.
        var (e2, a1) = await Entry(BasisKind.Assumption, "Utility depth", input with { NumericValue = null, Units = null, SourceSystem = null, StableSourceId = null, SourceUrl = null, DeclaredRevision = null });
        await Post(TestData.Marc, $"{root}/{e2}/versions/{a1}/proceed", new DesignBasisEndpoints.DispositionBody(Guid.NewGuid(), Version<DesignBasisVersion>(a1), input.Scope, alex, new DateOnly(2027, 1, 1), "Confirm before issue"));
        var (e3, c1) = await Entry(BasisKind.Criterion, "Soil cover", input with { Scope = "Bridge / Pier 2" });
        await Confirm(e3, c1);
        var second = await data.NewTask(project.Id, extra: new { assigneeId = alex });
        await Post(TestData.Alex, $"{root}/{e3}/uses", new DesignBasisEndpoints.UseBody(Guid.NewGuid(), c1, "Task", second.G("id"), "Cover check"));
        await Post(TestData.Marc, $"{root}/{e3}/versions/{c1}/withdraw", new DesignBasisEndpoints.WithdrawBody(Guid.NewGuid(), Version<DesignBasisEntry>(e3), Version<DesignBasisVersion>(c1), "Basis withdrawn pending replacement"));
        var (e4, d1) = await Entry(BasisKind.Criterion, "Frost depth", input with { Scope = "Bridge / Pier 3" });
        await Post(TestData.Marc, $"{root}/{e4}/versions/{d1}/withdraw", new DesignBasisEndpoints.WithdrawBody(Guid.NewGuid(), Version<DesignBasisEntry>(e4), Version<DesignBasisVersion>(d1), "Never applicable"));

        var list = await Get(TestData.Pm, root);
        Assert.Equal(["items", "page", "pageSize", "totalCount", "sort", "filters"], list.AsObject().Select(x => x.Key));
        Assert.Equal((1, 50, 5), (list.I("page"), list.I("pageSize"), list.I("totalCount")));
        var rows = list["items"]!.AsArray(); Assert.Equal([e1, peer, e2, e3, e4], rows.Select(r => r!.G("id"))); // the register's order is the entry sequence
        Assert.All(rows, r => Assert.Equal(ListKeys, r!.AsObject().Select(x => x.Key)));
        Assert.Equal(("Confirmed", "Bridge / Pier 1", "Confirmed", v2, 1), (rows[0]!.S("currentStatus"), rows[0]!.S("currentScope"), rows[0]!.S("latestStatus"), rows[0]!.G("latestVersionId"), rows[0]!.I("conflictCount")));
        Assert.Equal((v2, 2, "2026-10-05"), (rows[0]!.G("currentVersionId"), rows[0]!.I("rowVersion"), rows[0]!.S("confirmationDueDate")));
        Assert.Null(rows[2]!["currentVersionId"]); Assert.Null(rows[2]!["currentStatus"]); Assert.Null(rows[2]!["currentScope"]); // null, not omitted
        Assert.Equal(("Proposed", a1, 0), (rows[2]!.S("latestStatus"), rows[2]!.G("latestVersionId"), rows[2]!.I("conflictCount")));
        Assert.Equal(("Withdrawn", c1, "Withdrawn", d1), (rows[3]!.S("latestStatus"), rows[3]!.G("latestVersionId"), rows[4]!.S("latestStatus"), rows[4]!.G("latestVersionId")));
        var generated = await Get(TestData.Pm, root + $"?Kind=Criterion&Status=Confirmed&DisciplineId={civil}&Scope=Pier&Overdue=false&AffectedWorkId={task.G("id")}&page=1&pageSize=1"); // the names the generated client sends
        Assert.Equal((e1, 1, 1), (Assert.Single(generated["items"]!.AsArray())!.G("id"), generated.I("pageSize"), generated.I("totalCount")));
        var csv = (await (await f.As(TestData.Pm).GetAsync(root + "/export?format=csv")).Content.ReadAsStringAsync()).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal("Key,Name,Kind,Discipline,Owner,Version,Status,Scope,Value or statement,Numeric value,Units,Source system,Stable source ID,Source URL,Declared source revision,Confirmation due date", csv[0]);
        Assert.Equal(7, csv.Length); Assert.EndsWith("-B001,Bearing pressure,Criterion,Civil,Alex Chen,1,Superseded,Bridge / Pier 1,Allowable bearing pressure,100.000000,kPa,Geotechnical report,GEO-S-1,https://example.test/geotech,A,2026-10-05", csv[1]);
        Assert.EndsWith("-B003,Utility depth,Assumption,Civil,Alex Chen,1,Proposed,Bridge / Pier 1,Allowable bearing pressure,,,,,,,2026-10-05", csv[4]);

        Assert.Equal(DetailKeys, pending.AsObject().Select(x => x.Key));
        Assert.Equal([BasisStatus.Confirmed, BasisStatus.Proposed], pending["versions"]!.AsArray().Select(v => v!["version"]!.S("status")));
        Assert.Empty(pending["impacts"]!.AsArray()); Assert.Empty(pending["conflicts"]!.AsArray()); Assert.Empty(pending["dispositions"]!.AsArray()); // [], not omitted
        Assert.Equal((2, 1), (beforeDecision["versions"]!.AsArray().Count, beforeDecision["uses"]!.AsArray().Count));
        Assert.Equal("Pending Assessment", Assert.Single(beforeDecision["impacts"]!.AsArray())!.S("status"));

        var detail = await Get(TestData.Pm, $"{root}/{e1}");
        Assert.Equal(DetailKeys, detail.AsObject().Select(x => x.Key));
        Assert.Equal(EntryKeys, detail["entry"]!.AsObject().Select(x => x.Key));
        Assert.Equal((BasisKind.Criterion, "DesignBasisEntry", civil, v2, "Bearing pressure"), (detail["entry"]!.S("kind"), detail["entry"]!.S("auditType"), detail["entry"]!.G("auditDisciplineId"), detail["entry"]!.G("currentVersionId"), detail["entry"]!.S("auditName")));
        Assert.Equal(data.User(TestData.Marc), detail["entry"]!.G("independentApproverId")); Assert.Null(detail["entry"]!["deletedAt"]); Assert.Null(detail["entry"]!["deletedBy"]);
        var versions = detail["versions"]!.AsArray(); Assert.Equal(2, versions.Count);
        Assert.All(versions, v => { Assert.Equal(["version", "sourceMissing"], v!.AsObject().Select(x => x.Key)); Assert.Equal(VersionKeys, v["version"]!.AsObject().Select(x => x.Key)); Assert.False(v["sourceMissing"]!.GetValue<bool>()); });
        Assert.Equal((BasisStatus.Superseded, 1, BasisStatus.Confirmed, 2), (versions[0]!["version"]!.S("status"), versions[0]!["version"]!.I("number"), versions[1]!["version"]!.S("status"), versions[1]!["version"]!.I("number")));
        Assert.Null(versions[0]!["version"]!["supersedesVersionId"]); Assert.Null(versions[0]!["version"]!["decisionId"]); Assert.Equal(v1, versions[1]!["version"]!.G("supersedesVersionId"));
        Assert.Equal(("DesignBasisVersion", "Allowable bearing pressure", "Source checked", 125m), (versions[1]!["version"]!.S("auditType"), versions[1]!["version"]!.S("auditName"), versions[1]!["version"]!.S("confirmationRationale"), versions[1]!["version"]!["numericValue"]!.GetValue<decimal>()));
        Assert.Null(versions[1]!["version"]!["auditKey"]);
        var uses = detail["uses"]!.AsArray(); Assert.Equal(2, uses.Count);
        Assert.All(uses, u => Assert.Equal(UseKeys, u!.AsObject().Select(x => x.Key)));
        Assert.Equal(("Task", "Foundation sizing", true), (uses.Single(u => u!.G("versionId") == v2)!.S("targetType"), uses.Single(u => u!.G("versionId") == v2)!.S("intendedUse"), uses.Single(u => u!.G("versionId") == v2)!["isCurrent"]!.GetValue<bool>()));
        Assert.False(uses.Single(u => u!.G("versionId") == v1)!["isCurrent"]!.GetValue<bool>());
        var decided = Assert.Single(detail["impacts"]!.AsArray())!; Assert.Equal(ImpactKeys, decided.AsObject().Select(x => x.Key));
        Assert.Equal((AssessmentStatus.Resolved, "Foundation sizing revised to report B", "https://example.test/review-B", "BasisImpactAssessment"), (decided.S("status"), decided.S("rationale"), decided.S("evidenceUrl"), decided.S("auditType")));
        Assert.Equal((v1, v2, alex), (decided.G("oldVersionId"), decided.G("newVersionId"), decided.G("decidedBy"))); Assert.Null(decided["withdrawalVersionId"]); Assert.NotNull(decided["decidedAt"]); Assert.Null(decided["auditKey"]);
        var conflict = Assert.Single(detail["conflicts"]!.AsArray())!; Assert.Equal(["id", "resolved", "rowVersion", "left", "right"], conflict.AsObject().Select(x => x.Key));
        Assert.Equal(SideKeys, conflict["left"]!.AsObject().Select(x => x.Key)); Assert.Equal(SideKeys, conflict["right"]!.AsObject().Select(x => x.Key));
        Assert.False(conflict["resolved"]!.GetValue<bool>()); Assert.Equal(2, new[] { conflict["left"]!.G("versionId"), conflict["right"]!.G("versionId") }.Intersect([v2, peerVersion]).Count());
        Assert.Equal([125m, 150m], new[] { conflict["left"]!, conflict["right"]! }.Select(side => side["numericValue"]!.GetValue<decimal>()).Order()); // either side first: the pair is ordered by id
        Assert.Equal(("kPa", "Bridge / Pier 1", "Allowable bearing pressure"), (conflict["left"]!.S("units"), conflict["left"]!.S("scope"), conflict["left"]!.S("statement")));
        Assert.Empty(detail["dispositions"]!.AsArray());
        foreach (var (who, flags) in new[] { (TestData.Pm, (true, true, false)), (TestData.Marc, (true, true, true)), (TestData.Alex, (false, true, false)), (TestData.Omar, (false, false, false)), (TestData.Jill, (false, false, false)) })
        {
            var seen = await Get(who, $"{root}/{e1}"); Assert.Equal(DetailKeys, seen.AsObject().Select(x => x.Key));
            Assert.Equal(flags, (seen["canManage"]!.GetValue<bool>(), seen["canEditProposed"]!.GetValue<bool>(), seen["canConfirm"]!.GetValue<bool>()));
        }

        var assumption = await Get(TestData.Pm, $"{root}/{e2}");
        var proposedVersion = Assert.Single(assumption["versions"]!.AsArray())!;
        Assert.Equal((BasisStatus.Proposed, true), (proposedVersion["version"]!.S("status"), proposedVersion["sourceMissing"]!.GetValue<bool>())); // an incomplete source is flagged
        Assert.Null(proposedVersion["version"]!["numericValue"]); Assert.Null(proposedVersion["version"]!["units"]); Assert.Null(proposedVersion["version"]!["confirmedAt"]);
        Assert.Null(assumption["entry"]!["currentVersionId"]); Assert.Empty(assumption["uses"]!.AsArray());
        var disposition = Assert.Single(assumption["dispositions"]!.AsArray())!; Assert.Equal(DispositionKeys, disposition.AsObject().Select(x => x.Key));
        Assert.Equal((a1, input.Scope, alex, "2027-01-01", "Confirm before issue"), (disposition.G("versionId"), disposition.S("scope"), disposition.G("ownerId"), disposition.S("expiresOn"), disposition.S("reason")));
        Assert.Equal((data.User(TestData.Marc), "BasisAssumptionDisposition"), (disposition.G("approvedBy"), disposition.S("auditType")));
        var withdrawn = Assert.Single((await Get(TestData.Pm, $"{root}/{e3}"))["impacts"]!.AsArray())!; // a withdrawal has no replacement
        Assert.Equal(ImpactKeys, withdrawn.AsObject().Select(x => x.Key));
        Assert.Equal((AssessmentStatus.Pending, c1, c1), (withdrawn.S("status"), withdrawn.G("oldVersionId"), withdrawn.G("withdrawalVersionId"))); Assert.Null(withdrawn["newVersionId"]); Assert.Null(withdrawn["rationale"]); Assert.Null(withdrawn["decidedAt"]);
        var never = await Get(TestData.Pm, $"{root}/{e4}"); Assert.Equal(DetailKeys, never.AsObject().Select(x => x.Key));
        Assert.Equal(BasisStatus.Withdrawn, Assert.Single(never["versions"]!.AsArray())!["version"]!.S("status")); Assert.Empty(never["impacts"]!.AsArray());
    }

    static readonly string[] DetailKeys = ["entry", "versions", "uses", "impacts", "conflicts", "dispositions", "canManage", "canEditProposed", "canConfirm"];
    static readonly string[] ListKeys = ["id", "key", "title", "kind", "ownerId", "projectDisciplineId", "currentVersionId", "rowVersion", "currentStatus", "currentScope", "latestStatus", "latestVersionId", "confirmationDueDate", "conflictCount"];
    static readonly string[] EntryKeys = ["kind", "title", "ownerId", "projectDisciplineId", "independentApproverId", "currentVersionId", "auditType", "auditProjectId", "auditKey", "auditName", "auditDisciplineId",
        "projectId", "key", "seq", "deletedAt", "deletedBy", "createdAt", "createdBy", "updatedAt", "updatedBy", "rowVersion", "id"];
    static readonly string[] VersionKeys = ["entryId", "number", "supersedesVersionId", "status", "scope", "statement", "numericValue", "units", "sourceSystem", "stableSourceId", "sourceUrl", "declaredRevision",
        "confirmationDueDate", "decisionId", "confirmedBy", "confirmedAt", "confirmationRationale", "auditType", "auditName", "projectId", "auditProjectId", "auditKey", "createdAt", "createdBy", "updatedAt", "updatedBy", "rowVersion", "id"];
    static readonly string[] UseKeys = ["id", "versionId", "targetType", "targetId", "ownerId", "intendedUse", "rowVersion", "isCurrent"];
    static readonly string[] ImpactKeys = ["basisUseId", "oldVersionId", "newVersionId", "withdrawalVersionId", "ownerId", "status", "rationale", "evidenceUrl", "decidedBy", "decidedAt", "auditType", "projectId",
        "auditProjectId", "auditKey", "auditName", "createdAt", "createdBy", "updatedAt", "updatedBy", "rowVersion", "id"];
    static readonly string[] SideKeys = ["versionId", "entryKey", "scope", "statement", "numericValue", "units"];
    static readonly string[] DispositionKeys = ["versionId", "scope", "ownerId", "approvedBy", "expiresOn", "reason", "auditType", "projectId", "auditProjectId", "auditKey", "auditName", "createdAt", "createdBy",
        "updatedAt", "updatedBy", "rowVersion", "id"];

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
