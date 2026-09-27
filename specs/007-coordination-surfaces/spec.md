# Feature Specification: Coordination Surfaces

**Feature Branch**: `007-coordination-surfaces`

**Created**: 2026-09-24

**Status**: Draft

**Input**: User description: "Convert the Engineering Project Coordination Hub specification into Spec Kit: packet 007, coordination surfaces (the Project Dashboard, Weekly Coordination with meeting mode, My Work, and My Staff with manager staffing)."

**Source**: Product specification §1, §2.1–§2.4, §3.1, §3.2, §6, §7.1–§7.5, §11.9 (FR-DASH, FR-WC, FR-MYW), §11.13 (FR-ASG-05–07), §12.13, §12.17, §12.18 (ASG-08–11), §13.1, §13.9, §13.10, §13.19, §14 Workflows 9 and 14, §15.13, §31.13, §31.16

## User Scenarios & Testing *(mandatory)*

### User Story 1 - The PM runs the weekly coordination meeting from the screen (Priority: P1)

The PM (persona Priya) opens Weekly Coordination, projects it, and walks the team through a fixed
agenda built from live data: headline, attention items, approaching milestones, deliverables due,
decisions required, blocked work grouped by cause, overdue work, work due this week, a round of
every discipline, recent completions, what is coming, and held items. Meeting mode enlarges the
text, steps through sections with the keyboard, and lets the PM re-date, reassign, comment, block,
and create items inline. At the end the PM marks the project reviewed and copies a summary into
the meeting chat.

**Why this priority**: This is the reason the product exists: the meeting runs without anyone
preparing status by hand.

**Independent Test**: With a seeded project, run a full meeting in meeting mode, make three inline
changes, mark it reviewed, and paste the summary; confirm each section's contents against its
definition in §12.13.

**Acceptance Scenarios**:

1. **Given** a project last reviewed 7 days ago, **Then** "Recently completed" lists exactly the tasks completed, deliverables issued, and decisions decided since then. *(AC-WC-01)*
2. **Given** three tasks Blocked by the same predecessor, **Then** the Blocked work section shows one group headed by the predecessor with the three tasks beneath. *(AC-WC-02)*
3. **Given** meeting mode, **When** the user presses the right arrow, **Then** focus moves to the next section header and it scrolls into view. *(AC-WC-03)*
4. **Given** meeting mode, **When** the PM changes a task's due date inline with a reason, **Then** the change is saved, logged, and notified as usual and appears in the "Changes made in this meeting" tray. *(AC-WC-04)*
5. **Given** the PM clicks Mark as reviewed, **Then** the review time is set to now and logged, and the "since last review" marker updates. *(AC-WC-05)*
6. **Given** Copy summary is clicked, **Then** the clipboard holds plain text with the headline, approaching milestones, decisions required, blocked work, and overdue work, each row including the item key. *(AC-WC-06)*
7. **Given** a Discipline Lead opens Weekly Coordination scoped to their discipline, **Then** every section contains only their discipline's items and the discipline round shows only their card. *(AC-WC-07)*

---

### User Story 2 - Everyone works from My Work (Priority: P1)

Each person (persona Alex) opens My Work every morning: attention items routed to them, their
tasks by due bucket, reviews waiting on them, deliverables and decisions they own, what they are
waiting on and who owns it, what they are holding up, their projects with their follow level,
upcoming milestones, and what they recently finished. Most updates happen right in the list.

**Why this priority**: Individual adoption, and so the accuracy of every flag, depends on My Work
being the one list people use.

**Independent Test**: Give a user tasks, reviews, a deliverable, and a blocked task across two
projects and confirm every section's contents and counts.

**Acceptance Scenarios**:

1. **Given** a user who is assignee on 3 tasks, collaborator on 1, reviewer on 2 that are Ready for Review, and owner of 1 deliverable, **Then** My Tasks shows 4, My Reviews shows 2, and My Deliverables shows 1. *(AC-MYW-01)*
2. **Given** one of the user's tasks is Blocked by another person's task, **Then** it appears under Waiting on Others with the blocker and its owner. *(AC-MYW-02)*
3. **Given** one of the user's tasks blocks two others, **Then** it appears under Blocking Others with the two successors and their owners. *(AC-MYW-03)*
4. **Given** a Supervisor opens a direct report's My Work, **Then** it is read-only and shows the same sections; **Given** someone who is neither that person's supervisor nor a PM of their projects tries the same, **Then** it is refused. *(AC-MYW-04)*
5. **Given** a task due yesterday, **Then** it appears first, in the Overdue bucket. *(AC-TSK-04, §13.10)*

---

### User Story 3 - The PM sees the project's state on one screen (Priority: P1)

The Project Dashboard answers "how is this project doing and what needs attention right now"
without scrolling on a 1080p screen: the next five milestones, health (computed and reported),
next milestone and submission, phase, counts that each open the list behind them, the top ten
attention items, a table per discipline, work due this week, blocked work, and recent activity.

**Why this priority**: PMs use it daily, and Executives and Supervisors drill into it.

**Independent Test**: With a seeded project, confirm every number on the dashboard equals the
count of the list it opens.

**Acceptance Scenarios**:

1. **Given** the dashboard shows Blocked = 4, **When** the user clicks it, **Then** the task list opens filtered to Blocked with exactly 4 rows. *(AC-DASH-01)*
2. **Given** a project with 6 disciplines, **Then** the discipline table shows one row per active discipline with lead, status colour, open, overdue, blocked, and deliverables due within 14 days, and each count matches its filtered list. *(AC-DASH-02)*
3. **Given** the milestone strip, **Then** it shows the next 5 non-complete milestones ordered by date with overdue ones first, each with status colour and countdown. *(AC-DASH-03)*
4. **Given** a project in Setup or On Hold, **Then** the dashboard shows a banner explaining why evaluation is paused. *(§13.1)*

---

### User Story 4 - Managers see and staff their direct reports (Priority: P2)

A Supervisor (persona Sam) opens My Staff to see every direct report: projects and roles, open,
overdue, and blocked work, what they are holding up, reviews waiting on them, deliverables due
soon, and last activity. Sam opens anyone's My Work read-only, reassigns their tasks, and assigns
them to projects as Team Members or removes them. Each staffing change tells the project's PM.
Executives and Admins can widen the view to all staff.

**Why this priority**: The business asked for it; it replaces asking each PM who is doing what.

**Independent Test**: As a supervisor with three direct reports, check the counts, assign one
person to a new project, remove another from a project with open work, and confirm the PM
notifications and log entries.

**Acceptance Scenarios**:

1. **Given** Sam supervises Alex and Lena supervises Sam, **When** Sam opens My Staff, **Then** Alex appears with project count, roles, open, overdue, blocked, blocking others, and reviews waiting; **When** Lena opens it, **Then** Sam appears and Alex does not; **Given** a Standard User without the Supervisor role, **Then** My Staff is refused. *(AC-ASG-07, ASG-08, ASG-09)*
2. **Given** Alex has 2 tasks on a Restricted project Sam cannot view, **When** Sam opens My Staff, **Then** those tasks are absent from Alex's counts and expanded assignments. *(AC-ASG-08, ASG-09, §36.1)*
3. **Given** Sam supervises Alex, **When** Sam assigns Alex to project 1301 with primary discipline Civil, **Then** Alex is a Team Member following 1301 at All activity, the PM of 1301 receives an in-app notification naming Sam, and the change is logged with Sam as actor; **When** Sam tries to add someone who does not report to him, or to make Alex a Discipline Lead, **Then** the action is not offered and is refused. *(AC-ASG-09, ASG-10)*
4. **Given** Alex owns 2 open tasks on 1301, **When** Sam removes Alex from 1301, **Then** Sam chooses to reassign them within the project or leave them flagged, gives a reason, and the PM is notified with the affected items. *(E-29, TM-04)*
5. **Given** someone else adds Alex to a project, **Then** Sam receives an in-app notice and sees the change in the next digest's My staff section. *(ASG-11, FR-ASG-07)*

