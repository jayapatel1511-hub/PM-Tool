# Feature Specification: Discipline Handoffs and Acceptance

**Packet**: 025 | **Date**: 2026-09-26
**Status**: Approved scope; specified and planned; application implementation not started in this change.
**Input**: Jay: “Lets add all” following the multidisciplinary coordination research and the six previously accepted capabilities.
**Source**: Product specification §8.10, §10.8, §37.1, §37.2, §38.4; FR-HND-01, FR-HND-02, FR-HND-03, FR-HND-04, FR-HND-05, FR-HND-06, FR-HND-07, AC-HND-01, AC-HND-02, AC-HND-03, AC-HND-04, AC-HND-05; FR-MDC-01, FR-MDC-02, FR-MDC-03, FR-MDC-04, FR-MDC-05, FR-MDC-06, FR-MDC-07, FR-MDC-08.

## User outcome

A receiving discipline can identify, accept and incorporate the exact input it needs for a defined purpose.

## Functional requirements

- **FR-HND-01.** A handoff MUST record the sending and receiving disciplines, one sending owner, one receiving owner, source deliverable/revision, at least one receiving task or deliverable, intended use, acceptance criteria, needed-by date and promised date. Drafts may omit a promised date; submission requires it. A sender promising after the needed-by date generates a visible mismatch without silently changing either date.

- **FR-HND-02.** The sender submits a fixed revision. The receiver may accept it for the stated use, request clarification or return it with a reason. Acceptance records the receiver, time, purpose and criteria outcome. Incorporation is a separate receiver action recording the target work and revision actually used; acceptance is not technical approval or a transfer of professional responsibility.

- **FR-HND-03.** Canonical transitions MUST be Draft → Submitted; Submitted → Accepted, Clarification Requested or Returned; Clarification Requested/Returned → Submitted with a response and revision; Accepted → Incorporated. A sender cannot accept their own handoff unless the existing self-review setting explicitly permits that same-person case. PM/lead cancellation requires a reason; prior transitions remain in history.

- **FR-HND-04.** A newer source revision MUST NOT overwrite acceptance of the earlier revision. Register it as a new handoff revision linked to the old one and initiate change assessment under packet 027. The receiver explicitly adopts or retains the prior revision with a documented disposition. Partial input is accepted only for a named limited purpose and scope; another purpose requires another handoff.

- **FR-HND-05.** The sender owns delivery and response; the receiver owns acceptance and incorporation. The PM or relevant Discipline Lead may assign these roles but cannot silently sign on their behalf. Multiple receiving disciplines get separate handoff records or child receipts with separate owners and states.

- **FR-HND-06.** Incoming and outgoing lists MUST distinguish promised, submitted, accepted and incorporated information, with overdue-by-needed-date and awaiting-response reasons. Existing task dependencies remain effective; accepting a handoff alone does not complete a task or unblock a separate unresolved dependency.

- **FR-HND-07.** Handoff creation from a deliverable/task MUST prefill permitted project, discipline, source and target references. The form asks only for missing fields. Repeating a submitted command returns the existing result and does not create duplicate receipts or notifications.

All shared requirements (FR-MDC-01, FR-MDC-02, FR-MDC-03, FR-MDC-04, FR-MDC-05, FR-MDC-06, FR-MDC-07, FR-MDC-08) apply to this packet. They govern permissions, lifecycle, one owner, concurrent writes, notifications, source metadata, accessibility, exports and deterministic state. Canonical statuses are defined in §10.8, not invented by the UI.

## Acceptance scenarios

- **AC-HND-01.** Given a Survey handoff promised after Civil needs it, when it is submitted, then both dates and the mismatch are visible to both owners without changing the task dates.

- **AC-HND-02.** Given revision A is submitted, when the named Civil receiver accepts it, then the handoff is Accepted but not Incorporated and the downstream task is not marked Complete.

- **AC-HND-03.** Given revision A has been incorporated, when revision B is registered, then A remains the recorded input and the receiver sees a pending assessment for B.

- **AC-HND-04.** Given a receiver returns a handoff without a reason or a sender tries to accept it, then the operation is refused unless the same-person setting explicitly allows the latter; authorised acceptance is attributed to its actual actor.

- **AC-HND-05.** Given two disciplines receive one source package, when only one accepts, then the other receipt remains pending; a restricted-project viewer cannot see either receipt in search, aggregate or export.

## Data and relationships

Handoff; HandoffRevision; HandoffReceipt/history; immutable source-revision and intended-use references.

## Edge cases and verification obligations

- Repeat each core command as Read Only, a permitted owner, unrelated member and an unauthorised restricted-project user; repeat on Complete, Archived, Cancelled and On Hold projects.
- Exercise stale versions, concurrent finalisation, retry of the same command, inactive/replaced owners and soft-deleted required references. Assert no partial approval or duplicate notification.
- Assert source lists, derived counts and export use the same permission-filtered records. Test keyboard operation, non-colour status meaning and source-item navigation.

## Boundaries and decisions

Dependencies: 002, 003, 004, 005, 006, 009. Existing stack and single database remain. No new infrastructure or external credentials are needed for metadata-only workflows.

Outside this packet: File transfer, automatic technical acceptance, cross-project dependency creation and authoring engineering inputs.

The product scope is accepted; the detailed defaults here are authored design choices to validate during implementation and the pilot. They are not claims that these behaviours already work. No capability is a substitute for the organisation's technical approval process.
