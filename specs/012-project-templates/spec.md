# Feature Specification: Project Templates

**Feature Branch**: `012-project-templates`

**Created**: 2026-09-24

**Status**: Draft (Phase 2)

**Input**: User description: "Convert the Engineering Project Coordination Hub specification into Spec Kit: packet 012, project templates (building and publishing templates, creating a project from a template with a date wizard, adding a discipline pack from a template, and snapshot semantics)."

**Source**: Product specification §8.5.1, §11.10 (FR-TPL), §11.12 (FR-ADM-04), §12.14, §13.15, §14 Workflow 2, §17.5, §32 (E-12), Appendix A

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Administrators build and publish templates (Priority: P1)

An Admin, or a user with the Template Editor flag, builds a template such as "Municipal
Infrastructure Design": the disciplines included by default, milestones with date offsets,
deliverables per discipline with due offsets, tasks with estimates, review flags, and who they
are assigned to, and the dependencies between tasks. Publishing gives the template a new version.

**Why this priority**: Templates are only as good as the curated structure inside them.

**Independent Test**: Build a small template with two disciplines, three milestones, four
deliverables, eight tasks, and five dependencies; publish it; preview instantiation.

**Acceptance Scenarios**:

1. **Given** a Draft template, **When** an Admin publishes it, **Then** its version increments and it appears in the project-creation wizard; **When** it is retired, **Then** it disappears from the wizard but stays referenced by projects made from it. *(§12.14)*
2. **Given** a user without the Admin role or Template Editor flag, **Then** template editing is not offered. *(§8.5.1)*
3. **Given** a published template, **When** an editor changes it, **Then** a new Draft is created and the published version is unchanged until the Draft is published. *(§12.14)*

---

### User Story 2 - A PM creates a fully structured project in minutes (Priority: P1)

The PM chooses a template, unticks disciplines that are out of scope, picks a lead for each, and
enters the start date. Milestone dates are computed from the template's offsets, and the PM types
the contractual dates over them; blanks stay undated and are flagged. A summary shows what will be
created and warns about undated items. The project is created in Setup with every deliverable,
task, and dependency in place.

**Why this priority**: Setting up a 150-task project by hand takes hours and is where structure
is lost.

**Independent Test**: Reproduce Appendix A: create "DCC Dundurn Roads" from "Municipal
Infrastructure Design" and compare the result.

**Acceptance Scenarios**:

1. **Given** the Appendix A template, all six default disciplines, start date 2026-10-05, and contractual dates for M03–M07, **When** the PM creates the project, **Then** it has 11 milestones (5 dated by contract, 2 by offset, 4 undated), 28 deliverables, about 120 tasks, and about 95 dependencies, and is in Setup. *(Appendix A.2)*
2. **Given** a discipline is unticked, **Then** its deliverables and tasks are not created and every dependency involving its tasks is dropped. *(§12.14 step 6)*
3. **Given** tasks set to "assign to Discipline Lead", **Then** each is assigned to the lead chosen in the wizard for that discipline; "PM" goes to the creating PM; "Unassigned" stays empty. *(§12.14 step 4)*
4. **Given** 40 tasks were assigned during set-up, **When** the project is activated, **Then** each assignee receives one batched notification, and no digest mentions the project while it is in Setup. *(§17.5, §12.14 step 7)*
5. **Given** the template is retired while the PM is in the wizard, **When** they try to create the project, **Then** creation is refused with an explanation. *(§14 Workflow 2)*

---

### User Story 3 - Add a discipline pack to an existing project (Priority: P2)

When scope grows (Transportation joins mid-project), the PM appends one discipline's deliverables
and tasks from any published template, anchored to the project's existing milestones by name,
confirming the mapping first.

**Why this priority**: Scope changes are common, and rebuilding a discipline by hand is slow.

**Independent Test**: Add the Transportation pack to a project created without it and confirm
the milestone mapping and dates.

**Acceptance Scenarios**:

1. **Given** a project without Transportation, **When** the PM adds the Transportation pack, **Then** the Hub proposes a mapping from template milestones to project milestones by name, the PM confirms it, and the pack's deliverables and tasks are created with dates computed from the mapped milestones. *(FR-TPL-02, §12.14)*

---

### User Story 4 - Templates never change existing projects (Priority: P3)

A project copies its template at creation and records which template and version it came from.
Later template changes never touch existing projects.

**Why this priority**: Predictability; propagating changes would rewrite live plans.

**Independent Test**: Change a template after a project was created from it and confirm the
project is untouched.

**Acceptance Scenarios**:

1. **Given** a project created from version 3 of a template, **When** version 4 is published, **Then** the project is unchanged and still records version 3. *(E-12)*

---

### Edge Cases

- Computed dates in the past are allowed; items become Overdue only once the project is activated. *(§14 Workflow 2)*
- Milestones left undated leave their deliverables and tasks undated, and the summary says so. *(§12.14 step 3)*
- Milestone names can repeat between the template and manual entries without conflict, because every item has its own key. *(§14 Workflow 2)*
- There is deliberately no "compare with template" view. *(E-12)*

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Admins, and users with the Template Editor flag, MUST be able to create and edit templates with disciplines (included by default or not), milestones (type, order, offset from project start or the previous milestone, completes-phase, client-facing), deliverables (discipline, type, target milestone, due offset, requires review, description), tasks (deliverable, discipline, description, requires review, priority, estimate, due offset, assign to Discipline Lead, PM, or no one), and task dependencies. *(FR-TPL-01, FR-ADM-04, §12.14, §8.5.1)*
- **FR-002**: Templates MUST be Draft, Published, or Retired; publishing MUST increment the version; retired templates MUST leave the wizard but stay referenced. *(§12.14)*
- **FR-003**: Admins MUST be able to preview a template's instantiation. *(§13.15)*
- **FR-004**: Creating a project from a template MUST follow the steps in §12.14: choose disciplines and leads; compute milestone dates from the start date and offsets, accepting typed contractual dates and leaving blanks undated and flagged; compute deliverable and task due dates from offsets; resolve assignees; copy everything with its template origin; drop dependencies involving unticked disciplines; create the project in Setup. *(FR-TPL-01, §12.14)*
- **FR-005**: The wizard's summary MUST show the counts of deliverables, tasks, and dependencies to be created and warn about items that will be undated. *(§14 Workflow 2)*
- **FR-006**: Instantiation MUST log one "created from template" entry plus the item creations, and hold assignment notifications until activation, batched per person. *(§12.14, §17.5)*
- **FR-007**: The PM MUST be able to append one discipline pack or deliverable set from any published template to an existing project, mapping template milestones to project milestones by name with the PM's confirmation. *(FR-TPL-02, §12.14)*
- **FR-008**: Projects MUST record the template and version they came from, and later template changes MUST NOT propagate. *(§12.14, E-12)*
- **FR-009**: When templates exist, they MUST replace "copy the structure of an existing project" as the recommended set-up path. *(§28)*

### Key Entities *(include if feature involves data)*

- **Project template**: Name, description, project type, version, status, publication date, author.
- **Template discipline, milestone, deliverable, task, and dependency**: The template's structure, using offsets and roles instead of dates and people.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A PM creates a structured project with 6 disciplines, 11 milestones, about 40 deliverables, and about 150 tasks in under 10 minutes. *(§14 Workflow 2)*
- **SC-002**: The Appendix A instantiation produces the stated counts exactly in testing.
- **SC-003**: Publishing a new template version changes no existing project (100 % in testing).

## Assumptions

- Appendix A is the reference template and acceptance example; offsets there are illustrative defaults PMs overtype.
- Whether PMs may edit templates without the Template Editor flag follows the specification's recommendation: Admins and flagged users only.
- Depends on the MVP packets 002–006.
