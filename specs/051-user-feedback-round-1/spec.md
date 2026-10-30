# Feature Specification: User Feedback Round 1

**Packet**: 051 | **Feature directory**: `specs/051-user-feedback-round-1` | **Date**: 2026-10-24
**Status**: Implemented and checked locally (S01–S10), including save recovery, Backlog and admin review. S11 cleanup after pilot activation and hosted and human acceptance remain pending. Line of record: `codex/integrate-039-050`, committed locally and not pushed. Not Accepted. Code is deployed in public synthetic review `bf8a39d7` with visible feature flags off; full hosted-feature/pilot/human acceptance remains separate.
**Input**: the first handwritten feedback list from pilot users, "Things to add to Tuesday app" (twelve items), photographed on 2026-10-24 and transcribed in §55.1.
**Source**: product specification §55 (§55.1 to §55.6) in `spec-parts/30-user-feedback-round-1.md`. Related contracts: §12.5 (tasks, rules T-01 to T-18), §13.3 (Task List and task panel autosave), §13.4 (Board), §13.5 (Timeline Unscheduled list), §13.10 and §36.7 (My Work), §13.15 (Administration), §17.3 (daily digest), §18.4 (saved views), §19 (reports), §20.1 (log), §25.6 (errors), §36.3 (manual card order), §38 (readiness), §43 (flags, packet 039), §46 FR-LIVE-03 (reload notice), §49.3 (every count opens its list), §54 (units, packet 050), FR-AUTH-03 and FR-AUTH-04 (roles, inactive users), FR-GRP-01 (home discipline), FR-TEAM-05 (discipline summary).

## User outcome

- **Anyone entering data:** typed input survives a closed dialog, a refresh, a dropped network and an expired sign-in; the Hub never throws it away without the person choosing to.
- **Project Manager, Discipline Lead, team member:** known-but-undated work has a home in the project Backlog and in My Work → No date, without inventing due dates that poison overdue and workload figures.
- **System Administrator:** one page lists what has drifted (inactive users on teams, people with no manager or unit, in-app role grants, idle accounts, projects with an inactive PM, stale projects, changed settings, deactivated reference data in use), each count opens its list, and a recorded review with an export is the access-review evidence IT security asks for.

## Scope

1. **Part A, Data-entry safety** (§55.2; FR-SAV-01..08): per-field autosave on every edit panel; retry that never reverts typed input; local browser drafts for create dialogs and long text; leave prompt; input kept across sign-in expiry; reload notice stays non-automatic; one log entry per field change; `flag.data_entry_safety` for the visible additions.
2. **Part B, Backlog** (§55.3; FR-BKL-01..09): derived membership (open, no due date, no start date); project Backlog view, Board column and dashboard count; quick add with visible defaults; scheduling by date; excluded from date-based indicators and the Workload forecast; My Work → No date; discipline summary count; export column; `flag.backlog`.
3. **Part C, Admin review** (§55.4; FR-ADR-01..08): nine computed checks, each a count that opens its list; recorded reviews with notes; due date and digest line; fixes through existing tools only; CSV and XLSX export; Admin-only; `flag.admin_review`.
4. **Recorded, not specified** (§55.1): items 2, 3, 4, 6, 9, 11 and 12 are mapped to existing sections or to decisions 051-D9 to 051-D14; this packet builds nothing for them.

## Non-goals

- Server-side drafts or drafts shared between devices (051-D2).
- A Backlog status, label or stored flag; a deliverable or milestone backlog (051-D5, 051-D7); sprints, points or velocity (§30).
- Any automatic fix, bulk action or edit path on the Admin review page (FR-ADR-05); enforcement of a review before anything else is allowed.
- A months-ahead forecast (051-D9), admin-defined access levels (051-D11), the ERP sync (051-D12), local passwords (051-D13).

## Acceptance

AC-SAV-01, AC-SAV-02, AC-SAV-03, AC-SAV-04, AC-SAV-05, AC-SAV-06, AC-BKL-01, AC-BKL-02, AC-BKL-03, AC-BKL-04, AC-BKL-05, AC-BKL-06, AC-ADR-01, AC-ADR-02, AC-ADR-03, AC-ADR-04, AC-ADR-05 and AC-ADR-06 in §55.5 are this packet's acceptance criteria. [verification.md](verification.md) maps each one to its slice.

## Decisions

051-D1 to 051-D14 in [DECISIONS.md](../DECISIONS.md), mirrored in §55.6. D1 was approved on 2026-10-25; D2 to D8 shape Parts A to C and use their defaults; D9 to D14 dispose of the feedback items that are not built here. A pending row blocks only the flag flip.

## Assumptions

- **Packet 039, S09:** flags exist before any slice merges.
- **Task panel autosave** (`web/src/pages/projects/TaskPanel.tsx`) is the reference for FR-SAV-01 and is not rewritten.
- **Timeline Unscheduled list** (`web/src/pages/Gantt.tsx`) stays as it is; Part B adds beside it.
- **Packet 050, FR-UNT-07:** the "Users without a home unit" check reads `AppUser.UnitId` and appears only with `flag.org_structure` on; until packet 050 is built, the check is absent.
- **Packet 045, §49.3:** the count-opens-list link pattern exists; until then Part B and Part C counts link to the filtered list by URL the way today's counts do.
- **Workload** already separates unscheduled estimates from weekly load (§13.11); FR-BKL-05 changes no calculation.
