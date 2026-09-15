## 12. Module Specifications

Each module is specified with purpose, users, inputs, outputs, business rules, permissions, UI behaviour, dependencies on other modules, edge cases, and acceptance criteria. Rule IDs refer to Section 15; acceptance criteria IDs refer to Section 31; edge cases refer to Section 32.

### 12.1 Project Management

**Purpose.** Hold the identity, team, status, phase, dates, health, and key links of a project; act as the container and permission boundary for all work.

**Users.** PM (create/edit), all users (view), Executives/Supervisors (portfolio), Admin.

**Inputs — project fields**

| Field | Type | Required | Notes |
|---|---|---|---|
| `project_number` | text, unique | Yes | Format validated by a configurable regular expression (**TBD — Business Decision Required**: numbering scheme and source; MVP is manual entry). |
| `name` | text (200) | Yes | |
| `client_id` | FK Client | Yes | Recommendation: allow "Internal" client for internal projects. |
| `client_reference` | text | No | Client's PO / contract / project reference. |
| `project_manager_id` | FK User | Yes | Primary PM; also granted PM project role. |
| `office_id` | FK Office | Yes | Lead office; used for portfolio filtering and time zone (future). |
| `project_type_id` | FK ProjectType | Recommended | E.g., Municipal Infrastructure, Building, Environmental Assessment. Drives template suggestions and portfolio filters. |
| `description` | text (long) | No | Scope summary. |
| `location` | text | No | Free text (municipality, address, or site name). Recommendation: no map integration in MVP. |
| `status` | enum (§10.2) | Yes | Default `Setup` if that status is adopted, otherwise `Active`. |
| `phase_id` | FK Phase | No | Current phase; PM-set. |
| `start_date` | date | Recommended | |
| `target_completion_date` | date | Recommended | |
| `visibility` | enum Open/Restricted | Yes (default Open) | §8.7 |
| `internal_notes` | text (long) | No | Not shown on portfolio; visible to project members and management. |
| `coordination_day` | weekday | No | Day the weekly coordination meeting is held; drives the "this week" window (Recommendation). |
| `health_override`, `health_override_note`, `health_override_by`, `health_override_at`, `health_override_expires_at` | see §16.4 | No | |
| `created_from_template_id`, `template_version` | FK / int | No | Set on template instantiation. |
| `important_links` | child rows (`ProjectLink`: title, url, link_type) | No | SharePoint site, Teams channel, network folder, client portal. |
| Derived | `health_computed`, `next_milestone`, `next_submission_milestone`, task/deliverable/decision counts, discipline summaries, `last_coordination_reviewed_at` | — | Computed |

**Outputs.** Project header (used on every project screen), project card in list, portfolio row, dashboard metrics, activity log entries.

