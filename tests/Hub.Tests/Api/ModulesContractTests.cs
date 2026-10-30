using System.Net;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Hosting;

namespace Hub.Tests.Api;

/// Pins the bytes of GET /config (the public sign-in configuration) and the status of POST /admin/evaluate/{projectId} for the
/// typed records of packet 040 slice S15: property names and order, the arrays and nulls that stay in the body, and the 204.
[Collection("api")]
public sealed class ModulesContractTests(HubFactory f)
{
    static string[] Keys(JsonNode? n) => [.. n!.AsObject().Select(x => x.Key)];
    static string[] Order = ["authMode", "localPasswordEnabled", "providers", "entra"];
    static string[] EntraOrder = ["clientId", "tenantId", "apiScope"];

    static async Task<JsonNode> Config(HttpClient c)
    {
        var response = await c.GetAsync("/api/v1/config");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json; charset=utf-8", response.Content.Headers.ContentType?.ToString());
        return await response.Json();
    }

    [Fact]
    public async Task Development_config_keeps_its_names_order_empty_providers_and_blank_entra_values()
    {
        using var host = f.WithWebHostBuilder(b => { });
        var c = await Config(host.CreateClient()); // anonymous: no sign-in header
        Assert.Equal(Order, Keys(c));
        Assert.False(c["localPasswordEnabled"]!.GetValue<bool>());
        Assert.Equal("Development", c.S("authMode"));
        Assert.Empty(c["providers"]!.AsArray()); // the empty array stays in the body
        Assert.Equal(EntraOrder, Keys(c["entra"]));
        Assert.All(EntraOrder, k => Assert.Equal("", c["entra"]!.S(k))); // appsettings.json blanks them; they are present, not omitted
    }

    [Fact]
    public async Task Entra_config_echoes_the_public_client_values_and_never_the_audience()
    {
        using var host = f.WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Production");
            b.UseSetting("Auth:Mode", "Entra");
            b.UseSetting("Auth:Entra:TenantId", "00000000-0000-0000-0000-000000000001");
            b.UseSetting("Auth:Entra:Audience", "api://hub-test");
            b.UseSetting("Auth:Entra:SpaClientId", "spa-client");
            b.UseSetting("Auth:Entra:ApiScope", "api://hub-test/access");
        });
        var c = await Config(host.CreateClient());
        Assert.Equal(Order, Keys(c));
        Assert.False(c["localPasswordEnabled"]!.GetValue<bool>());
        Assert.Equal("Entra", c.S("authMode"));
        Assert.Empty(c["providers"]!.AsArray());
        Assert.Equal(EntraOrder, Keys(c["entra"]));
        Assert.Equal(("spa-client", "00000000-0000-0000-0000-000000000001", "api://hub-test/access"), (c["entra"]!.S("clientId"), c["entra"]!.S("tenantId"), c["entra"]!.S("apiScope")));
    }

    [Fact]
    public async Task Social_config_lists_only_the_configured_providers_and_no_secret()
    {
        var keys = Directory.CreateTempSubdirectory("tuesday-config-").FullName;
        try
        {
            using var host = f.WithWebHostBuilder(b =>
            {
                b.UseSetting("Auth:Mode", "Social");
                b.UseSetting("Auth:Social:KeyDirectory", Path.Combine(keys, "keys"));
                b.UseSetting("Auth:Social:Google:ClientId", "synthetic-client");
                b.UseSetting("Auth:Social:Google:ClientSecret", "synthetic-secret");
            });
            var response = await host.CreateClient().GetAsync("/api/v1/config");
            var c = await response.Json();
            Assert.Equal(Order, Keys(c));
            Assert.False(c["localPasswordEnabled"]!.GetValue<bool>());
            Assert.Equal("Social", c.S("authMode"));
            Assert.Equal(["Google"], c["providers"]!.AsArray().Select(x => x!.GetValue<string>()));
            Assert.Equal(EntraOrder, Keys(c["entra"]));
            Assert.DoesNotContain("synthetic-secret", await response.Content.ReadAsStringAsync());
        }
        finally { Directory.Delete(keys, true); }
    }

    [Fact]
    public async Task Admin_evaluate_answers_204_with_no_body_to_an_admin_and_refuses_everyone_else()
    {
        var project = await new TestData(f).Project();
        var path = $"/api/v1/admin/evaluate/{project.Id}";
        var admin = await f.As(TestData.Admin).PostAsync(path, null);
        Assert.Equal(HttpStatusCode.NoContent, admin.StatusCode);
        Assert.Equal("", await admin.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.NoContent, (await f.As(TestData.Admin).PostAsync($"/api/v1/admin/evaluate/{Guid.NewGuid()}", null)).StatusCode); // an unknown project is a no-op, as before
        Assert.Equal(HttpStatusCode.Forbidden, (await f.As(TestData.Pm).PostAsync(path, null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await f.CreateClient().PostAsync(path, null)).StatusCode);
    }
}
