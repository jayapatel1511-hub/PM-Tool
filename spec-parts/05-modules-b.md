### 12.7 Documents and Links

**Purpose.** Connect work items to where the documents actually live, without storing files.

**Recommended approach.** The Hub is a *pointer* system. Every project, deliverable, and task can hold any number of `DocumentLink` rows: `title`, `url`, `link_type` (SharePoint, OneDrive, Teams, Network Folder, External DMS, Other — auto-detected by URL pattern, overridable), `added_by`, `added_at`. Network paths are stored as UNC (`\\server\share\project\...`) and rendered with a "Copy path" button because browsers cannot open UNC links directly. No files are uploaded, no content is indexed, no permissions are mirrored; if a user cannot open the target, that is governed by the target system.

**Why this is the right approach.** Document control in an engineering firm already lives in SharePoint/DMS with its own versioning, permissions, and retention. Duplicating any of it creates two sources of truth and a compliance problem. The coordination value is knowing *where* the current package is and *who* is working on it, which a link provides.

**Business rules.** DOC-01 URL must be http(s) or a UNC path; DOC-02 link title defaults to the last path segment; DOC-03 links are soft-deleted and logged; DOC-04 links on a deliverable are shown on its tasks (inherited, read-only) so the package folder is one click from any task.

**Future capability [Phase 3].** SharePoint integration: browse a project library from within the Hub and pick a document (Microsoft Graph). Still no storage in the Hub.

**Acceptance criteria.** AC-DOC-01 … AC-DOC-03.

---

### 12.8 Comments and Collaboration

**Purpose.** Capture item-specific discussion and hand-off notes where the work is tracked, without becoming a chat tool.

**Inputs.** `Comment`: `item_type`, `item_id`, `project_id`, `author_id`, `body` (lightweight markdown: bold, italic, lists, links, inline code; no images), `mentions[]` (user IDs parsed from `@Name` tokens at save), `comment_kind` (General, Review, Status Note, System), `created_at`, `edited_at`, `deleted_at`.

**Rules.** C-01 comments allowed on tasks, deliverables, milestones, decisions (and P2 registers) by any project member and Viewer if project allows; C-02 author may edit within 15 minutes of posting (shows "edited"); after that, edits are not allowed — post a follow-up; C-03 author may delete own comment at any time; deletion leaves a "Comment deleted by author" placeholder and the body is retained in the database for audit (visible to Admin only); C-04 PM may delete any comment in their project (placeholder shows "removed by PM"); C-05 @mention resolves against project members first, then all active users; mentioning a non-member adds them as a Viewer on the project? **Recommendation:** No — mentioned non-members receive the notification and can open the item (read) but are not added to the team; PM sees "mentioned non-member" in activity; C-06 mentioned users are added as watchers on the item; C-07 review outcomes (Revision Required) are posted as comments of kind Review with the round number; C-08 system events are not comments (they live in the activity log), except an optional "status note" the user types when changing status, which is posted as a Status Note comment.

**UI behaviour.** Comment box at the bottom of the item panel; newest at the bottom; `@` triggers a people autocomplete; Enter adds a line, Ctrl+Enter posts. Comments and history are separate tabs (comments are conversation; history is facts).

**What is deliberately not built.** Threads/replies, reactions, chat channels, file attachments in comments, read receipts. Use Teams for discussion; link back to the item by key.

**Acceptance criteria.** AC-COM-01 … AC-COM-05.

---

### 12.9 Decision Register (thin version recommended for MVP)

**Purpose.** Track decisions that must be made — by the client, an authority, management, or the team — with an owner and a date, and connect them to the work they hold up.

**Users.** Anyone raises; owner decides; PM manages; everyone views.

**Inputs**

