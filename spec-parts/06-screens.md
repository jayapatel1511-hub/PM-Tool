## 13. Detailed Screen Specifications

### 13.0 Global UI conventions

These conventions apply to every screen and should be implemented once as a design system.

**Layout.** Persistent left navigation rail (collapsible to icons) with: Home, My Work, Boards, Projects, Tasks, Calendar, Files, Time, Reports, Team, Portfolio, Resources, Notifications, Admin, filtered by role as in §36.1; More holds permitted lower-frequency routes. Top bar: global search, permission-filtered quick-create (Project, Task, Decision, Event), notification bell with unread count, user menu. Content area max-width unconstrained on desktop (tables benefit from width). Project screens show a **project header** (key, name, client, PM, phase, status pill, health pill, next milestone countdown, links, follow control) and project tabs beneath it. Time opens the task-hour view (§13.20, §36.8).

**Density.** Tables default to compact rows (32–36 px), with a user toggle for comfortable rows. Typography: a single sans-serif system stack; 13–14 px table text; 16 px body. No decorative imagery.

**Status colour language (with redundant text and icon; never colour alone)**

| Meaning | Colour token | Icon | Used for |
|---|---|---|---|
| On Track / Normal | Green | ● | Milestone On Track, health Green |
| Attention / Warning | Amber | ▲ | Milestone At Risk, health Yellow, Due Soon, Waiting, Warning attention items |
| Critical / Blocked / Overdue | Red | ■ | Overdue, Blocked, health Red, Critical attention items |
| Not Started / Inactive / Not Evaluated | Grey | ○ | Not Started, On Hold, health Grey, Cancelled |
| Complete / Issued | Blue | ✓ | Complete, Issued, Accepted |
| In Progress / In Review | Neutral dark / Indigo outline | ◐ | Workflow statuses that are neither good nor bad |

Status pills always contain the status text. Indicators are small chips with an icon and short text ("Overdue 3d", "Blocked", "Waiting", "Review r2", "Stale 12d"). Colour contrast meets WCAG 2.1 AA; a high-contrast mode inherits from the OS setting.

**Interaction.** Detail views open as a right-side panel (approximately 560 px) over lists and boards, with a "open full page" control; URL updates so panels are deep-linkable. Inline edit on click for text fields and dropdowns in detail panels; explicit Save is not required except in multi-field dialogs (create forms, status changes with reason). Every destructive or consequential action (delete, cancel, status change with side-effects, milestone date change with cascade) uses a confirmation dialog that states the consequence in numbers. Undo toast for reassign and status changes for 10 seconds where the change has no side-effects on other users' notifications (Recommendation: skip undo in MVP if it complicates notifications).

**Filters.** Filter bar above every list with: quick chips (Mine, Overdue, Blocked, Due this week, Unassigned), a "+ Filter" menu for all fields, active filters shown as removable tokens, and "Clear". Filter state is encoded in the URL. Saved views are first-release requirements (§36.7).

**Sorting and grouping.** Click column headers to sort (with secondary sort by due date, then key). "Group by" control on lists (Discipline, Deliverable, Milestone, Assignee, Status, Due bucket).

**Empty states.** Every empty list explains what would appear there and offers the primary action ("No tasks are blocked. Blocked tasks appear here when a predecessor is incomplete or a manual block is set.").

**Loading and errors.** Skeleton rows while loading; inline error banners with a retry; optimistic concurrency conflicts show "This item was changed by Marc 2 minutes ago" with a reload-and-compare option.

**Keyboard.** `/` focuses search; `c` quick-create task on project screens; `j`/`k` move selection in lists; `Enter` opens the panel; `Esc` closes; in Weekly Coordination meeting mode, `→`/`←` move sections.

**Responsive.** ≥ 1280 px: full layout. 768–1279 px (tablet): navigation collapses to icons; tables hide low-priority columns (configurable per table via column chooser defaults); panels become full-width overlays. < 768 px (phone): navigation becomes a bottom bar with My Work, Search, Notifications; lists become cards; supported actions are read, change status, update progress, comment, and view blockers. Creating projects, editing milestones, templates, admin, resource and portfolio views are desktop/tablet only and display a notice on phones.

