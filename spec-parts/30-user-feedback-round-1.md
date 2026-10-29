## 55. User Feedback Round 1: Data-Entry Safety, Backlog and Admin Review

Approved scope: drafted on 2026-10-24 from the first handwritten feedback list from pilot users ("Things to add to Tuesday app", twelve items, photographed and transcribed in §55.1). Three items are new product behaviour and are specified here as Parts A to C. Seven are already covered by the specification and are mapped to their sections. Two are deployment decisions for the pilot. Approved by Jay on 2026-10-25 with the listed defaults (051-D1). The amendment is additive only: nothing in this section removes, hides, narrows or freezes a current feature, screen, route, report, setting or rule. Where an addition touches existing behaviour, today's behaviour stays the default.

Every addition is deterministic and explainable (constitution II): a backlog membership, a draft restore and an admin check each trace to one rule in this section, and the interface shows that rule. Nothing here assigns, scores or ranks people (§30).

Where this section adds to §12.5 (tasks), §13.3 (Task List and task panel), §13.4 (Board), §13.5 (Timeline), §13.10 (My Work), §13.15 (Administration), §19 (reports), §20.1 (what is logged), §36.7 (My Work views), §46 (live updates) and §49 (record recovery), this section governs.

### 55.1 The feedback list and where each item lands

| # | As written | Disposition |
|---|---|---|
| 1 | Auto save, for data entry, to reduce data loss | **Part A** (§55.2). The task panel already autosaves per field (§13.3); the guarantee is extended to every panel, dialog and long text field. |
| 2 | Forecast: months ahead, capacity, new hires | Covered and bounded: Workload is an 8-week task forecast by design (§13.11); open roles and planned hours are §40.2 and §51.5; months-ahead and hiring forecasts belong to the separate Friday capacity application (§39.12, §27.3). Decision 051-D9 records whether a longer horizon comes into the Hub. |
| 3 | PM template: "security requirement?" (word unclear; may read "seniority") | Project templates are §12.14 (packet 012). The qualifier is a question back to the author (051-D10). Not specified here. |
| 4 | Create access level (Phase 2) | System roles from Entra groups plus in-app assignment (FR-AUTH-03, Q2) and project roles (§8.5). Admin-defined access levels are not specified; recorded as a Phase 2 candidate (051-D11). |
| 5 | Admin review | **Part C** (§55.4). |
| 6 | Connecting to VTP data (Phase 2) | Already Phase 3 scope, item 3 of §29 (ERP master-data sync, nothing assumed). Whether it moves to Phase 2 is 051-D12. |
| 7 | Personalised view: tasks changing | Covered: My Work views (FR-MYW-01, FR-VIS-08), saved personal views (§18.4, packet 019), follow levels (FR-ASG-02) and change notifications (§17). |
| 8 | Backlog | **Part B** (§55.3). |
| 9 | Passwords | The product has no local passwords (FR-AUTH-01); review environments use a file-backed verifier only (§48, FR-OPS-17). The item is a question back to the author: does the pilot host lack Entra sign-in, or is this a request to keep sign-in password-free? (051-D13). |
| 10 | Employee list / OC list / Company (Atlantic) | Covered by §54 Company Structure (packet 050): named levels, unit tree, home unit, Organisation page. |
| 11 | Start it with one discipline | Pilot scoping, not a feature. Recorded as an amendment proposal to Q16 (051-D14). |
| 12 | OC54 use only | Pilot scoping, not a feature. Recorded with item 11 under 051-D14. |

### 55.2 Part A: Data-entry safety

**Problem.** Users lose typed input when a dialog is closed by mistake, the browser tab is refreshed, the network drops during a save, or a sign-in session expires while a long comment is being written. The task panel autosaves per field (§13.3), but create dialogs, register forms, comments, planning entries and the admin screens save only on an explicit button.

**Principle.** Input a person has typed is never discarded by the Hub without that person's action. Every form falls into one of two classes, and each class has one rule.

