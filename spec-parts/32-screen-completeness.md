## 57. Screen Completeness Amendment

Proposed scope: drafted on 2026-10-31 from the dashboard and screen research of that date (kept outside the repository). The research compared the 33-screen prototype of §56 with the specification and with observed behaviour of other planning and document-control tools, screen by screen. Jay accepted its Home ideas and five patterns from a second prototype the same day. Packet 053 delivers this section. The scope awaits Jay's approval (053-D1).

The amendment is additive only: nothing in this section removes, hides, narrows or freezes a current feature, screen, route, report, setting or rule. Where an addition touches existing behaviour, today's behaviour stays the default.

The research items fall into three groups:

- **Already specified.** These items are not restated. Packet 053 lists them as build and prototype gaps against their existing IDs.
- **Partly specified or new.** These are the requirements below, with acceptance criteria in §57.10.
- **In conflict with an existing rule.** These are open decisions with recommended defaults (§57.1). Where a default adopts an item, the requirement that embodies it names the decision.

Every addition is deterministic and explainable (constitution II): each figure, flag and marker traces to one rule stated here, and the interface shows that rule. The following are excluded throughout: AI, machine-learning and LLM features of any kind; money (rates, budgets, costs, margins, invoices); timesheet submission, approval, locking or completeness scoring; per-person comparisons, rankings or scores; critical-path calculation; file storage; and custom roles, fields or statuses (§30).

Where this section adds to §12.3, §12.4, §12.10, §12.14, §13.1 to §13.21, §17.4, §18.1, §18.4, §19, §36 and §37 to §55, this section governs.

**Data, permission and validation boundary.** This amendment introduces no unscoped
schema, role, status or permission. A retained concept is an additive, nullable or
project-scoped record/setting with a row version, server-side permission check, and
same-transaction activity entry under §§8, 10 and 20; derived display values are read
functions and are never stored. New organisation vocabularies are Admin-managed,
validated against their list, and retain an `Other` value where the decision table says
so. New person facts (certification, type and responsibility note) are optional and
display-only unless a requirement explicitly names a command. If implementation finds
that a concept needs a non-additive change to the §10 canonical model, its slice stops
and records an OPEN decision; it does not silently reinterpret §10. All new commands
use the existing optimistic-concurrency and permission contracts, and all new reads
apply project, people-scope and partial-view rules before calculating figures.

The task table in §57.2 uses **Blocked** and **Waiting** only as the canonical derived
indicators of §10.3/§15 D-05/D-06; neither is a workflow status. A next actor is resolved
from the canonical owner/assignee/reviewer/resolver/verifier field or an `ExternalParty`.
If the field is absent or the actor is not an internal user, the UI says so explicitly
(for example “Reviewer missing” or “External party — no account”) and never guesses the
viewer or another person. A drilldown link always targets the exact blocker/child record;
the parent record is shown only as context. Review-comment closure remains split between
the resolver and the independent verifier under §10; “originator” is not silently used as
the verifier. Canonical verification is the recorded verifier, or the originator where FR-MRV-03 assigns it and no reassignment exists; FR-MRV-04 still applies.

Each requirement names one accountable owner role, the role that answers for that screen outcome, and a size from the research: S is one slice, M two. Thresholds are organisation settings (§10.4), never hard-coded.

### 57.1 Priority, parts and dispositions

The parts are listed in priority order. Parts A and B come first because Jay accepted them on 2026-10-31. Each part ships dark behind its own flag (§57.9), as §51 to §55 do.

| Part | Covers | Requirements | Flag |
|---|---|---|---|
| A | Cross-screen patterns (§57.2) | FR-SCR-01 to FR-SCR-06 | `flag.screen_patterns` |
| B | Home (§57.3) | FR-SCR-07 to FR-SCR-14 | `flag.home_overview` |
| C | My Work, task lists, Boards, Timeline, Calendar, task panel (§57.4) | FR-SCR-15 to FR-SCR-27 | `flag.work_screens` |
| D | Projects, Portfolio, project Dashboard (§57.5) | FR-SCR-28 to FR-SCR-38 | `flag.project_screens` |
| E | Reports, planning, Workload, My Staff, Allocations, Time (§57.6) | FR-SCR-39 to FR-SCR-55 | `flag.capacity_screens` |
| F | Deliverables, Reviews, Milestones, Weekly Coordination, registers (§57.7) | FR-SCR-56 to FR-SCR-71 | `flag.register_screens` |
| G | Team, Files, Notifications, Search, Preferences, Templates, Admin (§57.8) | FR-SCR-72 to FR-SCR-83 | `flag.people_screens` |

The research named ten conflicts. They and the additional specification conflicts below remain open decisions with recommended defaults:

| Conflict | Rule it touches | Decision and default |
|---|---|---|
| About fourteen fill colours above the fold | §41 R8, AC-CALM-05 | 052-D2: fixed-meaning tokens replace the six-fill limit |
| One-list sidebar with no More group | Decision E2, §41 R4, AC-CALM-04, FR-VIS-01 | 052-D3: adopt the list of eight and a foot group |
| Charts on Reports, Portfolio and dashboards (donut, report chart) | §19 "not built: charts beyond the milestone strip", §41 R8, R9 | 053-D2: Home's two FR-VIS-07 charts in the prototype's forms; one chart per report and one summary row per planner group, each from the same read as its table; no new chart on Portfolio or the project Dashboard |
| My Staff six-week strip; this week split by project | §42.5 D1, §13.19, FR-SUP-01, FR-SUP-05 | 053-D3: not built; §42 stands |
| Estimated against logged hours per discipline; template hours against actual | 039-DEF1, §29 item 5, §30, §39.12 | 053-D4: not built; stays parked |
| Over-capacity chip on board cards | §41 R7, R8 | 053-D5: not built; the person picker (FR-PICK-02) and Workload keep showing it |
| Open roles outside Allocations and Workload | FR-ROLE-04 | 053-D6: allowed as separate, labelled figures that never enter a person's figures (FR-SCR-28, FR-SCR-34, FR-SCR-37, FR-SCR-47, FR-SCR-73) |
| Elapsed % beside progress % | §30 (earned value) | 053-D7: two plain facts, no ratio or index (FR-SCR-32) |
| Project identity colours (hashed today, so they collide) | §41 R8 | 052-D5: stored per project (FR-VSN-07) |
| Person panel and optional person facts | §39.12 deferred People/Person Profile | 053-D21: narrow read-only panel and fixed optional facts only; the full screen stays deferred |
| Holding immediate email while away | §42.5 deferred hold/redirect | 053-D10: propose hold only, no deputy redirection; existing daily-digest behaviour stays |
| Report FTE unit basis | Existing reports use hours; the research did not settle a denominator | 053-D22: standard full-time hours over the exact displayed period |
| Prototype wording that differs from the application's labels | `web/src/i18n/en.ts` | 052-D8: the application's labels win |

The following are recorded and not specified:

- **Under 053-D4:** planned hours beside logged hours on Time, which is FR-RES-17 and stays Phase 3; a task estimate against logged hours bar; template hours against actual. The task panel's actual total beside its estimate is already §13.3.1.
- **Excluded as per-person comparisons:** report charts grouped by person.
- **Under 053-D10:** redirecting notices to a deputy while away.
- **Not research findings:** prototype ideas outside the research findings, such as suggesting the colleague with the most free hours, are not specified.

### 57.2 Part A: Cross-screen patterns

