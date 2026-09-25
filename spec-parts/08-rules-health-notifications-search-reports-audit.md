## 15. Business Rules

All rules are deterministic, evaluated server-side, and unit-tested. Rules referenced in earlier sections are consolidated here with their IDs. Where a rule has a threshold, the threshold key from §10.4 is named.

### 15.1 General and date rules (G)

| ID | Rule |
|---|---|
| G-01 | **Overdue**: an item is Overdue when `due_date < today` (or `date` / `required_by_date` for milestones/decisions) **and** its status is not terminal (Complete, Cancelled, Issued, Accepted, Decided) **and** not On Hold **and** its project is Active. |
| G-02 | **Due Soon**: `today ≤ due_date ≤ today + N` where N is the item type's due-soon threshold, and the item is not terminal/On Hold. |
| G-03 | **Today** is the current date in `org_time_zone`. All date comparisons use calendar dates. |
| G-04 | **Terminal statuses**: Task — Complete, Cancelled; Deliverable — Issued (when `Accepted` is not used), Accepted, Cancelled; Milestone — Complete, Cancelled; Decision — Decided, Cancelled; Project — Complete, Archived, Cancelled. |
| G-05 | Items in a project whose status is Setup, On Hold, Complete, Archived, or Cancelled are **not evaluated** for Overdue, Due Soon, Blocked, Stale, attention rules, or health. They retain their stored data and show a banner explaining why. |
| G-06 | All deletions of work items (tasks, deliverables, milestones, decisions, comments, links, dependencies) are **soft deletes** (`deleted_at`, `deleted_by`) with a logged snapshot. Hard deletion is an Admin database operation outside the application, if ever. |
| G-07 | Every mutable entity carries `row_version`; updates must present the version they read; a mismatch is rejected with a conflict (§25.8). |
| G-08 | Item keys (`1234-T0042`) are immutable once assigned and never reused. |
| G-09 | The following changes **require a reason** (free text, ≥ 5 characters, stored in the activity log): project status change; task/deliverable On Hold or Cancelled; task reopen; milestone date change (Rec); due date change by a non-PM/DL (Rec); health override; decision deferral or cancellation; comment deletion by PM; bulk date shift. |
| G-10 | System-generated changes (rules engine, template instantiation, cascades) are logged with actor = System and a `correlation_id` linking them to the triggering user action. |
| G-11 | Users flagged Inactive cannot be assigned or selected as owner/reviewer; existing references are retained and flagged (A-18). |

### 15.2 Project rules (P)

| ID | Rule |
|---|---|
| P-01 | `project_number` is unique (case-insensitive, trimmed) and must match the configured format. |
| P-02 | Status transitions follow §10.2. Any other transition is rejected. |
| P-03 | Transition to On Hold, Cancelled, or Complete requires a reason. |
| P-04 | Transition to Complete presents the closeout checklist (open tasks, un-issued deliverables, open decisions, P2 registers); the PM chooses per group to cancel all (with reason) or leave open; the choice is logged. |
| P-05 | A Complete project remains editable by PM for `complete_project_edit_window_days`; after that the dashboard recommends Archive. Editing after the window is still permitted (with reasons) until Archived. |
| P-06 | Archived and Cancelled projects are read-only for all users; Admin may Unarchive (→ Complete). |
| P-07 | Changing `project_number` is Admin-only; item keys retain the original number (display uses the current number with the original in a tooltip). |
| P-08 | The user in `project_manager_id` always holds the PM project role and cannot be removed from the team. |
| P-09 | Setup → Active requires at least one milestone **or** one task (Recommendation: advisory warning only, not a hard rule). |

### 15.3 Team rules (TM)

TM-01 to TM-06 as in §12.2. Additionally:

| ID | Rule |
|---|---|
| TM-07 | A Discipline Lead must be an active user; when a lead becomes Inactive, A-18 fires for the discipline. |
| TM-08 | A user's project roles are removed when they are removed from the team; their historical references remain. |

