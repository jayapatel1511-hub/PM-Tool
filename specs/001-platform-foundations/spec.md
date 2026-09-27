# Feature Specification: Platform Foundations

**Feature Branch**: `001-platform-foundations`

**Created**: 2026-09-24

**Status**: Draft

**Input**: User description: "Convert the Engineering Project Coordination Hub specification into Spec Kit: packet 001, platform foundations (corporate sign-in, users and system roles, reference data and organisation settings, the application frame, and the activity-log foundation)."

**Source**: Product specification §5.2, §7.7, §8.2, §8.8, §9.4, §10.4, §10.6, §11.1, §11.12, §13.0, §13.15, §20, §20.4, §21, §22, §23.6, §31.1, §31.15

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Sign in with the corporate account (Priority: P1)

Any employee opens the Hub, signs in with their corporate Microsoft account, and lands on their
My Work page. There is no separate Hub password. People whose corporate account is disabled
cannot get in.

**Why this priority**: Nothing else in the product is usable without identity, and single
sign-on removes the first adoption barrier.

**Independent Test**: Sign in as an employee who has never used the Hub; confirm a user record
exists with their details and they land on My Work (empty until later packets add work).

**Acceptance Scenarios**:

1. **Given** an employee with a valid corporate account who has never used the Hub, **When** they sign in, **Then** a user record is created with their display name and email, they hold the Standard User role, and they land on My Work. *(AC-AUTH-01)*
2. **Given** an employee whose corporate account is disabled, **When** they attempt to sign in, **Then** sign-in fails and, after the next directory synchronisation, the user is marked Inactive and appears with an "(Inactive)" suffix wherever referenced. *(AC-AUTH-02)*
3. **Given** a request for any page or data without a valid sign-in, **When** it reaches the Hub, **Then** it is refused, except for the health status. *(AC-AUTH-04)*
4. **Given** a signed-in user who is idle longer than the configured period (default 8 hours), **When** they return, **Then** they must sign in again. *(FR-AUTH-05, Q17)*

---

### User Story 2 - Administrators maintain reference data and settings (Priority: P1)

A System Administrator (persona Jordan) maintains the lists every project uses: disciplines,
clients, offices, deliverable types, phases, and project types. They also maintain the
organisation settings: the rule thresholds, notification defaults, project-number format,
organisation time zone, date format, and whether self-review is allowed.

**Why this priority**: Projects cannot be created or evaluated until disciplines, clients,
offices, and thresholds exist.

**Independent Test**: As an Admin, add a discipline and a client, change a threshold, and
deactivate a deliverable type; confirm each change takes effect and is recorded.

**Acceptance Scenarios**:

1. **Given** an Admin, **When** they add a discipline with name, code, colour, and sort order, **Then** it appears in discipline pickers in that order.
2. **Given** a discipline used by 12 projects, **When** the Admin deactivates it, **Then** the Hub first states that 12 projects use it; after confirmation it disappears from pickers while existing projects keep it. *(E-18)*
3. **Given** an Admin changes "task due soon" from 5 to 7 days, **When** they save, **Then** the new value is used from then on and the activity history shows the old and new values and the Admin as actor. *(§10.4, §20.1)*
4. **Given** a user without the System Administrator role, **When** they try to open administration, **Then** it is not offered and direct access is refused.
5. **Given** the Admin changes a notification default, **When** it saves, **Then** new users receive it and existing users keep the choices they already made. *(§17.4)*

---

### User Story 3 - System roles come from directory groups (Priority: P2)

Executives, Supervisors, Project Managers, Read Only users, and Admins receive their system role
from corporate security groups, checked at every sign-in. Admins can add a role in the Hub for
exceptions. Every signed-in employee is a Standard User.

**Why this priority**: Roles decide who can create projects, see portfolios, and administer the
Hub; group mapping keeps access owned by IT.

**Independent Test**: Put a test user in the Project Manager group, sign in, and confirm the
Create project action appears; remove them and confirm it disappears at the next sign-in.

