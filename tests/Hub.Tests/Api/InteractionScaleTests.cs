using System.Diagnostics;
using System.Text.Json;
using Hub.Api.Data;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;
using Xunit.Abstractions;

namespace Hub.Tests.Api;

public sealed class InteractionScaleTests(ITestOutputHelper output)
{
    [Fact, Trait("Spec", "AC-INT-21"), Trait("Category", "LocalScaleMeasurement")]
    public async Task Measure_ranked_picker_and_task_actor_projection_with_500_tasks_and_100_concurrent_readers()
    {
        using var f = new HubFactory();
        f.Settings["ConnectionStrings:Hub"] = $"Host=localhost;Port=55432;Database={f.Database};Username=hub;Maximum Pool Size=20";
        var d = new TestData(f);
        var project = await d.Project();
        var discipline = d.ProjectDiscipline(project.Id, "Civil");
        var users = Enumerable.Range(1, 100).Select(i => new AppUser { Email = $"scale-interaction-{i}@hub.test", DisplayName = $"Synthetic reader {i:D3}" }).ToArray();
        var alex = d.User(TestData.Alex);
        await f.DbAsync(async db =>
        {
            db.Users.AddRange(users);
            db.Tasks.AddRange(Enumerable.Range(1, 500).Select(i => new WorkTask { ProjectId = project.Id, ProjectDisciplineId = discipline,
                AssigneeId = alex, Seq = i, Key = $"{project.ProjectNumber}-T{i:D4}", Name = $"Synthetic scale task {i:D3}", LastActivityAt = f.Clock.Now }));
            return await db.SaveChangesAsync();
        });
        foreach (var user in users)
        {
            using var client = f.As(user.Email);
            (await client.GetAsync("/api/v1/me")).EnsureSuccessStatusCode();
        }
        var paths = new[] { $"/api/v1/projects/{project.Id}/tasks?pageSize=100", $"/api/v1/users?projectId={project.Id}&q=Synthetic&limit=100" };
        // Matched OFF/ON and ON/OFF pairs control order without discarding slow observations.
        var order = new[] { false, true, true, false };
        for (var phase = 0; phase < order.Length; phase++)
        {
            var enabled = order[phase];
            await (await f.As(TestData.Admin).Put("/api/v1/admin/settings/flag.picker_project_rank", new { value = enabled })).Json();
            await (await f.As(TestData.Admin).Put("/api/v1/admin/settings/flag.small_ux", new { value = enabled })).Json();
            foreach (var path in paths)
            {
                using var warm = f.As(users[0].Email);
                var body = await (await warm.GetAsync(path)).Json();
                if (path.Contains("/tasks?"))
                {
                    Assert.Equal(500, body.I("totalCount")); Assert.Equal(100, body["items"]!.AsArray().Count);
                    Assert.Equal(enabled, body["items"]![0]!["lastActivityActorName"] is not null);
                }
                else Assert.Equal(100, body.AsArray().Count);
                async Task<double[]> Batch() => await Task.WhenAll(users.Select(async user =>
                {
                    using var client = f.As(user.Email);
                    var timer = Stopwatch.StartNew();
                    using var response = await client.GetAsync(path);
                    var bytes = await response.Content.ReadAsByteArrayAsync();
                    timer.Stop();
                    Assert.True(response.IsSuccessStatusCode, $"{response.StatusCode}: {System.Text.Encoding.UTF8.GetString(bytes)}");
                    return timer.Elapsed.TotalMilliseconds;
                }));
                // Two full-concurrency warmup batches per phase precede three recorded batches.
                for (var warmup = 0; warmup < 2; warmup++) await Batch();
                var measured = new List<double>();
                for (var round = 0; round < 3; round++) measured.AddRange(await Batch());
                var samples = measured.Order().ToArray();
                output.WriteLine(JsonSerializer.Serialize(new { scope = "Local warmed TestServer/PostgreSQL; excludes browser/corporate network and pilot infrastructure", taskCount = 500,
                    concurrentUsers = 100, databasePoolLimit = 20, phase = phase + 1, pairOrder = phase < 2 ? "OFF-ON" : "ON-OFF", warmupBatches = 2, warmupRequests = 200, measuredBatches = 3, flagOn = enabled, request = path.Contains("/tasks?") ? "task-list-100-with-actor" : "project-ranked-picker-100",
                    samples = samples.Length, measuredSamplesMs = measured, p50Ms = samples[149], p95Ms = samples[284], maxMs = samples[^1], apiTargetMs = 500, meetsLocalApiTarget = samples[284] <= 500 }));
            }
        }
    }
}
