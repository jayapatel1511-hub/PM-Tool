## 11. Functional Requirements

This catalogue is the ticket-level index of what the system does. Each requirement has a stable ID, a classification, and a pointer to the module specification that details it. Development tickets should reference these IDs.

Classification codes: **R** = MVP-Required, **Rec** = MVP-Recommended, **P2** = Phase 2, **P3** = Phase 3.

### 11.1 Authentication, users, and organisation reference data

| ID | Requirement | Class | Detail |
|---|---|---|---|
| FR-AUTH-01 | Users sign in with their corporate Microsoft Entra ID account (OIDC/OAuth 2.0, PKCE). No local passwords. | R | §23.6 |
| FR-AUTH-02 | A user record is created or updated on first sign-in (just-in-time provisioning) with display name, email, Entra object ID, job title, office (if available). | R | §23.6 |
| FR-AUTH-03 | System roles are derived from Entra ID group membership at sign-in and/or assigned in-app by a System Administrator. | R | §8.8, §34 |
| FR-AUTH-04 | Users disabled in Entra ID cannot sign in; a scheduled sync marks them Inactive in the Hub within 24 hours. | R | §23.6, §32 |
| FR-AUTH-05 | Sessions use short-lived access tokens with silent renewal; idle sign-out after a configurable period. | R | §23.6 |
| FR-ORG-01 | System Administrators maintain Disciplines (name, code, colour, active flag, sort order). | R | §12.2 |
| FR-ORG-02 | System Administrators maintain Clients (name, short name, active flag). | R | §12.1 |
| FR-ORG-03 | System Administrators maintain Offices (name, code, time zone). | R | §12.1 |
| FR-ORG-04 | System Administrators maintain Deliverable Types (name, default discipline optional, active). | R | §12.4 |
| FR-ORG-05 | System Administrators maintain the ordered Phase list. | R | §9.3 |
| FR-ORG-06 | System Administrators maintain organisation thresholds (Section 10.4). | R | §10.4 |
| FR-ORG-07 | System Administrators maintain each user's supervisor and active/inactive state. | R | §8.8 |
| FR-ORG-08 | Any user can create External Parties (name, organisation, email, role, is_client) within a project; PMs edit them. | Rec | §12.9 |

### 11.2 Projects, teams, disciplines

| ID | Requirement | Class | Detail |
|---|---|---|---|
| FR-PRJ-01 | Users with the Project Manager system role (or Admin) can create a project with the fields in §12.1; project number is unique. | R | §12.1 |
| FR-PRJ-02 | The creator becomes the primary Project Manager. | R | §12.1 |
| FR-PRJ-03 | PM can edit project information, links, phase, and internal notes. | R | §12.1 |
| FR-PRJ-04 | PM can change project status per the transition rules; status changes are logged and, where relevant, require a reason. | R | §12.1, §15 |
| FR-PRJ-05 | Projects display computed health and, optionally, a PM override with note and expiry. | R | §16 |
| FR-PRJ-06 | Project list and grouped board support search, filters, sorting, column selection, PM owner, priority, target completion due date, and derived progress. | R | §13.2, §36.2 |
| FR-PRJ-07 | Complete projects can be archived; archived projects are read-only and excluded from default lists but remain searchable. | R | §12.1, §35 |
| FR-PRJ-08 | A per-project `visibility` flag (Open/Restricted) exists in the schema and is enforced; UI control shipped only if required. | Rec | §8.7 |
| FR-TEAM-01 | PM manages the project team: add/remove members, set project roles, set a member's primary discipline. | R | §12.2 |
| FR-TEAM-02 | PM adds disciplines to the project and sets one Discipline Lead per discipline. | R | §12.2 |
| FR-TEAM-03 | Assigning a task or review to a non-member automatically adds them as Team Member or Reviewer, with notification to the PM. | R | §12.2 |
| FR-TEAM-04 | Changing the primary PM re-routes attention items and notifications; the previous PM is retained as a member unless removed. | R | §32 |
| FR-TEAM-05 | Per-discipline status summary (open, overdue, blocked, next due) is computed for the project. | R | §12.2 |

### 11.3 Milestones

| ID | Requirement | Class | Detail |
|---|---|---|---|
| FR-MS-01 | PM creates milestones with name, date, type, description, optional discipline tag, optional completes-phase. | R | §12.3 |
| FR-MS-02 | Milestone status (On Track / At Risk / Overdue / Complete / Cancelled) is derived per Section 16.2; Complete and Cancelled are set by PM. | R | §12.3, §16.2 |
| FR-MS-03 | Original date is captured on creation; slip (days) is shown when the current date differs. | Rec | §12.3 |
| FR-MS-04 | Changing a milestone date logs old/new, notifies PM and Discipline Leads, and flags deliverables/tasks that are now date-inconsistent. | R | §12.3 |
| FR-MS-05 | On a milestone date change, the PM may optionally shift linked deliverable due dates by the same delta after a preview. | Rec | §12.3 |
| FR-MS-06 | Milestone view lists linked deliverables with status and prerequisite completeness. | R | §13.7 |
| FR-MS-07 | Marking a milestone Complete while linked deliverables are not Issued/Accepted/Cancelled requires confirmation and is logged. | R | §15 M-05 |

