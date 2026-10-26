using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Hub.Api.Data;
using Hub.Api.Features;
using Hub.Api.Infrastructure;
using Hub.Api.Records;
using Hub.Domain;
using Xunit;

namespace Hub.Tests.Api;

public sealed class RegistryTests
{
    static string Snapshot(string name) => Path.Combine(ContractTests.Root, "tests/Hub.Tests/Snapshots/registry", name + ".json");
    static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    [Fact]
    public void Every_audited_type_has_one_valid_declaration() // AC-PLAT-01
    {
        AssertDeclarations(EntityRegistry.Entries);
        Assert.Equal(ViewEndpoints.Lists.Keys.Order(), ScreenLists.All.Select(l => l.Code).Order());
    }

    [Fact]
    public void Audit_fields_and_history_types_preserve_the_original_snapshots() // AC-PLAT-02, AC-PLAT-03
    {
        AssertSnapshots(EntityRegistry.Entries);
        Assert.NotNull(EntityRegistry.History("OpenRole")?.Project);
        Assert.Null(EntityRegistry.History("TimeEntry"));
        Assert.Null(EntityRegistry.History("CalendarEvent"));
    }

    /// Seeded registry defects: each one must fail the check that guards it, while the real registry passes both.
    [Fact]
    public void Registry_checks_fail_on_seeded_defects() // AC-PLAT-01, AC-PLAT-02, AC-PLAT-03
    {
        var entries = EntityRegistry.Entries;
        var role = EntityRegistry.For(typeof(OpenRole));
        var unscoped = entries.First(e => e.Project is null);
        IReadOnlyList<RegistryEntry> Replace(RegistryEntry from, RegistryEntry to) => [.. entries.Select(e => e == from ? to : e)];
        foreach (var (defect, mutated) in new (string, IReadOnlyList<RegistryEntry>)[] {
            ("entry deleted", [.. entries.Where(e => e != role)]),
            ("second entry for the same type", [.. entries, role]),
            ("neither project lookup nor reason", Replace(unscoped, unscoped with { NoProjectReason = null })),
            ("field the type does not have", Replace(role, role with { Fields = [.. role.Fields, "NoSuchField"] })) })
            Assert.True(Fails(() => AssertDeclarations(mutated)), defect);
        foreach (var (defect, mutated) in new (string, IReadOnlyList<RegistryEntry>)[] {
            ("PlannedHours removed from OpenRole", Replace(role, role with { Fields = [.. role.Fields.Where(f => f != "PlannedHours")] })),
            ("OpenRole project lookup removed", Replace(role, role with { Project = null, HasProjectLookup = false, NoProjectReason = "Seeded defect" })) })
            Assert.True(Fails(() => AssertSnapshots(mutated)), defect);
    }

    static bool Fails(Action check)
    {
        try { check(); return false; }
        catch (Xunit.Sdk.XunitException) { return true; }
    }

    static void AssertDeclarations(IReadOnlyList<RegistryEntry> actual)
    {
        var audited = typeof(HubDb).Assembly.GetTypes().Where(t => !t.IsAbstract && typeof(IAuditable).IsAssignableFrom(t)).ToHashSet();
        Assert.Equal(audited.OrderBy(t => t.FullName), actual.Select(e => e.ClrType).OrderBy(t => t.FullName));
        Assert.Equal(actual.Count, actual.Select(e => e.ClrType).Distinct().Count());
        foreach (var entry in actual)
        {
            Assert.True(entry.Project is not null || !string.IsNullOrWhiteSpace(entry.NoProjectReason), entry.ClrType.Name);
            Assert.Equal(entry.HasProjectLookup, entry.Project is not null);
            foreach (var field in entry.Fields) Assert.NotNull(entry.ClrType.GetProperty(field));
            var entity = (IAuditable)Activator.CreateInstance(entry.ClrType)!;
            // ItemLink writes its parent's stored type, never a new item-history surface.
            if (entity is not ItemLink) Assert.Equal(entity.AuditType, entry.ItemType);
        }
    }

    static void AssertSnapshots(IReadOnlyList<RegistryEntry> entries)
    {
        var original = JsonSerializer.Deserialize<Dictionary<string, string[]>>(File.ReadAllText(Snapshot("audit-fields")))!;
        foreach (var (type, fields) in original)
        {
            var entry = Assert.Single(entries, entry => entry.ClrType.Name == type);
            Assert.Equal(fields, entry.Fields.Where(fields.Contains));
        }
        var history = JsonSerializer.Deserialize<string[]>(File.ReadAllText(Snapshot("item-history-types")))!;
        var lookups = entries.Where(e => e.Project is not null).Select(e => e.ItemType).ToList();
        Assert.Equal(history.Order(), lookups.Order());
        Assert.Contains("PlannedHours", entries.Single(e => e.ClrType == typeof(OpenRole)).Fields);
        Assert.Contains("OpenRole", lookups);
        Assert.DoesNotContain("TimeEntry", lookups);
        Assert.DoesNotContain("CalendarEvent", lookups);
    }

