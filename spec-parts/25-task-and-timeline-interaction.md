## 50. Task and Timeline Interaction Amendment

Approved scope: Jay approved the build-approach report and directed building all of it on 2026-10-14. Features are fixed and additive only: nothing here removes, hides, narrows or freezes a current feature, and where an addition touches existing behaviour, today's behaviour stays the default. This section takes roadmap items 18, 20, 22, 26, 30 and 32 of that report and is delivered by packet 046. It adds row quick actions and inline name edit on the task list, a person picker that knows the project, an earliest consistent date hint with successor warnings, honest schedule display (a slip display threshold, a PM re-baseline beside the original date, and the latest planned date against the target), a bundle of small additions, and handoffs and a chain-to-milestone highlight on the project Timeline. Every rule is deterministic and every hint is advisory (constitution I and II).

Where this section adds to §13.3 and §13.3.1 (Task List and Task Detail Panel), §13.6.1 (Deliverable Detail Panel), §13.7 (Milestone View), §40.6 (capacity in the person picker, FR-PICK-02), §12.6 (date consistency, D-12 and D-13), §12.3 and §12.4 (milestone and deliverable dates), §12.16, §13.5 and §36.4 (Timeline and Gantt, FR-VIS-05), §37.2 (handoffs, FR-HND-01 and FR-HND-06), §37.6 (over-capacity confirmation, FR-CAP-07), §18.4 (saved views, FR-VIEW-04), §13.19 (My Staff), §13.14 (Activity History), §13.12 (Portfolio) and §13.21 (Weekly Planner), this section governs. The changes it makes to §8.5.2 (one new permission row), §10.4 (thresholds), §12.3 (`original_date`), §12.16 and §13.7 (baseline ghosts) and §30 (baselining beyond original dates) are recorded as amendments to those sections. It never shifts a date automatically, never computes float or a critical path, and never enforces a date: the Should-NOT rows on auto-shifting successors and §30's CPM exclusion are unchanged.

### 50.1 Scope and boundary

Six parts: row actions on the task list (§50.2), the project-aware person picker (§50.3), date context and successor warnings (§50.4), schedule honesty (§50.5), small additions (§50.6), and handoffs and the milestone chain on the Timeline (§50.7). Outside this amendment: quick actions on the board, the cross-project task list or phone cards; any status or label editor; enforcing a date; rewriting a lag or link; undo; named or multiple baselines; re-baselining tasks; a new Portfolio column; handoffs or the chain on the cross-project Gantt; critical path, float and successor auto-shift (parked roadmap item 37).

- **FR-INT-01.** Each part MUST ship dark behind its own organisation flag (`flag.row_quick_actions`, `flag.picker_project_rank`, `flag.date_context`, `flag.schedule_baseline`, `flag.small_ux`, `flag.timeline_handoffs_chain`) under the flag rules of §43. With a flag off, every screen, response field, ordering, figure, export and report behaves exactly as it does without this section: new endpoints answer as not found, new request parameters are ignored, new settings have no effect, and stored baselines are ignored. The server decides every action and every visibility question; the client only shows what the server allows.

### 50.2 Row quick actions and inline name edit (roadmap item 18)

§13.3 already says "Row hover reveals quick actions". This part builds it on the project Task List desktop table.

- **FR-INT-02.** A task row MUST reveal its quick actions when the pointer is over it or keyboard focus is inside it, in a fixed trailing cell so no column moves. The actions are labelled buttons whose accessible names include the task key ("Start 1234-T014"); they are never hover-only and never colour-only. A viewer who may not change the row sees Open only. Rows in Archived or Cancelled projects show Open only. The deleted-items list of §49 shows no quick actions.

- **FR-INT-03.** The actions MUST behave as the existing controls do. **Open** opens the task panel, as the key does. **Start** appears only on a Not Started task and requests In Progress through the status menu's path, so its reason and reviewer dialogs, the start authorisation of FR-RDY-02 and every refusal behave as they do there; a refusal shows the server's reason and changes nothing. **Block** appears only when no manual block is set and opens the existing manual-block dialog (type and reason, §13.3.1). Which actions show follows the same permission source as the row's other inline edits; the server still decides each request.