| Field | Type | Required | Notes |
|---|---|---|---|
| `key` | generated | — | `1234-DEC02` |
| `subject` | text | Yes | "Confirm pavement structure for Dundurn St" |
| `description` | text | Yes | What must be decided and the options if known. |
| `requested_by_id` | FK User | Yes | Default current user. |
| `owner_user_id` **or** `owner_external_party_id` | FK | Exactly one | Internal or external decision owner. |
| `date_requested` | date | Yes | Default today. |
| `required_by_date` | date | Yes | |
| `impact_if_delayed` | enum Low/Medium/High + text | Yes | |
| `status` | enum §10.2 | Yes | |
| `decision_text`, `decision_date`, `decided_by_id` | text, date, FK | On Decided | |
| `deferral_reason` | text | On Deferred | |
| Links | `ItemLink` to Tasks (relation `blocked_by_decision` or `related`), Deliverables, Milestones | No | |
| Derived | Overdue, Due Soon, `blocking_count`, `days_overdue` | | |

**Rules.** DEC-01 owner required (internal or external); DEC-02 Decided requires decision text and date; DEC-03 Deferred requires a new `required_by_date` later than today and a reason; the previous date is kept in history; DEC-04 an Overdue decision (`required_by_date < today`, status Pending/Under Review/Deferred) blocks linked tasks with relation `blocked_by_decision` (D-15) and appears in attention rule A-04; DEC-05 a decision with `impact_if_delayed = High` that is Due Soon appears in attention (A-04 Warning); DEC-06 decisions owned by external parties generate no external notifications in MVP; the requester and PM receive the digest/attention items instead; DEC-07 reopening a Decided decision (PM) requires a reason and re-blocks linked tasks if overdue.

**Additional statuses considered.** "Approved/Rejected" — not needed; the decision text records the outcome. "Escalated" — not needed; use a comment and change the owner. "Superseded" — not needed; Cancel with reason and raise a new decision.

**External Parties.** `ExternalParty`: `project_id` (nullable for shared parties such as a municipality — Recommendation: project-scoped in MVP), `name`, `organisation`, `email`, `role`, `is_client`, `notes`. Used as owner of decisions and (P2) meeting actions, and as `manual_block_type = Client/External Party` reference on tasks.

**Fallback if the Decision Register is cut from MVP.** Decisions are represented as tasks with `manual_block_type = Decision` on the blocked work and a task named "DECISION: …" owned by the PM. This loses the external owner, the required-by semantics, and the register view, and is the reason the thin register is recommended.

**Acceptance criteria.** AC-DEC-01 … AC-DEC-06.

---

### 12.10 Risk and Issue Registers [Phase 2]

**Distinction.** A **risk** is something that *may* happen and would hurt the project ("Environmental approval may delay fieldwork"). An **issue** is something that *has* happened and needs resolution ("Survey crew cannot access site — gate locked, owner unresponsive"). A realised risk becomes an issue (linked).

**Risk fields.** `key`, `title`, `description`, `owner_id`, `probability` (1–3), `impact` (1–3), `severity` (derived = P × I, banded Low 1–2 / Medium 3–4 / High 6–9), `mitigation` (text), `trigger_indicator` (text: how we will know it is happening), `review_date` (date the risk should be re-assessed), `status` (Open, Monitoring, Closed, Realised), `realised_issue_id`, links to tasks/deliverables/milestones.

**Issue fields.** `key`, `title`, `description`, `raised_by_id`, `owner_id`, `severity` (Low/Medium/High, directly selected), `date_raised`, `target_resolution_date`, `resolution` (text), `resolved_date`, `status` (Open, In Progress, Resolved, Cancelled), links to tasks/deliverables/milestones, `origin_risk_id`.

**Scoring approach.** A 3×3 grid is sufficient for coordination purposes. Probability and impact are chosen from three labelled levels with plain-language anchors (e.g., Impact High = "would move a submission milestone or require re-work of an issued deliverable"). Severity is displayed as a colour band and a number. No expected monetary value, no Monte Carlo, no risk appetite matrices.