    [Fact]
    public void Saved_views_exports_names_and_search_are_snapshot_supersets() // AC-PLAT-04, AC-PLAT-05
    {
        var views = JsonSerializer.Deserialize<Dictionary<string, string[]>>(File.ReadAllText(Snapshot("saved-view-keys")))!;
        foreach (var (code, keys) in views)
            foreach (var key in keys) Assert.Contains(key, ScreenLists.SavedViewKeys[code]);
        var search = JsonSerializer.Deserialize<string[]>(File.ReadAllText(Snapshot("search-groups")))!;
        Assert.Equal(search, ScreenLists.SearchGroups);
        foreach (var (file, selected) in new[] {
            ("activity-names", EntityRegistry.Entries.Where(e => e.Names is not null).OrderBy(e => e.NameOrder)),
            ("export-names", EntityRegistry.Entries.Where(e => e.ExportNames is not null).OrderBy(e => e.ExportNameOrder)) })
            Assert.Equal(JsonSerializer.Deserialize<string[]>(File.ReadAllText(Snapshot(file)))!, selected.Select(e => e.ClrType.Name));
        var exports = JsonNode.Parse(File.ReadAllText(Snapshot("export-columns")))!.AsObject();
        foreach (var (key, columns) in exports)
        {
            var field = typeof(EntityRegistry).Assembly.GetTypes().Where(t => t.Namespace == typeof(EntityRegistry).Namespace)
                .SelectMany(t => t.GetFields(BindingFlags.Public | BindingFlags.Static)).Single(f => f.Name == key.Replace(".", ""));
            var actual = ((Col[])field.GetValue(null)!).Select(c => new { c.Path, c.Header, c.Type, c.Label });
            Assert.True(JsonNode.DeepEquals(columns, JsonSerializer.SerializeToNode(actual, Json)), key);
        }
    }

    [Fact]
    public void Generated_key_families_match_the_registry_and_vocabulary() // AC-PLAT-10
    {
        var itemKeys = EntityRegistry.Entries.Where(e => e.ClrType != typeof(ItemLink)).Select(e => "itemType." + e.ItemType).Distinct().Order();
        var fieldKeys = EntityRegistry.Entries.SelectMany(e => e.Fields).Distinct().Order().Select(f => "field." + f);
        var source = File.ReadAllText(Path.Combine(ContractTests.Root, "src/Hub.Domain/Vocabulary.cs"));
        var values = Regex.Matches(source, @"public static class (\w+)").Select(m => typeof(SystemRole).Assembly.GetType("Hub.Domain." + m.Groups[1].Value)!)
            .SelectMany(t => t.GetFields(BindingFlags.Public | BindingFlags.Static)).Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!).Distinct().Order();
        static string Array(string name, IEnumerable<string> values) => "export const " + name + " = " + JsonSerializer.Serialize(values) + " as const\n";
        var text = "/** Generated by RegistryTests; do not edit. */\n" + Array("itemTypeKeys", itemKeys) + Array("fieldKeys", fieldKeys) + Array("vocabularyValues", values)
            + "export type GeneratedItemTypeKey = typeof itemTypeKeys[number]\nexport type GeneratedFieldKey = typeof fieldKeys[number]\nexport type VocabularyValue = typeof vocabularyValues[number]\n"
            + "export interface SavedViewKeys {\n" + string.Join("\n", ScreenLists.All.OrderBy(l => l.Code).Select(l => "  " + JsonSerializer.Serialize(l.Code) + ": " + string.Join(" | ", l.Keys.Concat(l.NotKept.Keys).Distinct().Order().Select(k => JsonSerializer.Serialize(k))))) + "\n}\n";
        var file = Path.Combine(ContractTests.Root, "web/src/gen/registry.gen.ts");
        var golden = File.ReadAllText(file);
        if (Environment.GetEnvironmentVariable("HUB_UPDATE_REGISTRY") == "1" && golden != text)
        {
            File.WriteAllText(file, text);
            // An update run still fails on drift, so a variable left set cannot hide a registry change.
            Assert.Fail("Updated web/src/gen/registry.gen.ts; review the diff and rerun without HUB_UPDATE_REGISTRY");
        }
        Assert.Equal(golden, text);
    }
}
