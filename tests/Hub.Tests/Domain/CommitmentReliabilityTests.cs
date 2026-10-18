using Hub.Domain;
namespace Hub.Tests.Domain;
public sealed class CommitmentReliabilityTests
{
    [Fact] public void Snapshot_denominator_keeps_withdrawn_and_rounds_half_up()
    {
        var result = CommitmentReliabilityRules.Calculate(9, 7, 1, 0, 0, new());
        Assert.Equal(78, result.Percent); Assert.Equal("Watch", result.Band); Assert.Equal(9, result.Committed); Assert.Equal(1, result.Withdrawn);
        Assert.Equal(13, CommitmentReliabilityRules.Calculate(8, 1, 0, 0, 0, new()).Percent);
        Assert.Null(CommitmentReliabilityRules.Calculate(0, 0, 0, 0, 0, new()).Percent);
    }
}
