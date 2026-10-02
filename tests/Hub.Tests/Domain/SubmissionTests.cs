using Hub.Domain;

namespace Hub.Tests.Domain;

public sealed class SubmissionTests
{
    [Fact]
    public void Required_checks_and_blockers_determine_readiness()
    {
        Assert.False(SubmissionRules.Ready([]));
        Assert.False(SubmissionRules.Ready([(true, SubmissionCheckStatus.Pass), (true, SubmissionCheckStatus.Pending)]));
        Assert.False(SubmissionRules.Ready([(true, SubmissionCheckStatus.NotApplicable)]));
        Assert.True(SubmissionRules.Ready([(true, SubmissionCheckStatus.Pass), (false, SubmissionCheckStatus.NotApplicable)]));
        Assert.False(SubmissionRules.CanIssue(SubmissionStatus.Draft, true));
        Assert.False(SubmissionRules.CanIssue(SubmissionStatus.Ready, false));
        Assert.True(SubmissionRules.CanIssue(SubmissionStatus.Checking, true));
    }

    [Fact]
    public void Only_named_coordinator_and_pm_can_advance_or_authorise()
    {
        var pm = Guid.NewGuid(); var coordinator = Guid.NewGuid(); var owner = Guid.NewGuid(); var outsider = Guid.NewGuid();
        var ctx = new ProjectContext(Guid.NewGuid(), ProjectStatus.Active, Visibility.Restricted, pm, false,
            new HashSet<string> { ProjectRole.TeamMember }, new HashSet<Guid>(), null);
        Actor Person(Guid id, params string[] roles) => new(id, true, roles.ToHashSet());
        Assert.True(Permissions.CoordinateSubmission(Person(coordinator), ctx, coordinator));
        Assert.False(Permissions.CoordinateSubmission(Person(pm), ctx, coordinator));
        Assert.True(Permissions.AuthoriseSubmission(Person(pm), ctx));
        Assert.False(Permissions.AuthoriseSubmission(Person(coordinator), ctx));
        Assert.False(Permissions.AuthoriseSubmission(Person(outsider, SystemRole.Admin), ctx));
        Assert.True(Permissions.SignSubmissionCheck(Person(owner), ctx, owner));
        Assert.False(Permissions.SignSubmissionCheck(Person(pm), ctx, owner));
        Assert.False(Permissions.CreateSubmission(Person(outsider), ctx));
        foreach (var state in new[] { ProjectStatus.Archived, ProjectStatus.Cancelled }) {
            var frozen = ctx with { Status = state };
            Assert.False(Permissions.AuthoriseSubmission(Person(pm), frozen));
            Assert.False(Permissions.CoordinateSubmission(Person(coordinator), frozen, coordinator));
        }
        Assert.False(Permissions.AuthoriseSubmission(Person(pm, SystemRole.ReadOnly), ctx));
    }

    [Fact]
    public void Issue_is_terminal_except_superseding_history()
    {
        Assert.True(SubmissionRules.Step(SubmissionStatus.Draft, SubmissionStatus.Checking));
        Assert.True(SubmissionRules.Step(SubmissionStatus.Checking, SubmissionStatus.Issued));
        Assert.True(SubmissionRules.Step(SubmissionStatus.Issued, SubmissionStatus.Superseded));
        Assert.False(SubmissionRules.Step(SubmissionStatus.Draft, SubmissionStatus.Issued));
        Assert.False(SubmissionRules.Step(SubmissionStatus.Issued, SubmissionStatus.Cancelled));
        Assert.False(SubmissionRules.Step(SubmissionStatus.Superseded, SubmissionStatus.Ready));
        Assert.True(SubmissionCheckKind.Waivable(SubmissionCheckKind.Applicability));
        foreach (var kind in SubmissionCheckKind.All.Where(k => k != SubmissionCheckKind.Applicability))
            Assert.False(SubmissionCheckKind.Waivable(kind));
    }
}
