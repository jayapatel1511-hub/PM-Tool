using Hub.Domain;

namespace Hub.Tests.Domain;

public sealed class HandoffsTests
{
    static readonly Guid Sender = Guid.NewGuid(), Receiver = Guid.NewGuid(), Manager = Guid.NewGuid(), Civil = Guid.NewGuid(), Electrical = Guid.NewGuid();
    static Actor Person(Guid id, params string[] roles) => new(id, true, roles.ToHashSet());
    static ProjectContext Context(string status = ProjectStatus.Active, bool lead = false, bool member = true) =>
        new(Guid.NewGuid(), status, Visibility.Restricted, Manager, false, member ? new HashSet<string> { ProjectRole.TeamMember } : [], lead ? new HashSet<Guid> { Civil } : [], Civil);
    static HandoffFacts Receipt(string status = HandoffStatus.Submitted) => new(Civil, Electrical, Sender, Receiver, status, Sender);

    [Fact]
    public void AC_HND_02_receipt_requires_submission_then_acceptance_then_incorporation()
    {
        Assert.True(HandoffRules.Step(HandoffStatus.Draft, HandoffStatus.Submitted));
        Assert.False(HandoffRules.Step(HandoffStatus.Draft, HandoffStatus.Accepted));
        Assert.False(HandoffRules.Step(HandoffStatus.Submitted, HandoffStatus.Incorporated));
        Assert.True(HandoffRules.Step(HandoffStatus.Submitted, HandoffStatus.Accepted));
        Assert.True(HandoffRules.Step(HandoffStatus.Accepted, HandoffStatus.Incorporated));
        foreach (var to in HandoffStatus.All) Assert.False(HandoffRules.Step(HandoffStatus.Incorporated, to));
        Assert.False(HandoffRules.Step("unknown", HandoffStatus.Cancelled));
    }

    [Theory]
    [InlineData(HandoffStatus.Returned)] [InlineData(HandoffStatus.ClarificationRequested)]
    public void Returning_and_resubmitting_requires_a_recorded_response(string state)
    {
        Assert.True(HandoffRules.Step(HandoffStatus.Submitted, state));
        Assert.True(HandoffRules.NeedsReason(HandoffStatus.Submitted, state));
        Assert.True(HandoffRules.Step(state, HandoffStatus.Submitted));
        Assert.True(HandoffRules.NeedsReason(state, HandoffStatus.Submitted));
        Assert.True(HandoffRules.Editable(state));
    }

    [Fact]
    public void Only_manager_can_cancel_and_only_named_people_can_sign_even_for_admin()
    {
        var ctx = Context(); var h = Receipt();
        Assert.True(Permissions.HandoffTransition(Person(Sender), ctx, Receipt(HandoffStatus.Draft), HandoffStatus.Submitted, false));
        Assert.False(Permissions.HandoffTransition(Person(Manager), ctx, Receipt(HandoffStatus.Draft), HandoffStatus.Submitted, false));
        foreach (var actor in new[] { Person(Sender), Person(Manager), Person(Manager, SystemRole.Admin) })
            Assert.False(Permissions.HandoffTransition(actor, ctx, h, HandoffStatus.Accepted, false));
        Assert.True(Permissions.HandoffTransition(Person(Receiver), ctx, h, HandoffStatus.Accepted, false));
        Assert.True(Permissions.HandoffTransition(Person(Receiver), ctx, Receipt(HandoffStatus.Accepted), HandoffStatus.Incorporated, false));
        Assert.False(Permissions.HandoffTransition(Person(Receiver), ctx, h, HandoffStatus.Cancelled, false));
        Assert.True(Permissions.HandoffTransition(Person(Manager), ctx, h, HandoffStatus.Cancelled, false));
        Assert.True(HandoffRules.NeedsReason(h.Status, HandoffStatus.Cancelled));
        Assert.False(HandoffRules.NeedsReason(HandoffStatus.Draft, HandoffStatus.Submitted));
        Assert.False(HandoffRules.NeedsReason(h.Status, HandoffStatus.Accepted));
        Assert.False(Permissions.HandoffTransition(Person(Receiver), ctx, h, HandoffStatus.Draft, false));
    }

    [Fact]
    public void AC_HND_04_reassignment_cannot_turn_the_original_submitter_into_an_independent_receiver()
    {
        var same = Receipt() with { ReceivingOwnerId = Sender };
        Assert.False(Permissions.HandoffTransition(Person(Sender), Context(), same, HandoffStatus.Accepted, false));
        Assert.True(Permissions.HandoffTransition(Person(Sender), Context(), same, HandoffStatus.Accepted, true));
        var reassigned = same with { SendingOwnerId = Manager };
        Assert.True(HandoffRules.SelfReceipt(reassigned));
        Assert.False(Permissions.HandoffTransition(Person(Sender), Context(), reassigned, HandoffStatus.Accepted, false));
        Assert.False(HandoffRules.SelfReceipt(Receipt()));
    }

