# Feature Specification: Supervisor Capacity Signals

**Packet**: 038 | **Feature directory**: `specs/038-supervisor-capacity-signals` | **Date**: 2026-10-12
**Status**: Implemented and verified locally on 2026-10-12, using defaults D1–D7 under Jay's build request. Jay explicitly confirmed the shared hidden-allocation Partial view rule. Source changes remain uncommitted; hosted and human acceptance are pending. See [verification.md](verification.md).
**Input**: a 2026-10-12 gap review of the October 2026 product research (kept outside the repository) against §39–§41. Two of its recommendations were still open and cheap: show a supervisor their people's capacity where they already look, and stop sending the daily digest to people who are away.
**Source**: product specification §42 (§42.1–§42.5) in `spec-parts/17-supervisor-capacity-signals.md`. Related contracts: §39 (weekly planning, packet 034), §40.6 (capacity in the person picker, packet 036), §12.18 (approved leave and office holidays, packet 035), §17.3–§17.5 (daily digest), §13.19 (My Staff), §41 R7 (one signal per row). The approved hidden-work correction amends §39 and §40 as recorded in §42.

## User outcome

A Supervisor opening My Staff sees, beside each direct report's task counts, how many hours they have free this week and how many they are away, with the same figures the Weekly Planner shows. A person on leave does not get a daily digest each morning they are away, and the first digest after they return summarises what changed while they were out.

## Scope

1. **Capacity on My Staff** (FR-SUP-01 to FR-SUP-05): a "This week" column with "N h free" and "N h away" for the current ISO week, fed by the FR-PICK-01 batch read, one request per 50 rows, following the picker's partial-view rule, with the week's Over-planned and Stale plan chips folded into the row's single-signal rule.
2. **Digest hold during time away** (FR-DIG-01 to FR-DIG-04): the daily digest is not sent on a working day on which the person has 0 h available; nothing is queued or merged; a per-user preference, default on, turns it off; immediate notices are unchanged.

## Non-goals

- A Manager Home or exception dashboard; People and Person Profile; Overlapping Plans; Deliverable Pressure (all still deferred under §39.12).
- Changes to visible planner hours, scope or permissions (PLN-05 to PLN-16), Workload, My Week, readiness, or the Staff Assignments report. The approved hidden-work partial-view correction and Under-planned lookahead suppression are included.
- Holding or redirecting immediate in-app or email notices, the weekly summary or the Following feed.
- An "away" marker beside names elsewhere in the product (§51.10 later adds one on the project Team tab); sorting or filtering My Staff by free hours; a next-week column.

## Decisions (defaults used under the build request)

| # | Decision | Default |
|---|---|---|
| D1 | Which week My Staff shows | The current ISO week (Monday in `org_time_zone`), on every day of the week. |
| D2 | Partial-view rule on My Staff | The person picker's (FR-PICK-02), including any later decision on it. |
| D3 | Admins and Executives on the all-staff scope | Figures for everyone, one request per 50 rows. |
| D4 | What holds the digest | A working day of the person's calendar with 0 h available, from a full-day Unavailable override, approved full-day leave or an office holiday. A Reduced day sends. |
| D5 | What happens to a held digest | Nothing is queued or merged; the next digest's "since the last digest" sections cover the time away under the existing caps. |
| D6 | Where the hold is controlled | A per-user preference, default on. No organisation setting. |
| D7 | Exports and reports | Unchanged. |

## User Scenarios & Testing

### Story 1: See who has room this week (P1)

Taylor opens My Staff on Tuesday. Each direct report's row shows "12 h free · 8 h away" or "Partial view", and a person who is Over-planned this week carries that chip as the row's signal.

- **AC-SUP-01.** The figures equal the Weekly Planner's remaining capacity and time away for that person and the current week.
- **AC-SUP-02.** One request per 50 listed rows, never one per person; people outside the viewer's planner scope show no figure and are absent from the response.
- **AC-SUP-03.** "Partial view" on My Staff exactly when the owner picker would show it, with the PLN-15 label as tooltip and accessible description.
- **AC-SUP-04.** Over-planned and "Stale plan n" for the current week join the row's signal set under §41 R7, naming their week.
- **AC-SUP-05.** Visible planner hours, My Week, Workload and readiness calculations and the Staff Assignments report retain their values. Hidden Confirmed allocations overlapping displayed weeks set Partial view; hidden work only in the extended lookahead suppresses Under-planned without labelling the displayed week partial. Hidden records and hours remain absent. Packet 034 and 036 regressions and the focused correction tests pass.

### Story 2: No digest while away (P1)

Yagmur is on approved leave Monday to Friday. No digest is queued on those mornings. Monday's digest after she returns carries the dated sections as of that day and the project updates since the last digest sent before her leave.

- **AC-DIG-01.** A full-day Unavailable override on a working day holds that day's digest; the job counts it as held; immediate notices still go out.
- **AC-DIG-02.** A Reduced day with hours available sends.
- **AC-DIG-03.** Five away days send nothing; the weekend rule is unchanged; the first digest back covers the gap under the 10-row caps.
- **AC-DIG-04.** Turning the preference off sends on away days; the preference defaults to on for new and existing users.
- **AC-DIG-05.** An office holiday holds; a non-working day of the person's own calendar is governed by the weekend rule alone.
- **AC-DIG-06.** No leave reason, type, balance or HR reference is read, logged or shown; no record is written; the Supervisor receives nothing.

## Requirements

Functional requirements are FR-SUP-01, FR-SUP-02, FR-SUP-03, FR-SUP-04, FR-SUP-05, FR-DIG-01, FR-DIG-02, FR-DIG-03 and FR-DIG-04, as written in §42.2 and §42.3. Acceptance criteria are AC-SUP-01 to AC-SUP-05 and AC-DIG-01 to AC-DIG-06 (§42.4). Decisions and deferred proposals are in §42.5.

## Wording rule

Code, interface strings, specification text, commit messages and this packet's records name no third-party product. The research is "the October 2026 product research".

## Build order

Spec first (this packet), then backend (the digest condition and job count, the batch read already exists), then UI (the My Staff column and the preference control). Both parts are independent of each other and of packet 037's remaining human sessions.
