## 8. Roles and Permissions

### 8.1 Two-layer role model

The same employee is routinely a Civil supervisor, a discipline lead on one project, the PM on another, and a task assignee on a third. A single flat role cannot express this. The Hub therefore uses two layers:

1. **System roles** — assigned per user for the whole application. They control cross-project visibility and administration. A user has one or more system roles.
2. **Project roles** — assigned per user per project (and, for Discipline Lead, per discipline within the project). They control what the user can do inside that project.

Effective permission for any action = the union of what the user's system roles allow and what their project roles on the relevant project allow, further extended by **ownership** (assignee, reviewer, owner, creator) as defined in 8.6. Permissions are always evaluated server-side. Deny is the default.

**Recommendation:** Keep exactly these roles. Do not add per-field or per-status permissions. Where finer control seems needed, prefer a business rule with a warning over a permission.

### 8.2 System roles

| System role | Purpose | Cross-project visibility | Typical holders |
|---|---|---|---|
| **System Administrator** | Configure the system: users, roles, disciplines, clients, offices, deliverable types, phases, thresholds, templates. Can unarchive projects and perform data corrections. | All projects (read and write) | IT / application admin (2–3 people) |
| **Executive** | Portfolio-level visibility. | Read all projects, portfolio dashboard, reports, resource view | Regional/business unit managers |
| **Supervisor** | Visibility of their staff (direct reports, §8.8): project assignments, work, and workload; staffing them on projects and reassigning their tasks. | Read all projects; My Staff page (§13.19) and first-release Resource View for supervised staff; may add or remove supervised staff on project teams as Team Members and reassign tasks they own | Group/department managers |
| **Project Manager** | May create projects and becomes PM of projects they create. | Read all projects (subject to 8.7); write only where they hold a project role | Staff designated as PMs |
| **Standard User** | Default for every employee. | Read all projects (subject to 8.7); write only where they hold a project role | All staff |
| **Read Only** | View-only account. Cannot comment or edit. | Read all projects (subject to 8.7) | Auditors, temporary staff, contractors (TBD) |

**Recommendation:** "Standard User" is granted automatically to every active user provisioned from Entra ID. Other system roles are mapped from Entra ID security groups (see Section 23.6) or set by a System Administrator in the Hub. **TBD — Business Decision Required:** group-mapped or in-app-assigned system roles (see Section 34).

### 8.3 Project roles

| Project role | Scope | Purpose |
|---|---|---|
| **Project Manager (PM)** | Whole project | Accountable for the project. Full write within the project: project info, team, disciplines, milestones, deliverables, tasks, dependencies, decisions, registers, health override, status changes including Complete. Exactly one *primary* PM per project (the `Project.project_manager_id`); additional users may hold the PM role (e.g., deputy PM). |
| **Discipline Lead (DL)** | One discipline within the project | Accountable for the discipline's work. Full write on deliverables and tasks whose owning discipline matches; may create tasks/deliverables in their discipline; may assign within the project team; may set deliverable status; may add dependencies from/to their discipline's tasks. Read on everything else. Cannot change milestone dates or project info. |
| **Team Member** | Whole project | May create tasks in disciplines they are a member of (Recommendation: allow, with owning discipline defaulting to their discipline); update tasks assigned to them or on which they are collaborators; comment anywhere in the project; add document links; view everything. |
| **Reviewer** | Whole project | A team-member-level role granted automatically when someone outside the team is assigned as reviewer. Can view the project, comment, and act on reviews assigned to them. |
| **Viewer** | Whole project | Read and comment (Recommendation: allow comments; make it configurable per project). For stakeholders who want to follow a project without owning work. |

A user can hold multiple project roles on the same project (e.g., PM and DL of Project Management discipline). A user can be DL for more than one discipline on a project.

### 8.4 Role combinations — worked example

Employee **Marc**:

| Context | Role(s) | What Marc can do |
|---|---|---|
| System | Supervisor (Civil group), Project Manager, Standard User | See all projects; see Resource View for the 12 civil staff; create projects |
| Project 1234 (DCC Dundurn Roads) | Discipline Lead — Civil | Manage all Civil deliverables/tasks on 1234; cannot move 1234's milestones |
| Project 1301 | PM | Everything on 1301 |
| Project 1187 | Team Member (assigned 3 tasks) | Update own tasks, comment |
| Project 1187 | Reviewer on task 1187-T0042 | Review that task |

Marc's My Work aggregates all of these. The Hub never asks Marc to "switch role"; it evaluates effective permissions per action.

### 8.5 Permissions matrix

Legend: **Y** = allowed; **O** = allowed for own items only (see 8.6); **D** = allowed within own discipline only; **—** = not allowed.

#### 8.5.1 System-level actions

