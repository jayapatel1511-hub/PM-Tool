using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Data;

/// Idempotent reference data every environment needs (§9.3, §12.4, Appendix A, §14 Workflow 1).
public static class Seed
{
    public static async Task Reference(HubDb db)
    {
        db.Audit.AsSystem("Migration");
        if (!await db.Disciplines.AnyAsync())
        {
            (string Name, string Code, string Colour)[] d =
            [
                ("Project Management", "PM", "#475569"), ("Survey", "SUR", "#0891b2"), ("Civil", "CIV", "#2563eb"),
                ("Geotechnical", "GEO", "#a16207"), ("Electrical", "ELE", "#ca8a04"), ("Environmental", "ENV", "#16a34a"),
                ("Structural", "STR", "#7c3aed"), ("Transportation", "TRN", "#db2777"), ("Architecture", "ARC", "#ea580c"),
            ];
            db.Disciplines.AddRange(d.Select((x, i) => new Discipline { Name = x.Name, Code = x.Code, Colour = x.Colour, SortOrder = i }));
        }
        if (!await db.Phases.AnyAsync())
            db.Phases.AddRange(new[] { "Proposal/Setup", "Kickoff", "Field Investigation", "Preliminary Design", "Detailed Design", "IFC", "Tender", "Construction", "Closeout" }
                .Select((n, i) => new Phase { Name = n, SortOrder = i }));
        if (!await db.DeliverableTypes.AnyAsync())
            db.DeliverableTypes.AddRange(new[] { "Drawing Package", "Report", "Specification", "Calculation", "Cost Estimate", "Quantity Estimate",
                "Permit Submission", "Tender Package", "IFC Package", "Record Drawings", "Certification", "Memo", "Model/Base Plan", "Other" }
                .Select((n, i) => new DeliverableType { Name = n, SortOrder = i }));
        if (!await db.Clients.AnyAsync())
            db.Clients.Add(new Client { Name = "Internal / TBD", ShortName = "Internal", SortOrder = 0 });
        if (!await db.Offices.AnyAsync())
            db.Offices.Add(new Office { Name = "Head Office", Code = "HO", TimeZone = "America/Halifax" });
        if (!await db.ProjectTypes.AnyAsync())
            db.ProjectTypes.AddRange(new[] { "Municipal Infrastructure", "Building", "Environmental Assessment", "Transportation" }
                .Select((n, i) => new ProjectType { Name = n, SortOrder = i }));
        // Every setting exists as a row, so the first Admin change is logged as old → new (§20.1).
        var have = await db.Settings.Select(x => x.Key).ToListAsync();
        foreach (var d in OrgSettings.Defs.Where(d => !have.Contains(d.Key)))
            db.Settings.Add(new OrgSetting { Key = d.Key, Value = System.Text.Json.JsonSerializer.Serialize(d.Default, System.Text.Json.JsonSerializerOptions.Web), UpdatedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();
    }

    /// Development-only people for the sign-in picker; never run outside Development/Testing.
    public static async Task DevUsers(HubDb db)
    {
        if (await db.Users.AnyAsync()) return;
        db.Audit.AsSystem("Migration");
        var office = await db.Offices.FirstAsync();
        AppUser U(string name, string email, string title) => new() { DisplayName = name, Email = email, JobTitle = title, OfficeId = office.Id, EntraObjectId = "dev-" + email };
        var jordan = U("Jordan Lee", "jordan@hub.test", "Applications Administrator");
        var lena = U("Lena Brooks", "lena@hub.test", "Regional Manager");
        var sam = U("Sam Patel", "sam@hub.test", "Civil Group Manager");
        var priya = U("Priya Nair", "priya@hub.test", "Project Manager");
        var marc = U("Marc Dubois", "marc@hub.test", "Senior Civil Engineer");
        var alex = U("Alex Chen", "alex@hub.test", "Civil Designer (EIT)");
        var jill = U("Jill Martin", "jill@hub.test", "Civil Designer");
        var diane = U("Diane Roy", "diane@hub.test", "Senior Technical Reviewer");
        var omar = U("Omar Haddad", "omar@hub.test", "Geotechnical Lead");
        var rita = U("Rita Gomez", "rita@hub.test", "Auditor");
        db.Users.AddRange(jordan, lena, sam, priya, marc, alex, jill, diane, omar, rita);
        sam.SupervisorId = lena.Id; alex.SupervisorId = sam.Id; jill.SupervisorId = sam.Id; marc.SupervisorId = sam.Id;
        priya.SupervisorId = lena.Id; omar.SupervisorId = lena.Id; diane.SupervisorId = lena.Id;
        void R(AppUser u, string role) => u.Roles.Add(new UserSystemRole { UserId = u.Id, Role = role, Source = RoleSource.Manual, GrantedAt = DateTimeOffset.UtcNow });
        R(jordan, SystemRole.Admin); R(lena, SystemRole.Executive); R(lena, SystemRole.Supervisor); R(sam, SystemRole.Supervisor);
        R(priya, SystemRole.ProjectManager); R(marc, SystemRole.ProjectManager); R(marc, SystemRole.Supervisor); R(rita, SystemRole.ReadOnly);
        await db.SaveChangesAsync();
    }
}