- **FR-INT-04.** A pencil button beside the task name, revealed like the quick actions, MUST turn the name into the existing inline text control. Enter or leaving the field saves through the existing task edit (name required, at most 200 characters); Escape cancels. Save feedback, the G-07 conflict message and the activity-log entry are the existing ones. Clicking the name still opens the panel. The pencil shows only to viewers who may edit the task.

### 50.3 Person picker ranked by project (roadmap item 20)

- **FR-INT-05.** The people read (`GET /users`) MUST accept an optional project. When the viewer can read that project, results are ordered: the viewer first, then people on the project's team now or removed from it within `picker_recent_days` (default 90), then everyone else. Within each group, people matched by name or email come before people matched only by job title or home discipline, then by display name. Each result says whether the person is on the project. A project the viewer cannot read gets the same not-found answer as any project read. Without a project, the order is today's (display name).

- **FR-INT-06.** Search MUST also match job title and home discipline name, ranked after every name and email match, so every person today's search returns keeps its place ahead of the new matches. G-11 (inactive people cannot be chosen) is unchanged.

- **FR-INT-07.** Every picker inside a project page, or inside an item panel of that project, MUST send that project through one shared context, as the capacity week already is (FR-PICK-02), so no call site changes. Candidates on the project show the text "On this project". Pickers outside a project (Portfolio, Weekly Planner, Workload, Reports, Time, My Staff, Admin) keep today's order. Capacity figures (FR-PICK-02) are unchanged.

### 50.4 Earliest consistent dates and successor warnings (roadmap item 22)

- **FR-INT-08.** A date-context read for one task or deliverable, optionally with proposed start and due dates, MUST return: the earliest consistent start and the earliest consistent due, each with reasons naming the predecessor, link type and lag (D-12 by link type, FR-DEPT-05); the latest consistent due (a task's deliverable due date, T-04; a deliverable's milestone date, DL-03); and, for proposed dates, the item's own inconsistencies and the successors that would become Date Inconsistent, each with its reason. The read MUST use the same functions the evaluator uses for D-12, T-04 and DL-03, so a hint and a later flag never disagree. Lag counts working days when working days are on (packet 021). A successor already inconsistent for the same pair is not listed again.

- **FR-INT-09.** When a proposed or earliest date falls on a day the task's assignee or the deliverable's owner has no available hours (the planner's day rule, FR-PLN-08, with office holidays, approved leave and the person's own schedule), the read MUST add "not available on that date" and the next date with available hours inside the planning horizon. This part appears only for viewers who could already see that person in Workload or the Weekly Planner (§40.10 D8); for others it is left out silently. No leave reason, type, balance or HR reference is read, returned or shown (as FR-DIG-04).

- **FR-INT-10.** In the task panel and the deliverable panel, while a start or due date input is open, a muted line under the input MUST show the earliest consistent date and, when the chosen date conflicts, the reason. The read runs when the input opens and when the chosen date changes, never per keystroke. The hint is advisory: it never blocks a save and never changes a date, lag or link; saving follows today's rules, reasons and flags. Task list date cells are unchanged.

- **FR-INT-11.** The drag confirmation on the Timeline and the Gantt (§12.16, §36.4) MUST list, from the date-context read, the successors that would become Date Inconsistent and, for a deliverable, its tasks that would fall after its new due date (T-04), each with its reason, plus the dragged item's own new inconsistencies. Confirm stays available, and only the dragged item's dates change.

### 50.5 Schedule honesty (roadmap item 26)

