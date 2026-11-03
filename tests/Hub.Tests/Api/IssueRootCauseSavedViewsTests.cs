using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

/// AC-SCR-71 / AC-SCR-84: saved cause cohorts retain real retired references and hide/drop the addition while dark.
public sealed class IssueRootCauseSavedViewsTests
{
    [Fact]
    public async Task Active_retired_and_unknown_views_roundtrip_but_invalid_references_never_save()
    {
        using var f = new HubFactory(); var data = new TestData(f); var p = await data.Project();
        async Task Flag(bool on) => await (await f.As(TestData.Admin).Put("/api/v1/admin/settings/flag.register_screens", new { value = on })).Json();
        await Flag(true);
        var category = await (await f.As(TestData.Admin).Post("/api/v1/admin/issueRootCauses", new { name = "Synthetic saved cause" })).Json(201);
        var id = category.G("id");
        async Task<JsonNode> Save(string cause, int status = 201) => await (await f.As(TestData.Alex).Post("/api/v1/views", new {
            name = "Synthetic cause cohort " + Guid.NewGuid().ToString("N"), listType = "issues", projectId = p.Id,
            @params = new Dictionary<string, string> { ["rootCause"] = cause, ["status"] = "Resolved" } })).Json(status);
        var saved = await Save(id.ToString());
        var userViews = await (await f.As(TestData.Alex).GetAsync($"/api/v1/views?listType=issues&projectId={p.Id}")).Json();
        var row = userViews["views"]!.AsArray().Single(row => row!.G("id") == saved.G("id"))!;
        Assert.Equal(id.ToString(), row["params"]!.S("rootCause")); Assert.Empty(row["dropped"]!.AsArray());
        await (await f.As(TestData.Admin).Patch($"/api/v1/admin/issueRootCauses/{id}", new { isActive = false }, category.I("rowVersion"))).Json();
        await Save(id.ToString()); await Save("not-recorded");
        var count = f.Db(db => db.SavedViews.Count());
        foreach (var invalid in new[] { "bad-id", Guid.Empty.ToString(), Guid.NewGuid().ToString(), id + "," + id }) await Save(invalid, 400);
        Assert.Equal(count, f.Db(db => db.SavedViews.Count()));

        var before = f.Db(db => db.SavedViews.AsNoTracking().Where(view => view.Id == saved.G("id")).Select(view => new { view.Filters, view.RowVersion }).Single());
        await Flag(false);
        var dark = await (await f.As(TestData.Alex).GetAsync($"/api/v1/views?listType=issues&projectId={p.Id}")).Json();
        var darkRow = dark["views"]!.AsArray().Single(row => row!.G("id") == saved.G("id"))!;
        Assert.False(darkRow["params"]!.AsObject().ContainsKey("rootCause"));
        Assert.Contains("rootCause", darkRow["dropped"]!.AsArray().Select(value => value!.GetValue<string>()));
        var ignored = await Save("bad-id");
        Assert.DoesNotContain("rootCause", f.Db(db => db.SavedViews.Where(view => view.Id == ignored.G("id")).Select(view => view.Filters).Single()));
        var after = f.Db(db => db.SavedViews.AsNoTracking().Where(view => view.Id == saved.G("id")).Select(view => new { view.Filters, view.RowVersion }).Single());
        Assert.Equal(before, after); // Reading the cleaned projection must not rewrite stored history.
        await Flag(true);
        var restored = await (await f.As(TestData.Alex).GetAsync($"/api/v1/views?listType=issues&projectId={p.Id}")).Json();
        Assert.Equal(id.ToString(), restored["views"]!.AsArray().Single(row => row!.G("id") == saved.G("id"))!["params"]!.S("rootCause"));
    }
}