| Action | Sys Admin | Executive | Supervisor | Project Manager (sys) | Standard User | Read Only |
|---|---|---|---|---|---|---|
| Sign in and use the application | Y | Y | Y | Y | Y | Y |
| View project list and any project (subject to 8.7) | Y | Y | Y | Y | Y | Y |
| Create a project | Y | — | — | Y | — | — |
| View Portfolio Dashboard | Y | Y | Y | Y (own projects by default; all with filter) | — | — |
| View Resource / Workload View | Y | Y (all) | Y (supervised staff, plus all in read) | Y (members of own projects) | — | — |
| View My Staff page (staff assignments and work, §13.19) | Y (all staff) | Y (all staff) | Y (supervised staff) | — | — | — |
| Reassign tasks across projects for supervised staff | Y | — | Y | — | — | — |
| Add or remove supervised staff on a project team (as Team Member) | Y | — | Y | — | — | — |
| Manage users, system roles, offices, disciplines, clients, deliverable types, phases | Y | — | — | — | — | — |
| Manage project templates | Y | — | — | Y (Recommendation: also allow "Template Editor" as an Admin-granted flag) | — | — |
| Manage organisation thresholds and notification defaults | Y | — | — | — | — | — |
| Unarchive a project | Y | — | — | — | — | — |
| View all activity history | Y | Y | Y | Y | Y (projects they can view) | Y |
| Export reports | Y | Y | Y | Y | Y (own projects) | Y |
| Manage own notification preferences, project follow levels, and saved views | Y | Y | Y | Y | Y | Y |

#### 8.5.2 Project-level actions

| Action | PM | Discipline Lead | Team Member | Reviewer | Viewer |
|---|---|---|---|---|---|
| View project, dashboard, all registers | Y | Y | Y | Y | Y |
| Edit project information (name, client, description, dates, phase, links) | Y | — | — | — | — |
| Change project status (Active, On Hold, Complete, Cancelled) | Y | — | — | — | — |
| Archive project | Y (Complete → Archived) | — | — | — | — |
| Manage project team and roles (Supervisors may also add or remove their direct reports as Team Members, §8.5.1) | Y | — | — | — | — |
| Add/remove disciplines, set Discipline Lead | Y | — | — | — | — |
| Set/clear health override | Y | — | — | — | — |
| Create / edit / delete milestone; change milestone date | Y | — | — | — | — |
| Mark milestone Complete | Y | — | — | — | — |
| Create deliverable | Y | D | — | — | — |
| Edit deliverable (fields, owner, dates) | Y | D | O (owner) | — | — |
| Change deliverable status | Y | D | O (owner, except Issued/Accepted — Recommendation: PM or DL only) | — | — |
| Delete / cancel deliverable | Y | D (if no completed tasks) | — | — | — |
| Create task | Y | D | Y (in own discipline; Recommendation) | — | — |
| Create / edit / cancel calendar event tied to project | Y | O (event owner) | O (event owner) | O (event owner) | — |
| View task-hour entries | Y (project) | D | O (own entries) | O (own entries) | — |
| Add / edit / delete task-hour entries | Y (correct with reason) | D (own entries) | O (own entries) | O (own entries) | — |
| Edit task fields | Y | D | O (assignee/collaborator/creator) | — | — |
| Assign / reassign task | Y | D | O (creator may assign at creation) | — | — |
| Change task status | Y | D | O (assignee/collaborator: Not Started → In Progress → Ready for Review; On Hold with reason) | O (reviewer: In Review, Revision Required, Complete) | — |
| Set reviewer on a task | Y | D | O (creator at creation) | — | — |
| Add / remove task dependency | Y | D (either end in own discipline) | O (successor is own task) | — | — |
| Set manual block / clear manual block | Y | D | O | — | — |
| Change task due date | Y | D | O (Recommendation: assignee may change with reason; logged; PM notified) | — | — |
| Delete task | Y | D (Recommendation: only if Not Started) | O (creator, only if Not Started and no dependencies) | — | — |
| Cancel task | Y | D | — | — | — |
| Reopen completed task | Y | D | — | O (reviewer) | — |
| Comment, @mention, add document link | Y | Y | Y | Y | Y (comment configurable) |
| Edit / delete own comment (within rules, Section 12.8) | O | O | O | O | O |
| Create / edit decision, risk, issue, meeting, action | Y | Y (Recommendation: DL may raise decisions/risks/issues; owner edits own) | Y (raise only; edit own) | — | — |
| Record a decision outcome | Y | O (decision owner) | O (decision owner) | — | — |
| Run Weekly Coordination in meeting mode (record "reviewed" timestamp) | Y | Y | — | — | — |

**Recommendation:** Treat the matrix as the acceptance test for the authorisation layer. Each row becomes a set of automated tests.

### 8.6 Ownership-based permissions

Ownership extends role permissions on individual items regardless of project role:

- **Assignee** of a task: update status (within assignee transitions), progress, description, estimated effort, document links; add dependencies where their task is the successor; set manual block; propose due-date change (Recommendation: allowed with reason).
- **Collaborator** on a task: same as assignee except cannot reassign or change due date.
- **Reviewer** of a task or deliverable: transition In Review → Revision Required / Complete (task) or In Review → Revision Required / Ready to Issue (deliverable); comment.
- **Owner** of a deliverable, decision, risk, issue, or action: edit the item and change its status (within rules).
- **Creator** of an item: edit it until someone else has acted on it (status change, comment by another user, assignment accepted); delete it if it is Not Started/Pending and has no links.
- **Owner of a task-hour entry**: edit or soft-delete that entry while its project is editable; this does not grant permission to change the task itself. A project PM may correct another person's entry only with a logged reason (§36.8).

### 8.7 Visibility model

**Assumption:** All active employees may view all projects (open-by-default). This is typical in consulting firms, maximises coordination value, and keeps the permission model simple.

**TBD — Business Decision Required:** whether some projects must be **restricted** (e.g., confidential clients, litigation support, M&A due diligence). If yes, the design accommodates a per-project `visibility` flag (`Open` | `Restricted`), where Restricted projects are visible only to project members, System Administrators, and Executives. This is cheap to add at the data-model level and should be included in the schema from day one even if the UI control ships later. Recommendation: include the column and enforce it in the authorisation layer in MVP; expose the toggle only if the business confirms the need.