**Accessibility.** All interactive elements keyboard-reachable with visible focus; ARIA roles on tables, tabs, dialogs, and live regions for toasts; colour never the sole carrier of meaning; form fields with labels and error text; reduced-motion respected (there is little motion to begin with).

**Time display.** Dates as `2026-09-15` (ISO) or `15 Sep 2026` (**TBD — Business Decision Required**: date format); relative helpers alongside ("in 4 days", "3 days ago"); timestamps in local time with tooltip showing full value.

---

### 13.1 Project Dashboard

**Purpose.** Answer "how is this project doing and what needs attention right now" in one screen without scrolling on a 1080p display.

**Intended users.** PM (daily), DLs, team members, Executives/Supervisors (drill-in from portfolio).

**Information displayed (layout top to bottom)**

1. **Project header** (shared component).
2. **Milestone strip**: next 5 non-complete milestones as diamonds on a mini timeline with name, date, countdown, status colour; overdue ones first; "View all".
3. **Headline tiles** (each a link): Health (computed; reported override if any, with note preview); Next Milestone (name, date, days); Next Submission (name, date, days); Phase.
4. **Counts row** (each number links to the filtered list):
   - Tasks: Total · Complete · In Progress · Ready/In Review · Overdue · Blocked · Waiting · Unassigned
   - Deliverables: Upcoming (due ≤ 14 d) · At Risk · Issued/Accepted · Total
   - Decisions: Pending · Overdue
   - (P2) Issues: Open · High
   - (P2) Risks: High
5. **PM Attention** panel: top 10 attention items with severity icon, item key/name, "why", owner, age, and quick actions (open, snooze). "Show all (23)".
6. **Discipline progress** table: Discipline · Lead · Status colour · Open · Overdue · Blocked · Deliverables due ≤ 14 d · Next due item. Rows link to Task List filtered by discipline.
7. **Due this week / Blocked** two-column lists (top 8 each) with links.
8. **Recent activity**: last 15 activity entries (who did what to which item, when) with filter to "important only" (status, assignment, date, deletion).

**Primary actions.** Quick-create task; Open Weekly Coordination; Change health override (PM); Change status (PM); Edit project (PM).

**Filters.** None on the dashboard itself except "Discipline" scope chip (view the dashboard for one discipline), which filters sections 4–8.

**Sorting.** Fixed per section (attention rank; due date; recency).

**Navigation.** Every count and row deep-links into the corresponding list with filters applied and the item panel opened where relevant.

**Key UX considerations.** Numbers must reconcile with the lists they link to (same rules, same code path). Health pill has a "Why?" popover listing the exact indicators that fired (§16). Avoid charts for their own sake; the only visual is the milestone strip and the progress bars in the discipline table. Dashboard for a `Setup` or `On Hold` project shows a banner explaining why evaluation is paused.

---

### 13.2 Project List

**Purpose.** Find and open projects; give PMs and DLs a compact view of their projects' state.

**Users.** Everyone.

**Information.** Table: Project number · Name · Client · PM · Office · Phase · Status · Health · Priority · Target completion/due · Progress · Next milestone (name, date) · Overdue tasks · Blocked tasks · My role (for current user) · Last activity. The first-release compact board presentation groups Active and Upcoming/Planning with counts and shows Owner (PM), status, priority, due, and progress (§36.2). Default filter: Status in (Active, Setup, On Hold) and "My projects" for users who have project roles; "All projects" toggle.

**Primary actions.** Create project (PM system role/Admin); open project; star/favourite (Recommendation) for personal ordering.

**Filters.** Status, PM, client, office, phase, discipline (projects having the discipline), health, project type, "include archived".

**Sorting.** Default: health severity (Red, Yellow, Green, Grey) then next milestone date. Any column sortable.

**Navigation.** Row → Project Dashboard. Health cell → dashboard with the "Why?" popover open.

**UX.** Project number is monospace and copyable. Health has text label in the cell.

---

### 13.3 Task List

**Purpose.** The working table for a project's tasks; the destination for every dashboard count.

**Users.** Everyone on the project; DLs use it grouped by deliverable within their discipline.