- **FR-INT-12.** An organisation setting `slip_display_days` (default 0) MUST govern when the Timeline, the Gantt and the Milestone View strip draw today's ghost at the original date (bar or hollow diamond) and the "Moved n d" label: only when the start or due date differs from its original date by more than that many calendar days. The same threshold governs the second ghost that FR-INT-14 adds at a PM baseline. At 0 every difference draws, exactly as today. The server computes whether each item's slip is shown and sends it with the item, so screens do not re-derive it. The setting changes display only: the Slipped indicator, slip days, filters, exports and reports are unchanged by it.

- **FR-INT-13.** On a milestone or a deliverable, the project's PM MUST be able to "Re-baseline" with a required reason. It stores the current planned dates as `baseline_date` (milestone) or `baseline_start_date` and `baseline_due_date` (deliverable). `original_date` and the deliverable's original dates never change. The change and its reason are written to the activity log in the same transaction with the old and new baseline (constitution V, FR-AUD-02), so the previous baseline stays readable in History. Re-baselining again replaces the one baseline; "Use original dates" clears it, also with a reason. Row versions apply (G-07). It is not offered on Complete or Cancelled items or in Archived or Cancelled projects. Only the project's PM may do it; this adds one row to the §8.5.2 matrix, with its permission test.

- **FR-INT-14.** Slip MUST keep measuring from the original date everywhere it does today: the Slipped indicator, slip days in the Milestone View, ghosts, exports and reports. When a PM baseline is set, the Milestone View, the panels, exports and reports add "Since re-baseline: n d", measured from the baseline, beside today's slip, and the Timeline and Gantt draw a second ghost at the baseline beside the original one. An item never re-baselined keeps exactly today's values. The milestone and deliverable panels show the original date always and the baseline when set. M-02 notices are unchanged.

- **FR-INT-15.** For an Active project with a target completion date, when the latest planned date among its open milestones (not Complete or Cancelled) and open deliverables (not Issued, Accepted or Cancelled) is after the target, the project MUST show the note "Latest planned date [date] ([item key]) is after target [date]". It is computed by a pure function in the domain layer on read and stored nowhere. It appears as one muted line in the project Dashboard's health tile, on the project Timeline's target line and on the Gantt project row. It is information only: it is not a health input, attention item or notification, and "Why?" names this rule.

### 50.6 Small additions (roadmap item 30)

- **FR-INT-16.** The saved-view menu MUST offer "Copy as personal view" on the view in use. It opens the existing save dialog prefilled with "[name] (copy)" and personal scope, and saves a new personal view with the same parameters through the existing create. The original view is unchanged.

- **FR-INT-17.** My Staff MUST gain the saved-view menu for personal views only, as the Weekly Planner has, saving its URL parameters (scope, inactive, office, discipline, indicator). A `staff` list joins the saved-view lists; office and discipline values are checked on opening as other lists' are. A default view applies only when the page opens without parameters (§18.4).

- **FR-INT-18.** Activity History (§13.14) and the item History tab MUST offer "Group changes", off by default. When on, consecutive entries by the same actor on the same item within `activity_burst_minutes` (default 10) show as one entry ("Pat made 4 changes", with the time span) that expands to the unchanged entries. The choice is kept in the URL. Display only: the log, its filters and its export are unchanged.

- **FR-INT-19.** The task list's Last activity cell MUST name who acted ("2 h ago · Pat"), from the newest activity-log entry for that task ("System" for system writes), read once per page with the existing item-history index. Its sort is unchanged.

- **FR-INT-20.** The task panel MUST offer "Create deliverable from this task" to viewers who may create a deliverable in the task's discipline. It opens the existing deliverable form prefilled with the task's name, discipline, direct milestone and start and due dates; the owner default is unchanged. Saving creates the deliverable and a Related link between it and the task in one transaction, both logged. The task keeps its type, deliverable and dates (constitution V). The task panel lists "Related deliverables" and the deliverable panel "Related tasks" for these links.

- **FR-INT-21.** When a status change needs no dialog, the status pill MUST show the requested status at once, marked as saving in text and to assistive technology, then the server's answer. A refusal returns the pill to the previous status and shows the server's reason, as today. Changes that need a reason, comment, reviewer or start authorisation open their dialogs as today and show nothing early.

