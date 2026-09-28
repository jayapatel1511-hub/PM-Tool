# Verification: Ready-to-Start Planning and Weekly Commitments

**Date**: 2026-09-26
**State**: Domain, additive schema and partial API increments. No packet UI, deployed browser or pilot execution performed.

Documentation validation is recorded in `docs/coordination-spec-validation.md`. Product acceptance scenarios AC-RDY-01, AC-RDY-02, AC-RDY-03, AC-RDY-04, AC-RDY-05 are **not run end to end**. Implementation tasks remain unchecked until their full scope is complete.

## Domain rules increment

`src/Hub.Domain/Readiness.cs` now defines canonical readiness checks and derived states, including explicit unknown applicability, a narrowly scoped live assumption permission, and the rule that review/submission gates cannot be overridden by that permission. It also defines performer-only commitment and immutable-snapshot denominator rules. The focused `ReadinessTests` domain suite passed 3/3 locally after recompilation, covering a submitted handoff, unknown applicability, expiry and changed-basis reassessment, nonwaivable review gate, performer signature and a withdrawn promise retained in the original denominator. The rules alone do not prove API, browser or acceptance behavior. Packet 032 remains **UNPROVEN** for product acceptance.

## Additive schema increment

The `ReadinessSchema` migration adds project-scoped assessments/checks, work constraints, assumption exceptions, weekly snapshots, commitments and commitment events. It constrains canonical states and target types, unique assessment checks and one snapshot per project/week. Database triggers reject changes to captured snapshots, edits to a signed promise's output/criteria/date/owner, removal of commitment history, and edits to commitment events. The migration was applied only to ephemeral PostgreSQL by the test factory; the persistent `hub_review_local` database has **not** received it. `dotnet ef migrations has-pending-model-changes` reported no drift after a fresh build. Focused readiness tests passed 5/5, including database uniqueness, immutable promise and snapshot behavior, and a permitted Met transition. This is schema evidence, not an API or product acceptance result.

## Scoped assessment API increment

The project API now lets the named task or deliverable owner create an intended output and completion criteria with an explicit unknown value for each of the nine canonical checks. A PM or responsible discipline lead can record a check's applicability, reason and optional source URL. Both commands use project-scoped permissions, row versions and idempotent request IDs; neither command accepts a caller-supplied Ready or pass/fail result. The focused `ReadinessApiTests` passed 1/1 against ephemeral PostgreSQL after recompilation, covering restricted-project denial, wrong-role denial, stale versions, retry, duplicate creation and the unknown state after one applicability decision. Actual source-derived outcomes, exceptions, constraints, weekly commitments, browser workflow and the acceptance scenarios remain **UNPROVEN**.

## Constraint and weekly promise API increments

The weekly promise API now records a chair or performer proposal for the current task/deliverable owner, requires that performer to sign it, and captures project-wide weekly snapshots under PM authority. An attributed event records Met, Not Met or Withdrawn; the snapshot count remains the denominator after withdrawal. A PM can record Not Met or Withdrawn with a reason when reassignment prevents the performer from closing the promise, but cannot attest Met for someone else. The focused `Chair_proposal_requires_performer_confirmation` PostgreSQL API test passed 1/1, covering those permissions and the retained withdrawal count.

The test sets readiness checks directly in its isolated fixture. A later increment derives several checks from live sources, described below; capacity and review source evaluation remains open. Proceed under Assumption is refused until its exact-version, expiry, verifier and approval provenance is validated. Meeting-week boundary automation, a three-week UI, matching completion evidence to criteria, export and full AC-RDY-01 through 05 remain **UNPROVEN**. This increment is not packet acceptance.

The same API now creates a dated, sourced constraint for affected task/deliverable work, with a distinct removal owner. That owner may propose evidence of resolution, but only the unchanged affected work owner can verify removal; a PM can cancel an open or proposed constraint. Versioned, project-locked and retry-safe commands preserve each transition in audit. A focused PostgreSQL `ReadinessApiTests` run passed 1/1 after recompilation, including proposal refusal for the affected owner, verification refusal for the removal owner, stale-version refusal, and successful affected-owner verification. Mandatory production-owner applicability cannot be marked false. These checks do not establish source-derived readiness, the exception workflow, weekly snapshots, UI or AC-RDY-01 through 05; those remain **UNPROVEN**.

## Source-derived check increment

The readiness detail and weekly signature command now reevaluate linked handoffs, task/deliverable predecessors, blocking decisions, the latest basis use for each entry, unresolved basis conflicts, pending basis impacts, assigned production owner and open or resolution-proposed constraints. A stale stored Ready state cannot by itself authorise a weekly signature. A submitted handoff remains a blocker until acceptance or incorporation, and soft-deleted dependencies or links are excluded. The combined focused PostgreSQL set for design basis, templates, readiness and handoffs passed 36/36 after recompilation. Availability, review and submission checks still rely on recorded applicability rather than full source evaluation; the exception workflow, three-week UI, browser flow and AC-RDY-01 through 05 remain **UNPROVEN**.
