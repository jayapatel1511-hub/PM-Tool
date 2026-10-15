# Feature Specification: Planning, Reporting and Onboarding

**Packet**: 047 | **Feature directory**: `specs/047-planning-reporting-and-onboarding` | **Date**: 2026-10-14
**Status**: Specified, not started.
**Input**: Jay's 2026-10-14 instruction to build everything in the build-approach report, additive only, with no deadlines. This packet holds roadmap items 21, 23, 24, 25, 27, 28, 29, 34 and 35.
**Source**: product specification §51 (§51.1 to §51.13) in `spec-parts/26-planning-reporting-and-onboarding.md`. Related contracts: §12.14 (templates), §12.15.2 and §37.6 FR-CAP-02 (daily capacity), §13.10 (My Work), §13.16 (Team), §19 (reports), §20.1 and §23.8 (logging), §39 (weekly planning, PLN-05, FR-PLN-14, FR-PLN-15), §40.2 (open roles, FR-ROLE-01 to FR-ROLE-05), §41 (calm layout, AC-CALM-01), §42 (FR-SUP-03), §43 (flags, packet 039), §45 (honest free hours, packet 041), §49 (template working-day offsets, packet 045).

## User outcome

A first-time pilot user sees a short getting-started list for their role that ticks itself as they work. Supervisors get the two capacity reports §19 always promised, see unfilled roles and unassigned work next to free capacity, and see per-project estimates beside planned hours. Project Managers start projects with open roles already in place, write an optional status note, and see what they are watching and what they asked others for. Jay can import the project list and learn, in aggregate only, which screens the pilot uses.

## Scope

1. **Getting-started card** (§51.2; FR-PRO-01, FR-PRO-02, FR-PRO-03): a collapsed one-line card on My Work's Today view with three or four self-ticking steps per held role, hideable per user. Today stays the opening view.
2. **Team Capacity and Allocations by Source reports** (§51.3; FR-PRO-04, FR-PRO-05, FR-PRO-06, FR-PRO-07): two catalogue reports computed by the planner's own read, under the planner's visibility rules. Resolves §19 versus §39.12 by building the reports; the §19 rows stay.
3. **Aggregate route counter** (§51.4; FR-PRO-08, FR-PRO-09, FR-PRO-10): per-day, per-route open counts and load-time bands with no user, item or text, and a Pilot route use report. Amends §20.1 and §23.8. The organisation setting `route_counter_enabled` is the lasting control, on for the pilot only.
4. **Template roles become open roles** (§51.5; FR-PRO-11, FR-PRO-12, FR-PRO-13): template roles anchored to template milestones, shown in the wizard preview and created as Open open roles. A template without roles instantiates exactly as today.
5. **Unfilled roles and unassigned work beside free capacity** (§51.6; FR-PRO-14, FR-PRO-15): an Unfilled roles group with Fill links in the Planner's Projects view, and an Unassigned work section with "Assign to" under Workload. No placeholders in capacity, no drag. Amends the FR-ROLE-04 wording.
6. **Optional project status note** (§51.7; FR-PRO-16, FR-PRO-17): one 500-character note with a computed header on the Project Dashboard and Portfolio. No seen-by, no stale reminder; it never changes health.
7. **My Work Watching and Requested by me** (§51.8; FR-PRO-18, FR-PRO-19): two read-only views in the My Work view switch over existing watch rows and created records.
8. **Project-list import** (§51.9; FR-PRO-20, FR-PRO-21): an Admin CSV import of number, name, client, office and Project Manager, on the leave-import pattern of preview, commit and log. Create only.
9. **Capacity polish** (§51.10; FR-PRO-22, FR-PRO-23, FR-PRO-24): estimate versus planned per project in the planner; optional per-weekday hours on a person's schedule, with today's even split the default; an "N h away" chip on the project Team tab.
10. **Shipping dark** (§51.11; FR-PRO-25): each area merges behind its own flag; flag off is today's behaviour.

## Non-goals

