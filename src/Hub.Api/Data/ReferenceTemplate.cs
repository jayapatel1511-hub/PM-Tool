using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Data;

/// Appendix A's "Municipal Infrastructure Design" template, published as version 1: the six default disciplines (and
/// Transportation, Structural and Architecture available), 11 milestones, 28 deliverables with a Transportation pack of
/// one more, the 85 % Civil package's eight sample tasks, a four-step chain (prepare, check, review, issue) for every other
/// deliverable, four project-management tasks, and the cross-discipline chain of A.1. With the defaults it instantiates
/// 11 milestones, 28 deliverables, 120 tasks and 96 dependencies. Seeded for development and tests only.
public static class ReferenceTemplate
{
    public const string Name = "Municipal Infrastructure Design";

    public static async Task<Guid> Seed(HubDb db)
    {
        if (await db.Templates.Where(t => t.Name == Name).Select(t => (Guid?)t.Id).FirstOrDefaultAsync() is { } existing) return existing;
        db.Audit.AsSystem("Migration");
        var disc = await db.Disciplines.ToDictionaryAsync(d => d.Name, d => d.Id);
        var types = await db.DeliverableTypes.ToDictionaryAsync(d => d.Name, d => d.Id);
        var phases = await db.Phases.ToDictionaryAsync(d => d.Name, d => d.Id);
        var projectType = await db.ProjectTypes.Where(x => x.Name == "Municipal Infrastructure").Select(x => (Guid?)x.Id).FirstOrDefaultAsync();
        var t = new ProjectTemplate
        {
            FamilyId = Guid.CreateVersion7(), Name = Name, ProjectTypeId = projectType, Status = TemplateStatus.Published, Version = 1, PublishedAt = DateTimeOffset.UtcNow,
            Description = "Survey, civil, geotechnical, electrical and environmental design of municipal roads and services, from kickoff to closeout (Appendix A).",
        };
        db.Templates.Add(t);

        var tdisc = new Dictionary<string, Guid>();
        string[] defaults = ["Project Management", "Survey", "Civil", "Geotechnical", "Electrical", "Environmental"];
        foreach (var (name, i) in defaults.Concat(["Structural", "Transportation", "Architecture"]).Select((n, i) => (n, i)))
        {
            var row = new TemplateDiscipline { TemplateId = t.Id, DisciplineId = disc[name], SortOrder = i, IsDefaultIncluded = defaults.Contains(name) };
            db.TemplateDisciplines.Add(row);
            tdisc[name] = row.Id;
        }

        (string Name, string Type, int Offset, string? Phase, bool Client)[] milestones =
        [
            ("Project Kickoff", MilestoneType.Kickoff, 7, "Kickoff", true), ("Field Investigation Complete", MilestoneType.FieldWork, 45, "Field Investigation", false),
            ("30% Design Submission", MilestoneType.DesignSubmission, 90, "Preliminary Design", true), ("60% Design Submission", MilestoneType.DesignSubmission, 150, null, true),
            ("85% Design Submission", MilestoneType.DesignSubmission, 210, null, true), ("100% Design Submission", MilestoneType.DesignSubmission, 250, "Detailed Design", true),
            ("Issued for Construction (IFC)", MilestoneType.IFC, 270, "IFC", true), ("Tender Close", MilestoneType.Tender, 310, "Tender", true),
            ("Construction Start", MilestoneType.Construction, 340, "Construction", true), ("Record Drawings Issued", MilestoneType.RecordDrawings, 600, null, true),
            ("Project Closeout", MilestoneType.Closeout, 630, "Closeout", false),
        ];
        var ms = new List<Guid>();
        foreach (var (m, i) in milestones.Select((m, i) => (m, i)))
        {
            var row = new TemplateMilestone
            {
                TemplateId = t.Id, Name = m.Name, MilestoneType = m.Type, SortOrder = i + 1, Anchor = TemplateAnchor.ProjectStart, OffsetDaysFromAnchor = m.Offset,
                CompletesPhaseId = m.Phase is null ? null : phases[m.Phase], IsClientFacing = m.Client,
            };
            db.TemplateMilestones.Add(row);
            ms.Add(row.Id);
        }

        (string Disc, string Name, string Type, int M, int Offset)[] deliverables =
        [
            ("Project Management", "Project Management Plan", "Report", 1, 0), ("Project Management", "Kickoff Meeting Minutes", "Memo", 1, 3),
            ("Project Management", "Preliminary Cost Estimate", "Cost Estimate", 3, -2), ("Project Management", "Class B Cost Estimate", "Cost Estimate", 5, -2),
            ("Project Management", "Tender Package", "Tender Package", 8, -21), ("Survey", "Topographic Survey Base Plan", "Model/Base Plan", 2, 0),
            ("Survey", "Legal/Property Fabric Plan", "Drawing Package", 2, 7), ("Civil", "Existing Conditions Plan", "Drawing Package", 3, -14),
            ("Civil", "Preliminary Servicing Plan", "Drawing Package", 3, -3), ("Civil", "Grading Design", "Drawing Package", 4, -3),
            ("Civil", "Stormwater Management Report", "Report", 4, -3), ("Civil", "Road Design (Plan & Profile)", "Drawing Package", 4, -3),
            ("Civil", "Utility Coordination Plan", "Drawing Package", 5, -5), ("Civil", "Quantity Estimate", "Quantity Estimate", 5, -3),
            ("Civil", "30% Civil Drawing Package", "Drawing Package", 3, -3), ("Civil", "60% Civil Drawing Package", "Drawing Package", 4, -3),
            ("Civil", "85% Civil Drawing Package", "Drawing Package", 5, -3), ("Civil", "100% Civil Drawing Package", "Drawing Package", 6, -3),
            ("Civil", "Civil IFC Package", "IFC Package", 7, -2), ("Civil", "Specifications", "Specification", 6, -3), ("Civil", "Civil Record Drawings", "Record Drawings", 10, -5),
            ("Geotechnical", "Geotechnical Investigation Report", "Report", 3, -10), ("Geotechnical", "Pavement Design Recommendations", "Report", 4, -30),
            ("Electrical", "Street Lighting Design", "Drawing Package", 5, -5), ("Electrical", "Electrical Utility Coordination", "Memo", 5, -10),
            ("Electrical", "Electrical IFC Package", "IFC Package", 7, -2), ("Environmental", "Environmental Screening Report", "Report", 2, 14),
            ("Environmental", "Permit Submission (regulatory)", "Permit Submission", 6, -20), ("Transportation", "Traffic Management Plan", "Report", 5, -5),
        ];
        var tasks = new Dictionary<string, Guid>();
        var order = 0;
        void Task(string key, string discipline, Guid? deliverable, string name, string role, decimal hours, int? offset, bool review = false)
        {
            var row = new TemplateTask
            {
                TemplateId = t.Id, TemplateDisciplineId = tdisc[discipline], TemplateDeliverableId = deliverable, Name = name, AssignToRole = role, EstimatedHours = hours,
                DueOffsetDays = offset, RequiresReview = review, SortOrder = ++order,
            };
            db.TemplateTasks.Add(row);
            tasks[key] = row.Id;
        }
        var deps = new List<(string, string)>();
        foreach (var (d, i) in deliverables.Select((d, i) => (d, i)))
        {
            var row = new TemplateDeliverable
            {
                TemplateId = t.Id, TemplateDisciplineId = tdisc[d.Disc], Name = d.Name, DeliverableTypeId = types[d.Type], TemplateMilestoneId = ms[d.M - 1], DueOffsetDays = d.Offset,
                RequiresReview = true, SortOrder = i + 1,
            };
            db.TemplateDeliverables.Add(row);
            if (d.Name == "85% Civil Drawing Package")
            {
                // Appendix A.1 sample tasks and their dependencies.
                Task("grading", d.Disc, row.Id, "Update grading", AssignToRole.Unassigned, 24, -12);
                Task("pipes", d.Disc, row.Id, "Update pipe network", AssignToRole.Unassigned, 24, -12);
                Task("profiles", d.Disc, row.Id, "Update profiles", AssignToRole.Unassigned, 16, -10);
                Task("quantities", d.Disc, row.Id, "Update quantities", AssignToRole.Unassigned, 8, -6);
                Task("cadqa", d.Disc, row.Id, "CAD QA", AssignToRole.Unassigned, 8, -5, review: true);
                Task("techreview", d.Disc, row.Id, "Technical review", AssignToRole.DisciplineLead, 8, -3, review: true);
                Task("pmreview", d.Disc, row.Id, "PM review", AssignToRole.PM, 4, -1, review: true);
                Task("issue85", d.Disc, row.Id, "Issue package", AssignToRole.DisciplineLead, 2, 0);
                deps.AddRange([("grading", "pipes"), ("grading", "profiles"), ("pipes", "quantities"), ("profiles", "quantities"), ("quantities", "cadqa"),
                    ("cadqa", "techreview"), ("techreview", "pmreview"), ("pmreview", "issue85")]);
                continue;
            }
            var k = d.Name;
            Task($"prepare:{k}", d.Disc, row.Id, $"Prepare {d.Name}", AssignToRole.Unassigned, 16, -10);
            Task($"check:{k}", d.Disc, row.Id, $"Check {d.Name}", AssignToRole.Unassigned, 4, -5);
            Task($"review:{k}", d.Disc, row.Id, $"Review {d.Name}", AssignToRole.DisciplineLead, 4, -2, review: true);
            Task($"issue:{k}", d.Disc, row.Id, $"Issue {d.Name}", AssignToRole.DisciplineLead, 1, 0);
            deps.AddRange([($"prepare:{k}", $"check:{k}"), ($"check:{k}", $"review:{k}"), ($"review:{k}", $"issue:{k}")]);
        }
        Task("controls", "Project Management", null, "Set up project controls and filing", AssignToRole.PM, 8, 3);
        Task("meetings", "Project Management", null, "Weekly coordination meetings", AssignToRole.PM, 40, null);
        Task("reports", "Project Management", null, "Monthly client progress reports", AssignToRole.PM, 24, null);
        Task("closeout", "Project Management", null, "Project closeout checklist", AssignToRole.PM, 8, null);
        // The cross-discipline chain of A.1.
        deps.AddRange([
            ("issue:Topographic Survey Base Plan", "prepare:Existing Conditions Plan"), ("issue:Existing Conditions Plan", "prepare:Preliminary Servicing Plan"),
            ("issue:Preliminary Servicing Plan", "prepare:Electrical Utility Coordination"), ("issue:Preliminary Servicing Plan", "prepare:Pavement Design Recommendations"),
            ("issue:Pavement Design Recommendations", "grading"), ("grading", "prepare:Street Lighting Design"), ("issue:Street Lighting Design", "cadqa"),
        ]);
        foreach (var (a, b) in deps)
            db.TemplateDependencies.Add(new TemplateDependency { TemplateId = t.Id, PredecessorTemplateTaskId = tasks[a], SuccessorTemplateTaskId = tasks[b] });
        await db.SaveChangesAsync();
        return t.Id;
    }
}
