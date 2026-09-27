using Hub.Domain;

namespace Hub.Tests.Domain;

/// §12.10 worked examples: the 3 × 3 score and its bands, RSK-02 review overdue, ISS-03 issue overdue (packet 014).
public sealed class RegistersTests
{
    static readonly DateOnly Today = new(2026, 9, 14);

    [Theory]
    [InlineData(1, 1, 1, "Low")]
    [InlineData(1, 2, 2, "Low")]
    [InlineData(3, 1, 3, "Medium")]
    [InlineData(2, 2, 4, "Medium")]
    [InlineData(3, 2, 6, "High")] // US1-1
    [InlineData(3, 3, 9, "High")]
    public void Severity_is_probability_times_impact_in_three_bands(int p, int i, int score, string band)
    {
        Assert.Equal(score, Registers.Score(p, i));
        Assert.Equal(band, Registers.Band(score));
        var (min, max) = Registers.Range(band);
        Assert.InRange(score, min, max);
    }

    [Fact]
    public void Every_score_on_the_grid_falls_in_exactly_one_band()
    {
        foreach (var p in Registers.Levels)
            foreach (var i in Registers.Levels)
            {
                var s = Registers.Score(p, i);
                Assert.Single(new[] { Impact.Low, Impact.Medium, Impact.High }, b => Registers.Range(b) is var r && s >= r.Min && s <= r.Max);
            }
    }

    [Fact]
    public void An_open_risk_past_its_review_date_is_review_overdue() // US1-2, RSK-02
    {
        Assert.Equal(4, Registers.ReviewOverdueDays(RiskStatus.Open, new(2026, 9, 10), Today));
        Assert.Equal(1, Registers.ReviewOverdueDays(RiskStatus.Monitoring, new(2026, 9, 13), Today));
        Assert.Equal(0, Registers.ReviewOverdueDays(RiskStatus.Open, Today, Today)); // due today is not overdue
        Assert.Equal(0, Registers.ReviewOverdueDays(RiskStatus.Open, null, Today));
        Assert.Equal(0, Registers.ReviewOverdueDays(RiskStatus.Closed, new(2026, 9, 1), Today));
        Assert.Equal(0, Registers.ReviewOverdueDays(RiskStatus.Realised, new(2026, 9, 1), Today));
    }

    [Fact]
    public void An_open_issue_past_its_target_date_is_overdue() // US2-3, ISS-03
    {
        Assert.Equal(2, Registers.IssueOverdueDays(IssueStatus.Open, new(2026, 9, 12), Today));
        Assert.Equal(2, Registers.IssueOverdueDays(IssueStatus.InProgress, new(2026, 9, 12), Today));
        Assert.Equal(0, Registers.IssueOverdueDays(IssueStatus.Open, new(2026, 9, 15), Today));
        Assert.Equal(0, Registers.IssueOverdueDays(IssueStatus.Open, null, Today));
        Assert.Equal(0, Registers.IssueOverdueDays(IssueStatus.Resolved, new(2026, 9, 1), Today));
        Assert.Equal(0, Registers.IssueOverdueDays(IssueStatus.Cancelled, new(2026, 9, 1), Today));
    }

    [Fact]
    public void An_open_action_past_its_due_date_is_overdue() // packet 015
    {
        Assert.Equal(3, Registers.ActionOverdueDays(ActionStatus.Open, new(2026, 9, 11), Today));
        Assert.Equal(3, Registers.ActionOverdueDays(ActionStatus.InProgress, new(2026, 9, 11), Today));
        Assert.Equal(0, Registers.ActionOverdueDays(ActionStatus.Open, Today, Today));
        Assert.Equal(0, Registers.ActionOverdueDays(ActionStatus.Open, null, Today));
        Assert.Equal(0, Registers.ActionOverdueDays(ActionStatus.Complete, new(2026, 9, 1), Today));
    }

    [Theory]
    [InlineData("Complete", "Complete")]
    [InlineData("Cancelled", "Cancelled")]
    [InlineData("In Progress", "In Progress")] // reopened
    [InlineData("Not Started", "In Progress")]
    [InlineData("In Review", "In Progress")]
    [InlineData("On Hold", "In Progress")]
    public void A_converted_action_follows_its_task(string task, string action) => Assert.Equal(action, Workflow.ActionFollowing(task)); // MTG-03

    [Fact]
    public void High_sorts_before_Medium_before_Low()
    {
        Assert.Equal(new[] { Impact.High, Impact.Medium, Impact.Low }, new[] { Impact.Low, Impact.High, Impact.Medium }.OrderBy(Registers.Weight));
    }
}