### 15.4 Milestone rules (M)

M-01 to M-08 as in §12.3. Additionally:

| ID | Rule |
|---|---|
| M-09 | A milestone may be marked Complete only if `date ≤ today + 7` (Recommendation: prevents accidental early completion of far-future milestones; PM can move the date first). |
| M-10 | Reopening a Complete milestone (PM, reason) clears `completed_date` and restores derived status. |

### 15.5 Deliverable rules (DL)

DL-01 to DL-10 as in §12.4. Additionally:

| ID | Rule |
|---|---|
| DL-11 | Deliverable Overdue per G-01 uses `due_date` and terminal statuses Issued/Accepted/Cancelled. |
| DL-12 | If a task under an Issued deliverable is reopened or created, the deliverable shows "Issued with open work"; status is not changed automatically. |
| DL-13 | Deliverable `start_date` must be ≤ `due_date` when both set. |

### 15.6 Task rules (T)

T-01 to T-18 as in §12.5. Additionally:

| ID | Rule |
|---|---|
| T-19 | `start_date ≤ due_date` when both set (validation). |
| T-20 | Progress is stored as an integer 0–100 in steps of 10; Complete forces 100; reopen sets 90 (Recommendation). |
| T-21 | A task moved to another deliverable keeps its dependencies; its milestone context changes to the new deliverable's milestone. |
| T-22 | A task's `discipline_id` must be one of the project's active disciplines. |
| T-23 | Bulk actions apply row-by-row with per-row permission checks and produce a summary; a failed row never blocks others. |

### 15.7 Review rules (R)

R-01 to R-05 as in §12.5.

### 15.8 Dependency rules (D)

D-01 to D-18 as in §12.6.

### 15.9 Decision rules (DEC)

DEC-01 to DEC-07 as in §12.9.

### 15.10 Comment and document rules (C, DOC)

C-01 to C-08 as in §12.8; DOC-01 to DOC-04 as in §12.7.

### 15.11 Attention engine rules (ATT)

| ID | Rule |
|---|---|
| ATT-01 | Rules A-01…A-20 (§12.12) are evaluated only for Active projects. |
| ATT-02 | An attention item is uniquely identified by (`rule_id`, `item_type`, `item_id`); it persists (with `first_detected_at`) while its condition holds and disappears when it clears. |
| ATT-03 | Snooze (PM/DL) hides an item for 1–30 days with a mandatory note; it is logged; the item reappears at expiry or if its severity increases. |
| ATT-04 | Ranking: severity (Critical > Warning > Info), then `days_overdue`/`days_blocked` descending, then priority, then due date ascending. |
| ATT-05 | Routing sets are computed from current roles at evaluation time (a new PM inherits the items). |

### 15.12 Worked examples

**Example 1 — Overdue predecessor.** Task T0031 (Geotech recommendations) due 2026-09-10, In Progress. Task T0042 (Civil detailed grading) start 2026-09-15, due 2026-09-30, depends on T0031. On 2026-09-11: T0031 Overdue (G-01). T0042: unsatisfied predecessor and predecessor Overdue → Blocked (D-06 b). T0031: Blocking Others (D-11). Attention: A-01 for T0031 (Warning), A-03 for T0031 (Critical), A-02 for T0042 (Warning). Affected milestone for both: 60% Design Submission (D-13).

**Example 2 — Normal waiting.** Same tasks on 2026-09-05: T0031 not overdue; T0042 start in future; not In Progress; due not soon → T0042 is **Waiting**, not Blocked; no attention item.

**Example 3 — Due-soon override.** T0042 due 2026-09-17, today 2026-09-14, T0031 incomplete but not overdue (due 2026-09-16). `due − today = 3 ≤ task_due_soon_days (5)` → T0042 Blocked (D-06 c) even though the predecessor is not yet overdue; this is the "you will not make it" signal.

### 15.13 Assignment and following rules (ASG)

ASG-01 to ASG-11 as in §12.18.

---