**Information.** Columns (default): Key · Name · Deliverable · Discipline · Assignee · Reviewer · Status · Indicators · Priority · Start · Due · Progress · Est. hrs · Last activity. Column chooser adds: Milestone, Created, Completed, Blocking count, Review round, Due changes. Indicators cell shows chips (Overdue, Blocked, Waiting, Blocking n, Stale, Review rn, Date inconsistent).

**Primary actions.** Create task (inline "add row" under a deliverable group or button); open panel; inline edit of status, assignee, due date, priority, progress from the row; multi-select with bulk actions (assign, set/shift due date, set priority, set deliverable, mark On Hold with reason, cancel); export CSV/XLSX of the filtered view.

**Filters.** Quick chips: Mine · Overdue · Blocked · Blocking others · Due this week · Unassigned · Ready for review. Full: status (multi), assignee, reviewer, discipline, deliverable, milestone, priority, due range, start range, indicators, requires review, has dependencies, created by, text search within project.

**Sorting.** Any column; default due date ascending with nulls last, then priority.

**Grouping.** Deliverable (default), Discipline, Milestone, Assignee, Status, Due bucket (Overdue, Today, This week, Next week, Later, No date). Group headers show counts and, for deliverables, the deliverable's status pill and progress.

**Navigation.** Key → panel; deliverable name → deliverable panel; assignee → My Work of that user (read) for PM/DL/Supervisor.

**UX.** Virtualised rows for large projects (1,000+ tasks). Sticky header and group headers. Row hover reveals quick actions. Keyboard navigation per §13.0. Blocked chip click opens the blockers popover without opening the whole panel.

#### 13.3.1 Task Detail Panel

**Sections (top to bottom).** Header (key, name, status pill/menu, indicator chips, "open full page", close). Blockers box (only when Waiting/Blocked): list of blockers with links and "Start anyway" note; affected milestones. Fields grid: Assignee, Reviewer, Requires review, Priority, Discipline, Deliverable, Milestone (derived or direct), Start, Due (with change count when ≥ 3), Progress slider (10% steps), Estimated hours, Actual hours total with Add Time (§36.8). Description. Dependencies: Depends on / Blocks lists with add/remove and "Show chain". Manual block: set/clear with type and reason. Collaborators and watchers. Document links (own and inherited from deliverable). Tabs: Comments · History.

**Behaviour.** Autosave per field with subtle confirmation; status transitions through the pill menu with reason dialogs where required; review outcomes through explicit "Approve" and "Request revision" buttons visible to the reviewer when In Review.

---

### 13.4 Kanban Board

**Purpose.** A visual flow view for a discipline, deliverable, project, or selected set of permitted projects; useful for DLs and teams who think in columns.

**Users.** DLs, team members.

**Information.** Default visible lanes = To Do · In Progress · Review · Done, mapping to the canonical statuses in §36.3; the exact canonical status remains visible on review cards. On Hold and Cancelled are accessible through filters/side columns. Card: key, name, project chip when multiple projects are selected, assignee initials, due date (coloured if overdue/due soon), indicator icons (blocked chain, review, stale, comments), deliverable chip, priority marker. Column header shows count.

**Primary actions.** Drag between columns (transition rules enforced; invalid drop shows why and snaps back; transitions needing a reason open the dialog on drop); create card in a column; open panel; swimlane toggle.

**Filters.** Same as Task List; board is typically scoped by Discipline or Deliverable via filter.

**Sorting.** Within a column: due date (default), priority, or manual; manual order persists per project or named workspace in the first release (§36.3).

**Swimlanes.** None (default), Discipline, Deliverable, Assignee.

**Navigation.** Card → panel. Deliverable chip → deliverable panel.

**UX.** No WIP limits, no card colours by custom label, no automation. Board and List share the same filter and project scope so switching views keeps context. Manual card ordering is required for the first release (§36.3).

---

### 13.5 Timeline (Gantt)

**Purpose.** See tasks, milestones, and deliverables across the selected permitted projects in time; spot crowding before submissions.

**Users.** PM, DLs, Executives.

**Information.** Per §12.16 and §36.4: project groups, milestone diamonds, discipline/deliverable hierarchy, task bars with progress, dependency arrows, today line, and an Unscheduled list.

**Primary actions.** Zoom (week/month/quarter); expand/collapse projects and disciplines; click to open panel; print; authorised drag to reschedule with preview and confirmation.

