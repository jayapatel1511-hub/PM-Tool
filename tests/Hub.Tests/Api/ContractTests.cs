using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace Hub.Tests.Api;

/// The test host is the only document producer: a generator must never migrate or seed a database (AC-PLAT-12).
[Collection("api")]
public sealed class ContractTests(HubFactory f)
{
    internal static string Root
    {
        get
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Hub.slnx"))) dir = dir.Parent;
            return dir?.FullName ?? throw new InvalidOperationException("Repository root not found");
        }
    }

    internal static JsonNode? Normalize(JsonNode? node) => node switch
    {
        JsonObject o => new JsonObject(o.Where(p => p.Key != "servers").OrderBy(p => p.Key, StringComparer.Ordinal)
            .Select(p => KeyValuePair.Create(p.Key, Normalize(p.Value)))),
        JsonArray a => new JsonArray(a.Select(Normalize).ToArray()),
        _ => node?.DeepClone(),
    };

    static JsonObject Resolve(JsonNode document, JsonNode schema)
    {
        var reference = schema["$ref"]?.GetValue<string>();
        if (reference is null) return schema.AsObject();
        var name = reference.Split('/').Last();
        return document["components"]!["schemas"]![name]!.AsObject();
    }

    [Fact]
    public async Task Openapi_matches_the_committed_golden_document() // AC-PLAT-12
    {
        var response = await f.As(TestData.Alex).GetAsync("/api/v1/openapi.json");
        var document = Normalize(await response.Json())!;
        var file = Path.Combine(Root, "web/src/gen/openapi.json");
        var json = document.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + "\n";
        if (Environment.GetEnvironmentVariable("HUB_UPDATE_CONTRACT") == "1")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            File.WriteAllText(file, json);
        }
        Assert.True(File.Exists(file), "Missing golden contract; regenerate with HUB_UPDATE_CONTRACT=1");
        Assert.Equal(File.ReadAllText(file), json);
    }

    [Fact]
    public void Normalization_removes_servers_and_orders_objects_without_reordering_arrays()
    {
        var input = JsonNode.Parse("{\"z\":[2,1],\"servers\":[{}],\"a\":{\"b\":null,\"a\":1}}")!;
        Assert.Equal("{\"a\":{\"a\":1,\"b\":null},\"z\":[2,1]}", Normalize(input)!.ToJsonString());
    }
    [Fact]
    public async Task Nested_dtos_with_equal_names_have_distinct_contract_schemas()
    {
        var document = await f.As(TestData.Alex).GetAsync("/api/v1/openapi.json").Result.Json();
        var body = document["paths"]!["/api/v1/open-roles/{id}/cancel"]!["post"]!["requestBody"]!["content"]!["application/json"]!["schema"]!;
        var name = body["$ref"]!.GetValue<string>().Split('/')[^1];
        var properties = document["components"]!["schemas"]![name]!["properties"]!.AsObject();
        Assert.Equal(new[] { "rowVersion", "reason" }.Order(), properties.Select(p => p.Key).Order());
        Assert.Contains("null", properties["reason"]!["type"]!.ToJsonString());
        Assert.DoesNotContain("string", properties["rowVersion"]!["type"]!.ToJsonString());
    }

    [Fact]
    public async Task Me_settings_are_typed_and_route_counter_field_is_flag_scoped()
    {
        var document = await (await f.As(TestData.Alex).GetAsync("/api/v1/openapi.json")).Json();
        var me = Resolve(document, document["paths"]!["/api/v1/me"]!["get"]!["responses"]!["200"]!["content"]!["application/json"]!["schema"]!);
        var settings = Resolve(document, me["properties"]!["settings"]!);
        var required = settings["required"]!.AsArray().Select(x => x!.GetValue<string>()).ToHashSet();
        foreach (var key in new[] { "dateFormat", "orgTimeZone", "today", "idleTimeoutHours", "chainDepthLimit", "workingDaysEnabled" })
            Assert.Contains(key, settings["properties"]!.AsObject().Select(p => p.Key));
        Assert.Contains("routeCounterEnabled", settings["properties"]!.AsObject().Select(p => p.Key));
        Assert.DoesNotContain("routeCounterEnabled", required);
        Assert.Contains("null", settings["properties"]!["routeCounterEnabled"]!["type"]!.ToJsonString());
    }

    [Fact]
    public async Task Template_preview_contract_keeps_nested_counts_and_role_fields()
    {
        var document = await (await f.As(TestData.Alex).GetAsync("/api/v1/openapi.json")).Json();
        var preview = Resolve(document, document["paths"]!["/api/v1/templates/{id}/preview"]!["post"]!["responses"]!["200"]!["content"]!["application/json"]!["schema"]!);
        var counts = Resolve(document, preview["properties"]!["counts"]!);
        foreach (var key in new[] { "milestones", "deliverables", "tasks", "dependencies", "roles" })
            Assert.Contains(key, counts["properties"]!.AsObject().Select(p => p.Key));
        Assert.DoesNotContain("roles", counts["required"]!.AsArray().Select(x => x!.GetValue<string>()));
        var roles = preview["properties"]!["roles"]!;
        var role = Resolve(document, roles["items"]!);
        foreach (var key in new[] { "id", "label", "disciplineId", "disciplineName", "from", "through", "hours", "note", "skipReason" })
            Assert.Contains(key, role["properties"]!.AsObject().Select(p => p.Key));
    }

    [Theory]
    [InlineData("/api/v1/tasks/{id}", "get", "200", "permissions.transitions[].allowed", "boolean")]
    [InlineData("/api/v1/me/onboarding", "get", "200", "groups[].steps[].done", "boolean")]
    [InlineData("/api/v1/me/work/watching", "get", "200", "items[].projectNumber", "string")]
    [InlineData("/api/v1/me/work/requested", "get", "200", "total", "integer")]
    [InlineData("/api/v1/pickup-offers", "get", "200", "[].task.rowVersion", "integer")]
    [InlineData("/api/v1/tasks/{id}/pickup-offer", "get", "200", "allowed.pickUp", "boolean")]
    [InlineData("/api/v1/admin/organisation/levels", "get", "200", "[].name", "string")]
    [InlineData("/api/v1/admin/organisation/units", "get", "200", "[].rowVersion", "integer")]
    [InlineData("/api/v1/admin/organisation/import/preview", "post", "200", "rows", "integer")]
    [InlineData("/api/v1/admin/organisation/import/preview", "post", "200", "applied", "boolean")]
    [InlineData("/api/v1/projects/{projectId}/commitment-reliability", "get", "200", "misses[].commitments[]", "string")]
    [InlineData("/api/v1/projects/{projectId}/revision-check", "get", "200", "deliverables[].issues[].marks[]", "string")]
    [InlineData("/api/v1/deliverables/{id}/baselines", "get", "200", "people[].displayName", "string")]
    [InlineData("/api/v1/admin/audit-chains", "get", "200", "[].chainId", "string")]
    [InlineData("/api/v1/projects/import-preview", "post", "200", "rows[].projectNumber", "string")]
    [InlineData("/api/v1/admin/operations/outbox/retry", "post", "200", "retried", "integer")]
    public async Task New_endpoint_contracts_keep_required_nested_wire_types(string route, string method, string status, string field, string type)
    {
        var document = await (await f.As(TestData.Alex).GetAsync("/api/v1/openapi.json")).Json();
        var schema = Resolve(document, document["paths"]![route]![method]!["responses"]![status]!["content"]!["application/json"]!["schema"]!);
        foreach (var part in field.Replace("[]", ".[]").Split('.', StringSplitOptions.RemoveEmptyEntries))
        {
            if (part == "[]")
            {
                Assert.Equal("array", schema["type"]!.GetValue<string>());
                schema = Resolve(document, schema["items"]!);
            }
            else
            {
                Assert.Contains(part, schema["required"]!.AsArray().Select(x => x!.GetValue<string>()));
                schema = Resolve(document, schema["properties"]![part]!);
            }
        }
        Assert.Equal(type, schema["type"]!.GetValue<string>());
    }

    [Theory]
    [InlineData("/api/v1/projects/{projectId}/allocations")]
    [InlineData("/api/v1/projects/{projectId}/allocations/{id}/edit")]
    [InlineData("/api/v1/projects/{projectId}/allocations/{id}/confirm")]
    public async Task Allocation_preview_contract_describes_both_saved_and_preview_responses(string route)
    {
        var document = await (await f.As(TestData.Alex).GetAsync("/api/v1/openapi.json")).Json();
        var operation = document["paths"]![route]!["post"]!;
        var parameter = Assert.Single(operation["parameters"]!.AsArray(), p => p!["name"]!.GetValue<string>() == "preview");
        Assert.Equal("boolean", parameter!["schema"]!["type"]!.GetValue<string>());
        var schema = operation["responses"]!["200"]!["content"]!["application/json"]!["schema"]!;
        var branches = schema["anyOf"]!.AsArray().Select(x => Resolve(document, x!)).ToArray();
        Assert.Equal(2, branches.Length);
        var saved = Assert.Single(branches, x => x["properties"]!["id"] is not null);
        Assert.Contains("rowVersion", saved["required"]!.AsArray().Select(x => x!.GetValue<string>()));
        var preview = Assert.Single(branches, x => x["properties"]!["preview"] is not null);
        var week = Resolve(document, preview["properties"]!["weeks"]!["items"]!);
        Assert.Equal("number", week["properties"]!["capacity"]!["type"]!.GetValue<string>());
        Assert.Equal("array", week["properties"]!["indicators"]!["type"]!.GetValue<string>());
    }


}
