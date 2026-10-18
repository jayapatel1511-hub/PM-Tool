using System.Net;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Hub.Tests.Api;

/// Packet 044 FR-OPS-18, AC-OPS-17: every Admin route, read from the application's own endpoint list, refuses a
/// team member and a Project Manager before it changes anything.
[Collection("api")]
public sealed class AdminSweepTests(HubFactory f)
{
    /// Routes whose binding fails before the Admin check can run, each with its reason. Keep empty unless a route
    /// genuinely cannot reach its check; a 400 here instead of 403 is not a pass.
    static readonly Dictionary<string, string> BindsFirst = [];

    static IEnumerable<(string Method, string Url)> AdminRoutes(IServiceProvider services) =>
        services.GetServices<EndpointDataSource>().SelectMany(s => s.Endpoints).OfType<RouteEndpoint>()
            .Where(e => ("/" + (e.RoutePattern.RawText ?? "").TrimStart('/')).StartsWith("/api/v1/admin", StringComparison.OrdinalIgnoreCase))
            .SelectMany(e => (e.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? ["GET"]).Select(m => (m, Url(e.RoutePattern))))
            .Distinct();

    /// Placeholder route values: a fresh GUID for guid-constrained or id-like parameters, 1 for int, otherwise "x".
    static string Url(RoutePattern p)
    {
        var sb = new StringBuilder();
        foreach (var seg in p.PathSegments)
        {
            sb.Append('/');
            foreach (var part in seg.Parts)
                sb.Append(part switch
                {
                    RoutePatternLiteralPart l => l.Content,
                    RoutePatternSeparatorPart s => s.Content,
                    RoutePatternParameterPart q => Placeholder(q),
                    _ => "",
                });
        }
        return sb.ToString();
    }

    static string Placeholder(RoutePatternParameterPart q)
    {
        var constraints = q.ParameterPolicies.Select(x => x.Content ?? "").ToArray();
        if (constraints.Contains("guid") || q.Name.EndsWith("id", StringComparison.OrdinalIgnoreCase)) return Guid.NewGuid().ToString();
        if (constraints.Contains("int") || constraints.Contains("long")) return "1";
        if (constraints.Contains("datetime")) return "2026-09-14";
        return "x";
    }

    [Fact]
    public async Task Every_admin_route_refuses_team_members_and_project_managers()
    {
        var routes = AdminRoutes(f.Services).ToList();
        Assert.Contains(routes, r => r.Method == "POST" && r.Url.StartsWith("/api/v1/admin/evaluate/")); // mapped outside the Admin group
        Assert.True(routes.Count > 10, $"Expected the Admin route list, found {routes.Count}");

        var before = await f.DbAsync(db => db.ActivityLog.CountAsync());
        var wrong = new List<string>();
        foreach (var who in new[] { TestData.Alex, TestData.Pm })
        {
            var c = f.As(who);
            foreach (var (method, url) in routes)
            {
                var req = new HttpRequestMessage(new HttpMethod(method), url);
                if (method is not ("GET" or "HEAD" or "DELETE")) req.Content = new StringContent("{}", Encoding.UTF8, "application/json");
                var res = await c.SendAsync(req);
                if (res.StatusCode != HttpStatusCode.Forbidden && !BindsFirst.ContainsKey(RouteKey(url)))
                    wrong.Add($"{who} {method} {url} -> {(int)res.StatusCode} {await res.Content.ReadAsStringAsync()}");
            }
        }
        Assert.True(wrong.Count == 0, "Admin routes not refused:\n" + string.Join("\n", wrong));
        Assert.Equal(before, await f.DbAsync(db => db.ActivityLog.CountAsync()));
    }

    static string RouteKey(string url) => string.Join('/', url.Split('/').Select(s => Guid.TryParse(s, out _) ? "{id}" : s));
}