## 16. Project Health Logic

### 16.1 Principles

1. Health is a **summary of indicators**, not a prediction. The interface never says "likely to be late"; it says "2 overdue deliverables; milestone at risk".
2. Health is computed from a small set of stated inputs. Every input is visible in the "Why?" popover.
3. The PM may **override** the reported health with a note, for a bounded time. Both values are always visible where health is shown to management.
4. Health is only computed for Active projects; everything else is Grey.

### 16.2 Milestone status rules

Evaluated for each milestone that is not Complete or Cancelled, in a project that is Active. Let D = milestone `date`; T = today; A = `milestone_approaching_days`.

| Status | Condition (first match wins) |
|---|---|
| **Overdue** | `D < T` |
| **At Risk** | `D ≥ T` and any of: (a) any targeted deliverable is Overdue; (b) any task under a targeted deliverable (or directly targeting the milestone) is Overdue or Blocked; (c) `D − T ≤ A` and any targeted deliverable is not Issued/Accepted/Cancelled and has `progress_pct < 100`; (d) `D − T ≤ A` and the milestone is a submission type with zero targeted deliverables (nothing planned to meet it); (e) any targeted deliverable has `due_date > D` (planned to be late). |
| **On Track** | Otherwise. |

Milestones with no targeted deliverables and no tasks (e.g., "Kickoff") are On Track until Overdue, except for (d).

### 16.3 Project health computation

Inputs (all counts consider only non-terminal, non-On-Hold items in an Active project):

| Input | Definition |
|---|---|
| `ms_overdue` | Number of milestones Overdue |
| `ms_at_risk` | Number of milestones At Risk |
| `del_overdue` | Number of deliverables Overdue |
| `del_overdue_5` | Deliverables Overdue by more than 5 days |
| `open_tasks` | Tasks not Complete/Cancelled/On Hold |
| `task_overdue` | Tasks Overdue |
| `task_overdue_pct` | `task_overdue / open_tasks` (0 if `open_tasks` = 0) |
| `task_blocked_long` | Tasks Blocked for more than `health_blocked_days_yellow` days |
| `dec_overdue` | Decisions Overdue |
| `dec_overdue_blocking` | Decisions Overdue that block ≥ 1 task |
| `issue_high` [P2] | Issues Open/In Progress with severity High |
| `inactive_owner` | Items with Inactive owner/assignee (A-18 count) |

Rules (evaluate Red first, then Yellow, else Green):

| Colour | Condition |
|---|---|
| **Grey** | Project not Active, **or** (`open_tasks` = 0 and no non-complete milestones) — nothing to evaluate |
| **Red** | `ms_overdue ≥ 1` **or** `del_overdue_5 ≥ 1` **or** (`task_overdue_pct ≥ health_overdue_task_pct_red` and `task_overdue ≥ health_overdue_task_min_yellow`) **or** `dec_overdue_blocking ≥ 1` **or** `issue_high ≥ 1` [P2] |
| **Yellow** | `ms_at_risk ≥ 1` **or** `del_overdue ≥ 1` **or** (`task_overdue_pct ≥ health_overdue_task_pct_yellow` and `task_overdue ≥ health_overdue_task_min_yellow`) **or** `task_blocked_long ≥ 1` **or** `dec_overdue ≥ 1` **or** `inactive_owner ≥ 1` |
| **Green** | Otherwise |

The "Why?" popover lists each input that is non-zero with its value and the colour it contributes ("Milestone at risk: 60% Design Submission (9 days, 2/5 deliverables issued) → Yellow"; "Overdue tasks: 7 of 41 (17%) → Yellow").

### 16.4 Manual override