**Acceptance Scenarios**:

1. **Given** a user in the Project Manager group, **When** they open Projects, **Then** Create project is visible; **Given** a user without that role, **Then** it is not visible and a direct create attempt is refused. *(AC-AUTH-03)*
2. **Given** an Admin adds the Supervisor role to a user in the Hub, **When** the user's groups change, **Then** the manually added role stays and is shown with the source "manual", while group-derived roles follow the groups. *(§8.8)*
3. **Given** the only active System Administrator, **When** anyone tries to remove that person's Admin role, **Then** the change is refused with an explanation.

---

### User Story 4 - Leavers are deactivated and supervisor links are kept (Priority: P2)

A nightly directory synchronisation marks disabled accounts Inactive within 24 hours and keeps
job title, office, and manager up to date. Admins can deactivate a user and set a user's
supervisor by hand. Inactive people can no longer be chosen as owners, while their past work
stays attributed to them.

**Why this priority**: Turnover is certain; stale owners are the most common cause of work that
nobody is doing.

**Independent Test**: Disable a test account in the directory, run the synchronisation, and
confirm the user is Inactive, cannot sign in, and is missing from people pickers.

**Acceptance Scenarios**:

1. **Given** a user disabled in the directory, **When** the nightly synchronisation runs, **Then** the user is marked Inactive, logged with actor System, and removed from owner, assignee, and reviewer pickers. *(FR-AUTH-04, G-11, AC-AUD-03)*
2. **Given** a user whose directory manager is Sam, **When** the synchronisation runs, **Then** Sam becomes the user's supervisor; **Given** an Admin tries to make a user their own supervisor, **Then** the save is refused. *(§8.8)*
3. **Given** an Admin filters users by "No supervisor", **Then** every active user without a supervisor is listed. *(E-26)*

---

### User Story 5 - Every administrative change is permanently recorded (Priority: P1)

Every change the Hub makes is written to an activity log at the same moment as the change, with
who made it, when, what changed from and to, and why when a reason was required. Nobody can
alter or delete that record. This packet records administrative changes, role changes, user
activation changes, sign-ins, and exports; later packets record their own changes through the
same log.

**Why this priority**: Traceability is a core promise of the product and is costly to retrofit.

**Independent Test**: Make an administrative change, then confirm the organisation activity
history shows it with actor, time, and old and new values, and that no screen or action can
change or remove the entry.

**Acceptance Scenarios**:

1. **Given** the system expires or changes something on its own, **Then** the log entry shows actor System and, where a person's action triggered it, a link to that action. *(AC-AUD-03, G-10)*
2. **Given** any user, including an Admin, **Then** no screen or action exists to edit or delete an activity-log entry. *(AC-AUD-04)*
3. **Given** a user signs in successfully or exports a report, **Then** the event is recorded; viewing items is not recorded. *(§20.1)*

---

### User Story 6 - A consistent, accessible application frame (Priority: P3)

Every screen shares one frame: a navigation rail showing only the areas the user may use, a top
bar with the search box, quick-create, the notification bell, and the user menu. The frame
adapts to desktop, tablet, and phone, works from the keyboard, and never relies on colour alone.

**Why this priority**: Later packets place their screens in this frame; it has little value on
its own.

**Independent Test**: Sign in as a Standard User and as an Admin and compare the navigation;
use the frame with the keyboard only and at phone width.

**Acceptance Scenarios**:

1. **Given** a Standard User, **Then** the navigation shows My Work, Projects, Reports, and Notifications but not My Staff or Admin; **Given** an Admin, **Then** Admin is also shown. *(§9.4)*
2. **Given** any screen, **When** the user presses `/`, **Then** focus moves to the search box, and every control is reachable by keyboard with a visible focus. *(§13.0)*
3. **Given** a phone-width screen, **Then** navigation becomes a bottom bar with My Work, Search, and Notifications. *(§13.0)*

---

### Edge Cases

