## 49. Record Recovery and Counts Amendment

Approved scope: Jay approved the 14 October build-approach report and directed building all of it on 2026-10-14. Features are fixed and additive only: nothing here removes, hides, narrows or freezes a current feature, and where an addition touches existing behaviour, today's behaviour stays the default. This section records roadmap items 12, 13, 14 and 31 of that report, delivered by packet 045. It records the product contract; implementation and acceptance are pending.

It keeps four promises the product already makes or implies. The delete dialog says a deleted task "can be restored by the PM", but after the 10-second undo toast there is no way to do it. Some counts open nothing or open a list that disagrees with them. Template offsets count calendar days only, so a due date can land on a Saturday. The status of a discipline's deliverables, and the overdue deliverables across projects, can only be read item by item.

Where this section adds to §12.14 (project templates), §13.1 (Project Dashboard), §13.3, §13.6 and §13.7 (task, deliverable and milestone lists), §13.12 (Portfolio), §13.19 (My Staff), §13.20 (Time), §19 (reporting), §40.7 (count reconciliation) and E-04 (restore of a deleted predecessor), this section governs. G-06 (soft delete only) and G-07 (row versions) are unchanged, and §40.7's rules FR-CNT-01 to FR-CNT-07 stay in force; this section extends them to more screens.

### 49.1 Scope and boundary

Part A is Show deleted and Restore for tasks, deliverables and milestones (§49.2). Part B makes every count open its exact list, or say what it sums (§49.3). Part C adds optional working-day offsets per template (§49.4). Part D adds two fixed roll-ups: a deliverable status bar in each discipline row (§49.5) and Portfolio grouping with an Overdue deliverables column (§49.6).

Outside this amendment:

- permanent erase, purge or an archive state for any record (G-06, constitution V);
- Show deleted for decisions, risks, issues, meetings, actions, comments, links, time entries or planning entries;
- a Delete action for deliverables and milestones in the interface (today only the API deletes them, `src/Hub.Api/Features/Deliverables.cs:27`, `src/Hub.Api/Features/Milestones.cs:27`);
- any change to the value or definition of an existing count;
- Home's fixed tiles (FR-VIS-07);
- a dashboard widget or chart builder (Should-NOT list);
- cross-office comparisons, rates, ranks or trends (§29 item 7);
- working days as the default for templates or any change to the dates of existing projects;
- changes to `working_days_enabled`, dependency lag or thresholds.

### 49.2 Show deleted and Restore

- **FR-RCV-01.** The Tasks list (§13.3), the Deliverables register (§13.6) and the Milestones view (§13.7) MUST offer a "Show deleted" toggle in their Filters popover (§41 R1) to a viewer who may restore items in that project. That is the existing `RestoreItem` rule: the project's PM or an Admin, on a project that is not Archived or Cancelled, with an account that is not Read Only. The server returns it as a `restore` permission flag on the project, so the client does not re-derive it. The toggle is off by default and lives only in the URL (`deleted=true`), never in a saved view. With it off, every list, count, search result, report and export is exactly as today and leaves deleted items out (G-06).

- **FR-RCV-02.** With the toggle on, the page MUST show that project's deleted items of its type instead of its live rows: newest deletion first, with key, name, discipline, parent (the deliverable or milestone key, when there is one), status at deletion, deleted at, deleted by and a Restore action. The list has text search on key and name, pages like other lists (200 rows a page) and exports to CSV and XLSX with the same rows; the export is logged like any other (§19). Reading it needs the restore permission. Anyone else gets 403 and learns nothing about deleted items. Restricted project rules apply unchanged (§8.7).

- **FR-RCV-03.** Deliverables and milestones MUST gain a restore action (`POST /deliverables/{id}/restore`, `POST /milestones/{id}/restore`) that behaves like the existing task restore (`POST /tasks/{id}/restore`): the restore permission; `deleted_at` and `deleted_by` cleared; a "Restored" activity entry written by the save hook in the same transaction (§20.1); the project queued for re-evaluation; the item's id and row version returned. The item keeps its key, and deleted keys are never reused. Restoring a deliverable does not restore its deleted tasks. Restoring a task still brings back its dependencies (E-04).