**Filters.** Discipline, milestone, status, hide completed, date range.

**Sorting.** Disciplines by project sort order; deliverables by due date.

**Navigation.** Elements → panels; milestone → Milestone view.

**UX.** Today line always visible; labels never overlap (truncate with tooltip); overdue segments hatched; legend visible. Date changes follow the guards in §36.4.

---

### 13.6 Deliverables Register

**Purpose.** The discipline lead's and PM's control list of what the project must produce.

**Users.** DLs, PM, owners, reviewers.

**Information.** Columns: Key · Deliverable · Type · Discipline · Owner · Reviewer · Milestone · Due · Status · Progress (bar + n/m tasks) · Indicators (Overdue, At Risk, Due soon, Unassigned, Date inconsistent, Slipped) · Revision · Issued date. Expand row → tasks inline (key, name, assignee, status, due).

**Primary actions.** Create deliverable (PM/DL); open panel; change status (with guards); Issue deliverable (dialog); add task under deliverable; bulk: set milestone, shift due dates, set owner.

**Filters.** Discipline, status, milestone, owner, type, due range, indicators, requires review.

**Sorting.** Default: due date asc; also status, discipline, milestone, progress.

**Grouping.** Discipline (default), Milestone, Status, Owner.

**Navigation.** Milestone → Milestone view; owner → filtered Task List; task rows → task panel.

**UX.** Status guard messages are explicit ("Cannot set Ready to Issue: this deliverable requires review and has not been In Review"). Issue dialog captures date, revision, issued to, and an optional link to the transmittal. The register is the natural home for a DL's weekly check.

#### 13.6.1 Deliverable Detail Panel

Header (key, name, status pill/menu, indicators). Fields grid (Type, Discipline, Owner, Reviewer, Milestone, Start, Due, Priority, Requires review, Revision, Issued date/to, Accepted date). Progress with task breakdown. Tabs: Tasks (mini list with add) · Dependencies (derived from tasks, read-only; P2 explicit) · Links · Comments · History. "Issue deliverable" button when Ready to Issue (or with confirmation from other states for PM/DL).

---

### 13.7 Milestone View

**Purpose.** Make submission dates and their readiness prominent.

**Users.** PM, DLs, Executives.

**Information.** Two presentations: **Strip/Timeline** (all milestones on a horizontal axis with today marker; diamonds coloured by status; hollow ghost at original date when slipped) and **Table**: Key · Milestone · Type · Date · Original date · Slip (d) · Status (with "why") · Days remaining · Deliverables (n Issued / m total) · Tasks under them (complete/total, overdue, blocked) · Discipline tag · Client-facing. Expand row → targeted deliverables with status, owner, due, progress; and under each, counts of open/overdue/blocked tasks.

**Primary actions.** Create milestone (PM); edit date (PM; dialog shows slip and cascade option per M-04); mark Complete (PM; confirmation per M-05 and phase suggestion per M-06); cancel milestone (PM).

**Filters.** Type, status, discipline, show completed.

**Sorting.** Date asc (default).

**Navigation.** Deliverable → panel; "what's outstanding" → Task List filtered to open tasks under the milestone.

**UX.** Status "why" popover lists the rule that fired ("At Risk: 2 of 5 deliverables not issued with 9 days remaining; 1234-T0042 overdue"). Completing a milestone is a deliberate PM action, celebrated only by a blue tick.

---

### 13.8 Decision Register

**Purpose.** Show what decisions are outstanding, who owns them, when they are needed, and what they are holding up.

**Users.** PM, DLs, requesters, everyone (view).

**Information.** Columns: Key · Subject · Owner (internal user or external party with organisation) · Requested by · Requested · Required by · Days to/overdue · Impact · Status · Blocking (n tasks) · Linked deliverables/milestones. Row expand → linked items.

**Primary actions.** Raise decision; record decision (owner/PM; dialog with decision text and date); defer (new date and reason); cancel; link items; comment.

**Filters.** Status, owner, owner type (internal/external/client), impact, required-by range, "blocking work".

**Sorting.** Default: overdue first, then required-by asc, then impact.

**Navigation.** Blocking count → Task List filtered to blocked-by-decision; linked items → panels.

