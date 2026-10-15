## 51. Planning, Reporting and Onboarding Amendment

Approved scope: on 2026-10-14 Jay approved the build-approach report and directed building all of it. Features are fixed and additive only: nothing in this section removes, hides, narrows or freezes a current feature, screen, route, report, setting or rule, and where an addition touches existing behaviour, today's behaviour stays the default. Packet 047 delivers this section. It adds nine things the report's roadmap lists: a getting-started card per role, the two capacity reports that §19 lists as MVP, an aggregate count of screen opens for the pilot, open roles defined in templates, unfilled roles and unassigned work beside free capacity, an optional project status note, two follow-up views on My Work, a project-list import, and three capacity refinements. Each one reuses records, rules and reads that already exist.

Where this section adds to §12.14 (templates), §12.15.2 and §37.6 (daily capacity, FR-CAP-02), §13.1 (Project Dashboard), §13.10 (My Work), §13.11 and §13.21 (Workload and Weekly Planner), §13.12 (Portfolio), §13.16 (Team), §19 (reports), §20.1 (what is logged), §23.8 (observability), §39 (PLN-05, FR-PLN-14, FR-PLN-15), §40.2 (FR-ROLE-04) and §42 (the away marker), this section governs. It resolves the contradiction between §19, which lists Team Capacity and Allocations by Source as MVP reports, and §39.12, which deferred them, by building the reports; the §19 rows stay. It also specifies three items that earlier sections deferred: per-person weekday hours and a project status note without seen-by (§40.10), and an away marker on the project Team tab (§42.5).

Every addition is deterministic and explainable. Nothing here assigns people, levels resources, forecasts demand, scores people or uses AI (§30, constitution II).

### 51.1 Scope and boundary

Part A (people starting and seeing the plan) covers the getting-started card (§51.2), the capacity reports (§51.3) and the route counter (§51.4). Part B (demand and follow-up) covers template roles (§51.5), unfilled roles and unassigned work (§51.6), the project status note (§51.7) and the My Work follow-up views (§51.8). Part C (setup and capacity detail) covers the project-list import (§51.9) and capacity polish (§51.10). §51.11 sets how each addition ships.

Outside this amendment:

- tours, badges, points, streaks, progress bars or any other gamification (§30);
- counting what a named person opens, or any route data tied to a user, session or item;
- a custom report builder;
- open roles inside a person's capacity, cells, bands, totals or indicators;
- placeholders in person capacity;
- dragging work onto people;
- seen-by, read receipts or stale reminders for the status note;
- an intake form or form builder;
- synchronising projects from another system, scheduled imports, or updating or deleting projects by import (§29 item 3);
- per-person dependency lag;
- readiness capacity checks reading schedules (§38.2 unchanged);
- hours budgets, actual-hours imports, custom fields and a public API, which stay parked under §30.

### 51.2 Getting-started card

- **FR-PRO-01.** My Work's Today view (§41 E3) MUST show the signed-in person a getting-started card on their own My Work only. The card lists the steps of §51.2's catalogue for each role the person holds, in the catalogue's order. Each step names one action and links to the screen where it is done. Collapsed, the card is one line, "Getting started: n of m done", with Show steps and Hide; it opens collapsed. It has no tour, badge, points, streak, percentage, progress bar or celebration. With the card present, My Work still meets AC-CALM-01 and AC-CALM-03.

- **FR-PRO-02.** Each step MUST tick itself when its record exists, computed on the server on every read by one pure function over the person's roles and a set of yes-or-no facts. Each fact is an existence check over the person's own records or, for Admin steps, organisation facts an Admin already sees. The response carries only the steps and whether each is done; it never carries counts, names or records of other people or of projects the person cannot view. A step whose action the person cannot take (role, Read Only, inactive) is not shown. A Read Only user sees no card.

- **FR-PRO-03.** Hide MUST store a per-user hidden time with the person's preferences (§17.4), so the card stays hidden on every device. The Preferences page offers Show getting started, which clears it. The card also stops showing once every step is done. Hiding or showing writes no activity entry, as other personal preferences do.

Step catalogue (default D2):

