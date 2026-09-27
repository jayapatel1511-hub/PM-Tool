# Implementation Plan: Discipline Handoffs and Acceptance

**Date**: 2026-09-26 | **Status**: Proposed implementation plan; no application changes in this specification commit.

## Approach

Extend the existing ASP.NET Core/EF Core/PostgreSQL modular monolith and React/TypeScript interface. Use the same permission evaluation, row-version conventions, transactions, immutable audit, soft deletion and notification queue as the established features. Add narrowly scoped commands; do not introduce a generic workflow engine.

Dependencies: 002, 003, 004, 005, 006, 009. The shared contract is §37.1. Conceptual entities: Handoff; HandoffRevision; HandoffReceipt/history; immutable source-revision and intended-use references.

Build immutable revision references as shared foundations for 026/027. AC-HND-03 depends on the later 027 impact commands and remains pending until that integration is verified; the initial handoff increment is not full packet acceptance. See §38.4 for staged delivery.

## Planned file boundaries

- `src/Hub.Domain/Handoffs.cs`
- `src/Hub.Domain/Permissions.cs`
- `src/Hub.Api/Data/Entities.cs`
- `src/Hub.Api/Data/HubDb.cs`
- `src/Hub.Api/Data/Migrations/`
- `src/Hub.Api/Features/Handoffs.cs`
- `src/Hub.Api/Program.cs`
- `web/src/pages/projects/Handoffs.tsx`
- `web/src/app/routes.tsx`
- `web/src/i18n/en.ts`
- `src/Hub.Api/Text.cs`
- `tests/Hub.Tests/Domain/HandoffsTests.cs`
- `tests/Hub.Tests/Api/HandoffsTests.cs`
- Existing notification, report and search modules only for this packet's events and permission-filtered metadata.
- An existing file with the named responsibility is extended rather than replaced; exact additions are reviewed in the implementation diff.

## Constitution check (design review, not executed proof)

| Principle | Planned treatment |
|---|---|
| Coordination | Outcome stated in spec.md; reuse existing work records |
| Deterministic | Explicit transitions and source relationships; no AI |
| One owner | One accountable owner per record and per child assignment |
| Source of truth | §10.8 and §37.2 amended together with this packet |
| Traceability | Audited, version-checked commands and retained revision snapshots |
| Access | Server checks on writes, reads, aggregates, exports and queued delivery |
| Small-team architecture | One API/database; no broker, model engine or file service |

## Contracts and data integrity

Provide project-scoped list/detail/create/update/transition operations under the existing `/api/v1` conventions. Mutations carry expected versions; immutable issue/approval records carry the exact referenced versions. Use existing problem responses for validation, permission and conflict. A transaction must cover state, evidence links, activity log and outbox. Do not expose endpoints until their permissions and state guards are tested.

## Validation

Implement AC-HND-01, AC-HND-02, AC-HND-03, AC-HND-04, AC-HND-05 as domain/API scenarios where applicable, then exercise the complete UI flow. Include Read Only/ownership collision, restricted project filtering, archived writes, self-review through reassignment, stale bulk versions and simultaneous finalisation. Check keyboard operation and export reconciliation. Record the actual commands/results in verification.md; unchecked tasks and passing documentation checks are not application evidence.

## Complexity tracking

No exception to the existing constitution is proposed. The added entities store coordination metadata only. Deferred capabilities are listed in spec.md.