### 8.8 How roles are assigned

| Layer | Mechanism |
|---|---|
| System roles | Recommendation: map from Entra ID security groups at sign-in (`HUB-Admins`, `HUB-Executives`, `HUB-Supervisors`, `HUB-ProjectManagers`, `HUB-ReadOnly`), with an in-app override table for exceptions. Standard User is implicit for any authenticated employee. See Section 23.6. |
| Supervisor → staff relationship | Recommendation: `User.supervisor_id` maintained by System Administrators in the Hub (or synchronised from Entra ID `manager` attribute if populated — **TBD**). **Supervised staff** means the supervisor's direct reports: active users whose `supervisor_id` is the supervisor (decided, Q19). This definition applies wherever the specification says "supervised staff" (My Staff, read access to My Work, staffing, reassignment, Resource View). |
| Project roles | Assigned in-app by the PM on the Project Team screen; Supervisors may also add or remove their direct reports as Team Members (§12.18). Creating a project makes the creator its PM. Setting a Discipline Lead grants the DL role for that discipline. Assigning a reviewer who is not on the team grants Reviewer automatically. Every assignment to a project team also makes the person follow the project, so they receive its updates (§12.18). |

### 8.9 Permission principles for implementation

1. Every API endpoint declares the permission it requires; authorisation is evaluated in the application service layer, never only in the UI.
2. The UI hides or disables actions the user cannot perform and explains why on hover ("Only the Project Manager can change milestone dates").
3. Permission checks are pure functions of (user, roles, project memberships, item) and are unit-tested against the matrix above.
4. No "super-user impersonation" in MVP. Administrators act as themselves and their actions are logged.

---

### 8.10 Multidisciplinary coordination permissions

These actions extend the existing matrix for packets 025–033. Existing access/lifecycle checks are applied first, including the Read Only veto and restricted-project membership. A system role does not bypass a project visibility restriction. Scope-qualified ownership is an additional right, never an exception to a refusal.

| Action | Authorised actor after existing write/access gates |
|---|---|
| Create/assign handoff | PM, source/receiving Discipline Lead, or permitted source-work owner within their discipline |
| Submit/respond to handoff | Named sending owner; PM/lead may reassign with reason |
| Accept/incorporate handoff | Named receiving owner; self-review setting governs same-person exception |
| Coordinate review package | PM or responsible Discipline Lead; may assign coordinator |
| Technical review approval | Named discipline reviewer, with independent-review checks |
| Respond to/verify review finding | Resolution owner responds; originator or authorised independent replacement verifies |
| Publish source revision/change | PM, responsible Discipline Lead or source deliverable owner |
| Assess changed input | Named receiving-work owner; PM/lead approves retention of an old revision |
| Issue submission package | PM, after transactional recheck of manifest and mandatory gates |
| Propose allocation | PM or Discipline Lead within the project |
| Confirm allocation/change availability | Supervisor for direct reports, or Admin; no added access to restricted project details |
| Propose design basis/assumption | Project member within their discipline; PM/lead assigns owner |
| Confirm design basis | Responsible Discipline Lead or explicitly assigned independent approver |
| Approve Proceed under Assumption | PM or responsible Discipline Lead, with fixed scope and expiry |
| Propose/verify constraint removal | Named removal owner proposes; affected work owner verifies |
| Commit weekly output | Named performer; meeting chair may propose only |
| Resolve/verify location issue | Existing Issue owner proposes; independent designated verifier verifies |
| View coordination, sources and export | Existing permitted project viewer; source-system access is checked separately when opening external links |

Reassignment is attributed, reasoned and version checked, retains earlier signatures/decisions, and reruns self-review and access invariants. No proxy approval is implied by manager, coordinator or administrator status. Common refusal/notification rules: §37.1.

### 8.11 Weekly planning permissions

These rows extend the matrix for the weekly planning layer (§39, packet 034). Inactive and Read Only accounts cannot write. A linked project's visibility (§8.7) applies to every read, and a Supervisor's staff are their direct reports (§8.8, Q19). Ownership extends a row only where the table says so and is never an exception to a refusal; Private draft privacy is defined in §39.6. Every row becomes an automated test (§8.9).

| Action | The person (own row) | The person's Supervisor | System Administrator | Executive | Project Manager (system role) | Anyone else |
|---|---|---|---|---|---|---|
| Open the Weekly Planner (§13.21) | — (own row on My Work, My Week) | Y (direct reports and own row) | Y (all active people) | Y (all active people) | Y (members of Setup or Active projects they manage, and own row) | — |
| See a person's Self-entered entries, Proposed and Confirmed assignments, approved project allocations, capacity and time away | Y | Y | Y | Y | Y (those members) | — |
| See a Private draft | O (drafts they own) | O (drafts they own) | Only in data-correction mode with a reason; each access is logged | — | — | — |
| Create a self entry | Y | — | — | — | — | — |
| Create a manager entry | — | Y | Y | — | — | — |
| Change, move, extend, mark Still valid or delete an entry; copy creates a new entry and needs the create right for its person | O (own self entries) | O (manager entries they own, while still the Supervisor) | Y (own entries; others' entries only as a data correction with a reason) | — | — | — |
| Change a manager entry's visibility | — | O (manager entries they own) | Y (own entries; others' entries only as a data correction with a reason) | — | — | — |
| Record or clear time away (availability overrides, §39.3) | — | Y | Y | — | — | — |
| Export planner and entry lists; save personal planner views | — | Y | Y | Y | Y | — |
| Change planning settings (§10.9) | — | — | Y | — | — | — |

