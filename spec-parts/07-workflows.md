## 14. User Workflows

Each workflow states the actor, trigger, steps, system response, exceptions, and result. Rule IDs refer to Section 15; screen references to Section 13.

### Workflow 1 — PM creates a new project (without template)

**Actor.** Project Manager (system role). **Trigger.** New project awarded or internal project approved.

**Steps.**
1. From Projects, click **Create project**.
2. Step 1 of 2 — Identity: enter project number (validated against format and uniqueness), name, client (search or "request new client" note to Admin), office, project type, description, location, start date, target completion date, client reference. Choose "Start blank" (template path is Workflow 2).
3. Step 2 of 2 — Team & disciplines: tick disciplines (from the organisation list); for each, choose a Discipline Lead (optional now, flagged later); add members with roles.
4. Click **Create**.
5. On the new Project Dashboard, add milestones (Milestone view), then deliverables per discipline (or delegate to DLs), then tasks.
6. When ready, change status from `Setup` to `Active`.

**System response.** Creates `Project` in `Setup`; adds creator as primary PM and member; creates `ProjectDiscipline` rows and adds leads as members; logs creation; sends in-app notifications to leads and members ("You were added to 1234 DCC Dundurn Roads as Civil Discipline Lead"). Dashboard shows a Setup banner with a checklist (milestones ≥ 1, each discipline has a lead, each submission milestone has ≥ 1 deliverable) that is advisory only.

**Exceptions.** Duplicate project number → inline error with link to the existing project (E-13). User lacks PM system role → Create button hidden; Admin can grant. Client does not exist → allow save with client blank? No: client is required; provide "Internal / TBD" client option managed by Admin.

**Result.** A project exists with team and disciplines; evaluation begins when Active.

---

### Workflow 2 — PM creates a project from a template [Phase 2]

**Actor.** PM. **Trigger.** Standard project type (e.g., municipal infrastructure design).

**Steps.**
1. Create project → Step 1 identity as in Workflow 1 → choose **Start from template** → pick "Municipal Infrastructure Design v3".
2. Wizard page A — Disciplines: pre-ticked per template; untick those not in scope (e.g., Environmental); choose a lead per discipline.
3. Wizard page B — Milestones: table of template milestones with computed dates from offsets and the entered start date; PM overtypes contractual dates (e.g., 60% Design Submission = 2027-01-29); milestones left blank stay undated.
4. Wizard page C — Summary: counts of deliverables and tasks to be created, dependency count, warnings (e.g., "3 deliverables will be undated because 'Tender' has no date").
5. Click **Create project**.
6. Review the generated deliverables and tasks; delete or edit what does not apply; activate.

**System response.** Instantiates per §12.14; stamps `created_from_template_id`/`template_version`; resolves `assign_to_role`; drops dependencies referencing unticked disciplines; project is in `Setup`; logs one "Created from template" entry plus item creation entries; no digests until Active.

**Exceptions.** Template retired mid-wizard → refuse with message. Milestone name collisions with manual entries → keyed uniquely, no collision possible. Dates produce task due dates in the past → allowed but flagged Overdue only after activation.

**Result.** A structured project with 6 disciplines, 11 milestones, ~40 deliverables, and ~150 tasks created in minutes; PM edits rather than builds.

---

### Workflow 3 — Discipline Lead creates deliverables

**Actor.** Discipline Lead (Civil). **Trigger.** Project activated or scope confirmed; DL defines what Civil will produce for each submission.

**Steps.**
1. Open project → Deliverables tab → filter Discipline = Civil (defaulted for a DL).
2. Click **Create deliverable**: name "60% Civil Drawing Package", type Drawing Package, owner (self or senior designer), reviewer (senior engineer), milestone "60% Design Submission", due date defaults to the milestone date; DL sets due to 3 days earlier for internal buffer; requires review = on.
3. Repeat for Stormwater Management Report, Preliminary Cost Estimate, etc.
4. Optionally expand a deliverable and add tasks inline ("Update grading", "Update pipe network", "CAD QA", "Technical review", "PM review", "Issue package") assigning people and due dates, marking "Technical review" and "PM review" as requiring review, and adding dependencies (Update grading → CAD QA → Technical review → PM review → Issue package).

**System response.** Creates deliverables with keys; validates DL-01; sets Not Started; if due date > milestone date, flags Date Inconsistent (A-17) but saves; notifies owner ("You own 1234-D012"); logs.

