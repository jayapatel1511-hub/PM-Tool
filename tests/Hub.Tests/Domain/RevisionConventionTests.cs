using Hub.Domain;
namespace Hub.Tests.Domain;
public sealed class RevisionConventionTests
{
    [Theory]
    [InlineData("letters", " b ", "C", RevisionConvention.OutOfSequence)]
    [InlineData("letters", "Rev 1", "C", RevisionConvention.NonStandard)]
    [InlineData("numbers", "2", "10", RevisionConvention.OutOfSequence)]
    [InlineData("pc", "P99", "C01", RevisionConvention.OutOfSequence)]
    public void Presets_mark_without_rejecting(string preset, string code, string prior, string mark) => Assert.Equal([mark], new RevisionConvention(preset).Marks(code, prior));
    [Fact] public void Custom_codes_are_ordered_and_case_insensitive()
    {
        var c = new RevisionConvention("custom", ["Draft", "For review", "Final"]);
        Assert.True(c.Valid); Assert.Empty(c.Marks(" final ", "Draft")); Assert.Equal([RevisionConvention.OutOfSequence], c.Marks("Draft", "Final"));
        Assert.False(new RevisionConvention("custom", ["a", " A "]).Valid);
    }

    [Theory]
    [InlineData("01")]
    [InlineData("2147483648")]
    [InlineData(null)]
    [InlineData("")]
    public void Numeric_invalid_values_are_nonstandard(string? code)
    {
        var c = new RevisionConvention("numbers");
        Assert.Null(c.Position(code));
        Assert.Equal([RevisionConvention.NonStandard], c.Marks(code, null));
    }

    [Fact]
    public void Pc_sequence_accepts_c01_after_p99_but_rejects_p00()
    {
        var c = new RevisionConvention("pc");
        Assert.Equal(101, c.Position("C01"));
        Assert.Empty(c.Marks("C01", "P99"));
        Assert.Null(c.Position("P00"));
        Assert.Equal([RevisionConvention.NonStandard], c.Marks("P00", null));
    }

    [Fact]
    public void Custom_missing_or_blank_values_are_nonstandard()
    {
        var c = new RevisionConvention("custom", ["Draft", "For review", "Final"]);
        Assert.Null(c.Position("Missing"));
        Assert.Null(c.Position(null));
        Assert.Equal([RevisionConvention.NonStandard], c.Marks(" ", null));
    }

    [Fact]
    public void Builtin_conventions_are_valid_and_custom_limits_are_enforced()
    {
        Assert.All(new[] { "letters", "numbers", "pc" }, preset => Assert.True(new RevisionConvention(preset).Valid));
        Assert.False(new RevisionConvention("custom", null).Valid);
        Assert.False(new RevisionConvention("custom", []).Valid);
        Assert.False(new RevisionConvention("custom", Enumerable.Repeat("x", 201).ToArray()).Valid);
        Assert.False(new RevisionConvention("custom", [" "]).Valid);
        Assert.False(new RevisionConvention("custom", [new string('x', 51)]).Valid);
        Assert.False(new RevisionConvention("custom", ["A", " a "]).Valid);
        Assert.True(new RevisionConvention("custom", Enumerable.Range(0, 200).Select(i => $"R{i}").ToArray()).Valid);
    }
}
