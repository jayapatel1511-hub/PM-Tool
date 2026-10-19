using System.Net;
using System.Text.Json.Nodes;
using Hub.Api.Data;
using Hub.Api.Features;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

/// Packet 012: project templates — building and publishing (US1), creating a project from Appendix A (US2), adding a
/// discipline pack (US3) and snapshot semantics (US4).
[Collection("api")]
public sealed class TemplatesTests(HubFactory f)
{
    readonly TestData d = new(f);
    Guid U(string email) => d.User(email);

    async Task<Guid> Reference() => await f.DbAsync(db => ReferenceTemplate.Seed(db));
    async Task<JsonNode> Template(Guid id, string as_ = TestData.Pm) => await f.As(as_).GetAsync($"/api/v1/templates/{id}").Result.Json();

    /// Appendix A.2: contractual dates for M03–M07, M08–M11 left blank, the six default disciplines with leads.
    async Task<(HttpResponseMessage Res, string Number)> FromTemplate(Guid templateId, string[]? disciplines = null, Guid? tplOverride = null)
    {
        var tpl = await Template(templateId);
        var ms = tpl["milestones"]!.AsArray().Select(m => m!.S("ref")).ToList();
        var dates = new JsonObject
        {
            [ms[2]] = "2027-01-15", [ms[3]] = "2027-03-12", [ms[4]] = "2027-05-14", [ms[5]] = "2027-06-25", [ms[6]] = "2027-07-16",
            [ms[7]] = null, [ms[8]] = null, [ms[9]] = null, [ms[10]] = null,
        };
        var leads = new Dictionary<string, string> { ["Civil"] = TestData.Marc, ["Geotechnical"] = TestData.Omar, ["Electrical"] = TestData.Omar, ["Survey"] = TestData.Sam,
            ["Environmental"] = TestData.Diane, ["Project Management"] = TestData.Pm, ["Transportation"] = TestData.Rita };
        var chosen = disciplines ?? ["Project Management", "Survey", "Civil", "Geotechnical", "Electrical", "Environmental"];
        var discs = new List<object>();
        foreach (var name in chosen) discs.Add(new { disciplineId = await d.Discipline(name), leadUserId = (Guid?)U(leads[name]) });
        var number = TestData.Number();
        var res = await f.As(TestData.Pm).Post("/api/v1/projects", new
        {
            projectNumber = number, name = "DCC Dundurn Roads " + number, clientId = await d.Client(), officeId = await d.Office(), startDate = "2026-10-05",
            disciplines = discs, templateId = tplOverride ?? templateId, template = new { milestoneDates = dates },
        });
        return (res, number);
    }

    [Fact, Trait("flag-off", "template_roles"), Trait("flag-on", "template_roles")] // AC-PRO-10, AC-PRO-11, AC-PRO-23
    public async Task Template_roles_preview_copy_preserve_and_instantiate_only_for_whole_projects_when_enabled()
    {
        var admin = f.As(TestData.Admin); var civil = await d.Discipline("Civil");
        async Task Flag(bool value) => (await admin.Put("/api/v1/admin/settings/flag.template_roles", new { value })).EnsureSuccessStatusCode();
        await Flag(true);
        try
        {
            var template = await (await admin.Post("/api/v1/templates", new { name = "Synthetic role template " + Guid.NewGuid().ToString("N")[..6] })).Json(201);
            var id = template.G("id");
            var saved = await (await admin.Put($"/api/v1/templates/{id}/structure", new
            {
                rowVersion = template.I("rowVersion"), disciplines = new[] { new { disciplineId = civil, isDefaultIncluded = true } },
                milestones = new[] {
                    new { @ref = "start", name = "Start", milestoneType = MilestoneType.Kickoff, anchor = TemplateAnchor.ProjectStart, offset = (int?)0, isClientFacing = false },
                    new { @ref = "end", name = "End", milestoneType = MilestoneType.Closeout, anchor = TemplateAnchor.ProjectStart, offset = (int?)10, isClientFacing = false },
                    new { @ref = "unknown", name = "Not dated", milestoneType = MilestoneType.Other, anchor = TemplateAnchor.None, offset = (int?)null, isClientFacing = false } },
                deliverables = Array.Empty<object>(), tasks = Array.Empty<object>(), dependencies = Array.Empty<object>(),
                roles = new[] {
                    new TemplateEndpoints.TemplateRoleIn("role", civil, "Civil reviewer", "start", 0, "end", 0, 12m, "Synthetic staffing demand"),
                    new TemplateEndpoints.TemplateRoleIn("skip", null, "Undated demand", "unknown", 0, "end", 0, 4m, null) },
            })).Json();
            Assert.Equal(2, saved["roles"]!.AsArray().Count);
            var omission = saved.DeepClone().AsObject(); omission.Remove("roles");
            saved = await (await admin.Put($"/api/v1/templates/{id}/structure", omission)).Json();
            Assert.Equal(2, saved["roles"]!.AsArray().Count); // Older structure clients preserve roles.
            var invalid = saved.DeepClone().AsObject(); invalid["milestones"]![0]!["offset"] = int.MinValue;
            Assert.Equal(HttpStatusCode.BadRequest, (await admin.Put($"/api/v1/templates/{id}/structure", invalid)).StatusCode);
            var preview = await (await admin.Post($"/api/v1/templates/{id}/preview", new { startDate = "2026-10-02", disciplineIds = new[] { civil } })).Json();
            Assert.Equal(2, preview["roles"]!.AsArray().Count);
            Assert.Equal("2026-10-02", preview["roles"]![0]!.S("from"));
            Assert.Equal("start_milestone_undated", preview["roles"]![1]!.S("skipReason"));
            (await admin.Post($"/api/v1/templates/{id}/publish", new { rowVersion = saved.I("rowVersion") })).EnsureSuccessStatusCode();
            async Task<Project> Create() => await d.Project(activate: false, tweak: body => {
                body["templateId"] = id; body["startDate"] = "2026-10-02";
                body["disciplines"] = new[] { new { disciplineId = civil, leadUserId = (Guid?)U(TestData.Marc) } };
            });
            var project = await Create();
            var role = await f.DbAsync(db => db.OpenRoles.AsNoTracking().SingleAsync(r => r.ProjectId == project.Id));
            Assert.Equal(("Civil reviewer", OpenRoleStatus.Open, 12m, U(TestData.Pm)), (role.RoleLabel, role.Status, role.PlannedHours, role.CreatedBy));
            Assert.True(await f.DbAsync(db => db.ActivityLog.AnyAsync(a => a.ItemType == "OpenRole" && a.ItemId == role.Id && a.Action == "Created")));
            var draft = await (await admin.Post($"/api/v1/templates/{id}/draft", new { })).Json(201);
            Assert.Equal(2, (await Template(draft.G("id"), TestData.Admin))["roles"]!.AsArray().Count);
            await Flag(false);
            Assert.False((await Template(id)).AsObject().ContainsKey("roles"));
            var off = await Create(); Assert.False(await f.DbAsync(db => db.OpenRoles.AnyAsync(r => r.ProjectId == off.Id)));
            var flagOffPreview = await (await admin.Post($"/api/v1/templates/{id}/preview", new { startDate = "2026-10-02", disciplineIds = new[] { civil } })).Json();
            Assert.False(flagOffPreview.AsObject().ContainsKey("roles"));
            Assert.False(flagOffPreview["counts"]!.AsObject().ContainsKey("roles"));
        }
        finally { await Flag(false); }
    }

