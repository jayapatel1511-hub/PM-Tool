# Feature Specification: Submission Readiness and Issue Manifest

**Packet**: 028 | **Date**: 2026-09-26
**Status**: Approved scope; specified and planned; application implementation not started in this change.
**Input**: Jay: “Lets add all” following the multidisciplinary coordination research and the six previously accepted capabilities.
**Source**: Product specification §8.10, §10.8, §37.1, §37.5, §38.4; FR-SUB-01, FR-SUB-02, FR-SUB-03, FR-SUB-04, FR-SUB-05, FR-SUB-06, FR-SUB-07, AC-SUB-01, AC-SUB-02, AC-SUB-03, AC-SUB-04, AC-SUB-05; FR-MDC-01, FR-MDC-02, FR-MDC-03, FR-MDC-04, FR-MDC-05, FR-MDC-06, FR-MDC-07, FR-MDC-08.

## User outcome

A submission is assembled from explicit revisions and accountable checks, with evidence for every readiness gate.

## Functional requirements

- **FR-SUB-01.** A submission package MUST have one accountable coordinator, one existing milestone, purpose, recipient reference, target date and a manifest of required deliverable revisions. A template may supply the checklist, but each project takes its own versioned snapshot.

- **FR-SUB-02.** Each checklist item MUST identify one checking owner, evidence or a derived source rule, and whether it is required. Required checks include required deliverables present, current required multidisciplinary reviews approved, blocking findings verified closed, required handoffs accepted for the purpose, and outstanding change assessments resolved. Show individual blockers; a percentage must not imply Ready.

- **FR-SUB-03.** States MUST be Draft, Checking, Ready, Issued, Superseded and Cancelled. Ready is derived from all required current checks Pass or an allowed approved Not Applicable. Draft → Checking is coordinator-controlled; Checking/Ready → Issued requires an authorised PM command. Issued is an immutable manifest snapshot linked to the actual external transmittal reference.

- **FR-SUB-04.** Only the PM may approve Not Applicable with reason and evidence on an optional applicability check. Required independent review, current revision identity, access checks and unresolved blocking findings cannot be waived through this checklist. A conditional submission requires a separate purpose and explicit applicable criteria; never relabel failed mandatory criteria as passed.

- **FR-SUB-05.** Changes to the manifest, linked review round, accepted input or applicable design basis MUST invalidate affected readiness evidence and return an unissued package to Checking. Issued manifests remain unchanged; publish a superseding package for corrections and retain both histories.

- **FR-SUB-06.** Issue MUST re-evaluate the manifest and all required checks in one transaction using expected versions. A change during checking returns a conflict or explicit blocker; no issue is recorded against stale sign-offs. Record who authorised issue, when, declared destination and external transmittal link; this does not send files or create a legal signature.

- **FR-SUB-07.** The readiness screen MUST group blockers by discipline and owner and allow opening the source item. A per-submission export includes revision manifest, evidence, unresolved items, exceptions and issue history within the caller access scope.

All shared requirements (FR-MDC-01, FR-MDC-02, FR-MDC-03, FR-MDC-04, FR-MDC-05, FR-MDC-06, FR-MDC-07, FR-MDC-08) apply to this packet. They govern permissions, lifecycle, one owner, concurrent writes, notifications, source metadata, accessibility, exports and deterministic state. Canonical statuses are defined in §10.8, not invented by the UI.

## Acceptance scenarios

- **AC-SUB-01.** Given every checklist row except an Electrical blocking review finding passes, then the package is Checking and Issue is refused with that source finding.

- **AC-SUB-02.** Given a Ready package references revision A, when an affected deliverable changes to B, then the unissued package returns to Checking and affected approvals are invalidated.

- **AC-SUB-03.** Given revision A was issued, when B is issued later, then the first manifest still shows A, its original authorisation and its original transmittal reference.

- **AC-SUB-04.** Given an owner changes a required review while the PM issues from a stale screen, then the issue transaction is refused without recording a partial issued manifest.

- **AC-SUB-05.** Given a PM tries to waive an unresolved blocking finding using Not Applicable, then the command is refused; a genuinely inapplicable optional check requires recorded justification.

## Data and relationships

SubmissionPackage; SubmissionManifestItem; SubmissionCheck; CheckEvidence; immutable SubmissionIssue.

## Edge cases and verification obligations

- Repeat each core command as Read Only, a permitted owner, unrelated member and an unauthorised restricted-project user; repeat on Complete, Archived, Cancelled and On Hold projects.
- Exercise stale versions, concurrent finalisation, retry of the same command, inactive/replaced owners and soft-deleted required references. Assert no partial approval or duplicate notification.
- Assert source lists, derived counts and export use the same permission-filtered records. Test keyboard operation, non-colour status meaning and source-item navigation.

## Boundaries and decisions

Dependencies: 003, 006, 009, 025, 026, 027. Existing stack and single database remain. No new infrastructure or external credentials are needed for metadata-only workflows.

Outside this packet: Document transmission, legal signing/sealing and replacing the external document-management system.

The product scope is accepted; the detailed defaults here are authored design choices to validate during implementation and the pilot. They are not claims that these behaviours already work. No capability is a substitute for the organisation's technical approval process.
