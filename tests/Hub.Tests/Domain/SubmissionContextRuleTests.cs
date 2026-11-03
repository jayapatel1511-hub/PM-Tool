using Hub.Domain;

namespace Hub.Tests.Domain;

public sealed class SubmissionContextRuleTests
{
    static RecordContextSubmissionProgress PackageProgress(bool assessed = true, bool coordinator = true, bool projectManager = true, bool unmapped = false) => new(assessed, coordinator, projectManager, unmapped);
    static RecordContextSubmissionCheckProgress CheckProgress(bool blocking = true, bool known = true, bool ownerEligible = true, bool sourcesKnown = true, bool terminalPackage = false) => new(blocking, known, ownerEligible, sourcesKnown, terminalPackage);
    static RecordContextFacts Check(string id, string state, string owner, bool blocking = true, bool known = true, bool ownerEligible = true, bool sourcesKnown = true, DateOnly? neededBy = null) =>
        new(RecordContextKind.SubmissionCheck, id, state, owner, neededBy, Actors: [new(owner)]) { SubmissionCheckProgress = CheckProgress(blocking, known, ownerEligible, sourcesKnown) };
    static RecordContextFacts Package(string state = SubmissionStatus.Checking, string coordinator = "coordinator", string projectManager = "pm", IReadOnlyList<RecordContextInput>? inputs = null, RecordContextSubmissionProgress? progress = null) =>
        new(RecordContextKind.SubmissionPackage, "submission", state, coordinator, CoordinatorId: coordinator, ProjectManagerId: projectManager, BlockingInputs: inputs) { SubmissionProgress = progress ?? PackageProgress() };

    [Fact]
    public void Failed_check_is_selected_before_pending_check()
    {
        var failed = new RecordContextInput(Check("failed", SubmissionCheckStatus.Fail, "failed-owner"));
        var pending = new RecordContextInput(Check("pending", SubmissionCheckStatus.Pending, "pending-owner"));

        var result = RecordContextRules.Evaluate(Package(inputs: [pending, failed]));

        Assert.Equal("failed", result.PrimaryInputId);
        Assert.Equal("coordinator", result.OwnerId);
        Assert.Equal("failed-owner", Assert.Single(result.NextActors).Id);
        Assert.Equal(RecordContextAction.ResolveCheck, result.NextAction);
        Assert.Null(result.NeededBy);
    }

    [Fact]
    public void Equal_priority_checks_have_stable_id_tie_break_and_no_invented_date()
    {
        var laterId = new RecordContextInput(Check("z-check", SubmissionCheckStatus.Pending, "z-owner"));
        var earlierId = new RecordContextInput(Check("a-check", SubmissionCheckStatus.Pending, "a-owner"));

        var result = RecordContextRules.Evaluate(Package(inputs: [laterId, earlierId]));

        Assert.Equal("a-check", result.PrimaryInputId);
        Assert.Equal("a-owner", Assert.Single(result.NextActors).Id);
        Assert.Null(result.NeededBy);
    }

    [Fact]
    public void Checking_package_uses_recorded_check_owner_not_source_actor()
    {
        var check = Check("check", SubmissionCheckStatus.Pending, "checking-owner") with
        {
            Actors = [new("source-actor"), new("checking-owner")]
        };

        var result = RecordContextRules.Evaluate(Package(inputs: [new(check)]));

        Assert.Equal("coordinator", result.OwnerId);
        Assert.Equal("checking-owner", Assert.Single(result.NextActors).Id);
        Assert.NotEqual("source-actor", result.OwnerId);
    }

    [Fact]
    public void Ready_package_retains_project_manager_for_issue()
    {
        var result = RecordContextRules.Evaluate(Package(SubmissionStatus.Ready, projectManager: "pm", progress: PackageProgress(projectManager: true)) with
        {
            Actors = [new("pm")]
        });

        Assert.Equal("coordinator", result.OwnerId);
        Assert.Equal("pm", Assert.Single(result.NextActors).Id);
        Assert.Equal(RecordContextAction.Issue, result.NextAction);
        Assert.Null(result.NeededBy);
    }

    [Fact]
    public void Ready_package_does_not_fallback_to_a_failed_optional_check()
    {
        var failed = new RecordContextInput(Check("optional", SubmissionCheckStatus.Fail, "check-owner"));
        var result = RecordContextRules.Evaluate(Package(SubmissionStatus.Ready, projectManager: "pm", inputs: [failed]) with
        {
            Actors = [new("pm"), new("check-owner")]
        });

        Assert.Equal(RecordContextAction.Issue, result.NextAction);
        Assert.Equal("pm", Assert.Single(result.NextActors).Id);
        Assert.True(result.BlockingInputs.Count == 1);
        Assert.Null(result.NeededBy);
    }

    [Fact]
    public void Missing_progress_and_unknown_state_are_unassessed()
    {
        var missing = RecordContextRules.Evaluate(new RecordContextFacts(RecordContextKind.SubmissionPackage, "missing", SubmissionStatus.Checking, "coordinator"));
        var unknown = RecordContextRules.Evaluate(Package("FutureSubmissionState"));
        var unknownCheck = RecordContextRules.Evaluate(Check("unknown", "FutureCheckState", "owner"));

        Assert.Equal(RecordContextAction.NotAssessed, missing.NextAction);
        Assert.Equal(RecordContextAction.NotAssessed, unknown.NextAction);
        Assert.Equal(RecordContextAction.NotAssessed, unknownCheck.NextAction);
        Assert.Empty(missing.NextActors);
        Assert.Empty(unknownCheck.NextActors);
    }

    [Fact]
    public void Terminal_package_with_pending_child_has_no_next_action()
    {
        var pending = new RecordContextInput(Check("pending", SubmissionCheckStatus.Pending, "check-owner"));
        var result = RecordContextRules.Evaluate(Package(SubmissionStatus.Issued, inputs: [pending]) with
        {
            Actors = [new("coordinator"), new("check-owner")]
        });

        Assert.True(result.IsTerminal);
        Assert.Equal(RecordContextAction.None, result.NextAction);
        Assert.Empty(result.NextActors);
        Assert.Null(result.NeededBy);
        var child = RecordContextRules.Evaluate(Check("pending", SubmissionCheckStatus.Pending, "check-owner") with
        { SubmissionCheckProgress = CheckProgress(terminalPackage: true) });
        Assert.True(child.IsTerminal); Assert.Empty(child.NextActors);
        Assert.Equal(RecordContextAction.None, child.NextAction);
    }

    [Fact]
    public void Derived_blocker_state_prioritises_fail_then_pending_and_rejects_unknown()
    {
        Assert.Equal(SubmissionCheckStatus.Fail, RecordContextRules.SubmissionBlockerState("submission.check_failed"));
        Assert.Equal(SubmissionCheckStatus.Pending, RecordContextRules.SubmissionBlockerState("submission.check_pending"));
        Assert.Equal(SubmissionCheckStatus.Fail, RecordContextRules.SubmissionCheckState(SubmissionCheckStatus.Pending, true, ["submission.check_pending", "submission.check_failed"]));
        Assert.Equal(SubmissionCheckStatus.Pending, RecordContextRules.SubmissionCheckState(SubmissionCheckStatus.Pending, true, ["submission.check_pending"]));
        Assert.Null(RecordContextRules.SubmissionBlockerState("submission.unknown"));
        Assert.Null(RecordContextRules.SubmissionCheckState(SubmissionCheckStatus.Pending, true, ["submission.unknown"]));
    }
}