- **FR-INT-22.** The over-capacity message when confirming an allocation (FR-CAP-07) MUST give the count of dates over capacity out of the dates in the preview: "2 of 5 dates exceed available capacity by 6 h in total."

- **FR-INT-23.** The cross-project Timeline MUST keep which projects are collapsed in the URL (`collapsed`, as project numbers, or `all`), so reload, back and shared links keep it. With no parameter every project is expanded, as today.

- **FR-INT-24.** The Portfolio MUST offer a "Capacity by discipline" link that opens the Weekly Planner grouped by home discipline (`/planner?groupBy=homeDiscipline`), shown only to viewers who may open the Weekly Planner.

### 50.7 Handoffs on the Timeline and the chain to a milestone (roadmap item 32)

- **FR-INT-25.** The project Timeline MUST show a Handoffs row group with one row per handoff that is not Cancelled: key, title, and sending and receiving discipline, with distinct marks at the needed-by and promised dates. The FR-HND-01 mismatch ("promised n d after needed") and the FR-HND-06 overdue-by-needed-date reason show as text. There are no arrows and no drag, and no date changes here. A row opens its handoff on the Handoffs page. The discipline filter matches the sending or receiving discipline, the date range applies, and Hide completed hides Incorporated handoffs. Visibility is the Handoffs list's.

- **FR-INT-26.** The project Timeline's filters MUST offer "Chain to milestone". When a milestone is chosen, the Timeline highlights the tasks whose affected milestones (D-13, stored by the evaluator up to `chain_depth_limit`) include it, the deliverables holding those tasks or targeting the milestone, and the arrows between highlighted tasks; everything else is dimmed, never hidden. Highlighted rows say "in the chain to [milestone key]" in their accessible label, and a count names how many items feed the milestone. The choice is kept in the URL (`chainTo`) and shows as a removable filter chip. Nothing is calculated beyond D-13: no float, no critical path, no rescheduling.

### 50.8 Acceptance criteria

- **AC-INT-01.** Given each flag of FR-INT-01 is off, then the existing browser suites and API tests pass unchanged, `GET /users` orders as today with or without a project, the new endpoints answer as not found, a stored baseline does not change slip, and `slip_display_days` has no effect. Given each flag is on, then its part works as below.

- **AC-INT-02.** Given a PM on the project Task List, when the pointer rests on a row or focus moves into it with Tab, then Open, Start and Block appear in a trailing cell without moving any column, each named with the task key; given a Viewer, only Open appears; given an Archived project, only Open appears.

- **AC-INT-03.** Given a Not Started task the viewer may start, when they choose Start, then it moves to In Progress through the status menu's path; given the task needs a start authorisation (FR-RDY-02), then that dialog opens; given the server refuses, then its reason shows and the row is unchanged.

- **AC-INT-04.** Given a task with no manual block, when Block is chosen and a type and reason are saved, then the block is set and logged as from the panel, and the row no longer offers Block.

- **AC-INT-05.** Given an editor activates the pencil beside a task name, when they type a new name and press Enter, then the name saves with one activity-log entry with old and new values; Escape restores the old name; an empty name is refused with the field message; a stale row version shows the existing conflict message; clicking the name still opens the panel.

- **AC-INT-06.** Given project P whose team has Sam now, Lee was removed 30 days ago and Kim 120 days ago, when the viewer Pat opens the picker with project P and no search text, then the order is Pat, then Lee and Sam by name, then everyone else, Kim included, by name. Lee and Sam are marked as on the project and Kim is not.

- **AC-INT-07.** Given the search "civil", then people named or emailed "civil" come first in today's order and people whose job title or home discipline matches follow; given no project, the order equals today's; given a project the viewer cannot read, the answer is not found.

- **AC-INT-08.** Given the task panel of project P, when the assignee picker opens, then it sends P and shows "On this project" beside team members; given the Portfolio PM filter, then no project is sent and the order is by name.