**Exceptions.** DL tries to create a deliverable in another discipline → permission error with hint ("Ask the PM or the Electrical lead"). Milestone missing → allowed; deliverable has no roll-up to milestone until linked (A-12 will flag the milestone if it is a submission with no deliverables).

**Result.** The Civil scope for the submission is visible on the register and the milestone view immediately shows readiness (0/5 issued).

---

### Workflow 4 — PM assigns a task

**Actor.** PM (or DL). **Trigger.** Work identified in coordination meeting or during planning.

**Steps.**
1. From Task List (or Weekly Coordination in meeting mode), click **Create task** or `c`.
2. Enter name "Coordinate hydro pole relocation with utility", discipline Electrical, deliverable (optional) "Utility Coordination Plan", assignee (people picker), reviewer (optional), due date, priority, estimated hours, requires review.
3. Optionally add "Depends on" 1234-T0031 "Civil preliminary alignment".
4. Save.

**System response.** Creates task with key; if assignee is not a member, adds them as Team Member and notifies PM (TM-06); sends assignment notification (immediate: in-app + email per defaults) to assignee and, if set, reviewer ("You are reviewer for…"); computes indicators — if predecessor is incomplete, task shows Waiting; logs.

**Exceptions.** Assignee is Inactive → picker excludes inactive users; existing tasks with inactive assignees are flagged A-18. Reviewer = assignee with `allow_self_review` false → validation error (R-02). Dependency would create a cycle → rejected with path (D-03).

**Result.** The task appears on the assignee's My Work under the right due bucket, with its blocker visible if any.

---

### Workflow 5 — Employee updates assigned work

**Actor.** Team member. **Trigger.** Daily work; Monday planning; before coordination meeting.

**Steps.**
1. Open My Work → My Tasks (due bucket grouping).
2. For a task not yet started: change status to In Progress (or set progress 10%, which prompts In Progress).
3. Update progress to 60%; add a comment "Grading updated for 60%; waiting on survey topo for the east limit" and set a manual block if truly waiting (type Internal, reason "Survey topo east limit", or add a dependency on the survey task if one exists).
4. When finished: if the task requires review, set Ready for Review (system asks for reviewer if missing); otherwise set Complete.

**System response.** Each change autosaves, updates `last_activity_at` (clearing Stale), logs field changes, and notifies watchers/PM per preferences (in-app). Ready for Review notifies the reviewer immediately and moves the task into their My Reviews. Manual block marks the task Blocked and surfaces it in the project's blocked list with the reason. Complete sets `completed_at`, progress 100, re-evaluates successors and, if any become unblocked, notifies their assignees ("1234-T0031 is complete — 1234-T0042 is no longer waiting").

**Exceptions.** Task is Blocked and user sets In Progress → warning, allowed (D-01). Task requires review but user tries Complete → transition not offered; hint shown. Concurrent edit by PM → conflict message with reload.

**Result.** Status is current without a status meeting; the PM sees changes on the dashboard and in "Recently completed".

---

### Workflow 6 — Task becomes blocked by another task

**Actor.** System (rules), with PM/DL/assignee responding. **Trigger.** A predecessor slips or a successor's start date arrives while the predecessor is incomplete.

**Steps (system).**
1. Nightly and on each relevant change, evaluate D-05/D-06 for all successors.
2. Successor 1234-T0042 "Civil detailed grading" has predecessor 1234-T0031 "Geotechnical pavement recommendations" (due 2026-09-10, In Progress). On 2026-09-11 the predecessor becomes Overdue → successor becomes Blocked (D-06 b) and predecessor becomes Blocking Others (D-11) and fires A-03 (Critical).
3. Notifications: successor assignee ("Your task 1234-T0042 is blocked by 1234-T0031, overdue, owned by Sam"), predecessor assignee ("1234-T0031 is overdue and blocking 1 task"), both DLs and PM (in-app; digest).
4. Dashboard: Blocked count +1; PM Attention shows A-03 at the top; affected milestone "60% Design Submission" listed on both tasks.

**Steps (people).**
5. PM opens the attention item; sees the chain; talks to the Geotech lead; the predecessor's due date is moved with reason, or resources are added.
6. When 1234-T0031 is Complete, the system clears the block (D-07) and notifies the successor's assignee that they can start.

**Exceptions.** Predecessor put On Hold → remains unsatisfied; successor stays Blocked with "predecessor on hold" shown. Predecessor Cancelled → dependency satisfied with info note (D-04). Predecessor deleted → edge removed, successor unblocked, PM and assignee notified (D-09).