### 11.4 Deliverables

| ID | Requirement | Class | Detail |
|---|---|---|---|
| FR-DEL-01 | PM or DL creates deliverables with the fields in §12.4 (discipline and type required; owner required; milestone optional). | R | §12.4 |
| FR-DEL-02 | Deliverable status follows the lifecycle in §10.2 with the guards in §15. | R | §12.4 |
| FR-DEL-03 | Deliverable progress is derived from its tasks (percentage complete by count; by estimated hours when all tasks have estimates). | R | §12.4 |
| FR-DEL-04 | Issued state records issued date, revision, and issued-to text. | R | §12.4 |
| FR-DEL-05 | Deliverables Register lists all deliverables with filters by discipline, status, milestone, owner, due window, and indicators. | R | §13.6 |
| FR-DEL-06 | Deliverable due date defaults to its milestone's date and is flagged if later than the milestone. | R | §15 DL-03 |
| FR-DEL-07 | Setting a deliverable to Issued with open tasks requires confirmation; open tasks are listed. | R | §15 DL-05 |
| FR-DEL-08 | Issue history (multiple issues/revisions of one deliverable) is recorded as a list. | P2 | §12.4 |

### 11.5 Tasks and review

| ID | Requirement | Class | Detail |
|---|---|---|---|
| FR-TSK-01 | Authorised users create tasks with the fields in §12.5; discipline required, deliverable optional, single assignee, optional reviewer. | R | §12.5 |
| FR-TSK-02 | Task status follows the workflow in §10.2 with the transition rules in §15. | R | §12.5 |
| FR-TSK-03 | Tasks flagged `requires_review` cannot be completed without passing through review. | R | §15 R-01 |
| FR-TSK-04 | Assignee may not be the reviewer unless `allow_self_review` is enabled. | R | §15 R-02 |
| FR-TSK-05 | Progress percentage (0–100 in steps of 10) is editable by assignee/collaborators; Complete forces 100. | R | §12.5 |
| FR-TSK-06 | Tasks show derived indicators (Overdue, Due Soon, Waiting, Blocked, Blocking Others, Stale, Unassigned, No Due Date, Date Inconsistent). | R | §10.3 |
| FR-TSK-07 | Manual block with type and reason can be set and cleared; while set, the task is Blocked. | R | §12.6 |
| FR-TSK-08 | Collaborators and watchers can be added to a task; collaborators may update progress/status; watchers receive notifications only. | Rec | §12.5 |
| FR-TSK-09 | Task list supports filters (status, assignee, reviewer, discipline, deliverable, milestone, priority, due window, indicators), sorting, grouping, column selection, and bulk actions (assign, due date shift, status). | R | §13.3 |
| FR-TSK-10 | Four-lane Kanban across selected projects, with project-labelled cards, swimlanes, counts, add controls, and guarded transitions. | R | §13.4, §36.3 |
| FR-TSK-11 | Tasks are soft-deleted; deletion is logged with a snapshot; dependencies are removed and affected users notified. | R | §12.5 |
| FR-TSK-12 | Completed tasks can be reopened with a reason; successors are re-evaluated. | R | §15 T-14 |
| FR-TSK-13 | Due-date change count is tracked and shown when ≥ 3. | Rec | §12.5 |
| FR-REV-01 | Assignee marks a task Ready for Review; reviewer is notified and the task appears in My Reviews. | R | §12.5 |
| FR-REV-02 | Reviewer moves to In Review, then Complete or Revision Required (comment mandatory). | R | §12.5 |
| FR-REV-03 | Review round counter increments each time Revision Required is set. | R | §12.5 |
| FR-REV-04 | Stalled reviews (Ready for Review or In Review beyond `review_stale_days`) are flagged. | R | §12.12 A-11 |

### 11.6 Dependencies