- **Edit panels** (an existing record's side panel or page: task, deliverable, milestone, decision, risk, issue, meeting, action, allocation, planning entry, handoff, design-basis item, coordination issue, project settings, template editor) save per field.
- **Create dialogs and long text** (every "New …" dialog, comment and note editors, the review comment, the import previews, bulk-action dialogs) keep a local draft until the form is submitted or discarded.

- **FR-SAV-01.** Every edit panel MUST autosave each field when it loses focus or, for text, after `autosave_delay_ms` (default 800) of no typing, with the subtle "Saved" confirmation the task panel already shows. The existing task panel behaviour (§13.3) is the reference implementation and is unchanged. Status transitions, review outcomes and anything that requires a reason keep their explicit buttons and dialogs.

- **FR-SAV-02.** When an autosave fails, the field MUST keep the typed value, show "Not saved" with a Retry action beside the field, and retry on its own at 2, 4 and 8 seconds. The value is never reverted to the server's copy without the person choosing "Discard my change". On an optimistic-concurrency conflict (§25.6 `concurrency_conflict`) the field shows both values, who saved the other one and when, and offers "Keep mine" or "Take theirs".

- **FR-SAV-03.** Every create dialog and long text editor MUST keep a draft in the browser's local storage, keyed by user, form and context (for example `draft.task.new.<projectId>`, `draft.comment.<itemId>`). The draft is written after each change, restored when the same form opens again on the same device with a one-line "Draft restored from <time> · Discard" notice, cleared on successful submit or on Discard, and dropped after `draft_keep_days` (default 7). A draft lives in the browser only; it is never sent to the server, never visible to anyone else and never counted anywhere. An empty form writes no draft.

- **FR-SAV-04.** Leaving a form that holds unsaved input (closing a dialog by its close control, Escape or a click outside; navigating away; closing the tab) MUST ask "Keep editing or discard?" except when FR-SAV-03 has a draft for it, in which case the form closes silently and the draft notice appears on reopening. The browser's own leave-page prompt is used for tab close and reload. A form with no change closes without a prompt.

- **FR-SAV-05.** When a request is refused because the sign-in session has expired (401), the Hub MUST keep the pending input: the field or draft stays as typed, the existing sign-in flow runs, and on return the same form is open with the input in place. For an edit-panel field the autosave retries once after sign-in. Nothing is lost between the refusal and the retry.

- **FR-SAV-06.** The build-update notice of FR-LIVE-03 ("Tuesday was updated. Reload…") MUST stay non-automatic. Reload after that notice goes through FR-SAV-04, so an unsaved form is prompted or drafted before the page reloads.

- **FR-SAV-07.** Each autosave MUST write the same activity entry a button save writes today (§20.1), one per field change, so the log is unchanged in content. Autosave never batches several fields into one entry.

- **FR-SAV-08.** Part A MUST merge dark behind `flag.data_entry_safety` for the visible additions (the draft notice, the leave prompt, the Retry control). The retry-and-never-revert rule of FR-SAV-02 is an unflagged correction (051-D4; its slice is Tier B). With the flag off, every form behaves as it does today.

### 55.3 Part B: Backlog

**Problem.** Work that is known but not yet dated or scheduled has no home. People either invent a due date to make the task appear somewhere, which poisons the overdue and workload figures, or keep the item in a notebook. The Timeline's Unscheduled list (§13.5, §36.4) shows undated items but only inside the Timeline.

**Principle.** The Backlog is a derived list, not a status and not a separate record. A task is in the Backlog by one rule, leaves it by giving it a date, and is never counted in any date-based indicator while it is there. No new task status is added (§30).

- **FR-BKL-01.** A task is **in the Backlog** when it is open (status Not Started or On Hold), has no due date and no planned start date, and is not Cancelled. A task with either date set is not in the Backlog. Membership is computed, shown with the rule ("No due date and no start date") and never stored.

- **FR-BKL-02.** Each project MUST have a **Backlog** list reachable from the project's Task List (§13.3) as a view, from the Board (§13.4) as a first "Backlog" column that can be collapsed, and from the project dashboard as a count ("Backlog: 14") that opens the list (§49.3). The list shows the same columns and filters as the Task List (discipline, deliverable, assignee, priority, label) plus a manual order kept the way §36.3 keeps board card order.

- **FR-BKL-03.** **Quick add** at the top of the Backlog MUST create a task from a title alone, in one interaction: discipline defaults to the creator's home discipline (FR-GRP-01) or the project's first discipline, assignee defaults to the creator so the item has an accountable owner from the first second (constitution III), priority Normal, no dates, no deliverable. Every default is visible and editable on the row. Each quick add is one ordinary task create with the usual permissions (§8.5 "create task") and log entry.

- **FR-BKL-04.** **Scheduling** a Backlog item is setting a due date or a start date, by the row's date cell, by dragging the card to another Board column (which prompts for the date the way the Timeline drag confirmation does, §36.4), or by the existing bulk "Shift due date" action with an absolute date option. Scheduling removes the item from the Backlog and places it where its dates put it. Clearing both dates puts it back.

- **FR-BKL-05.** Backlog items MUST be excluded from Overdue, Due Soon, Slipped, Date Inconsistent, the 8-week Workload forecast, readiness checks (§38), milestone status and health. They stay in the project's open-task count, the Unassigned indicator if the assignee is cleared, Stale (T-07) and every search. The Timeline's Unscheduled list is unchanged and remains the cross-record (deliverables, milestones) view of undated items.

- **FR-BKL-06.** My Work (§13.10, §36.7) MUST add a **No date** view beside Today, Upcoming and Overdue, listing the person's Backlog items across projects with the project and the Backlog rule shown. It is a saved view like the others (§18.4) and opens with current data.

- **FR-BKL-07.** A Discipline Lead's per-discipline status summary (FR-TEAM-05) MUST add a Backlog count beside open, overdue, blocked and next due. The count opens the project Backlog filtered to that discipline.

- **FR-BKL-08.** The Task List export MUST add a `backlog` column (Yes or No) so a Backlog filter can be applied outside the Hub. No new report is added.

- **FR-BKL-09.** Part B MUST merge dark behind `flag.backlog`. With the flag off, the Backlog view, column, count, quick add, No date view and export column are absent, and every existing count and indicator is unchanged.

### 55.4 Part C: Admin review

**Problem.** A System Administrator has no one place to see what has drifted: people who left and are still on teams, users without a manager or unit, roles granted in-app and forgotten, projects whose Project Manager is inactive, and settings changed since anyone last looked. IT security asks for a periodic access review and there is nothing to hand over.

**Principle.** The Admin review is a checklist of **computed checks**, each a count that opens its list and is fixed with the tool that already exists for it. A review is **recorded**, not enforced: it writes who looked, when, what each count was and what they noted. Nothing is changed by recording a review.

- **FR-ADR-01.** An **Admin review** page under Administration (§13.15) MUST list the checks below, each with its current count, the rule it applies and a link that opens the matching list with the filter applied. The checks are:

  | Check | Rule |
  |---|---|
  | Inactive users still on teams | Users Inactive (FR-AUTH-04) who are a team member, Discipline Lead, Project Manager or reviewer on a live project |
  | Users without a manager | Active users with no `supervisor_id` (§8.8) |
  | Users without a home unit | Active users with no `unit_id`; shown only when `flag.org_structure` is on (FR-UNT-07) |
  | In-app role grants | Active users holding a system role assigned in-app rather than from an Entra group (FR-AUTH-03), with the grant date |
  | No sign-in for 90 days | Active users whose last sign-in is older than `admin_review_idle_days` (default 90) |
  | Projects with an inactive Project Manager | Live projects whose PM is Inactive |
  | Live projects with no activity | Active or Setup projects with `last_activity_at` older than `status_stale_days` (today's Stale rule) |
  | Settings changed since the last review | Organisation settings and flags whose last audit entry (§43 FR-DLV-07) is later than the last recorded review |
  | Reference data deactivated while in use | Disciplines, offices, clients, deliverable types or phases deactivated (§13.15) but still set on a live record |

- **FR-ADR-02.** Each count MUST be computed when the page opens, from the same queries the linked lists use, so the count and the list always agree (§49.3). No count is stored except inside a recorded review.

- **FR-ADR-03.** An Admin MUST be able to **record a review**: the page captures the date, the reviewer, every check's count at that moment and an optional note per check and for the whole review, as one `AdminReview` record with one activity entry (§20.1). A recorded review is never edited or deleted; a correction is a new review.

- **FR-ADR-04.** The page MUST show the last recorded review (date, reviewer, counts then and now side by side) and when the next one is due, from `admin_review_interval_days` (default 90). When a review is overdue, every System Administrator's daily digest (§17.3) MUST carry one line, "Admin review due since <date>", until a review is recorded. There is no other reminder and no escalation.

- **FR-ADR-05.** Every fix MUST go through an existing tool: deactivating a user, the Reassign work tool (§13.15), setting a manager, placing a person in a unit (FR-UNT-17), changing a project's PM, editing a setting or reactivating a reference item. The Admin review page adds no edit path of its own and no bulk action.

- **FR-ADR-06.** A recorded review MUST export as CSV and XLSX (§19 conventions) with one row per check (check, rule, count, note) and a header block (date, reviewer, note), so it can be handed to IT security as the access-review evidence. The export carries counts and the reviewer's notes only, never a person list, so it stays within the reviewer's own scope (§8.7).

- **FR-ADR-07.** The page and its export MUST be visible to System Administrators only. Executives see the last review date and the counts on the Portfolio page's admin strip when `flag.admin_review` is on, nothing else.

- **FR-ADR-08.** Part C MUST merge dark behind `flag.admin_review`. With the flag off, the page, the digest line, the record and the Portfolio strip are absent.

### 55.5 Acceptance criteria

**Part A**

- **AC-SAV-01.** Given Alex is typing a new task's description in the New task dialog and presses Escape, when Alex reopens New task in the same project on the same device, then the dialog shows "Draft restored" with the typed title and description, and Discard clears it.

- **AC-SAV-02.** Given a deliverable panel field is edited while the network is down, then the field keeps the typed value, shows "Not saved · Retry", and when the network returns the value is saved within 8 seconds with one activity entry; the server's older value is never shown in the field.

- **AC-SAV-03.** Given Pat saved the same task field 10 seconds earlier, when Alex's autosave of that field is refused as a concurrency conflict, then Alex sees both values with "Pat, 10 s ago", and "Keep mine" saves Alex's value on a fresh version.

- **AC-SAV-04.** Given a half-written comment and an expired sign-in session, when the comment is posted, then sign-in runs, the same item is open afterwards with the comment text still in the editor, and posting succeeds.

- **AC-SAV-05.** Given the build-update notice and an open New milestone dialog with input, when Reload is chosen, then the leave prompt or the draft notice protects the input, and after reload the draft is offered.

- **AC-SAV-06.** Given `flag.data_entry_safety` is off, then no draft is written to local storage, no leave prompt appears, and existing browser suites pass unchanged; the FR-SAV-02 retry still keeps typed values.

**Part B**

- **AC-BKL-01.** Given a project with 20 open tasks of which 6 have no due date and no start date, then the Backlog list and the dashboard count show 6, the Board's Backlog column shows the same 6, and the Overdue and Due Soon counts exclude them.

- **AC-BKL-02.** Given Alex (home discipline Civil) types "Check culvert sizing" into quick add and presses Enter, then one task exists with that title, discipline Civil, assignee Alex, priority Normal, no dates, and one activity entry; the row shows every default.

- **AC-BKL-03.** Given a Backlog task is given a due date of next Friday, then it leaves the Backlog, appears in Upcoming, and clearing the date returns it to the Backlog at the end of the manual order.

- **AC-BKL-04.** Given Alex has Backlog items on three projects, then My Work → No date lists all of them with their projects, and none appears in Today, Upcoming or Overdue.

- **AC-BKL-05.** Given Workload for the next 8 weeks, then a Backlog task with a 40 h estimate adds nothing to any week and is listed under the existing "unscheduled estimates" note.

- **AC-BKL-06.** Given `flag.backlog` is off, then the Backlog view, column, count, quick add, No date view and export column are absent, and existing count tests pass unchanged.

**Part C**

- **AC-ADR-01.** Given two Inactive users still on live project teams, one active user with no manager and one setting changed yesterday, then the Admin review page shows 2, 1 and 1 for those checks, and each count opens the list filtered to exactly those records.

- **AC-ADR-02.** Given an Admin records a review with a note on the inactive-users check, then one `AdminReview` record and one activity entry exist, the page shows that review as the last one, and a second visit shows the counts then and now side by side.

- **AC-ADR-03.** Given the last review is 91 days old with `admin_review_interval_days` at 90, then each Admin's next daily digest carries "Admin review due since <date>" and the line stops after a review is recorded.

- **AC-ADR-04.** Given a Supervisor or Project Manager opens the Admin review route, then the server answers 403 and the navigation does not show the page.

- **AC-ADR-05.** Given a recorded review is exported, then the CSV holds the header block and nine rows with check, rule, count and note, and no person's name other than the reviewer's.

- **AC-ADR-06.** Given `flag.admin_review` is off, then the page, the digest line and the Portfolio strip are absent and existing API tests pass unchanged.

### 55.6 Decisions

Each decision below uses its default until Jay decides otherwise. The rows are also in `specs/DECISIONS.md` as 051-D1 to 051-D14.

| # | Decision | Default used |
|---|---|---|
| D1 | Approve this amendment's scope (Parts A to C) | Approved by Jay 2026-10-25 with the listed defaults |
| D2 | Where do form drafts live? | In the browser's local storage only, per device; no server-side draft table |
| D3 | How long is a draft kept? | `draft_keep_days` = 7; an organisation setting |
| D4 | Is the never-revert retry rule (FR-SAV-02) flagged? | No; it is an unflagged correction of a data-loss path, its slice is Tier B |
| D5 | What puts a task in the Backlog? | Derived: open, no due date and no start date; no stored flag and no new status |
| D6 | Quick-add defaults? | Discipline from the creator's home discipline or the project's first discipline; assignee the creator; priority Normal |
| D7 | Does the Backlog include deliverables? | No; tasks only. The Timeline's Unscheduled list keeps the cross-record view |
| D8 | Which checks are in the Admin review? | The nine in FR-ADR-01; a new check needs a spec change |
| D9 | Does a months-ahead forecast come into the Hub (item 2)? | No; it stays in the Friday capacity application; the Hub keeps the 8-week Workload |
| D10 | What does "PM template · security/seniority requirement?" (item 3) mean? | Question back to the author; nothing specified |
| D11 | Admin-defined access levels (item 4)? | Phase 2 candidate; not specified; roles stay as §8.8 |
| D12 | Move the ERP (VTP) sync (item 6) from Phase 3 to Phase 2? | No change until the ERP owner confirms capabilities (§29 item 3) |
| D13 | "Passwords" (item 9)? | Question back to the author; FR-AUTH-01 stands (no local passwords in pilot or production) |
| D14 | Pilot scope (items 11 and 12): one operating centre (OC54) and one discipline first? | Proposed amendment to Q16; Jay decides; no product change needed |