- **FR-SCR-01.** Every item panel (task, deliverable, milestone, decision, risk, issue, meeting action, handoff, change notice, constraint, review comment, submission package) and every Weekly Coordination card MUST show four facts together at the top: **owner** (the item's one accountable owner), **next actor**, **needed by** and **next action**. Next actor and next action are derived on read by one pure function from the item's type and state, as the table below gives; needed by is the item's due, required-by or needed-by date. When the next actor is the viewer the line says "You". Terminal states retain the owner, needed date and explicit "No next action"; the next actor is absent. Owner never changes to match the next actor. Nothing is stored. Links reuse the existing application route and registered entity kind plus stable ID/key, not the prototype's review-only hashes. Related records are only explicit stored source/target/blocker/review relationships, deduplicated by kind and ID and ordered by relationship kind, key and ID. Keep project, filters, sort and scroll when opening/closing a panel; a copied link reopens that exact record under the recipient's current permissions. Missing, inaccessible or deleted records follow existing authorization/recovery behaviour without exposing their existence or substituting a parent. Unknown, inactive, unassigned or multiple eligible next actors are labelled explicitly; the UI never guesses an actor or grants a command that the server's permission response refuses. *Owner: Project Manager. Size M.*

  | Item and state | Next actor | Next action |
  |---|---|---|
  | Task Not Started or In Progress, not Waiting/Blocked | Assignee | Start or Complete |
  | Task Waiting or Blocked | Owner of the first blocking input (FR-SCR-02) | Resolve the input |
  | Task On Hold | Assignee | Resume |
  | Task Ready for Review or In Review | Reviewer | Review |
  | Task Revision Required | Assignee | Revise |
  | Deliverable not Issued | Deliverable owner | Its canonical permitted next transition; name any required review gate |
  | Milestone not Complete or Cancelled | Project Manager | Review prerequisites or the permitted milestone transition; never bypass M-09 |
  | Change notice with pending assessments | Recorded affected owners of pending assessments | Assess the change; show each pending assessment and its actor |
  | Change notice with required updates | Recorded owner of each follow-up/verification step | Complete or verify that recorded step |
  | Handoff Draft | Sending owner | Submit |
  | Decision not decided | Decision owner, internal or external | Decide |
  | Handoff Submitted | Receiving owner | Accept, request clarification or return |
  | Handoff Accepted | Receiving owner | Incorporate |
  | Handoff Clarification Requested or Returned | Sending owner | Respond and resubmit |
  | Constraint Open | Removal owner | Remove |
  | Constraint Resolution Proposed | Affected work owner | Verify removal |
  | Review comment Open | Resolution owner | Respond |
  | Review comment Responded | Canonical verifier: originator unless reassigned under FR-MRV-03; independent-review setting still applies | Verify closure |
  | Submission package Draft | Coordinator | Start checking |
  | Submission package Checking | Checking owner of the first failed/pending check, otherwise coordinator | Resolve that check or run the current checks; Ready remains derived under FR-SUB-03 |
  | Submission package Ready | Authorised Project Manager | Issue after the canonical server-side recheck |
  | Risk, issue or meeting action, open | Owner | Review, Resolve or Complete |

  Derived Waiting/Blocked takes precedence over a task's ordinary next action, except a terminal state. Select the primary input from required Fail checks before Pending, then earliest known needed-by date (undated last), stable kind and ID; all remaining inputs stay visible. Resolve each input's next actor from its own canonical state (for example a Submitted handoff needs its receiving owner, a Resolution Proposed constraint its affected work owner). An unmapped or ambiguous state says "Next action not assessed" with a link to the exact record, never a guessed transition.

- **FR-SCR-02.** A task that is Blocked or Waiting MUST show a "Waiting on" block in its panel, listing each input from the existing canonical materialized blocker list (§10.3). Each input shows its key, title, the person who must resolve it, its needed-by date and the other open tasks with that same actual blocker evidence. One shared input is one entry, "Also holds up n tasks", and appears once on Weekly Coordination with every affected task retained. A relationship link alone MUST NOT create a Task Waiting/Blocked state or a blocking-effect claim.

  Separately, the panel MUST show the task's current canonical readiness inputs and their readiness state, with exact source links, state-based actors, dates and next actions. A handoff keeps its one direct target (§37). If another task has an explicit open constraint linked to that handoff, show the constraint as that task's readiness input and the handoff as its labelled linked source, retaining both exact records and the relationship provenance. Do not call that task a second direct handoff target or assert that accepting the handoff removes its open constraint. Weekly Coordination may group the actual shared linked source once while keeping the direct-target and via-constraint relationships separately labelled. These read-side facts do not change the task evaluator, readiness rules or handoff commands. Jay confirmed this one-target/readiness-link interpretation on 2026-10-31.

  Independently, whenever the task has a Source Use record, the panel shows its exact source key and revision used, current registered revision and assessment status, including Not Started tasks (FR-CHG-07). Everything is derived on read. *Owner: Discipline Lead. Size M.*

- **FR-SCR-03.** My Work's Today view (§41 E3) MUST open with its focus list first and My Week (§13.10) collapsed to one line, "My Week: n h planned, m h free", with Show. My Week opens expanded on the planning and Overview views. A person's choice to expand it is kept on that device. Today's membership is unchanged. *Owner: Team Member. Size S.*

- **FR-SCR-04.** The task panel, review assignment rows and board review cards MUST name the performer (assignee) and the independent reviewer as two labelled facts. A task that requires review and has no reviewer shows "Reviewer missing" (▲ with text) in its panel and as its row's signal under §41 R7. While `allow_self_review` is off, the reviewer picker leaves out the performer and says why; while it is on, a same-person review is labelled "Self-review (allowed by setting)". A review package shows "Reviewer missing" on each required discipline without a reviewer, and Start round stays disabled with that reason (FR-MRV-01). The server's refusals (FR-MRV-04, AC-MRV-04, FR-SUB-04) are unchanged; this makes them visible before saving. *Owner: Discipline Lead. Size S.*

- **FR-SCR-05.** The Submissions screen and each package MUST lead with blockers. Checks appear in the order Fail, then Pending, then Pass and Not Applicable. Failed and Pending checks are grouped by checking owner (FR-SUB-02) and discipline, and each shows its reason and the exact next action as a link, for example "Verify comment 250101-RC014 closed" or "Accept handoff 250101-H003". The package header shows state, target date, coordinator and "n blockers". Deliverable progress (counts by status) follows the checks and never as a percentage that implies Ready (FR-SUB-02). The revision manifest opens in place, showing each required deliverable's manifest revision beside its current registered revision, with a mismatch marked. Summary-only or partial captures/read responses must label unavailable checklist/manifest/issue details explicitly; never infer Pass, package membership or a per-deliverable failure from package totals. Each check links its recorded source or exact review finding, not an enclosing package as a substitute. *Owner: Project Manager. Size M.*

- **FR-SCR-06.** The Readiness screen (FR-RDY-07) MUST list work that is Not Ready or Needs Assessment first, followed by Proceed under Assumption and then Ready. For each item it shows the missing inputs of FR-RDY-01, grouped by the state-based next actor from FR-SCR-01 (Submitted handoff: receiving owner; Resolution Proposed constraint: affected work owner; open decision: decision owner; applicability: authorised Project Manager or Discipline Lead) and by discipline, each with its reason and exact next action. *Owner: Discipline Lead. Size M.*

### 57.3 Part B: Home

- **FR-SCR-07.** Home (FR-VIS-07) MUST be laid out in this order:
  1. The count-card row (§56.6), overlapping the band: Active Projects, Open Tasks, Overdue Tasks, Due Within n Days (FR-SCR-09), On Track and Team Members.
  2. Tasks by Project as the main chart, two thirds wide, beside Tasks by Status, one third wide (053-D2).
     - Tasks by Project has one horizontal bar per project in lane colours, with swatch, number and name, the total at the right and a shared axis. Its legend carries each lane's total.
     - Tasks by Status is a donut in lane colours with On Hold as its own hatched slice and the total in the centre, beside the status list with counts. Below it a check line, "Open tasks: a + b + c + d on hold = n", equals the Open Tasks card.
  3. This Week's Capacity (FR-SCR-11).
  4. Upcoming Deadlines, with a runway above its list: a "Before today" cell holding the overdue task count, then one column per day of the selected range (up to 14 days). Today is marked, non-working days are faintly hatched, and each due item shows its mark (FR-VSN-08) and key suffix, at most four per day, then "+n".

  Every bar segment, slice, mark and count opens its records (FR-VIS-07). Edit Dashboard still reorders and hides every widget, the new ones included. Below 1180 px the two charts stack. *Owner: Project Manager. Size M.*

- **FR-SCR-08.** Each Home count card MUST name where its count sits, computed in the same read as the figure:
  - **Open Tasks and Overdue Tasks:** "n in <project number>" for the project with the most (ties to the lowest number).
  - **Due Within n Days:** the busiest day ("2 on Sat 31 Oct") and the split by item type.
  - **Active Projects:** "of N projects in scope".
  - **On Track:** "g Green / e evaluated", each part a link, plus "Critical: <numbers>" for Red projects.
  - **Team Members:** "across n disciplines" (distinct home disciplines, with Not set counted once).

  *Owner: Project Manager. Size S.*

- **FR-SCR-09.** Home MUST add a **Due Within n Days** card: open tasks, deliverables and milestones in scope due from today to today plus `home_due_window_days` (organisation setting, default 7), by the Upcoming Deadlines rules, with a meta line splitting it by type. Under its check line, Tasks by Status MUST show "n open tasks have no due date", a link to the task list filtered to open tasks without a due date; it shows when n is 0 too. *Owner: Project Manager. Size S.*

- **FR-SCR-10.** Home count cards MUST show the change since a comparison snapshot.
  - **Snapshot:** each project's daily snapshot (`ProjectHealthSnapshot`, §10.1) adds the counts the cards sum: open tasks, overdue tasks and items due within the window, beside the health and status it already holds.
  - **Comparison:** the latest snapshot at or before today minus `trend_compare_days` (organisation setting, default 7; 053-D8). The title block shows its time as "Trends compare with" (FR-VSN-14).
  - **Display:** a chip reads "↑ n", "↓ n" or "± 0", in percentage points for On Track. It is green when the change is good for the metric (fewer overdue, more on track), red when it is bad, and grey for unchanged values and for the neutral metrics Active Projects, Open Tasks and Due Within n Days.
  - **Missing snapshots:** projects without a snapshot on the comparison day are left out of both sides, and the chip's description says how many.
  - **Same scope:** historical and current values must use identical metric definitions, selected project cohort, status/discipline/people filters, deadline-window basis and current permission scope. Per-project totals alone do not support discipline or supervisor presets. Store sufficient permission-safe dimensions or reconstruct from the activity history; otherwise show "Trend unavailable for this scope". Do not compare a filtered current count with a whole-project historical count. Show both values, the comparison cohort and excluded-project count on inspection; never reveal hidden historic records.
  - **Team Members** shows no trend, because a distinct count cannot be summed from per-project snapshots.

  *Owner: Executive. Size M.*

- **FR-SCR-11.** For viewers with Workload access, Home MUST show **This Week's Capacity** for the people in their planner scope (PLN-14) for the current ISO week. The card states that its scope is people, not Home's projects. It shows:
  - the share of available hours planned: `100 × P / A`, where P is Confirmed plus Expected and A is the planner's effective capacity after availability overrides, time away and holidays; time away is never subtracted twice;
  - a bar of Planned (Confirmed plus Expected, the PLN-09 basis), Time off (PLN-06, hatched) and Free, with Gross capacity beside it;
  - "n people above 100 % this week" (PLN-10), linking to the planner filtered to them;
  - one row per home discipline (FR-GRP-01, with Not set), giving people count, a bar against a 100 % mark and the percentage, with the part beyond 100 % in red.

  Every figure comes from the Weekly Planner's own read (FR-PLN-11), counting the viewer's own drafts as the planner does. Free is the signed `A − P`; Possible is shown separately and never subtracted. Gross is `A + time away` on this planner basis. A zero/unknown denominator shows "Percentage unavailable". A partial view carries the PLN-15 label: the percentage describes visible planned hours only and Free is unavailable or explicitly "at most N h" under FR-FIG-01/02, never a spare-capacity claim. Unknown and excluded people/weeks are disclosed. Viewers without Workload access see no card. *Owner: Supervisor. Size M.*

- **FR-SCR-12.** Home's band MUST show the active scope as chips under the subtitle: Projects (selection and count), Project status, Deadlines (date range) and every other active filter. Each chip opens its control, and "+ Filter" adds one. The chips are the URL's filter state, so they state exactly what the counts include. *Owner: Project Manager. Size S.*

- **FR-SCR-13.** An Upcoming Deadlines row MUST show at most one short note when one applies. An item in review shows "in review n days" (days since it entered Ready for Review or In Review, whichever came first, from the activity log). Otherwise, a milestone or deliverable later than its baseline shows "n days later than baseline" (§50.5, under `slip_display_days`). *Owner: Project Manager. Size S.*

- **FR-SCR-14.** Home MUST offer a preset for each of these roles the viewer holds:
  - **Project Manager:** projects the viewer manages.
  - **Discipline Lead:** projects where the viewer leads a discipline, with task, deliverable and deadline figures narrowed to those disciplines.
  - **Supervisor:** all permitted projects, with task and deadline figures narrowed to work owned by the viewer's people scope (direct reports, or §54 scope when it is on).

  A preset only sets the scope and filters that the chips then show; no definition changes. The choice is kept with the person's preferences (§17.4). No preset applies until one is chosen. *Owner: Project Manager. Size S.*

### 57.4 Part C: Work screens

- **FR-SCR-15.** My Work rows and the workspace task list MUST show the Last activity cell of FR-INT-19 and, on rows with comments, a comment count that opens the panel at Comments, read once per page. *Owner: Team Member. Size S.*

- **FR-SCR-16.** My Work MUST mark "New" (text, not colour alone) on items assigned to the viewer, or on which they became reviewer, after their previous My Work visit. The visit time is stored with their preferences. Opening an item clears its marker, and Mark all seen sets the time to now (053-D20). *Owner: Team Member. Size S.*

- **FR-SCR-17.** When a task list (project Task List, workspace list or My Work) is grouped, each group header MUST show the row count, the overdue count, estimated hours with "n unestimated", progress by the DL-07 rule over the group's tasks (by hours when every task is estimated, otherwise by count, with the basis named) and the next due date, all under the current filters. *Owner: Discipline Lead. Size S.*

- **FR-SCR-18.** The workspace task list MUST offer the project Task List's grouping by Discipline, Deliverable and Milestone, with Project as the outer group when several projects are selected. It MUST also offer the columns Deliverable, Reviewer, Est. hrs, Last activity and Submission date (the target date of the earliest open submission package whose manifest holds the task's deliverable, FR-SUB-01) in its column chooser. *Owner: Project Manager. Size S.*