| Role | Step | Done when |
|---|---|---|
| Everyone except Read Only | Move one of your tasks to a new status | The activity log holds a task status change made by the person |
| | Comment on an item | A comment written by the person exists |
| | Add your own plan in My Week | A self entry of the person exists (FR-PLN-03) |
| Project Manager system role or Admin | Create a project | A project names the person as Project Manager |
| | Add a milestone | A milestone exists on a project the person manages |
| | Assign a task | A task on a project the person manages has an assignee |
| | Activate a project | A project the person manages has left Setup |
| Discipline Lead on any project | Add a deliverable | A deliverable created by the person exists |
| | Propose an allocation or open role | An allocation or open role created by the person exists |
| Supervisor with a direct report | Plan a week for a direct report | A manager entry owned by the person is Proposed assignment or Confirmed assignment |
| | Confirm an allocation | An allocation names the person as its confirmer |
| | Record time away for a direct report | The activity log holds an availability override recorded by the person |
| Executive | Star a project | A project star of the person exists |
| | Follow a project | A follow the person set themselves exists |
| Admin | Set Supervisors | At least one active user has a Supervisor |
| | Import approved leave | A leave import has been applied (§12.18.2) |
| | Import the project list | A project import has been committed (§51.9) |

### 51.3 Team Capacity and Allocations by Source reports

- **FR-PRO-04.** The report catalogue (§13.18, FR-RPT-01) MUST offer Team Capacity and Allocations by Source to viewers with Workload access. Their people are the Weekly Planner's scope (PLN-14). Parameters: Supervisor, discipline, from week, number of weeks (1 to 26, default `planning_horizon_weeks`) and "Include my private drafts" (default on, as on the planner). Every figure comes from the planner's own read (FR-PLN-11, PLN-05 to PLN-11) for the same people, weeks and filters; there is no second calculation. In §19's wording "allocations" means planning entries (§39) and approved project allocations (FR-PLN-12).

- **FR-PRO-05.** Team Capacity MUST have one column per week and these rows: Capacity, Time away, Confirmed, Expected, Possible, Remaining capacity (Capacity minus Confirmed minus Expected, with the §45.3 basis line "Remaining capacity in the Weekly Planner: capacity − Confirmed − Expected"), People over-planned (PLN-10), People under-planned (PLN-11) and People with partial view (PLN-15). Sums are exact and rounded after aggregation. A person with a partial view is never counted as under-planned. When any person in scope has a partial view, the screen and the export's Parameters sheet carry the PLN-15 label. "Open as filtered list" opens the Weekly Planner with the same filters.

- **FR-PRO-06.** Allocations by Source MUST offer two views through a Show parameter. "Hours by source" (default) has one column per week and one row per source category (§10.2) plus a row "Approved project allocation"; each value is counted hours after coverage (PLN-07, PLN-08) of the visible entries in that source, in all three confidence bands unless the Confidence parameter narrows them. A Total row equals Team Capacity's Confirmed plus Expected plus Possible for the same parameters. "Entries without a project" lists the visible entries with no project link: person, owner, label, source category, hours per week, start week, end week, confidence and visibility, labelled as FR-PLN-04 requires. A Source category parameter narrows both views. "Open as filtered list" opens the planner's entry list with the same filters.

- **FR-PRO-07.** Both reports MUST apply PLN-04, FR-PLN-18 and FR-PLN-19: another person's private drafts and work on projects the viewer cannot view are never counted, listed or revealed, and no total differs because of them. Both export to CSV and XLSX under §19's export rules, with the Parameters sheet, and each export is logged (§20.1). The Workload by Person report, the planner's exports and every other report are unchanged.

### 51.4 Aggregate route counter

- **FR-PRO-08.** An organisation setting `route_counter_enabled` (Bool, default off, §10.4) MUST be the lasting control of the counter. An Admin sets it on the existing settings screen, audited like every setting, and Jay turns it on for the pilot only. `flag.route_counter` only releases the addition (§43.4): while the flag is off the setting is not offered and counts as off, and the flag's cleanup keeps the setting. While `route_counter_enabled` is on, the application MUST send one record for each screen opened: a route key and a load time. The route key is the pattern of the matched route (for example `projects/:number/tasks`) and must be on a fixed server allowlist; it never carries an identifier, project number, query string, search text or item. The server silently drops any key that is not on the list. The load time is whole milliseconds from the navigation starting to the screen's first render, capped at 60,000. A failed send is ignored and never retried.

- **FR-PRO-09.** The server MUST keep one row per organisation day (G-03) and route key: the number of opens and the number of opens in each of five load-time bands (under 0.5 s, 0.5 to 1 s, 1 to 2 s, 2 to 5 s, 5 s or more). It stores no user, session, address, item, text or time finer than the day, and the record is not an activity-log entry. Each caller is rate limited in memory, so no per-user state is stored. While `route_counter_enabled` is off the application sends nothing and the server stores nothing.