**Rules.** RSK-01 High severity Open risks appear on the dashboard and Weekly Coordination; RSK-02 a risk past its `review_date` is flagged "Review overdue"; RSK-03 Realised requires creating or linking an Issue; ISS-01 High severity Open/In Progress issues fire attention rule A-07 (Critical) and contribute Red to project health; ISS-02 Resolved requires resolution text; ISS-03 an issue past `target_resolution_date` is Overdue.

**Views.** Register tables with filters (status, severity, owner, discipline), sort by severity then date, detail panels with links and comments.

---

### 12.11 Meeting Action Register [Phase 2]

**Purpose.** Capture actions arising from project meetings and assign them to people, disciplines, clients, or external parties, without building a meeting-management product.

**Meeting fields.** `id`, `project_id`, `title` ("Weekly Coordination — 2026-09-14", "Client Progress Meeting #4"), `meeting_date`, `meeting_type` (Coordination, Client, Design Review, Site, Other), `notes_link` (URL to minutes in SharePoint/Teams), `created_by`.

**Action fields.** `key` (`1234-A07`), `meeting_id`, `action` (text), `owner_type` (User, Discipline, External Party), `owner_user_id` / `owner_discipline_id` / `owner_external_party_id`, `due_date`, `status` (Open, In Progress, Complete, Cancelled), `related_task_id`, `related_decision_id`, `comments`.

**Rules.** MTG-01 an action owned by a Discipline is routed to that discipline's lead for attention and appears in the DL's My Work; MTG-02 external-party actions generate no external notification; they appear in Weekly Coordination "Waiting on client / external"; MTG-03 "Convert to task" creates a task pre-filled from the action and links them; the action then tracks the task's completion; MTG-04 Weekly Coordination in meeting mode can create actions inline against the current meeting record.

**Not built.** Agendas, minutes authoring, attendance, automatic calendar synchronisation, recurring meeting series. A meeting may link to a first-release calendar event (§36.5).

---

### 12.12 "PM Attention Required" Engine

**Purpose.** Continuously identify items that need a human to intervene, using stated rules and thresholds, and route them to the right people.

**Users.** PM (primary), DL (own discipline), assignees (own items), Supervisors/Executives (counts).

**Design.**
- Rules are pure functions over the project's current data and organisation thresholds. They are evaluated on demand for dashboards (cached briefly) and on a 15-minute schedule for change detection and notifications (§23.5).
- Each firing produces an **attention item**: `rule_id`, `severity`, `item` (type, key, name, link), `route_to` (set of users), `why` (rendered message with the specific values: "Due 2026-09-10 — 5 days overdue; blocking 1234-T0057 and 1234-T0061"), `first_detected_at`.
- Items are ranked by severity, then days overdue/blocked, then priority, then due date.
- **Snooze** (Recommended): a PM or DL may snooze an item for 1–30 days with a note. Snoozed items are hidden from the default list, shown under "Snoozed (3)", logged, and reappear on expiry or if the severity increases.
- Items are never manually "dismissed"; they disappear when the underlying condition clears.

**Rules**

