# Feature Specification: Projects and Teams

**Feature Branch**: `002-projects-and-teams`

**Created**: 2026-09-24

**Status**: Draft

**Input**: User description: "Convert the Engineering Project Coordination Hub specification into Spec Kit: packet 002, projects and teams (projects and their lifecycle, project roles and permissions, teams, disciplines and discipline leads, item keys, and the project list)."

**Source**: Product specification §7.1, §8.1–§8.9, §9.2, §9.3, §9.5, §10.1, §10.2 (project status), §10.7, §11.2, §12.1, §12.2, §13.2, §13.16, §14 Workflows 1 and 13, §15.2, §15.3, §31.2, §31.3, §31.4

## User Scenarios & Testing *(mandatory)*

### User Story 1 - A PM creates a project and its team (Priority: P1)

A Project Manager (persona Priya) creates a project in two steps: its identity (number, name,
client, office, type, dates, description, location, client reference) and its team (the
disciplines on the project, a lead for each, and members with roles). The creator becomes the
primary PM. The project starts in Setup, shows an advisory checklist, and goes Active when the
PM is ready.

**Why this priority**: The project is the container and permission boundary for all later
work.

**Independent Test**: Create a project with three disciplines and two members, then activate
it; confirm the team, roles, and log entries.

**Acceptance Scenarios**:

1. **Given** a project number already exists, **When** a PM creates a project with the same number in any letter case, **Then** creation fails with an error linking to the existing project. *(AC-PRJ-01, P-01, E-13)*
2. **Given** a PM creates a project, **When** creation succeeds, **Then** the PM is the primary Project Manager and a member with the PM role, and the creation is logged. *(AC-PRJ-02, FR-PRJ-02)*
3. **Given** a new project, **Then** it is in Setup with a banner and an advisory checklist (at least one milestone, a lead for every discipline, a deliverable for every submission milestone); **When** the PM activates it with items unchecked, **Then** activation is allowed. *(P-09, §14 Workflow 1)*
4. **Given** the client does not exist yet, **When** the PM creates the project, **Then** they can choose "Internal / TBD" and ask an Admin to add the client. *(§14 Workflow 1)*
5. **Given** a PM sets a Discipline Lead who is not a member, **When** saved, **Then** that person is added to the team and receives a "You are Discipline Lead" notification. *(AC-TEAM-02)*

---

### User Story 2 - Roles decide what each person can do (Priority: P1)

Permissions combine two layers: organisation-wide system roles and per-project roles (PM,
Discipline Lead for a discipline, Team Member, Reviewer, Viewer), extended by ownership of an
item. The same person can be PM on one project, lead on another, and a team member on a third
without ever switching roles. Every employee can see every project unless a project is
Restricted.

**Why this priority**: Clear ownership and safe editing depend on it, and every later packet
reuses it.

**Independent Test**: Run the permission matrix rows for project-level actions (§8.5) against
users holding each role and confirm allowed and refused actions.

**Acceptance Scenarios**:

1. **Given** a Read Only user, **When** they open any project they can see, **Then** every edit action is hidden and comment posting is disabled. *(AC-PERM-04)*
2. **Given** Restricted visibility is enabled and a project is Restricted, **When** a non-member Standard User searches for its number, **Then** no result appears, and opening its address directly reports that it does not exist. *(AC-PERM-05, §8.7)*
3. **Given** a Discipline Lead, **When** they try to edit project information or change the project status, **Then** the action is not offered and a direct attempt is refused with a message naming the Project Manager role. *(§8.5.2)*
4. **Given** one person is PM, Discipline Lead, and assignee on the same project, **Then** their permissions are the union of those roles and they never have to switch role. *(E-19, §8.4)*

---

### User Story 3 - The PM manages the project lifecycle (Priority: P1)

The PM moves the project between Setup, Active, On Hold, Complete, Archived, and Cancelled.
Holding, cancelling, and completing need a reason; completing shows a closeout checklist.
Archived and Cancelled projects are read-only; an Admin can unarchive to correct records.

**Why this priority**: Status controls whether a project is evaluated, notified, and counted,
and closing projects keeps the portfolio honest.

**Independent Test**: Take a project through Active, On Hold, Active, Complete, Archived, and
Admin unarchive, confirming the rules and log entries at each step.

**Acceptance Scenarios**:

1. **Given** an Active project with 42 open tasks, **When** the PM puts it On Hold with a reason, **Then** the confirmation states the consequence in numbers, the change is logged with the reason, and the project stops being evaluated for overdue, blocked, attention, and health until it resumes. *(AC-PRJ-03, P-03, G-05; evaluation effects verified with packet 005)*
2. **Given** an Active project with 3 open tasks and 1 un-issued deliverable, **When** the PM marks it Complete, **Then** the closeout checklist lists those items with counts, requires a reason, lets the PM cancel each group or leave it open, and afterwards the project is Complete with its completion time recorded. *(AC-PRJ-04, P-04)*
3. **Given** a Complete project, **When** the PM archives it, **Then** it is read-only for everyone, absent from the default project list, present when "include archived" is on, and its items remain openable. *(AC-PRJ-05, P-06)*
4. **Given** an Archived project, **When** an Admin unarchives it, **Then** its status is Complete, the PM can edit with reasons, and both actions are logged. *(AC-PRJ-06, E-11)*
5. **Given** a Complete project more than 30 days old, **Then** its dashboard suggests archiving, and PM edits still need reasons until it is archived. *(P-05)*

---

### User Story 4 - The PM keeps the team and disciplines current (Priority: P2)

The PM adds and removes members, sets each member's roles and primary discipline, adds
disciplines, and changes leads or the primary PM as the project evolves.

**Why this priority**: Teams change during every project; wrong membership misroutes work and
notifications.

**Independent Test**: Add and remove members and a discipline, change a lead and the primary PM,
and confirm the guards and log entries.

**Acceptance Scenarios**:

1. **Given** the primary PM, **When** anyone tries to remove them from the team, **Then** the action is refused with guidance to change the PM first. *(AC-TEAM-04, TM-03)*
2. **Given** the PM changes the primary PM to Marc, **Then** Marc gains the PM role and is notified, the previous PM stays a member unless removed, attention items and digests route to Marc within one evaluation cycle, and the change is logged. *(FR-TEAM-04, E-02; routing verified with packets 005 and 006)*
3. **Given** a discipline with non-cancelled deliverables or tasks, **When** the PM tries to remove it, **Then** removal is refused and deactivation is offered, which hides it from pickers but keeps existing items. *(TM-05)*
4. **Given** a user already on the team, **When** the PM adds them again, **Then** no duplicate membership is created; the same applies to disciplines. *(TM-01, TM-02)*

---

### User Story 5 - Everyone finds their projects (Priority: P2)

The project list shows every project the user can see, with "My projects" as the default for
anyone holding a project role, and supports search, filters, sorting, column choice, and
personal stars.

**Why this priority**: With hundreds of active projects, finding the right one quickly is basic
usability.

**Independent Test**: With 20 projects across offices and statuses, filter to one office and
status, sort, star one, and open it.

**Acceptance Scenarios**:

1. **Given** a user with project roles, **When** they open Projects, **Then** the list shows their Active, Setup, and On Hold projects with number, name, client, PM, office, phase, status, health, next milestone, overdue and blocked counts, their role, and last activity. *(§13.2; health and counts appear once packet 005 is in place)*
2. **Given** filters for status, PM, client, office, phase, discipline, health, project type, and "include archived", **When** the user combines them, **Then** only matching projects appear.
3. **Given** a user stars a project, **Then** it is pinned to the top of their own list and nobody else's.

---

### Edge Cases