- **AC-INT-09.** Given task B depends on A Finish-to-Start with a 2-day lag and working days on, A due on a Thursday, then B's earliest consistent start is the following Monday with the reason naming A, the link type and the lag; Start-to-Start and Finish-to-Finish links give the earliest start and earliest due by their rules; and setting B to its earliest dates produces no D-12 flag at the next evaluation.

- **AC-INT-10.** Given a task under a deliverable due on day X, then its latest consistent due is X (T-04); given a deliverable targeting a milestone, then its latest consistent due is the milestone date (DL-03); given proposed dates that break either rule, then the item's own inconsistencies list them.

- **AC-INT-11.** Given the assignee has approved full-day leave on the earliest start and the viewer's planner scope includes them, then the read adds "not available on that date" and the next date with available hours; given a viewer outside that scope, then the availability part is absent; in both cases the response holds no leave reason, type or balance.

- **AC-INT-12.** Given the due date input is open in a task panel, then a muted hint shows the earliest consistent due; when the user saves an earlier, inconsistent date, then it saves as today and D-12 flags it at the next evaluation; no hint request is made per keystroke.

- **AC-INT-13.** Given A has successors B and C, and C is already inconsistent with A, when A is dragged four days later on the Timeline, then the dialog lists B with its reason and not C again; confirming changes only A's dates; cancelling changes nothing.

- **AC-INT-14.** Given `slip_display_days` is 0, then ghosts and "Moved n d" labels match today's for every item; given it is 3, then an item moved 2 days shows no ghost or label and an item moved 4 days shows both; in both cases the Slipped indicator, slip days, filters and exports are unchanged.

- **AC-INT-15.** Given the PM re-baselines milestone M with a reason, then `baseline_date` equals M's date, `original_date` is unchanged, and one activity-log entry holds the old and new baseline and the reason; given a Discipline Lead or owner, then it is refused (matrix test); given no reason, then it is refused; given a stale row version, then it is refused with the conflict answer.

- **AC-INT-16.** Given milestone M with original date D0, moved 14 days later, then re-baselined, then moved 3 more days, then M's slip is 17 days in the indicator, Milestone View and exports, "Since re-baseline: 3 d" shows beside it, and the original date still shows D0; after "Use original dates" with a reason, only the 17-day slip shows; given the flag is off, the slip is 17 days and nothing is added.

- **AC-INT-17.** Given an Active project whose open milestone 1234-M07 is planned 12 days after the target, then the Dashboard health tile, the Timeline target line and the Gantt project row show the latest-date note naming 1234-M07, "Why?" names FR-INT-15, and the health colour is unchanged; given the latest date is on or before the target, no target, or a project not Active, then no note shows.

- **AC-INT-18.** Given a shared project view the viewer cannot edit, when they choose "Copy as personal view" and save, then a new personal view with the same parameters appears under their views and the shared view is unchanged.

- **AC-INT-19.** Given a Supervisor on My Staff with office and indicator filters, when they save a personal view, then reopening it restores both filters; a project-scope save is refused; a default applies only when My Staff opens without parameters.

- **AC-INT-20.** Given an item edited four times by Pat within 10 minutes and once more 11 minutes later, then with "Group changes" off the History shows five entries as today; with it on, it shows one group of four and one single entry, and the group expands to the same four entries; the export is unchanged.

- **AC-INT-21.** Given Pat last edited a task, then its Last activity cell reads "[time] · Pat"; given the newest entry was written by the system, it reads "[time] · System"; sorting by Last activity orders as today.

- **AC-INT-22.** Given a task 1234-T014 in Civil with a milestone and dates, when a Discipline Lead chooses "Create deliverable from this task" and saves, then the deliverable is created with those prefilled values, a Related link joins it to the task, both are logged, the task is unchanged, and each panel lists the other; given a viewer who may not create a deliverable in Civil, the action is absent.