| ID | Rule | Condition (all: item not terminal; project Active) | Severity | Routed to |
|---|---|---|---|---|
| A-01 | Task overdue | Task Overdue (G-01) | Warning; Critical if `days_overdue > 5` or priority Critical | Assignee, DL, PM |
| A-02 | Task blocked | Task Blocked (D-06/D-14/D-15) for ≥ `blocked_attention_days` | Warning; Critical if Blocked and Due Soon or Overdue | Assignee, DL, PM |
| A-03 | Overdue predecessor blocking others | Task Overdue **and** Blocking Others | Critical | Predecessor assignee, both DLs, PM |
| A-04 | Decision overdue / at risk | Decision Overdue; or Due Soon with impact High | Critical (overdue) / Warning (due soon High) | Requester, owner (if internal), PM |
| A-05 | Milestone approaching with incomplete prerequisites | Milestone within `milestone_approaching_days`, not complete, and any targeted deliverable not Issued/Accepted/Cancelled with progress < 100 or any task under it Overdue/Blocked | Warning; Critical within 5 days | PM, DLs of affected deliverables |
| A-06 | Deliverable due soon with open work | Deliverable due within `deliverable_due_soon_days` and (open tasks Overdue/Blocked **or** progress < 50%) | Warning | Owner, DL, PM |
| A-07 | High-severity open issue [P2] | Issue severity High, status Open/In Progress | Critical | Owner, PM |
| A-08 | Task has no owner | Task Unassigned and (In Progress or later, **or** `start_date ≤ today`, **or** Due Soon) | Warning | DL, PM |
| A-09 | Task has no due date | Task with no due date and (In Progress or later, **or** parent deliverable has a due date) | Info | Assignee, DL |
| A-10 | Stale work | Task In Progress with `today − last_activity_at > task_stale_days` | Info; Warning if also Due Soon | Assignee, DL |
| A-11 | Review stalled | Task Ready for Review or In Review for > `review_stale_days` | Warning | Reviewer, DL, PM |
| A-12 | Milestone without deliverables | Active milestone of a submission type with zero targeted deliverables | Info | PM |
| A-13 | Deliverable without owner | Deliverable Unassigned and status ≠ Not Started, or due within `deliverable_due_soon_days` | Warning | DL, PM |
| A-14 | Milestone overdue | Milestone Overdue | Critical | PM, all DLs |
| A-15 | Health override expired | Override past `health_override_expires_at` | Info | PM |
| A-16 | Task date inconsistent with deliverable | Task `due_date > deliverable.due_date` | Info | Assignee, DL |
| A-17 | Deliverable date inconsistent with milestone | Deliverable `due_date > milestone.date` | Warning | DL, PM |
| A-18 | Work assigned to inactive user | Assignee/owner/reviewer is Inactive | Critical | DL, PM, Supervisor of the inactive user |
| A-19 | Task held past due | Task On Hold with `due_date < today` for > 10 days | Info | DL, PM |
| A-20 | Repeated due-date slips | `due_date_change_count ≥ 3` and task not Complete | Info | DL, PM |

**Configuration.** Each rule has `enabled` (bool) and, where noted, the thresholds in §10.4. Severity mappings are fixed in MVP. Adding rules requires code; the rule set is small and stable by design. Per-project overrides of thresholds are **[Phase 3]**.

**Outputs.** Dashboard "PM Attention" panel (top 10 with "show all"); Weekly Coordination "Items requiring PM attention" section; My Work "Needs my attention" strip for items routed to the user; Portfolio counts (Critical/Warning per project); digest email sections.

**Acceptance criteria.** AC-ATT-01 … AC-ATT-08.

---

### 12.13 Weekly Coordination (module logic)

The screen layout is in §13.9. This section defines the data behind each agenda section so it is computed identically everywhere.

**Time window.** "This week" = the 7 days starting from the project's `coordination_day` of the current week (or Monday if not set) through the day before the next; "next week" = the following 7 days. **Since last review** = changes since `project.last_coordination_reviewed_at` (or the last 7 days if never reviewed).

