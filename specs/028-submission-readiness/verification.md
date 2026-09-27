# Verification: Submission Readiness and Issue Manifest

**Date**: 2026-09-27
**State**: API and domain implementation in progress. Product acceptance is not complete.

## Evidence run in the isolated Mac worktree

- The additive `SubmissionReadinessSchema` migration applied to the dedicated local `hub_review_local` database. PostgreSQL metadata showed the waiver constraints, source-less check uniqueness index and append-only issue triggers. `dotnet-ef migrations has-pending-model-changes` reported no model drift.
- `dotnet test tests/Hub.Tests/Hub.Tests.csproj --no-restore --filter 'FullyQualifiedName~SubmissionApiTests|FullyQualifiedName~ReviewChangeTests|FullyQualifiedName~SubmissionTests' -v:q -p:WarningLevel=0` passed 17/17 against ephemeral PostgreSQL test databases.
- AC-SUB-01: an Electrical blocking finding appeared as an individual source blocker; Issue returned 422 without an issue row. PM attempt to mark the derived blocking-finding check Not Applicable returned 400.
- AC-SUB-02: publishing revision B moved an unissued Ready package that referenced A to Checking, cleared optional evidence and preserved the previous manifest version. A PM reassigned its optional check owner and coordinator, then the new coordinator created manifest version 2.
- AC-SUB-03: a superseding package retained the first issue's A manifest and original transmittal. A database update to the first issue failed under the immutability trigger.
- AC-SUB-04: changing a checklist check after the PM's read made the old readiness fingerprint return 409. Retrying the same successful Issue command did not create a duplicate issue row.
- AC-SUB-05: the mandatory blocker waiver attempt returned 400. A non-PM optional Not Applicable attempt returned 403; the PM recorded a reason and evidence, after which the named owner could record Pass.

## Remaining acceptance

The current API still needs per-submission export, notifications and the accessible project screen. Metadata editing exists but has not had a dedicated scenario run. Source-change invalidation is covered for revision publication and required review changes; handoff and design-basis changes are re-evaluated at Issue but their stored status and evidence invalidation hooks still need completion. Read Only, restricted-project, inactive-owner, soft-deletion, simultaneous finalisation and browser cases are not yet fully run. No Azure deployment, Entra sign-in, company pilot or production result is implied by these local tests.