| ID | Requirement | Class | Detail |
|---|---|---|---|
| FR-DEP-01 | Users can add Finish-to-Start dependencies between tasks in the same project (predecessor blocks successor). | R | §12.6 |
| FR-DEP-02 | The system rejects self-dependencies, duplicates, and cycles, showing the cycle path. | R | §15 D-03 |
| FR-DEP-03 | Successor tasks show Waiting or Blocked per rule D-05/D-06 and list their blocking tasks. | R | §12.6 |
| FR-DEP-04 | Predecessor tasks show "Blocking N tasks" with the list. | R | §12.6 |
| FR-DEP-05 | Completing or cancelling a predecessor automatically clears dependency blocking on successors and notifies their assignees. | R | §15 D-07 |
| FR-DEP-06 | Overdue predecessors are flagged as blocking; affected milestones are identified via the chain. | R | §15 D-13 |
| FR-DEP-07 | Dependency chain view shows transitive predecessors and successors up to `chain_depth_limit`. | R | §13.3 |
| FR-DEP-08 | Deliverable-level dependencies are derived from task dependencies and displayed on the deliverable. | Rec | §12.6 |
| FR-DEP-09 | Explicit deliverable-to-deliverable dependencies. | P2 | §28 |
| FR-DEP-10 | Cross-project dependencies. | P3 | §29 |

### 11.7 Decisions, risks, issues, meeting actions

| ID | Requirement | Class | Detail |
|---|---|---|---|
| FR-DEC-01 | Users raise decisions with subject, description, requested by, owner (internal user or external party), required-by date, impact if delayed, and links to tasks/deliverables/milestones. | Rec | §12.9 |
| FR-DEC-02 | Decision status follows §10.2; Decided requires decision text and date; Deferred requires a new required-by date. | Rec | §12.9 |
| FR-DEC-03 | Overdue decisions are flagged, appear on the dashboard and Weekly Coordination, and mark linked open tasks as Blocked (Recommendation). | Rec | §15 DEC-04 |
| FR-DEC-04 | Decision Register lists decisions with filters and sorting; decision detail shows history. | Rec | §13.8 |
| FR-RSK-01..05 | Risk Register per §12.10. | P2 | §12.10 |
| FR-ISS-01..05 | Issue Register per §12.10. | P2 | §12.10 |
| FR-MTG-01..05 | Meetings and Meeting Actions per §12.11. | P2 | §12.11 |

### 11.8 Collaboration and documents

| ID | Requirement | Class | Detail |
|---|---|---|---|
| FR-COM-01 | Comments on tasks, deliverables, milestones, decisions (and P2 registers) with @mentions, timestamps, and author. | R | §12.8 |
| FR-COM-02 | Authors may edit their comment within 15 minutes and delete their own comment (soft-delete leaves a placeholder); PMs may delete any comment in their project. | R | §12.8 |
| FR-COM-03 | @mention notifies the mentioned user and adds them as a watcher. | R | §12.8 |
| FR-DOC-01 | Document links (URL, title, type) can be added to projects, deliverables, and tasks; the Hub stores no files. | R | §12.7 |
| FR-DOC-02 | Links to SharePoint/OneDrive/Teams/network paths (UNC) are recognised by pattern and shown with an icon; UNC paths are copyable. | R | §12.7 |
| FR-DOC-03 | File upload/storage. | Out of scope | §30 |

### 11.9 Rules engine, attention, health, dashboards

| ID | Requirement | Class | Detail |
|---|---|---|---|
| FR-ATT-01 | A deterministic attention engine evaluates rules A-01…A-18 (§12.12) per project and produces a ranked list with rule name, severity, item link, and "why". | R | §12.12 |
| FR-ATT-02 | Users with PM or DL role may snooze an attention item for N days with a note; snoozes are logged and expire. | Rec | §12.12 |
| FR-ATT-03 | Attention items are visible on the Project Dashboard, Weekly Coordination, My Work (for items the user owns), and Portfolio (counts). | R | §13 |
| FR-HLT-01 | Project health is computed per §16 and recomputed on relevant changes and nightly. | R | §16 |
| FR-HLT-02 | PM may override health with a mandatory note; override expires after `health_override_expiry_days`; both computed and reported health are visible. | R | §16.4 |
| FR-HLT-03 | Daily health snapshots are stored and displayed for the first-release portfolio trend. | R | §16.5, §36.6 |
| FR-DASH-01 | Project Dashboard per §13.1. | R | §13.1 |
| FR-WC-01 | Weekly Coordination view per §13.9 with agenda sections, "since last review" delta, inline updates, and "mark reviewed". | R | §13.9 |
| FR-WC-02 | Export of the coordination summary as text/markdown to clipboard. | Rec | §13.9 |
| FR-MYW-01 | My Work per §13.10 with existing sections plus Today, Upcoming, Overdue, Completed, Inbox, and saved creator/assignee views. | R | §13.10, §36.7 |
| FR-PORT-01 | Portfolio Dashboard per §13.12, plus first-release overview requirements in §36.6. | R | §13.12, §36.6 |
| FR-RES-01 | Resource / Workload View per §12.15 and §13.11, available in the first release. | R | §12.15, §36.8 |

