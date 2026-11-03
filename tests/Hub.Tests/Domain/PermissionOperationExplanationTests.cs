using Hub.Domain;

namespace Hub.Tests.Domain;

/// Behavioral examples through the public adapter; no assertion of complete matrix or live-command authority.
public sealed class PermissionOperationExplanationTests
{
    static PermissionOperationExplanation Operation(string key, string role = "baseline-Standard",
        string scenario = "same-discipline", string membership = ProjectRole.TeamMember) =>
        Assert.Single(Assert.Single(PermissionExplanationCatalog.ReadRows(role, scenario, membership, key)).Operations);

    static PermissionRowFactor Factor(PermissionOperationExplanation operation, string action) =>
        Assert.Single(operation.Factors.Where(factor => factor.ActionKey == action));

    [Fact]
    public void A_project_lead_can_record_any_project_meeting_without_owning_its_register_item()
    {
        foreach (var key in new[] { "W31.meeting-create", "W31.meeting-edit", "W31.meeting-current" })
        {
            var lead = Operation(key, scenario: "other-discipline", membership: "DisciplineLead");
            Assert.Equal("Conditional", lead.DecisionKind);
            Assert.True(Factor(lead, nameof(Permissions.RunCoordination)).Outcome.Allowed);
            Assert.Equal("NotEligible", Operation(key, scenario: "other-discipline").DecisionKind);
        }
        var choices = PermissionExplanationCatalog.OperationChoices.Select(choice => choice.Key).ToArray();
        Assert.DoesNotContain("W31.meeting-cancel", choices);
        Assert.DoesNotContain("W31.risk-cancel", choices);
        Assert.Contains("W31.risk-transition", choices);
    }

    [Fact]
    public void Management_alternative_is_retained_for_staffing_completed_projects()
    {
        foreach (var key in new[] { "S15.add-report", "S15.remove-report" })
        {
            var admin = Operation(key, "system-Admin", "complete-project", "none");
            Assert.False(Factor(admin, nameof(Permissions.StaffOnProject)).Outcome.Allowed);
            Assert.True(Factor(admin, nameof(Permissions.ManageTeam)).Outcome.Allowed);
            Assert.Equal("Conditional", admin.DecisionKind);
        }
    }

    [Fact]
    public void Package_coordination_uses_the_named_coordinator_instead_of_the_unrelated_owner()
    {
        foreach (var key in new[] { "C04.new-round", "C04.start", "C04.assign-reviewer", "C06.assign-resolver", "C09.edit" })
        {
            var coordinator = Operation(key, scenario: "package-coordinator");
            Assert.Equal("Conditional", coordinator.DecisionKind);
            Assert.Contains("adminRole.fact.actorIsPackageCoordinator", coordinator.FactKeys);
            Assert.Equal("NotEligible", Operation(key, scenario: "own-item").DecisionKind);
        }
    }

    [Fact]
    public void Copying_a_direct_reports_self_entry_needs_target_creation_not_source_edit_ownership()
    {
        var copy = Operation("P06.copy", "system-Supervisor", "direct-report-self-entry", "none");
        Assert.Equal("Conditional", copy.DecisionKind);
        Assert.True(Factor(copy, nameof(Permissions.CreatePlanningEntry)).Outcome.Allowed);
        Assert.DoesNotContain(copy.Factors, factor => factor.ActionKey == nameof(Permissions.ManagePlanningEntry));
        Assert.DoesNotContain(copy.Requirements, requirement => requirement.Key is "adminRole.requirement.planning-owner" or "adminRole.requirement.version");
        Assert.Equal("NotEligible", Operation("P06.edit", "system-Supervisor", "direct-report-self-entry", "none").DecisionKind);
        Assert.Equal("NotEligible", Operation("P06.copy", scenario: "direct-report-self-entry", membership: "none").DecisionKind);
    }

    [Fact]
    public void Dedicated_issue_command_allows_management_from_review_while_retaining_source_and_review_checks()
    {
        var issue = Operation("W13.issued", scenario: "named-reviewer", membership: ProjectRole.PM);
        Assert.Equal("Conditional", issue.DecisionKind);
        Assert.True(Factor(issue, "Workflow.DeliverableIssueAllowedFrom").Outcome.Allowed);
        Assert.True(Factor(issue, nameof(Permissions.DeliverableTransition)).Outcome.Allowed);
        Assert.Contains(issue.Requirements, requirement => requirement.Key == "adminRole.requirement.review-source" && requirement.State == "RequiresLiveCheck");
        Assert.Equal("NotEligible", Operation("W13.issued", scenario: "named-reviewer").DecisionKind);
        Assert.Equal("NotEligible", Operation("W13.issued", scenario: "on-hold-deliverable", membership: ProjectRole.PM).DecisionKind);
    }