- **FR-SCR-19.** The workspace task list MUST allow selecting rows across projects and applying the bulk actions the project Task List offers today.
  - Each row is checked with its own project's permissions and row version.
  - The confirmation states how many rows will change and lists the refused rows with reasons.
  - Only the permitted rows change, each with its activity entry.
  - From a departing user's record, the Reassign work tool (FR-ADM-02) can open this list filtered to that person's open work.

  *Owner: Project Manager. Size M.*

- **FR-SCR-20.** After a change made from a list, board or panel (a field edit, status change, reassignment, date shift or bulk action), the confirmation MUST offer **Undo** naming the action ("Undo: reassign 6 tasks to Pat") for 10 seconds, as §13.0's undo toast does (053-D9).
  - Undo applies the reverse change through the same commands and permissions, writing its own activity entries marked "Undo of" the original. It never edits or deletes a log entry.
  - Undo is refused, with the reason, when the item has changed since (G-07) or the reverse is not permitted.
  - Notifications already sent are not recalled.

  *Owner: Team Member. Size M.*

- **FR-SCR-21.** Board cards in Ready for Review or In Review MUST name the reviewer and "n days waiting" since the task entered Ready for Review. From `review_stale_days` the waiting time shows ▲ and amber text. *Owner: Discipline Lead. Size S.*

- **FR-SCR-22.** Board lane headers MUST show the count, estimated hours and overdue count of their cards. Swimlane headers on grouped boards (§36.2) show the swimlane's lead (the Discipline Lead for a discipline, the PM for a project), hours and overdue count. *Owner: Discipline Lead. Size S.*

- **FR-SCR-23.** The workspace and project Timelines MUST support saved views (§18.4) holding filters, grouping, zoom, columns and shown layers, personal or shared with a project, as lists do. *Owner: Project Manager. Size S.*

- **FR-SCR-24.** Calendar (FR-VIS-06) MUST add a **Time away** toggle, person and discipline filters, and saved views.
  - The toggle shows an all-day "Away", or "Away 4 h" for reduced hours (PLN-06), on each affected day for each person the viewer could see in Workload or the Weekly Planner. No reason is shown or stored (§42).
  - The person and discipline filters and the saved views follow §18.4.

  *Owner: Supervisor. Size M.*

- **FR-SCR-25.** On Calendar, a viewer who may change a project deadline's date MUST be able to drag it to another day. The move goes through the Timeline's confirmation and reason rules (FR-INT-11). An **Undated** tray beside the calendar lists open tasks and deliverables in scope without a due date; dropping one on a day sets its due date through the same confirmation. *Owner: Project Manager. Size M.*

- **FR-SCR-26.** The task panel MUST show a strip of section links (Details, Dependencies, Review, Time, Comments, Links, History) that scrolls to each section and marks the one in view. History adds a filter by person. *Owner: Team Member. Size S.*

- **FR-SCR-27.** A collaborator on a task (§12.5) MUST be able to mark **My part done**, with an optional note of up to 200 characters, and withdraw the mark later. The mark shows beside their name with its time and writes one activity entry. It never changes the task's status, assignee or indicators: the assignee stays the one accountable owner and completes the task. This is not a checklist or subtask (§30). *Owner: Team Member. Size M.*

### 57.5 Part D: Projects, Portfolio and project Dashboard

