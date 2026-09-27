# Feature Specification: Collaboration and Notifications

**Feature Branch**: `006-collaboration-and-notifications`

**Created**: 2026-09-24

**Status**: Draft

**Input**: User description: "Convert the Engineering Project Coordination Hub specification into Spec Kit: packet 006, collaboration and notifications (comments with @mentions, document links, the notification centre, immediate emails, the daily digest, preferences, and following the projects a person is assigned to)."

**Source**: Product specification §11.8, §11.11 (FR-NOT), §11.13 (FR-ASG-01–04), §12.7, §12.8, §12.18 (ASG-01–07), §13.17, §15.10, §15.13, §17, §17.7, §31.11, §31.14, §31.16

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Discuss the work where it is tracked (Priority: P1)

Team members comment on tasks, deliverables, milestones, and decisions, mention colleagues with
@, and leave hand-off notes. Comments are the conversation; the item's History holds the facts.

**Why this priority**: Context has to live with the work, or the Hub becomes a list of names and
dates that people ignore.

**Independent Test**: Post, edit, and delete comments as different roles, mention a colleague,
and change a status with a note; confirm the rules and notifications.

**Acceptance Scenarios**:

1. **Given** a user posts a comment mentioning @Diane, **Then** Diane receives an immediate notification linking to the item and becomes a watcher of it. *(AC-COM-01, C-05, C-06)*
2. **Given** a comment the current user posted 10 minutes ago, **Then** they can edit it and it shows "edited"; at 16 minutes editing is no longer available. *(AC-COM-02, C-02)*
3. **Given** another user's comment, **When** the PM deletes it, **Then** a "removed by PM" placeholder remains and the deletion is logged. *(AC-COM-03, C-04)*
4. **Given** a task's status changes with a status note, **Then** the note appears as a Status Note comment and the status change appears in History. *(AC-COM-04, C-08)*
5. **Given** a Viewer on a project where Viewer comments are turned off, **Then** the comment box is not shown. *(AC-COM-05, §8.3)*

---

### User Story 2 - Point to where documents already live (Priority: P1)

Projects, deliverables, and tasks hold links to the SharePoint, OneDrive, Teams, network-folder,
or document-management location of the real files. The Hub stores no files.

**Why this priority**: The coordination value is knowing where the current package is and who is
working on it; duplicating document control would create a second source of truth.

**Independent Test**: Add a SharePoint link, a network path, and an external link to a
deliverable and confirm detection, the copy action, and inheritance on its tasks.

**Acceptance Scenarios**:

1. **Given** a user adds a SharePoint address, **Then** the link type is detected as SharePoint and shown with its icon. *(AC-DOC-01)*
2. **Given** a network path such as `\\server\share\project`, **Then** it shows a Copy path button and is not a clickable link. *(AC-DOC-02)*
3. **Given** a deliverable with a document link, **Then** its tasks show the link as inherited and read-only. *(AC-DOC-03, DOC-04)*

---

### User Story 3 - Hear about my work without drowning in it (Priority: P1)

People are told immediately about events that need a response or unblock them (assignments,
review requests, review outcomes, mentions, "you can start"), and everything else waits for the
digest. They are never told about their own actions, repeated changes collapse, and bulk changes
arrive as one summary. Each person chooses in-app, email, or off per event.

**Why this priority**: Without notifications nobody returns to the tool; with too many, people
filter it out.

**Independent Test**: Trigger each event type in §17.2 and confirm the channels, suppression, and
preference behaviour.

**Acceptance Scenarios**:

1. **Given** a user is assigned a task by someone else, **Then** they receive one in-app notification and one email within 2 minutes. *(AC-NOT-01)*
2. **Given** a user changes their own task's status, **Then** they receive no notification. *(AC-NOT-02)*
3. **Given** a task is set Ready for Review, **Then** the reviewer receives an immediate in-app and email notification. *(AC-REV-01)*
4. **Given** a user turns off email for "Comment on an item you own or watch", **Then** comments still create in-app notifications and no emails. *(AC-NOT-05)*
5. **Given** a PM bulk-reassigns 12 tasks to one user, **Then** that user receives one notification summarising 12 tasks. *(AC-NOT-06)*
6. **Given** a project in Setup with 40 tasks assigned during set-up, **When** it is activated, **Then** each assignee receives one batched assignment notification. *(AC-NOT-07)*

---

### User Story 4 - One morning digest instead of a stream of emails (Priority: P1)

Each person receives one email at 07:00 when there is something to say: their overdue items,
what is due soon, what is blocked and by what, reviews waiting on them, decisions they own or
requested, milestones approaching, updates on projects they follow, and, for PMs and leads, the
attention items on their projects.

**Why this priority**: The digest replaces the Monday status email and keeps date-driven reminders
out of the inbox during the day.

**Independent Test**: Seed a user with overdue, blocked, and review items and followed-project
activity; run the digest; confirm sections, caps, subject, and suppression.

**Acceptance Scenarios**:

1. **Given** a user with no new project updates who has 2 overdue tasks, 1 review waiting, and 1 blocked task at 07:00, **Then** they receive one digest with those sections and the subject "Hub digest — 2 overdue, 1 review, 1 blocked". *(AC-NOT-03)*
2. **Given** a user with nothing due, overdue, blocked, or awaiting review, and no project updates or staff changes to report, **Then** no digest is sent. *(AC-NOT-04)*
3. **Given** a weekend day, **Then** no digest is sent unless the organisation turns weekend digests on. *(§17.3)*

---

### User Story 5 - Being assigned to a project means following it (Priority: P2)

Everyone added to a project team follows it: at All activity for PMs, leads, team members, and
viewers, and at My items only for people added just to review. Following at All activity puts
every change others make on the project in a Following tab and summarises it in the digest,
never as one email per change. People change their level per project, and the Hub never
overrides a choice they made.

**Why this priority**: The business asked for it: assignment should mean receiving the project's
updates.

**Independent Test**: Add a user to a project, make changes as others, change the user's level,
remove them from the team; confirm the feed, digest, and follow rules at each step.

**Acceptance Scenarios**:

1. **Given** a PM adds Alex to project 1234 as a Team Member, **Then** Alex follows 1234 at All activity with source Assignment and the "Added to a project" notification says so; **Given** Diane is added only as Reviewer, **Then** she follows at My items only. *(AC-ASG-01, ASG-01)*
2. **Given** Alex follows 1234 at All activity, **When** Marc changes a task due date on 1234, **Then** the change appears in Alex's Following tab within 60 seconds and counts as unread; **When** Alex changes a task himself, **Then** it is not counted as unread. *(AC-ASG-02, ASG-05)*
3. **Given** Alex set 1234 to My items only, **When** the PM later changes Alex's role, **Then** his level is still My items only. *(AC-ASG-03, ASG-02)*
4. **Given** Alex's follow has source Assignment, **When** he is removed from the team, **Then** the follow is deleted; **Given** he had set the level himself and the project is Open, **Then** it is kept. *(AC-ASG-04, ASG-04)*
5. **Given** others made 9 changes on 1234 yesterday, one of which assigned Alex a task, **Then** the digest's Project updates section counts 8 changes for 1234 and the assignment appears only as its own notification. *(AC-ASG-05, ASG-06)*
6. **Given** a PM bulk-shifts 40 due dates on 1234, **Then** Alex's Following tab shows one collapsed entry for the 40 changes. *(AC-ASG-06, ASG-05)*

---

### Edge Cases

