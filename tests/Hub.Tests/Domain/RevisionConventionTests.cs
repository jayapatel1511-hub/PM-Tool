using Hub.Domain;
namespace Hub.Tests.Domain;
public sealed class RevisionConventionTests
{
    [Theory]
    [InlineData("letters", " b ", "C", "Out of sequence")]
    [InlineData("letters", "Rev 1", "C", "Non-standard revision: expected A, B, C …")]
    [InlineData("numbers", "2", "10", "Out of sequence")]
    [InlineData("pc", "P99", "C01", "Out of sequence")]
    public void Presets_mark_without_rejecting(string preset, string code, string prior, string mark) => Assert.Equal([mark], new RevisionConvention(preset).Marks(code, prior));
    [Fact] public void Custom_codes_are_ordered_and_case_insensitive()
    {
        var c = new RevisionConvention("custom", ["Draft", "For review", "Final"]);
        Assert.True(c.Valid); Assert.Empty(c.Marks(" final ", "Draft")); Assert.Equal(["Out of sequence"], c.Marks("Draft", "Final"));
        Assert.False(new RevisionConvention("custom", ["a", " A "]).Valid);
    }
}