- **AC-INT-23.** Given a status change with no dialog, then the pill shows the new status with a saving marker before the server answers; given the server refuses, then the pill returns to the previous status and the reason shows; a change needing a reason opens its dialog with nothing shown early.

- **AC-INT-24.** Given an allocation preview of 5 dates with 2 over capacity by 6 hours in total, then the confirm dialog reads "2 of 5 dates exceed available capacity by 6 h in total."

- **AC-INT-25.** Given two projects on the cross-project Timeline with one collapsed, when the page reloads or the link is shared, then the same project is collapsed; "Collapse all" writes `collapsed=all`; with no parameter all projects are expanded.

- **AC-INT-26.** Given a viewer who may open the Weekly Planner, then the Portfolio shows "Capacity by discipline", which opens the Planner grouped by home discipline; given a viewer who may not, the link is absent.

- **AC-INT-27.** Given a Submitted handoff from Survey to Civil promised 3 days after it is needed, then the project Timeline shows its row with both dates marked and "promised 3 d after needed" as text; it cannot be dragged; clicking opens it on the Handoffs page; a Cancelled handoff shows no row; Hide completed hides an Incorporated one; the discipline filter matches either discipline.

- **AC-INT-28.** Given milestone M fed by a chain of three tasks (the evaluator's D-13 result), when "Chain to milestone" M is chosen, then exactly those tasks, their deliverables and the arrows between them are highlighted, everything else is dimmed and still listed, the count names the items, the URL holds `chainTo`, removing the chip clears it, and nothing is saved.

- **AC-INT-29.** Every changed screen passes axe with zero violations at desktop and phone widths, every new control is reachable and usable by keyboard, colour is never the only signal, and the task list read with Last activity names stays within the §22 targets for a 500-task list.

### 50.9 Decisions and deferred proposals

Each decision below uses its default until Jay decides otherwise. Defaults apply while the parts are dark; a pending decision blocks only its flag being turned on.

| # | Decision | Default |
|---|---|---|
| D1 | Where row quick actions appear | The project Task List desktop table only. |
| D2 | Which quick actions | Open, Start and Block. No status or label editor (§30 custom statuses). |
| D3 | Who is "on this project" in the picker | Current team members, plus people removed within `picker_recent_days` = 90. |
| D4 | Picker order without a project | Unchanged: by display name; the viewer is not moved first. |
| D5 | Where the date hint shows | Task and deliverable panels and the drag confirmation; list date cells unchanged. |
| D6 | Who sees availability in the hint | Only viewers who could already see that person in Workload or the Weekly Planner (§40.10 D8). |
| D7 | `slip_display_days` default | 0, so today's display is unchanged until an Admin raises it. |
| D8 | What a re-baseline stores | The item's planned dates at that moment; one baseline per item; "Use original dates" clears it. |
| D9 | What slip measures from | The original date, everywhere slip appears today. A set PM baseline adds a separate "since re-baseline" figure beside it. |
| D10 | Who re-baselines, and what | The project's PM; milestones and deliverables only, not tasks. |
| D11 | Latest planned date scope | Active projects with a target; open milestones and open deliverables; tasks excluded; information only. |
| D12 | Activity grouping | Off by default; `activity_burst_minutes` = 10. |
| D13 | Deliverable from a task | Prefill and a Related link; the task is not moved under the new deliverable. |
| D14 | Flags | One per part; the §50.6 additions share `flag.small_ux`. |
| D15 | Handoff rows | Project Timeline only; Cancelled not drawn; Incorporated hidden by Hide completed. |
| D16 | Chain source | The stored D-13 result with `chain_depth_limit`; no new graph calculation. |

Deferred follow-up proposals, outside packet 046: quick actions on the board, the cross-project task list and phone cards; ranking by recent assignment rather than team membership; date hints in list cells; re-baselining tasks; the latest-date note as a Portfolio column or health input; handoffs and the milestone chain on the cross-project Gantt; collapsed state on the project Timeline. Critical path, float, successor auto-shift and cross-project links stay parked (roadmap item 37; §30; the Should-NOT rows).