| Agenda section | Definition | Default sort |
|---|---|---|
| 1. Headline | Health (computed + reported), next milestone with countdown, next submission, counts: overdue tasks, blocked tasks, decisions overdue, deliverables at risk | — |
| 2. PM attention | Attention items severity ≥ Warning, not snoozed | Rank (§12.12) |
| 3. Milestones approaching | Milestones within `milestone_approaching_days` or Overdue, with status and prerequisite completeness | Date |
| 4. Deliverables due | Deliverables due this week or next week, or Overdue, not Issued/Accepted/Cancelled; grouped by discipline | Due date |
| 5. Decisions required | Decisions Pending/Under Review/Deferred that are Overdue or Due Soon, or that block any task | Required-by |
| 6. Blocked work | Tasks Blocked, grouped by blocker (predecessor task / decision / manual reason) so the meeting discusses the *cause* once | Days blocked desc |
| 7. Overdue work | Tasks Overdue grouped by discipline | Days overdue desc |
| 8. Due this week | Tasks due this week, not overdue, grouped by discipline | Due date |
| 9. Discipline round | For each project discipline: DL, status colour, open / overdue / blocked counts, deliverables due next 14 days, top 3 items; the meeting walks discipline by discipline | Discipline sort order |
| 10. Open issues / high risks [P2] | Issues Open/In Progress; risks High | Severity |
| 11. Recently completed | Tasks and deliverables completed/issued since last review; decisions Decided | Completed desc |
| 12. Upcoming | Tasks due next week; deliverables due within 14 days beyond this week | Due date |
| 13. Held items | Tasks and deliverables On Hold with reason and days held | Days held desc |

**Meeting mode.** Toggling meeting mode: enlarges type; hides filters; allows stepping through sections with keyboard arrows; enables inline actions on each row (change status, change due date with reason, reassign, add comment, set manual block, create task, P2: create meeting action); shows a running "Changes made in this meeting" tray. "Mark as reviewed" sets `last_coordination_reviewed_at = now` (logged), which resets the "since last review" delta. A "Copy summary" action produces a plain-text/markdown digest of sections 1, 3, 5, 6, 7 for pasting into meeting notes.

**Per-discipline view.** A DL can open Weekly Coordination filtered to their discipline before the meeting; the same sections apply, scoped.

**Acceptance criteria.** AC-WC-01 … AC-WC-07.

---

### 12.14 Project Templates [Phase 2]

**Purpose.** Let a PM create a fully structured project — disciplines, milestones, deliverables, tasks, dependencies, review flags — in minutes, from a curated template such as "Municipal Infrastructure Design".

**Template structure**

| Entity | Fields |
|---|---|
| `ProjectTemplate` | `name`, `description`, `project_type_id`, `version` (int, incremented on publish), `status` (Draft, Published, Retired), `created_by`, `published_at` |
| `TemplateDiscipline` | `template_id`, `discipline_id`, `sort_order`, `is_default_included` (PM can untick at instantiation) |
| `TemplateMilestone` | `template_id`, `name`, `milestone_type`, `sort_order`, `offset_days_from_anchor` (nullable), `anchor` (ProjectStart | PreviousMilestone | None), `completes_phase_id`, `is_client_facing` |
| `TemplateDeliverable` | `template_id`, `template_discipline_id`, `name`, `deliverable_type_id`, `template_milestone_id` (target), `due_offset_days` (relative to target milestone; negative = before), `requires_review`, `sort_order`, `description` |
| `TemplateTask` | `template_id`, `template_deliverable_id` (nullable), `template_discipline_id`, `name`, `description`, `requires_review`, `priority`, `estimated_hours`, `due_offset_days` (relative to deliverable due; negative = before), `assign_to_role` (DisciplineLead | PM | Unassigned), `sort_order` |
| `TemplateDependency` | `predecessor_template_task_id`, `successor_template_task_id` |

**Instantiation algorithm**

1. PM chooses a template; the wizard shows disciplines (tick/untick), milestones (with a date column), and a summary count.
2. PM enters the project start date and either (a) accepts offsets to compute milestone dates, or (b) types actual milestone dates (common — submission dates are usually contractual). Milestones with no date remain undated and are flagged.
3. Deliverable due dates = milestone date + `due_offset_days` (blank if milestone undated). Task due dates = deliverable due + task offset. Tasks without a deliverable and without an offset are undated.
4. `assign_to_role` resolves: DisciplineLead → the lead chosen in the wizard for that discipline; PM → the creating PM; Unassigned → null.
5. All items are **copied** into the project with `template_*_id` origin references and the project records `created_from_template_id` and `template_version`.
6. Dependencies are copied between the corresponding tasks; any dependency whose tasks belong to an unticked discipline is dropped.
7. The project is created in `Setup` status; the PM adjusts and then activates. No digest notifications fire during Setup.