**Business rules.** P-01 unique project number; P-02 status transitions (§10.2); P-03 status change to On Hold/Cancelled/Complete requires reason; P-04 Complete requires closeout checklist confirmation (open tasks, deliverables not Issued/Accepted/Cancelled, pending decisions are listed; PM confirms and items are auto-cancelled or left as-is at PM's choice, all logged); P-05 Complete projects remain editable by PM for `complete_project_edit_window_days`, after which the dashboard suggests Archive; P-06 Archived projects are read-only; P-07 project number change is Admin-only and logged; P-08 primary PM must hold the PM project role (enforced automatically).

**Permissions.** §8.5.1 create; §8.5.2 edit/status/archive.

**UI behaviour.** Create-project dialog is a two-step form (identity → team & disciplines) with an option "Start from template" (P2). Project header shows key, name, client, PM, phase, status pill, health pill (with "why" popover), next milestone with countdown, and quick links. Status change opens a confirmation dialog with reason field and consequence text ("Putting this project on hold will stop overdue and attention evaluation for 42 open tasks.").

**Dependencies.** Users/Org reference data; Teams & Disciplines; Health (§16).

**Edge cases.** E-02 PM change; E-05 project on hold; E-11 completed project needs correction; E-13 duplicate project numbers.

**Acceptance criteria.** AC-PRJ-01 … AC-PRJ-06.

---

### 12.2 Project Team and Disciplines

**Purpose.** Define which disciplines are active on a project, who leads each, and who is on the team with what project role. Provide per-discipline status roll-ups.

**Users.** PM (manage), DL (view own discipline summary), everyone (view).

**Inputs**

| Entity | Fields |
|---|---|
| `ProjectDiscipline` | `project_id`, `discipline_id`, `lead_user_id` (nullable during setup; flagged if null on Active project), `sort_order`, `is_active` |
| `ProjectMember` | `project_id`, `user_id`, `roles[]` (PM, Team Member, Reviewer, Viewer), `primary_discipline_id` (optional), `added_by`, `added_at`, `removed_at` (soft) |

Discipline Lead is not stored on `ProjectMember`; it is derived from `ProjectDiscipline.lead_user_id`. The lead is auto-added as a member.

**Outputs.** Team list; discipline chips with lead; discipline summary table: per discipline → open tasks, overdue, blocked, waiting, deliverables due in next 14 days, next due item, DL name, and a discipline status colour derived by the same rules as project health but scoped to the discipline's items (§16.6).

**Business rules.** TM-01 a user appears once per project; TM-02 a discipline appears once per project; TM-03 the primary PM cannot be removed from the team (change the PM first); TM-04 removing a member who owns open items prompts for reassignment (bulk reassign dialog) or leaves items assigned with an "Inactive on project" indicator (PM's choice, logged); TM-05 removing a discipline is only allowed if it has no non-cancelled deliverables or tasks (otherwise deactivate: hidden from pickers, existing items retained); TM-06 assigning work to a non-member auto-adds them as Team Member (task) or Reviewer (review) and notifies the PM (in-app).

**Permissions.** PM only for changes.

**UI behaviour.** Team tab with two panels: Disciplines (with lead picker) and Members (with role checkboxes and primary discipline). People picker searches active users by name/email; shows office and job title. Adding a lead who is not on the team adds them in one action.

**Dependencies.** Users; Disciplines reference data.

**Edge cases.** E-01 employee leaves; E-02 PM change; E-06 task in multiple disciplines (single owning discipline; collaborators from other disciplines).

**Acceptance criteria.** AC-TEAM-01 … AC-TEAM-04.

---

### 12.3 Milestones

**Purpose.** Represent dated checkpoints — especially design submissions — and roll up whether the work targeting them is on track.

**Users.** PM (create/edit/complete), everyone (view), Executives (portfolio next milestone).

**Inputs**

| Field | Type | Required | Notes |
|---|---|---|---|
| `key` | generated | — | `1234-M03` |
| `name` | text | Yes | e.g., "60% Design Submission" |
| `milestone_type` | enum: Kickoff, Field Work, Design Submission, Client Workshop, Permit Submission, Tender, Construction, IFC, Record Drawings, Closeout, Other | Yes | `Design Submission`, `Permit Submission`, `Tender`, `IFC` are treated as **submissions** for "Next Submission" logic. |
| `date` | date | Yes | Current planned date. |
| `original_date` | date | auto | Set to `date` on creation; never changes afterwards. Slip = `date − original_date`. |
| `description` | text | No | |
| `discipline_id` | FK | No | Tag for discipline-specific milestones. |
| `completes_phase_id` | FK Phase | No | Suggests phase advance on completion. |
| `is_client_facing` | bool | No | Shown on portfolio "upcoming submissions" when true or type is a submission. |
| `status` | derived + `is_complete`, `completed_date`, `is_cancelled` | — | §16.2 |

**Outputs.** Milestone list and timeline; status pill with "why" (rule fired); linked deliverables with status; prerequisite completeness (% of linked deliverables Issued/Accepted, % of tasks under them Complete); days remaining / days overdue; slip.

**Business rules.** M-01 date required; M-02 date change logs old/new and notifies PM and all DLs (in-app) with the delta; M-03 after a date change, deliverables targeting the milestone with `due_date > milestone.date` are flagged Date Inconsistent; M-04 optional cascade: PM may shift the due dates of all deliverables (and, P2, their tasks) targeting the milestone by the same delta after a preview list — each shifted date is logged individually; M-05 Complete with un-issued deliverables requires confirmation; M-06 Complete sets `completed_date` = today by default (editable to a past date, not future); M-07 Cancelled milestones keep links but are excluded from status evaluation; deliverables that targeted them are flagged "Milestone cancelled — retarget"; M-08 "Next Milestone" = earliest non-complete, non-cancelled milestone by date; "Next Submission" = the same restricted to submission types.

**Permissions.** PM for all changes. DLs may comment.

**UI behaviour.** Milestone view (§13.7) has a horizontal strip at the top of the Project Dashboard showing the next 5 milestones as diamonds with status colour and countdown. Date edits show slip after save. Completing a milestone with `completes_phase_id` shows: "Advance project phase to Detailed Design?" (Yes/No; never automatic).

**Dependencies.** Deliverables (for status roll-up), Phases.

**Edge cases.** E-09 milestone moves; cancelled milestone with linked deliverables.

**Acceptance criteria.** AC-MS-01 … AC-MS-06.

---

### 12.4 Engineering Deliverables Register

**Purpose.** Track the engineering products the project must produce — drawings, reports, calculations, estimates, packages — separately from the tasks that produce them, through a lifecycle that includes internal review and issue.

**Users.** DL (create/manage within discipline), PM (all), deliverable owner (update), reviewers, everyone (view).

**Inputs**

| Field | Type | Required | Notes |
|---|---|---|---|
| `key` | generated | — | `1234-D012` |
| `name` | text | Yes | e.g., "85% Civil Drawing Package" |
| `discipline_id` | FK ProjectDiscipline | Yes | Owning discipline. |
| `deliverable_type_id` | FK DeliverableType | Yes | Drawing Package, Report, Specification, Calculation, Cost Estimate, Quantity Estimate, Permit Submission, Tender Package, IFC Package, Record Drawings, Certification, Memo, Model/Base Plan, Other (Admin-managed). |
| `description` | text | No | |
| `owner_id` | FK User | Yes | Accountable person; defaults to DL. |
| `reviewer_id` | FK User | No | Default reviewer for the package-level review. |
| `milestone_id` | FK Milestone | No | Target milestone. |
| `start_date` | date | No | |
| `due_date` | date | Recommended | Defaults to milestone date when a milestone is chosen and due date is blank. |
| `priority` | enum | Yes (default Medium) | |
| `status` | enum §10.2 | Yes | |
| `revision` | text | No | e.g., "Rev A", "Rev 0", "Rev 2". Free text; conventions vary by client. |
| `issued_date`, `issued_to` | date, text | On Issued | |
| `accepted_date` | date | On Accepted | |
| `on_hold_reason`, `cancelled_reason` | text | Conditional | |
| `requires_review` | bool | default true | Whether the package must pass In Review before Ready to Issue. |
| Derived | `progress_pct`, `task_counts` (total/complete/open/overdue/blocked), `estimated_hours_total`, `remaining_hours`, indicators (Overdue, Due Soon, Unassigned, Stale, Date Inconsistent, Slipped), derived predecessors/successors (§12.6) | — | |

**Outputs.** Deliverables Register (§13.6), deliverable detail with task list, milestone roll-up, dashboard counts (Upcoming, At Risk, Complete), reports.

**Progress derivation (DL-07).** If the deliverable has tasks: `progress_pct = complete_tasks / (total_tasks − cancelled_tasks) × 100`, rounded down to the nearest 5. If every non-cancelled task has `estimated_hours`, weight by hours instead. If no tasks exist, progress is shown as "—" and the status alone conveys state. Progress is never manually entered on a deliverable; this keeps deliverable progress honest.

**Deliverable "At Risk" indicator (DL-08).** A deliverable is At Risk when it is not Issued/Accepted/Cancelled/On Hold and any of: it is Overdue; it is due within `deliverable_due_soon_days` and has open tasks that are Overdue or Blocked; it is due within `deliverable_due_soon_days` and progress < 50%; its milestone is Overdue. This indicator feeds the dashboard "Deliverables: At Risk" count.

**Business rules.** DL-01 discipline, type, owner required; DL-02 status transitions: Not Started → In Progress (auto when first task moves to In Progress — Recommendation) → In Review → (Revision Required → In Progress) | Ready to Issue → Issued → Accepted; any non-terminal → On Hold (reason) → back to previous; any → Cancelled (PM/DL, reason); DL-03 due after milestone date → Date Inconsistent warning (not blocked); DL-04 In Review requires a reviewer; if `requires_review` is true, Ready to Issue is reachable only from In Review; DL-05 Issued with open tasks → confirmation listing open tasks; PM/DL may proceed; open tasks remain open and are flagged "Deliverable issued with task open"; DL-06 Issued requires `issued_date` (default today) and `revision` (default from previous +1 is not attempted; free text); DL-07 progress derivation above; DL-08 At Risk indicator above; DL-09 deleting a deliverable that has tasks is not allowed — cancel it, or move its tasks first; DL-10 changing a deliverable's discipline moves its tasks' owning discipline with confirmation.

**Permissions.** §8.5.2.

**UI behaviour.** Register is a dense table grouped by discipline (default) or milestone, with status pills, progress bar (with fraction "6/8 tasks"), due date with indicator, owner avatar, and an expand chevron that reveals the deliverable's tasks inline. Detail is a side panel with tabs: Overview, Tasks, Dependencies (derived), Links, Comments, History. "Issue deliverable" is an explicit action button opening a small dialog (date, revision, issued to, note).

**Dependencies.** Disciplines, Milestones, Tasks, Deliverable Types.

**Edge cases.** Deliverable issued then task reopened (flag, do not revert status automatically); deliverable with no tasks; owner leaves.

**Acceptance criteria.** AC-DEL-01 … AC-DEL-07.

---

### 12.5 Task Management (including Review)

**Purpose.** Track the units of work: who does them, when they are due, what they depend on, whether they are reviewed, and what state they are in.

**Users.** Everyone.

**Inputs**

| Field | Type | Required | Notes |
|---|---|---|---|
| `key` | generated | — | `1234-T0042` |
| `name` | text (200) | Yes | Imperative phrasing encouraged by placeholder text ("Update grading plan for 60%"). |
| `description` | text (long, lightweight markdown: bold, lists, links) | No | |
| `project_id` | FK | Yes | |
| `discipline_id` | FK ProjectDiscipline | Yes | Defaults from deliverable, else from creator's primary discipline. |
| `deliverable_id` | FK | No | Parent deliverable. |
| `milestone_id` | FK | No | Only when `deliverable_id` is null; otherwise derived from deliverable. |
| `assignee_id` | FK User | Recommended | Single accountable person. Null allowed (flagged Unassigned). |
| `reviewer_id` | FK User | Conditional | Required before Ready for Review when `requires_review`. |
| `requires_review` | bool | default false (default true when created from a template task marked review) | |
| `priority` | enum | default Medium | |
| `start_date` | date | No | Used for Blocked evaluation and timeline. |
| `due_date` | date | Recommended | |
| `status` | enum §10.2 | Yes | default Not Started |
| `progress_pct` | int 0–100 step 10 | Yes | default 0 |
| `estimated_hours` | decimal | No | Used by deliverable weighting and Resource View. |
| `manual_block_type` | enum: Client, External Party, Internal, Decision, Information, Other | No | |
| `manual_block_reason` | text | Required when type set | |
| `manual_block_set_at` | timestamp | auto | Drives "blocked for N days". |
| `on_hold_reason`, `cancelled_reason` | text | Conditional | |
| `review_round` | int | auto | Increments on each Revision Required. |
| `due_date_change_count` | int | auto | |
| `last_activity_at` | timestamp | auto | Any field change, status change, or comment. |
| `completed_at` | timestamp | auto | |
| `created_by`, `created_at`, `updated_by`, `updated_at`, `row_version` | audit | auto | |
| `sort_order` | int | auto | Manual ordering within a deliverable. |
| Children | `TaskParticipant` (collaborator/watcher), `TaskDependency`, `Comment`, `DocumentLink`, `ItemLink` (to decisions) | | |
| Derived | indicators (§10.3), `blocked_by[]` (tasks, decisions, manual reason), `blocking[]`, `affected_milestones[]`, `days_overdue`, `days_blocked` | | |

**Outputs.** Task list, Kanban card, task detail panel, My Work rows, dashboard counts, attention items, notifications, activity log.

**Status transitions (T-10 to T-14, summarised)**

| From | To | Allowed for | Conditions |
|---|---|---|---|
| Not Started | In Progress | Assignee, Collaborator, DL, PM | Warning (not block) if task is Blocked: "This task is blocked by 1234-T0031. Start anyway?" |
| In Progress | Ready for Review | Assignee, Collaborator, DL, PM | Only if `requires_review`; reviewer must be set (prompt to set one). |
| In Progress | Complete | Assignee, Collaborator, DL, PM | Only if `requires_review` is false. |
| Ready for Review | In Review | Reviewer, DL, PM | |
| Ready for Review | In Progress | Assignee, DL, PM | Withdraw from review. |
| In Review | Complete | Reviewer, DL, PM | Reviewer approves. Optional comment. |
| In Review | Revision Required | Reviewer, DL, PM | Comment mandatory. `review_round++`. |
| Revision Required | In Progress | Assignee, Collaborator, DL, PM | |
| Any non-terminal | On Hold | Assignee, DL, PM | Reason mandatory. Previous status stored. |
| On Hold | previous status | Assignee, DL, PM | |
| Any non-terminal | Cancelled | DL, PM | Reason mandatory. |
| Complete | In Progress (reopen) | DL, PM, Reviewer | Reason mandatory. Clears `completed_at`; progress set to 90 (Recommendation) so the reopen is visible. Successors re-evaluated. |
| Cancelled | Not Started (restore) | PM | Reason mandatory. |

Setting progress to 100 prompts completion (or Ready for Review if review required). Setting progress > 0 on a Not Started task prompts In Progress. Complete forces progress to 100.

**Business rules.** T-01 single assignee; T-02 discipline required; T-03 due date not required but flagged (A-09) when In Progress or when parent deliverable has a due date; T-04 task due after deliverable due → Date Inconsistent; T-05 Overdue definition (§15 G-01); T-06 On Hold excluded from Overdue counts but shown in a "Held past due date" list; T-07 `last_activity_at` drives Stale; T-08 soft delete only; deleting a task with dependencies removes them and notifies affected assignees and the PM; T-09 cancel preferred over delete once any progress/comments exist (UI offers Cancel first); T-10 – T-14 transitions above; T-15 collaborators may change status/progress but not assignee/due date; T-16 due-date changes by anyone other than PM/DL require a reason (Recommendation) and always notify assignee and reviewer if changed by someone else; T-17 changing deliverable moves the task's milestone context; T-18 task cannot be moved to a deliverable in another project.

**Review rules.** R-01 `requires_review` tasks cannot go In Progress → Complete; R-02 reviewer ≠ assignee unless `allow_self_review`; R-03 Revision Required requires a comment, which is posted as a comment tagged "Review — Round n"; R-04 reviewer change while Ready for Review/In Review notifies both old and new reviewer; R-05 review stalled when Ready for Review or In Review for more than `review_stale_days` (A-11).

**Permissions.** §8.5.2 and §8.6.

**UI behaviour.**
- Task detail opens as a **right-side panel** over the list/board so context is kept; full-page view available via the key link (deep-linkable URL `/projects/1234/tasks/1234-T0042`).
- Header: key, name (inline edit), status pill with transition menu (only allowed transitions shown; disallowed shown greyed with reason on hover), indicators row (Overdue 3d · Blocked · Review round 2).
- "Blocked" indicator is clickable and lists blockers: predecessor tasks (with their assignee, status, due), linked overdue decisions, and manual block reason, each with a link. "Blocking others" likewise.
- Fields in a two-column grid; dates via date picker; people via picker.
- Dependencies section: "Depends on" and "Blocks" lists with add/remove; add uses a search box scoped to the project (excluding tasks that would create a cycle, which are shown disabled with "would create a cycle").
- Comments with @mention; History tab shows field-level changes.
- Quick actions on list rows and cards: change status, reassign, change due date, add comment.
- Bulk actions on the list: assign, set due date / shift by N days, set priority, set deliverable, cancel. Bulk actions respect permissions per row and report "12 updated, 2 skipped (no permission)".

**Dependencies.** Deliverables, Disciplines, Users, Dependencies module, Decisions (for decision blocks), Notifications, Activity Log.

**Edge cases.** E-03 due date changed; E-04 predecessor deleted; E-06 multiple disciplines; E-07 multiple collaborators; E-08 reviewer is assignee; E-10 task reopened; E-01 assignee leaves.

**Acceptance criteria.** AC-TSK-01 … AC-TSK-12; AC-REV-01 … AC-REV-05.

> **Design note.** Two small fields carry disproportionate coordination value: `requires_review` and `due_date_change_count`. The first makes QA/QC a structural part of the workflow instead of a convention, so "done" can never silently mean "done but unreviewed". The second is a deterministic slippage signal: a task whose due date has moved three times is telling you something a single overdue flag cannot. Both cost one column each.

---

### 12.6 Dependencies

**Purpose.** Make cross-discipline hand-offs explicit and compute their consequences: which tasks are waiting, which are blocked, which are blocking others, and which milestones are exposed.

**Users.** PM, DL, assignees (add dependencies for own tasks), everyone (view).

**Inputs**

| Field | Type | Notes |
|---|---|---|
| `predecessor_task_id` | FK Task | The task that must finish first ("blocks"). |
| `successor_task_id` | FK Task | The task that waits ("blocked by"). |
| `dependency_type` | enum | MVP: `FinishToStart` only. Column exists so other types can be added later without migration. |
| `note` | text | Optional: "Need approved base plan, not draft." |
| `created_by`, `created_at` | audit | |

Lag/lead days: **[Phase 2]**. In MVP, represent a waiting period (e.g., client review time) as a task.

**Semantics and derived states**

| Rule | Definition |
|---|---|
| D-01 | A dependency is directed: predecessor → successor. It means "the successor should not be worked until the predecessor is Complete (or Cancelled)". It is advisory for starting work (a warning, not a lock) and authoritative for indicators. |
| D-02 | Both tasks must be in the same project. |
| D-03 | Self-dependencies, duplicate edges, and cycles are rejected at save with the offending path displayed ("T0042 → T0057 → T0061 → T0042"). |
| D-04 | A predecessor is **satisfied** when its status is Complete or Cancelled. A Cancelled predecessor satisfies the dependency but the successor shows an info note "Predecessor cancelled". |
| D-05 | **Waiting**: successor is not terminal/On Hold and has ≥ 1 unsatisfied predecessor, and none of the Blocked conditions in D-06 hold. Waiting is normal and is shown quietly. |
| D-06 | **Blocked (by dependency)**: successor is not terminal/On Hold and has ≥ 1 unsatisfied predecessor P, and at least one of: (a) `successor.start_date ≤ today`; (b) `P` is Overdue; (c) `successor.due_date − today ≤ task_due_soon_days`; (d) successor status is In Progress or later (someone is trying to work it). |
| D-07 | When a predecessor becomes satisfied, all successors are re-evaluated immediately; any that are no longer Waiting/Blocked trigger an "unblocked — you can start" notification to their assignee. |
| D-08 | When a Complete predecessor is reopened, successors are re-evaluated and re-blocked as applicable; assignees and PM are notified. |
| D-09 | Deleting a task removes its dependency edges; successors are re-evaluated; the deletion is logged with the list of removed edges; successor assignees and the PM are notified. |
| D-10 | Deleting a dependency edge is allowed by PM, DL (either end in own discipline), or the successor's assignee, and is logged. |
| D-11 | **Blocking Others**: a task is Blocking Others when ≥ 1 of its successors is Waiting or Blocked because of it. The count and list are shown. Blocking Others plus Overdue on the same task is the highest-severity task condition in the attention engine (A-03). |
| D-12 | **Date inconsistency**: if `successor.start_date < predecessor.due_date` (both set), or `successor.due_date < predecessor.due_date`, the successor is flagged Date Inconsistent with the specific pair shown. This is a warning only. |
| D-13 | **Affected milestones**: for a task X, affected milestones = the milestone of X's deliverable (or X's direct milestone) ∪ the milestones of all transitive successors' deliverables, up to `chain_depth_limit`. Shown when X is Overdue or Blocked. |
| D-14 | **Manual block**: setting `manual_block_type` + reason marks the task Blocked regardless of dependencies until cleared. Manual blocks appear in the blockers list with type and reason and "blocked for N days". |
| D-15 | **Decision block** (Recommended; requires thin Decision Register): a task linked to a Decision (via ItemLink with relation `blocked_by_decision`) whose status is Pending/Under Review/Deferred and whose `required_by_date` < today is Blocked with the decision listed as blocker. A linked decision that is not yet overdue shows as Waiting. |
| D-16 | Adding a dependency where the predecessor is already Complete is allowed (documents the relationship) and has no effect. |
| D-17 | Adding a dependency to a successor that is already Complete is allowed with a warning. |
| D-18 | **Derived deliverable dependency** (Recommended): Deliverable B "depends on" Deliverable A if any task in B has a predecessor in A (A ≠ B). Shown read-only on both deliverables and on the Timeline (P2 arrows). |

**Outputs.** Blockers list on task; "Blocking N" chip; chain view (predecessors up, successors down); affected milestones; dashboard "Blocked" count; attention items A-02, A-03; notifications.

**Permissions.** §8.5.2 "Add / remove task dependency".

**UI behaviour.** In the task panel, "Depends on" lists predecessors with status pill, assignee, due date, and a red/amber marker when unsatisfied. "Blocks" lists successors. A "Show chain" button opens a compact tree (indent per level, up to depth limit) with the current task highlighted. On the Task List, filter "Blocked" and "Blocking others" are one click. On Kanban cards, a small chain icon with count.

**Dependencies.** Tasks, Notifications, Decisions (for D-15).

**Edge cases.** E-04 predecessor deleted; cycle attempted through bulk edit; predecessor moved to another deliverable (edge unaffected); predecessor put On Hold (still unsatisfied; successor indicators unchanged; PM sees On Hold predecessor in blockers list).

**Acceptance criteria.** AC-DEP-01 … AC-DEP-09.
