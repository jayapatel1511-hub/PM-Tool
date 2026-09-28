# Verification: Location-Linked Coordination Issues

**Date**: 2026-09-26
**State**: Backend and scoped UI slices implemented in the isolated review worktree. Deployed acceptance and pilot execution remain unproven.

Documentation validation is recorded in `docs/coordination-spec-validation.md`. AC-LOC-01 and AC-LOC-02 have focused API evidence; AC-LOC-03, AC-LOC-04 and AC-LOC-05 remain unproven end-to-end. UI, export reconciliation, notifications and deployed browser flows remain unproven.

## Executed evidence

- PASS — `dotnet build src/Hub.Api/Hub.Api.csproj --no-restore` on the isolated review worktree.
- PASS — `dotnet ef migrations has-pending-model-changes --project src/Hub.Api/Hub.Api.csproj --startup-project src/Hub.Api/Hub.Api.csproj`: no pending model changes after `20260928010417_IssueMetadataIssueVersion`.
- PASS — `dotnet test tests/Hub.Tests/Hub.Tests.csproj --no-restore --filter FullyQualifiedName~LocationIssue`: 4 tests passed after integration review, including unversioned/stale issue writes, non-self or unappointed verification, a non-owner appointed verifier, latest rejection after verification, stale verification after a new reference, equal-clock ordering, and unavailable evidence.
- PASS — domain station/coordinate validation tests are included in `LocationIssueRulesTests`.
- PASS — additive migration creates `issue_location`, `issue_document_reference` and `issue_verification` with project/issue/user foreign keys, kind/status checks, station-order check and document identity uniqueness.
- PASS — additive issue-version migration persists the parent issue version on each metadata record; review DB migration applied successfully.

## Remaining evidence

- PASS — scoped issue location, document reference and appointed-verifier forms built with the frontend production build. API guards require a site area or building for a location.
- PASS — local synthetic browser on port 5084: Taylor raised DEMO-101-I01, saved a site area and drawing revision, appointed Yagmur, and all records survived reload. Yagmur signed in separately through Development auth, saw the issue without owner actions, and recorded Verified with a synthetic evidence link. An initial attempt to appoint Marc failed because he was outside DEMO-101; the picker was then changed to use the same eligible project people as the API. Frontend build and lint passed after that correction. This is local synthetic evidence, not the deployed individual-password flow.
- UNPROVEN — deployed browser operation, keyboard/accessibility and register/export integration.
- UNPROVEN — changed revision impact checks, audit/notification deduplication and concurrent finalisation.
- UNPROVEN — deployed review environment, homedev browser-to-API flow, backup/restore, real tenant sign-in, pilot and production acceptance.
