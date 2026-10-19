using System.Text.Json.Nodes;
using Xunit;

namespace Hub.Tests.Api;

[Collection("api")]
public sealed class DecisionContractTests(HubFactory f)
{
    static JsonObject Resolve(JsonNode document, JsonNode schema)
    {
        var reference = schema["$ref"]?.GetValue<string>();
        if (reference is null) return schema.AsObject();
        return document["components"]!["schemas"]![reference.Split('/').Last()]!.AsObject();
    }

    [Fact]
    public async Task Decision_detail_contract_preserves_nested_permissions_and_links()
    {
        var document = await (await f.As(TestData.Alex).GetAsync("/api/v1/openapi.json")).Json();
        var response = document["paths"]!["/api/v1/decisions/{id}"]!["get"]!["responses"]!["200"]!["content"]!["application/json"]!["schema"]!;
        var detail = Resolve(document, response);
        var permissions = Resolve(document, detail["properties"]!["permissions"]!);
        var transitions = Resolve(document, permissions["properties"]!["transitions"]!["items"]!);
        Assert.Contains("edit", permissions["required"]!.AsArray().Select(x => x!.GetValue<string>()));
        Assert.Contains("transitions", permissions["required"]!.AsArray().Select(x => x!.GetValue<string>()));
        Assert.Contains("to", transitions["required"]!.AsArray().Select(x => x!.GetValue<string>()));
        Assert.Contains("ok", transitions["required"]!.AsArray().Select(x => x!.GetValue<string>()));
        var links = Resolve(document, detail["properties"]!["links"]!["items"]!);
        foreach (var key in new[] { "id", "targetType", "targetId", "relation", "key", "name" })
            Assert.Contains(key, links["properties"]!.AsObject().Select(p => p.Key));
    }

    [Fact]
    public async Task Decision_register_contract_preserves_owner_and_status_fields()
    {
        var document = await (await f.As(TestData.Alex).GetAsync("/api/v1/openapi.json")).Json();
        var response = document["paths"]!["/api/v1/projects/{id}/decisions"]!["get"]!["responses"]!["200"]!["content"]!["application/json"]!["schema"]!;
        var row = Resolve(document, response["items"]!);
        foreach (var key in new[] { "id", "projectId", "key", "status", "ownerIsClient", "blockingTaskIds", "deliverables", "milestones" })
            Assert.Contains(key, row["properties"]!.AsObject().Select(p => p.Key));
        Assert.Contains("status", row["required"]!.AsArray().Select(x => x!.GetValue<string>()));
        Assert.Contains("blockingTaskIds", row["required"]!.AsArray().Select(x => x!.GetValue<string>()));
    }
}
