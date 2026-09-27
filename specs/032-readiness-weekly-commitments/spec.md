# Feature Specification: Ready-to-Start Planning and Weekly Commitments

**Packet**: 032 | **Date**: 2026-09-26
**Status**: Approved scope; specified and planned; application implementation not started in this change.
**Input**: Jay: “Lets add all” following the multidisciplinary coordination research and the six previously accepted capabilities.
**Source**: Product specification §8.10, §10.8, §37.1, §38.2, §38.4; FR-RDY-01, FR-RDY-02, FR-RDY-03, FR-RDY-04, FR-RDY-05, FR-RDY-06, FR-RDY-07, AC-RDY-01, AC-RDY-02, AC-RDY-03, AC-RDY-04, AC-RDY-05; FR-MDC-01, FR-MDC-02, FR-MDC-03, FR-MDC-04, FR-MDC-05, FR-MDC-06, FR-MDC-07, FR-MDC-08.

## User outcome

An owner commits to a defined output after checking inputs, decisions, assumptions and available production/review capacity.

## Functional requirements

- **FR-RDY-01.** For a task or deliverable, readiness MUST separately evaluate required handoffs, predecessor rules, required decisions, basis conflicts/assumptions, assigned production owner and confirmed production/review availability where those resources are required. The PM or responsible Discipline Lead records applicability with reason. Missing or unassessed inputs yield Needs Assessment, not Ready.

- **FR-RDY-02.** Use derived readiness states Needs Assessment, Not Ready, Ready and Proceed under Assumption. Preserve the existing task workflow separately. The owner records Intended Output and completion criteria. A permitted task start that is not Ready requires an explicit warning acknowledgement, reason and PM/lead authorisation; review, access and lifecycle guards cannot be bypassed.

- **FR-RDY-03.** A constraint MUST have a category, one removal owner, removal-needed-by date, affected work and source evidence. States are Open, Resolution Proposed, Verified Removed and Cancelled. The affected work owner verifies removal; the removal owner response alone is not verification. Use links to existing decisions/issues/handoffs rather than duplicate records when they already represent the constraint.

- **FR-RDY-04.** Proceed under Assumption MUST link a specific Proposed assumption version, state the limited work allowed, name the approving PM/lead, record risk and expiry and identify the responsible verifier. Expiry or a changed assumption returns readiness to assessment. Mandatory technical review and unresolved blocking review or submission gates cannot be overridden.

- **FR-RDY-05.** A weekly plan MUST snapshot owner-approved output commitments, target dates, linked work, criteria and readiness at the project coordination-week boundary. The responsible performer explicitly commits; a chair may propose but cannot silently commit another person. States are Proposed, Committed, Met, Not Met and Withdrawn. Later changes retain the original promise and add an attributed reason.

- **FR-RDY-06.** At week close, the owner records Met only with completion evidence matching the original criteria; otherwise record Not Met with a reason such as missing input, decision delay, changed scope or unavailable capacity. Withdrawal after commitment stays in the original snapshot. New scope is a separate commitment, not an edit that erases the earlier outcome.

- **FR-RDY-07.** The screen MUST show the coming three-week window by default (organisation setting), constraints to remove, ready outputs and weekly promises; allow other permitted dates. Display any completion ratio with numerator/denominator and the fixed committed snapshot; record later withdrawals separately and do not use the result as an individual productivity ranking. Link to the existing Weekly Coordination meeting.

All shared requirements (FR-MDC-01, FR-MDC-02, FR-MDC-03, FR-MDC-04, FR-MDC-05, FR-MDC-06, FR-MDC-07, FR-MDC-08) apply to this packet. They govern permissions, lifecycle, one owner, concurrent writes, notifications, source metadata, accessibility, exports and deterministic state. Canonical statuses are defined in §10.8, not invented by the UI.

## Acceptance scenarios

- **AC-RDY-01.** Given a task is due next week but its required handoff is Submitted and not Accepted, then readiness is Not Ready with the handoff as its reason.

- **AC-RDY-02.** Given the removal owner proposes that a constraint is resolved, then readiness remains blocked until the affected work owner verifies the evidence.

- **AC-RDY-03.** Given an authorised assumption expires, then Proceed under Assumption becomes Needs Assessment and the original approval remains in history.

- **AC-RDY-04.** Given five outputs were committed and one is withdrawn after the snapshot, then the original five remain inspectable and the withdrawal cannot erase the original promise or alter the denominator silently.

- **AC-RDY-05.** Given a meeting chair proposes an output for another person, then it remains Proposed until that performer confirms; task completion and technical review guards still apply.

## Data and relationships

ReadinessAssessment; Constraint; ReadinessException; WeeklyPlanSnapshot; OutputCommitment/history.

## Edge cases and verification obligations

- Repeat each core command as Read Only, a permitted owner, unrelated member and an unauthorised restricted-project user; repeat on Complete, Archived, Cancelled and On Hold projects.
- Exercise stale versions, concurrent finalisation, retry of the same command, inactive/replaced owners and soft-deleted required references. Assert no partial approval or duplicate notification.
- Assert source lists, derived counts and export use the same permission-filtered records. Test keyboard operation, non-colour status meaning and source-item navigation.

## Boundaries and decisions

Dependencies: 004, 005, 006, 007, 008, 009, 021, 025, 027, 029, 031. Existing stack and single database remain. No new infrastructure or external credentials are needed for metadata-only workflows.

Outside this packet: Automatic scheduling, productivity scores, inferred commitments and automatic meeting transcription.

The product scope is accepted; the detailed defaults here are authored design choices to validate during implementation and the pilot. They are not claims that these behaviours already work. No capability is a substitute for the organisation's technical approval process.