- Tours, badges, points, streaks, progress bars or other gamification (§30).
- Any route data tied to a user, session or item; a per-user opt-out (deferred).
- A custom report builder; changes to the Workload by Person report, the planner exports or Pilot measures.
- Open roles in a person's capacity, cells, totals or indicators; placeholders; dragging work onto people (FR-ROLE-04, FR-PLN-28).
- Seen-by, read receipts or stale reminders on the status note; an intake form or form builder.
- Synchronising, scheduling, updating or deleting projects by import (§29 item 3, §30 ERP).
- Per-person dependency lag; readiness capacity checks reading schedules (§38.2 unchanged).
- Parked items that need a §30 or constitution change: hours budgets and actual-hours imports, custom fields, a public API, client links. Anything AI (constitution II).
- Open-role export, which §47 FR-REC-12 builds behind its flag and keeps on a KILL verdict. If the 043 spike never runs because Jay declines 043-D1, it returns to the deferred list (§51.13, 043-D4).

## Decisions with defaults

Full table in §51.13. Summary of defaults:

- D1: card on Today, collapsed.
- D2: step catalogue as §51.2.
- D3: one group per held role.
- D4: Team Capacity measures as rows, weeks as columns.
- D5: caller's own drafts count by default.
- D6: counted hours after coverage; approved allocations as their own row.
- D7: a separate Pilot route use report.
- D8: no per-user opt-out; the `route_counter_enabled` setting is the control.
- D9: no roles from discipline packs.
- D10: an undatable role is skipped with its reason.
- D11: at most 50 unassigned rows plus Show all.
- D12: everyone who can view the project reads the note.
- D13: closed requests from the last 14 days.
- D14: manual and mention watches.
- D15: Tuesday's five import headers until Jay supplies an export sample.
- D16: existing project numbers skipped.
- D17: 0.5 h weekday steps.
- D18: away chip shows with a partial view.
- D19: one flag per area.

## Acceptance

AC-PRO-01, AC-PRO-02, AC-PRO-03 (card); AC-PRO-04, AC-PRO-05, AC-PRO-06, AC-PRO-07 (reports); AC-PRO-08, AC-PRO-09 (route counter); AC-PRO-10, AC-PRO-11 (template roles); AC-PRO-12, AC-PRO-13 (unfilled and unassigned); AC-PRO-14, AC-PRO-15 (status note); AC-PRO-16, AC-PRO-17 (My Work views); AC-PRO-18, AC-PRO-19 (import); AC-PRO-20, AC-PRO-21, AC-PRO-22 (capacity polish); AC-PRO-23 (flags). Each is cited by a test named in [tasks.md](tasks.md). Existing criteria that must keep passing: AC-PLN-11, AC-PLN-12, AC-CALM-01, AC-CALM-03, AC-ROLE-05.

## Depends on

- Packet 039 (§43) flags slice: `Flags.cs`, `flag.*` settings on `/me`, the retirement test and the flag pair test template. Every slice here needs it.
- Packet 041 (§45) honest-free-hours slice, before the two reports (S03, S04), so their partial-view figures match the corrected planner rule.
- Packet 045 slice S07 (§49, the `offset_day_kind` schema and API), before template roles (S07), so role offsets share one offset rule.
- Packet 043: if it converts the project Team tab, the Allocations list or Portfolio after this packet's additions there (the away chip, the `fill` parameter, the status note), the conversion carries them as slots and runs its parity run with each flag off and on (§47.6).
- Packet 040 (§44): if its entity registry has landed, audited fields are added there instead of `AuditRules.Fields`; if its Allocations tab parameter rename has landed, the Fill link uses the new name (old `view=roles` links keep working either way).
- Packet 038 (§42), already merged: the FR-PICK-01 batch read used by the away chip.
- No dependency between this packet's areas except where tasks.md says so.

## Flags

`flag.onboarding_card`, `flag.capacity_reports`, `flag.route_counter` (release only; the lasting control is the `route_counter_enabled` setting), `flag.template_roles`, `flag.unfilled_beside_capacity`, `flag.project_status_note`, `flag.my_work_follow_up`, `flag.project_import`, `flag.capacity_polish`. Slice S18 removes each flag once Jay has turned it on in review and pilot; the `route_counter_enabled` setting stays.

## Wording rule

Code, interface strings, specification text, commit messages and this packet's records name no third-party product.
