using System.Text.Json.Nodes;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

/// §55.3 Backlog, packet 051 S05: the `backlog` filter and row field, the dashboard and discipline counts and the export
/// column, all behind `flag.backlog` (FR-BKL-01, FR-BKL-02 count, FR-BKL-05, FR-BKL-07, FR-BKL-08, FR-BKL-09).
[Collection("api")]
public sealed class TaskQueriesTests(HubFactory f)
{
    readonly TestData d = new(f);
    DateOnly Today => DateOnly.FromDateTime(f.Clock.Now.UtcDateTime);
    string In(int days) => Today.AddDays(days).ToString("yyyy-MM-dd");

    async Task Backlog(bool on) => (await f.As(TestData.Admin).Put("/api/v1/admin/settings/flag.backlog", new { value = on })).EnsureSuccessStatusCode();

    async Task<Guid> Add(Guid projectId, object? extra = null, string? status = null, string discipline = "Civil")
    {
        var id = (await d.NewTask(projectId, TestData.Pm, extra, discipline)).G("id");
        if (status is not null) await f.DbAsync(db => db.Tasks.Where(x => x.Id == id).ExecuteUpdateAsync(x => x.SetProperty(y => y.Status, status)));
        return id;
    }

    async Task<string> Csv(string url) => System.Text.Encoding.UTF8.GetString((await f.As(TestData.Pm).GetByteArrayAsync(url))[3..]);

    [Fact, Trait("flag-off", "backlog"), Trait("flag-on", "backlog")]
    [Trait("Spec", "AC-BKL-01")]
    public async Task Backlog_filter_counts_and_export_column_agree_and_are_absent_with_the_flag_off()
    {
        var p = await d.Project();
        var alex = d.User(TestData.Alex);
        var backlog = new List<Guid>();
        for (var i = 0; i < 4; i++) backlog.Add(await Add(p.Id, new { assigneeId = alex }));
        backlog.Add(await Add(p.Id, new { assigneeId = alex }, discipline: "Electrical"));
        backlog.Add(await Add(p.Id, new { assigneeId = alex }, TaskStatuses.OnHold));
        await Add(p.Id, new { assigneeId = alex }, TaskStatuses.InProgress); // started work is not Backlog
        var soon = await Add(p.Id, new { assigneeId = alex, dueDate = In(3) });
        await Add(p.Id, new { assigneeId = alex, startDate = In(10) });
        var late = await Add(p.Id, new { assigneeId = alex, dueDate = In(-2) });
        await Add(p.Id, new { assigneeId = alex }, TaskStatuses.Cancelled);
        await Add(p.Id, new { assigneeId = alex }, TaskStatuses.Complete);
        await f.Evaluate(p.Id);

        var pm = f.As(TestData.Pm);
        var root = $"/api/v1/projects/{p.Id}";
        async Task<List<Guid>> Ids(string q) => (await pm.GetAsync($"{root}/tasks?pageSize=200&{q}").Result.Json())["items"]!.AsArray().Select(x => x!.G("id")).ToList();
        var civil = d.ProjectDiscipline(p.Id, "Civil").ToString();
        var all = await Ids("");
        Assert.Equal(11, all.Count); // Cancelled is left out by default

        // Off: the parameter is ignored like any unknown key, and no field, count or column is added (FR-BKL-09, AC-BKL-06).
        Assert.Equal(all, await Ids("backlog=true"));
        Assert.All((await pm.GetAsync($"{root}/tasks").Result.Json())["items"]!.AsArray(), x => Assert.Null(x!["backlog"]));
        var dash = await pm.GetAsync($"{root}/dashboard").Result.Json();
        Assert.Null(dash["tasks"]!["backlog"]);
        Assert.Null(dash["disciplineBacklog"]);
        Assert.DoesNotContain("Backlog", (await Csv($"{root}/tasks/export?format=csv")).Split('\n')[0]);

        await Backlog(true);
        try
        {
            Assert.Equal(backlog.Order(), (await Ids("backlog=true")).Order());
            Assert.Equal(backlog.Order(), (await Ids("indicator=backlog")).Order());
            Assert.All((await pm.GetAsync($"{root}/tasks?pageSize=200").Result.Json())["items"]!.AsArray(),
                x => Assert.Equal(backlog.Contains(x!.G("id")), x["backlog"]!.GetValue<bool>()));

            // FR-BKL-02 and FR-BKL-07: each count equals the list its link opens.
            foreach (var scope in new[] { "", $"?disciplineId={civil}" })
            {
                var count = (await pm.GetAsync($"{root}/dashboard{scope}").Result.Json())["tasks"]!["backlog"]!;
                Assert.Contains("backlog=true", count.S("link"));
                Assert.Equal(scope == "" ? 6 : 5, count.I("value"));
                Assert.Equal(count.I("value"), (await Ids(count.S("link"))).Count);
            }
            dash = await pm.GetAsync($"{root}/dashboard").Result.Json();
            foreach (var row in dash["disciplines"]!.AsArray())
            {
                var count = dash["disciplineBacklog"]![row!.S("disciplineId")]!;
                Assert.Contains($"disciplineId={row.S("disciplineId")}", count.S("link"));
                Assert.Equal(count.I("value"), (await Ids(count.S("link"))).Count);
            }
            Assert.Equal(5, dash["disciplineBacklog"]![civil]!.I("value"));

            // FR-BKL-05: the date indicators never hold a Backlog task.
            Assert.Equal([late], await Ids("overdue=true"));
            Assert.Equal([soon], await Ids("dueSoon=true"));

            // FR-BKL-08: a Yes/No column, filtered like the list.
            var lines = (await Csv($"{root}/tasks/export?format=csv")).Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(l => l.TrimEnd('\r')).ToList();
            Assert.EndsWith(",Backlog", lines[0]);
            Assert.Equal(6, lines.Count(l => l.EndsWith(",Yes")));
            Assert.Equal(5, lines.Count(l => l.EndsWith(",No")));
            var filtered = (await Csv($"{root}/tasks/export?format=csv&backlog=true")).Split('\n', StringSplitOptions.RemoveEmptyEntries).Skip(1).ToList();
            Assert.Equal(6, filtered.Count);
            Assert.All(filtered, l => Assert.EndsWith(",Yes", l.TrimEnd('\r')));
        }
        finally { await Backlog(false); }
    }
}
