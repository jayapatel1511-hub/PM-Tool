using System.Collections.Concurrent;
using System.Text.Json;

namespace Hub.Tests.Api;

public sealed class ShapeRecorderTests
{
    [Fact]
    public void Dictionary_ids_are_wildcards_but_child_fields_and_value_kinds_are_preserved()
    {
        using var document = JsonDocument.Parse("{\"people\":{\"00000000-0000-0000-0000-000000000001\":{\"name\":\"Synthetic\"},\"00000000-0000-0000-0000-000000000002\":{\"name\":2}}}");
        var paths = new ConcurrentDictionary<string, ConcurrentDictionary<string, byte>>();
        ShapeRecorder.Visit(document.RootElement, "$", paths);
        Assert.Equal(new[] { "number", "string" }, paths["$.people.{id}.name"].Keys.Order());
        Assert.DoesNotContain(paths.Keys, path => path.Contains("00000000"));
    }
}