- PM may set `health_override` ∈ {Green, Yellow, Red} with a mandatory `health_override_note` (≥ 20 characters recommended by placeholder: "Client has agreed a two-week extension to 60%; revised dates being entered").
- `health_override_expires_at = now + health_override_expiry_days`. On expiry, the override is removed automatically (logged by System) and A-15 informs the PM.
- Override cannot set Grey.
- Wherever health is shown to anyone other than project members (Portfolio, Project List, reports), **both** are shown when they differ: "Computed Yellow · Reported Green (PM note, 3 days ago)". On the Project Dashboard, reported is primary with computed beside it.
- Setting, changing, and clearing overrides is logged.

**Evaluation of the override.** An override is useful because computed health lags reality (the extension is agreed before dates are re-entered) and because context matters (one overdue low-value deliverable should not paint a project red for a month). It is dangerous if it can hide problems indefinitely, which is why it expires and why computed health is never hidden from management. Recommendation: include it in MVP as specified.

### 16.5 Health snapshots

Nightly, for every Active project, store `ProjectHealthSnapshot` (`project_id`, `date`, `computed_health`, `reported_health`, the input values). This enables the first-release Portfolio trend sparkline and "how long has this been red" without recomputing history. Storing and display are required for the first release under §36.

### 16.6 Discipline status

