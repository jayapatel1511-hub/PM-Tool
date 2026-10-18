using System.Text.Json;
using Hub.Domain;
using Hub.Api.Data;
namespace Hub.Tests.Domain;
public sealed class AuditChainTests
{
    [Fact]
    public void Canonical_objects_sort_recursively_and_keep_unicode()
    {
        using var a = JsonDocument.Parse("{\"z\":4.50,\"a\":{\"y\":1E30,\"x\":1e-27},\"text\":\"€\\u000f\\n\"}");
        Assert.Equal("{\"a\":{\"x\":1e-27,\"y\":1e+30},\"text\":\"€\\u000f\\n\",\"z\":4.5}", AuditChain.Canonical(a.RootElement));
        using var b = JsonDocument.Parse("{\"text\":\"€\\u000f\\n\",\"a\":{\"x\":1e-27,\"y\":1e30},\"z\":4.5}");
        Assert.Equal(AuditChain.Hash(a.RootElement), AuditChain.Hash(b.RootElement));
    }
    [Theory]
    [InlineData("-0", "0")]
    [InlineData("1e-6", "0.000001")]
    [InlineData("1e-7", "1e-7")]
    [InlineData("1e20", "100000000000000000000")]
    [InlineData("1e21", "1e+21")]
    [InlineData("333333333.33333329", "333333333.3333333")]
    public void Rfc_numbers(string input, string expected)
    { using var doc = JsonDocument.Parse(input); Assert.Equal(expected, AuditChain.Canonical(doc.RootElement)); }
    [Fact]
    public void Stored_fields_affect_hash_but_microsecond_normalization_round_trips()
    {
        var row = new ActivityLog { Id = Guid.NewGuid(), ChainId = Guid.NewGuid(), ChainSeq = 1, PrevHash = AuditChain.ZeroHash,
            OccurredAt = DateTimeOffset.Parse("2026-10-18T12:34:56.1234567Z"), Changes = "[]" };
        var original = AuditChainRows.Hash(row);
        foreach (var property in typeof(ActivityLog).GetProperties().Where(p => p.Name is not (nameof(ActivityLog.RowHash) or nameof(ActivityLog.OccurredAt))))
        {
            var value = property.GetValue(row);
            if (property.PropertyType == typeof(string)) property.SetValue(row, property.Name is "Changes" or "Snapshot" ? "{\"x\":1}" : "changed");
            else if (property.PropertyType == typeof(Guid) || property.PropertyType == typeof(Guid?)) property.SetValue(row, Guid.NewGuid());
            else if (property.PropertyType == typeof(long?)) property.SetValue(row, 2L);
            else if (property.PropertyType == typeof(List<string>)) property.SetValue(row, new List<string> { "changed" });
            else continue;
            Assert.NotEqual(original, AuditChainRows.Hash(row)); property.SetValue(row, value);
        }
        row.OccurredAt = AuditChainRows.Microseconds(row.OccurredAt); Assert.Equal(original, AuditChainRows.Hash(row));
        row.OccurredAt = row.OccurredAt.AddTicks(10); Assert.NotEqual(original, AuditChainRows.Hash(row));
    }
}