- **FR-RCV-04.** A task whose deliverable or milestone is deleted, and a deliverable whose target milestone is deleted, MUST NOT be restored. The refusal names the parent's key ("Restore 1234-D012 first"), and the deleted list shows the same reason with Restore unavailable. The 10-second undo never meets this case, because a deliverable or milestone with live items cannot be deleted (DL-09; `milestone_in_use`). On the existing task restore (`POST /tasks/{id}/restore`) this refusal applies only while `flag.show_deleted` is on; with it off, task restore behaves exactly as today.

- **FR-RCV-05.** A restore MUST check the item's row version when `If-Match` is sent, answering 409 and storing nothing when it is stale (G-07). The deleted list always sends it. The task undo toast, which sends none, keeps working as today. Restoring an item that is no longer deleted answers 404, and the deleted list refreshes. As in FR-RCV-04, the `If-Match` check on the existing task restore applies only while `flag.show_deleted` is on.

- **FR-RCV-06.** Today's behaviour stays: the 10-second Restore toast after a task delete, the delete dialog's text, no reason needed to delete or restore on any project status, and no notification on restore. Nothing here erases a record.

### 49.3 Every count opens its list

- **FR-RCV-07.** Every number shown as a count on Home, the Project Dashboard (tiles and discipline table), the Milestones view, Portfolio (tiles and per-project columns), My Staff (tiles) and Time (totals) MUST do one of two things. Either it opens a list or report whose row count equals the number, under the same permissions and filters; or, when the number is a sum (hours, assignments, stalled reviews, overdue decisions across projects), it carries a one-line definition naming what it sums (FR-CNT-06) and opens the rows it sums. This extends FR-CNT-01 beyond the Home, Project Dashboard and Portfolio tiles.

- **FR-RCV-08.** No existing count changes its value or its definition. Where a count and its list use different predicates today, the link carries the parameters that reproduce the count (for example a status list that leaves out Cancelled). Counts this section adds to the Project Dashboard response carry their link as `{Value, Link}`, built from the same list query, as `src/Hub.Api/Features/Dashboard.cs` already does for tasks and deliverables, so the client never builds a link for them. Existing response fields keep their names and shapes; new values are added beside them. Links the client builds (Portfolio rows, the Milestones view, My Staff and Time) come from one helper per screen and are checked by tests.

- **FR-RCV-09.** The gaps below MUST be closed as stated (paths and lines at `48586029`).

| Surface | Count | Today | Opens |
|---|---|---|---|
| Project Dashboard tiles | Open issues; High issues; High risks | Evaluation snapshot, link built in the client (`Dashboard.cs:96-97`; `projects/Dashboard.tsx:214-216`) | Issues `indicator=open`; issues `indicator=open&severity=High`; risks `indicator=open&severity=High`, counted with the register list query |
| Discipline table | Deliverables due ≤ 14 d | Plain number (`projects/Dashboard.tsx:96`) | That discipline's deliverables with `dueFrom` today, `dueTo` today + 14 and `indicator=open` (AC-DASH-02) |
| Milestones view | Deliverables issued of total; tasks complete of total; Overdue n; Blocked n | Plain text (`projects/Milestones.tsx:93-101`, `137-143`) | Deliverables targeting that milestone; tasks under it, with `status=Complete`, `overdue=true` or `blocked=true` |
| Portfolio rows | Overdue tasks; Blocked tasks; Decisions overdue; High issues; Attention Critical and Warning | Plain numbers (`pages/Portfolio.tsx:159-166`) | That project's tasks `overdue=true` or `blocked=true`; decisions `indicator=overdue`; open High issues; the project dashboard's attention list at that severity |
| Portfolio tile | Overdue decisions | Opens nothing (`pages/Portfolio.tsx:94`) | The Portfolio filtered to projects with an overdue decision (new filter `overdueDecisions=true`, kept with the other filters). The tile stays the sum over the rows shown, and each row's figure opens that project's overdue decisions |
| My Staff tiles | Staff; Assignments; With overdue; With blocked; Reviews stalled | No definitions (`pages/Staff.tsx:56-58`, `92-96`). Reviews stalled sums stalled reviews but opens people with any review (`src/Hub.Api/Features/MyWork.cs:194`, `204`) | A definition on all five. Reviews stalled opens the people with at least one stalled review (new indicator `reviewsStalled`); `indicator=reviews` keeps working |
| Time | Week total; totals by project and by task | No definition and no links (`pages/Time.tsx:156`, `175-187`) | A definition on the total; each project or task total opens the entries filtered to it (`projectId`, `taskId`), whose hours add up to it |