- **FR-SCR-28.** The project list MUST show a **Staffing** cell per project (053-D6):
  - "n open roles", counting the project's Open roles (FR-ROLE-01) and opening its Allocations open roles;
  - "m people over capacity", counting team members with an Over-planned week (PLN-10) in the next `staffing_lookahead_weeks` (organisation setting, default 4) and opening the planner filtered to them.

  People outside the viewer's planner scope are not counted, and the PLN-15 label says so. Open roles never enter a person's figures (FR-ROLE-04). *Owner: Project Manager. Size M.*

- **FR-SCR-29.** The project list MUST offer filters for Project Manager, Client, Phase, Project type, Priority and, with `flag.org_structure` on, Lead unit (FR-UNT-14). It MUST offer grouping by Project Manager, Client or Office with per-group counts, under §41 R1's Filters and Group controls. *Owner: Project Manager. Size S.*

- **FR-SCR-30.** Each project row MUST open a **peek** without leaving the list, showing the next three open milestones with date and status, each discipline's lead with that discipline's overdue task count, and the status note (FR-PRO-16) with its author and time. It is read on opening and filtered by permission. *Owner: Project Manager. Size M.*

- **FR-SCR-31.** Each project row MUST show the project's span from start to target completion as a thin bar with a today marker, and "n milestones left" (open milestones). A project without a target shows "No target". *Owner: Project Manager. Size S.*

- **FR-SCR-32.** The project list and Portfolio row detail MUST show two separate plain facts (053-D7). "Elapsed n %" is days from start to today over days from start to target. "Progress m %" is the project's progress (§36.2). There is no ratio, index or colour between them, and neither feeds health (§30 earned value stays excluded). *Owner: Project Manager. Size S.*

- **FR-SCR-33.** Project list rows MUST be selectable for a **bulk set** of Priority, Phase or, with `flag.org_structure` on, Lead unit, through the project edit command. Each project is checked for the viewer's edit right and row version, refused rows are listed with reasons, and each change writes one activity entry. Status is not bulk-set. *Owner: Project Manager. Size M.*

- **FR-SCR-34.** Each Portfolio row MUST show the FR-SCR-28 staffing figures as text (053-D6). The "Capacity by discipline" link (FR-INT-24) stays. *Owner: Executive. Size M.*

- **FR-SCR-35.** Portfolio MUST show one line above its table: "Moved in the last n days: a projects changed health, b milestones slipped, c projects changed status". The window is `trend_compare_days`; the figures come from daily snapshots and the activity log, and each part opens the list of those projects. *Owner: Executive. Size S.*

- **FR-SCR-36.** Portfolio MUST print for the weekly review: landscape, the band with its title block and read time, the tiles, then the grouped table with status notes, repeated table headers and no navigation. *Owner: Executive. Size S.*

- **FR-SCR-37.** The project Dashboard MUST show **Staffing until the next submission** (053-D6). The window runs from today to the target date of the next open submission package, or to the next milestone when there is none. For each discipline it shows three figures side by side, never summed:
  - remaining estimate hours of open tasks due in the window;
  - confirmed allocated hours (the FR-CAP-04 basis);
  - open-role hours (the FR-ROLE-04 spread).

  When the remaining hours exceed allocated hours, it adds "Remaining exceeds allocated by n h". *Owner: Project Manager. Size M.*

- **FR-SCR-38.** The project Dashboard's attention section (§13.1) MUST group items by kind with a chip per kind and its count, show the first five of the chosen kind, and offer "Show all (n)". *Owner: Project Manager. Size S.*

### 57.6 Part E: Reports, planning and time

- **FR-SCR-39.** Each task, workload or capacity report MUST show beside its table "n unestimated tasks" and "m open tasks with no due date" within its parameters, each linking to the list. This reuses Workload's unscheduled-estimates rule (§13.11). *Owner: Project Manager. Size S.*

- **FR-SCR-40.** A report's parameters MUST be savable as a named view (§18.4), personal or shared with a project. Copy link copies a URL carrying the parameters, which reopens the report under the recipient's own permissions. *Owner: Project Manager. Size S.*

- **FR-SCR-41.** A report MAY show one chart drawn from its own rows, after its counts row and beside its table, which always stays (053-D2). The chart groups by discipline or by project, never by person. Where the report has supply and demand, it adds a deficit bar "short by n h". The chart and table come from one read, and the export is unchanged. *Owner: Supervisor. Size M.*

- **FR-SCR-42.** The report catalogue (§13.18) MUST list each report under the question it answers (for example "Who has room in the coming weeks?" or "What is late on my projects?") and offer search over titles, questions and descriptions. The catalogue stays beside an open report. *Owner: Project Manager. Size S.*

- **FR-SCR-43.** Reports with week columns MUST offer month grouping and a unit switch, hours (default) or FTE (053-D22). The proposed FTE basis is standard full-time hours: for a cell spanning k ISO weeks, `FTE = sum(exact visible hours) / (k × default_weekly_capacity_hours)`. Use the same denominator for every person and aggregate so row and column totals reconcile; do not divide by an individual's reduced/custom availability. Capacity utilisation is a separate percentage using the planner's effective capacity. Each ISO week belongs to the month of its Thursday, and a partial month uses only its displayed weeks. Recompute from unrounded hours; never sum weekly FTE to label a month. A zero/unknown standard denominator shows "FTE unavailable"; unknown hours stay unknown and partial totals retain their coverage label. Exports state the standard hours, week count, scope and missing inputs. Each report keeps its own horizon and Workload keeps 8 weeks (051-D9). *Owner: Supervisor. Size S.*

- **FR-SCR-44.** The Weekly Planner MUST offer **Find room**. Its inputs are hours per week (N), number of weeks (W, within `planning_horizon_weeks`), a start week and an optional discipline. It lists, by name, only people in the viewer's planner scope whose remaining capacity (PLN-09) is verified at least N h in every one of the W weeks, with their free hours per week. People whose partial view cannot verify that condition are excluded from matches or shown separately as Unavailable, never presented as spare room. It never orders people by free hours. *Owner: Supervisor. Size M.*

- **FR-SCR-45.** Each Weekly Planner group (FR-GRP-02) MUST show a summary row with a small stacked bar per week of Confirmed, Expected and Possible hours against a capacity line, from the group's subtotals, with the values in its accessible name (053-D2). The numeric subtotal row stays. Planner and project Allocations week headers also show milestone and deliverable deadline markers from visible linked projects, by due week, with type, exact date and count; undated records remain in a separate tray. A marker opens the exact record or its same-scope dated list. This adds the week-header placement to §13.11's existing staffing/deadline context rather than changing its calculation. *Owner: Supervisor. Size M.*

- **FR-SCR-46.** The Weekly Planner MUST offer cells in hours (the default) or as a percentage of capacity, and a Months zoom over the planning horizon. In the Months zoom each ISO week belongs to the month of its Thursday. There is no day zoom. The choices are kept in the URL. *Owner: Supervisor. Size M.*

- **FR-SCR-47.** Workload MUST add, per home discipline and week, three figures side by side (053-D6): free hours of the discipline's people, unfilled-role hours of that discipline (FR-ROLE-04), and estimate hours of unassigned open tasks of that discipline due that week (FR-PRO-15). When unfilled-role hours plus unassigned-task hours exceed free hours, it adds "short by n h". None of these figures enters a person's row. *Owner: Supervisor. Size M.*

- **FR-SCR-48.** Workload MUST offer a view-only switch, **Show proposed as confirmed**, that recomputes the cells counting Proposed allocations as confirmed (FR-CAP-04). While it is on, the band and every changed cell say "Includes proposed". Nothing is saved, and an export made in this state says so in its Parameters sheet. *Owner: Supervisor. Size S.*

- **FR-SCR-49.** Optional person skills MUST accept certification tags (053-D21) with an optional expiry date (for example P.Eng, EIT, Confined space). As a proposed fixed-fact permission under 053-D21, the active person (unless Read Only) or a System Administrator may edit them; administrative corrections follow the existing reason and activity rules. This does not assume the deferred Person Profile screen exists. My Staff cards and the Team list show them as chips. From `certification_warn_days` (organisation setting, default 30) before expiry a chip reads "Expires in n days", and after expiry "Expired". The Team list filters by tag. Tags are shown per person only, with no count, comparison or ranking between people. *Owner: Supervisor. Size M.*

- **FR-SCR-50.** A System Administrator MAY set a person's type (Employee, Co-op, Contractor; 053-D11) and optional start and end dates. My Staff, Team and the person pickers show "Starts <date>" before the start date and "Ends <date>" within `availability_window_days` (organisation setting, default 60) of the end date. These are display only and change no capacity or planning rule. *Owner: System Administrator. Size S.*

