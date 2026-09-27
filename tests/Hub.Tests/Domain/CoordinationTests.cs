using Hub.Domain;
namespace Hub.Tests.Domain;

public sealed class CoordinationTests
{
    [Fact]
    public void Package_requires_every_discipline_and_verified_blocking_findings()
    {
        Assert.Equal(ReviewStatus.InReview, ReviewRules.PackageStatus([], false));
        Assert.Equal(ReviewStatus.InReview, ReviewRules.PackageStatus([DisciplineReviewStatus.Approved, DisciplineReviewStatus.Pending], false));
        Assert.Equal(ReviewStatus.ChangesRequired, ReviewRules.PackageStatus([DisciplineReviewStatus.ChangesRequired], false));
        Assert.Equal(ReviewStatus.ChangesRequired, ReviewRules.PackageStatus([DisciplineReviewStatus.Approved], true));
        Assert.Equal(ReviewStatus.Approved, ReviewRules.PackageStatus([DisciplineReviewStatus.Approved, DisciplineReviewStatus.Approved], false));
        foreach (var severity in new[] { "Blocking", "Advisory" }) foreach (var status in FindingStatus.All) foreach (var ack in new[] { false, true })
            Assert.Equal(severity == "Blocking" && status != FindingStatus.VerifiedClosed && (status != FindingStatus.Withdrawn || !ack), ReviewRules.BlockingOpen(severity, status, ack));
        var legal = new HashSet<(string, string)> { ("Open", "Responded"), ("Open", "Withdrawn"), ("Responded", "Open"), ("Responded", "Verified Closed"), ("Responded", "Withdrawn") };
        foreach (var from in FindingStatus.All.Append("invalid")) foreach (var to in FindingStatus.All.Append("invalid")) Assert.Equal(legal.Contains((from, to)), ReviewRules.FindingStep(from, to));
        var author = Guid.NewGuid(); Assert.False(ReviewRules.Independent(author, [author], false)); Assert.True(ReviewRules.Independent(author, [author], true)); Assert.True(ReviewRules.Independent(Guid.NewGuid(), [author], false));
    }
    [Fact]
    public void Acknowledgement_or_cancelled_correction_never_satisfies_close()
    {
        foreach (var state in AssessmentStatus.All) foreach (var evidence in new[] { false, true }) foreach (var retained in new[] { false, true }) foreach (var approved in new[] { false, true })
            Assert.Equal((state == AssessmentStatus.Unaffected || state == AssessmentStatus.Resolved) && evidence && (!retained || approved), ChangeRules.Complete(state, evidence, retained, approved));
        foreach (var state in TaskStatuses.All.Append(null)) foreach (var independent in new[] { false, true }) foreach (var evidence in new[] { false, true })
            Assert.Equal(state == TaskStatuses.Complete && independent && evidence, ChangeRules.CorrectionReady(state, independent, evidence));
        Assert.True(ChangeRules.CanClose([])); Assert.True(ChangeRules.CanClose([true, true])); Assert.False(ChangeRules.CanClose([true, false]));
        Assert.Equal(6, ReviewStatus.All.Length); Assert.Equal(4, DisciplineReviewStatus.All.Length); Assert.Equal(4, ChangeStatus.All.Length);
    }
    [Fact]
    public void Coordination_management_is_distinct_from_named_technical_signatures()
    {
        var owner = Guid.NewGuid(); var pm = Guid.NewGuid(); var discipline = Guid.NewGuid(); var stranger = Guid.NewGuid();
        Actor Person(Guid id, bool active = true, params string[] roles) => new(id, active, roles.ToHashSet());
        ProjectContext Context(string status = ProjectStatus.Active, bool member = true, bool lead = false, string visibility = Visibility.Restricted) => new(Guid.NewGuid(), status, visibility, pm, false, member ? new HashSet<string> { ProjectRole.TeamMember } : [], lead ? new HashSet<Guid> { discipline } : [], discipline);
        foreach (var method in new Func<Actor, ProjectContext, Allow>[] {
            (a,p) => Permissions.CoordinateReview(a,p,discipline,owner), (a,p) => Permissions.PublishSource(a,p,discipline,owner),
            (a,p) => Permissions.NamedCoordinationAction(a,p,owner), (a,p) => Permissions.ManageCoordination(a,p,discipline) }) {
            Assert.False(method(Person(owner, false), Context())); Assert.False(method(Person(owner, true, SystemRole.ReadOnly), Context()));
            Assert.False(method(Person(owner), Context(member:false))); Assert.False(method(Person(owner), Context(ProjectStatus.Archived)));
            Assert.False(method(Person(owner), Context(ProjectStatus.Cancelled))); Assert.False(method(Person(owner), Context(ProjectStatus.Complete)));
        }
        foreach (var gate in new Func<Actor, ProjectContext, Allow>[] { (a,p) => Permissions.CoordinateReview(a,p,discipline,owner), (a,p) => Permissions.PublishSource(a,p,discipline,owner) }) {
            Assert.True(gate(Person(owner), Context())); Assert.True(gate(Person(pm), Context())); Assert.True(gate(Person(stranger), Context(lead:true)));
            Assert.False(gate(Person(stranger), Context())); Assert.False(gate(Person(owner), Context(member:false, visibility:Visibility.Open)));
        }
        Assert.True(Permissions.NamedCoordinationAction(Person(owner),Context(),owner)); Assert.False(Permissions.NamedCoordinationAction(Person(pm),Context(),owner));
        Assert.False(Permissions.NamedCoordinationAction(Person(pm,true,SystemRole.Admin),Context(),owner)); Assert.False(Permissions.NamedCoordinationAction(Person(owner),Context(member:false,visibility:Visibility.Open),owner));
        Assert.True(Permissions.ManageCoordination(Person(pm), Context(), discipline)); Assert.True(Permissions.ManageCoordination(Person(stranger), Context(lead:true), discipline)); Assert.False(Permissions.ManageCoordination(Person(owner), Context(), discipline));
    }
}