- A new user's directory profile has no office or job title: the user record is created with those fields empty and they can be filled later. *(FR-AUTH-02)*
- Directory synchronisation permission is not approved: Admins deactivate leavers manually, and a disabled account that fails to sign in is marked Inactive at that attempt. *(§23.6, Q3)*
- A user's email changes in the directory: the Hub matches people by their directory identity, not email, and updates the email.
- Reference data in use is never hard-deleted; deactivation only hides it from pickers. *(FR-ADM-01)*
- "Today" for every date rule is the calendar date in the organisation time zone, so a user in another zone may see an item turn overdue an hour early or late. *(E-21)*

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST authenticate users only through the organisation's corporate single sign-on and MUST NOT store passwords. *(FR-AUTH-01, §21, T2)*
- **FR-002**: System MUST create or update a user record at first sign-in with display name, email, directory identity, job title, and office when available. *(FR-AUTH-02)*
- **FR-003**: System MUST treat every signed-in active employee as a Standard User. *(§8.2)*
- **FR-004**: System MUST derive the System Administrator, Executive, Supervisor, Project Manager, and Read Only roles from directory groups at each sign-in, allow Admins to add roles manually, and show each role's source. *(FR-AUTH-03, §8.8, Q2)*
- **FR-005**: System MUST refuse sign-in for accounts disabled in the directory and MUST mark them Inactive within 24 hours through a scheduled synchronisation; Admins MUST be able to deactivate a user manually. *(FR-AUTH-04, FR-ORG-07)*
- **FR-006**: System MUST keep users signed in without interruption during use and sign them out after a configurable idle period. *(FR-AUTH-05, Q17)*
- **FR-007**: System MUST refuse every page and data request without a valid sign-in, except a health status that reveals nothing but availability. *(AC-AUTH-04, §22)*
- **FR-008**: Admins MUST be able to maintain disciplines (name, code, colour, active flag, sort order). *(FR-ORG-01)*
- **FR-009**: Admins MUST be able to maintain clients (name, short name, active flag), including an "Internal / TBD" client. *(FR-ORG-02, §14 Workflow 1)*
- **FR-010**: Admins MUST be able to maintain offices (name, code, time zone). *(FR-ORG-03)*
- **FR-011**: Admins MUST be able to maintain deliverable types (name, optional default discipline, active flag). *(FR-ORG-04)*
- **FR-012**: Admins MUST be able to maintain the ordered phase list, defaulting to Proposal/Setup, Kickoff, Field Investigation, Preliminary Design, Detailed Design, IFC, Tender, Construction, Closeout. *(FR-ORG-05, §9.3)*
- **FR-013**: Admins MUST be able to maintain project types. *(§12.1, §13.15)*
- **FR-014**: Admins MUST be able to maintain organisation settings: every threshold in §10.4 with its default, the enabled flag of each attention rule, notification defaults, the project-number format, whether self-review is allowed, the organisation time zone, the date format, and the digest send time. *(FR-ORG-06, FR-ADM-03, §12.12)*
- **FR-015**: System MUST deactivate rather than delete reference data that is in use, hide deactivated entries from pickers, keep existing records valid, and state how many projects are affected before confirming. *(FR-ADM-01, E-18)*
- **FR-016**: Admins MUST be able to maintain each user's supervisor and active state; the supervisor MUST come from the directory manager when available, and a user MUST NOT be their own supervisor. *(FR-ORG-07, §8.8)*
- **FR-017**: System MUST prevent Inactive users from being chosen as an owner, assignee, reviewer, or lead, and MUST show existing references with an "(Inactive)" suffix. *(G-11, AC-AUTH-02)*
- **FR-018**: System MUST record in an activity log every creation, field-level update, status change, deletion, ownership change, administrative change, role change, activation change, successful sign-in, and export, with actor (user, System, or Admin), time, item, old and new values, reason when required, and source; each entry MUST be written together with the change it describes. *(FR-AUD-01, §20.1, §20.2, §20.3, T5)*
- **FR-019**: System MUST NOT offer any way, for any user, to edit or delete an activity-log entry. *(AC-AUD-04, §20.3)*
- **FR-020**: System MUST attribute automatic changes to System and link them to the triggering user action when there is one. *(G-10, AC-AUD-03)*
- **FR-021**: Admins MUST be able to view and filter the organisation-level history of administrative changes. *(§13.14, §20.3)*
- **FR-022**: System MUST show a navigation rail with only the areas the user may use, including first-release routes Home, My Work, Boards, Projects, Tasks, Calendar, Files, Time, Reports, Team, Portfolio, Resources, Notifications, My Staff, and Admin as applicable, and a top bar with search, quick-create, notifications, and the user menu. Time opens task-hour entry. *(§9.4, §13.0, §36.1, §36.8)*
- **FR-023**: System MUST adapt the frame to desktop (1280 px and wider), tablet (768–1279 px), and phone (under 768 px) widths as described in §13.0. *(§13.0)*
- **FR-024**: System MUST make every control reachable by keyboard with a visible focus, never use colour as the only carrier of meaning, and meet WCAG 2.1 AA. *(§13.0, §22)*
- **FR-025**: System MUST show dates in the organisation date format with relative helpers ("in 4 days") and timestamps in the viewer's local time, and MUST evaluate "today" in the organisation time zone. *(§10.6, §13.0)*
- **FR-026**: System MUST keep all interface text separate from code so a second language can be added without rework. *(§22, Q7)*
- **FR-027**: System MUST refuse removal of the System Administrator role from the last active administrator.

