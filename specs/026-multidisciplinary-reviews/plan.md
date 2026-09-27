# Implementation Plan: Multidisciplinary Reviews and Comment Closure

**Date**: 2026-09-26 | **Status**: Foundation implemented; automated checks passed. Authorised by Jay on 2026-09-27; full acceptance remains pending.

## Approach

Extend the existing ASP.NET Core/EF Core/PostgreSQL modular monolith and React/TypeScript interface. Use the same permission evaluation, row-version conventions, transactions, immutable audit, soft deletion and notification queue as the established features. Add narrowly scoped commands; do not introduce a generic workflow engine.

Dependencies: 003, 004, 006, 009. The shared contract is §37.1. Conceptual entities: ReviewPackage; ReviewRound; ReviewManifestItem; DisciplineReview; ReviewFinding; FindingResponse/verification.

## Planned file boundaries

- `src/Hub.Domain/Reviews.cs`
- `src/Hub.Domain/Permissions.cs`
- `src/Hub.Api/Data/Entities.cs`
- `src/Hub.Api/Data/HubDb.cs`
- `src/Hub.Api/Data/Migrations/`
- `src/Hub.Api/Features/Reviews.cs`
- `src/Hub.Api/Program.cs`
- `web/src/pages/projects/Reviews.tsx`
- `web/src/app/routes.tsx`
- `web/src/i18n/en.ts`
- `src/Hub.Api/Text.cs`
- `tests/Hub.Tests/Domain/ReviewsTests.cs`
- `tests/Hub.Tests/Api/ReviewsTests.cs`
- Existing notification, report and search modules only for this packet's events and permission-filtered metadata.
- An existing file with the named responsibility is extended rather than replaced; exact additions are reviewed in the implementation diff.

## Constitution check (design review, not executed proof)

| Principle | Planned treatment |
|---|---|
| Coordination | Outcome stated in spec.md; reuse existing work records |
| Deterministic | Explicit transitions and source relationships; no AI |
| One owner | One accountable owner per record and per child assignment |
| Source of truth | §10.8 and §37.3 amended together with this packet |
| Traceability | Audited, version-checked commands and retained revision snapshots |
| Access | Server checks on writes, reads, aggregates, exports and queued delivery |
| Small-team architecture | One API/database; no broker, model engine or file service |

## Contracts and data integrity

Provide project-scoped list/detail/create/update/transition operations under the existing `/api/v1` conventions. Mutations carry expected versions; immutable issue/approval records carry the exact referenced versions. Use existing problem responses for validation, permission and conflict. A transaction must cover state, evidence links, activity log and outbox. Do not expose endpoints until their permissions and state guards are tested.

## Validation

Implement AC-MRV-01, AC-MRV-02, AC-MRV-03, AC-MRV-04, AC-MRV-05 as domain/API scenarios where applicable, then exercise the complete UI flow. Include Read Only/ownership collision, restricted project filtering, archived writes, self-review through reassignment, stale bulk versions and simultaneous finalisation. Check keyboard operation and export reconciliation. Record the actual commands/results in verification.md; unchecked tasks and passing documentation checks are not application evidence.

## Complexity tracking

No exception to the existing constitution is proposed. The added entities store coordination metadata only. Deferred capabilities are listed in spec.md.

## Resolved implementation details (2026-09-27)

Use the existing Modules route registration and ProjectLayout navigation. Shared implementation helpers in Infrastructure/Coordination.cs own project-locked command receipts, eligible participants and source references; Data/CoordinationEntities.cs holds the new records. Extend the shared SourceRevision record, never create a second revision store. Supporting changes include existing audit, keys, idempotency, notification/digest, search, saved views, export, deliverable gate, handoff and UI modules, plus CI/browser tests.

Review manifests are immutable per round. Every new round requests fresh reviewer decisions (no silent carry-forward); unresolved findings are copied with provenance and earlier rows remain frozen. Reviewers are checked against captured authors and current work owners at assignment and approval. Blocking withdrawal requires both independent verification authority and coordinator acknowledgement. Required review links are checked on both Ready to Issue and issue commands.

Revision labels are never ordered lexically. Explicit source heads and supersedes relationships define published order. Superseding registrations create a draft notice; publication atomically advances the head and creates deduplicated assessments for explicit input-use and handoff targets. Adoption compares the expected current head and receiving-work version. Retaining an old revision requires a reasoned PM/lead approval; correction verification is a separate named-reviewer action. Existing dates, progress, and issued records are not changed by publication. The design-basis adapter remains the planned packet 031 dependency.

All new public writes carry a request ID and optimistic version. A project lock serialises related publications, adoptions and review finalisation; immutable command receipts cover state, activity and notification in the same transaction. Existing stored handoff snapshots without a source head remain readable and may be explicitly selected as the baseline of a registration.
