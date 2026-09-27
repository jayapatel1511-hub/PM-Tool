# Feature Specification: Shared Design Basis and Assumptions

**Packet**: 031 | **Date**: 2026-09-26
**Status**: Approved scope; specified and planned; application implementation not started in this change.
**Input**: Jay: “Lets add all” following the multidisciplinary coordination research and the six previously accepted capabilities.
**Source**: Product specification §8.10, §10.8, §37.1, §38.1, §38.4; FR-BAS-01, FR-BAS-02, FR-BAS-03, FR-BAS-04, FR-BAS-05, FR-BAS-06, FR-BAS-07, AC-BAS-01, AC-BAS-02, AC-BAS-03, AC-BAS-04, AC-BAS-05; FR-MDC-01, FR-MDC-02, FR-MDC-03, FR-MDC-04, FR-MDC-05, FR-MDC-06, FR-MDC-07, FR-MDC-08.

## User outcome

Teams can see the current approved criterion or explicit assumption and identify the work that relies on it.

## Functional requirements

- **FR-BAS-01.** An entry MUST identify its kind (Criterion or Assumption), title, one accountable owner, responsible discipline, applicable location/system scope, value or statement, units when numeric, source link/revision, confirmation due date when provisional and linked consuming tasks/deliverables. Distinguish authority/client requirements from project assumptions; free-text content is not an engineering calculation.

- **FR-BAS-02.** Use Proposed, Confirmed, Superseded and Withdrawn. Only the responsible Discipline Lead or an explicitly assigned independent approver may confirm an entry, with source evidence and rationale. The existing self-review setting applies. An unconfirmed assumption can be used only through the explicit Proceed under Assumption readiness disposition with approval, owner and expiry; it must never display as confirmed.

- **FR-BAS-03.** Confirmed entries are immutable versions. Changing value, units, scope or source produces a proposed replacement with an explicit supersedes link. Confirmation of the replacement invokes change assessment for every linked consumer; the former version remains in historical InputUse records.

- **FR-BAS-04.** Each consumer MUST identify the exact basis version it uses. Highlight consumers using a superseded or withdrawn version and require assessment rather than silently updating it. Relationships must be acyclic and limited to the same project. A missing source is an explicit data-quality condition.

- **FR-BAS-05.** A source decision may confirm or change a basis entry, but the records retain distinct purposes and linked histories. Reopening a decision creates an assessment requirement; it does not silently unconfirm every downstream design.

- **FR-BAS-06.** Duplicate entries with the same title, scope and discipline MUST prompt the user to inspect existing entries before creating another. Conflicting confirmed values remain visible with an unresolved conflict; do not choose a value automatically. Conflict resolution creates an authorised replacement and retains the competing histories.

- **FR-BAS-07.** The register MUST filter by discipline, scope, kind, status, overdue confirmation and affected work; show source evidence, current registered version and version used. Export retains units and provenance. Templates may suggest entries as Proposed only; project confirmation never transfers from a template.

All shared requirements (FR-MDC-01, FR-MDC-02, FR-MDC-03, FR-MDC-04, FR-MDC-05, FR-MDC-06, FR-MDC-07, FR-MDC-08) apply to this packet. They govern permissions, lifecycle, one owner, concurrent writes, notifications, source metadata, accessibility, exports and deterministic state. Canonical statuses are defined in §10.8, not invented by the UI.

## Acceptance scenarios

- **AC-BAS-01.** Given Civil and Structural use confirmed basis version A, when B is confirmed, then both consumers require impact assessment and their recorded version remains A until explicitly adopted.

- **AC-BAS-02.** Given a numeric criterion has no units, then confirmation is refused; a narrative criterion does not require invented units.

- **AC-BAS-03.** Given an assumption is still Proposed, then ordinary Ready is unavailable for dependent work; authorised Proceed under Assumption records scope, approver and expiry.

- **AC-BAS-04.** Given two confirmed values conflict in the same scope, then the register displays the conflict and does not silently select the newer value.

- **AC-BAS-05.** Given a template is copied into a new project, then its basis entries are Proposed and no approval, source-use acknowledgement or technical sign-off is inherited.

## Data and relationships

DesignBasisEntry; immutable DesignBasisVersion; BasisUse; BasisConflict; links to Decision and ChangeNotice.

## Edge cases and verification obligations

- Repeat each core command as Read Only, a permitted owner, unrelated member and an unauthorised restricted-project user; repeat on Complete, Archived, Cancelled and On Hold projects.
- Exercise stale versions, concurrent finalisation, retry of the same command, inactive/replaced owners and soft-deleted required references. Assert no partial approval or duplicate notification.
- Assert source lists, derived counts and export use the same permission-filtered records. Test keyboard operation, non-colour status meaning and source-item navigation.

## Boundaries and decisions

Dependencies: 002, 003, 004, 006, 008, 009, 027. Existing stack and single database remain. No new infrastructure or external credentials are needed for metadata-only workflows.

Outside this packet: A standards library, automated engineering advice, inferred design values and technical calculations.

The product scope is accepted; the detailed defaults here are authored design choices to validate during implementation and the pilot. They are not claims that these behaviours already work. No capability is a substitute for the organisation's technical approval process.
