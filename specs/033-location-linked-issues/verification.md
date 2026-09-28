# Verification: Location-Linked Coordination Issues

**Date**: 2026-09-26
**State**: Backend slice implemented in the isolated review worktree. UI integration, deployed acceptance, and pilot execution remain unproven.

Documentation validation is recorded in `docs/coordination-spec-validation.md`. AC-LOC-01 and AC-LOC-02 have focused API evidence; AC-LOC-03, AC-LOC-04 and AC-LOC-05 remain unproven end-to-end. UI, export reconciliation, notifications and deployed browser flows remain unproven.

## Executed evidence

- PASS — `dotnet build src/Hub.Api/Hub.Api.csproj --no-restore` on the isolated review worktree.
- PASS — `dotnet ef migrations has-pending-model-changes --project src/Hub.Api/Hub.Api.csproj --startup-project src/Hub.Api/Hub.Api.csproj`: no pending model changes after `20260928010417_IssueMetadataIssueVersion`.
- PASS — `dotnet test tests/Hub.Tests/Hub.Tests.csproj --no-restore --filter FullyQualifiedName~LocationIssue`: 4 tests passed after integration review, including unversioned/stale issue writes, non-self or unappointed verification, a non-owner appointed verifier, latest rejection after verification, stale verification after a new reference, equal-clock ordering, and unavailable evidence.
- PASS — domain station/coordinate validation tests are included in `LocationIssueRulesTests`.
- PASS — additive migration creates `issue_location`, `issue_document_reference` and `issue_verification` with project/issue/user foreign keys, kind/status checks, station-order check and document identity uniqueness.
- PASS — additive issue-version migration persists the parent issue version on each metadata record; review DB migration applied successfully.

## Remaining evidence

- UNPROVEN — frontend forms, keyboard/accessibility and register/export integration.
- UNPROVEN — changed revision impact checks, audit/notification deduplication and concurrent finalisation.
- UNPROVEN — deployed review environment, homedev browser-to-API flow, backup/restore, real tenant sign-in, pilot and production acceptance.