- Project numbers may carry sub-project suffixes (for example `1234-02`) if the format allows; each is a distinct project. *(E-13)*
- Changing a project number is Admin-only and logged; item keys keep the number they were created with, and screens show the current number with the original in a tooltip. *(P-07, §9.5)*
- A Complete project can be reopened to Active by the PM with a reason. *(§10.2)*
- Setup and Cancelled projects can be cancelled; Cancelled projects are read-only like Archived ones. *(§10.2, P-06)*
- Clicking a project's health cell opens its dashboard with the "Why?" explanation shown. *(§13.2)*
- When an On Hold project resumes, a banner lists items whose dates passed while it was on hold, with an action to shift them all by a number of days. *(E-05)*

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Users with the Project Manager system role, and Admins, MUST be able to create a project with the fields in §12.1; the project number MUST be unique ignoring case and match the organisation format. *(FR-PRJ-01, P-01)*
- **FR-002**: System MUST make the creator the primary PM and give them the PM project role; the primary PM MUST always hold that role and cannot be removed from the team. *(FR-PRJ-02, P-08, TM-03)*
- **FR-003**: The PM MUST be able to edit project information, important links (SharePoint site, Teams channel, network folder, client portal), phase, coordination day, and internal notes. *(FR-PRJ-03, §12.1)*
- **FR-004**: System MUST allow only these status transitions: Setup to Active; Active to On Hold and back; Active to Complete; Active, On Hold, or Setup to Cancelled; Complete to Archived; Complete to Active with a reason; Archived to Complete by an Admin. *(FR-PRJ-04, P-02, §10.2)*
- **FR-005**: System MUST require a reason for On Hold, Cancelled, and Complete, state the consequence of each change in numbers before confirming, and log it. *(P-03, §12.1)*
- **FR-006**: Completing a project MUST present the closeout checklist of open tasks, un-issued deliverables, and open decisions, letting the PM cancel each group with a reason or leave it open, and logging the choice. *(P-04)*
- **FR-007**: A Complete project MUST stay editable by the PM for the configured window (default 30 days), after which archiving is suggested. *(P-05)*
- **FR-008**: Archived and Cancelled projects MUST be read-only for everyone, hidden from default lists, and searchable with "include archived"; Admins MUST be able to unarchive to Complete. *(FR-PRJ-07, P-06, E-11)*
- **FR-009**: Only Admins MAY change a project number, and the change MUST be logged. *(P-07)*
- **FR-010**: System MUST store a visibility setting (Open or Restricted) on every project and enforce it everywhere: Restricted projects are visible only to members, Admins, and Executives. The control to change it is shown only if the organisation enables Restricted projects. *(FR-PRJ-08, §8.7, Q4)*
- **FR-011**: The PM MUST be able to add and remove members, set their project roles (PM, Team Member, Reviewer, Viewer), and set a primary discipline. *(FR-TEAM-01, §12.2)*
- **FR-012**: The PM MUST be able to add disciplines from the organisation list and set one Discipline Lead per discipline; setting a lead who is not a member MUST add them to the team and notify them. *(FR-TEAM-02, AC-TEAM-02)*
- **FR-013**: System MUST keep each user and each discipline at most once per project. *(TM-01, TM-02)*
- **FR-014**: System MUST refuse to remove a discipline with non-cancelled items and offer deactivation instead. *(TM-05)*
- **FR-015**: A Discipline Lead MUST be an active user, and removing someone from the team MUST remove their project roles while keeping their historical references. *(TM-07, TM-08)*
- **FR-016**: Changing the primary PM MUST grant the PM role to the new PM, notify them, keep the previous PM as a member unless removed, and re-route attention items and digests. *(FR-TEAM-04, E-02)*
- **FR-017**: System MUST evaluate every action against the union of the user's system roles, project roles, and item ownership, deny by default, on the server, following the matrices in §8.5 and the ownership rules in §8.6. *(§8.1, §8.5, §8.6, §8.9)*
- **FR-018**: The interface MUST hide or disable actions the user cannot perform and explain why on hover. *(§8.9)*
- **FR-019**: Administrators MUST act as themselves; there is no impersonation. *(§8.9)*
- **FR-020**: Each project MUST issue human-readable keys for its items (`{project}-M01`, `-D001`, `-T0001`, `-DEC01`, and in Phase 2 `-R01`, `-I01`, `-A01`) from per-project sequences; keys MUST never change or be reused. *(§9.5, G-08)*
- **FR-021**: The project list MUST support search, filters (status, PM, client, office, phase, discipline, health, project type, include archived), sorting on any column (default: health severity, then next milestone date), a column chooser, a "My projects" default for anyone with project roles, and personal stars. *(FR-PRJ-06, §13.2)*
- **FR-022**: The project header MUST show key, name, client, PM, phase, status, health with "Why?", next milestone with countdown, quick links, and the user's follow control, and project screens MUST share tabs as in §9.4. *(§12.1, §13.0, §9.4)*

### Key Entities *(include if feature involves data)*

- **Project**: Number, name, client, client reference, primary PM, office, project type, description, location, status, phase, start and target completion dates, visibility, internal notes, coordination day, template origin (Phase 2), and computed values supplied by later packets.
- **Project link**: A titled link to a SharePoint site, Teams channel, network folder, client portal, or other location.
- **Project discipline**: A discipline active on a project, with its lead, order, and active flag.
- **Project member**: A user on a project with one or more project roles and a primary discipline; removal keeps history.
- **Item key sequence**: The per-project counters that issue item keys.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A PM can create a project with its disciplines, leads, and team in under 5 minutes.
- **SC-002**: 100 % of the permission-matrix rows for project-level actions pass automated checks for every role.
- **SC-003**: Zero duplicate project numbers can be created in testing, including letter-case variants.
- **SC-004**: In testing, Restricted projects never appear to non-members in lists, search, or direct links.
- **SC-005**: A user finds and opens a specific project among 300 in under 15 seconds using search or filters.

## Assumptions

- Open decisions use the specification's defaults: open-by-default visibility with the Restricted setting enforced but its control hidden (Q4); manual project numbers with a free format plus uniqueness (Q5); project creation limited to PM role holders and Admins (Q13).
- Auto-adding a person when they are assigned a task or review, and the reassignment prompt when a member who owns open items is removed, are specified with tasks in packet 004 (TM-04, TM-06, AC-TEAM-01, AC-TEAM-03).
- Following a project on assignment is specified in packet 006 (§12.18). Supervisors staffing their direct reports is specified in packet 007.
- Health, next-milestone, and count columns, the health sort, and the effect of On Hold on evaluation appear once packets 003 and 005 are in place, following the cross-packet rule in `specs/README.md`.
- Permission-matrix rows for milestones, deliverables, tasks, decisions, and comments are specified and tested in the packets that introduce those items.