**Result.** The blocker is visible and attributed within one evaluation cycle, and nothing needs to be typed to make it so.

---

### Workflow 7 — Task is submitted for review

**Actor.** Assignee, then Reviewer. **Trigger.** Work complete on a task with `requires_review`.

**Steps.**
1. Assignee sets Ready for Review; adds a comment summarising what to review and where the files are (document link to the SharePoint folder).
2. Reviewer sees the task in My Reviews with the deliverable's due date and priority; opens it; sets In Review.
3. Reviewer approves → Complete.

**System response.** Ready for Review → immediate notification to reviewer; In Review → in-app notification to assignee; if the task stays Ready for Review beyond `review_stale_days`, A-11 fires to reviewer, DL, PM. Complete → `completed_at`, successors re-evaluated, deliverable progress recalculated; if all tasks of the deliverable are Complete, the deliverable panel suggests moving the deliverable to In Review (never automatic).

**Exceptions.** Reviewer unavailable → PM/DL changes reviewer; both notified (R-04). Reviewer is also the assignee → prevented at assignment (R-02). Assignee withdraws (found an error) → Ready for Review → In Progress allowed.

**Result.** Review is visible work with an owner and an age, not an invisible queue.

---

### Workflow 8 — Reviewer requests revisions

**Actor.** Reviewer. **Trigger.** Review finds issues.

**Steps.**
1. In Review → click **Request revision**; comment mandatory ("Profile stationing does not match plan; update sheets C-201 to C-205").
2. Save.

**System response.** Status → Revision Required; `review_round` increments (r2); a Review comment with the round number is posted; assignee notified immediately; DL notified in-app; deliverable progress unchanged; if the task is now Overdue or Due Soon, indicators show. When the assignee resubmits (Revision Required → In Progress → Ready for Review), the reviewer is notified with "Round 2".

**Exceptions.** Reviewer tries to request revision without comment → blocked (R-03). Task reaches round ≥ 3 → shown as a chip; no rule fires (Recommendation: keep it informational).

**Result.** Revision cycles are counted and visible; PM can see a package that is bouncing.

---

### Workflow 9 — PM runs the weekly coordination meeting

**Actor.** PM with DLs. **Trigger.** Scheduled weekly meeting.

**Steps.**
1. Open project → Weekly Coordination → toggle **Meeting mode**; project the screen.
2. Section 1 Headline: state health and next submission.
3. Section 2 PM attention: address Critical items; snooze with a note where a plan exists; reassign or re-date inline.
4. Section 3 Milestones approaching: confirm readiness; for "At Risk", open the "why" and agree actions.
5. Section 5 Decisions required: confirm who is chasing the client; record any decisions received; defer with new dates where the client has committed to a date.
6. Section 6 Blocked work (grouped by blocker): agree the unblock action for each blocker once.
7. Section 7–8 Overdue and Due this week: DLs commit to new dates (with reason) or confirm.
8. Section 9 Discipline round: each DL speaks to their card; create tasks or decisions inline as needed.
9. Section 11 Recently completed: acknowledge.
10. Click **Mark as reviewed**; **Copy summary** into the Teams meeting chat.

**System response.** Every inline change is a normal change (logged, notified) and appears in the meeting tray; "Mark as reviewed" stamps `last_coordination_reviewed_at`; the summary text lists headline, milestones, decisions, blocked, and overdue with keys.

**Exceptions.** Two people edit the same task during the meeting → second save gets a conflict prompt. Meeting runs on a different day than `coordination_day` → the "this week" window still uses the configured day; the PM can change the day in settings.

**Result.** The meeting is run from live data; decisions and re-dates are captured as they are made; the following week's "since last review" is accurate.

---

### Workflow 10 — Supervisor reviews staff workload [Phase 2]

**Actor.** Supervisor (Civil group). **Trigger.** Weekly resourcing check or PM request for help.

**Steps.**
1. Open Resources → filter Supervisor = me → 8-week grid.
2. Sort by load; identify Alex at 140% next week with a deadline cluster (3 tasks due 2026-09-24/25 across 2 projects) and Jill at 25% with no unestimated tasks.
3. Expand Alex → see tasks by project; open 1234-T0058; reassign to Jill (Supervisor permission for supervised staff) with a comment.
4. Note tasks without estimates for the DLs to fill in.

**System response.** Reassignment notifies both people and the PM; workload grid recomputes; the change is logged with actor Supervisor.

