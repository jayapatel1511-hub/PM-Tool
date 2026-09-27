using Hub.Domain;

namespace Hub.Tests.Domain;

public sealed class DesignBasisTests
{
    [Fact]
    public void Numeric_confirmation_requires_units_evidence_rationale_and_independence()
    {
        Assert.False(BasisRules.MayConfirm(BasisStatus.Proposed, 100, null, true, true, true));
        Assert.True(BasisRules.MayConfirm(BasisStatus.Proposed, 100, "kPa", true, true, true));
        Assert.True(BasisRules.MayConfirm(BasisStatus.Proposed, null, null, true, true, true));
        Assert.False(BasisRules.MayConfirm(BasisStatus.Proposed, 100, "kPa", false, true, true));
        Assert.False(BasisRules.MayConfirm(BasisStatus.Proposed, 100, "kPa", true, true, false));
        Assert.False(BasisRules.MayConfirm(BasisStatus.Confirmed, 100, "kPa", true, true, true));
    }

    [Fact]
    public void Provisional_assumption_needs_live_explicit_disposition()
    {
        var today = new DateOnly(2026, 9, 27);
        Assert.False(BasisRules.AssumptionReady(BasisStatus.Proposed, false, today.AddDays(1), today));
        Assert.False(BasisRules.AssumptionReady(BasisStatus.Proposed, true, today.AddDays(-1), today));
        Assert.True(BasisRules.AssumptionReady(BasisStatus.Proposed, true, today, today));
        Assert.True(BasisRules.AssumptionReady(BasisStatus.Confirmed, false, null, today));
        Assert.False(BasisRules.AssumptionReady(BasisStatus.Withdrawn, true, today.AddDays(1), today));
    }

    [Fact]
    public void Superseded_usage_and_conflicting_confirmed_values_remain_explicit()
    {
        var discipline = Guid.NewGuid();
        Assert.True(BasisRules.NeedsAssessment(BasisStatus.Superseded));
        Assert.True(BasisRules.NeedsAssessment(BasisStatus.Withdrawn));
        Assert.False(BasisRules.NeedsAssessment(BasisStatus.Confirmed));
        Assert.True(BasisRules.ValuesConflict(BasisKind.Criterion, BasisKind.Criterion, BasisStatus.Confirmed, BasisStatus.Confirmed,
            discipline, discipline, "Bearing pressure", "bearing pressure", "Bridge / Pier 1", "bridge / pier 1", "100 kPa", "125 kPa"));
        Assert.False(BasisRules.ValuesConflict(BasisKind.Criterion, BasisKind.Criterion, BasisStatus.Confirmed, BasisStatus.Proposed,
            discipline, discipline, "Bearing pressure", "Bearing pressure", "Bridge / Pier 1", "Bridge / Pier 1", "100 kPa", "125 kPa"));
        Assert.False(BasisRules.ValuesConflict(BasisKind.Criterion, BasisKind.Criterion, BasisStatus.Confirmed, BasisStatus.Confirmed,
            discipline, discipline, "Bearing pressure", "Settlement", "Bridge / Pier 1", "Bridge / Pier 1", "100 kPa", "125 kPa"));
    }
}
