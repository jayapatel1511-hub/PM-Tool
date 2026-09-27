# Feature Specification: Location-Linked Coordination Issues

**Packet**: 033 | **Date**: 2026-09-26
**Status**: Approved scope; specified and planned; application implementation not started in this change.
**Input**: Jay: “Lets add all” following the multidisciplinary coordination research and the six previously accepted capabilities.
**Source**: Product specification §8.10, §10.8, §37.1, §38.3, §38.4; FR-LOC-01, FR-LOC-02, FR-LOC-03, FR-LOC-04, FR-LOC-05, FR-LOC-06, FR-LOC-07, AC-LOC-01, AC-LOC-02, AC-LOC-03, AC-LOC-04, AC-LOC-05; FR-MDC-01, FR-MDC-02, FR-MDC-03, FR-MDC-04, FR-MDC-05, FR-MDC-06, FR-MDC-07, FR-MDC-08.

## User outcome

A coordination issue identifies the affected physical area and exact drawing/model reference so disciplines can resolve and verify the same problem.

## Functional requirements

- **FR-LOC-01.** Extend the existing Issue record with optional structured location references: site area, building/level/room, asset/system, or alignment and start/end station with units. Drawing/model references MUST include identifier, declared revision and external source link. Allow multiple references when one issue spans boundaries; require at least one location or drawing/model reference for a Coordination issue.

- **FR-LOC-02.** Location fields MUST retain the project coordinate/station convention; validate end station at or after start within the same alignment and units. A coordinate requires a declared coordinate reference system and units. Do not infer datums, convert coordinates or interpret engineering geometry.

- **FR-LOC-03.** Keep one resolution owner and identify affected disciplines plus one independent verifying owner. Reuse the existing Issue status vocabulary; a new verification record captures Resolution Proposed, Verified or Rejected. Resolving a Coordination issue requires evidence and verification; creator/PM can appoint a replacement verifier with reason, subject to self-review rules.

- **FR-LOC-04.** Store links to external markups, screenshots and model viewpoints. Include source revision in the issue context and retain historical references after resolution. A changed referenced revision creates a pending impact check; the issue owner/verifier decides whether to reopen through the existing workflow.

- **FR-LOC-05.** Filter and group by location, discipline, drawing/model, revision, owner and verification status. The same issue ID appears in the project register, review package and coordination view; linking it to another view must not create a duplicate issue.

- **FR-LOC-06.** When location or document access is restricted, apply existing project and source permissions without fetching external file bytes automatically. Show unavailable evidence explicitly; a broken link cannot satisfy a required verification check. Exports include only permitted metadata.

- **FR-LOC-07.** BCF import/export is a deferred interoperability follow-up, not part of this initial implementation. Preserve optional external topic ID, model element GUID and viewpoint URL metadata now without claiming BCF conformance. A later packet requires validated file/API contracts, permission mapping and round-trip acceptance cases before enabling exchange.

All shared requirements (FR-MDC-01, FR-MDC-02, FR-MDC-03, FR-MDC-04, FR-MDC-05, FR-MDC-06, FR-MDC-07, FR-MDC-08) apply to this packet. They govern permissions, lifecycle, one owner, concurrent writes, notifications, source metadata, accessibility, exports and deterministic state. Canonical statuses are defined in §10.8, not invented by the UI.

## Acceptance scenarios

- **AC-LOC-01.** Given a Civil/Utilities issue concerns a station range and drawing revision A, then both disciplines see the same issue ID, exact range/units and source reference.

- **AC-LOC-02.** Given a resolver supplies a correction, then the Coordination issue cannot become Resolved until the independent verifier records verification evidence.

- **AC-LOC-03.** Given drawing B supersedes A, then the closed issue retains A and receives an impact check; it is not silently reopened or marked unaffected.

- **AC-LOC-04.** Given an issue is linked from a review package and the discipline view, then editing its owner updates the single existing record and creates one audit event.

- **AC-LOC-05.** Given station end precedes start or a coordinate omits its reference system, then validation refuses the location entry; no coordinate conversion is guessed.

## Data and relationships

IssueLocation; IssueDocumentReference; IssueVerification; optional external issue/topic identifiers. Extend Issue, do not fork a new register.

## Edge cases and verification obligations

- Repeat each core command as Read Only, a permitted owner, unrelated member and an unauthorised restricted-project user; repeat on Complete, Archived, Cancelled and On Hold projects.
- Exercise stale versions, concurrent finalisation, retry of the same command, inactive/replaced owners and soft-deleted required references. Assert no partial approval or duplicate notification.
- Assert source lists, derived counts and export use the same permission-filtered records. Test keyboard operation, non-colour status meaning and source-item navigation.

## Boundaries and decisions

Dependencies: 003, 006, 009, 014, 026, 027. Existing stack and single database remain. No new infrastructure or external credentials are needed for metadata-only workflows.

Outside this packet: CAD/BIM authoring, embedded viewers, clash detection, drawing markup editing and BCF exchange in the first increment.

The product scope is accepted; the detailed defaults here are authored design choices to validate during implementation and the pilot. They are not claims that these behaviours already work. No capability is a substitute for the organisation's technical approval process.