### Key Entities *(include if feature involves data)*

- **User**: An employee known to the Hub: directory identity, email, display name, job title, office, supervisor, active state, weekly capacity (used in Phase 2), last sign-in.
- **System role assignment**: A user's organisation-wide role (Admin, Executive, Supervisor, Project Manager, Read Only) with its source (group or manual).
- **Reference data**: Offices, disciplines, clients, deliverable types, phases, and project types, each with an active flag and, where relevant, an order.
- **Organisation setting**: A named value such as a threshold, a rule's enabled flag, or a notification default.
- **Activity-log entry**: An immutable record of one change: time, actor and actor type, project (if any), item and its key, action, field changes, reason, correlation to a triggering action, and source.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A new employee can sign in and reach My Work in under 1 minute on first use, with no account setup.
- **SC-002**: In testing, 100 % of accounts disabled in the directory cannot sign in, and all are shown as Inactive in the Hub within 24 hours.
- **SC-003**: An Admin can add, edit, or deactivate a reference-data entry in under 1 minute.
- **SC-004**: 100 % of administrative changes made during acceptance testing appear in the activity history with actor, time, and old and new values, and no entry can be modified.
- **SC-005**: Screens in this packet are usable within 2 seconds for 95 % of loads on the corporate network. *(§22)*
- **SC-006**: Every screen in this packet passes automated WCAG 2.1 AA checks and a keyboard-only walk-through.

## Assumptions

- The organisation's identity provider is Microsoft Entra ID, and single sign-on uses it (§21, §23.6). This is a business constraint, not a design choice left to the plan.
- Open decisions use the specification's defaults unless decided first: system roles from directory groups plus manual additions (Q2); directory-synchronisation and mail permissions requested, with manual deactivation as the fallback (Q3); one organisation time zone and ISO dates (Q6); English at launch with text externalised (Q7); an 8-hour idle sign-out (Q17).
- The search box is part of the frame; its results arrive with packet 009. The notification bell arrives empty; notifications come with packet 006.
- Environments, deployment, monitoring, and backups are planning concerns (§22, §23.7–§23.9), carried by `/speckit-plan` for this packet and packet 011. The backend stack (Q1) and the CI/CD platform (Q15) are fixed in this packet's plan.
- The last-administrator guard (FR-027) is a reasonable default not stated in the specification.
- Cross-packet effects follow the rule in `specs/README.md`: later packets use this packet's activity log and settings.