    [Fact]
    public async Task Template_basis_suggestions_copy_as_proposed_without_approval_or_links()
    {
        var tid = await Reference();
        var civil = await d.Discipline("Civil");
        var templateDiscipline = await f.DbAsync(db => db.TemplateDisciplines.FirstAsync(x => x.TemplateId == tid && x.DisciplineId == civil));
        await f.DbAsync(async db =>
        {
            db.TemplateDesignBases.Add(new TemplateDesignBasis { TemplateId = tid, TemplateDisciplineId = templateDiscipline.Id,
                Kind = BasisKind.Criterion, Title = "Template bearing criterion", Scope = "Bridge / Pier 1",
                Statement = "Allowable bearing pressure", NumericValue = 100, Units = "kPa",
                SourceSystem = "Template source", StableSourceId = "TPL-GEO-1", SourceUrl = "https://example.test/template", DeclaredRevision = "A" });
            await db.SaveChangesAsync();
            return 0;
        });
        var (res, number) = await FromTemplate(tid, ["Project Management", "Civil"]);
        var project = await res.Json(201);
        var pid = project.G("id");
        var basis = await f.DbAsync(db => db.DesignBasisEntries.Where(x => x.ProjectId == pid).SingleAsync());
        var version = await f.DbAsync(db => db.DesignBasisVersions.SingleAsync(x => x.EntryId == basis.Id));
        Assert.Equal(BasisStatus.Proposed, version.Status);
        Assert.Null(version.ConfirmationDueDate);
        Assert.Equal("Template bearing criterion", basis.Title);
        Assert.Equal(U(TestData.Marc), basis.OwnerId);
        Assert.Null(basis.CurrentVersionId);
        Assert.Null(version.DecisionId);
        Assert.Null(version.ConfirmedBy);
        Assert.Null(version.ConfirmedAt);
        Assert.Equal(100, version.NumericValue);
        Assert.Equal("https://example.test/template", version.SourceUrl);
        Assert.DoesNotContain(version.Id, await f.DbAsync(db => db.BasisUses.Where(x => x.ProjectId == pid).Select(x => x.VersionId).ToListAsync()));

        // Template-derived basis must acquire a project-specific confirmation date;
        // an independent approver cannot approve an unknown date carried by the copy.
        var root = $"/api/v1/projects/{pid}/design-basis/{basis.Id}";
        var appointed = await (await f.As(TestData.Pm).Post(root + "/assign", new DesignBasisEndpoints.AssignBody(
            Guid.NewGuid(), basis.RowVersion, basis.OwnerId, U(TestData.Pm), "Appoint independent project approver"))).Json();
        var refusal = await (await f.As(TestData.Pm).Post($"{root}/versions/{version.Id}/confirm", new DesignBasisEndpoints.ConfirmBody(
            Guid.NewGuid(), appointed.I("rowVersion"), version.RowVersion, "Review copied source evidence"))).Json(400);
        Assert.NotNull(refusal["errors"]?["confirmationDueDate"]);
        var unchanged = await f.DbAsync(db => db.DesignBasisVersions.AsNoTracking().SingleAsync(v => v.Id == version.Id));
        Assert.Equal(BasisStatus.Proposed, unchanged.Status);
        Assert.Equal(version.RowVersion, unchanged.RowVersion);
        Assert.Null(unchanged.ConfirmedBy);
        Assert.Null((await f.DbAsync(db => db.DesignBasisEntries.AsNoTracking().SingleAsync(e => e.Id == basis.Id))).CurrentVersionId);

        var date = new DateOnly(2026, 10, 12);
        var input = new DesignBasisEndpoints.VersionInput(version.Scope, version.Statement, version.NumericValue, version.Units,
            version.SourceSystem, version.StableSourceId, version.SourceUrl, version.DeclaredRevision, date, null);
        var edited = await (await f.As(TestData.Marc).Post($"{root}/versions/{version.Id}/edit", new DesignBasisEndpoints.EditProposedBody(
            Guid.NewGuid(), appointed.I("rowVersion"), unchanged.RowVersion, input, "Supply the project's confirmation date"))).Json();
        await (await f.As(TestData.Pm).Post($"{root}/versions/{version.Id}/confirm", new DesignBasisEndpoints.ConfirmBody(
            Guid.NewGuid(), appointed.I("rowVersion"), edited.I("rowVersion"), "Independently confirm dated project basis"))).Json();
        var confirmed = await f.DbAsync(db => db.DesignBasisVersions.AsNoTracking().SingleAsync(v => v.Id == version.Id));
        Assert.Equal(BasisStatus.Confirmed, confirmed.Status);
        Assert.Equal(date, confirmed.ConfirmationDueDate);
        Assert.Equal(U(TestData.Pm), confirmed.ConfirmedBy);
        Assert.Equal(version.Id, (await f.DbAsync(db => db.DesignBasisEntries.AsNoTracking().SingleAsync(e => e.Id == basis.Id))).CurrentVersionId);
    }

