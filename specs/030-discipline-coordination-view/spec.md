# Feature Specification: Discipline Coordination View

**Packet**: 030 | **Date**: 2026-09-26
**Status**: Approved scope; specified and planned; application implementation not started in this change.
**Input**: Jay: “Lets add all” following the multidisciplinary coordination research and the six previously accepted capabilities.
**Source**: Product specification §8.10, §10.8, §37.1, §37.7, §38.4; FR-DCV-01, FR-DCV-02, FR-DCV-03, FR-DCV-04, FR-DCV-05, FR-DCV-06, FR-DCV-07, AC-DCV-01, AC-DCV-02, AC-DCV-03, AC-DCV-04, AC-DCV-05; FR-MDC-01, FR-MDC-02, FR-MDC-03, FR-MDC-04, FR-MDC-05, FR-MDC-06, FR-MDC-07, FR-MDC-08.

## User outcome

A discipline lead can work through incoming inputs, outgoing promises, revisions in use, changes and ready work from one scoped view.

## Functional requirements

- **FR-DCV-01.** The view MUST answer five questions through fixed sections: What do we owe? What are we waiting for? Which revision are we using? What changed? What can we start? Include pending reviews, upcoming submissions and staffing conflicts as contextual filters and source links.

- **FR-DCV-02.** The default is the user discipline and permitted workspace projects; allow explicit project, discipline, owner and date filters. Every row identifies its project and underlying stable item. Scope persists across drill-down and return, and a saved view stores filters/columns rather than a copy of data.

- **FR-DCV-03.** Incoming handoffs distinguish submitted, accepted and incorporated; outgoing handoffs show promised versus needed dates. Change rows show Pending Assessment separately from acknowledgement. Readiness rows display remaining constraints, and submission rows show the exact failing checks.

- **FR-DCV-04.** Every count and export MUST reconcile with the same filtered source records at the same evaluation timestamp. Show evaluation freshness. Cross-project totals omit restricted work and never describe a partial view as an organisation-wide total.

- **FR-DCV-05.** Inline actions MUST invoke the same domain commands as the owning screen, with permission/refusal reasons, version checks and required evidence. Viewing, discussing or marking a meeting reviewed cannot close a review finding, accept a handoff or approve technical work.

- **FR-DCV-06.** Meeting mode MUST let the chair group related rows by blocker or source change, assign an action and set its date without duplicating existing tasks. Reuse meeting actions and existing notifications. Preserve prior weekly commitment snapshots while discussing a later week.

- **FR-DCV-07.** Place this capability under existing Coordination/Weekly Coordination and My Work. Render only implemented, authorised sections; show explicit unavailable capability messaging during staged rollout. Status text, keyboard navigation, a print view and direct source-item links are required.

All shared requirements (FR-MDC-01, FR-MDC-02, FR-MDC-03, FR-MDC-04, FR-MDC-05, FR-MDC-06, FR-MDC-07, FR-MDC-08) apply to this packet. They govern permissions, lifecycle, one owner, concurrent writes, notifications, source metadata, accessibility, exports and deterministic state. Canonical statuses are defined in §10.8, not invented by the UI.

## Acceptance scenarios

- **AC-DCV-01.** Given three tasks wait on one source handoff, then the view shows one blocker group with three linked tasks and clicking the count opens those same tasks.

- **AC-DCV-02.** Given only one project in a workspace is permitted, then rows, counts, search, print and export contain only that project.

- **AC-DCV-03.** Given an owner opens an item from a saved discipline view and returns, then project/discipline/date scope is preserved with refreshed current data.

- **AC-DCV-04.** Given the chair marks the coordination meeting reviewed, then no handoff, technical review, change assessment or commitment is automatically approved or closed.

- **AC-DCV-05.** Given one source change has several linked actions, then capture action reuses an existing linked action when selected and never silently creates duplicate work.

## Data and relationships

Read-only coordination projection over existing records; reuse SavedView, Workspace and MeetingAction.

## Edge cases and verification obligations

- Repeat each core command as Read Only, a permitted owner, unrelated member and an unauthorised restricted-project user; repeat on Complete, Archived, Cancelled and On Hold projects.
- Exercise stale versions, concurrent finalisation, retry of the same command, inactive/replaced owners and soft-deleted required references. Assert no partial approval or duplicate notification.
- Assert source lists, derived counts and export use the same permission-filtered records. Test keyboard operation, non-colour status meaning and source-item navigation.

## Boundaries and decisions

Dependencies: 007, 009, 019, 022, 025, 026, 027, 028, 029, 031, 032, 033. Existing stack and single database remain. No new infrastructure or external credentials are needed for metadata-only workflows.

Outside this packet: A new independent task database, custom dashboards, duplicate statuses and a chat system.

The product scope is accepted; the detailed defaults here are authored design choices to validate during implementation and the pilot. They are not claims that these behaviours already work. No capability is a substitute for the organisation's technical approval process.
