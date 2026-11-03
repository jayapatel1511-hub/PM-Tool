using Xunit;

namespace Hub.Tests.Api;

/// §56.5: the visual-system read stamp is present only while its flag is enabled.
[Collection("api")]
public sealed class VisualAsOfTests(HubFactory f)
{
    [Fact, Trait("flag-off", "visual_system"), Trait("flag-on", "visual_system")]
    public async Task New_as_of_reads_follow_the_visual_system_flag_and_clock()
    {
        var data = new TestData(f);
        var project = await data.Project();
        var alex = data.User(TestData.Alex);
        var from = new DateOnly(2026, 9, 14);
        var pm = f.As(TestData.Pm);
        var sam = f.As(TestData.Sam);
        var alexClient = f.As(TestData.Alex);
        var routes = new (HttpClient Client, string Path, string Field, bool Always)[]
        {
            (pm, "/api/v1/home", "home", true),
            (pm, $"/api/v1/projects/{project.Id}/dashboard", "dashboard", false),
            (pm, $"/api/v1/portfolio?ids={project.Id}", "portfolio", false),
            (sam, $"/api/v1/workload?projectId={project.Id}", "workload", false),
            (sam, $"/api/v1/planning/grid?personId={alex}&from={from:yyyy-MM-dd}&weeks=1", "planning/grid", false),
            (sam, $"/api/v1/planning/entries?personId={alex}&from={from:yyyy-MM-dd}", "planning/list", false),
            (sam, "/api/v1/staff", "staff", false),
            (pm, $"/api/v1/reports/overdue-tasks?projectId={project.Id}&minDaysOverdue=1", "report", false),
            (alexClient, $"/api/v1/time?from={from:yyyy-MM-dd}&to={from.AddDays(6):yyyy-MM-dd}", "time", false),
        };

        var admin = f.As(TestData.Admin);
        try
        {
            await (await admin.Put("/api/v1/admin/settings/flag.visual_system", new { value = false })).Json();
            foreach (var route in routes)
                Assert.Equal(route.Always, (await (await route.Client.GetAsync(route.Path)).Json()).AsObject().ContainsKey("asOf"));

            await (await admin.Put("/api/v1/admin/settings/flag.visual_system", new { value = true })).Json();
            foreach (var route in routes)
            {
                var response = await (await route.Client.GetAsync(route.Path)).Json();
                Assert.True(response.AsObject().ContainsKey("asOf"), route.Field);
                Assert.Equal(f.Clock.Now, response["asOf"]!.GetValue<DateTimeOffset>());
            }

            await (await admin.Put("/api/v1/admin/settings/flag.visual_system", new { value = false })).Json();
            foreach (var route in routes)
                Assert.Equal(route.Always, (await (await route.Client.GetAsync(route.Path)).Json()).AsObject().ContainsKey("asOf"));
        }
        finally
        {
            await admin.Put("/api/v1/admin/settings/flag.visual_system", new { value = false });
        }
    }
}