**UX.** The register reads as an agenda for the client call: "Decisions we need from you, with dates." External owners are visually distinct (organisation shown). "Record decision" is one dialog with the text, date, and a "notify linked task assignees" checkbox (default on).

---

### 13.9 Weekly Coordination

**Purpose.** Run the coordination meeting from the screen. The single most important PM screen.

**Users.** PM (runs), DLs (present their discipline), team (follow along), Executives (occasionally).

**Information.** Agenda sections 1–13 as defined in §12.13, rendered as collapsible cards in order, each with a count in the header and a "jump to" side index. Section content is table-like rows with the same status pills, indicators, owners, and due dates used elsewhere. A "since last review" marker (`last_coordination_reviewed_at`) is shown at the top with the date.

**Primary actions.**
- **Meeting mode** toggle (§12.13): larger type, keyboard stepping, inline actions, "changes made in this meeting" tray.
- Inline: change status, change due date (with reason), reassign, add comment, set/clear manual block, create task, record decision, (P2) create meeting action.
- **Mark as reviewed** (PM/DL): stamps the review time.
- **Copy summary**: plain-text digest to clipboard.
- **Print** view.

**Filters.** Discipline scope (one or all); "hide items already discussed" toggle in meeting mode (items acted on in this session collapse).

**Sorting.** Fixed per section (§12.13).

**Navigation.** Every row opens its panel without leaving the view; the panel closes back to the same scroll position.

**Key UX considerations.** The screen is designed to be projected: high contrast, no dense side panels in meeting mode, section headers readable from across a room. Grouping blocked work by *blocker* means the meeting talks about the cause once rather than per task. The "Discipline round" section has a fixed rhythm (DL, status, counts, top items) so the meeting has the same shape every week. "Recently completed" gives the team visible progress. Nothing on this screen requires data entry that is not also useful outside the meeting.

---

### 13.10 My Work

**Purpose.** The one page a person opens each morning. Everything assigned to, waiting on, or blocked by them, across all projects.

**Users.** Everyone. Supervisors and PMs may view another user's My Work read-only (Supervisors: supervised staff; PMs: their project members' tasks within their projects only).

**Information (sections/tabs, each with count)**

| Section | Definition |
|---|---|
| Needs my attention | Attention items routed to me (A-01…A-20) |
| My Tasks | Tasks where I am assignee or collaborator, not terminal; default grouped by due bucket (Overdue, Today, This week, Next week, Later, No date) |
| My Reviews | Tasks where I am reviewer and status is Ready for Review or In Review; deliverables where I am reviewer and status In Review |
| My Deliverables | Deliverables I own, not Issued/Accepted/Cancelled |
| Waiting on Others | My tasks that are Waiting or Blocked, with the blocker and its owner |
| Blocking Others | My tasks that are Blocking Others, with the successors and their owners |
| My Decisions | Decisions I own or requested that are open |
| My Projects | Projects where I hold any role or that I follow, with my role(s), follow level (changeable inline, §12.18), health, next milestone |
| Upcoming Milestones | Milestones in my projects within 30 days |
| My Week | My own planning row for this week and the next five (§39.9): capacity, time away, hours by confidence (Confirmed, Expected, Possible) and remaining capacity; my self entries with quick add; Proposed and Confirmed assignments and approved project allocations, read-only. Private drafts never appear. Shown only on my own My Work; editable on desktop, read-only on tablet, a notice on phones. |
| Recently completed by me | Last 14 days (collapsed) |

**Primary actions.** Change status/progress inline; open panel; add comment; start review; quick-create task (into a chosen project).

**Filters.** Project, discipline, status, priority, due range, "hide waiting".

**Sorting.** Due date (default), priority, project, last activity.

**Navigation.** Rows → panels; project → dashboard; blocker owner → (for PM/DL/Supervisor) their My Work.

**UX.** Overdue and due-today items are visually first. The page works well at tablet width because most actions are status and progress changes. On phones, My Tasks and My Reviews are the primary tabs.

The first-release personal presentation adds Today, Upcoming, Overdue, Completed, Inbox, and saved views My Tasks, Assigned to Me, and Created by Me, with task/project/due/priority columns and guarded completion controls (§36.7). It retains the sections above.

