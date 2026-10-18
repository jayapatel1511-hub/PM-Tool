namespace Hub.Domain;

/// Route keys are intentionally opaque and allowlisted. Raw URLs never cross this boundary.
public static class RouteCounter
{
    public static readonly IReadOnlySet<string> AllowedRoutes = new HashSet<string>(StringComparer.Ordinal)
    {
        "my-work",
        "home",
        "boards",
        "tasks",
        "timeline",
        "files",
        "team",
        "projects",
        "projects/:number/dashboard",
        "projects/:number/coordination",
        "projects/:number/readiness",
        "projects/:number/handoffs",
        "projects/:number/reviews",
        "projects/:number/changes",
        "projects/:number/submissions",
        "projects/:number/allocations",
        "projects/:number/design-basis",
        "projects/:number/tasks",
        "projects/:number/board",
        "projects/:number/milestones",
        "projects/:number/timeline",
        "projects/:number/deliverables",
        "projects/:number/decisions",
        "projects/:number/risks",
        "projects/:number/issues",
        "projects/:number/meetings",
        "projects/:number/files",
        "projects/:number/vacation",
        "projects/:number/calendar",
        "projects/:number/team",
        "projects/:number/activity",
        "projects/:number/settings",
        "tasks/:id",
        "notifications",
        "search",
        "reports",
        "reports/:code",
        "staff",
        "portfolio",
        "workload",
        "planner",
        "vacation",
        "calendar",
        "time",
        "people/:id/reassign-work",
        "preferences",
        "templates",
        "templates/:id",
        "admin",
        "admin/reference/:kind",
        "admin/settings",
        "admin/users",
        "admin/activity",
        "admin/operations",
        "admin/holidays",
    };

    public static string? Match(string key) => AllowedRoutes.Contains(key) ? key : null;

    public static string Band(TimeSpan elapsed) => Math.Clamp((int)Math.Round(elapsed.TotalMilliseconds), 0, 60000) switch
    {
        < 500 => "under-500",
        < 1000 => "500-999",
        < 2000 => "1000-1999",
        < 5000 => "2000-4999",
        _ => "5000+"
    };
}
