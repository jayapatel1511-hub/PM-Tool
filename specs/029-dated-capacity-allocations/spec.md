# Feature Specification: Dated Capacity and Project Allocations

**Packet**: 029 | **Date**: 2026-09-26
**Status**: Approved scope; specified and planned; application implementation not started in this change.
**Input**: Jay: “Lets add all” following the multidisciplinary coordination research and the six previously accepted capabilities.
**Source**: Product specification §8.10, §10.8, §37.1, §37.6, §38.4; FR-CAP-01, FR-CAP-02, FR-CAP-03, FR-CAP-04, FR-CAP-05, FR-CAP-06, FR-CAP-07, AC-CAP-01, AC-CAP-02, AC-CAP-03, AC-CAP-04, AC-CAP-05; FR-MDC-01, FR-MDC-02, FR-MDC-03, FR-MDC-04, FR-MDC-05, FR-MDC-06, FR-MDC-07, FR-MDC-08.

## User outcome

Supervisors can confirm production and review commitments for defined dates without double-counting the existing task forecast.

## Functional requirements

- **FR-CAP-01.** A PM or Discipline Lead may propose an allocation with one person, project, production/review purpose, inclusive date range, positive planned hours and linked tasks or review assignments. The person supervisor or Admin confirms it. States are Proposed, Confirmed, Declined, Cancelled and Completed; changing person, dates or hours after confirmation returns it to Proposed.

- **FR-CAP-02.** A person availability override MUST record date, available hours and a non-sensitive category; the supervisor for direct reports or Admin edits it. Store no leave reason, diagnosis or HR document. The override replaces that day capacity, not subtracts twice. Otherwise distribute the existing weekly capacity over that calendar working days; holidays contribute zero unless an explicit override supplies hours.

- **FR-CAP-03.** Spread allocation hours over eligible days within its inclusive range using the person calendar, with explicit per-day overrides available. Store decimal hours; reject negative values, inverted ranges and a positive allocation with no eligible days. Preserve exact totals and apply display rounding only after aggregation.

- **FR-CAP-04.** The grid MUST show available capacity, confirmed reservations, proposed requests and the existing remaining-work forecast separately. To calculate committed load, use max(reservation hours, linked remaining-work hours) for each confirmed allocation plus unlinked remaining-work hours, per person/week. A task/review estimate may belong to at most one allocation for the same person/date slice. Proposed requests are separate scenario demand, never confirmed load.

- **FR-CAP-05.** Allocation changes MUST not modify task estimates, progress, due dates, payroll or actual-hour entries. Use the existing workload thresholds and explain missing estimates. Review effort is explicit review-assignment demand, not duplicated production-task effort. Allocation is a staffing commitment, not evidence of work completion.

- **FR-CAP-06.** Supervisors see direct reports only within existing project permissions; PMs see their permitted project requests. If other assignments are outside the viewer scope, label visible totals as partial and do not claim complete spare capacity. Do not expose restricted project names, hours, counts or existence through aggregate differences.

- **FR-CAP-07.** Over-capacity confirmation MUST warn with the exact dates/hours and require a supervisor reason; it does not silently level or reschedule work. Conflicting simultaneous edits use expected versions, and confirmation records the current capacity/commitment snapshot for audit. A calendar/capacity change recomputes the warning without silently cancelling commitments.

All shared requirements (FR-MDC-01, FR-MDC-02, FR-MDC-03, FR-MDC-04, FR-MDC-05, FR-MDC-06, FR-MDC-07, FR-MDC-08) apply to this packet. They govern permissions, lifecycle, one owner, concurrent writes, notifications, source metadata, accessibility, exports and deterministic state. Canonical statuses are defined in §10.8, not invented by the UI.

## Acceptance scenarios

- **AC-CAP-01.** Given a confirmed 12-hour reservation and 8 linked estimated remaining hours in one week, then committed load is 12 hours, not 20; a separate unlinked 3-hour task makes it 15.

- **AC-CAP-02.** Given a day capacity override of 4 hours, then that day shows 4 hours regardless of the normal daily capacity and no leave reason is visible.

- **AC-CAP-03.** Given a confirmed request changes dates, then confirmation is withdrawn to Proposed and the supervisor must confirm the new range.

- **AC-CAP-04.** Given a PM can see only some of a person work, then the screen labels the visible workload partial and does not infer spare capacity from hidden work.

- **AC-CAP-05.** Given two stale competing confirmations, then the second must refresh the affected person/date version before confirming; a resulting overload requires an explicit reason.

## Data and relationships

PersonAvailabilityOverride; ResourceAllocation; AllocationWorkLink; person/date concurrency record. Extend workload calculations and reuse calendars.

## Edge cases and verification obligations

- Repeat each core command as Read Only, a permitted owner, unrelated member and an unauthorised restricted-project user; repeat on Complete, Archived, Cancelled and On Hold projects.
- Exercise stale versions, concurrent finalisation, retry of the same command, inactive/replaced owners and soft-deleted required references. Assert no partial approval or duplicate notification.
- Assert source lists, derived counts and export use the same permission-filtered records. Test keyboard operation, non-colour status meaning and source-item navigation.

## Boundaries and decisions

Dependencies: 002, 004, 006, 009, 017, 021. Existing stack and single database remain. No new infrastructure or external credentials are needed for metadata-only workflows.

Outside this packet: HR integration, leave administration, automatic resource levelling, billing and individual productivity rankings.

The product scope is accepted; the detailed defaults here are authored design choices to validate during implementation and the pilot. They are not claims that these behaviours already work. No capability is a substitute for the organisation's technical approval process.