    [Theory]
    [InlineData(ProjectStatus.Archived)] [InlineData(ProjectStatus.Cancelled)] [InlineData(ProjectStatus.Complete)]
    public void Lifecycle_veto_precedes_ownership(string status)
    {
        var ctx = Context(status);
        Assert.False(Permissions.HandoffTransition(Person(Receiver), ctx, Receipt(), HandoffStatus.Accepted, false));
        Assert.False(Permissions.EditHandoff(Person(Sender), ctx, Receipt(HandoffStatus.Draft)));
        Assert.False(Permissions.CreateHandoff(Person(Sender), ctx, Civil, Electrical, Sender));
        Assert.False(Permissions.AssignHandoff(Person(Sender), ctx, Receipt()));
    }

    [Fact]
    public void Read_only_inactive_and_restricted_nonmembers_cannot_mutate()
    {
        foreach (var a in new[] { Person(Receiver, SystemRole.ReadOnly), Person(Receiver) with { IsActive = false } })
        {
            Assert.False(Permissions.HandoffTransition(a, Context(), Receipt(), HandoffStatus.Accepted, false));
            Assert.False(Permissions.CreateHandoff(a, Context(lead: true), Civil, Electrical, Receiver));
            Assert.False(Permissions.AssignHandoff(a, Context(lead: true), Receipt()));
            Assert.False(Permissions.EditHandoff(a, Context(), Receipt(HandoffStatus.Draft)));
        }
        Assert.False(Permissions.HandoffTransition(Person(Receiver), Context(member: false), Receipt(), HandoffStatus.Accepted, false));
        Assert.True(Permissions.HandoffTransition(Person(Receiver), Context(ProjectStatus.OnHold), Receipt(), HandoffStatus.Accepted, false));
        Assert.False(Permissions.CreateHandoff(Person(Manager, SystemRole.ReadOnly), Context(), Civil, Electrical, Sender));
    }

    [Fact]
    public void Managers_relevant_leads_and_in_discipline_source_owners_can_create()
    {
        Assert.True(Permissions.CreateHandoff(Person(Manager), Context(), Civil, Electrical, Sender));
        Assert.True(Permissions.CreateHandoff(Person(Receiver), Context(lead: true), Civil, Electrical, Sender));
        Assert.True(Permissions.CreateHandoff(Person(Receiver), Context(lead: true), Electrical, Civil, Sender));
        Assert.True(Permissions.CreateHandoff(Person(Sender), Context(), Civil, Electrical, Sender));
        Assert.False(Permissions.CreateHandoff(Person(Sender), Context(), Electrical, Civil, Sender));
        Assert.False(Permissions.CreateHandoff(Person(Receiver), Context(), Civil, Electrical, Sender));
        var outside = Context(member: false) with { Visibility = Visibility.Open };
        Assert.False(Permissions.CreateHandoff(Person(Sender), outside, Civil, Electrical, Sender));
        Assert.True(Permissions.AssignHandoff(Person(Receiver), Context(lead: true), Receipt()));
        Assert.True(Permissions.AssignHandoff(Person(Receiver), Context(lead: true), Receipt() with { SendingDisciplineId = Electrical, ReceivingDisciplineId = Civil }));
        Assert.True(Permissions.EditHandoff(Person(Sender), Context(), Receipt(HandoffStatus.Draft)));
        Assert.True(Permissions.EditHandoff(Person(Manager), Context(), Receipt(HandoffStatus.Draft)));
        Assert.False(Permissions.EditHandoff(Person(Receiver), Context(), Receipt(HandoffStatus.Draft)));
        Assert.False(Permissions.EditHandoff(Person(Manager), Context(), Receipt(HandoffStatus.Accepted)));
    }

    [Fact]
    public void AC_HND_01_dates_stay_separate_and_hold_suspends_overdue()
    {
        var needed = new DateOnly(2026, 9, 10); var today = needed.AddDays(2);
        Assert.True(HandoffRules.DateMismatch(needed, needed.AddDays(1)));
        Assert.False(HandoffRules.DateMismatch(needed, needed)); Assert.False(HandoffRules.DateMismatch(needed, null));
        Assert.True(HandoffRules.Overdue(HandoffStatus.Submitted, needed, today, ProjectStatus.Active));
        Assert.False(HandoffRules.Overdue(HandoffStatus.Submitted, needed, today, ProjectStatus.OnHold));
        Assert.False(HandoffRules.Overdue(HandoffStatus.Submitted, today, today, ProjectStatus.Active));
        foreach (var state in new[] { HandoffStatus.Accepted, HandoffStatus.Incorporated, HandoffStatus.Cancelled })
            Assert.False(HandoffRules.Overdue(state, needed, today, ProjectStatus.Active));
    }
}
