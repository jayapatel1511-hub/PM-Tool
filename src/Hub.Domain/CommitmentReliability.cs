namespace Hub.Domain;

public sealed record CommitmentReliability(int Met, int Committed, int? Percent, string? Band, int Withdrawn, int NotYetRecorded, int Awaiting);
public static class CommitmentReliabilityRules
{
    public static CommitmentReliability Calculate(int committed, int met, int withdrawn, int notYetRecorded, int awaiting, OrgSettings settings)
    {
        var percent = committed == 0 ? (int?)null : (int)Math.Round(100m * met / committed, 0, MidpointRounding.AwayFromZero);
        return new(met, committed, percent, percent is null ? null : percent >= settings.CommitmentReliabilityGoodPct ? "Good" : percent >= settings.CommitmentReliabilityWatchPct ? "Watch" : "Low",
            withdrawn, notYetRecorded, awaiting);
    }
}