- **FR-PRO-10.** A Pilot route use report MUST show, to viewers who may see Pilot measures, one row per route key and week with opens and the five load-time bands, for a Weeks parameter of 1 to 26 (default 8). Days are never shown. The Pilot measures report is unchanged. While `route_counter_enabled` is on, the Preferences page tells every user in one line what is counted and what is not.

### 51.5 Template roles become open roles

- **FR-PRO-11.** A template MAY carry template roles. A template role records a role label (1 to 80 characters), an optional template discipline, a start anchor (a template milestone and an offset in days), an end anchor (a template milestone and an offset in days), planned hours greater than 0 and at most 10,000, an optional note (up to 2,000 characters) and a sort order. Roles are edited on a Draft template with the rest of its structure, copied into a new draft, and versioned with the template (§12.14 governance). A structure save that sends no roles keeps the template's roles. A template without roles instantiates exactly as today.

- **FR-PRO-12.** Creating a project from a template (§12.14 steps 1 to 7) MUST turn each template role into one Open open role on the new project, in the same transaction: the label, the project discipline mapped from the template discipline, a from date of the start milestone's planned date plus its offset, a through date of the end milestone's planned date plus its offset, the hours and the note. The creating Project Manager is its creator and accountable owner (FR-ROLE-01), and each role writes its activity entry. Offsets count calendar days, as other template offsets do, or working days when the template uses §49's working-day offsets. A role is skipped, never blocking the project, when its discipline is not chosen, either anchor milestone has no date, or its dates break FR-ROLE-01 (through before from, a range over 366 days, or no working day on the project's office calendar).

- **FR-PRO-13.** The wizard preview MUST list each template role with its computed dates, hours and discipline, marked "Will be created" or with its skip reason, and the preview counts include roles. Created roles are ordinary open roles: FR-ROLE-01 to FR-ROLE-05 apply, including fill, Workload's Unfilled roles section and visibility. Adding a discipline pack from a template (FR-TPL-02) creates no roles (D9).

### 51.6 Unfilled roles and unassigned work beside free capacity

- **FR-PRO-14.** The Weekly Planner's Projects view MUST show an Unfilled roles group: Open roles on visible Setup or Active projects that overlap the displayed weeks and match the project and discipline filters, with the role label, project, discipline and hours for each week, spread as FR-ROLE-04 spreads them. Each row has a Fill link that opens the project's open-roles tab with that role's fill form, when the viewer may fill it (FR-ROLE-02); otherwise it links to the tab. The rows come from the same function as Workload's Unfilled roles section and are requested only by the Projects view. Open roles never enter a person's row, cell, band, remaining capacity, indicator, contribution list or the planner grid export. The grid view, the person picker and My Staff are unchanged.

- **FR-PRO-15.** Workload MUST show an Unassigned work section after its people and Unfilled roles: open tasks with no assignee on visible Setup or Active projects that match Workload's project and discipline filters, ordered by due date and then key, at most 50 rows, with the total count. Each row shows key, task, project, discipline, due date and estimate. "Assign to" appears only where the server says the viewer may assign that task (the AssignTask permission); it opens the existing owner picker with free hours for the task's week (FR-PICK-02) and saves through the existing task edit with its row version. "Show all" opens the cross-project task list with the Unassigned filter, and the count equals that list's rows (FR-CNT-01). An unassigned task enters no one's load, there is no drag, and the Workload export is unchanged.

### 51.7 Project status note

- **FR-PRO-16.** A project MAY carry one optional status note of 1 to 500 characters with its author and time. Whoever may edit the project (§8) sets, changes or clears it through the project edit command with the row version, under the Complete-project rule (P-05). Each change writes an activity entry with old and new values in the same transaction. Everyone who can view the project can read the note. It is plain text.

- **FR-PRO-17.** The Project Dashboard MUST show the note after its counts row (§41 R9), and Portfolio MUST show it on the project's row detail and as the last column of the Portfolio export. Above the note an automatic header shows, computed when read: computed health, reported health when an override is active (§16.4), the next milestone's name and date, and the count of open attention items. The header is never stored or edited. The note never changes computed or reported health and feeds no rule. The note shows its age and author ("updated 9 days ago by Sam"); there is no seen-by, read receipt or stale reminder.

### 51.8 My Work: Watching and Requested by me