### 11.10 Views, timeline, templates

| ID | Requirement | Class | Detail |
|---|---|---|---|
| FR-VIEW-01 | Timeline showing milestones and deliverables with today line, progress fill, and status colour. | R | §12.16, §36.4 |
| FR-VIEW-02 | Tasks on timeline, dependency arrows, drag-to-reschedule with confirmation. | R | §12.16, §36.4 |
| FR-VIEW-03 | Activity History view per project and per item. | R | §13.14, §20 |
| FR-VIEW-04 | Saved views (personal and project-shared filter/sort/column presets). | R | §18.4, §36.7 |
| FR-TPL-01 | Project Templates with disciplines, milestones, deliverables, tasks, dependencies, and relative date offsets; instantiation with a date wizard; snapshot semantics. | P2 | §12.14 |
| FR-TPL-02 | "Add from template" to append a discipline pack to an existing project. | P2 | §12.14 |

### 11.11 Notifications, search, reporting, audit

| ID | Requirement | Class | Detail |
|---|---|---|---|
| FR-NOT-01 | In-app notification centre with unread count, mark read, and link to item. | R | §17 |
| FR-NOT-02 | Email notifications for immediate events and a daily digest, per §17 defaults. | R | §17 |
| FR-NOT-03 | Per-user notification preferences (per event: in-app, email, off; digest on/off). | R | §17.4 |
| FR-NOT-04 | Microsoft Teams notifications. | P3 | §26 |
| FR-SRCH-01 | Global search across project number/name, client, tasks, deliverables, milestones, decisions, and people; results grouped by type; permission-filtered. | R | §18 |
| FR-SRCH-02 | Direct key lookup (typing `1234-T0042` opens the item). | R | §18 |
| FR-RPT-01 | Deterministic reports per §19 with CSV/XLSX export, including first-release portfolio and workload reports. | R (core, portfolio, workload) / P2 (later registers) | §19, §36 |
| FR-AUD-01 | Immutable activity log for creates, updates (field-level), status changes, assignments, date changes, deletions, decision changes, and health overrides. | R | §20 |
| FR-AUD-02 | Item-level history tab and project-level Activity History with filters. | R | §13.14 |
| FR-AUD-03 | Export of activity log for a project. | Rec | §20 |

### 11.12 Administration

| ID | Requirement | Class | Detail |
|---|---|---|---|
| FR-ADM-01 | Admin screens for all reference data in §11.1 with soft-delete/deactivate (never hard-delete referenced data). | R | §13.15 |
| FR-ADM-02 | "Reassign work" tool listing all open items owned by an inactive or departing user, with bulk reassignment. | Rec | §32 |
| FR-ADM-03 | Health and notification settings screen. | R | §13.15 |
| FR-ADM-04 | Template administration. | P2 | §12.14 |

### 11.13 Assignments, following, and staff visibility

| ID | Requirement | Class | Detail |
|---|---|---|---|
| FR-ASG-01 | Adding a user to a project team makes them follow the project: at **All activity** as PM, Discipline Lead, Team Member, or Viewer; at **My items only** when auto-added only as Reviewer. A follow level the user has set themselves is never changed. | R | §12.18 ASG-01 |
| FR-ASG-02 | Users set their own follow level per project (All activity, My items only, Muted) or unfollow, from the project header and My Work → My Projects. Any user who can view a project may follow it. | R | §12.18 |
| FR-ASG-03 | The Notification Centre has a Following tab listing every change by others on projects the user follows at All activity, with unread counts per project. | R | §12.18, §13.17 |
| FR-ASG-04 | The daily digest includes a Project updates section for followed projects. | R | §17.3 |
| FR-ASG-05 | Supervisors have a My Staff page listing their direct reports with project assignments, roles, and work counts; Executives and Admins can widen the scope to all staff. | R | §13.19 |
| FR-ASG-06 | From My Staff, a Supervisor can open a direct report's My Work (read-only), reassign their tasks, and staff them on projects: add them to a project team as Team Member or remove them; the project's PM is notified. | R | §8.5, §12.18 ASG-10 |
| FR-ASG-07 | A person's supervisor is notified (in-app and in the My staff digest section) when someone else adds them to or removes them from a project, or makes them a Discipline Lead. | Rec | §17.2, §17.3 |
| FR-ASG-08 | Staff Assignments report (person, project, role, primary discipline, added on) with CSV/XLSX export, scoped to the user's staff. | Rec | §19 |

### 11.14 Six-view visual workspace

The first-release requirements FR-VIS-01 through FR-VIS-10 and acceptance criteria AC-VIS-01 through AC-VIS-08 are specified in §36. They extend the existing project, task, portfolio, resource, saved-view, and task-hour requirements without replacing their permission or workflow rules.