- **FR-SCR-51.** The open role Fill dialog and the allocation Propose dialog MUST show, for the chosen person, available hours per week across the dates and the effect of the proposal on that person's load as before and after per week, with over-capacity weeks marked. Where §52.4 is on, they use its allocation preview read. People outside the viewer's planner scope show nothing. FR-CAP-07's confirmation is unchanged. *Owner: Discipline Lead. Size M.*

- **FR-SCR-52.** Allocations MUST show, per discipline and week, allocated hours (confirmed and proposed separately) beside the remaining estimate hours of that discipline's open tasks due that week (the FR-RES-11 context column). The figures are never summed. *Owner: Project Manager. Size M.*

- **FR-SCR-53.** Allocations MUST offer **Shift all** for a selection or the whole project, by plus or minus N weeks.
  - A preview lists each allocation's old and new dates and the people whose load changes.
  - Confirmed allocations that move return to Proposed and go back through confirmation (FR-CAP-07).
  - Each moved allocation writes its activity entry.

  *Owner: Project Manager. Size M.*

- **FR-SCR-54.** Time's week MUST show the person's own time away (PLN-06), hatched on its days, with the week's reduced capacity ("Capacity 32 h this week"). It is information only, and entries on those days stay allowed. *Owner: Team Member. Size S.*

- **FR-SCR-55.** Time's week MUST offer rows prefilled with the person's assigned open tasks and the tasks they logged the previous week, with a cell per day.
  - Typing hours in a cell creates or changes the person's own entry under FR-VIS-10's rules.
  - No time is created unless the person types it.
  - There is no timer, no reminder sent about other people, and no submit, lock or approval.

  *Owner: Team Member. Size M.*

### 57.7 Part F: Registers and delivery records

- **FR-SCR-56.** Each deliverable issue (§12.4 issue history) MUST record a **purpose of issue** from an organisation list (053-D12). When the recipient responds, it records a **return code** from a second organisation list, with the response date. Both show on the register row and in the issue history, and both are filters. Package purpose (FR-SUB-01) is unchanged. *Owner: Discipline Lead. Size S.*

- **FR-SCR-57.** The Deliverables register MUST show a **No due date** tray, with its count and list, beside its date view, and a matching "No due date" filter. *Owner: Project Manager. Size S.*

- **FR-SCR-58.** A deliverable MAY carry a **client drawing number**. When the project declares a numbering pattern (053-D17), a number that does not match is flagged "Does not match <pattern>" and is never refused. *Owner: Discipline Lead. Size S.*

- **FR-SCR-59.** A Project Manager or Discipline Lead MAY import deliverables from a drawing list in CSV or XLSX.
  - The import checks every row first and previews creates and errors.
  - It commits all rows or none.
  - It never updates or deletes an existing deliverable.
  - It follows the pattern of the project import (FR-PRO-20, FR-PRO-21) and writes one activity entry per created deliverable.

  *Owner: Project Manager. Size M.*

- **FR-SCR-60.** The Deliverables register MUST offer a **client export** with number, client drawing number, title, revision, purpose of issue, issue date, return code and status. It leaves out hours, internal reviewers and internal notes, and names no person other than the PM. *Owner: Project Manager. Size S.*

- **FR-SCR-61.** Reviews MUST add three things:
  - a list of open review comments across all of the project's packages (FR-MRV-03), with discipline, severity, originator and resolution owner filters;
  - a **response outcome** on each comment's response: Incorporated, Not incorporated (reason required), Clarified or Noted;
  - a response-sheet export per package listing comment, originator, discipline, severity, response and outcome.

  *Owner: Discipline Lead. Size M.*

- **FR-SCR-62.** A project member MAY record an **external review** of an issued revision (053-D16), but the recorder is not thereby its verifier. The PM designates an independent permitted verifier with a reason, applying FR-MRV-03/04 and the self-review rules.
  - The originator is an `ExternalParty` (§10.1), with received date and return code.
  - Its comments are review comments whose originator is that party; the Project Manager-designated independent permitted verifier verifies closure on the party's behalf with an evidence link.
  - No external account is created (constitution VI).
  - An issued revision without a captured technical author list is unavailable for external review. A new issue must capture its actual authors; current ownership or the issuing actor cannot substitute for unknown historical authors. There is no PM attestation workflow for older issues (Jay, 2026-11-02).

  *Owner: Project Manager. Size M.*

- **FR-SCR-63.** Reviews MUST show, per discipline over the project's approved packages, the rounds to approval (median and maximum) and the median days per round. Figures are at discipline level only, never per reviewer. *Owner: Discipline Lead. Size S.*

- **FR-SCR-64.** A System Administrator MAY set default required review disciplines on a deliverable type. A new review package whose deliverables are of that type preselects them, and the coordinator may change them (FR-MRV-01). *Owner: System Administrator. Size S.*

- **FR-SCR-65.** Every milestone or deliverable date change MUST record a **reason category** from an organisation list (053-D13) beside the free-text reason, which stays. The Milestone View shows "Why dates moved": counts by category over the project's history, each opening the activity list filtered to them. *Owner: Project Manager. Size S.*

- **FR-SCR-66.** The Milestone View MUST flag "Out of sequence" on a milestone dated before a milestone whose type comes earlier in the `milestone_type` order (§12.3, Other excluded), naming that milestone. A milestone with no date is flagged "No date". Both flags are information only. *Owner: Project Manager. Size S.*

- **FR-SCR-67.** On Weekly Coordination and in the handoff panel, a handoff that is overdue or whose promised date is after its needed-by date (FR-HND-01) MUST show "Readiness-linked open tasks: n; first recorded due <date>". The full-project count is distinct nonterminal tasks from its one direct Task target and explicit Open or Resolution Proposed Task constraints linked to that handoff. Keep **Direct target** and **Via constraint** relationships separately labelled; never infer downstream tasks or deliverable children. Known zero, undated work and unavailable sources remain distinct. Accepted or Incorporated satisfies the direct receipt; a separately open constraint remains its own unresolved work and is not evidence that the handoff still blocks it. This applies the recorded one-target clarification without changing any handoff or readiness command. *Owner: Discipline Lead. Size S.*

- **FR-SCR-68.** Weekly Coordination MUST show, for the meeting's window, open constraints (FR-RDY-03) with removal owner and needed-by date, and Proposed commitments awaiting their performer (FR-RDY-05), each linking to Readiness. *Owner: Project Manager. Size S.*

- **FR-SCR-69.** A decision MAY carry a category from an organisation list (053-D15), up to five options considered and, when it is decided, a rationale. The Decision Register shows **decision lag by owner organisation**: median days from requested to decided for each owner organisation (internal, or each `ExternalParty`), at organisation level only. *Owner: Project Manager. Size S.*

- **FR-SCR-70.** A project template MAY include risks (title, probability, impact, mitigation, trigger). Creating a project from the template copies them as Open risks owned by the PM, under §12.14's snapshot rules. *Owner: Project Manager. Size S.*

- **FR-SCR-71.** Resolving an issue MUST record a **root-cause category** from an organisation list (053-D14). The Issue Register shows counts by cause over resolved issues, each opening its list. *Owner: Project Manager. Size S.*

### 57.8 Part G: People, files and settings

- **FR-SCR-72.** The workspace Team list and the project Team & Disciplines tab MUST offer the columns home discipline (FR-GRP-01), office and supervisor, with grouping and filtering by each, and by unit when `flag.org_structure` is on (FR-UNT-13). *Owner: Supervisor. Size S.*

- **FR-SCR-73.** The project Team & Disciplines tab MUST show an **Unfilled roles** strip: each Open role with label, discipline, dates and hours, and a Fill link where the viewer may fill it (053-D6). Open roles never enter a person's figures (FR-ROLE-04). *Owner: Project Manager. Size S.*

- **FR-SCR-74.** Selecting a person's name MUST open a read-only **person panel** showing:
  - name, job title, home discipline, office, supervisor and, when on, unit;
  - skills and certifications (FR-SCR-49);
  - type and dates (FR-SCR-50);
  - the projects and project roles the viewer can see;
  - this week's free and away hours (FR-PICK-01 read), only for viewers with Workload access who have the person in their planner scope.

  This narrow read-only panel and the optional person facts are proposed under 053-D21 against §39.12's deferred People/Person Profile; they do not adopt that full screen. It shows only permitted directory/project-member facts, no private allocations, performance data, utilisation history or comparison between people. *Owner: Supervisor. Size M.*

