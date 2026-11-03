using Hub.Domain;

namespace Hub.Tests.Domain;

public sealed class IdentityColourTests
{
    [Fact]
    public void Palette_is_exact_and_least_used_breaks_ties_in_listed_order()
    {
        Assert.Equal(["#ec6a3e", "#20a36d", "#d9467b", "#1597cc", "#84a32a"], IdentityColour.Palette);
        Assert.Equal(IdentityColour.Coral, IdentityColour.LeastUsed([IdentityColour.Mint, IdentityColour.Rose]));
        Assert.True(IdentityColour.IsValid("#EC6A3E"));
        Assert.False(IdentityColour.IsValid("#ffffff"));
    }

    [Fact]
    public void Least_used_counts_only_values_and_is_deterministic()
    {
        Assert.Equal(IdentityColour.Cyan, IdentityColour.LeastUsed([IdentityColour.Coral, IdentityColour.Mint, IdentityColour.Rose, IdentityColour.Lime, IdentityColour.Coral]));
    }
}