---

### Edge Cases

- A meeting held on a different day from the project's coordination day still uses the configured day for "this week"; the PM can change the day in settings. *(§14 Workflow 9)*
- Two people editing the same item during the meeting: the second save gets the conflict prompt. *(E-23)*
- An employee who moves to another manager appears in the new supervisor's My Staff at once and leaves the old one's, whose read access to their My Work ends. *(E-25)*
- People with no supervisor appear only in the All staff scope and in the Admin "No supervisor" filter. *(E-26)*
- Inactive staff are hidden from My Staff unless "show inactive" is on; their open work is still flagged. *(ASG-08)*

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Weekly Coordination MUST compute "this week" from the project's coordination day (Monday if unset), "next week" as the following 7 days, and "since last review" from the last review time (or the last 7 days if never reviewed). *(§12.13)*
- **FR-002**: Weekly Coordination MUST show the 13 agenda sections defined in §12.13 in order, as collapsible cards with counts and a jump index, each with its definition and default sort; blocked work MUST be grouped by blocker (predecessor, decision, or manual reason). *(FR-WC-01, §12.13, §13.9)*
- **FR-003**: Meeting mode MUST enlarge type, hide filters, step through sections with the arrow keys, allow inline status, due-date (with reason), reassignment, comment, manual block, task creation, and decision recording on every row, collect every change in a "Changes made in this meeting" tray, and offer "hide items already discussed". *(§12.13, §13.9)*
- **FR-004**: The PM or a lead MUST be able to mark the meeting reviewed, which records the time and person and resets "since last review". *(FR-WC-01)*
- **FR-005**: Copy summary MUST put plain text of the headline, approaching milestones, decisions required, blocked work, and overdue work on the clipboard, with item keys. *(FR-WC-02)*
- **FR-006**: Weekly Coordination MUST offer a print-friendly view and a discipline scope. *(§13.9)*
- **FR-007**: My Work MUST show these sections with counts: Needs my attention, My Tasks (assignee or collaborator, by due bucket: Overdue, Today, This week, Next week, Later, No date), My Reviews, My Deliverables, Waiting on Others (with blocker and owner), Blocking Others (with successors and owners), My Decisions, My Projects (roles, follow level, health, next milestone), Upcoming Milestones (30 days), and Recently completed by me (14 days). *(FR-MYW-01, §13.10)*
- **FR-008**: My Work MUST allow status and progress changes in the row, starting a review, adding comments, and quick-creating a task in a chosen project; filter by project, discipline, status, priority, and due range, with "hide waiting"; and sort by due date, priority, project, or last activity, with overdue and due-today items first. *(§13.10)*
- **FR-009**: Supervisors MUST be able to open a direct report's My Work read-only, and PMs the tasks of their project members within their projects; others MUST be refused. *(§13.10, AC-MYW-04)*
- **FR-010**: The Project Dashboard MUST show the project header; the next 5 non-complete milestones (overdue first) with countdown; tiles for health (computed and reported, with note), next milestone, next submission, and phase; counts of tasks (total, complete, in progress, ready or in review, overdue, blocked, waiting, unassigned), deliverables (due within 14 days, at risk, issued or accepted, total), and decisions (pending, overdue); the top 10 attention items with open and snooze; the discipline table; up to 8 items due this week and 8 blocked; and the last 15 activity entries with "important only". *(FR-DASH-01, §13.1)*
- **FR-011**: Attention items MUST appear on the Project Dashboard, in Weekly Coordination, in My Work for the items a person owns, and as counts where projects are listed (FR-ATT-03). Every dashboard number MUST open the list that produced it, computed the same way, and the discipline scope chip MUST filter the counts, attention, discipline, due, blocked, and activity sections. *(§13.1, §12.17)*
- **FR-012**: The dashboard MUST fit a 1080p screen without scrolling for its headline, counts, and attention sections, avoid decorative charts, and show a banner for Setup and On Hold projects. *(§13.1)*
- **FR-013**: My Staff MUST show a Supervisor their direct reports (read at request time, inactive people hidden unless shown on request) and let Executives and Admins switch to all staff; it MUST list each person's project count, roles, open, overdue, blocked, and blocking tasks, reviews waiting on them, owned deliverables due within 14 days, and last activity, with each count opening the matching My Work section. *(FR-ASG-05, ASG-08, §13.19)*
- **FR-014**: Expanding a person MUST list their permitted assignments (project, health, roles, primary discipline, date added, open and overdue tasks there, next due item); work on Restricted projects the viewer cannot see MUST be absent from lists and counts. *(ASG-09, §13.19, §36.1)*
- **FR-015**: Supervisors MUST be able to reassign tasks owned by their direct reports, and add a direct report to, or remove them from, the team of any Setup, Active, or On Hold project they can view, as Team Member with a primary discipline; removal of someone with open work MUST run the reassignment prompt; each change MUST notify the PM and be logged with the supervisor as actor. All other team changes remain with the PM, who can remove anyone a supervisor added. *(FR-ASG-06, ASG-10)*
- **FR-016**: When someone else adds a person to or removes them from a project, or makes them a Discipline Lead, their supervisor MUST receive an in-app notice and see it in the next digest's My staff section, which also lists direct reports with overdue or blocked work or reviews waiting longer than the review threshold. *(FR-ASG-07, ASG-11, §17.3)*
- **FR-017**: My Staff MUST default to sorting by overdue, then blocked, then name, show counts only (no hours), and appear read-only on phones. *(§13.19)*