- **FR-SCR-75.** The project Team tab's Add a person picker MUST show "N h free" and "N h away" for the current week, as FR-PICK-02 does for the task owner picker. *Owner: Project Manager. Size S.*

- **FR-SCR-76.** A project member MAY carry a free-text **responsibility note** of up to 80 characters (for example "checker" or "storm lead"), set by the PM or that member's Discipline Lead and shown on the Team tab and the person panel. It grants no permission and is not a project role (§8.3). *Owner: Project Manager. Size S.*

- **FR-SCR-77.** Files (FR-VIS-09) MUST add three things:
  - "Deliverables with no link": open deliverables in scope that have no document link;
  - a flag on any link whose declared revision differs from the deliverable's current registered revision ("Declared C, current D", the FR-MDC-05 metadata);
  - filters by discipline, by the person who added the link, and "Added this week".

  *Owner: Discipline Lead. Size S.*

- **FR-SCR-78.** The notification centre (§13.17) MUST add three things:
  - a **Needs your action** filter, for notifications whose item's next actor (FR-SCR-01) is the viewer;
  - a follow-level menu (All activity, My items only, Muted; §17.4) on each row's project chip;
  - a delivery line per row ("Emailed 07:00", "Held: away", "Not sent: muted").

  *Owner: Team Member. Size S.*

- **FR-SCR-79.** The search results page (§18.1) MUST offer filters on each type tab (project, status, and discipline where the type has one) and find people by job title and home discipline. It shows the person's last ten searches, kept in this browser only, with Clear. *Owner: Team Member. Size S.*

- **FR-SCR-80.** Preferences (§17.4) MUST offer every user three things:
  - a digest frequency of Daily or Weekly, the weekly digest sent on Monday;
  - **Hold emails while away**: on days with no available hours, as §42 defines them, immediate emails are held and listed in the next digest. There is no redirect to a deputy (053-D10).
  - an opt-in reminder, to oneself only, to enter time at a chosen weekday and time.

  *Owner: Team Member. Size S.*

- **FR-SCR-81.** Templates MUST show, for each version, the projects created from it, and a project shows "Created from <template> v3; v5 exists". This is information only; nothing changes the project (§12.14 snapshot rules). A draft version shows its changes against the version it was copied from: items added, removed and changed. *Owner: System Administrator. Size S.*

- **FR-SCR-82.** Admin's user list MUST add the columns home discipline, weekly capacity and template editor, and, for supervisors, the number of people in their people scope (FR-UNT-08 when on, otherwise direct reports). Deactivating a user who owns open items MUST show the count and a link to Reassign work (FR-ADM-02) before confirming; it does not block. *Owner: System Administrator. Size S.*

- **FR-SCR-83.** Admin MUST show a plain-language explainer for each system and project role, generated from the same §8 permission matrix the server enforces, so the text cannot drift from the rules. *Owner: System Administrator. Size S.*

### 57.9 Shipping dark

- **FR-SCR-84.** Each part MUST merge dark behind its own flag (§57.1) under §43's flag rules (FR-DLV-07 to FR-DLV-10), and Jay turns each one on. With a flag off, every screen, response, report and export of that part is as it was before this amendment, and the new organisation settings (`home_due_window_days`, `trend_compare_days`, `staffing_lookahead_weeks`, `certification_warn_days`, `availability_window_days`) are not offered. A part whose screens read §56's tokens works with `flag.visual_system` off. No existing feature is put behind a flag. *Owner: Project Manager. Size S.*

### 57.10 Acceptance criteria

Each AC-SCR-nn verifies FR-SCR-nn.

**Part A**

- **AC-SCR-01.** Given task 250101-T0013 In Review with reviewer Diane, when Alex opens it, then the panel shows owner Alex, next actor Diane, needed by its due date and next action Review; when Diane opens it, next actor reads "You". A link to a review finding opens that exact finding, never its package; closing restores the source view. A recipient without access gets the normal non-disclosing unavailable result. Missing/inactive actors are labelled. Milestones, Draft handoffs, change assessments and Ready submissions follow the canonical table, and a blocked task with two inputs shows deterministic primary input plus both links.
- **AC-SCR-02.** Given Submitted handoff 250101-H003 directly targets T0005 and T0010 has an Open constraint explicitly linked to H003, normal commands and current reads show H003 as T0005's readiness input with receiving owner/needed-by/action, and the exact constraint as T0010's readiness input with its removal owner plus a separately labelled H003 link. Weekly Coordination shows the shared H003 source once with both relationship roles and exact task links. Accepting H003 changes T0005's readiness through the existing rules; T0010's Open constraint remains until its own canonical resolution. Neither relationship fabricates a Task Waiting/Blocked row or second handoff target. Given two open tasks share a materialized predecessor/decision blocker through normal commands, each shows "Also holds up 1 task" and Weekly Coordination shows that source once with both tasks. Given T0002 is Not Started with Source Use revision 2025A while the registered revision is 2026B, it shows 2025A → 2026B with assessment state even though it is not blocked.
- **AC-SCR-03.** Given Alex opens My Work, then Today shows the focus list first and My Week as one line; Show expands it, and after a reload on the same device it stays expanded.
- **AC-SCR-04.** Given a review-required task with no reviewer and `allow_self_review` off, then its row and panel show "Reviewer missing", the reviewer picker leaves out the assignee and says why, and a package with a required discipline lacking a reviewer keeps Start round disabled with that reason.
- **AC-SCR-05.** Given a package with one Fail, two Pending and five Pass checks, then the Fail check is first, the two Pending checks follow grouped by checking owner, each shows its next action as a link, the header reads "3 blockers", and no percentage is shown. A check linked to D001 does not attach its failure to unrelated D002; absent manifest, passing-check and issue-history detail remains unavailable rather than fabricated.
- **AC-SCR-06.** Given three tasks, one Ready, one Not Ready waiting on a decision and one Needs Assessment, then Readiness lists Not Ready and Needs Assessment first, the decision appears under its owner with "Decide", and the Ready task is last. A Submitted handoff is grouped under its receiving owner; a Resolution Proposed constraint is grouped under its affected work owner, with the removal owner retained as a separate fact.

**Part B**

- **AC-SCR-07.** Given Home for five projects, then the six count cards come first, Tasks by Project (two thirds) sits beside the Tasks by Status donut (one third) whose check line equals Open Tasks, the runway shows a "Before today" cell and 14 day columns with today marked, and Edit Dashboard can hide This Week's Capacity.
- **AC-SCR-08.** Given 13 of 32 open tasks are in 250101, then the Open Tasks card reads "13 in 250101", and On Track reads "3 Green / 4 evaluated" with "Critical: 250101".
- **AC-SCR-09.** Given six items due in the next 7 days (two on 2026-11-06) and three open tasks without a due date, then Due Within 7 Days shows 6 with "2 on Fri 6 Nov" and its split by type, and the no-due-date link shows 3 and opens exactly those tasks.
- **AC-SCR-10.** Given the comparison snapshot from 2026-10-24 counted 3 overdue tasks and today counts 1, then the Overdue Tasks chip reads "↓ 2" in green, the title block reads "Trends compare with 2026-10-24" and its time, and Team Members shows no trend. A discipline-only preset compares only the matching historical discipline cohort; if the stored snapshot cannot reproduce it, the chip says "Trend unavailable for this scope", rather than comparing the whole-project count. Changed project membership/permissions use the disclosed common visible cohort or unavailable state.
- **AC-SCR-11.** Given Lena with Workload access and 648 h gross, 36 h time off and 529 h planned this week, then A is 612 h, the percentage is 529/612 = 86 % rounded, and Free is 83 h, not a second subtraction of time off. A partial view never claims that 83 h is verified spare capacity; zero/unknown available hours show unavailable. A user without Workload access sees no card.
- **AC-SCR-12.** Given Home filtered to Active projects and a date range, then the chips show Projects, Project status and Deadlines with those values, and removing a chip changes the URL and the counts together.
- **AC-SCR-13.** Given deliverable 250101-D001 in review for 5 days and milestone 250101-M02 3 days later than baseline, then their rows show "in review 5 days" and "3 days later than baseline", and no row shows two notes.
- **AC-SCR-14.** Given Priya is a Project Manager and Discipline Lead but not a Supervisor, then Home offers only the Project Manager and Discipline Lead presets, and choosing Project Manager sets the Projects chip to the projects she manages, keeps every count definition and is restored on her next visit.

**Part C**

