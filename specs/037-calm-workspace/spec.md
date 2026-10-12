# Feature Specification: Calm Workspace

**Packet**: 037 | **Feature directory**: `specs/037-calm-workspace` | **Date**: 2026-10-11
**Status**: Proposed. Planning only; no application code until Jay answers E1–E4.
**Input**: Jay, 2026-10-11: the app looks overloaded and tries to show everything at once; make it neat and clean.
**Baseline**: measured on the local preview at 1600×900 as Priya (PM) at commit `121e3ae2`. Numbers are in [verification.md](verification.md) section 2, and screenshots are in `baseline/`.

## Problem (measured)

| Symptom | Evidence |
|---|---|
| Work starts below the fold | The first data row sits 74–84 % down the screen on My Work, Tasks, Planner and Workload. On the Project Dashboard it is below the fold (1137 px at a 900 px screen height). |
| Filters are always open | 4–9 filter fields are always visible on 9 of 17 screens, plus up to 14 quick-filter chips on My Work. |
| Too many destinations | 24 tabs in two rows on every project page; 14 sidebar items for a PM, up to 17 for an Admin. |
| Too much text | 140–350 words above the fold on list screens. |
| Mixed signals | The Planner shows "Over-planned" and "Under-planned" on the same person row. |
| Heavy headers | The project header takes about 150 px (two-line title, three buttons, badges) before the tabs. |

The product's engineering depth is its advantage, so no capability is removed. The fix is **progressive disclosure**: show the work first, and put options one click away.

## Rules (binding on every screen, including packet 036's additions)

- **R1 One toolbar row.** Lists show one row: primary action, search, **Filters (n)**, Group or Sort when relevant, view switch, and an overflow menu (Export, Columns, Views). Filter fields open in a popover (a sheet on tablet). Active filters show as removable chips only while set.
- **R2 Three quick filters at most** visible on a screen. The rest become saved views under **Views**.
- **R3 Project navigation**: at most 7 visible tabs plus **More** (decision E1). Inside a group, a small second-level switch shows that group's pages. Every existing route and deep link keeps working and highlights its group.
- **R4 Sidebar**: at most 8 items for any role (decision E2). Duplicates of the header (Notifications) and views of another page (Boards, Calendar, Portfolio) move under their parent page or under More.
- **R5 One headline**: page title, plus at most a one-line scope. Explanations move to a help popover.
- **R6 One-line project header**: key · name (truncated) · status · health. Links and Following move into the overflow menu. Height ≤ 72 px.
- **R7 One signal per row**: list rows show the most severe indicator plus "+n". The rest appear in the row's detail. A week-specific indicator always names its week.
- **R8 Colour budget**: semantic colours only for status and health. At most 6 distinct fill colours above the fold. Brand navy and blue stay as they are.
- **R9 Dashboards lead with numbers**: the count row comes first and secondary visuals follow. Data begins above the fold.
- **R10 Personal home is a focus list**: My Work opens on what needs action today (decision E3). Other sections stay one click away.

## Open decisions (defaults used until Jay decides)

| # | Decision | Default |
|---|---|---|
| E1 | Project tab groups | **Overview** (Dashboard); **Work** (Tasks, Board, Timeline, Calendar, Milestones as views); **Deliverables** (Deliverables, Submissions, Reviews, Readiness); **Coordination** (Weekly Coordination, Handoffs, Changes, Design basis, Decisions, Risks, Issues, Meetings & Actions); **People** (Team & Disciplines, Allocations, Workload); **Files**; More: Activity, Settings. |
| E2 | Sidebar | My Work, Projects (List, Board, Portfolio views), Tasks (List, Board, Calendar views), Resources (Weekly Planner, Workload), Time, Reports. More: Home, Team, Files, Templates, Admin. Notifications only in the header bell. |
| E3 | My Work default | "Today": my overdue, due this week, blocked and needs-attention items. Upcoming, Completed, Inbox and Coordination are reachable through a view switch. |
| E4 | Quick-filter chips | Keep up to 3 pinned per screen (for example Mine, Overdue, Blocked). The rest become saved views. |

## Non-goals

- Removing any feature, route, permission, field, count or export.
- Changing data, rules, wording of statuses, or brand tokens. No theme change and no new UI library.
- Copying any third-party product's look. Only general layout principles are used.
- Phone layouts beyond today's behaviour.

## Acceptance

See [verification.md](verification.md). The measurable thresholds are AC-CALM-01 to AC-CALM-10, plus SC-CALM-01 and SC-CALM-02.

## Wording rule

Code, comments, UI text, specification text, commit messages and documentation name no third-party products.