---

### 13.11 Resource / Workload View [First release under §36]

**Purpose.** Cross-project workload per person, per §12.15.

**Users.** Supervisors, Executives, PMs (their members).

**Information.** Grid: rows = people (grouped by supervisor or discipline), columns = next 8 weeks; cell = assigned hours / capacity with heat shading and the number; row summary: projects, open tasks, unestimated tasks, overdue, deadline cluster flag, indicator (Over/OK/Under). Expand row → Project → tasks (key, name, due, est., remaining).

**Primary actions.** Reassign task (Supervisor for supervised staff; PM within project); set a person's capacity override (Supervisor/Admin); export.

**Filters.** Supervisor, discipline, office, project, indicator, date range.

**Sorting.** Load desc (default), overdue, name.

**UX.** Method popover explains the calculation and its limits in plain language; unestimated counts sit beside hours so nobody over-reads the numbers.

---

### 13.12 Portfolio Dashboard [First release under §36]

**Purpose.** Which projects need help this week, and why.

**Users.** Executives, Supervisors, PMs (own projects).

**Information.** Table: Project · PM · Client · Office · Phase · Health (computed and reported, with "why") · Next milestone (name, date, status) · Next submission · Overdue tasks · Blocked tasks · Decisions overdue · High issues · Attention (Critical/Warning counts) · Health trend (sparkline from daily snapshots, last 8 weeks). Summary tiles: Active projects; Red/Yellow/Green counts; submissions in next 14 days; total overdue decisions.

**Primary actions.** Open project dashboard; open project's Weekly Coordination; export.

**Filters.** PM, discipline, client, office, status, phase, health, project type, submission within N days.

**Sorting.** Default health severity then next submission date.

**UX.** Health "why" is always available; reported health that differs from computed is shown as two pills ("Computed: Yellow · Reported: Green — note by PM, 3 days ago") so optimism is visible, not hidden.

Home also provides the first-release overview dashboard in §36.6: five headline metrics, two task charts, Upcoming Deadlines, a date-range control, and a personal Edit Dashboard layout control. This overview does not replace the project dashboard's attention sections.

---

### 13.13 Risk Register, Issue Register, Meetings & Actions [Phase 2]

Standard register tables with panels, per §12.10 and §12.11. Risk Register default sort: severity desc then review date; Issue Register: severity desc then target resolution date; Actions: due date asc grouped by meeting. Each register has a "raise from here" action available from task/deliverable panels (pre-links the item).

---

### 13.14 Activity History

**Purpose.** Answer "who changed what, when".

**Users.** Everyone (project scope); Admin (global).

**Information.** Reverse-chronological list: timestamp · actor (or "System") · action verb · item (key + name) · change detail (field: old → new) · source (UI/API/System). Item-level history tab shows the same, scoped.

**Primary actions.** Export CSV (Rec); open item.

**Filters.** Date range, actor, item type, action type (status change, assignment, date change, deletion, creation, comment, decision, health override, dependency), discipline, "important only".

**Sorting.** Time desc only.

**UX.** Deletions show a snapshot summary of the deleted item. The log is read-only; nothing here can be edited or removed by any user.

---

### 13.15 Administration Screens

**Users & Roles.** List of users (from sign-in provisioning and sync) with system roles, supervisor, office, active state, last sign-in; edit system roles (if in-app assignment is used), supervisor, capacity (P2); "Reassign work" tool for a user (lists all open items across projects with bulk reassignment, Rec).

**Reference data.** Disciplines, Clients, Offices, Deliverable Types, Phases, Project Types, Milestone types are fixed enum (no admin). Each list: add, edit, deactivate (never hard-delete when referenced), reorder.

**Settings.** Organisation thresholds (§10.4) with defaults and descriptions; notification defaults; project-number format regex; `allow_self_review`; org time zone; date format.

**Templates [P2].** Template list; template editor with tabs for disciplines, milestones, deliverables, tasks, dependencies; publish/retire; preview instantiation.

**UX.** Admin screens are plain forms and tables; changes are logged to the activity log with actor.

---

### 13.16 Project Team & Settings

**Team tab.** Per §12.2: disciplines with leads; members with roles and primary discipline; add/remove; reassign prompt on removal.

