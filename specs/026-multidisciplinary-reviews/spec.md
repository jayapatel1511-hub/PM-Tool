# Feature Specification: Multidisciplinary Reviews and Comment Closure

**Packet**: 026 | **Date**: 2026-09-26
**Status**: Approved scope; specified and planned; application implementation not started in this change.
**Input**: Jay: “Lets add all” following the multidisciplinary coordination research and the six previously accepted capabilities.
**Source**: Product specification §8.10, §10.8, §37.1, §37.3, §38.4; FR-MRV-01, FR-MRV-02, FR-MRV-03, FR-MRV-04, FR-MRV-05, FR-MRV-06, FR-MRV-07, AC-MRV-01, AC-MRV-02, AC-MRV-03, AC-MRV-04, AC-MRV-05; FR-MDC-01, FR-MDC-02, FR-MDC-03, FR-MDC-04, FR-MDC-05, FR-MDC-06, FR-MDC-07, FR-MDC-08.

## User outcome

Each required discipline reviews a fixed package revision and unresolved comments remain visible through correction and verification.

## Functional requirements

- **FR-MRV-01.** A review package MUST identify one coordinator, a fixed manifest of deliverable revisions, the required disciplines, one reviewer per discipline, review due dates and the review purpose. The coordinator starts a round only when required reviewers and referenced revisions exist and are accessible.

- **FR-MRV-02.** A discipline review assignment MUST use Pending, In Review, Changes Required or Approved. The reviewer records their result for that round and revision set. The package uses Draft, In Review, Changes Required, Approved, Superseded or Cancelled; only all required current-round assignments Approved and all blocking comments Verified Closed allow Approved.

- **FR-MRV-03.** Each review comment MUST have an originator, one resolution owner, affected discipline/deliverable/revision, severity (Blocking or Advisory), response and evidence link. Its states are Open, Responded, Verified Closed and Withdrawn. The resolver records Responded; the originator verifies closure or returns it to Open with a reason. PM reassignment of verification requires a reason and an independent permitted reviewer.

- **FR-MRV-04.** Independent-review checks MUST apply to the work author/assignee and assigned reviewers on creation, replacement, bulk reassignment and final approval. The existing allow_self_review setting governs explicit exceptions. Package coordination rights alone do not grant technical approval rights.

- **FR-MRV-05.** Changing a manifest revision, required discipline or blocking criterion after review starts MUST create a new round. Preserve earlier comments and approvals; mark affected assignments Pending and carry unresolved comments forward with provenance. A recorded mapping may carry unaffected approvals forward, with reviewer acknowledgement of the new scope; never silently reuse approval of different content.

- **FR-MRV-06.** The package list MUST show outstanding disciplines, review workload, unresolved blocking comments and elapsed waiting time. Review completion remains separate from the deliverable Issued and Accepted lifecycle. Where a deliverable requires this package, its Ready to Issue command rechecks current review approval.

- **FR-MRV-07.** A required discipline cannot be removed to bypass open findings: removal requires PM reason, impact review and a new round. Withdrawing a comment requires its originator or a PM-designated independent verifier, a reason and retained history; a blocking finding requires the coordinator to acknowledge the withdrawal.

All shared requirements (FR-MDC-01, FR-MDC-02, FR-MDC-03, FR-MDC-04, FR-MDC-05, FR-MDC-06, FR-MDC-07, FR-MDC-08) apply to this packet. They govern permissions, lifecycle, one owner, concurrent writes, notifications, source metadata, accessibility, exports and deterministic state. Canonical statuses are defined in §10.8, not invented by the UI.

## Acceptance scenarios

- **AC-MRV-01.** Given Civil and Structural have approved but Electrical is Pending, then the package cannot be Approved or satisfy a required submission review gate.

- **AC-MRV-02.** Given the resolver responds to a blocking comment, then it remains open for verification until the originator or authorised independent replacement verifies closure.

- **AC-MRV-03.** Given an approved review references revision A, when revision B replaces an affected item, then affected approvals become pending in a new round and A remains in history.

- **AC-MRV-04.** Given a reassignment would make the author their own reviewer while self-review is disabled, then the entire reassignment/approval operation is refused without partial changes.

- **AC-MRV-05.** Given a required discipline is removed, then a reason and new round are required, and its earlier comments/approvals remain inspectable by authorised users.

## Data and relationships

ReviewPackage; ReviewRound; ReviewManifestItem; DisciplineReview; ReviewFinding; FindingResponse/verification.

## Edge cases and verification obligations

- Repeat each core command as Read Only, a permitted owner, unrelated member and an unauthorised restricted-project user; repeat on Complete, Archived, Cancelled and On Hold projects.
- Exercise stale versions, concurrent finalisation, retry of the same command, inactive/replaced owners and soft-deleted required references. Assert no partial approval or duplicate notification.
- Assert source lists, derived counts and export use the same permission-filtered records. Test keyboard operation, non-colour status meaning and source-item navigation.

## Boundaries and decisions

Dependencies: 003, 004, 006, 009. Existing stack and single database remain. No new infrastructure or external credentials are needed for metadata-only workflows.

Outside this packet: Professional seals, digital-signature certification, PDF editing and automatic approval.

The product scope is accepted; the detailed defaults here are authored design choices to validate during implementation and the pilot. They are not claims that these behaviours already work. No capability is a substitute for the organisation's technical approval process.