- **AC-SCR-15.** Given a task last changed by Pat 2 hours ago with three comments, then its My Work row reads "2 h ago · Pat" and a comment count of 3 that opens the panel at Comments.
- **AC-SCR-16.** Given a task was assigned to Alex after his last My Work visit, then it shows "New" until he opens it, and Mark all seen clears every marker.
- **AC-SCR-17.** Given a discipline group of four tasks, every one estimated (10, 10, 15 and 5 h) with the first two Complete and one overdue, then its header shows 4 tasks, 1 overdue, 40 h, progress 50 % by hours, and the next due date.
- **AC-SCR-18.** Given the workspace list over three projects grouped by Deliverable, then rows group by project then deliverable, and the column chooser offers Deliverable, Reviewer, Est. hrs, Last activity and Submission date.
- **AC-SCR-19.** Given Marc selects eight tasks on three projects and reassigns them, while he may edit only six, then the confirmation lists two refused rows with reasons, six change, and six activity entries exist.
- **AC-SCR-20.** Given Pat reassigns six tasks and chooses Undo within 10 seconds, then the six return to their previous assignees with six entries marked "Undo of"; if one task changed in between, that row is refused with the reason and the others are undone.
- **AC-SCR-21.** Given a card entered Ready for Review 6 days ago with `review_stale_days` 5, then it names its reviewer and shows "▲ 6 days waiting".
- **AC-SCR-22.** Given an In Progress lane with five cards (12 h estimated, one overdue), then its header shows 5, 12 h and 1 overdue; a discipline swimlane names its Discipline Lead.
- **AC-SCR-23.** Given Priya saves a project Timeline view with a discipline filter, Months zoom and dependencies shown and shares it, then Marc opens it with the same filter, zoom and layers.
- **AC-SCR-24.** Given Alex has a full day away on 2026-11-04 and Lena may see him in the planner, then with Time away on, the Calendar shows "Away" on that day for Alex with no reason, and a user without that visibility sees nothing.
- **AC-SCR-25.** Given task 260105-T0011 due 2026-11-02, when Priya drags it to 2026-11-05, then the Timeline confirmation lists any successors made inconsistent and asks for the reason; dropping an undated task from the tray onto a day sets its due date after the same confirmation.
- **AC-SCR-26.** Given a long task panel, when Comments is chosen in the strip, then the panel scrolls there and marks Comments; filtering History to Pat shows only Pat's entries.
- **AC-SCR-27.** Given Jill is a collaborator on Alex's task, when she marks "My part done", then her mark and time show beside her name, one activity entry is written, and the task's status and assignee are unchanged.

**Part D**

- **AC-SCR-28.** Given 250101 has two Open roles and one team member Over-planned in week 46, then its Staffing cell reads "2 open roles · 1 over capacity", each part opens its list, and no person's figures change.
- **AC-SCR-29.** Given the project list grouped by Client and filtered to Priority High, then each group shows its count, and the URL keeps both settings.
- **AC-SCR-30.** Given 250101 has a status note, when its row peek opens, then it shows the next three open milestones, each discipline lead with an overdue count, and the note with author and time.
- **AC-SCR-31.** Given a project from 2026-01-05 to 2027-02-28 with four open milestones, then its row shows the span bar with today marked and "4 milestones left"; a project without a target shows "No target".
- **AC-SCR-32.** Given a project 60 % through its dates with 13 of 26 non-cancelled tasks Complete, then it shows "Elapsed 60 %" and "Progress 50 %" with no third figure, colour or index, and health is unchanged.
- **AC-SCR-33.** Given Marc selects three projects and sets Priority High while he may edit two, then two change with one entry each and the third is listed as refused.
- **AC-SCR-34.** Given the Portfolio, then each row shows its open roles and over-capacity people as text, and the Capacity by discipline link still opens the planner grouped by home discipline.
- **AC-SCR-35.** Given one project went from Yellow to Red and two milestones slipped in the last 7 days, then the line reads "1 project changed health, 2 milestones slipped", and each part opens those projects.
- **AC-SCR-36.** Given Portfolio is printed, then the output is landscape with the title block's read time, tiles, grouped rows, status notes and repeated table headers, and no navigation.
- **AC-SCR-37.** Given Civil has 120 h of remaining estimates due before the next submission, 80 h confirmed allocated and 24 h of open roles, then the three figures show side by side with "Remaining exceeds allocated by 40 h" and no sum.
- **AC-SCR-38.** Given 12 attention items of four kinds, then four chips show their counts, the chosen kind lists five items, and "Show all (n)" lists the rest.

**Part E**

- **AC-SCR-39.** Given the Workload by Person report for Civil with four unestimated tasks and two without a due date, then the report shows "4 unestimated tasks" and "2 open tasks with no due date", each opening its list.
- **AC-SCR-40.** Given Lena saves Team Capacity for Civil over 12 weeks and copies its link, when Sam opens the link, then the report opens with those parameters under Sam's permissions.
- **AC-SCR-41.** Given a capacity report with a chart grouped by discipline, then every bar equals its table row, a deficit bar shows "short by n h" where demand exceeds supply, grouping by person is not offered, and the export is unchanged.
- **AC-SCR-42.** Given the catalogue, when "room" is searched, then the reports under "Who has room in the coming weeks?" are listed, and opening one keeps the catalogue beside it.
- **AC-SCR-43.** Given four displayed ISO weeks and standard weekly hours 40, one person with 160 visible hours shows 1.0 FTE and another with 120 h shows 0.75 FTE, even when the second person's custom capacity is 32 h; the aggregate is 1.75 FTE. A two-week partial month containing 80 h is 1.0 FTE. Zero/unknown standard hours show unavailable; partial/missing inputs remain labelled, the export states denominator and week count, and Workload keeps 8 weeks.

- **AC-SCR-44.** Given Find room with 8 h per week for 4 weeks in Civil, then only Civil people with verified at least 8 h free in every week are listed by name; a partial-view person whose hidden work prevents verification is excluded or shown separately as Unavailable, never counted as a match.
- **AC-SCR-45.** Given a planner group of three people, then its summary row draws Confirmed, Expected and Possible per week against capacity, each bar's accessible name states its values, and the numeric subtotal row is still present. In Planner and Allocations, one milestone and two deliverables due in week 46 appear as dated type markers in that week header; clicking them opens exactly those permitted records, and an undated deliverable remains in the separate tray.
- **AC-SCR-46.** Given the planner in % mode and Months zoom, then a cell with 32 h planned against 40 h capacity reads 80 %, a week from Monday 2026-11-30 to Sunday 2026-12-06 counts in December, and the URL keeps both choices.
- **AC-SCR-47.** Given Civil with 40 h free, a 30 h unfilled role and 20 h of unassigned Civil tasks due in week 46, then Workload shows the three figures and "short by 10 h", and no person's row changes.
- **AC-SCR-48.** Given a Proposed 16 h allocation for Alex in week 46, when Show proposed as confirmed is on, then his week includes it with "Includes proposed"; turning it off restores the figure, and nothing is saved.
- **AC-SCR-49.** Given Omar's Confined space certification expires in 20 days, then his My Staff card and Team row show "Expires in 20 days", the Team list filters by that tag, and no count by person appears.
- **AC-SCR-50.** Given an Admin sets Jill as Co-op ending 2026-12-18, then within 60 days of that date My Staff and the pickers show "Ends 2026-12-18", and her capacity is unchanged.
- **AC-SCR-51.** Given Lena fills an open role from 2026-11-09 to 2026-11-27 with Alex, then the dialog shows Alex's available hours for each of the three weeks and his load before and after, with any over-capacity week marked.
- **AC-SCR-52.** Given Civil with 60 h confirmed, 16 h proposed and 90 h of remaining estimates due in week 47, then Allocations shows 60, 16 and 90 h separately and no total of them.
- **AC-SCR-53.** Given three allocations, one Confirmed, when Shift all +2 weeks is applied after the preview, then all three move, the Confirmed one becomes Proposed and awaits confirmation, and three activity entries exist.
- **AC-SCR-54.** Given Alex is away 8 h on Wednesday, then his Time week shows Wednesday hatched and "Capacity 32 h this week", and he can still add an entry on Wednesday.
- **AC-SCR-55.** Given Alex logged on two tasks last week and has three assigned, then his week offers those rows; typing 2 in Tuesday's cell creates one entry of 2 h owned by Alex, and no other entry exists.

**Part F**