    [Fact]
    public async Task Editors_add_basis_suggestions_to_drafts_and_the_template_lists_them() // 031 FR-BAS-07, 012 US1 scenario 3
    {
        var admin = f.As(TestData.Admin);
        var civil = await d.Discipline("Civil");
        var t = await (await admin.Post("/api/v1/templates", new { name = "Storm Sewer " + Guid.NewGuid().ToString("N")[..4] })).Json(201);
        var id = t.G("id");
        var saved = await (await admin.Put($"/api/v1/templates/{id}/structure", new
        {
            rowVersion = t.I("rowVersion"), disciplines = new[] { new { disciplineId = civil, isDefaultIncluded = true } },
            milestones = new[] { new { @ref = "m1", name = "Kickoff", milestoneType = "Kickoff", anchor = "ProjectStart", offset = (int?)5, completesPhaseId = (Guid?)null, isClientFacing = false } },
            deliverables = Array.Empty<object>(), tasks = Array.Empty<object>(), dependencies = Array.Empty<object>(),
        })).Json();
        var body = new
        {
            templateDisciplineId = saved["disciplines"]!.AsArray().Single()!.G("templateDisciplineId"), kind = BasisKind.Criterion, title = "Design storm",
            scope = "Site / storm sewers", statement = "Minor system return period", numericValue = 5m, units = "years", sourceUrl = "https://example.test/standard",
        };
        Assert.Equal(HttpStatusCode.Forbidden, (await f.As(TestData.Pm).Post($"/api/v1/templates/{id}/design-basis", body)).StatusCode);
        var unitless = await (await admin.Post($"/api/v1/templates/{id}/design-basis", body with { units = " " })).Json(400);
        Assert.Equal("A numeric value needs its units.", unitless["errors"]!["units"]![0]!.GetValue<string>());
        var added = await (await admin.Post($"/api/v1/templates/{id}/design-basis", body)).Json(201);

        var tpl = await Template(id, TestData.Admin);
        var listed = tpl["basisSuggestions"]!.AsArray().Single()!;
        Assert.Equal((added.G("id"), civil, "Design storm", 5m, "years"), (listed.G("id"), listed.G("disciplineId"), listed.S("title"), listed["numericValue"]!.GetValue<decimal>(), listed.S("units")));
        Assert.True(tpl.I("rowVersion") > saved.I("rowVersion")); // a suggestion moves the Draft's version, like a structure save
        Assert.Equal("Design storm", await f.DbAsync(db => db.ActivityLog.Where(a => a.ItemId == id && a.Action == "BasisSuggestionAdded").Select(a => a.Reason).SingleAsync()));
        (await admin.Post($"/api/v1/templates/{id}/publish", new { rowVersion = tpl.I("rowVersion") })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await admin.Post($"/api/v1/templates/{id}/design-basis", body)).StatusCode); // only Drafts change
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await admin.DeleteAsync($"/api/v1/templates/{id}/design-basis/{added.S("id")}")).StatusCode);
        Assert.Single((await Template(id))["basisSuggestions"]!.AsArray()); // the wizard's PM sees what a project would copy
        var draft = await (await admin.Post($"/api/v1/templates/{id}/draft", new { })).Json(201);
        Assert.Equal("Design storm", (await Template(draft.G("id"), TestData.Admin))["basisSuggestions"]!.AsArray().Single()!.S("title")); // the next Draft keeps them
    }

    [Fact]
    public async Task Draft_saves_keep_basis_suggestions_and_refuse_to_drop_their_discipline() // 031 FR-BAS-07, 012 FR-001
    {
        var admin = f.As(TestData.Admin);
        var civil = await d.Discipline("Civil");
        var geo = await d.Discipline("Geotechnical");
        var id = (await (await admin.Post("/api/v1/templates", new { name = "Basis Save " + Guid.NewGuid().ToString("N")[..4] })).Json(201)).G("id");
        async Task<HttpResponseMessage> Save(string name, params Guid[] disciplines) => await admin.Put($"/api/v1/templates/{id}/structure", new
        {
            rowVersion = (await Template(id, TestData.Admin)).I("rowVersion"), header = new { name, description = (string?)null, projectTypeId = (Guid?)null },
            disciplines = disciplines.Select(x => new { disciplineId = x, isDefaultIncluded = true }),
            milestones = new[] { new { @ref = "m1", name = "Kickoff", milestoneType = "Kickoff", anchor = "ProjectStart", offset = (int?)5, completesPhaseId = (Guid?)null, isClientFacing = false } },
            deliverables = Array.Empty<object>(), tasks = Array.Empty<object>(), dependencies = Array.Empty<object>(),
        });
        static Guid Row(JsonNode tpl, Guid discipline) => tpl["disciplines"]!.AsArray().Single(x => x!.G("disciplineId") == discipline)!.G("templateDisciplineId");
        async Task<JsonNode> Add(Guid discipline, string title) => await (await admin.Post($"/api/v1/templates/{id}/design-basis", new
        {
            templateDisciplineId = Row(await Template(id, TestData.Admin), discipline), kind = BasisKind.Assumption, title, scope = "Site", statement = "Groundwater is below 3 m",
        })).Json(201);
        (await Save("Basis Save", civil, geo)).EnsureSuccessStatusCode();
        var added = await Add(civil, "Groundwater level");

        // A save that keeps Civil moves its suggestion to Civil's new row.
        var resaved = await (await Save("Basis Save", geo, civil)).Json();
        var kept = resaved["basisSuggestions"]!.AsArray().Single()!;
        Assert.Equal((added.G("id"), civil), (kept.G("id"), kept.G("disciplineId")));
        Assert.Equal(Row(resaved, civil), await f.DbAsync(db => db.TemplateDesignBases.Where(x => x.TemplateId == id).Select(x => x.TemplateDisciplineId).SingleAsync()));

        // Dropping Civil while a suggestion uses it is refused, naming it, and nothing changes, not even the header.
        var refused = await (await Save("Renamed", geo)).Json(400);
        Assert.Equal("Civil has design basis suggestions (1). Remove those suggestions first.", refused["errors"]!["disciplines"]![0]!.GetValue<string>());
        var after = await Template(id, TestData.Admin);
        Assert.Equal(("Basis Save", resaved.I("rowVersion"), 2, 1), (after.S("name"), after.I("rowVersion"), after["disciplines"]!.AsArray().Count, after["basisSuggestions"]!.AsArray().Count));

        // Only editors remove a suggestion; then Civil can go.
        var path = $"/api/v1/templates/{id}/design-basis/{added.S("id")}";
        Assert.Equal(HttpStatusCode.Forbidden, (await f.As(TestData.Pm).DeleteAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.DeleteAsync($"/api/v1/templates/{id}/design-basis/{Guid.NewGuid()}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.DeleteAsync(path)).StatusCode);
        (await Save("Basis Save", geo)).EnsureSuccessStatusCode();
        Assert.Equal(new[] { "BasisSuggestionAdded", "BasisSuggestionRemoved" }, (await f.DbAsync(db => db.ActivityLog
            .Where(a => a.ItemType == ItemType.Template && a.ItemId == id && a.Action.StartsWith("BasisSuggestion")).Select(a => a.Action).ToListAsync())).Order());

        // A Draft with suggestions can still be discarded, and they go with it.
        await Add(geo, "Bearing stratum");
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/v1/templates/{id}")).StatusCode);
        Assert.False(await f.DbAsync(db => db.TemplateDesignBases.AnyAsync(x => x.TemplateId == id)));
        Assert.False(await f.DbAsync(db => db.Templates.AnyAsync(x => x.Id == id)));
    }

    [Fact]
    public async Task Appendix_A_creates_the_stated_structure_in_setup() // US2 scenarios 1 and 3, SC-002, FR-004, FR-006, FR-008
    {
        var tid = await Reference();
        var (res, number) = await FromTemplate(tid);
        var created = await res.Json(201);
        var pid = created.G("id");
        var p = await f.DbAsync(db => db.Projects.AsNoTracking().FirstAsync(x => x.Id == pid));
        Assert.Equal((ProjectStatus.Setup, tid, 1), (p.Status, p.CreatedFromTemplateId!.Value, p.TemplateVersion!.Value));

        var ms = await f.DbAsync(db => db.Milestones.AsNoTracking().Where(m => m.ProjectId == pid).OrderBy(m => m.SortOrder).ToListAsync());
        Assert.Equal(11, ms.Count);
        Assert.Equal(new DateOnly?[] { new(2026, 10, 12), new(2026, 11, 19), new(2027, 1, 15), new(2027, 3, 12), new(2027, 5, 14), new(2027, 6, 25), new(2027, 7, 16), null, null, null, null },
            ms.Select(m => m.Date));
        Assert.All(ms, m => Assert.NotNull(m.TemplateMilestoneId));
        Assert.Equal(28, await f.DbAsync(db => db.Deliverables.CountAsync(x => x.ProjectId == pid)));
        Assert.Equal(120, await f.DbAsync(db => db.Tasks.CountAsync(x => x.ProjectId == pid)));
        Assert.Equal(96, await f.DbAsync(db => db.Dependencies.CountAsync(x => x.ProjectId == pid)));

        // Dates follow the plan: the 85 % package is due M05 − 3, and "Update grading" 12 days before it.
        var pkg = await f.DbAsync(db => db.Deliverables.AsNoTracking().FirstAsync(x => x.ProjectId == pid && x.Name == "85% Civil Drawing Package"));
        Assert.Equal((new DateOnly(2027, 5, 11), U(TestData.Marc)), (pkg.DueDate!.Value, pkg.OwnerId!.Value));
        var tasks = await f.DbAsync(db => db.Tasks.AsNoTracking().Where(x => x.DeliverableId == pkg.Id).ToDictionaryAsync(x => x.Name));
        Assert.Equal(new DateOnly(2027, 4, 29), tasks["Update grading"].DueDate);
        Assert.Equal((null, U(TestData.Marc), U(TestData.Pm)), (tasks["Update grading"].AssigneeId, tasks["Technical review"].AssigneeId!.Value, tasks["PM review"].AssigneeId!.Value));
        Assert.True(tasks["CAD QA"].RequiresReview);
        Assert.Null(await f.DbAsync(db => db.Tasks.Where(x => x.ProjectId == pid && x.Name == "Prepare Tender Package").Select(x => x.DueDate).FirstAsync())); // M08 left blank

        // One "created from template" entry beside the item creations (FR-006); no assignment notices in Setup.
        Assert.Equal(1, await f.DbAsync(db => db.ActivityLog.CountAsync(a => a.ProjectId == pid && a.Action == "CreatedFromTemplate")));
        Assert.Equal(0, await f.DbAsync(db => db.Notifications.CountAsync(n => n.ProjectId == pid && n.EventType == NotificationEvents.TaskAssigned)));
        var v = d.Version(pid);
        (await f.As(TestData.Pm).Post($"/api/v1/projects/{pid}/transition", new { toStatus = "Active", rowVersion = v })).EnsureSuccessStatusCode();
        var notices = await f.DbAsync(db => db.Notifications.Where(n => n.ProjectId == pid && n.EventType == NotificationEvents.TaskAssigned).Select(n => n.UserId).ToListAsync());
        Assert.Equal(notices.Distinct().Count(), notices.Count); // one batched notice per person (US2 scenario 4)
        Assert.Contains(U(TestData.Marc), notices);
    }

    [Fact]
    public async Task An_unticked_discipline_and_its_dependencies_are_left_out() // US2 scenario 2
    {
        var tid = await Reference();
        var (res, _) = await FromTemplate(tid, ["Project Management", "Survey", "Civil", "Electrical", "Environmental"]);
        var pid = (await res.Json(201)).G("id");
        Assert.Equal(26, await f.DbAsync(db => db.Deliverables.CountAsync(x => x.ProjectId == pid)));
        Assert.Equal(112, await f.DbAsync(db => db.Tasks.CountAsync(x => x.ProjectId == pid)));
        Assert.Equal(88, await f.DbAsync(db => db.Dependencies.CountAsync(x => x.ProjectId == pid))); // Geotechnical's own 6 and 2 cross links
    }

    [Fact]
    public async Task Editors_build_publish_and_version_templates_and_projects_keep_theirs() // US1, US4, FR-001..FR-003, FR-008
    {
        var admin = f.As(TestData.Admin);
        Assert.Equal(HttpStatusCode.Forbidden, (await f.As(TestData.Alex).Post("/api/v1/templates", new { name = "Mine" })).StatusCode);
        var t = await admin.Post("/api/v1/templates", new { name = "Culvert Replacement " + Guid.NewGuid().ToString("N")[..4] }).Result.Json(201);
        var id = t.G("id");
        var civil = await d.Discipline("Civil");
        var geo = await d.Discipline("Geotechnical");
        var report = await d.DeliverableType("Report");
        var drawings = await d.DeliverableType("Drawing Package");
        object Structure(int rowVersion, bool cycle = false) => new
        {
            rowVersion,
            disciplines = new[] { new { disciplineId = civil, isDefaultIncluded = true }, new { disciplineId = geo, isDefaultIncluded = true } },
            milestones = new[]
            {
                new { @ref = "m1", name = "Kickoff", milestoneType = "Kickoff", anchor = "ProjectStart", offset = (int?)5, completesPhaseId = (Guid?)null, isClientFacing = true },
                new { @ref = "m2", name = "Design", milestoneType = "Design Submission", anchor = "PreviousMilestone", offset = (int?)30, completesPhaseId = (Guid?)null, isClientFacing = true },
                new { @ref = "m3", name = "IFC", milestoneType = "IFC", anchor = "PreviousMilestone", offset = (int?)20, completesPhaseId = (Guid?)null, isClientFacing = true },
            },
            deliverables = new[]
            {
                new { @ref = "d1", disciplineId = geo, name = "Borehole report", deliverableTypeId = report, milestoneRef = "m2", offset = (int?)-10, requiresReview = true, description = (string?)null },
                new { @ref = "d2", disciplineId = civil, name = "Culvert design", deliverableTypeId = drawings, milestoneRef = "m2", offset = (int?)0, requiresReview = true, description = (string?)null },
                new { @ref = "d3", disciplineId = civil, name = "Culvert IFC", deliverableTypeId = drawings, milestoneRef = "m3", offset = (int?)-2, requiresReview = true, description = (string?)null },
                new { @ref = "d4", disciplineId = civil, name = "Specifications", deliverableTypeId = report, milestoneRef = (string?)null, offset = (int?)null, requiresReview = false, description = (string?)null },
            },
            tasks = Enumerable.Range(1, 8).Select(i => new
            {
                @ref = $"t{i}", disciplineId = i <= 2 ? geo : civil, deliverableRef = i <= 2 ? "d1" : i <= 5 ? "d2" : "d3", name = $"Task {i}", description = (string?)null,
                requiresReview = i % 4 == 0, priority = "Medium", estimatedHours = (decimal?)8, offset = (int?)(-i), assignTo = i % 2 == 0 ? "DisciplineLead" : "Unassigned",
            }),
            dependencies = new[] { new { predecessor = "t1", successor = "t2" }, new { predecessor = "t2", successor = "t3" }, new { predecessor = "t3", successor = "t4" },
                new { predecessor = "t4", successor = "t6" }, new { predecessor = cycle ? "t6" : "t5", successor = cycle ? "t1" : "t7" } },
        };
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.Put($"/api/v1/templates/{id}/structure", Structure(t.I("rowVersion"), cycle: true))).StatusCode);
        var saved = await admin.Put($"/api/v1/templates/{id}/structure", Structure(t.I("rowVersion"))).Result.Json();
        Assert.Equal((2, 3, 4, 8, 5), (saved["disciplines"]!.AsArray().Count, saved["milestones"]!.AsArray().Count, saved["deliverables"]!.AsArray().Count,
            saved["tasks"]!.AsArray().Count, saved["dependencies"]!.AsArray().Count));
        Assert.Equal(HttpStatusCode.Conflict, (await admin.Put($"/api/v1/templates/{id}/structure", Structure(t.I("rowVersion")))).StatusCode); // stale version

        var preview = await admin.Post($"/api/v1/templates/{id}/preview", new { startDate = "2026-10-05" }).Result.Json();
        Assert.Equal(new[] { "2026-10-10", "2026-11-09", "2026-11-29" }, preview["milestones"]!.AsArray().Select(m => m!.S("date")));
        Assert.Equal((4, 8, 5, 1), (preview["counts"]!.I("deliverables"), preview["counts"]!.I("tasks"), preview["counts"]!.I("dependencies"), preview["undated"]!.I("deliverables")));
        Assert.DoesNotContain((await f.As(TestData.Pm).GetAsync("/api/v1/templates").Result.Json())["templates"]!.AsArray(), x => x!.G("id") == id); // drafts are for editors

        var v1 = await admin.Post($"/api/v1/templates/{id}/publish", new { rowVersion = saved.I("rowVersion") }).Result.Json();
        Assert.Equal((1, "Published"), (v1.I("version"), v1.S("status")));
        Assert.Contains((await f.As(TestData.Pm).GetAsync("/api/v1/templates").Result.Json())["templates"]!.AsArray(), x => x!.G("id") == id);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await admin.Put($"/api/v1/templates/{id}/structure", Structure(v1.I("rowVersion")))).StatusCode); // published stays as it is

        var number = TestData.Number();
        var p = await f.As(TestData.Pm).Post("/api/v1/projects", new { projectNumber = number, name = "Culvert " + number, clientId = await d.Client(), officeId = await d.Office(),
            startDate = "2026-10-05", disciplines = new[] { new { disciplineId = civil, leadUserId = (Guid?)U(TestData.Marc) } }, templateId = id }).Result.Json(201);
        Assert.Equal(6, await f.DbAsync(db => db.Tasks.CountAsync(x => x.ProjectId == p.G("id")))); // Civil only

        var draft = await admin.Post($"/api/v1/templates/{id}/draft", new { }).Result.Json(201);
        Assert.Equal(draft.G("id"), (await admin.Post($"/api/v1/templates/{id}/draft", new { }).Result.Json()).G("id")); // one draft per family
        var draftBody = await Template(draft.G("id"), TestData.Admin);
        var edited = await admin.Put($"/api/v1/templates/{draft.S("id")}/structure", new
        {
            rowVersion = draftBody.I("rowVersion"), disciplines = draftBody["disciplines"], milestones = draftBody["milestones"], deliverables = draftBody["deliverables"],
            tasks = draftBody["tasks"]!.AsArray().Take(6), dependencies = draftBody["dependencies"]!.AsArray().Take(3),
        }).Result.Json();
        var v2 = await admin.Post($"/api/v1/templates/{draft.S("id")}/publish", new { rowVersion = edited.I("rowVersion") }).Result.Json();
        Assert.Equal(2, v2.I("version"));
        Assert.Equal(TemplateStatus.Retired, await f.DbAsync(db => db.Templates.Where(x => x.Id == id).Select(x => x.Status).FirstAsync())); // v1 superseded
        Assert.Equal(8, (await Template(id, TestData.Admin))["tasks"]!.AsArray().Count); // v1's content is kept
        var proj = await f.DbAsync(db => db.Projects.AsNoTracking().FirstAsync(x => x.Id == p.G("id")));
        Assert.Equal((id, 1), (proj.CreatedFromTemplateId!.Value, proj.TemplateVersion!.Value)); // US4: nothing propagates
        Assert.Equal(6, await f.DbAsync(db => db.Tasks.CountAsync(x => x.ProjectId == p.G("id"))));

        // US2 scenario 5: a template retired while the PM was in the wizard is refused, and nothing is created.
        var (refused, refusedNumber) = await FromTemplate(await Reference(), tplOverride: id);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, refused.StatusCode);
        Assert.False(await f.DbAsync(db => db.Projects.AnyAsync(x => x.ProjectNumber == refusedNumber)));
        (await admin.Post($"/api/v1/templates/{draft.S("id")}/retire", new { })).EnsureSuccessStatusCode();
        Assert.DoesNotContain((await f.As(TestData.Pm).GetAsync("/api/v1/templates").Result.Json())["templates"]!.AsArray(), x => x!.G("id") == id || x!.G("id") == draft.G("id"));
    }

    [Fact]
    public async Task A_discipline_pack_is_added_with_milestones_mapped_by_name() // US3, FR-007
    {
        var tid = await Reference();
        var (res, _) = await FromTemplate(tid);
        var pid = (await res.Json(201)).G("id");
        var transport = await d.Discipline("Transportation");
        var proposal = await f.As(TestData.Pm).GetAsync($"/api/v1/projects/{pid}/template-packs?templateId={tid}").Result.Json();
        var pack = proposal["disciplines"]!.AsArray().Single(x => x!.G("disciplineId") == transport)!;
        Assert.False(pack["inProject"]!.GetValue<bool>());
        var map = pack["mapping"]!.AsArray().Single()!;
        var m05 = await f.DbAsync(db => db.Milestones.AsNoTracking().FirstAsync(m => m.ProjectId == pid && m.Name == "85% Design Submission"));
        Assert.Equal(("85% Design Submission", m05.Id), (map.S("name"), map.G("projectMilestoneId")));
        Assert.Equal(HttpStatusCode.Forbidden, (await f.As(TestData.Alex).Post($"/api/v1/projects/{pid}/template-packs", new { templateId = tid, disciplineId = transport })).StatusCode);

        var added = await f.As(TestData.Pm).Post($"/api/v1/projects/{pid}/template-packs", new
        {
            templateId = tid, disciplineId = transport, leadUserId = U(TestData.Rita), mapping = new Dictionary<string, Guid?> { [map.S("templateMilestoneId")] = m05.Id },
        }).Result.Json();
        Assert.Equal((1, 4, 3), (added.I("deliverables"), added.I("tasks"), added.I("dependencies")));
        var plan = await f.DbAsync(db => db.Deliverables.AsNoTracking().FirstAsync(x => x.ProjectId == pid && x.Name == "Traffic Management Plan"));
        Assert.Equal((new DateOnly(2027, 5, 9), m05.Id, U(TestData.Rita)), (plan.DueDate!.Value, plan.MilestoneId!.Value, plan.OwnerId!.Value)); // M05 − 5
        Assert.True(await f.DbAsync(db => db.ProjectDisciplines.AnyAsync(x => x.ProjectId == pid && x.DisciplineId == transport && x.LeadUserId == U(TestData.Rita))));
        Assert.Equal(1, await f.DbAsync(db => db.ActivityLog.CountAsync(a => a.ProjectId == pid && a.Action == "AddedFromTemplate")));
    }

    // ---------- Packet 045 S07, §49.4: working-day offsets behind flag.template_working_days ----------

    async Task WorkingDays(bool on) => await f.As(TestData.Admin).Put("/api/v1/admin/settings/flag.template_working_days", new { value = on }).Result.Json();

    async Task WithWorkingDays(Func<Task> body)
    {
        await WorkingDays(true);
        try { await body(); } finally { await WorkingDays(false); }
    }

    static object Milestone(string @ref, string anchor, int offset) =>
        new { @ref, name = "M " + @ref, milestoneType = "Kickoff", anchor, offset = (int?)offset, completesPhaseId = (Guid?)null, isClientFacing = false };

    [Fact]
    [Trait("flag-off", "template_working_days"), Trait("flag-on", "template_working_days")]
    public async Task Existing_templates_count_calendar_days_and_give_the_same_dates() => await WithWorkingDays(async () => // AC-RCV-14, FR-RCV-11
    {
        var tid = await Reference();
        Assert.Equal(DayKind.CalendarDays, await f.DbAsync(db => db.Templates.Where(x => x.Id == tid).Select(x => x.OffsetDayKind).SingleAsync()));
        Assert.Equal(DayKind.CalendarDays, (await Template(tid)).S("offsetDayKind"));
        var on = await f.As(TestData.Pm).Post($"/api/v1/templates/{tid}/preview", new { startDate = "2026-10-05", officeId = await d.Office() }).Result.Json();
        await WorkingDays(false);
        Assert.Null((await Template(tid))["offsetDayKind"]); // flag off: the read model is today's
        var off = await f.As(TestData.Pm).Post($"/api/v1/templates/{tid}/preview", new { startDate = "2026-10-05" }).Result.Json();
        Assert.Equal(off["milestones"]!.ToJsonString(), on["milestones"]!.ToJsonString());
        await WorkingDays(true);
        var (res, _) = await FromTemplate(tid); // Appendix A's calendar-day dates, as before this packet
        var pid = (await res.Json(201)).G("id");
        Assert.Equal(new DateOnly?[] { new(2026, 10, 12), new(2026, 11, 19) },
            await f.DbAsync(db => db.Milestones.Where(m => m.ProjectId == pid).OrderBy(m => m.SortOrder).Select(m => m.Date).Take(2).ToListAsync()));
        Assert.Equal(new DateOnly(2027, 5, 11), await f.DbAsync(db => db.Deliverables.Where(x => x.ProjectId == pid && x.Name == "85% Civil Drawing Package").Select(x => x.DueDate).SingleAsync()));
    });

    [Fact]
    public async Task Preview_create_and_add_pack_give_identical_working_day_dates() => await WithWorkingDays(async () => // AC-RCV-15, AC-RCV-16, AC-RCV-17, FR-RCV-12, FR-RCV-13
    {
        var admin = f.As(TestData.Admin);
        var civil = await d.Discipline("Civil");
        var geo = await d.Discipline("Geotechnical");
        var report = await d.DeliverableType("Report");
        // An office whose Monday 2031-01-06 is a holiday; the project starts Friday 2031-01-03.
        var office = await f.DbAsync(async db =>
        {
            var o = new Office { Name = "Synthetic working-day office", Code = Guid.NewGuid().ToString("N") };
            db.Offices.Add(o);
            db.Holidays.Add(new Holiday { Date = new DateOnly(2031, 1, 6), OfficeId = o.Id, Name = "Synthetic office holiday" });
            await db.SaveChangesAsync();
            return o.Id;
        });
        var t = await (await admin.Post("/api/v1/templates", new { name = "Working Days " + Guid.NewGuid().ToString("N")[..4], offsetDayKind = DayKind.WorkingDays })).Json(201);
        var id = t.G("id");
        var saved = await (await admin.Put($"/api/v1/templates/{id}/structure", new
        {
            rowVersion = t.I("rowVersion"),
            disciplines = new[] { new { disciplineId = civil, isDefaultIncluded = true }, new { disciplineId = geo, isDefaultIncluded = false } },
            milestones = new[] { Milestone("m1", "ProjectStart", 1), Milestone("m2", "PreviousMilestone", 3) },
            deliverables = new[]
            {
                new { @ref = "d1", disciplineId = civil, name = "Civil report", deliverableTypeId = report, milestoneRef = "m2", offset = (int?)-2, requiresReview = false, description = (string?)null },
                new { @ref = "d2", disciplineId = geo, name = "Geo report", deliverableTypeId = report, milestoneRef = "m2", offset = (int?)-2, requiresReview = false, description = (string?)null },
            },
            tasks = new[]
            {
                new { @ref = "t1", disciplineId = civil, deliverableRef = (string?)"d1", name = "Civil draft", offset = (int?)-1 },
                new { @ref = "t2", disciplineId = geo, deliverableRef = (string?)"d2", name = "Geo draft", offset = (int?)-1 },
                new { @ref = "t3", disciplineId = civil, deliverableRef = (string?)null, name = "Civil setup", offset = (int?)2 },
            }.Select(k => new { k.@ref, k.disciplineId, k.deliverableRef, k.name, description = (string?)null, requiresReview = false, priority = "Medium", estimatedHours = (decimal?)null, k.offset, assignTo = "Unassigned" }),
            dependencies = Array.Empty<object>(),
        })).Json();
        Assert.Equal(DayKind.WorkingDays, saved.S("offsetDayKind"));
        (await admin.Post($"/api/v1/templates/{id}/publish", new { rowVersion = saved.I("rowVersion") })).EnsureSuccessStatusCode();

        string[] Dates(JsonNode preview) => preview["milestones"]!.AsArray().Select(m => m!.S("date")).ToArray();
        var preview = await f.As(TestData.Pm).Post($"/api/v1/templates/{id}/preview", new { startDate = "2031-01-03", officeId = office }).Result.Json();
        Assert.Equal(["2031-01-07", "2031-01-10"], Dates(preview)); // Friday + 1 skips the weekend and the office's Monday holiday
        var noOffice = await f.As(TestData.Pm).Post($"/api/v1/templates/{id}/preview", new { startDate = "2031-01-03" }).Result.Json();
        Assert.Equal("2031-01-06", Dates(noOffice)[0]); // without an office only organisation-wide holidays count

        var number = TestData.Number();
        var pid = (await f.As(TestData.Pm).Post("/api/v1/projects", new
        {
            projectNumber = number, name = "Working days " + number, clientId = await d.Client(), officeId = office, startDate = "2031-01-03",
            disciplines = new[] { new { disciplineId = civil, leadUserId = (Guid?)U(TestData.Marc) } }, templateId = id,
        }).Result.Json(201)).G("id");
        var ms = await f.DbAsync(db => db.Milestones.AsNoTracking().Where(m => m.ProjectId == pid).OrderBy(m => m.SortOrder).ToListAsync());
        Assert.Equal(Dates(preview), ms.Select(m => m.Date!.Value.ToString("yyyy-MM-dd")));
        var dueOf = async (string name) => await f.DbAsync(db => db.Deliverables.Where(x => x.ProjectId == pid && x.Name == name).Select(x => x.DueDate).SingleAsync());
        var taskDue = async (string name) => await f.DbAsync(db => db.Tasks.Where(x => x.ProjectId == pid && x.Name == name).Select(x => x.DueDate).SingleAsync());
        Assert.Equal(new DateOnly(2031, 1, 8), await dueOf("Civil report")); // Friday − 2 working days
        Assert.Equal(new DateOnly(2031, 1, 7), await taskDue("Civil draft"));
        Assert.Equal(new DateOnly(2031, 1, 8), await taskDue("Civil setup")); // start + 2 skips the holiday

        var tplMs = (await Template(id))["milestones"]!.AsArray().Select(m => m!.S("ref")).ToList();
        (await f.As(TestData.Pm).Post($"/api/v1/projects/{pid}/template-packs", new
        {
            templateId = id, disciplineId = geo, leadUserId = U(TestData.Omar), mapping = new Dictionary<string, Guid?> { [tplMs[1]] = ms[1].Id },
        })).EnsureSuccessStatusCode();
        Assert.Equal(await dueOf("Civil report"), await dueOf("Geo report"));
        Assert.Equal(await taskDue("Civil draft"), await taskDue("Geo draft"));

        await WorkingDays(false); // flag off: the same template counts calendar days, exactly as today
        var off = await f.As(TestData.Pm).Post($"/api/v1/templates/{id}/preview", new { startDate = "2031-01-03", officeId = office }).Result.Json();
        Assert.Equal(["2031-01-04", "2031-01-07"], Dates(off));
    });

    [Fact]
    public async Task Offset_kind_is_audited_copied_to_drafts_and_fixed_once_published() => await WithWorkingDays(async () => // AC-RCV-17, FR-RCV-10
    {
        var admin = f.As(TestData.Admin);
        var civil = await d.Discipline("Civil");
        var name = "Offset Kind " + Guid.NewGuid().ToString("N")[..4];
        var t = await (await admin.Post("/api/v1/templates", new { name })).Json(201);
        var id = t.G("id");
        Assert.Equal(DayKind.CalendarDays, (await Template(id, TestData.Admin)).S("offsetDayKind")); // new templates default to calendar days
        object Body(int rowVersion, string kind) => new
        {
            rowVersion, header = new { name, description = (string?)null, projectTypeId = (Guid?)null, offsetDayKind = kind },
            disciplines = new[] { new { disciplineId = civil, isDefaultIncluded = true } },
            milestones = new[] { Milestone("m1", "ProjectStart", 5) },
            deliverables = Array.Empty<object>(), tasks = Array.Empty<object>(), dependencies = Array.Empty<object>(),
        };
        var bad = await (await admin.Put($"/api/v1/templates/{id}/structure", Body(t.I("rowVersion"), "Fortnights"))).Json(400);
        Assert.NotNull(bad["errors"]?["offsetDayKind"]);
        var saved = await (await admin.Put($"/api/v1/templates/{id}/structure", Body(t.I("rowVersion"), DayKind.WorkingDays))).Json();
        Assert.Equal(DayKind.WorkingDays, saved.S("offsetDayKind"));
        var change = (await f.DbAsync(db => db.ActivityLog.Where(a => a.ItemType == ItemType.Template && a.ItemId == id).Select(a => a.Changes).ToListAsync()))
            .Single(c => c.Contains("OffsetDayKind") && c.Contains(DayKind.WorkingDays)); // the change, old and new
        Assert.Contains(DayKind.CalendarDays, change);

        var v1 = await (await admin.Post($"/api/v1/templates/{id}/publish", new { rowVersion = saved.I("rowVersion") })).Json();
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await admin.Put($"/api/v1/templates/{id}/structure", Body(v1.I("rowVersion"), DayKind.CalendarDays))).StatusCode);
        Assert.Equal(DayKind.WorkingDays, (await Template(id, TestData.Admin)).S("offsetDayKind"));
        var draft = await (await admin.Post($"/api/v1/templates/{id}/draft", new { })).Json(201);
        Assert.Equal(DayKind.WorkingDays, (await Template(draft.G("id"), TestData.Admin)).S("offsetDayKind"));

        // Flag off: the field is neither read nor written.
        await WorkingDays(false);
        var draftBody = await Template(draft.G("id"), TestData.Admin);
        Assert.Null(draftBody["offsetDayKind"]);
        (await admin.Put($"/api/v1/templates/{draft.S("id")}/structure", Body(draftBody.I("rowVersion"), DayKind.CalendarDays))).EnsureSuccessStatusCode();
        Assert.Equal(DayKind.WorkingDays, await f.DbAsync(db => db.Templates.Where(x => x.Id == draft.G("id")).Select(x => x.OffsetDayKind).SingleAsync()));
    });
}