The same rules as 16.3 are applied to the subset of items owned by a discipline within a project (milestones considered are those whose targeted deliverables include the discipline's deliverables). Result shown on the dashboard discipline table and in the Weekly Coordination discipline round. No override at discipline level.

### 16.7 Recalculation triggers

Health and milestone status are recomputed (a) on demand when a dashboard/portfolio/list requests them (cached for up to 60 seconds per project), (b) after any write to tasks, deliverables, milestones, decisions, dependencies, or project status in that project (invalidating the cache), and (c) nightly for snapshots and date-driven changes (things become overdue at midnight without anyone touching them). See §23.5.

---

## 17. Notification Logic

### 17.1 Principles

- Notify people about **their** work and about **changes others made** to things they own or watch. Never notify a user about their own actions.
- Prefer **one daily digest** for date-driven conditions (due soon, overdue, stale) over per-item emails.
- **Immediate** notifications are reserved for events that require a response or unblock someone.
- Every notification links to the item and states the project.
- Users can turn any channel off per event type; PMs cannot force notifications on others.
- Nothing is sent to external parties in MVP.
- **Following is how "all updates" works.** Being assigned to a project follows it (§12.18). Following at All activity delivers every change others make on the project to the in-app Following feed and the daily digest, never as one email per change.

### 17.2 Event catalogue and defaults

Channels: **App** = in-app notification centre; **Email** = immediate email; **Digest** = included in the daily email digest.

| Event | Recipients | App | Email | Digest |
|---|---|---|---|---|
| Task assigned to you / reassigned to you | Assignee | ● | ● | |
| You were set as reviewer | Reviewer | ● | ● | |
| Review requested (task Ready for Review) | Reviewer | ● | ● | |
| Review outcome (Complete / Revision Required) | Assignee | ● | ● | |
| Your task became Blocked (dependency, decision, or manual block set by someone else) | Assignee | ● | | ● |
| Your task became unblocked ("you can start") | Assignee | ● | ● | |
| Your task is blocking others and is overdue (A-03) | Predecessor assignee | ● | ● (once, on first detection) | ● |
| Due date changed on your task (by someone else) | Assignee, Reviewer | ● | | ● |
| Task approaching due date (Due Soon) | Assignee | | | ● |
| Task overdue | Assignee | | | ● (daily while overdue) |
| Deliverable you own approaching due / overdue | Owner | | | ● |
| Comment on an item you own/watch | Owner, watchers | ● | | |
| @mention | Mentioned user | ● | ● | |
| Decision assigned to you (internal owner) | Owner | ● | ● | |
| Decision you requested/own approaching or overdue | Requester, owner, PM | ● (overdue only) | | ● |
| Decision recorded (Decided) on a decision linked to your task | Linked task assignees, requester | ● | ● | |
| Milestone approaching (within `milestone_approaching_days`) | PM, DLs with targeted deliverables | | | ● |
| Milestone At Risk / Overdue (status change) | PM, DLs | ● | | ● |
| Milestone date changed | PM, all DLs | ● | | ● |
| Added to a project / role changed | User | ● | ● | |
| You became Discipline Lead | User | ● | ● | |
| Project status changed (On Hold, Active, Complete) | All members | ● | | |
| Attention items routed to you (any new Critical) | Routed users | ● | | ● |
| Health override set/expired | PM | ● | | |
| Work reassigned away from you | Previous assignee | ● | | |
| Predecessor of your task was deleted / dependency removed | Successor assignee | ● | | |
| Any change by someone else on a project you follow at All activity | Followers | ● (Following tab) | | ● (Project updates) |
| Someone else added one of your direct reports to or removed them from a project, or made them Discipline Lead | Supervisor | ● | | ● (My staff) |
| A Supervisor added or removed one of their direct reports on your project team | PM | ● | | ● |

### 17.3 Daily digest

One email per user per day at `digest_send_time_local` (default 07:00), only if there is content. Sections, each capped at 10 rows with "and n more" linking to My Work: Overdue (mine) · Due in the next N days (mine) · Blocked (mine) with blockers · Reviews waiting on me · Decisions I own/requested due or overdue · For PMs and DLs: attention items Critical/Warning per project · Milestones approaching in my projects · **Project updates**: for each project followed at All activity, counts of changes by type since the last digest and the five most important (status, assignment, date, deliverable issued, decision), linking to the Following tab · **For Supervisors — My staff**: direct reports with overdue or blocked work or reviews waiting beyond `review_stale_days`, and assignment changes made by others since the last digest. Subject line: "Hub digest — 3 overdue, 2 reviews, 1 blocked" (plus "· 14 project updates" when that section has content). No digest for projects in Setup/On Hold. Weekend digests are suppressed by default (Recommendation).

### 17.4 Preferences

Per user: each event type → App / Email / Off (Digest is a single on/off with time). Per project follow level (§12.18): All activity, My items only, or Muted (App and Email off for that project except direct assignments and mentions). Defaults are set by Admin (Settings) and applied to new users; changes to defaults do not overwrite existing user choices.

### 17.5 Suppression and de-duplication

- No self-notifications.
- Collapse: multiple changes to the same item by the same actor within 5 minutes produce one in-app notification ("Marc updated 1234-T0042 (3 changes)").
- Digest de-dup: an item appears once per digest in its most severe section.
- Following de-dup: an event that already produced a personal notification for the user is not counted again in the Following feed's unread count or the Project updates digest section (ASG-06).
- Immediate emails for the same event on the same item to the same user are not repeated within 24 hours (e.g., A-03 first detection only).
- Bulk actions produce one notification per recipient summarising the affected items, not one per item.
- Template instantiation in Setup status sends assignment notifications only when the project is activated (batched per user: "You were assigned 12 tasks on 1234").

### 17.6 Delivery

- In-app: `Notification` rows (§24) read by the notification centre; unread count polled every 60 seconds or pushed via server-sent events (Recommendation: polling in MVP).
- Email: sent by a background worker via an outbound email provider. **TBD — Business Decision Required:** Microsoft Graph `sendMail` from a service mailbox (keeps mail in Exchange Online, respects corporate mail policies) vs. Azure Communication Services Email vs. SMTP relay. Recommendation: Graph with an application permission scoped to a single shared mailbox, subject to IT approval.
- Emails are plain, branded minimally, with the item key in the subject and deep links. No reply-by-email.

### 17.7 Future: Microsoft Teams [Phase 3]

Per-user Teams chat notifications (Graph) or per-project channel posts via Incoming Webhook/Workflows for the digest and Critical attention items. Designed for by keeping notification rendering separate from channel delivery (`NotificationRenderer` → `Channel` adapters).

---

## 18. Search and Filtering

### 18.1 Global search

- Single search box in the top bar; results in a dropdown grouped by type (Projects, Tasks, Deliverables, Milestones, Decisions, People, P2 registers) with the top 5 per group and "See all results" opening a results page with type tabs.
- Scope: project number, project name, client name, task key/name, deliverable key/name, milestone key/name, decision key/subject, user display name/email; **not** comment bodies or descriptions in MVP (Recommendation: add description/comment search in P2 if requested; keeps the index small and results predictable).
- Permission-filtered: only items in projects the user can view (matters if Restricted is enabled).
- Default excludes Archived/Cancelled projects; toggle "include archived".
- **Key lookup**: input matching an item key pattern (`\d+-(T|D|M|DEC|R|I|A)\d+`) opens that item directly on Enter.
- Ranking: exact key match; then project number prefix; then name prefix; then full-text rank; recency as a tiebreaker. Active projects ranked above others.

### 18.2 Technical approach

PostgreSQL full-text search (`tsvector` columns maintained by trigger or application code, GIN indexed) plus `pg_trgm` for prefix/substring matching on names and keys. No external search service in MVP; revisit only if latency exceeds targets at scale (§22).

### 18.3 List filtering model

Every list endpoint accepts filters as query parameters (§25.5). The UI's filter bar maps one-to-one to these parameters so URLs are shareable and reproducible. Filter operators: equals / in-list (enums, IDs), date range (`dueFrom`, `dueTo`), boolean indicators (`overdue=true`, `blocked=true`), text `q` within the list. Filters combine with AND; multi-value fields are OR within the field.

### 18.4 Saved views [First release under §36]

`SavedView`: `owner_id`, `scope` (Personal | Project), `project_id` (nullable), `list_type` (Tasks, Deliverables, Decisions, Projects…), `name`, `filters` (JSON), `sort`, `columns`, `group_by`, `is_default`. Personal views appear in the user's view switcher; Project views (created by PM/DL) appear for all project members. Views store filter definitions, not results.

---

## 19. Reporting

All reports are deterministic queries over current data (or snapshots where noted), presented as tables with the same components as lists, and exportable to CSV and XLSX. Reports respect permissions. A report that maps to a list offers "Open as filtered list".

| Report | Parameters | Columns (abridged) | Scope | Phase |
|---|---|---|---|---|
| Tasks Due This Week | project(s), discipline, assignee, week | Key, task, project, deliverable, discipline, assignee, due, status, indicators | Project / My / All | MVP |
| Overdue Tasks | project(s), discipline, assignee, min days overdue | + days overdue, blocking count, affected milestone | Project / All | MVP |
| Blocked Tasks | project(s), discipline, blocker type | Key, task, assignee, blocked since, days, blockers (task/decision/manual), affected milestones | Project / All | MVP |
| Tasks Blocking Others | project(s) | Key, task, assignee, due, status, successors count, successors' milestones | Project / All | MVP |
| Upcoming Deliverables | project(s), discipline, days ahead | Key, deliverable, discipline, owner, milestone, due, status, progress, indicators | Project / All | MVP |
| Deliverable Status by Project | project(s), discipline | Deliverable rows grouped by project with status, due, issued date, revision | All | MVP |
| Upcoming Milestones | project(s), days ahead, type | Project, milestone, type, date, status, slip, deliverables issued/total, open tasks | All | MVP |
| Open Decisions | project(s), owner type, overdue only | Key, subject, owner, requested, required by, days, impact, blocking count | Project / All | MVP (if register in MVP) |
| Review Queue | reviewer, project(s) | Key, task/deliverable, assignee, ready since, days waiting, round | My / All | MVP |
| Stale Work | project(s), days | Key, task, assignee, last activity, days | Project / All | MVP |
| Project Activity Log | project, date range, action types | Timestamp, actor, action, item, change | Project | MVP (Rec) |
| Staff Assignments | scope (direct reports / all staff), discipline, office, project, role | Person, project, project status, role(s), primary discipline, added on, added by, open and overdue tasks on the project | Staff | MVP (Rec) |
| Projects At Risk | office, PM, health | Project, PM, computed/reported health, why, next submission | Portfolio | First release (§36) |
| Open Issues / High Risks | project(s), severity | Register rows | Project / All | P2 |
| Meeting Actions Outstanding | project, owner type | Action rows | Project | P2 |
| Workload by Employee | supervisor, discipline, weeks | Person, week columns (hours/capacity), open tasks, unestimated, overdue | Resources | First release (§36) |
| Workload by Discipline | office, weeks | Discipline, week columns aggregated | Resources | First release (§36) |
| Health History | project(s), date range | Date, computed, reported (from snapshots) | Portfolio | First release (§36) |
| Task Hours | project(s), task, person, work-date range | Work date, person, project, task, actual hours, note, total hours | My / Project / Staff (permission-filtered) | First release (§36.8) |
| Attention Items Export | project(s), severity | Rule, severity, item, why, owner, age | Project / All | P2 |

**Export rules.** CSV is UTF-8 with BOM (Excel-friendly); XLSX has a header row, frozen pane, date-typed columns, and a "Parameters" sheet recording filters and generation time. Exports are limited to 50,000 rows per request (larger runs split by project). Exports are logged (who exported which report when) but not their content.

**Not built.** Custom report builder, pivot tables, scheduled report emails (P3 if requested), charts beyond the milestone strip and sparkline.

---

## 20. Audit Requirements

### 20.1 What is logged

| Category | Events |
|---|---|
| Lifecycle | Create, soft-delete, restore of any work item, register item, comment, link, dependency, project, membership |
| Ownership | Assignee, reviewer, owner, Discipline Lead, PM, project membership and role changes |
| Dates | Due, start, milestone date, required-by, issued date, completed date changes (old → new; for milestones also slip) |
| Status | Every status transition (old → new, reason if given) for tasks, deliverables, milestones, decisions, projects, P2 registers |
| Structure | Task moved between deliverables/disciplines; deliverable retargeted to another milestone; dependency added/removed; template instantiation |
| Decisions | Raised, deferred (date change), decided (text), reopened, cancelled |
| Health | Override set/changed/cleared/expired; nightly computed value (via snapshot) |
| Attention | Snooze set/expired |
| Admin | Reference data changes; threshold changes; system role changes; user activation/deactivation |
| Access (minimal) | Sign-in events (success), export events |
| Not logged | Views/reads of items; comment body edits within the 15-minute window (only "edited" flag); search queries; follow level changes and feed read markers (personal preferences) |

### 20.2 Structure

`ActivityLog`: `id`, `occurred_at`, `actor_user_id` (nullable for System), `actor_type` (User | System | Admin), `project_id` (nullable for org-level), `item_type`, `item_id`, `item_key`, `action` (enum), `changes` (JSONB array of `{field, old, new}`), `reason` (text), `correlation_id`, `source` (UI | API | Job | Migration), `snapshot` (JSONB, on delete only), `ip_hash` (optional, Recommendation: omit).

### 20.3 Properties

- **Append-only.** No application code path updates or deletes rows; the database role used by the application has INSERT and SELECT only on the table (Recommendation).
- **Same transaction.** Log rows are written in the same database transaction as the change they describe, so a logged change always happened and a change is never unlogged.
- **Human-readable.** The UI renders `changes` as "Due date: 2026-09-10 → 2026-09-17" and uses display names resolved at render time (IDs stored).
- **Retention.** Same as the project (indefinite by default; **TBD — Business Decision Required**: retention policy).
- **Access.** Project members and management see project logs; Admin sees org-level logs; nobody can edit.
- **Export.** CSV export per project (Rec).

### 20.4 Implementation approach

Logging is performed in the application service layer (a single `ActivityLogger` invoked by every command handler with the before/after entity state), not by database triggers, so that reasons, correlation IDs, actor context, and human-meaningful action names are available. Field-level diffs are computed generically from the entity's tracked properties with an explicit allow-list per entity (to avoid logging noise such as `updated_at`). Unit tests assert that each command produces the expected log entries.