- **AC-SCR-56.** Given deliverable 250101-D001 is issued For review and later returned with code B, then the register row and the issue history show both, and filtering by return code B lists it.
- **AC-SCR-57.** Given four open deliverables without a due date, then the tray shows 4 and lists them, and the No due date filter returns the same four.
- **AC-SCR-58.** Given the project pattern `C-###` and a client number "C12", then the deliverable saves with "Does not match C-###".
- **AC-SCR-59.** Given a drawing list of 40 rows with one missing title, then the preview lists the error and nothing is created; the corrected file creates 40 deliverables with 40 entries.
- **AC-SCR-60.** Given a client export of the register, then it holds the listed columns and no hours, internal reviewer or internal note, and names no person other than the PM.
- **AC-SCR-61.** Given open comments on two packages, then the cross-package list shows both with filters; recording "Not incorporated" without a reason is refused; and the response sheet lists each comment with its outcome.
- **AC-SCR-62.** Given an issued revision, when Priya records a client review with code C and two comments from that client, then both comments carry the client as originator, a separately designated independent verifier records closure with evidence and reason, and no account is created for the client.
- **AC-SCR-63.** Given Civil packages approved after 1, 2 and 3 rounds, then Civil shows a median of 2 and a maximum of 3 rounds, with no figure per reviewer.
- **AC-SCR-64.** Given the Drawing Package type defaults to Civil and Geotechnical, when a review package is created for two drawing packages, then both disciplines are preselected and can be changed.
- **AC-SCR-65.** Given a milestone date moved with category "Waiting on input" and a reason, then the activity entry holds both, and "Why dates moved" counts it under that category.
- **AC-SCR-66.** Given an IFC milestone dated before the Design Submission milestone, then the IFC row shows "Out of sequence: before <Design Submission milestone>"; a milestone without a date shows "No date".
- **AC-SCR-67.** Given a handoff promised three days after its needed-by date, one direct Task target and a second open Task whose explicit Open constraint links to that handoff, with the first recorded Task due 2026-11-10, then its card reads "Readiness-linked open tasks: 2; first recorded due 2026-11-10". Its drilldown labels Direct target and Via constraint, counts a task once even when it has both relationships, and agrees with the full-project summary. Acceptance satisfies the direct receipt without removing or relabelling the independent constraint as satisfied; unavailable source data is not shown as zero.
- **AC-SCR-68.** Given two open constraints and one Proposed commitment in the meeting's window, then Weekly Coordination lists them with their owners and dates and links to Readiness.
- **AC-SCR-69.** Given decisions owned by the client decided after 4, 10 and 12 days, then the register shows a median lag of 10 days for the client, with no figure per person.
- **AC-SCR-70.** Given a template with two risks, when a project is created from it, then the project has two Open risks owned by its PM, and later template edits do not change them.
- **AC-SCR-71.** Given an issue is resolved without a root-cause category, then the save is refused with the reason; with "Coordination gap", the register's count by cause includes it.

**Part G**

- **AC-SCR-72.** Given the Team list grouped by home discipline and filtered to the Halifax office, then each group lists only Halifax people, with Not set as its own group.
- **AC-SCR-73.** Given 250101 has two Open roles, then the Team tab's strip lists both with a Fill link for Lena, and no member's figures change.
- **AC-SCR-74.** Given Lena selects Alex's name, then the person panel shows his facts and this week's free and away hours; Rita (Read Only, without Workload access) sees the panel without hours.
- **AC-SCR-75.** Given Priya adds a person to 250101, then the picker shows "N h free" and "N h away" for people in her planner scope and nothing for others.
- **AC-SCR-76.** Given Priya sets Jill's note to "checker", then the Team tab and the person panel show it, and Jill gains no permission.
- **AC-SCR-77.** Given a link declares revision C while the deliverable's registered revision is D, then Files flags "Declared C, current D"; "Deliverables with no link" lists open deliverables without a link.
- **AC-SCR-78.** Given Alex has five notifications, two on items where he is next actor, then Needs your action shows two; setting a project to Muted from its chip changes his follow level, and a held email shows "Held: away".
- **AC-SCR-79.** Given Alex searches three times, then the three searches show as recent searches on that device only, and Clear removes them; searching people by "EIT" finds people with that job title.
- **AC-SCR-80.** Given Jill chooses Weekly and Hold emails while away, then on her day with no available hours an assignment email is held and listed in her next digest, and no one else receives it.
- **AC-SCR-81.** Given 250101 was created from template v3 and v5 exists, then its settings read "Created from <template> v3; v5 exists", and the project is unchanged.
- **AC-SCR-82.** Given Jordan deactivates a user who owns seven open items, then the dialog shows 7 with a link to Reassign work, and deactivation can still proceed.
- **AC-SCR-83.** Given the explainer for Discipline Lead, then each listed permission matches the §8 matrix row the server enforces, and a matrix change changes the text with no separate edit.

**Shipping**

- **AC-SCR-84.** Given each part's flag is off, then existing API tests and browser suites pass unchanged, and no new setting, field, column or screen of that part is present.

### 57.11 Decisions

Each decision below uses its default until Jay decides otherwise. The rows are also in `specs/DECISIONS.md` as 053-D1 to 053-D22.

| # | Decision | Default used |
|---|---|---|
| D1 | Approve this amendment's scope (Parts A to G) | Approve with the listed defaults |
| D2 | Charts on Reports, Portfolio and dashboards (§19, §41 R8, R9) | Home's two FR-VIS-07 charts take the prototype's forms (bars by project, donut by status); a report may show one chart from its own rows beside its table (FR-SCR-41); each planner group may show one summary row (FR-SCR-45); no new chart on Portfolio or the project Dashboard; §19's "not built" line gains these exceptions |
| D3 | My Staff six-week strip and this week split by project (§42.5 D1, FR-SUP-01, FR-SUP-05) | Not built; §42 stands |
| D4 | Estimated against logged hours per discipline, template hours against actual, planned beside logged on Time, a task estimate-against-logged bar (039-DEF1, §29 item 5, §30, FR-RES-17) | Not built; 039-DEF1 stays parked and FR-RES-17 stays Phase 3 |
| D5 | Over-capacity chip on board cards (§41 R7, R8) | Not built |
| D6 | Open roles outside Allocations and Workload (FR-ROLE-04) | Allowed as separate, labelled figures on the project list, Portfolio, project Dashboard, Workload discipline rows and project Team tab; never in a person's capacity, load, indicators or planner row |
| D7 | Elapsed % beside progress % (§30 earned value) | Two plain facts; no ratio, index, colour or health input |
| D8 | Trend comparison | Latest snapshot at or before today minus `trend_compare_days` = 7; Team Members shows no trend |
| D9 | Undo window | 10 seconds, as §13.0; the reverse is written as new entries |
| D10 | Lift §42.5's deferral to hold immediate notices while away, or redirect to a deputy? | Propose hold only (FR-SCR-80), no redirection; existing daily-digest hold remains unchanged |
| D11 | Person types | Employee, Co-op, Contractor; set by a System Administrator |
| D12 | Purpose-of-issue and return-code lists | Purposes: For review, For approval, For construction, For tender, For information. Codes: A Accepted, B Accepted with comments, C Revise and resubmit, D Rejected. Both organisation-managed |
| D13 | Date-move reason categories | Client request, Waiting on input, Scope change, Resource availability, Technical problem, Correction, Other; organisation-managed |
| D14 | Issue root-cause categories | Design error, Coordination gap, Missing input, Client change, Site condition, Other; organisation-managed |
| D15 | Decision categories | Design, Scope, Schedule, Method, Client, Other; organisation-managed |
| D16 | External reviews | Recorded by a project member against an `ExternalParty`; no external account |
| D17 | Client drawing numbers | Optional per-project pattern; a mismatch is flagged, never refused |
| D18 | Warning windows | `certification_warn_days` = 30; `availability_window_days` = 60; `staffing_lookahead_weeks` = 4; `home_due_window_days` = 7 |
| D19 | How many flags? | One per part: `flag.screen_patterns`, `flag.home_overview`, `flag.work_screens`, `flag.project_screens`, `flag.capacity_screens`, `flag.register_screens`, `flag.people_screens` |
| D20 | What counts as "New to me"? | Assigned, or made reviewer, after the person's previous My Work visit; cleared on opening or by Mark all seen |
| D21 | Narrow person panel and optional skills/certifications despite §39.12's deferred People/Person Profile? | Adopt only FR-SCR-49, FR-SCR-50, FR-SCR-74 and FR-SCR-76 with explicit self/Admin facts editing and permission-scoped reads; full People/Person Profile stays deferred |
| D22 | FTE denominator for report unit and month grouping | Standard full-time hours: period hours divided by displayed ISO weeks times `default_weekly_capacity_hours`; personal custom capacity affects utilisation only; never sum weekly FTE as monthly FTE |
