## 41. Calm Workspace Amendment

Approved scope: on 2026-10-11 Jay asked for a neat and clean workspace that stops showing everything at once, delivered by packet 037, and accepted the default for every open decision (§41.3). This section records the layout contract; implementation and acceptance remain pending. The product's engineering depth is kept: no feature, route, permission, field, count or export is removed. The fix is progressive disclosure: show the work first and put options one click away.

This section governs placement only. It amends the layout, filter and project header conventions of §13.0, the Project Dashboard order of §13.1, the shell placement of §36.1 and the opening view of My Work in §36.7. Every destination FR-VIS-01 requires stays in the shell, as a sidebar item, as a view of its parent item or under More. Data, rules, permissions, status names, the brand palette and the API are unchanged, and no new interface library is introduced. The rules bind every screen, including those added by §40.

### 41.1 Measured baseline

Measured on 2026-10-11 on the local preview at 1600×900 as a Project Manager, before any change, on 17 screens: Home, My Work, Boards, Projects, Tasks, Calendar, Portfolio, Workload, Weekly Planner, Reports, Team, and the project Dashboard, Tasks, Deliverables, Timeline, Allocations and Weekly Coordination pages.

| Measure (above the fold, outside tables and grids) | Baseline |
|---|---|
| First data row, from the top of the screen | 425–1137 px (74–84 % down on My Work, Tasks, Weekly Planner and Workload; below the fold on the project Dashboard) |
| Always-visible filter fields with no filter set | 0–9 (4–9 on 9 of 17 screens), plus up to 14 quick-filter chips on My Work |
| Chrome controls (buttons, links and fields) | 11–50 |
| Chrome words | 16–232 |
| Distinct fill colours | 5–12 |
| Project tabs | 24, in two rows |
| Sidebar items | 14 for a Project Manager, up to 17 for an Admin |
| Project header height | about 150 px before the tabs |

The Weekly Planner also shows Over-planned and Under-planned on the same person row. Packet 037's verification record holds the per-screen figures, the census method and the baseline screenshots.

### 41.2 Layout rules

| Rule | Requirement |
|---|---|
| R1 | **One toolbar row.** Lists show one row: primary action, search, **Filters (n)**, Group or Sort when relevant, view switch, and an overflow menu (Export, Columns, Views). Filter fields open in a popover (a sheet on tablet). Active filters show as removable chips only while set. Filter state stays in the URL exactly as before. |
| R2 | **Three quick filters at most** are visible on a screen. The rest of the §13.0 quick chips become built-in entries under **Views** with the same URL parameters, so every former quick-filter URL still works. |
| R3 | **Project navigation**: at most 7 visible tabs plus **More**, grouped as in §41.3 (E1). Inside a group, a small second-level switch shows that group's pages. Every existing route and deep link keeps working and highlights its group. |
| R4 | **Sidebar**: at most 8 items for any role, as in §41.3 (E2). Duplicates of the header (Notifications) and views of another page (Boards, Calendar, Portfolio) move under their parent page or under More. |
| R5 | **One headline**: the page title, plus at most a one-line scope. Longer explanations move to a help popover next to the title. |
| R6 | **One-line project header**: key · name (truncated, full name on hover and focus) · status · health, at most 72 px high. Links and Following move into the header's overflow menu; the other §13.0 header facts stay reachable within one click. |
| R7 | **One signal per row**: list rows show the most severe indicator plus "+n"; the rest appear in a tooltip and the row's detail. A week-specific indicator always names its week. |
| R8 | **Colour budget**: semantic colours only for status and health, and at most 6 distinct fill colours above the fold. Brand navy and blue stay as they are. Colour is never the only signal (§13.0). |
| R9 | **Dashboards lead with numbers**: the counts row comes first and secondary visuals, such as the milestone strip, follow it. Data begins above the fold. |
| R10 | **Personal home is a focus list**: My Work opens on what needs action today (§41.3, E3). Other sections stay one click away. |

