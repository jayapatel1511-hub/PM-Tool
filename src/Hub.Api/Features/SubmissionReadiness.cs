using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Features;

public sealed record SubmissionBlocker(string Kind, Guid? SourceId, Guid? DisciplineId, Guid? OwnerId, string Code, string Message, string? SourcePath);
public sealed record SubmissionReadinessResult(bool Ready, string Fingerprint, IReadOnlyList<SubmissionBlocker> Blockers);

/// <summary>Reads live source state inside the caller's project transaction. A stored Pass never overrides a derived blocker.</summary>
public static class SubmissionReadiness
{
    public static async Task<SubmissionReadinessResult> Evaluate(HubDb db, SubmissionPackage package)
    {
        var manifest = await db.SubmissionManifestItems.Where(m => m.PackageId == package.Id && m.ManifestVersion == package.ManifestVersion)
            .OrderBy(m => m.DeliverableId).ToListAsync();
        var checks = await db.SubmissionChecks.Where(c => c.PackageId == package.Id && c.ManifestVersion == package.ManifestVersion)
            .OrderBy(c => c.Kind).ThenBy(c => c.SourceId).ToListAsync();
        var blockers = new List<SubmissionBlocker>();
        var facts = new List<object>();
        var project = await db.Projects.SingleAsync(p => p.Id == package.ProjectId);
        string WorkPath(Guid id) => $"/projects/{project.ProjectNumber}/deliverables?panel=Deliverable:{id}";
        void Block(string kind, Guid? source, Guid? discipline, Guid? owner, string code, string? path = null) => blockers.Add(new(kind, source, discipline, owner, code, Text.Get(code), path));
        if (manifest.Count == 0) Block(SubmissionCheckKind.Deliverable, null, null, package.CoordinatorId, "submission.empty_manifest");
        if (checks.Count == 0) Block(SubmissionCheckKind.Applicability, null, null, package.CoordinatorId, "submission.empty_checks");
        var eligible = (await Coordination.People(db, project).Select(u => u.Id).ToListAsync()).ToHashSet();
        if (!eligible.Contains(package.CoordinatorId)) Block(SubmissionCheckKind.Access, null, null, package.CoordinatorId, "submission.coordinator_inactive");
        foreach (var check in checks)
        {
            facts.Add(new { check.Id, check.RowVersion, check.Kind, check.SourceId, check.Status, check.Required, check.OwnerId, check.EvidenceRule, check.EvidenceUrl, check.ApprovedBy });
            foreach (var evidence in await db.CheckEvidences.AsNoTracking().Where(e => e.CheckId == check.Id).OrderBy(e => e.Id).ToListAsync())
                facts.Add(new { evidence.Id, evidence.RowVersion, evidence.EvidenceUrl, evidence.Note });
            if (!eligible.Contains(check.OwnerId)) Block(SubmissionCheckKind.Access, check.Id, check.ProjectDisciplineId, check.OwnerId, "submission.check_owner_inactive");
            if (check.Required && check.Status != SubmissionCheckStatus.Pass && check.EvidenceRule is null)
                Block(check.Kind, check.SourceId ?? check.Id, check.ProjectDisciplineId, check.OwnerId, "submission.check_pending");
            if (check.Status == SubmissionCheckStatus.NotApplicable && (!SubmissionCheckKind.Waivable(check.Kind) || check.Required || check.ApprovedBy is null || string.IsNullOrWhiteSpace(check.Reason) || string.IsNullOrWhiteSpace(check.EvidenceUrl)))
                Block(check.Kind, check.Id, check.ProjectDisciplineId, check.OwnerId, "submission.invalid_waiver");
        }
        foreach (var item in manifest)
        {
            var deliverable = await db.Deliverables.IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(d => d.Id == item.DeliverableId && d.ProjectId == package.ProjectId);
            var revision = await db.SourceRevisions.AsNoTracking().SingleOrDefaultAsync(r => r.Id == item.SourceRevisionId && r.ProjectId == package.ProjectId && r.DeliverableId == item.DeliverableId);
            if (deliverable is null || deliverable.DeletedAt is not null || deliverable.Status == DeliverableStatus.Cancelled)
            { Block(SubmissionCheckKind.Deliverable, item.DeliverableId, null, null, "submission.deliverable_missing", WorkPath(item.DeliverableId)); continue; }
            facts.Add(new { item.Id, ManifestRowVersion = item.RowVersion, item.SourceRevisionId, item.ReviewRoundId, DeliverableRowVersion = deliverable.RowVersion, deliverable.Status, deliverable.RequiresReview });
            if (revision is null) { Block(SubmissionCheckKind.CurrentRevision, item.SourceRevisionId, deliverable.ProjectDisciplineId, deliverable.OwnerId, "submission.revision_missing", WorkPath(item.DeliverableId)); continue; }
            var head = await Coordination.Head(db, revision);
            facts.Add(new { revision.Id, revision.RowVersion, revision.Revision, HeadId = head?.CurrentRevisionId, HeadVersion = head?.RowVersion });
            if (head is null || head.CurrentRevisionId != revision.Id)
                Block(SubmissionCheckKind.CurrentRevision, revision.Id, deliverable.ProjectDisciplineId, deliverable.OwnerId, "submission.revision_changed", WorkPath(item.DeliverableId));
            if (!Uri.TryCreate(revision.Url, UriKind.Absolute, out var uri) || uri.Scheme is not ("https" or "http"))
                Block(SubmissionCheckKind.Access, revision.Id, deliverable.ProjectDisciplineId, deliverable.OwnerId, "submission.source_link_invalid", WorkPath(item.DeliverableId));
            var review = deliverable.RequiredReviewPackageId is { } reviewId
                ? await db.ReviewPackages.AsNoTracking().SingleOrDefaultAsync(r => r.Id == reviewId && r.ProjectId == package.ProjectId) : null;
            if (deliverable.RequiresReview && (review is null || review.Status != ReviewStatus.Approved || review.CurrentRoundId != item.ReviewRoundId))
                Block(SubmissionCheckKind.IndependentReview, deliverable.Id, deliverable.ProjectDisciplineId, deliverable.ReviewerId, "submission.review_not_current", review is null ? WorkPath(deliverable.Id) : $"/projects/{project.ProjectNumber}/reviews?panel=ReviewPackage:{review.Id}");
            if (review is not null)
            {
                facts.Add(new { review.Id, review.RowVersion, review.CurrentRoundId, review.Status });
                if (review.CurrentRoundId is { } roundId)
                {
                    var round = await db.ReviewRounds.AsNoTracking().SingleOrDefaultAsync(r => r.Id == roundId);
                    facts.Add(new { RoundId = roundId, RoundVersion = round?.RowVersion, RoundStatus = round?.Status });
                    if (round is null || round.Status != ReviewStatus.Approved)
                        Block(SubmissionCheckKind.IndependentReview, deliverable.Id, deliverable.ProjectDisciplineId, deliverable.ReviewerId, "submission.review_round_not_approved", $"/projects/{project.ProjectNumber}/reviews?panel=ReviewPackage:{review.Id}");
                    foreach (var assignment in await db.DisciplineReviews.AsNoTracking().Where(a => a.RoundId == roundId).OrderBy(a => a.Id).ToListAsync())
                        facts.Add(new { assignment.Id, assignment.RowVersion, assignment.Status, assignment.ReviewerId });
                    if (!await db.ReviewManifestItems.AnyAsync(m => m.RoundId == roundId && m.DeliverableId == item.DeliverableId && m.SourceRevisionId == revision.Id))
                        Block(SubmissionCheckKind.IndependentReview, deliverable.Id, deliverable.ProjectDisciplineId, deliverable.ReviewerId, "submission.review_manifest_mismatch", $"/projects/{project.ProjectNumber}/reviews?panel=ReviewPackage:{review.Id}");
                    foreach (var finding in await db.ReviewFindings.AsNoTracking().Where(f => f.RoundId == roundId).OrderBy(f => f.Id).ToListAsync())
                    {
                        facts.Add(new { finding.Id, finding.RowVersion, finding.Status, finding.WithdrawalAcknowledgedBy });
                        if (ReviewRules.BlockingOpen(finding.Severity, finding.Status, finding.WithdrawalAcknowledgedBy is not null))
                            Block(SubmissionCheckKind.BlockingFindings, finding.Id, finding.ProjectDisciplineId, finding.ResolverId, "submission.blocking_finding", $"/projects/{project.ProjectNumber}/reviews?panel=ReviewPackage:{review.Id}");
                    }
                }
            }
        }
        var ids = manifest.Select(m => m.DeliverableId).ToArray();
        var taskIds = await db.Tasks.AsNoTracking().Where(t => t.ProjectId == package.ProjectId && t.DeliverableId != null && ids.Contains(t.DeliverableId.Value) && t.Status != TaskStatuses.Cancelled).Select(t => t.Id).ToArrayAsync();
        foreach (var handoff in await db.Handoffs.AsNoTracking().Where(h => h.ProjectId == package.ProjectId && h.DeletedAt == null && h.Status != HandoffStatus.Cancelled &&
            (h.TargetDeliverableId != null && ids.Contains(h.TargetDeliverableId.Value) || h.TargetTaskId != null && taskIds.Contains(h.TargetTaskId.Value))).OrderBy(h => h.Id).ToListAsync())
        {
            facts.Add(new { handoff.Id, handoff.RowVersion, handoff.Status, handoff.CurrentRevisionId });
            if (handoff.Status is not (HandoffStatus.Accepted or HandoffStatus.Incorporated))
                Block(SubmissionCheckKind.Handoff, handoff.Id, handoff.ReceivingDisciplineId, handoff.ReceivingOwnerId, "submission.handoff_pending", $"/projects/{project.ProjectNumber}/handoffs?panel=Handoff:{handoff.Id}");
        }
        foreach (var assessment in await db.ChangeAssessments.AsNoTracking().Where(a => a.ProjectId == package.ProjectId &&
            (a.TargetType == "Deliverable" && ids.Contains(a.TargetId) || a.TargetType == "Task" && taskIds.Contains(a.TargetId)) &&
            db.ChangeNotices.Any(n => n.Id == a.ChangeNoticeId && n.Status == ChangeStatus.Open)).OrderBy(a => a.Id).ToListAsync())
        {
            facts.Add(new { assessment.Id, assessment.RowVersion, assessment.Status, assessment.RevisionUsedId, assessment.EvidenceUrl, assessment.RetentionApprovedBy });
            if (!ChangeRules.Complete(assessment.Status, !string.IsNullOrWhiteSpace(assessment.EvidenceUrl), assessment.RetainOldRevision, assessment.RetentionApprovedBy is not null))
                Block(SubmissionCheckKind.ChangeAssessment, assessment.Id, null, assessment.OwnerId, "submission.change_pending", $"/projects/{project.ProjectNumber}/changes?panel=ChangeNotice:{assessment.ChangeNoticeId}");
        }
        foreach (var input in await db.InputUses.AsNoTracking().Where(u => u.ProjectId == package.ProjectId &&
            (u.TargetType == "Deliverable" && ids.Contains(u.TargetId) || u.TargetType == "Task" && taskIds.Contains(u.TargetId))).OrderBy(u => u.Id).ToListAsync())
        {
            var head = await db.SourceHeads.AsNoTracking().SingleOrDefaultAsync(h => h.ProjectId == package.ProjectId && h.Identity == input.SourceIdentity);
            facts.Add(new { input.Id, input.RowVersion, input.SourceRevisionId, HeadRevisionId = head?.CurrentRevisionId });
            if (head is null || head.CurrentRevisionId != input.SourceRevisionId)
                Block(SubmissionCheckKind.CurrentRevision, input.Id, null, input.OwnerId, "submission.input_changed", $"/projects/{project.ProjectNumber}/changes");
        }
        var basisUses = await db.BasisUses.AsNoTracking().Where(u => u.ProjectId == package.ProjectId &&
            (u.TargetType == "Deliverable" && ids.Contains(u.TargetId) || u.TargetType == "Task" && taskIds.Contains(u.TargetId)))
            .Join(db.DesignBasisVersions.AsNoTracking(), u => u.VersionId, v => v.Id,
                (u, v) => new { UseId = u.Id, u.VersionId, u.TargetType, u.TargetId, u.OwnerId, u.CreatedAt, v.EntryId, v.Status })
            .OrderBy(u => u.EntryId).ThenBy(u => u.TargetType).ThenBy(u => u.TargetId).ThenBy(u => u.CreatedAt).ThenBy(u => u.UseId)
            .ToListAsync();
        foreach (var use in basisUses)
            facts.Add(new { use.UseId, use.VersionId, use.TargetType, use.TargetId, use.OwnerId, use.CreatedAt, use.EntryId, use.Status });
        foreach (var currentUses in basisUses.GroupBy(u => new { u.EntryId, u.TargetType, u.TargetId }))
        {
            var current = currentUses.OrderByDescending(u => u.CreatedAt).ThenByDescending(u => u.UseId).First();
            var versionIds = new[] { current.VersionId };
            var conflicts = await db.BasisConflicts.AsNoTracking().Where(c => c.ProjectId == package.ProjectId &&
                (versionIds.Contains(c.LeftVersionId) || versionIds.Contains(c.RightVersionId))).AnyAsync(c => !c.Resolved);
            var impacts = await db.BasisImpactAssessments.AsNoTracking().Where(a => a.ProjectId == package.ProjectId &&
                a.BasisUseId == current.UseId).OrderBy(a => a.Id).ToListAsync();
            foreach (var impact in impacts)
                facts.Add(new { impact.Id, impact.RowVersion, impact.BasisUseId, impact.OldVersionId, impact.NewVersionId, impact.WithdrawalVersionId, impact.Status });
            var pendingImpact = impacts.Any(a => a.Status == AssessmentStatus.Pending);
            var ready = !conflicts && !pendingImpact && current.Status == BasisStatus.Confirmed;
            if (!ready)
                Block(SubmissionCheckKind.ChangeAssessment, impacts.FirstOrDefault(a => a.Status == AssessmentStatus.Pending)?.Id ?? current.VersionId,
                    null, current.OwnerId, "submission.change_pending", $"/projects/{project.ProjectNumber}/design-basis?basis={current.EntryId}");
        }
        return new SubmissionReadinessResult(blockers.Count == 0, Coordination.Hash(new { package.Id, package.RowVersion, package.ManifestVersion, Facts = facts }), blockers);
    }
}