**Settings tab (PM).** Edit project information; important links; coordination day; visibility (if enabled); status change; health override; archive; danger zone (cancel project).

---

### 13.17 Notification Centre

**Purpose.** In-app list of notifications with unread state.

**Information.** Two tabs. **Notifications** (personal): grouped by day; each: icon by type, text ("Marc assigned you 1234-T0042 Update grading plan"), project, time; unread dot. Filter: unread, type, project. Actions: mark read, mark all read, open item, open preferences. **Following**: changes by others on projects the user follows at All activity (§12.18), grouped by project then day and rendered like Activity History ("Marc changed 1234-T0042 due date 2026-09-10 → 2026-09-17 — client extension"), with an unread count per project, "mark project as read", and an "important only" filter (status, assignment, date, deletion, decision, milestone).

**Preferences page.** Per event type: In-app / Email / Off; daily digest on/off and time; per-project follow level (All activity / My items only / Muted, §12.18).

---

### 13.18 Reports

**Purpose.** Run the deterministic reports in §19 with filters and export.

**Information.** Report catalogue with description; parameter form; results table (same components as lists); export CSV/XLSX; "open as filtered list" where the report maps to a list.

---

### 13.19 My Staff

**Purpose.** Show a manager their direct reports on one page — who is assigned to which projects in what role, and who is overloaded, blocked, or holding up others — and let them staff people on projects without going through each PM.

**Intended users.** Supervisors (their direct reports, §8.8); Executives and System Administrators (can switch the scope to all staff).

**Information displayed (top to bottom)**

1. **Scope bar**: My direct reports (default) · All staff (Executives and Admins only). Filters: office, discipline, project, indicator (has overdue, has blocked, blocking others, reviews waiting), show inactive.
2. **Summary tiles** (each a link to the filtered table): Staff · Project assignments · Staff with overdue work · Staff with blocked work · Reviews waiting longer than `review_stale_days`.
3. **Staff table**, one row per person: Name · Job title · Office · Projects (count) · Roles (chips such as "PM 1 · DL 3 · Team 4 · Reviewer 2") · Open tasks · Overdue · Blocked · Blocking others · Reviews waiting on them · Deliverables owned due ≤ 14 d · Last activity. Every count links to that person's My Work section, filtered.
4. **Expanded row — assignments**: one line per project the person is on: project number and name, health pill, role(s), primary discipline, added on, their open and overdue tasks on that project, next due item. Each line has **Remove from project** (Supervisor; Team Member role only). The row has **Assign to project**, which opens a picker of projects the supervisor can view in Setup, Active, or On Hold, and a primary-discipline picker (ASG-10).

**Primary actions.** Assign to project and remove from project (Supervisor, direct reports, ASG-10); open the person's My Work (read-only); open a project; reassign a task (Supervisor, direct reports); export via the Staff Assignments report (§19).

**Sorting.** Default: Overdue descending, then Blocked descending, then name. Any column sortable.

**Navigation.** Person → their My Work (read-only); project → Project Dashboard; counts → filtered lists. Scope and filters are encoded in the URL.

**Key UX considerations.** Counts only, no hours on this screen: hours and capacity belong to the first-release Resource View, which reuses this page's scope and adds the week grid. Work on Restricted projects the viewer cannot see is excluded (ASG-09, §36.1). Counts come from the same permitted materialised state as My Work, so a person's row reconciles with their permitted My Work. On phones the table becomes a read-only card list.

---

### 13.20 Time / Task Hours [First release]

**Purpose.** Record actual effort against tasks without turning the Hub into a billing or payroll timesheet (§36.8).

**Users.** Project members enter their own hours. PMs and Discipline Leads review permitted project/discipline entries; Supervisors review direct reports within projects they may view.

**Information.** A Today/This week summary above a dated list: Work date · Project · Task key/name · Hours · Note · Entered by. Daily and weekly totals are sums of non-deleted entries, not estimates. Filters cover project, task, person (for authorised reviewers), and date range. Task and project totals are visible from their details.