### 41.3 Navigation groups and defaults

Jay accepted every default below on 2026-10-11 ("lets start build").

| # | Decision | Default used |
|---|---|---|
| E1 | Project tab groups | **Overview** (Dashboard); **Work** (Tasks, Board, Timeline, Calendar and Milestones as views); **Deliverables** (Deliverables, Submissions, Reviews, Readiness); **Coordination** (Weekly Coordination, Handoffs, Changes, Design basis, Decisions, Risks, Issues, Meetings & Actions); **People** (Team & Disciplines, Allocations, Workload); **Files**; More: Activity, Settings. |
| E2 | Sidebar | My Work, Projects (List, Board and Portfolio views), Tasks (List, Board and Calendar views), Resources (Weekly Planner, Workload), Time, Reports. More: Home, Team, Files, Templates, Admin. Notifications only in the header bell. Items still follow role permissions. |
| E3 | My Work default | "Today": the user's overdue items, items due this week, blocked items and attention items routed to them. This focus list replaces FR-VIS-08's narrower Today (due on the current date) as the opening view. Upcoming, Overdue and Completed, the Inbox, saved views and the Coordination sections stay reachable through the view switch. |
| E4 | Quick-filter chips | At most 3 pinned per screen (for example Mine, Overdue, Blocked); the rest become saved views. |

### 41.4 Acceptance criteria

The census in packet 037's verification record measures AC-CALM-01 to AC-CALM-05 and AC-CALM-09 at 1600×900 on the 17 screens of §41.1, for a Project Manager and a Supervisor, on one release commit.

- **AC-CALM-01.** Work is visible first: the first data row starts at most 405 px (45 % of the screen) from the top on all 17 screens. Baseline: 425–1137 px.

- **AC-CALM-02.** Filters are closed by default: with no filter set, at most 1 filter field (search) is visible on each of the 17 screens. Baseline: 0–9.

- **AC-CALM-03.** Calm chrome: at most 25 chrome controls above the fold on every screen. Baseline: 11–50.

- **AC-CALM-04.** Fewer destinations: at most 7 project group tabs plus More, and at most 8 sidebar items for an Admin, an Executive, a Supervisor, a Project Manager, a team member and a Read Only user. Baseline: 24 tabs; 14–17 items.

- **AC-CALM-05.** Less reading and fewer colours: chrome words above the fold are at most 50 % of each screen's baseline, and at most 6 fill colours show above the fold. Baseline: 16–232 words; 5–12 colours.

- **AC-CALM-06.** Nothing lost: every route loads for a permitted user and highlights its sidebar item and project group; every former tab and quick filter is reachable in at most 2 clicks from its page; and every former quick-filter URL returns the same rows as before the change.

- **AC-CALM-07.** Filters behave the same: setting a filter in the popover updates the URL exactly as before, shows a removable chip and a count on the Filters button; Clear resets all; reloading restores the state.

- **AC-CALM-08.** Keyboard and screen readers: Enter opens the Filters popover, focus moves inside, and Esc closes it and returns focus. More menus are operable with the arrow keys. An automated accessibility scan reports no serious or critical issues on the 17 screens.

- **AC-CALM-09.** One signal per row: no row shows two indicators at once; extras appear as "+n" with a tooltip; week-specific indicators name their week. Baseline: Over-planned and Under-planned on one Weekly Planner row.

- **AC-CALM-10.** Common tasks are not slower: the click count for each path below is no higher than before the change, recorded before and after: open my most overdue task from My Work; show a project's overdue tasks for one discipline; open a project's high risks; add 8 h of planning for a person next week; export a project's deliverables; change a task's owner.

People checks: two working Project Managers, shown project Tasks, My Work and the Weekly Planner for 5 seconds each, both name each page's purpose and the first thing they would click; and Jay approves the 17 screens at 1440 px and 1024 px widths against the baseline as neat, clean and complete. Phone layouts keep today's behaviour.