**Editing after creation.** Everything is ordinary project data and fully editable. Later template changes never propagate to existing projects (snapshot semantics). "Add from template" lets a PM append a single discipline pack or deliverable set from any published template to an existing project, using the same date logic anchored to the project's existing milestones by name match (PM confirms mapping).

**Template governance.** Admins and users with the Template Editor flag can create/edit Draft templates; publishing increments the version; Retired templates are hidden from the wizard but remain referenced by projects.

**Reference example.** Appendix A specifies the "Municipal Infrastructure Design" template and how the DCC Dundurn Roads project instantiates it.

---

### 12.15 Resource / Workload View [First release under §36]

**Purpose.** Give supervisors and PMs a defensible, cross-project view of who is carrying how much, who has capacity, and where deadlines collide — without pretending estimates are precise.

**Calculation (transparent and stated on the screen)**

1. For each open task (not Complete/Cancelled/On Hold) with an assignee:
   - `remaining_hours = estimated_hours × (1 − progress_pct/100)` if `estimated_hours` is set; otherwise the task is counted in an "unestimated" bucket and contributes no hours.
   - Window = from `max(start_date, today)` to `due_date`. If `due_date < today` (overdue), the entire remainder lands in the current week. If `due_date` is null, remaining hours are shown in a "no due date" bucket and not spread.
   - Spread `remaining_hours` evenly across working days (Mon–Fri) in the window; sum per ISO week.
2. Per user per week: `assigned_hours`, `capacity_hours` (user override or `default_weekly_capacity_hours`), `load_pct = assigned / capacity`, `unestimated_task_count`, `overdue_count`, `task_count`, `project_count`.
3. Indicators: **Over-assigned** when `load_pct > 110%` in the current or next week; **Under-assigned** when `load_pct < 40%` for the next 2 weeks *and* `unestimated_task_count = 0` (never call someone under-assigned when their work is unestimated); **Deadline cluster** when ≥ 3 tasks across ≥ 2 projects are due within any 3-day window in the next 14 days.
4. The screen always shows the unestimated count next to the hours so the reader can judge reliability, and states the method in a help popover.

**What it deliberately does not do.** The first-release workload calculation does not subtract logged actual task hours from estimates or treat them as an approved timesheet (§36.8). It also does not use leave/holiday calendars (Phase 3, or import from HR), utilisation targets, resource levelling, forecasts beyond 8 weeks, or capacity planning by role.

**Views.** Person × week grid (heat-shaded load with the number visible) with expandable rows: Person → Project → Tasks (with due dates). Filters: supervisor, discipline, office, project, date range. Sort by load, overdue count, task count. Export.

---

### 12.16 Gantt / Timeline

**Purpose.** A visual sense of sequence and proximity — what is coming, in what order, and how deliverables relate to milestones. First-release manual date changes are permitted with confirmation (§36.4); the view is not an automatic scheduling engine.

**Supported [MVP-Required]**
- Horizontal time axis with week and month zoom; today line; project start/target dates.
- Milestones as diamonds on a top lane, coloured by status, labelled with name and date; slip shown as a hollow diamond at the original date when different.
- Deliverables as bars from `start_date` (or `created_at` if no start) to `due_date`, grouped by discipline (collapsible), with progress fill and status colour; Overdue bars extend to today with a hatched segment.
- Click any element to open its detail panel.
- Filters: discipline, milestone, status, show/hide completed.
- Print-friendly rendering (browser print CSS).