- **FR-PRO-18.** My Work's view switch MUST offer Watching: the open items the signed-in person watches (a manual watch, or a mention under FR-COM-03) on projects they can view, with type, key, title, project, status and due date. A row opens the item's panel, and Stop watching removes the watch. Terminal and deleted items are left out. The view is offered only on the person's own My Work; Today stays the opening view (§41 E3).

- **FR-PRO-19.** My Work's view switch MUST offer Requested by me: a read-only list of the decisions the person requested and the handoffs, change notices and open roles the person created, on projects they can still view, with type, key or label, project, status, the date the item is needed by, and its current owner. Open items come first, then items closed within the last 14 days, the window Recently completed already uses (§13.10). The view adds no record, field or notification, and its count equals its rows.

### 51.9 Project-list import

- **FR-PRO-20.** An Admin MUST be able to preview a CSV of projects (UTF-8, with or without a byte-order mark, at most 256 KB and 1,000 rows) with the columns project number, name, client, office and Project Manager email. The preview checks every row: the number matches `project_number_format` and is unique in the file; the name has 1 to 200 characters; the client matches an active client by name or short name, and the office an active office by name or code, ignoring case; the Project Manager email matches an active user who holds the Project Manager system role or Admin (FR-PRJ-01). Each row is Create, Exists (the number is already in Tuesday) or Error with its reason. The preview stores nothing.

- **FR-PRO-21.** Commit MUST parse and check the same file again and refuse it while any row is an Error. It creates every Create row as a Setup project with the named Project Manager, under the existing project-create rules, in one transaction. Each project's activity entry names the Admin as actor and Import as source, and one import summary records the actor, time and counts, never the file (§20.1). Exists rows are skipped and never updated, so a repeated commit creates nothing new; Idempotency-Key applies. The import is manual and one way: no synchronisation, no schedule, no update and no deletion.

### 51.10 Capacity polish

- **FR-PRO-22.** The planner's contribution list (FR-PLN-15) MUST add one line per visible project in that person-week: the project's planned hours (approved allocation hours plus counted entry hours linked to it, PLN-07 and PLN-08) and the task-estimate context for that project's open tasks (FR-PLN-14) with its unestimated count. The Projects view shows a Task estimates context row for each person and project. These figures are context only: they never enter bands, remaining capacity or indicators, and the per-project estimates sum to the cell's existing task-estimate figure.

- **FR-PRO-23.** A person's schedule (§12.15.2) MAY set hours for each weekday, from 0 to 24 in steps of 0.5, instead of weekly hours split evenly. When set, a working day's normal capacity is that weekday's hours, the working days are the weekdays with hours above 0, and the weekly hours are their sum. When not set, the even split applies, so every existing schedule gives the same capacity as today. The same editors, effective-from date, row version and audit apply as for today's schedule, and the leave hours-per-day check uses the day's hours. The rule applies wherever a schedule is read today: Workload, the planner, My Week, the person picker, My Staff, the digest hold and allocation confirmation. Readiness capacity checks (§38.2) are unchanged.

- **FR-PRO-24.** The project Team tab (§13.16) MUST show "N h away" beside a member who has time away in the current ISO week (PLN-06), from the FR-PICK-01 batch read: one request per 50 members, only for viewers with Workload access, and only for members in the viewer's planner scope. Other members show nothing, and the response holds nothing about them. The chip shows no free hours and no reason, and it is information only. Time away carries no project detail, so the chip shows for a person with a partial view too.

### 51.11 Shipping dark

- **FR-PRO-25.** Each addition in §51.2 to §51.10 MUST merge dark behind its own flag under §43's flag rules, declared with its packet, and is turned on by Jay. With a flag off, every screen, response, report and export is as it was before that addition. No existing feature is put behind a flag. Each flag has a pair test, off and on.

| Flag | Covers |
|---|---|
| `flag.onboarding_card` | §51.2 |
| `flag.capacity_reports` | §51.3 |
| `flag.route_counter` | §51.4, release only; the lasting control is the `route_counter_enabled` setting, on for the pilot only |
| `flag.template_roles` | §51.5 |
| `flag.unfilled_beside_capacity` | §51.6 |
| `flag.project_status_note` | §51.7 |
| `flag.my_work_follow_up` | §51.8 |
| `flag.project_import` | §51.9 |
| `flag.capacity_polish` | §51.10 |

### 51.12 Acceptance criteria

- **AC-PRO-01.** Given a Project Manager with no projects opens My Work, then Today shows one collapsed line "Getting started: 0 of 7 done" for the Everyone and Project Manager steps; after they create a project, the next read shows "1 of 7 done" with Create a project ticked. The card has no badge, percentage or progress bar, and Today still meets AC-CALM-01 and AC-CALM-03 with the card present.

