# Feature Specification: Revision Awareness and Change Impact

**Packet**: 027 | **Date**: 2026-09-26
**Status**: Approved scope; specified and planned; application implementation not started in this change.
**Input**: Jay: “Lets add all” following the multidisciplinary coordination research and the six previously accepted capabilities.
**Source**: Product specification §8.10, §10.8, §37.1, §37.4, §38.4; FR-CHG-01, FR-CHG-02, FR-CHG-03, FR-CHG-04, FR-CHG-05, FR-CHG-06, FR-CHG-07, AC-CHG-01, AC-CHG-02, AC-CHG-03, AC-CHG-04, AC-CHG-05; FR-MDC-01, FR-MDC-02, FR-MDC-03, FR-MDC-04, FR-MDC-05, FR-MDC-06, FR-MDC-07, FR-MDC-08.

## User outcome

People can identify the revision they used and make an explicit disposition when linked information changes.

## Functional requirements

- **FR-CHG-01.** A source revision MUST have a stable internal ID, declared external identifier/revision, title, issuer, registration time, link and scope. Published registrations are immutable snapshots; corrections create a replacement with a reason. Do not lexically sort revision labels to infer recency: an authorised explicit supersedes relationship defines the registered sequence, without cycles.

- **FR-CHG-02.** An InputUse link MUST record a receiving task/deliverable, its owner, source revision, intended use and adoption time. Multiple source documents are allowed. Registering a new revision never overwrites an InputUse link or claims that the source system has no newer information.

- **FR-CHG-03.** Publishing a change notice MUST require one owner, old/new revision or changed design-basis entry, a human-authored description, effective date, affected scope and assessment due date. Identify recipients by explicit InputUse/handoff/requirement relationships; allow an authorised owner to add known affected work. Display the detection boundary: unlinked work is not assessed.

- **FR-CHG-04.** Each affected owner MUST disposition the notice as Pending Assessment, Unaffected, Update Required or Clarification Needed, with rationale and evidence. Update Required creates or links a single-owner follow-up task and estimated effort/date impact; the reviewer verifies completion before Resolved. Retaining an older revision requires a recorded reason and approval from the PM or responsible Discipline Lead.

- **FR-CHG-05.** The notice is closed only after every required assessment is Unaffected with evidence (and approval where retaining an older revision) or Resolved with verified correction. Cancelling a correction task does not resolve an Update Required assessment; it needs a new evidenced, authorised disposition. No response remains pending. Acknowledgement means the notice was seen, not that an engineering impact assessment was completed.

- **FR-CHG-06.** Change propagation MUST be deterministic, bounded to explicit relationships and deduplicated per change/target. Show downstream linked items as potentially affected until assessed; do not automatically revise designs, dates, task completion or technical conclusions. Concurrent publication and adoption must recheck referenced versions.

- **FR-CHG-07.** The interface MUST show Current registered revision, Revision used and Assessment status together, with manual-registration/source-check timestamps. Closed projects retain history; later changes cannot mutate their issued records. Reopening or a permitted new change process is required.

All shared requirements (FR-MDC-01, FR-MDC-02, FR-MDC-03, FR-MDC-04, FR-MDC-05, FR-MDC-06, FR-MDC-07, FR-MDC-08) apply to this packet. They govern permissions, lifecycle, one owner, concurrent writes, notifications, source metadata, accessibility, exports and deterministic state. Canonical statuses are defined in §10.8, not invented by the UI.

## Acceptance scenarios

- **AC-CHG-01.** Given three items record use of revision A, when B explicitly supersedes A, then exactly those three receive pending assessments and unrelated/unlinked work is not claimed as checked.

- **AC-CHG-02.** Given an owner acknowledges a notice but has not assessed it, then Pending Assessment remains and closure is refused.

- **AC-CHG-03.** Given a receiver retains A with authorised rationale, then InputUse remains A, the assessment records that disposition and B remains the current registered source revision.

- **AC-CHG-04.** Given two simultaneous adoption/publication commands based on stale versions, then a conflict is returned and no mixed revision/approval snapshot is committed.

- **AC-CHG-05.** Given an Update Required assessment links a correction task, then the notice stays open until correction and verification are recorded; no due date changes automatically.

## Data and relationships

SourceRevision; InputUse; ChangeNotice; ChangeAssessment; ImpactAction link. Reuse DeliverableIssue history and external document links.

## Edge cases and verification obligations

- Repeat each core command as Read Only, a permitted owner, unrelated member and an unauthorised restricted-project user; repeat on Complete, Archived, Cancelled and On Hold projects.
- Exercise stale versions, concurrent finalisation, retry of the same command, inactive/replaced owners and soft-deleted required references. Assert no partial approval or duplicate notification.
- Assert source lists, derived counts and export use the same permission-filtered records. Test keyboard operation, non-colour status meaning and source-item navigation.

## Boundaries and decisions

Dependencies: 003, 004, 005, 006, 009, 025. Existing stack and single database remain. No new infrastructure or external credentials are needed for metadata-only workflows.

Outside this packet: Content diffing, automatic CAD/BIM parsing, guaranteed detection of unlinked impacts and automatic rescheduling.

The product scope is accepted; the detailed defaults here are authored design choices to validate during implementation and the pilot. They are not claims that these behaviours already work. No capability is a substitute for the organisation's technical approval process.