### 49.4 Working-day offsets in templates

- **FR-RCV-10.** Each template version MUST carry "Offsets count" (`offset_day_kind`): Calendar days, the default for every existing and new template, or Working days. It is edited in the template editor's header while the version is a Draft, is read-only on Published and Retired versions, is copied to a new draft, and every change appears in the template's history. The reference template stays Calendar days.

- **FR-RCV-11.** With Calendar days, dates MUST be computed exactly as today (§12.14 steps 2 and 3; `src/Hub.Domain/Templates.cs:40,49,54-55`), so every existing template gives the same dates for the same inputs.

- **FR-RCV-12.** With Working days, each non-zero offset in §12.14 steps 2 and 3 MUST count working days of the project office's calendar: Monday to Friday, without the organisation-wide holidays and that office's holidays, the same calendar dependency lag uses (`src/Hub.Api/Infrastructure/EvaluationService.cs:64`). A positive offset steps forward and a negative one steps back, each landing on a working day. An offset of 0 keeps its base date. A typed (contract) milestone date is never moved. The template's choice applies whatever `working_days_enabled` says; that setting keeps governing thresholds and lag only. The rule is a pure function in `Hub.Domain` under its branch-coverage gate (constitution II).

- **FR-RCV-13.** The creation wizard's preview, project creation from a template and "Add from template" (FR-TPL-02) MUST give identical dates for identical inputs. The preview uses the office chosen in the wizard; the other two use the project's office. When a template counts working days, the wizard says so in one line: "Offsets skip weekends and office holidays." Projects already created never change (snapshot semantics, §12.14).

### 49.5 Discipline deliverable status bar

- **FR-RCV-14.** Each row of the Project Dashboard's discipline table (§13.1 item 6) MUST show a deliverable status bar with four segments: Not started (Not Started), In progress (In Progress, In Review, Revision Required, Ready to Issue), Issued (Issued, Accepted) and On hold (On Hold). Cancelled deliverables are left out. Each segment opens the Deliverables register filtered to that discipline and those statuses, and its count equals that list's rows; the server counts each segment with the deliverable list query and returns it as `{Value, Link}`. Beside the bar, text says "n of m issued". Each segment has a tooltip and an accessible label naming its group and count, so colour is never the only signal (§13.0, §41 R8). A discipline with no deliverables shows "No deliverables" and no bar. On phones the text stays and the bar is hidden. The bar is fixed: it has no settings and adds no other chart. It takes up the status-mix bar that §40.10 deferred, for deliverables only.

### 49.6 Portfolio grouping and overdue deliverables