**Supported [MVP-Required under §36]**
- Tasks as thin bars under their deliverable (collapsible).
- Dependency arrows between tasks (and derived deliverable dependencies), highlighted when unsatisfied and overdue.
- Drag a bar or diamond to change dates, with a confirmation dialog showing the change and (for milestones) the optional cascade (M-04). Dates are the only thing that changes; nothing else is recalculated.
- Baseline (original) dates shown as ghost bars.
- A selected set of permitted projects appears as expandable project groups, each with tasks, deliverables, and milestones; date navigation preserves that scope. Cross-project dependencies are not inferred or created.

**Not supported [Out of Scope]**
- Automatic scheduling, critical path, float/slack, resource levelling, calendars/working-time, lag/lead (P2 at most), constraint types beyond Finish-to-Start, cost or earned value, import/export of MS Project or P6 files.

**Implementation note.** Render with a purpose-built SVG/Canvas component or a lightweight library; avoid heavyweight commercial Gantt components whose feature surface would tempt scope creep and whose licensing is a business decision.

---

### 12.17 Dashboards and My Work

Module-level behaviour for the Project Dashboard, Portfolio Dashboard, and My Work is fully specified in their screen specifications (§13.1, §13.12, §13.10) because they are composed entirely of the modules above. The shared principle: every number on a dashboard is a link to the filtered list that produced it, and every colour has a visible reason.

---

### 12.18 Project Assignments, Following, and My Staff

**Purpose.** Make project assignment do two things without anyone configuring it: everyone assigned to a project receives all of that project's updates, and every manager can see their direct reports — which projects each person is on, in what role, and what they are carrying, waiting on, and holding up — and staff them on projects.

**Users.** Everyone (following); Supervisors (their supervised staff, §8.8); Executives and System Administrators (all staff).

**Definitions**

- **Project assignment** is membership of the project team (`ProjectMember`, any project role), including Discipline Leads and reviewers auto-added under TM-06. There is no separate assignment entity: the project team is the assignment list.
- **Following** is a per-user, per-project subscription with one of three levels:

| Level | What the user receives for that project |
|---|---|
| **All activity** | Everything in *My items only*, plus every logged change on the project made by someone else (creates, status changes, assignments, date changes, dependency changes, deliverable issues, decisions, milestone changes, comments), shown in the Following feed and summarised in the daily digest. |
| **My items only** | The standard notifications of §17.2 for items the user is assigned, reviews, owns, or watches, and @mentions. This is also the behaviour for users who do not follow the project. |
| **Muted** | Only direct assignments to the user (task, review, decision ownership) and @mentions. |

- **Supervised staff** means the supervisor's direct reports (§8.8).

**Inputs.** `ProjectFollow`: `user_id`, `project_id`, `level` (AllActivity, MyItemsOnly, Muted), `source` (Assignment = set by the system on team assignment; Manual = set or changed by the user), `last_seen_at` (Following feed read marker), `created_at`, `updated_at`. One row per user per project.

**Outputs.** Follow control in the project header; Following tab in the Notification Centre (§13.17); Project updates and My staff digest sections (§17.3); My Staff page (§13.19); Staff Assignments report (§19).

**Business rules**