### Key Entities *(include if feature involves data)*

- **Coordination review**: The time and person that last marked a project's Weekly Coordination reviewed.
- **Staff scope**: The set of people a viewer may see in My Staff, derived from supervisor links (direct reports) or, for Executives and Admins, all staff.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Pilot PMs run their weekly coordination meeting from the Hub with no other preparation, replacing their status spreadsheet or email. *(G1, §2.4)*
- **SC-002**: A Discipline Lead sees everything their discipline owes and is waiting on in under 1 minute. *(§2.4)*
- **SC-003**: Pilot users update task status at least weekly in the Hub without being chased. *(G4)*
- **SC-004**: 100 % of dashboard and My Staff counts equal the lists they open in testing.
- **SC-005**: My Work and the Project Dashboard are usable within 2 seconds for 95 % of loads. *(§22)*
- **SC-006**: A supervisor can see who among their staff has overdue or blocked work without asking anyone. *(§2.4)*

## Assumptions

- The values shown come from packet 005 (derived state, attention, health) and the notifications from packet 006; issues, risks, and meeting actions on these surfaces are Phase 2 (packets 014 and 015); Portfolio and hours-based Workload are first-release scope (packets 016 and 017, §36).
- Supervisor scope is direct reports only (decided, Q19).
- The product's non-goals apply to every surface here (§3.2, §6): no scheduling engine, document storage, billing/payroll timesheets, or AI. Task-hour entry is first-release scope in packet 024 (§36.8).
- The Staff Assignments report is specified with the other reports in packet 009 (FR-ASG-08).

## Coordination expansion amendment — 2026-09-26

Packet 030 extends Weekly Coordination/My Work with incoming/outgoing handoffs, revision-use and impact checks, readiness and fixed weekly commitments (§37.7, §38.2). Marking a meeting reviewed is not an approval or a change to those source workflows.

This is approved specification scope with implementation pending in the named new packets. Historical verification for this packet does not verify the added behaviour.