**Primary actions.** Add Time against a permitted task; edit or soft-delete own entry; PM correction with a required reason; export the filtered list. The form defaults the work date to today and never substitutes another user's identity. An invalid or over-24-hour day shows a field error. Entries on Completed tasks are allowed while the project remains editable; Archived and Cancelled projects are read-only.

**Navigation.** Task → task panel; project → project dashboard; Time remains a direct global route. The responsive view preserves the same fields and permission rules.

---

### 13.21 Weekly Planner [Approved under §39]

**Purpose.** A Supervisor's weekly planning surface: each person's capacity, what is already planned and with what confidence, and adding or adjusting planning entries in one or two interactions (§39). Workload (§13.11) remains the task-forecast and allocation-commitment view; the two screens link to each other and keep their own indicators.

**Users.** Supervisors (plan their direct reports), System Administrators (all people; data correction), Executives (read all), Project Managers with the system role (read members of the projects they manage). Everyone sees their own row in My Work, My Week (§13.10). Permissions: §8.11.

**Information.**

1. **Header**: title; previous, this and next week; week picker; horizon (`planning_horizon_weeks`, default 12); Grid or List; Export; saved views; "Show my private drafts" (on by default); the partial-view note when it applies.
2. **Filter bar** (§13.0): Supervisor, discipline, office, person, project, source category, confidence, visibility, indicator (Over-planned, Under-planned, Stale plan) and text. Entry filters select people and highlight entries; they never change totals.
3. **Grid**: a sticky 240 px person column (name, Supervisor, weekly capacity, indicator chips, partial chip) grouped by Supervisor; one column of at least 144 px per ISO week with a sticky header ("W42 · 12 Oct"), the current week first and highlighted; deliberate horizontal scrolling inside the grid with a visible scrollbar. Each cell shows remaining capacity prominently (for example "−3.0 h"), planned hours against capacity in smaller text, a stacked confidence bar (Confirmed, Expected, Possible, each labelled in the legend and the cell's accessible name; Possible dotted), time away ("Time away 16 h") and indicator chips with text. A team footer row gives band totals and the number of Over-planned people per week.
4. **Expanded row**: one sub-row per visible planning entry spanning its weeks (source tint, label, hours per week, Confidence and Visibility labels, lock, hatch and the words Private draft for a draft, dotted outline and the word Possible for possible work, Stale plan and other warnings); one sub-row per approved project allocation (project, hours, link icon, "Approval status: Confirmed", read-only); a Time away sub-row; and a "Task estimates (context)" sub-row ("14 h · 3 unestimated") that is never added to totals.
5. **List view**: a paged table of visible planning entries — person, owner, label, source category, project, hours per week, start and end week, Confidence, Visibility, last validated, warnings — with the same filters and export.

**Primary actions.** Quick add in an editable cell (Enter or click, type "8 h proposal support", Enter saves, Esc cancels). The entry side panel (560 px): hours, label, source category, project and project discipline, start and end week, Repeat for N weeks, person, Confidence, Visibility (a segmented control of Private draft, Proposed assignment and Confirmed assignment, shown to the owner of a manager entry), notes, Still valid, Copy, Delete and history. A contribution list on any cell total (§39.4). Record or clear time away for a date range (Supervisor or Admin). Open Workload or a project.

**Sorting.** Remaining capacity ascending in the selected week (default), name, Over-planned first.

**Responsive.** At 1280 px and wider the planner is editable. From 768 px to 1279 px it is read-only: no quick add, side panels read-only, contribution lists available. Below 768 px it shows the §13.0 notice that the planner is available on desktop and tablet.

**Keyboard and accessibility.** The grid uses grid semantics with one tab stop: arrow keys move between cells; Enter opens quick add in an editable cell or the contribution list in a read-only one; Esc cancels and returns focus to the cell; Tab moves into the side panel, and closing it returns focus to the originating cell. Saves, conflicts and errors are announced in a live region. Every state has text and a symbol, colour is never the only signal, and no operation needs a pointer or dragging.

**Empty states.** "No one is in your planning scope." "No planning entries yet. Select a week cell and type, for example, 8 h proposal support."

**Navigation.** Route `/planner` under Resources, beside Workload; an entry opens its side panel with a deep link; a project link opens the project; an approved project allocation opens its §37.6 detail for viewers allowed to see it. Scope, filters and week are encoded in the URL.