- Mentioning someone who is not on the project notifies them and lets them read the item without adding them to the team; the PM sees "mentioned non-member" in activity. *(C-05)*
- Several changes to one item by one person within 5 minutes produce one notification ("Marc updated 1234-T0042, 3 changes"). *(§17.5)*
- The same immediate email for the same event on the same item is not repeated within 24 hours. *(§17.5)*
- A follower who loses access to a Restricted project loses the follow, and that project's entries disappear from their Following tab. *(E-28)*
- Set-up work in a Setup project never floods followers: on activation each follower's read marker moves to the activation time. *(ASG-07)*
- Inactive users receive nothing. *(E-01)*
- Someone on many projects keeps updates manageable with per-project levels, a digest grouped by project and capped at five rows each, and the "important only" filter. *(E-27)*

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Project members, and Viewers where the project allows it, MUST be able to comment on tasks, deliverables, milestones, and decisions, with bold, italic, lists, links, and inline code but no images. *(FR-COM-01, C-01, §12.8)*
- **FR-002**: Authors MUST be able to edit a comment within 15 minutes of posting (marked "edited") and delete it at any time, leaving a "deleted by author" placeholder while the text is kept for Admin audit; the PM MUST be able to delete any comment in the project, leaving "removed by PM". *(FR-COM-02, C-02, C-03, C-04)*
- **FR-003**: @mentions MUST resolve to project members first and then other active users, notify the mentioned person, and make them a watcher; non-members MUST NOT be added to the team. *(FR-COM-03, C-05, C-06)*
- **FR-004**: Review outcomes MUST appear as Review comments with the round number, a status note typed with a status change MUST appear as a Status Note comment, and other system events MUST appear only in History. *(C-07, C-08)*
- **FR-005**: Comments and History MUST be separate tabs, newest comment last, with Ctrl+Enter to post; threads, reactions, attachments, and read receipts are not provided. *(§12.8)*
- **FR-006**: Users MUST be able to add document links (title, address or network path, type) to projects, deliverables, and tasks; the type MUST be detected from the address (SharePoint, OneDrive, Teams, Network Folder, External DMS, Other) and be changeable. *(FR-DOC-01, FR-DOC-02, §12.7)*
- **FR-007**: Only web addresses and network paths MUST be accepted; the title MUST default to the last path segment; network paths MUST show Copy path instead of a link. *(DOC-01, DOC-02, AC-DOC-02)*
- **FR-008**: Document links MUST be soft-deleted and logged, and a deliverable's links MUST appear read-only on its tasks. *(DOC-03, DOC-04)*
- **FR-009**: System MUST NOT store files. *(FR-DOC-03, §12.7, §30)*
- **FR-010**: The notification centre MUST show personal notifications grouped by day with an unread count, mark read and mark all read, filters by unread, type, and project, and a link to each item. *(FR-NOT-01, §13.17)*
- **FR-011**: System MUST send the events in §17.2 on their default channels: immediate in-app and email for assignments, reviewer set and review requested, review outcomes, "you can start", mentions, decision assignment, decision recorded on a linked task, added to a project, and becoming Discipline Lead; in-app and digest for the others listed there. *(FR-NOT-02, §17.2)*
- **FR-012**: System MUST never notify people about their own actions, collapse changes to one item by one actor within 5 minutes, list each item once per digest in its most severe section, not repeat an immediate email for the same event and item within 24 hours, summarise bulk changes in one notification per recipient, and hold set-up assignments until activation. *(§17.5)*
- **FR-013**: Each user MUST be able to set in-app, email, or off per event, turn the digest on or off and choose its time, and set a follow level per project; Admin defaults MUST apply to new users without overwriting existing choices. *(FR-NOT-03, §17.4)*
- **FR-014**: Emails MUST be plain, carry the item key in the subject and deep links in the body, come from one service mailbox, and not accept replies. *(§17.6, Q3)*
- **FR-015**: Unread counts MUST refresh at least every 60 seconds. *(§17.6)*
- **FR-016**: The daily digest MUST go out at the configured time (default 07:00 organisation time) only when it has content, with sections for overdue items, items due soon, blocked items with blockers, reviews waiting, decisions owned or requested, approaching milestones, and project updates, plus attention items for PMs and leads and the staff section for supervisors; each section MUST be capped at 10 rows with "and n more", the subject MUST carry the counts, Setup and On Hold projects MUST be left out, and weekend digests MUST be off by default. *(FR-NOT-02, §17.3)*
- **FR-017**: Nothing MUST be sent to external parties. *(§17.1)*
- **FR-018**: Adding a user to a project team MUST make them follow it at All activity (PM, lead, Team Member, Viewer) or My items only (Reviewer only), recording the source as Assignment; a user-set level MUST never be changed by the system. *(FR-ASG-01, ASG-01, Q20)*
- **FR-019**: Users MUST be able to set their follow level (All activity, My items only, Muted) or unfollow for any project they can view, from the project header, My Work, and preferences; nobody can set another person's level. *(FR-ASG-02, ASG-02, ASG-03)*
- **FR-020**: Leaving a team MUST delete an Assignment follow and keep a Manual follow while the user can still view the project; losing view access MUST delete any follow. *(ASG-04)*
- **FR-021**: The Following tab MUST show every change by others on projects followed at All activity, from the activity log, filtered by current permissions, grouped by project and day, collapsed where changes share a triggering action, with unread counts per project, mark-as-read, and an "important only" filter. *(FR-ASG-03, ASG-05, §13.17)*
- **FR-022**: An event that produced a personal notification MUST NOT be counted again in the Following tab or the Project updates section. *(ASG-06)*
- **FR-023**: Setup projects MUST record feed entries without unread counts or digest content; On Hold projects MUST keep their feed but leave the digest; Archived and Cancelled projects MUST produce nothing. *(ASG-07)*
- **FR-024**: The digest's Project updates section MUST give, per followed project, counts of changes by type and the five most important changes. *(FR-ASG-04, §17.3)*
- **FR-025**: Muted MUST keep only direct assignments (task, review, decision ownership) and @mentions for that project. *(§12.18, §17.4)*