- ASG-01 **Assignment turns following on.** When a user is added to a project team as PM, Discipline Lead, Team Member, or Viewer, a follow is created at **All activity** with source Assignment. A user auto-added only as Reviewer (TM-06) gets **My items only**, because they are there for one review. An existing follow with source Assignment is raised to All activity when the user gains a role other than Reviewer; a follow with source Manual is never changed by the system.
- ASG-02 **The user's choice sticks.** Users may change their level or unfollow at any time; any change sets source to Manual. Nobody can set another user's follow level (PMs cannot force following, §17.1).
- ASG-03 **Manual following.** Any user who can view a project may follow it (source Manual), for example an Executive tracking a key project. Restricted projects (§8.7) can only be followed by users who can view them.
- ASG-04 **Leaving a team.** When a user is removed from a project team, a follow with source Assignment is deleted; a Manual follow is kept while the user can still view the project. Losing view access deletes any follow.
- ASG-05 **Feed contents.** The Following feed is the Activity History (§13.14) of the user's followed projects at All activity, excluding the user's own actions and filtered by current permissions at read time. It is read from `activity_log`; the Hub does not write a `Notification` row per change per follower. Entries sharing a `correlation_id` (bulk actions, cascades, template instantiation; G-10) collapse into one entry with a count. Unread means after the project's `last_seen_at`; unread counts use the notification centre's polling (§17.6).
- ASG-06 **No duplicates.** An event that already produced a personal notification for the user (§17.2) is shown in the feed with a "notified" marker but is not counted again as unread or repeated in the Project updates digest section.
- ASG-07 **Project status.** Setup: changes are recorded but produce no unread counts or digest content, and on activation every follower's `last_seen_at` is set to the activation time so set-up work does not arrive as a flood. On Hold: the feed continues; the digest section is suppressed (§17.3). Archived and Cancelled: follows are kept but produce nothing.
- ASG-08 **Staff scope.** A Supervisor's staff is their direct reports: active users whose `supervisor_id` is the supervisor, read at request time with no hierarchy traversal. Inactive users are hidden from My Staff by default ("show inactive" toggle); their open work is still flagged by A-18.
- ASG-09 **What managers see.** For each person in scope: their permitted project assignments (project, roles, primary discipline, date added), their permitted My Work read-only (§13.10), and counts from permitted materialised state rows — open tasks, overdue, blocked, blocking others, reviews waiting on them, owned deliverables due within 14 days, last activity. Work on Restricted projects the manager cannot view is excluded from the list and counts (§36.1).
- ASG-10 **What managers can do.** Reassign tasks owned by their direct reports (§8.5.1), and staff them on projects: add a direct report to the team of any project the supervisor can view (Setup, Active, or On Hold) as Team Member with a primary discipline, or remove them from it (the TM-04 reassignment prompt applies to their open items). Each staffing change notifies the project's PM in-app and is logged with the supervisor as actor. Everything else about the team — other roles, Discipline Leads, and people who are not the supervisor's direct reports — stays with the PM (§12.2), who can also remove anyone a supervisor added.
- ASG-11 **Staff assignment notices.** When someone else adds a person to or removes them from a project, or makes them a Discipline Lead, the person's supervisor receives an in-app notification and sees the change in the next digest's My staff section. Supervisors do not receive every change on their staff's work; the person's row in My Staff is the drill-down.

**Permissions.** Follow, unfollow, and change level: any user, for their own follows on projects they can view. My Staff: Supervisors (supervised staff); Executives and System Administrators (any scope, including all staff). Staffing: Supervisors add or remove their direct reports as Team Members (ASG-10); all other team changes remain PM-only. Project Managers without the Supervisor role see their project members through the Team tab as today.

**UI behaviour.** The project header shows a follow control ("Following: All activity ▾") with the tooltip "You follow this project because you are on the team." The "Added to a project" notification says the user now follows the project and how to change the level. My Work → My Projects shows each project's follow level with inline change. The Following tab and the My Staff page are specified in §13.17 and §13.19.

**Dependencies.** Project Team (§12.2), Activity Log (§20), Notifications (§17), My Work (§13.10), supervisor data (§8.8, §23.6).

**Edge cases.** E-25 to E-29.

**Acceptance criteria.** AC-ASG-01 … AC-ASG-09.

> **Design note.** "Everyone on the project gets all updates" is implemented as a feed over the activity log rather than as notifications, because the log is already written in the same transaction as every change (§20.3). All updates therefore cost one table and one query instead of a notification row per member per change, and immediate email stays reserved for events that need a response (§17.1). Assignment switches following on because being on the team is the strongest signal of interest; the three levels exist because a discipline lead on ten projects will want All activity on two of them and Muted on the rest.