    [Fact]
    public void Distinct_lifecycle_commands_retain_their_actual_actor_and_source_requirements()
    {
        Assert.Equal("NotEligible", Operation("W32.reopen", scenario: "own-item").DecisionKind);
        Assert.Equal("Conditional", Operation("W32.reopen", membership: ProjectRole.PM).DecisionKind);
        Assert.Equal("Conditional", Operation("W31.decision-cancel", scenario: "own-item").DecisionKind);
        Assert.Equal("NotEligible", Operation("W31.decision-cancel").DecisionKind);
        Assert.Equal("Conditional", Operation("C07.close-notice", scenario: "own-item").DecisionKind);
        Assert.Equal("NotEligible", Operation("C07.close-notice").DecisionKind);
        foreach (var (key, requirement) in new[] { ("W08.cancel", "milestone-cancel"), ("W10.reopen", "milestone-reopen"),
            ("C07.assign-notice", "change-notice-manage"), ("C07.cancel-notice", "change-notice-manage"), ("C13.resolve-conflict", "basis-conflict") })
        {
            var command = Operation(key, membership: ProjectRole.PM);
            Assert.Equal("Conditional", command.DecisionKind);
            Assert.Contains(command.Requirements, item => item.Key == "adminRole.requirement." + requirement && item.State == "RequiresLiveCheck");
        }
    }

    [Fact]
    public void Private_entry_refusal_does_not_veto_capacity_or_time_away_source_kinds()
    {
        foreach (var key in new[] { "P02.capacity", "P02.time-away" })
        {
            var read = Operation(key, "system-Admin", "private-draft", "none");
            Assert.Equal("Conditional", read.DecisionKind);
            Assert.DoesNotContain(read.Requirements, requirement => requirement.Key == "adminRole.requirement.private-source"
                && requirement.State == "NotMetByExample");
            Assert.Contains(read.Requirements, requirement => requirement.Key == "adminRole.requirement.people-read"
                && requirement.State == "RequiresLiveCheck");
        }
        var privateEntry = Operation("P03.view-draft", "system-Admin", "private-draft", "none");
        Assert.Equal("NotEligible", privateEntry.DecisionKind);
        Assert.Equal("planning.private", Factor(privateEntry, nameof(Permissions.SeePlanningEntry)).Outcome.ReasonCode);
    }

    [Fact]
    public void Coordination_snapshot_uses_project_management_and_does_not_mask_a_definitive_denial()
    {
        var lead = Operation("C16.snapshot", membership: "DisciplineLead");
        var leadGate = Factor(lead, nameof(Permissions.ManageCoordination));
        Assert.False(leadGate.Outcome.Allowed); // Actual Snapshot passes Guid.Empty, not the lead's own discipline.
        Assert.Equal("perm.pm_or_dl", leadGate.Outcome.ReasonCode);
        Assert.Equal("NotEligible", lead.DecisionKind); // This is the actual sole role gate, not an unknown alternative.

        var admin = Operation("C16.snapshot", "system-Admin", membership: "none");
        Assert.True(Factor(admin, nameof(Permissions.ManageCoordination)).Outcome.Allowed);
        Assert.Equal("Conditional", admin.DecisionKind);
        Assert.Contains(admin.Requirements, requirement => requirement.Key == "adminRole.requirement.weekly-output"
            && requirement.State == "RequiresLiveCheck");
    }

    [Fact]
    public void A_resolver_or_discipline_reviewer_does_not_acquire_the_findings_verifier_signature()
    {
        var response = Operation("C06.respond", scenario: "own-item");
        Assert.True(Factor(response, nameof(Permissions.NamedCoordinationAction)).Outcome.Allowed);
        Assert.Equal("Conditional", response.DecisionKind);

        foreach (var scenario in new[] { "own-item", "named-reviewer", "package-coordinator" })
        {
            var withdrawal = Operation("C06.withdraw", scenario: scenario);
            Assert.False(Factor(withdrawal, nameof(Permissions.NamedCoordinationAction)).Outcome.Allowed);
            Assert.Equal("NotEligible", withdrawal.DecisionKind);
        }
        var verifier = Operation("C06.withdraw", scenario: "designated-verifier");
        Assert.True(Factor(verifier, nameof(Permissions.NamedCoordinationAction)).Outcome.Allowed);
        Assert.Equal("Conditional", verifier.DecisionKind);
        Assert.Contains("adminRole.fact.selectedNamedVerifier", verifier.FactKeys);
        Assert.Contains(verifier.Requirements, requirement => requirement.Key == "adminRole.requirement.independent-source"
            && requirement.State == "RequiresLiveCheck");
    }

    [Fact]
    public void Blocking_withdrawal_acknowledgement_belongs_to_the_package_coordinator_not_the_verifier()
    {
        foreach (var scenario in new[] { "own-item", "designated-verifier" })
        {
            var acknowledgement = Operation("C06.acknowledge-withdrawal", scenario: scenario);
            Assert.False(Factor(acknowledgement, nameof(Permissions.NamedCoordinationAction)).Outcome.Allowed);
            Assert.Equal("NotEligible", acknowledgement.DecisionKind);
        }
        var coordinator = Operation("C06.acknowledge-withdrawal", scenario: "package-coordinator");
        Assert.True(Factor(coordinator, nameof(Permissions.NamedCoordinationAction)).Outcome.Allowed);
        Assert.Equal("Conditional", coordinator.DecisionKind);
        Assert.Contains("adminRole.fact.selectedPackageCoordinator", coordinator.FactKeys);
        Assert.Contains(coordinator.Requirements, requirement => requirement.Key == "adminRole.requirement.withdrawal-acknowledgement"
            && requirement.State == "RequiresLiveCheck");
        Assert.DoesNotContain(coordinator.Requirements, requirement => requirement.Key == "adminRole.requirement.independent-source");
    }