### Key Entities *(include if feature involves data)*

- **Comment**: Text on one item with author, kind (General, Review, Status Note, System), review round, times, and deletion state.
- **Mention**: A person named in a comment.
- **Watcher**: A person following one item, with how they became a watcher.
- **Document link**: A titled address or network path on a project, deliverable, or task, with its type.
- **Notification**: One message for one person about one item, with read, emailed, and digest state.
- **Notification preference**: A person's channel choice for one event type, plus digest settings.
- **Project follow**: A person's follow level for one project, its source (Assignment or Manual), and their read marker.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Assignment and review-request notifications arrive in-app and by email within 2 minutes in 95 % of cases. *(AC-NOT-01)*
- **SC-002**: In testing, nobody receives a notification about their own action, and nobody receives more than one digest per day.
- **SC-003**: Changes on a followed project appear in the Following tab within 60 seconds.
- **SC-004**: Pilot users say the digest replaces the Monday status email for their projects. *(G1)*
- **SC-005**: Link-type detection is correct for all six link types in testing.

## Assumptions

- The service mailbox route (Q3) uses the specification's default: send through the corporate mail service from one shared mailbox if approved, otherwise another outbound mail service.
- Microsoft Teams notifications are Phase 3 (FR-NOT-04) and not part of this set of packets.
- The staff notices and the digest's My staff section for supervisors are specified in packet 007 (FR-ASG-07). The weekly PM summary, digest content preferences, and live unread counts are Phase 2 (packet 020).
- Events raised by later packets (decisions, milestones) use the channels defined here, following the cross-packet rule in `specs/README.md`.

## Coordination expansion amendment — 2026-09-26

Packets 025–033 reuse source links, activity history and notification/digest preferences (§37.1). Registered revision metadata is distinct from external file storage. Recipient visibility is checked at composition and delivery under FR-MDC-02.

This is approved specification scope with implementation pending in the named new packets. Historical verification for this packet does not verify the added behaviour.