---

## 9. Information Architecture

### 9.1 Evaluation of the proposed hierarchy

The proposed hierarchy was:

```
Organization → Project → Discipline → Milestone → Deliverable → Task
```

It is a good starting point but it treats two things as containers that are not containers in practice:

| Level | Problem with strict containment | Observation from engineering projects |
|---|---|---|
| Discipline → Milestone | Milestones are almost always **project-wide** (60% Design Submission, IFC, Tender). A few are discipline-specific (Geotechnical Field Investigation), but forcing every milestone under one discipline breaks the common case. | A submission milestone is met by deliverables from several disciplines. |
| Milestone → Deliverable | A deliverable *targets* a milestone; it is not *contained* by it. Some deliverables (e.g., a survey base plan) are prerequisites that do not correspond to a client submission at all. | Deliverables need an optional target milestone, not a mandatory parent. |
| Deliverable → Task | Correct. Tasks are the units of work that produce a deliverable. But some tasks are not deliverable work (arrange kickoff meeting, coordinate utility locates, chase a permit). | Tasks need an optional deliverable parent, with the discipline still mandatory. |
| Organization | Correct as the tenant boundary. The Hub is single-organisation; "Office" is an attribute used for filtering, not a hierarchy level. | — |

### 9.2 Recommended structure

**Recommendation:** Treat *Project* as the root container, *Discipline* as an ownership dimension, *Milestone* as a date-target dimension, and *Deliverable → Task* as the work hierarchy.

```
Organization (single tenant; Offices, Disciplines, Clients, Users, Templates, Settings)
└── Project
    ├── Project Disciplines      (which disciplines are on this project; each with a Discipline Lead)
    ├── Project Team             (members with project roles)
    ├── Milestones               (project-level dated checkpoints; optionally tagged with a discipline)
    ├── Deliverables             (owned by ONE discipline; optionally target ONE milestone)
    │   └── Tasks                (owned by ONE discipline; belong to at most ONE deliverable;
    │                             may alternatively target a milestone directly)
    ├── Dependencies             (task → task, Finish-to-Start, within the project)
    ├── Registers                (Decisions; Phase 2: Risks, Issues, Meetings & Actions)
    ├── Comments, Document Links, Notifications, Activity History (attached to items)
    └── External Parties         (client and third-party contacts referenced by items)
```

Rules of containment:

1. Every Deliverable and every Task belongs to exactly one Project and exactly one owning Discipline (from the project's disciplines).
2. A Task belongs to at most one Deliverable. If it has a Deliverable, it inherits the Deliverable's milestone for roll-up purposes. If it has no Deliverable, it may target a Milestone directly (optional).
3. A Deliverable optionally targets one Milestone. Milestone status is derived from its targeted deliverables and their tasks.
4. A Milestone belongs to the Project and may optionally be tagged with a Discipline for filtering (e.g., "Geotech Field Investigation").
5. Registers (Decisions, Risks, Issues, Meeting Actions) belong to the Project and *link* to any number of Tasks, Deliverables, and Milestones through a generic link table. They are not in the containment tree.
6. Dependencies are edges between Tasks within the same Project. Deliverable-level and milestone-level dependencies are **derived** from task dependencies in MVP (see Section 12.6). Explicit deliverable-to-deliverable dependencies are Phase 2.

### 9.3 Project phase

**Recommendation:** Add **Phase** as a project attribute drawn from a configurable ordered list (default: Proposal/Setup, Kickoff, Field Investigation, Preliminary Design, Detailed Design, IFC, Tender, Construction, Closeout). The PM sets the current phase manually. Milestones may optionally carry a `completes_phase` reference so the Hub can *suggest* advancing the phase when the milestone is completed; it never advances the phase automatically.

### 9.4 Application navigation architecture

Global navigation (persistent left rail or top bar; desktop-first):

| Area | Contents | Audience |
|---|---|---|
| **My Work** (landing page) | My Tasks, My Reviews, My Deliverables, Waiting on Others, Blocking Others, My Projects, Upcoming Milestones | Everyone |
| **Projects** | Project list (search, filter); opens a project workspace | Everyone |
| **Portfolio** (first release) | Portfolio Dashboard, Projects At Risk | Executives, Supervisors, PMs |
| **Resources** (first release) | Resource / Workload View; Weekly Planner (§13.21, §39) | Supervisors, Executives, PMs; everyone sees their own planning row in My Work (My Week) |
| **My Staff** | The user's direct reports with their project assignments and work counts; staffing them on projects (§13.19) | Supervisors, Executives, Admins |
| **Reports** | Deterministic report list with export | Everyone (scoped) |
| **Notifications** | In-app notification centre: personal notifications and the Following feed of followed projects | Everyone |
| **Admin** | Users & roles, Disciplines, Clients, Offices, Deliverable Types, Phases, Templates, Settings | System Administrators |
| Global search | Always visible search box | Everyone |

For the first-release visual workspace (§36), the shell also presents Home (overview), Boards, Tasks, Calendar, Files, Time, and Team as direct routes into the corresponding permitted views. Workload is a first-release route for authorised users. A navigation item appears only when it has a working destination; Time opens task-hour entry (§36.8).

Project workspace navigation (tabs within a project):

`Dashboard · Weekly Coordination · Tasks (List / Board) · Deliverables · Milestones · Timeline · Decisions · Risks (P2) · Issues (P2) · Meetings & Actions (P2) · Team & Disciplines · Activity · Settings`

The workspace view switcher additionally presents Board/List, Timeline, Workload, Dashboard, Files, and Calendar for the selected permitted project set (§36.1). It preserves that selection across views.

### 9.5 Identifiers

Every user-facing item has a stable, human-readable key in addition to its database primary key:

| Item | Key format | Example |
|---|---|---|
| Project | Business project number (unique, entered or imported) | `1234` or `2026-0417` (format **TBD — Business Decision Required**) |
| Milestone | `{project}-M{nn}` | `1234-M03` |
| Deliverable | `{project}-D{nnn}` | `1234-D012` |
| Task | `{project}-T{nnnn}` | `1234-T0042` |
| Decision | `{project}-DEC{nn}` | `1234-DEC02` |
| Risk / Issue / Action (P2) | `{project}-R{nn}` / `{project}-I{nn}` / `{project}-A{nn}` | `1234-I04` |

Keys are assigned from a per-project sequence at creation and never reused. They appear in search, URLs, notifications, and exports. Renumbering a project does not change item keys (the key stores the project number at creation; Recommendation: display the current project number but keep the stored key stable).

---

## 10. Core Data Model

This section defines the conceptual model and all canonical vocabulary. The physical schema is in Section 24.

### 10.1 Entity overview

| Group | Entities | Notes |
|---|---|---|
| Organisation | `User`, `Office`, `Discipline`, `Client`, `ExternalParty`, `DeliverableType`, `Phase`, `OrgSetting` | Reference data managed by System Administrators |
| Access | `UserSystemRole`, `ProjectMember` (with project role), `ProjectDiscipline` (with lead) | See Section 8 |
| Project | `Project`, `ProjectLink` (important links), `ProjectHealthSnapshot` (daily) | |
| Work | `Milestone`, `Deliverable`, `Task`, `TaskDependency`, `TaskParticipant`, `TaskTimeEntry` | Core hierarchy and first-release actual hours (§36.8) |
| Registers | `Decision` (MVP-thin), `Risk` (P2), `Issue` (P2), `Meeting` (P2), `MeetingAction` (P2), `ItemLink` | `ItemLink` is a generic relation between any two items |
| Collaboration | `Comment`, `Mention`, `DocumentLink` | Polymorphic target (item type + id) |
| System | `ActivityLog`, `Notification`, `NotificationPreference`, `ProjectFollow`, `AttentionSnooze`, `SavedView` | `ProjectFollow` records who follows which project and at what level (§12.18); saved views are first-release scope (§36.7) |
| Templates | `ProjectTemplate`, `TemplateDiscipline`, `TemplateMilestone`, `TemplateDeliverable`, `TemplateTask`, `TemplateDependency` (P2) | Snapshot-copied into projects |

### 10.2 Canonical statuses

Status names are stored as fixed codes and displayed exactly as below. They are not configurable per project in MVP (custom workflows are [Out of Scope]).

#### Project status

| Status | Meaning | Effect |
|---|---|---|
| **Setup** (Recommendation) | Project is being built out (team, milestones, template items) before going live. | Health = Grey; no digest notifications; excluded from portfolio counts; immediate notifications (assignment) still sent. |
| **Active** | Live project. | Fully evaluated. |
| **On Hold** | Work paused by PM. | Health = Grey; items excluded from overdue/attention/digests; retained in portfolio with "On Hold" indicator. |
| **Complete** | Work finished; closeout done. | Read-mostly; PM may still make corrections (Section 12.1); evaluation stops; retained in searches. |
| **Archived** | Retained for record. | Read-only for everyone; Admin may unarchive; excluded from default lists; searchable with "include archived". |
| **Cancelled** | Project stopped before completion. | Read-only; treated like Archived for visibility. |

Allowed transitions: Setup → Active; Active ↔ On Hold; Active → Complete; Active/On Hold/Setup → Cancelled; Complete → Archived; Complete → Active (reopen, PM, with reason); Archived → Complete (Admin unarchive).

#### Task status (workflow)

| Status | Meaning | Who typically sets it |
|---|---|---|
| **Not Started** | Created, not begun. | System (default) |
| **In Progress** | Being worked. | Assignee |
| **Ready for Review** | Assignee has finished; awaiting reviewer (only if task requires review). | Assignee |
| **In Review** | Reviewer has started reviewing. | Reviewer |
| **Revision Required** | Reviewer returned it with comments. | Reviewer |
| **Complete** | Done (and reviewed, if review required). Terminal unless reopened. | Reviewer (if review required) or Assignee/PM/DL |
| **On Hold** | Deliberately paused; reason required. | Assignee, DL, PM |
| **Cancelled** | Will not be done. Terminal. | PM, DL |

Transitions are defined in Section 15 (rules T-10 to T-14). **Blocked is not a status** — see 10.3 and the insight below.

#### Deliverable status (lifecycle)

| Status | Meaning |
|---|---|
| **Not Started** | Defined, no work begun. |
| **In Progress** | Tasks under way. |
| **In Review** | Internal QA/QC / technical review of the package under way. |
| **Revision Required** | Returned by internal review or by the client with comments. |
| **Ready to Issue** | Reviewed and approved internally; awaiting issue/submission. |
| **Issued** | Issued/submitted (to client, authority, or internally to another discipline). `issued_date` and `revision` recorded. |
| **Accepted** | Recipient accepted / no further action (optional final state; PM decides whether to use it). |
| **On Hold** | Paused; reason required. |
| **Cancelled** | Will not be produced. Terminal. |

#### Milestone status (derived, except Complete/Cancelled)

| Status | Meaning |
|---|---|
| **On Track** | Not complete; date in future; no risk conditions met. |
| **At Risk** | Not complete; a risk rule fired (Section 16.2). |
| **Overdue** | Not complete; date has passed. |
| **Complete** | PM marked complete; `completed_date` recorded. |
| **Cancelled** | Removed from plan but retained for history. |

#### Decision status

| Status | Meaning |
|---|---|
| **Pending** | Raised; owner has not started. |
| **Under Review** | Owner is actively considering / has requested information. |
| **Decided** | Decision recorded with `decision_text` and `decision_date`. Terminal unless reopened. |
| **Deferred** | Owner has explicitly postponed; a new `required_by_date` is mandatory. Justified because it distinguishes a consciously postponed decision from a stale one. |
| **Cancelled** | No longer needed. |

#### Risk status (Phase 2)

`Open` → `Monitoring` → `Closed`; `Realised` (converted into an Issue; link retained).

#### Issue status (Phase 2)

`Open` → `In Progress` → `Resolved`; `Cancelled`.

#### Meeting Action status (Phase 2)

`Open` → `In Progress` → `Complete`; `Cancelled`.

#### Project health

`Green` (On Track) · `Yellow` (Attention Required) · `Red` (At Risk / Critical) · `Grey` (Not Evaluated). Computed per Section 16, with optional PM override.

### 10.3 Canonical derived indicators

Indicators are computed, never stored as the primary status (they may be cached). Each has a rule in Section 15 and a visual treatment in Section 13.

| Indicator | Applies to | Plain-language definition |
|---|---|---|
| **Overdue** | Task, Deliverable, Milestone, Decision, Action | Due/required date is before today and the item is not in a terminal or held state. |
| **Due Soon** | Task, Deliverable, Milestone, Decision | Due within the configured lead time and not terminal. |
| **Waiting** | Task | Has at least one incomplete predecessor, but it is not yet a problem (successor not due to start). |
| **Blocked** | Task | Cannot proceed: incomplete predecessor when the task should have started or the predecessor is overdue, **or** a manual block is set, **or** a linked decision is overdue (Recommendation). |
| **Blocking Others** | Task, Decision | Has at least one open successor/linked item that is Waiting or Blocked because of it. |
| **Stale** | Task, Deliverable | In Progress (or Ready for Review/In Review) with no update for longer than the stale threshold. |
| **Unassigned** | Task, Deliverable | No accountable owner while active. |
| **No Due Date** | Task | Active (In Progress or later) without a due date, or Not Started inside a deliverable that has a due date. |
| **Date Inconsistent** | Task, Deliverable | Task due after its deliverable's due date; deliverable due after its milestone date; successor starts before predecessor is due. |
| **Slipped** | Milestone, Deliverable | Current date is later than original (baseline) date; shows days of slip. |
| **Inactive Owner** | Task, Deliverable, Decision | Owner's user account is inactive. |

> **Design note.** Why "Blocked" is an indicator and not a status: a blocked task is still *In Progress* or *Not Started* from a workflow point of view. If Blocked replaced the workflow status, unblocking it would require guessing which status to restore, and reporting would lose the distinction between "not started because blocked" and "half done and blocked". Modelling Blocked as a computed flag with an explicit list of blockers means it appears and disappears automatically as predecessors complete, and the workflow status is never corrupted. The same reasoning applies to Overdue.

### 10.4 Canonical thresholds (organisation settings)

All thresholds are stored in `OrgSetting`, editable by System Administrators, with the defaults below. Calendar days are used in MVP; working-day calendars are Phase 2 (**TBD — Business Decision Required:** statutory holiday calendar by office).

| Setting key | Default | Used by |
|---|---|---|
| `task_due_soon_days` | 5 | Due Soon indicator; digest |
| `deliverable_due_soon_days` | 10 | Due Soon; attention rule A-06 |
| `milestone_approaching_days` | 14 | Milestone At Risk rule; attention rule A-05 |
| `decision_due_soon_days` | 5 | Decision Due Soon; digest |
| `task_stale_days` | 10 | Stale indicator; attention rule A-10 |
| `review_stale_days` | 5 | Stalled review; attention rule A-11 |
| `blocked_attention_days` | 0 | Days a task may be Blocked before A-02 fires (0 = immediately) |
| `health_overdue_task_pct_yellow` | 10% | Project health |
| `health_overdue_task_pct_red` | 25% | Project health |
| `health_overdue_task_min_yellow` | 3 | Minimum count to trigger yellow by percentage |
| `health_blocked_days_yellow` | 5 | A task blocked longer than this contributes to Yellow |
| `health_override_expiry_days` | 14 | Manual health override validity |
| `chain_depth_limit` | 10 | Maximum depth for dependency chain display |
| `allow_self_review` | false | Whether assignee may be their own reviewer |
| `complete_project_edit_window_days` | 30 | Days a Complete project stays editable by PM before Archive is suggested |
| `default_weekly_capacity_hours` | 40 | First-release Resource View; **TBD** |
| `org_time_zone` | **TBD** | "Today" for date rules (Recommendation: single organisation time zone in MVP) |
| `digest_send_time_local` | 07:00 | Daily digest |

### 10.5 Scales

| Scale | Values | Notes |
|---|---|---|
| Task/Deliverable priority | `Low`, `Medium`, `High`, `Critical` | Default Medium. Priority affects sorting and attention severity; it never changes rules. |
| Project priority | `Low`, `Medium`, `High`, `Critical` | Default Medium; PM-set for the Projects board (§36.2). It does not replace project health. |
| Decision impact if delayed | `Low`, `Medium`, `High` | Free-text impact description also required. |
| Risk probability (P2) | 1 Low, 2 Medium, 3 High | |
| Risk impact (P2) | 1 Low, 2 Medium, 3 High | |
| Risk severity (P2) | Probability × Impact → 1–2 Low, 3–4 Medium, 6–9 High | Practical 3×3 grid; see Section 12.10 |
| Issue severity (P2) | `Low`, `Medium`, `High` | Direct selection; no scoring |
| Attention severity | `Critical`, `Warning`, `Info` | Assigned per rule; Section 12.12 |

### 10.6 Date and time conventions

- Due dates, start dates, milestone dates, required-by dates are **calendar dates** (no time component). Stored as `DATE`.
- First-release calendar events (§36.5) have explicit start and end timestamps in the organisation time zone. Date-only deadline projections remain all-day; no time is invented for them.
- Timestamps (created, updated, completed, issued, activity) are stored in UTC (`TIMESTAMPTZ`) and displayed in the user's browser time zone.
- "Today" for rule evaluation is the current date in `org_time_zone`. A project-level time-zone override is a future capability.
- "Within N days" means `date − today ≤ N` and `date ≥ today`.
- **Overdue** means `date < today` (a task due today is not overdue until tomorrow).

### 10.7 Key relationship summary

| Relationship | Cardinality | Notes |
|---|---|---|
| Project — ProjectDiscipline | 1 : many | Discipline appears at most once per project; each has an optional lead |
| Project — ProjectMember | 1 : many | User appears once per project with one or more project roles |
| Project — Milestone | 1 : many | |
| Project — Deliverable | 1 : many | Deliverable → Discipline (required), → Milestone (optional), → owner User (required when not Not Started; Recommendation: required always) |
| Deliverable — Task | 1 : many (optional parent) | Task → Discipline (required), → Deliverable (optional), → Milestone (optional, only when no deliverable) |
| Task — Task (TaskDependency) | many : many, directed | predecessor → successor; same project; acyclic |
| Task — User | assignee (0..1), reviewer (0..1), participants (0..many collaborators/watchers) | Single accountable assignee |
| Decision — Items (ItemLink) | many : many | Decision ↔ Task/Deliverable/Milestone |
| Comment / DocumentLink / ActivityLog — Item | many : 1 polymorphic | `item_type` + `item_id`, plus `project_id` for scoping and permissions |
| Template → Project | snapshot copy | Project records `template_id` and `template_version`; later template changes do not propagate |
| User — User (supervisor) | many : 1 | Drives Supervisor scope: direct reports (§8.8) |
| User — Project (ProjectFollow) | many : many | One row per user per project; created automatically on team assignment (§12.18) |

### 10.8 Multidisciplinary coordination vocabulary and records

This is the canonical extension to §10 for the approved nine-packet amendment (§37, §38). Existing task/deliverable/project states do not change. Readiness, handoff acceptance, review approval and document issue are distinct concepts. This amendment adds metadata and workflow evidence; it does not implement a file-versioning service.

| Record | Canonical states or representation | Key relationships/invariants |
|---|---|---|
| Handoff / receipt | Draft, Submitted, Clarification Requested, Returned, Accepted, Incorporated, Cancelled | One sending owner, one receiving owner per receipt, exact source revision, target work, intended use, criteria, needed/promised dates; revision-specific acceptance |
| Review package | Draft, In Review, Changes Required, Approved, Superseded, Cancelled | One coordinator, immutable round manifest, required discipline assignments |
| Discipline review | Pending, In Review, Changes Required, Approved | One reviewer per discipline and round; approval of exact revisions |
| Review finding | Open, Responded, Verified Closed, Withdrawn | One resolver and separate verification attribution; Blocking or Advisory severity |
| Source revision | Immutable registered snapshot with explicit supersedes link | Existing DeliverableIssue or external source ID/URL/revision, source-check provenance; supersedes graph acyclic |
| Input use | Versioned adoption link | Consumer work, source/basis revision, intended use, adopting actor and time |
| Change notice | Draft, Open, Closed, Cancelled | One owner, old/new references, stated scope; publication moves Draft to Open; closure needs completed assessments; cancellation needs PM/lead reason and preserves impacts |
| Change assessment | Pending Assessment, Unaffected, Update Required, Clarification Needed, Resolved | One affected owner per consumer/change; rationale, correction task and verification; acknowledgement separate |
| Submission | Draft, Checking, Ready, Issued, Superseded, Cancelled | Coordinator, milestone, fixed manifest and issue snapshot; issued record immutable |
| Submission check | Pending, Pass, Fail, Not Applicable | Evidence, responsible checker, applicability reason; mandatory gates cannot be waived |
| Resource allocation | Proposed, Confirmed, Declined, Cancelled, Completed | One person/project, production or review purpose, dates/hours; linked work unique per person/date slice |
| Availability override | Date and available decimal hours | One person/date; replaces default daily capacity; no sensitive leave details |
| Design basis entry/version | Proposed, Confirmed, Superseded, Withdrawn | Criterion or Assumption, scope, value/statement, units, owner, source and affected consumers; confirmed versions immutable |
| Readiness | Needs Assessment, Not Ready, Ready, Proceed under Assumption | Derived from named requirements/constraints; not a task status or permission grant |
| Constraint | Open, Resolution Proposed, Verified Removed, Cancelled | One removal owner, affected work, needed date, source and verifier; existing issue/decision may be linked |
| Weekly output commitment | Proposed, Committed, Met, Not Met, Withdrawn | Performer, immutable original output/criteria/date snapshot and attributed later changes |
| Location-linked issue | Existing Issue status vocabulary (§10.2) | Extend existing Issue; source revision, station/area/asset context and one resolution owner |
| Issue verification | Resolution Proposed, Verified, Rejected | Evidence and independent verifying owner; separate from Issue status |
| Discipline coordination view | Permission-filtered projection | No independent copy of underlying work or workflow status |

Readiness precedence is Needs Assessment for missing/unknown applicable checks, then Not Ready for known unsatisfied required checks; the UI always lists all reasons. Ready requires every applicable check satisfied. Proceed under Assumption is available only when the remaining unsatisfied inputs are covered by valid, scoped, unexpired authorised assumptions and no non-overridable gate is failed. A known failure alongside an unknown check remains visible even when the headline is Needs Assessment.

New records use stable UUIDs and readable per-project keys where they appear as standalone items: Handoff H, Review RV, Change CH, Submission SUB, Basis B, Constraint CT and Commitment WC. Existing Issue keys remain unchanged. Prefixes do not alter existing item sequences; deleted keys are never reused. New records carry project, owner, created/updated attribution, row version and soft-deletion fields as applicable. Immutable published snapshots use append-only superseding records rather than edits or deletes. Source references must remain same-project; use a unique source/version identity and unique change/consumer assignment to deduplicate.

Add `coordination_lookahead_weeks` to organisation settings, default 3 and allowed range 1–12. Existing working-day calendars, weekly capacity and workload warning thresholds continue to apply. Submission checks and readiness constraints are purpose-specific records, not arbitrary checklists inside tasks or a custom workflow builder. Physical tables and migrations are designed in each packet before application implementation.

### 10.9 Weekly planning vocabulary and settings

This is the canonical extension to §10 for the weekly planning layer (§39, packet 034). It adds one record beside the §10.8 resource allocation and availability override, which are unchanged. Planning entries belong to a person, not to a project.

| Term | Canonical values or representation | Key rules |
|---|---|---|
| Planning entry | Person, owner, hours per week, start week, end week, label, source category, optional project and project discipline, confidence, visibility, notes, last validated | One row per block of weekly hours; the owner is the creator; soft-deleted; row-versioned; never a project record |
| Owner kind (derived) | Self (the owner is the person) · Manager (the owner is the person's Supervisor or an Admin) | Displayed as "Self-entered" or "Manager plan" |
| Source category | Major project · Other project · Proposal · Business development · Training · Admin · Supervision · Internal initiative · Field work · Other | Major project if and only if a project is linked; there is no leave category |
| Confidence | Confirmed · Expected · Possible | Displayed as "Confidence: …"; Possible never reduces remaining capacity |
| Visibility | Draft · Published · Confirmed | Manager entries display "Visibility: Private draft", "Visibility: Proposed assignment" or "Visibility: Confirmed assignment"; self entries are stored as Confirmed and display "Visibility: Self-entered" |
| Approved project allocation | Read-only projection of a resource allocation whose status is Confirmed (§10.8) | Displayed with "Approval status: Confirmed"; counted in the Confirmed band; changed only through §37.6 |
| Time away | An availability override (§10.8) with category Unavailable or Reduced | Explains reduced capacity; no separate leave record, reason or leave type |
| Remaining capacity | Capacity − Confidence Confirmed hours − Confidence Expected hours | May be negative; Possible hours are shown separately |
| Planner indicators | Over-planned and Under-planned (person and week) · Stale plan (planning entry) | Distinct from the Workload indicators Over-assigned, Under-assigned and Deadline cluster |

Confirmed has three meanings here — a confidence band, a visibility state and the §37.6 allocation status — and is never displayed without its qualifier (FR-PLN-04).

Planning settings are organisation settings, editable by System Administrators. `default_weekly_capacity_hours` (§10.4, Q18) remains the default capacity for both Workload and the planner.

| Setting key | Default | Allowed | Used by |
|---|---|---|---|
| `planning_horizon_weeks` | 12 | 6–26 | Weeks shown by the Weekly Planner |
| `planning_over_pct` | 105 | 50–300 | Over-planned (PLN-10) |
| `planning_under_pct` | 50 | 0–100 (0 switches it off) | Under-planned (PLN-11) |
| `planning_under_weeks` | 2 | 1–12 | Under-planned (PLN-11) |
| `planning_stale_days` | 28 | 1–365 | Stale plan (PLN-12) |
| `planning_max_hours_per_week` | 80 | 1–168 | Largest hours per week for one planning entry (PLN-02) |
