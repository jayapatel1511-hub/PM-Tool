using System.Net.Http.Headers;
using System.Text.Json;
using Azure.Core;
using Azure.Identity;
using Hub.Api.Data;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Infrastructure;

public sealed record DirectoryUser(string ObjectId, string? Mail, string DisplayName, bool Enabled, string? JobTitle, string? Office, string? ManagerObjectId);

/// Adapter for the organisation directory (§26.1). Disabled unless Graph is configured (Q3).
public interface IUserDirectory
{
    bool Enabled { get; }
    IAsyncEnumerable<DirectoryUser> Users(CancellationToken ct);
}

/// Token for Microsoft Graph through the App Service managed identity (§21 least privilege).
public sealed class GraphToken(IConfiguration cfg)
{
    readonly TokenCredential cred = new DefaultAzureCredential(new DefaultAzureCredentialOptions { ManagedIdentityClientId = cfg["Graph:ManagedIdentityClientId"] });
    public async Task<string> Get(CancellationToken ct) =>
        (await cred.GetTokenAsync(new TokenRequestContext(["https://graph.microsoft.com/.default"]), ct)).Token;
}

public sealed class GraphDirectory(IHttpClientFactory http, GraphToken token, IConfiguration cfg) : IUserDirectory
{
    public bool Enabled => cfg["Graph:DirectorySync"] == "true";

    public async IAsyncEnumerable<DirectoryUser> Users([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        var client = http.CreateClient("graph");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await token.Get(ct));
        string? url = "https://graph.microsoft.com/v1.0/users?$select=id,mail,userPrincipalName,displayName,accountEnabled,jobTitle,officeLocation&$expand=manager($select=id)&$top=500";
        while (url is not null)
        {
            using var doc = JsonDocument.Parse(await client.GetStringAsync(url, ct));
            foreach (var u in doc.RootElement.GetProperty("value").EnumerateArray())
            {
                string? S(string p) => u.TryGetProperty(p, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
                var manager = u.TryGetProperty("manager", out var m) && m.ValueKind == JsonValueKind.Object ? m.GetProperty("id").GetString() : null;
                yield return new DirectoryUser(S("id")!, S("mail") ?? S("userPrincipalName"), S("displayName") ?? "",
                    u.TryGetProperty("accountEnabled", out var en) && en.ValueKind == JsonValueKind.True, S("jobTitle"), S("officeLocation"), manager);
            }
            url = doc.RootElement.TryGetProperty("@odata.nextLink", out var next) ? next.GetString() : null;
        }
    }
}

/// Daily directory synchronisation: leavers become Inactive within 24 hours; job title, office and
/// manager (→ supervisor) follow the directory (FR-AUTH-04, §23.6). Logged with actor System.
public sealed class DirectorySyncJob : IJob
{
    public string Name => "directory-sync";
    public bool IsDue(DateTimeOffset now, DateTimeOffset? last, OrgSettings s) => Schedule.DailyAt("01:00", now, last, s);

    public async Task<object?> Run(IServiceProvider sp, CancellationToken ct)
    {
        var dir = sp.GetRequiredService<IUserDirectory>();
        if (!dir.Enabled) return new { skipped = "directory sync not configured" };
        var db = sp.GetRequiredService<HubDb>();
        var users = await db.Users.Where(u => u.EntraObjectId != null).ToDictionaryAsync(u => u.EntraObjectId!, ct);
        var offices = await db.Offices.ToListAsync(ct);
        int changed = 0, deactivated = 0;
        var managers = new List<(AppUser User, string? ManagerOid)>();
        await foreach (var d in dir.Users(ct))
        {
            if (!users.TryGetValue(d.ObjectId, out var u)) continue; // only people who have used the Hub
            if (u.IsActive && !d.Enabled) { u.IsActive = false; deactivated++; }
            if (d.JobTitle != u.JobTitle) u.JobTitle = d.JobTitle;
            if (d.Office is { } o && offices.FirstOrDefault(x => x.Name == o || x.Code == o) is { } office && u.OfficeId != office.Id) u.OfficeId = office.Id;
            managers.Add((u, d.ManagerObjectId));
        }
        foreach (var (u, mgr) in managers)
            if (mgr is not null && users.TryGetValue(mgr, out var sup) && sup.Id != u.Id && u.SupervisorId != sup.Id) u.SupervisorId = sup.Id;
        changed = db.ChangeTracker.Entries<AppUser>().Count(e => e.State == EntityState.Modified);
        await db.SaveChangesAsync(ct);
        return new { changed, deactivated };
    }
}