    [Fact]
    public void Deliverable_hold_requires_a_reason_and_resumes_only_the_retained_previous_state()
    {
        var hold = Operation("W13.onhold", scenario: "own-item");
        Assert.Equal("Conditional", hold.DecisionKind);
        Assert.True(Factor(hold, nameof(Permissions.DeliverableTransition)).Outcome.RequiresReason ?? false);
        Assert.Contains(hold.Requirements, requirement => requirement.Key == "adminRole.requirement.reason"
            && requirement.State == "RequiresLiveCheck");
        Assert.Contains("adminRole.fact.onHoldRetainsPreviousStatus", hold.FactKeys);

        var resume = Operation("W13.revisionrequired", scenario: "on-hold-deliverable");
        Assert.True(Factor(resume, "Workflow.DeliverableStep").Outcome.Allowed);
        Assert.Equal("Conditional", resume.DecisionKind);
        Assert.Contains("adminRole.fact.deliverablePrevious.Revision Required", resume.FactKeys);
        Assert.Contains("adminRole.fact.recordedHoldReason", resume.FactKeys);

        var wrongResume = Operation("W13.inprogress", scenario: "on-hold-deliverable");
        Assert.False(Factor(wrongResume, "Workflow.DeliverableStep").Outcome.Allowed);
        Assert.Equal("NotEligible", wrongResume.DecisionKind);
    }

    [Fact]
    public void Default_and_system_project_manager_examples_do_not_imply_team_membership()
    {
        var defaultRows = PermissionExplanationCatalog.ReadRows("baseline-Standard", "same-discipline", operationKey: "W15.operation-1");
        var noMember = Assert.Single(Assert.Single(defaultRows).Operations);
        Assert.Equal("NotEligible", noMember.DecisionKind);
        Assert.Contains("adminRole.fact.membership.None", noMember.FactKeys);
        Assert.DoesNotContain("adminRole.fact.membership.TeamMember", noMember.FactKeys);
        Assert.False(Factor(noMember, nameof(Permissions.CreateTask)).Outcome.Allowed);

        var systemPmOnly = Operation("W15.operation-1", "system-ProjectManager", membership: "none");
        Assert.Equal("NotEligible", systemPmOnly.DecisionKind);
        var member = Operation("W15.operation-1");
        Assert.True(Factor(member, nameof(Permissions.CreateTask)).Outcome.Allowed);
        Assert.Equal("Conditional", member.DecisionKind);
    }

    [Fact]
    public void Stored_readonly_admin_precedence_does_not_erase_inactivity_or_private_entry_boundaries()
    {
        var admin = Operation("W11.operation-1", "system-Admin", "read-only", "none");
        Assert.Equal("Conditional", admin.DecisionKind);
        Assert.True(Factor(admin, nameof(Permissions.CreateDeliverable)).Outcome.Allowed);
        Assert.Contains("adminRole.fact.storedReadOnly", admin.FactKeys);
        Assert.Contains("adminRole.fact.effectiveWritable", admin.FactKeys);
        Assert.Equal("NotEligible", Operation("W11.operation-1", scenario: "read-only").DecisionKind);

        var inactiveAdmin = Operation("W11.operation-1", "system-Admin", "inactive-person", "none");
        Assert.Equal("NotEligible", inactiveAdmin.DecisionKind);
        Assert.Contains(inactiveAdmin.Requirements, requirement => requirement.Key == "adminRole.requirement.identity"
            && requirement.State == "NotMetByExample");
        Assert.Equal("NotEligible", Operation("P03.view-draft", "system-Admin", "private-draft", "none").DecisionKind);
        Assert.Equal("Conditional", Operation("P03.correction-read", "system-Admin", "admin-correction", "none").DecisionKind);
    }

    [Fact]
    public void Pending_report_policy_and_successful_role_factors_never_become_whole_command_authority()
    {
        var report = Operation("S21.export", "system-Admin", membership: "none");
        Assert.Equal("PolicyPending", report.DecisionKind);
        Assert.Contains(report.Requirements, requirement => requirement.Key == "adminRole.requirement.report-policy"
            && requirement.State == "RequiresLiveCheck");
        var incoming = Operation("C06.record-incoming");
        Assert.True(Factor(incoming, nameof(Permissions.RecordExternalReview)).Outcome.Allowed);
        Assert.Equal("Conditional", incoming.DecisionKind);
        Assert.Contains(incoming.Requirements, requirement => requirement.Key == "adminRole.requirement.finding-source"
            && requirement.State == "RequiresLiveCheck");
        Assert.DoesNotContain(new[] { report.DecisionKind, incoming.DecisionKind }, decision => decision == "Allowed");
    }
}