- **FR-RCV-15.** Portfolio (§13.12) MUST offer a Group menu (§41 R1) with None (the default, today's table), Client, Office and Project type, kept in the URL as `group` and in saved Portfolio views. Each group has a header row with its number of projects, its Red, Yellow and Green computed-health counts and the sums of the count columns over its rows; each figure equals the sum of the rows below it. The project count opens the Portfolio filtered to that client, office or project type, giving the same rows; a "Not set" project type group carries a definition instead. Groups are ordered by name with "Not set" last, and rows inside a group keep the current sort. Subtotals are counts and sums only: no rates, ranks, ratios or trends across groups, so §29 item 7 stays Phase 3. Grouping changes no figure, filter, permission or export, and every figure counts only projects the viewer can see.

- **FR-RCV-16.** Portfolio MUST show an "Overdue deliverables" column: the number of the project's deliverables with the Overdue indicator (G-01, terminal deliverables excluded), counted with the deliverable list query for `indicator=overdue` and opening that list. It is not the At Risk count, which stays on the Project Dashboard. The column joins the group subtotals and the Portfolio export, which also gains a Project type column. Grouping does not change the export's rows or order.

### 49.7 Acceptance criteria

- **AC-RCV-01.** Given a PM deletes task 1234-T0042, which has two successors, and the undo toast has gone, when the PM turns on Show deleted in the Tasks Filters, then 1234-T0042 is listed with when and by whom it was deleted; Restore returns it to the task list with both dependencies (E-04) and writes a "Restored" entry to its history.

- **AC-RCV-02.** Given a deliverable and, separately, a milestone that were deleted, when the PM restores each from Show deleted on its page, then each reappears with its key, the activity log shows "Restored" in the same transaction, and the project is re-evaluated.

- **AC-RCV-03.** Given task 1234-T0057 whose deliverable 1234-D012 is also deleted, then 1234-T0057's Restore is unavailable with "Restore 1234-D012 first" and the API refuses it naming 1234-D012; after 1234-D012 is restored, 1234-T0057 restores. A deliverable whose milestone is deleted behaves the same way.

- **AC-RCV-04.** Given a Discipline Lead, a Team Member, a Read Only account, or a PM on an Archived or Cancelled project, then no Show deleted toggle appears, the deleted list answers 403 and every restore is refused; a viewer who cannot see a Restricted project gets the same answer as today.

- **AC-RCV-05.** Given a restore sent with a stale `If-Match`, then it answers 409 and nothing changes; given an item already restored, a second restore answers 404 and the list refreshes; the task undo toast without `If-Match` still restores.

- **AC-RCV-06.** Given Show deleted is off, then lists, counts, search, reports and exports contain no deleted item, the delete dialog and the 10-second Restore toast are unchanged, and no endpoint erases a record.

- **AC-RCV-07.** Given Show deleted is on, then CSV and XLSX exports contain the same rows as the screen, and the export is logged without its content.

- **AC-RCV-08.** For the Project Dashboard's Open issues, High issues and High risks, and for every discipline row's "Due ≤ 14 d", the number equals the rows of the list its link opens, for a PM, a Discipline Lead and a Supervisor, with and without a discipline scope.

- **AC-RCV-09.** On the Milestones view, each milestone's deliverables, tasks, Overdue and Blocked figures open lists whose row counts equal them.

- **AC-RCV-10.** On Portfolio, each row's Overdue tasks, Blocked tasks, Decisions overdue, High issues and Attention figures open lists whose row counts equal them. The Overdue decisions tile filters the table to projects with an overdue decision; its value is unchanged and equals the sum of the Decisions overdue column over the rows shown.

- **AC-RCV-11.** On My Staff, each tile has a one-line definition reachable by keyboard and announced to screen readers. Given 3 stalled reviews held by 2 people, the Reviews stalled tile shows 3 and opens exactly those 2 people; an old `indicator=reviews` link still shows everyone with a review waiting.

- **AC-RCV-12.** On Time, the total has a definition, and each project or task total opens the entries for that project or task, whose hours add up to the total shown.

- **AC-RCV-13.** Given the packet 036 count fixtures, every count's value is the same before and after this packet, and the counts suite finds no count whose list disagrees with it.

- **AC-RCV-14.** Given any existing template, then it counts Calendar days, and creating a project from it gives the same dates as before this packet for the same start date, typed dates and disciplines.

- **AC-RCV-15.** Given a template that counts Working days, a project start on a Friday and a milestone offset of 1, then the milestone falls on the next Monday; if that Monday is a holiday for the project's office, on the Tuesday.

- **AC-RCV-16.** Given the same template, a milestone on a Tuesday and a deliverable offset of −2, then the deliverable is due the Friday before; an offset of 0 or a typed date on a Saturday stays on that Saturday.

- **AC-RCV-17.** Given a template that counts Working days, then the wizard preview, the created project and "Add from template" give identical dates; the wizard shows its one-line note; changing the setting appears in the template's history; a new draft copies it; a Published version cannot change it.

- **AC-RCV-18.** Given a discipline with 2 Not Started, 3 In Progress or In Review, 4 Issued or Accepted, 1 On Hold and 1 Cancelled deliverable, then its bar shows segments of 2, 3, 4 and 1 with "4 of 10 issued", and each segment opens the Deliverables register with exactly that many rows. Each segment's tooltip and accessible label name the group and count, and an axe check passes.

- **AC-RCV-19.** Given Portfolio grouped by Client, then each client's header shows its project count, health counts and column sums, each equal to the sum of its rows; its project count opens the Portfolio filtered to that client with the same rows; the URL and a saved view keep `group=client`; with no group the table is today's.

- **AC-RCV-20.** Given a project with 3 overdue deliverables and 2 At Risk deliverables that are not overdue, then its Overdue deliverables figure is 3 and opens `indicator=overdue` with 3 rows; the Portfolio export contains the Overdue deliverables and Project type columns.

- **AC-RCV-21.** Given a viewer who cannot see a Restricted project, then no roll-up, group subtotal, segment or overdue figure counts anything from it.

### 49.8 Decisions and deferred proposals

Each decision uses its default until Jay decides otherwise.

| # | Decision | Default |
|---|---|---|
| D1 | Who sees deleted items and restores them | The existing `RestoreItem` rule: the project's PM or an Admin. Discipline Leads do not. |
| D2 | Which records get Show deleted | Tasks, deliverables and milestones only. |
| D3 | Is the Show deleted toggle saved in views | No. URL only, so no saved view ever opens on deleted items. |
| D4 | A child whose parent is deleted | Refused, naming the parent. No cascade restore. |
| D5 | Reason and concurrency on restore | No reason on any project status, as today's task restore. `If-Match` checked when sent. |
| D6 | A count that is a sum | A definition plus a link to the rows it sums (§49.3 table). |
| D7 | My Staff Reviews stalled tile | Opens people with a stalled review through a new filter; old `indicator=reviews` links keep working. |
| D8 | Existing count values | Never changed; links carry the parameters that reproduce them. |
| D9 | Working-day calendar for templates | Monday to Friday without organisation-wide and project-office holidays, whatever `working_days_enabled` says. |
| D10 | Status bar groups | Not started; In progress (In Progress, In Review, Revision Required, Ready to Issue); Issued (Issued, Accepted); On hold. Cancelled left out. |
| D11 | Portfolio group keys | Client, Office and Project type, no grouping by default, counts and sums only. |
| D12 | Portfolio deliverable figure | Overdue (G-01), not At Risk. |
| D13 | Flags | Part A behind `flag.show_deleted`, Part C behind `flag.template_working_days`, Part D behind `flag.status_rollups`, each enforced on the server as FR-DLV-08 (§43) requires: while off, the new endpoints answer 404 and the new fields are absent. Part B ships without a flag because it corrects existing counts, and existing features are never flagged (FR-DLV-09); its slices are Tier B (§43.11). With `flag.show_deleted` off, task restore behaves exactly as today. |

Deferred follow-up proposals, outside packet 045: Show deleted for the other registers (trigger: the record engine of §47 is adopted for that register, or a PM asks); a Delete action for deliverables and milestones in the interface; restoring a deliverable together with its deleted tasks; an At Risk deliverables column on Portfolio; grouping on the project list beyond status; a separate export per Portfolio group.