- **AC-PRO-02.** Given a Read Only user, then no card appears. Given another person creates a project, then this person's Create a project step stays unticked, and the response contains no data about the other person or their project.

- **AC-PRO-03.** Given a person hides the card, then it stays hidden in another browser; Show getting started on Preferences brings it back; once every step is done, the card no longer shows. Neither action writes an activity entry.

- **AC-PRO-04.** Given a Supervisor scope and 12 weeks, then for each week Team Capacity's Capacity, Time away, Confirmed, Expected, Possible and Remaining capacity equal the sums of the Weekly Planner's cells for the same people, weeks and filters, and People over-planned equals the number of Over-planned cells. A person with a partial view is counted in People with partial view, never in People under-planned, and the PLN-15 label shows.

- **AC-PRO-05.** Given another Supervisor's private draft and a Confirmed allocation on a Restricted project the viewer cannot view, then neither record nor its hours appears in either report. The viewer's own draft counts only while "Include my private drafts" is on, and the Parameters sheet records the setting.

- **AC-PRO-06.** Given a 12 h approved allocation on project P in week W, an 8 h Confidence: Expected entry linked to P and an unlinked 6 h Admin entry, then Hours by source shows 12 h on Approved project allocation, 0 h on Major project and 6 h on Admin for week W, and the Total row equals Team Capacity's Confirmed plus Expected plus Possible. Entries without a project lists the 6 h entry only.

- **AC-PRO-07.** Both reports export to CSV and XLSX with a Parameters sheet, and each export is logged. For the same fixture, the Workload by Person report and the planner grid and entry exports are identical before and after this packet.

- **AC-PRO-08.** Given `flag.route_counter` and `route_counter_enabled` are on, when a user opens a project's task list twice and My Work once on the same day, then the day's rows hold opens 2 for `projects/:number/tasks` and 1 for `my-work`, each with its load-time bands, and no column holds a user, item, project number or text. A key that is not on the allowlist, or that carries a project number, is not stored.

- **AC-PRO-09.** Given `route_counter_enabled` is off, or `flag.route_counter` is off, then the browser sends no route record and nothing is stored. The Pilot measures report gives identical output with the flag off and on. Pilot route use shows weekly rows only, and Preferences shows the one-line notice only while the counter is on.

- **AC-PRO-10.** Given a template role "Civil technologist" from the 30% submission milestone plus 0 days to the 60% submission milestone minus 5 days, 120 h, when a Project Manager creates a project from the template with both milestone dates set, then one Open open role exists with those dates, 120 h, the mapped discipline and the creating Project Manager as creator, with its activity entry. A template without roles creates the same milestones, deliverables, tasks and dependencies as before this packet.

- **AC-PRO-11.** Given template roles whose discipline is not chosen, whose anchor milestone has no date, or whose range exceeds 366 days, then none is created, the project is created, and the wizard preview shows each skip reason before creation.

- **AC-PRO-12.** Given an Open role on a visible project, then the Planner's Projects view lists it under Unfilled roles with its hours per week and a Fill link that opens its fill form for a viewer allowed to fill it. Every person row, cell, band, indicator and the grid export are identical with and without the role. A viewer who cannot view the project sees no row.

- **AC-PRO-13.** Given three unassigned open tasks on a visible project and one on a project the viewer cannot view, then Unassigned work lists three rows, and its count equals the rows of the cross-project task list with the Unassigned filter. "Assign to" sets the assignee with the row version, a stale version gets a conflict and stores nothing, and no person's load changes before the assignment.

- **AC-PRO-14.** Given a Project Manager saves a 480-character status note, then a Viewer of the project reads it with its author and age, a 501-character note is refused, and the project history records the change. The automatic header shows computed health, the next milestone and the attention count, and computed and reported health are unchanged.

- **AC-PRO-15.** Given a project with a status note, then Portfolio shows it on the project's row detail and as the last column of the export. A team member who may not edit the project is refused, and on a Complete project the change needs a reason.

- **AC-PRO-16.** Given a person watches a task and is mentioned on a decision, then Watching lists both. When the task is completed it leaves the list; when the person loses access to the decision's project the decision leaves it; Stop watching removes the watch.

- **AC-PRO-17.** Given a person requested a decision and created a handoff, a change notice and an open role, then Requested by me lists all four with status and owner; an item closed 10 days ago is listed and one closed 20 days ago is not; nothing is created or notified; the view is not offered on another person's My Work.