**Exceptions.** Task belongs to a Restricted project the supervisor cannot see → row shows "Restricted project" with hours only; reassignment must be done by the PM. Person on leave → not modelled in MVP/P2 (Phase 3 or manual capacity override).

**Result.** Load is rebalanced with a traceable change; estimates gaps are visible.

---

### Workflow 11 — Decision becomes overdue and blocks work

**Actor.** System; PM; decision owner (client, external). **Trigger.** `required_by_date` passes with status Pending/Under Review.

**Steps.**
1. Decision 1234-DEC02 "Confirm pavement structure" owned by external party (Client PM), required by 2026-09-12, linked with relation `blocked_by_decision` to tasks 1234-T0042 and 1234-T0043.
2. On 2026-09-13 the decision is Overdue → A-04 Critical fires to requester and PM; linked tasks become Blocked with the decision as blocker (D-15); their assignees are notified; dashboard Decisions: Overdue = 1; Blocked = 2; affected milestone 60% shown.
3. PM contacts the client; client commits to 2026-09-19 → PM sets status Deferred with new date and reason; blocks clear to Waiting (still linked, no longer overdue); A-04 clears.
4. Client decides → PM records Decided with text and date; tasks' decision link becomes satisfied; assignees notified ("Decision recorded: …").

**System response.** As above, all logged; the decision's history shows Pending → Deferred (date change) → Decided.

**Exceptions.** Decision owner is internal and inactive → A-18 for the decision; PM reassigns owner. Decision is Cancelled → linked tasks unblocked with an info note.

**Result.** A client delay is dated, attributed, visible on the dashboard, and its impact (two blocked tasks and a submission) is explicit.

---

### Workflow 12 — Project reaches a design submission milestone

**Actor.** PM, DLs, deliverable owners. **Trigger.** Milestone "60% Design Submission" within `milestone_approaching_days`.

**Steps.**
1. 14 days out: milestone becomes At Risk if any targeted deliverable is not on course (H rules) → A-05 Warning to PM and affected DLs; Weekly Coordination section 3 lists it with readiness (2/5 issued).
2. DLs drive their deliverables through In Review → Ready to Issue; owners click **Issue deliverable** with date, revision "Rev A", issued to "City of X — via transmittal T-014", link to the transmittal.
3. On submission day, all 5 deliverables are Issued; PM marks the milestone **Complete**; system offers "Advance phase to Detailed Design?" → PM accepts.
4. PM records the client review period as a task or a decision ("Client 60% comments") owned by the external party, required by the contractual date, linked to the 90% deliverables so they show Waiting.

**System response.** Milestone → Complete with date; phase advanced (logged); dashboard Next Milestone rolls to "90% Design Submission"; portfolio "upcoming submissions" updates; deliverables' Issued dates recorded.

**Exceptions.** One deliverable not issued on the day → PM may still mark the milestone Complete with confirmation (M-05) and the deliverable stays Overdue with an "issued after milestone" indicator when eventually issued; or PM moves the milestone (M-02/M-04) with slip recorded. Deliverable issued with an open task ("File record copy") → confirmation, task flagged (DL-05).

**Result.** The submission is recorded with dates, revisions, and slippage, and the next cycle starts with the client's review visible as a dated wait.

---

### Workflow 13 — Project is closed and archived

**Actor.** PM; Admin for unarchive. **Trigger.** Record drawings issued; closeout complete.

**Steps.**
1. PM opens Settings → **Mark project Complete**.
2. Closeout checklist dialog lists: open tasks (n), deliverables not Issued/Accepted/Cancelled (n), open decisions (n), (P2) open issues/risks/actions. For each group the PM chooses: cancel all with reason, or leave open (they will be shown as "open at completion"). Reason for completion required.
3. Confirm. Project → Complete. Dashboard shows a Complete banner with the edit window countdown (`complete_project_edit_window_days`).
4. After the window (or immediately), PM clicks **Archive** → confirmation.

**System response.** Complete: evaluation stops; digests stop; health frozen as last computed; items remain editable by PM for corrections within the window; logs. Archive: project becomes read-only for everyone; removed from default lists; search includes it only with "include archived"; comments disabled; export of the project (CSV per entity plus activity log) offered for download at archive time (Rec).

**Exceptions.** A correction is needed after archive (E-11) → Admin unarchives (→ Complete), PM edits with reasons, PM re-archives; all logged. Project Cancelled instead → same read-only behaviour with Cancelled status.

**Result.** History is preserved, searchable, and immutable; the portfolio only shows live work.