- **AC-PRO-18.** Given a five-row file with three valid rows, one existing project number and one unknown client, then the preview shows 3 Create, 1 Exists and 1 Error, and commit is refused. After the file is fixed, commit creates three Setup projects with their named Project Managers and one import summary, and a repeated commit creates none. A user who is not an Admin is refused.

- **AC-PRO-19.** Given a file of 1,001 rows, or over 256 KB, then it is refused. A project number repeated in the file, or a Project Manager email of a user without the Project Manager system role or Admin, is an Error row.

- **AC-PRO-20.** Given a person-week with 10 h of task estimates on project P and 4 h on project Q, and an 8 h entry linked to P, then the contribution list shows P with 8 h planned and 10 h of estimates and Q with 0 h planned and 4 h of estimates; the estimates sum to the cell's 14 h, and the bands and remaining capacity are unchanged.

- **AC-PRO-21.** Given a schedule of Monday 8 h, Tuesday 8 h, Wednesday 4 h and no hours on Thursday and Friday, then the week's capacity is 20 h, Wednesday's is 4 h, Thursday is not a working day, and a leave of 6 h per day on Wednesday is refused as above the day's hours. A schedule without weekday hours gives the same capacity as before this packet, and readiness results are unchanged.

- **AC-PRO-22.** Given a Supervisor opens the Team tab of a project with 12 members, then one capacity request names the members; a direct report with 8 h of time away this week shows "8 h away"; a member outside the Supervisor's planner scope shows nothing; a viewer without Workload access makes no request.

- **AC-PRO-23.** Given each flag of §51.11 off, then the screens, responses, reports and exports it covers are identical to those before its addition; given it on, the addition is present. No existing feature checks a flag.

### 51.13 Decisions and deferred proposals

Each decision below uses its default until Jay decides otherwise.

| # | Decision | Default used |
|---|---|---|
| D1 | Where the getting-started card shows | My Work's Today view, collapsed to one line, on the person's own My Work only. |
| D2 | Step catalogue | The table in §51.2: three or four steps per role, each ticked by an existing record. |
| D3 | A person with several roles | One group per held role, in the catalogue's order. |
| D4 | Team Capacity layout | Measures as rows, weeks as columns, for the people in scope. |
| D5 | Private drafts in the reports | The caller's own drafts count by default, as on the planner, with a parameter to leave them out. Other people's drafts never count. |
| D6 | Allocations by Source hours | Counted hours after coverage, all three confidence bands, with approved project allocations as their own row. |
| D7 | Where route counts appear | A Pilot route use report beside Pilot measures, which stays unchanged. |
| D8 | Opting out of the route counter | No per-user opt-out. The organisation setting `route_counter_enabled` is the control, on for the pilot only, and users are told while it is on. `flag.route_counter` only releases the addition and is cleaned up as usual. |
| D9 | Template roles in discipline packs | Not created; roles come only with a whole new project. |
| D10 | A template role that cannot be dated | Skipped with its reason shown in the preview; it never blocks the project. |
| D11 | Rows in Unassigned work | At most 50, with the count and Show all. |
| D12 | Who reads the status note | Everyone who can view the project. It is also the last Portfolio export column. |
| D13 | Closed items in Requested by me | Closed within the last 14 days, as Recently completed. |
| D14 | Which watches Watching lists | Every watch the person holds, manual or from a mention. |
| D15 | Import column names | Tuesday's five headers until Jay supplies a sample of the company project-list export; the headers then follow the sample, which is a data change, not a behaviour change. |
| D16 | A project number that already exists | Skipped and never updated. |
| D17 | Weekday hours step | 0.5 h, as planning entries use. |
| D18 | Away chip for a person with a partial view | Shown, because time away carries no project detail. |
| D19 | Flags | One flag per area, as §51.11 lists. |

Deferred follow-up proposals, outside packet 047:

- open roles from discipline packs;
- readiness capacity checks reading schedules and weekday hours;
- a per-user opt-out from the route counter;
- seen-by or a stale reminder for the status note;
- an intake form;
- updating existing projects or adding fields by import;
- Team Capacity grouped by Supervisor or discipline rows;
- Watching for items the person may not open in a panel.

Open-role export is built by §47 FR-REC-12 behind its flag and kept on a KILL verdict (FR-REC-14). If the 043 spike never runs because Jay declines 043-D1, it returns to this deferred list (043-D4).
