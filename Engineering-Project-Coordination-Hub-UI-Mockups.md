# Engineering Project Coordination Hub — UI Mockups

> **Reference boundary (2026-09-24):** The user confirmed Projects Board, Task Board, Gantt,
> Calendar, Dashboard, and My Work for the first release, plus task-hour entry under Time. The
> [six-panel image](docs/reference/six-view-workspace.png) records that feature-scope discussion.
> The downloaded [Coordination Hub V2 prototype](docs/reference/coordination-hub-v2/README.md)
> is the only UI mockup to use for visual cues; it is not a design specification. The wireframes
> below are historical proposals. Product behavior and acceptance are specified in §36 and
> packets 022–024, regardless of old Phase 2 badges or layouts in either prototype.

| Item | Value |
|---|---|
| Companion to | `Engineering-Project-Coordination-Hub-Specification.md` (v1.0 draft, including §12.18 Assignments, Following, and My Staff) |
| Covers | Earlier illustrative wireframes for engineering detail screens; the user's first-release decisions and §36 govern the pictured workspace views. |
| Date | 2026-09-24 |
| Status | Historical draft wireframes for review; not an approved design or source of release scope |

### How to read these mockups

- These are low-fidelity proposals for screen content and flows. They do not fix final placement, layout, visual style, or release scope. The V2 prototype is the sole downloaded UI visual clue; the product specification defines required behavior.
- Section references such as (§13.9) point to the specification. When this document and the specification disagree, the specification wins.
- A number in brackets inside a wireframe, like `(3)`, matches note 3 under that wireframe.
- Every mockup uses the same sample project and people (below), so numbers can be cross-checked between screens.
- There is no AI anywhere in the product. Every flag, colour, and count shown here comes from a stated rule.

#### Wireframe legend

| Symbol | Meaning |
|---|---|
| `[ Button ]` | Button. `[ + Task ]` is a create action. |
| `[Label v]` | Dropdown or picker |
| `[x]` / `[ ]` | Checkbox on / off |
| `(o)` / `( )` | Radio button selected / not selected |
| `(In Progress)` | Status pill; always contains the status text. `(In Progress v)` opens the transition menu. |
| `[Overdue 3d]` `[Blocked]` `[Waiting]` `[Blocking 2]` `[Stale 12d]` `[Review r2]` `[Date!]` `[At Risk]` | Indicator chips (§10.3) |
| `[R]` `[Y]` `[G]` `[-]` | Health or status colour: Red, Yellow (amber), Green, Grey (not evaluated). Always shown with a word or a reason. |
| `!!` `!` `i` | Attention severity: Critical, Warning, Info (§12.12) |
| `<>` `<!>` `<X>` `<v>` `(o)` | Milestone: On Track, At Risk, Overdue, Complete; hollow ghost at the original date when slipped |
| `[#####-----]` | Progress bar |
| `>` / `v` | Collapsed / expanded group or row |
| `Why?` | Opens the popover that lists the rule that fired |
| `(AC)` | Avatar with initials |
| `P2` | Phase 2. Shown so the layout reserves room; not built in the MVP. |
| `...` | More rows of the same kind |

In the real interface colour is never the only signal (§13.0). Every coloured element also carries text or an icon, which is why the wireframes spell them out.

#### Sample data used in every mockup

- **Today is Monday 2027-01-11**, organisation time zone Eastern.
- **Project 1234 DCC Dundurn Roads**: client Dundurn County Corporation; PM **Priya Nair**; phase Preliminary Design; next submission **1234-M03 30% Design Submission, Friday 2027-01-15**, currently At Risk.
- **Discipline leads**: Civil **Marc Dubois**, Geotechnical **Wei Zhang**, Electrical **Nadia Rahman**, Survey **Omar Haddad**, Environmental **Chloe Tremblay**, Project Management **Priya Nair**.
- **Other people**: **Alex Chen** (EIT, Civil), **Jill Martin** (designer, Civil), **Diane Roy** (senior technical reviewer), **Sam Okafor** (Civil group supervisor; Alex, Jill, and Marc report to Sam), **Lena Brooks** (regional manager, Executive role; Sam reports to Lena), **Jordan Lee** (System Administrator).
- **The situation**: Geotechnical's pavement recommendations (**1234-T0031**, Wei, due Wed 2027-01-06) are 5 days overdue. They block Civil grading (**1234-T0042**, Alex) and the road profile (**1234-T0043**, Jill). The client's pavement-structure decision (**1234-DEC02**) is 3 days overdue and blocks pavement layer design (**1234-T0044**). The 30% Civil Drawing Package (**1234-D012**) is at risk.

### Contents

- **A. Application shell**: A1 desktop layout · A2 project header and tabs · A3 detail panel · A4 responsive rules
- **B. Screens**: B1 My Work · B2 Project List · B3 Create project · B4 Project Dashboard · B5 Weekly Coordination · B6 Task List · B7 Task Detail Panel · B8 Kanban Board · B9 Timeline · B10 Deliverables Register · B11 Milestones · B12 Decision Register · B13 Activity History · B14 Notification Centre · B15 My Staff · B16 Resource View · B17 Portfolio Dashboard · B18 Risks, Issues, Meetings (P2) · B19 Reports · B20 Project Team and Settings · B21 Administration · B22 Global Search. See the six-view image for the new first-release Calendar and overview.
- **C. Shared components and states**: popovers, menus, dialogs, conflicts, toasts, banners, empty and loading states
- **D. Phone and tablet**
- **E. Emails**
- **F. Coverage checklist**: every specification screen, dialog, and workflow mapped to its mockup

---

## A. Application shell

### A1 Desktop layout, 1280 px and wider (§9.4, §13.0)

```text
+------------------------------------------------------------------------------------------------------------+
| [=] Coordination Hub   [ Search projects, keys, people ...   (/) ]     [ + New v ]  (bell) 4   (AC) Alex v |
+--------------------+---------------------------------------------------------------------------------------+
| > My Work        2 |                                                                                  (1)  |
|   Projects         |   CONTENT AREA                                                                        |
|   My Staff         |   - full width on desktop, because tables benefit from width                          |
|   Portfolio     P2 |   - project screens add the project header and tabs (A2)                              |
|   Resources     P2 |   - item details open as a right-side panel over the list (A3)                        |
|   Reports          |                                                                                       |
|   Notifications  4 |                                                                                       |
|   Admin            |                                                                                       |
|                    |                                                                                       |
| [<< Collapse]      |                                                                                       |
+--------------------+---------------------------------------------------------------------------------------+
```

**Notes**

1. The rail shows only what the user may use: **My Staff** for Supervisors, Executives, and Admins; **Admin** for System Administrators; **Portfolio** and **Resources** are first-release views under §36. The new Home, Boards, Tasks, Calendar, Files, and Team routes are in the six-view image. The number beside My Work counts attention items routed to the user; the number beside Notifications counts unread personal notifications (the Following tab shows its own count).

- Top bar: `/` focuses search. `+ New` quick-creates a **Task** or a **Decision**; off a project screen it first asks for the project. The bell opens the Notification Centre (B14). The user menu holds notification preferences, display density (compact or comfortable rows), and sign-out.
- The rail collapses to icons, which is the default on tablets.
- Keyboard: `/` search · `c` create task on project screens · `j` / `k` move the selection · `Enter` open the panel · `Esc` close it.

### A2 Project header and tabs, shared by every project screen (§12.1, §13.0)

```text
+------------------------------------------------------------------------------------------------------------+
| 1234  DCC Dundurn Roads               (Active)  Reported [Y] Yellow  Computed [R] Red  Why?          (1)   |
| Dundurn County Corporation  |  PM Priya Nair  |  Phase: Preliminary Design  |  Office: Hamilton            |
| Next milestone: 30% Design Submission  Fri 2027-01-15 (in 4 days)   Next submission: the same              |
| Links: SharePoint site | Teams channel | \\fs01\projects\1234 [Copy path]    [ Following: All activity v ] |
|                                                                                  (3)                  (2)  |
+------------------------------------------------------------------------------------------------------------+
| [Dashboard] Weekly Coordination  Tasks  Deliverables  Milestones  Timeline  Decisions  Team  Activity      |
|  Settings   Risks P2   Issues P2   Meetings P2                                                       (4)   |
+------------------------------------------------------------------------------------------------------------+
```

**Notes**

1. Health shows the PM's reported value first and the computed value beside it whenever they differ; `Why?` lists the rules that fired (C2). Without an override, only the computed value shows.
2. Follow control (§12.18): **All activity**, **My items only**, **Muted**, or **Unfollow**. Tooltip: "You follow this project because you are on the team."
3. Important links: UNC paths get **Copy path**, because browsers cannot open them.
4. Tabs follow §9.4. **Settings** appears for PMs only. The Phase 2 register tabs appear when those modules ship. The project number is monospace and copyable.

### A3 Detail panel pattern (§13.0)

```text
+------------------------------------------------------+----------------------------------------------------+
| LIST OR BOARD (stays visible, dimmed)                | 1234-T0042                [Open full page]  [x]    |
| 1234-T0041  Survey control check                     | Update grading for 30%                             |
| 1234-T0042  Update grading for 30%   <- selected     | ... panel content (B7) ...                         |
| 1234-T0043  Road profile 30%                         |                                                    |
|                                                      | URL: /projects/1234/tasks/1234-T0042               |
+------------------------------------------------------+----------------------------------------------------+
```

- Details open as a panel about 560 px wide, so the list keeps its context. The URL updates, so every panel can be linked to. `Esc` closes the panel and returns to the same scroll position. On tablets and phones the panel becomes a full-width overlay.

### A4 Responsive rules (§13.0)

| Width | Behaviour |
|---|---|
| 1280 px and wider | Full layout as in A1 |
| 768–1279 px (tablet) | Rail collapses to icons; tables hide low-priority columns (the column chooser still works); panels become full-width overlays |
| Under 768 px (phone) | Bottom bar with My Work, Search, and Notifications; lists become cards; actions limited to reading, changing status, updating progress, commenting, and viewing blockers (D1–D5) |
| Desktop and tablet only | Creating projects, editing milestones, templates, administration, My Staff staffing actions, Resource View, and Portfolio. Phones show a notice instead (D5). |

---

## B. Screens

### B1 My Work, the landing page (§13.10)

The one page a person opens each morning: everything assigned to them, waiting on them, or blocked, across all projects.

```text
+------------------------------------------------------------------------------------------------------------+
| MY WORK                                           Mon 2027-01-11     [Project v] [Discipline v] [Due v]    |
|                                                                      [ ] Hide waiting   Sort [Due date v]  |
| NEEDS MY ATTENTION (2)                                                                             (1)     |
|  !! A-02  1234-T0042 Update grading for 30%   Blocked 4 d by 1234-T0031 (Wei Zhang, overdue 5 d) [Open]    |
|  !  A-10  1301-T0007 Site grading sketch      In Progress, no update for 11 d; due Fri          [Open]     |
|                                                                                                            |
| Jump to: My Tasks 7 | My Reviews 1 | My Deliverables 0 | Waiting on Others 2 | Blocking Others 0           |
|          My Decisions 0 | My Projects 3 | Upcoming Milestones 2 | Recently completed 5             (2)     |
+------------------------------------------------------------------------------------------------------------+
| MY TASKS (7)                                                        Group [Due bucket v]  [ + Task ]       |
| Key          Task                         Project  Status              Progress  Indicators    Due         |
| OVERDUE (1)                                                                                       (3)      |
| 1234-T0039   Existing utilities overlay   1234     (In Progress v)     [80% v]   [Overdue 3d]  Fri 01-08   |
| THIS WEEK (4)                                                                                              |
| 1234-T0042   Update grading for 30%       1234     (In Progress v)     [60% v]   [Blocked]     Wed 01-13   |
| 1234-T0061   Stormwater concept           1234     (Revision Req. v)   [80% v]   [Review r2]   Wed 01-13   |
| 1234-T0051   CAD QA 30% package           1234     (Not Started v)     [0% v]    [Waiting]     Wed 01-13   |
| 1301-T0007   Site grading sketch          1301     (In Progress v)     [30% v]   [Stale 11d]   Fri 01-15   |
| NEXT WEEK (1)                                                                                              |
| 1187-T0042   Parking lot drainage check   1187     (Not Started v)     [0% v]                  Tue 01-19   |
| > LATER (1)     > NO DATE (0)                                                                              |
+------------------------------------------------------------------------------------------------------------+
| MY REVIEWS (1)                                                                                             |
| 1234-T0058   Utility conflict table       1234     (Ready for Review)  Jill M.   waiting 3 d   [Start]     |
+------------------------------------------------------------------------------------------------------------+
| WAITING ON OTHERS (2)                                                                             (4)      |
| 1234-T0042 Update grading for 30%  <- 1234-T0031 Issue pavement recommendations   Wei Zhang  overdue 5 d   |
|                                    <- 1234-DEC02 Confirm pavement structure       Client PM  overdue 3 d   |
| 1234-T0051 CAD QA 30% package      <- 1234-T0042 Update grading for 30%           Alex Chen  (you)         |
+------------------------------------------------------------------------------------------------------------+
| MY PROJECTS (3)                                                                                   (5)      |
| 1234 DCC Dundurn Roads    Team (Civil)    [R] Red      30% Design Sub. Fri 01-15     [All activity v]      |
| 1301 King St Watermain    Team (Civil)    [Y] Yellow   Tender Close Tue 02-02        [My items only v]     |
| 1187 Mall Parking Reno    Team (Civil)    [G] Green    IFC Mon 03-01                 [All activity v]      |
+------------------------------------------------------------------------------------------------------------+
| UPCOMING MILESTONES, NEXT 30 DAYS (2)                                                                      |
| Fri 01-15  1234-M03 30% Design Submission  <!> At Risk     Tue 02-02  1301-M06 Tender Close  <> On Track   |
+------------------------------------------------------------------------------------------------------------+
| > RECENTLY COMPLETED BY ME, LAST 14 DAYS (5)                                                               |
+------------------------------------------------------------------------------------------------------------+
```

**Notes**

1. Attention items routed to this user (rules A-01 to A-20), most severe first, each with its reason.
2. Jump links with counts. Every section collapses; the page remembers which sections the user collapsed.
3. Due buckets: Overdue, Today, This week, Next week, Later, No date. Overdue and due-today items always come first.
4. Each wait shows the blocker and its owner. Clicking an owner opens their My Work, but only for PMs, Discipline Leads, and that person's Supervisor.
5. Follow level per project (§12.18), changeable in place.

- Inline edits: the status menu offers only the transitions this user may make (T-10 to T-14). Progress moves in 10 % steps. Setting progress above 0 on a Not Started task offers In Progress; 100 % offers Complete, or Ready for Review when the task requires review.
- **Deliverables I own** and **My Decisions** use the same row layout as tasks.
- **Read-only view.** A Supervisor or PM who opens someone else's My Work sees the banner below; inline controls are hidden. PMs see only that person's tasks within their own projects.

```text
+------------------------------------------------------------------------------------------------------------+
| i  Viewing Alex Chen's My Work (read-only). You are Alex's supervisor.              [ Back to My Staff ]   |
+------------------------------------------------------------------------------------------------------------+
```

- Empty state: "Nothing is assigned to you. Tasks, reviews, deliverables, and decisions you own will appear here."

### B2 Project List (§13.2)

```text
+-------------------------------------------------------------------------------------------------------------+
| PROJECTS                                                                         [ + Create project ] (1)   |
| ( ) My projects  (o) All projects    [ Search number, name, client ... ]           Columns v   Export v     |
| [Status: Active, Setup, On Hold x]  [Office: Hamilton x]  [+ Filter]  Clear          [ ] Include archived   |
+-------------------------------------------------------------------------------------------------------------+
|    No.   Name              Client      PM          Status    Health     Next milestone      Ovd Blk My role |
| *  1234  DCC Dundurn Roads Dundurn Cty Priya Nair  (Active)  [R] Red    30% Dsn Fri 01-15   7   3   Team    |
|    1301  King St Watermain City of X   Marc Dubois (Active)  [Y] Yellow Tender Close 02-02  2   0   PM      |
|    1187  Mall Parking Reno ABC Corp    Priya Nair  (Active)  [G] Green  IFC 03-01           0   0   Team    |
|    1320  Hwy 6 Culverts    MTO         Priya Nair  (Setup)   [-] Grey   Kickoff (undated)   -   -   -       |
|    1150  Arena Expansion   City of X   Omar Haddad (On Hold) [-] Grey   (on hold)           -   -   -       |
|                                                                                                   (2)       |
+-------------------------------------------------------------------------------------------------------------+
```

**Notes**

1. **Create project** appears only for users with the Project Manager system role, and for Admins (AC-AUTH-03).
2. The default columns also include **Phase**, **Office**, and **Last activity** (off-screen to the right here). The column chooser adds client reference, project type, start date, and target completion date.

- Default filter: Active, Setup, and On Hold projects, and "My projects" for anyone who holds a project role. Default sort: health severity (Red, Yellow, Green, Grey), then next milestone date. Every column sorts.
- `*` stars a project for the user's own ordering. A row opens the Project Dashboard; the health cell opens it with the `Why?` popover already open.
- Filters: status, PM, client, office, phase, discipline, health, project type, include archived. Filter state lives in the URL (C1).

### B3 Create project (§12.1, §14 Workflows 1–2)

**Step 1 of 2: identity**

```text
+-- Create project ----------------------------------------------------------- Step 1 of 2: Identity ----+
|                                                                                                        |
|  Project number *   [ 1340        ]   must match the organisation format and be unique                 |
|                     ! 1301 already exists: King St Watermain  [Open it]                            (1) |
|  Name *             [ Barton St Reconstruction                        ]                                |
|  Client *           [ City of Hamilton                 v ]   Not listed? Choose "Internal / TBD" and   |
|                                                               ask an Admin to add it                   |
|  Office *           [ Hamilton v ]         Project type   [ Municipal Infrastructure v ]               |
|  Client reference   [ PO 45-2231      ]    Location       [ Barton St E, Ottawa St to Kenilworth ]     |
|  Start date         [ 2027-02-01 ]         Target completion [ 2028-06-30 ]                            |
|  Description        [ Full reconstruction incl. watermain and storm sewer ...                  ]       |
|                                                                                                        |
|  Start from   (o) Blank project                                                                        |
|               ( ) Template  P2        [ Municipal Infrastructure Design v3 v ]                         |
|               ( ) Copy the structure of an existing project  [ 1234 DCC Dundurn Roads v ]         (2)  |
|                                                                                                        |
|                                                                   [ Cancel ]   [ Next: Team > ]        |
+--------------------------------------------------------------------------------------------------------+
```

**Step 2 of 2: team and disciplines**

```text
+-- Create project ------------------------------------------------ Step 2 of 2: Team and disciplines ----+
|                                                                                                         |
|  DISCIPLINES                         DISCIPLINE LEAD (optional now; flagged later if empty)             |
|  [x] Project Management              [ Priya Nair (you)          v ]                                    |
|  [x] Survey                          [ Omar Haddad               v ]                                    |
|  [x] Civil                           [ Marc Dubois               v ]                                    |
|  [x] Geotechnical                    [ Wei Zhang                 v ]                                    |
|  [x] Electrical                      [ Nadia Rahman              v ]                                    |
|  [ ] Environmental   [ ] Structural   [ ] Transportation   [ ] Architecture                             |
|                                                                                                         |
|  TEAM                                ROLE                  PRIMARY DISCIPLINE                           |
|  Alex Chen    EIT, Hamilton          [x] Team  [ ] Viewer  [ Civil v ]                 [Remove]         |
|  Jill Martin  Designer, Hamilton     [x] Team  [ ] Viewer  [ Civil v ]                 [Remove]         |
|  [ + Add people: search name or email ...                   ]   shows office and job title              |
|                                                                                                         |
|  i Everyone added will follow the project (All activity) and can change that later.                     |
|                                                                                                         |
|                                                   [ < Back ]   [ Cancel ]   [ Create project ]          |
+---------------------------------------------------------------------------------------------------------+
```

**Notes**

1. Project numbers are checked for format and case-insensitive uniqueness as the user types (P-01). A duplicate links to the existing project (E-13).
2. **Copy the structure** (MVP-Recommended stopgap until templates) copies disciplines, milestones without dates, deliverables, unassigned tasks, and dependencies.

- **Create project** makes the creator the primary PM, creates the project in **Setup**, adds discipline leads and members (who each start following at All activity), and notifies them. The dashboard then shows the Setup checklist banner (C12).

**Template wizard (P2, Workflow 2)**

```text
+-- New project from template: Municipal Infrastructure Design v3 ----------------------------------------+
|  [1 Disciplines]  [2 Milestones]  [3 Summary]                                     Start [ 2026-10-05 ]  |
|                                                                                                         |
|  PAGE 2: MILESTONES. Type contractual dates over the computed ones; blank stays undated.                |
|  #    Milestone                        Type                 Computed      Contractual date              |
|  M01  Project Kickoff                  Kickoff              2026-10-12    [            ]                |
|  M02  Field Investigation Complete     Field Work           2026-11-19    [            ]                |
|  M03  30% Design Submission            Design Submission    2027-01-03    [ 2027-01-15 ]                |
|  M04  60% Design Submission            Design Submission    2027-03-04    [ 2027-03-12 ]                |
|  ...                                                                                                    |
|  M08  Tender Close                     Tender               2027-08-11    [  undated   ]                |
|                                                                                                         |
|  PAGE 3: SUMMARY                                                                                        |
|  6 disciplines, 11 milestones (5 contractual, 2 computed, 4 undated), 28 deliverables, 120 tasks,       |
|  95 dependencies.   ! 3 deliverables will be undated because "Tender Close" has no date.                |
|                                          [ < Back ]   [ Cancel ]   [ Create project in Setup ]          |
+---------------------------------------------------------------------------------------------------------+
```

### B4 Project Dashboard (§13.1)

Answers "how is this project doing and what needs attention right now" on one 1080p screen.

```text
+--------------------------------------------------------------------------------------------------------------+
| (project header and tabs, A2)                                          Scope: [All disciplines v]   (1)      |
+--------------------------------------------------------------------------------------------------------------+
| MILESTONES  <!> 30% Design Sub.   <> 60% Design Sub.   <> 85% Design Sub.   <> 100% Design    <> IFC         |
|             Fri 01-15 (4 d)       Fri 03-12 (60 d)     Fri 05-14 (123 d)    Fri 06-25        Fri 07-16       |
|                                                                                              View all >      |
+---------------------------+-------------------------+-------------------------+------------------------------+
| HEALTH                    | NEXT MILESTONE          | NEXT SUBMISSION         | PHASE                        |
| Reported [Y] Yellow       | 30% Design Submission   | 30% Design Submission   | Preliminary Design           |
| Computed [R] Red  Why?    | Fri 2027-01-15, in 4 d  | Fri 2027-01-15, in 4 d  | 4 of 9                       |
| "Client agreed +1 wk..."  |                         |                         |                              |
+---------------------------+-------------------------+-------------------------+------------------------------+
| TASKS         103 total | 56 complete | 12 in progress | 3 ready / in review | 7 overdue | 3 blocked         |
|               4 waiting | 2 unassigned                                                              (2)      |
| DELIVERABLES  4 upcoming (due in 14 d) | 2 at risk | 6 issued / accepted | 28 total                          |
| DECISIONS     2 pending | 1 overdue          ISSUES P2  1 open | 0 high          RISKS P2  1 high            |
+--------------------------------------------------------------------------------------------------------------+
| PM ATTENTION (23)                                                              Snoozed (2)   Show all >      |
| !!  A-03  1234-T0031  Issue pavement recommendations Overdue 5 d; blocking T0042, T0043  Wei Z.  [Snooze]    |
| !!  A-04  1234-DEC02  Confirm pavement structure     Overdue 3 d; blocks 2 tasks         Client  [Snooze]    |
| !!  A-05  1234-M03    30% Design Submission          4 d left; 2 of 5 issued             Priya   [Snooze]    |
| !   A-11  1234-T0052  Technical review 30% package   Ready for Review 7 d (Diane Roy)    Diane   [Snooze]    |
| !   A-08  1234-T0066  Traffic count review           No owner; due Wed 01-13             -       [Snooze]    |
| !   A-06  1234-D014   Preliminary cost estimate      Due in 2 d at 40 %                  Priya   [Snooze]    |
| i   A-20  1234-T0039  Existing utilities overlay     Due date changed 3 times            Alex    [Snooze]    |
|  ... top 10 of 23                                                                                  (3)       |
+--------------------------------------------------------------------------------------------------------------+
| DISCIPLINE PROGRESS                                                                                (4)       |
| Discipline      Lead            Status  Open  Overdue  Blocked  Due in 14 d  Next due item                   |
| Civil           Marc Dubois     [R]     14    4        3        2            1234-D012 30% Civil package Tue |
| Geotechnical    Wei Zhang       [R]     3     1        0        0            1234-T0031 (overdue 5 d)        |
| Electrical      Nadia Rahman    [Y]     4     1        0        1            1234-T0048 hydro poles, Tue     |
| Project Mgmt    Priya Nair      [Y]     3     1        0        1            1234-D014 cost estimate Wed     |
| Survey          Omar Haddad     [G]     1     0        0        0            1234-T0071 control check 01-20  |
| Environmental   Chloe Tremblay  [G]     0     0        0        0            -                               |
+------------------------------------------------------+-------------------------------------------------------+
| DUE THIS WEEK (8)                                    | BLOCKED (3)                                           |
| 1234-T0048 Confirm hydro pole conflicts  Tue  Nadia  | 1234-T0042 Update grading   by T0031   Alex           |
| 1234-D012  30% Civil Drawing Package     Tue  Marc   | 1234-T0043 Road profile     by T0031   Jill           |
| 1234-T0060 Preliminary cost estimate     Wed  Priya  | 1234-T0044 Pavement layers  by DEC02   Jill           |
| ... top 8                                            |                                                       |
+------------------------------------------------------+-------------------------------------------------------+
| RECENT ACTIVITY                                                                   [ ] Important only         |
| 09:42  Diane Roy    requested revision on 1234-T0061 Stormwater concept (round 2)                            |
| 09:10  Priya Nair   changed due date of 1234-T0060  2027-01-12 -> 2027-01-13  "Waiting on unit rates"        |
| 08:30  System       1234-T0055 complete: 1234-T0056 is no longer waiting                                     |
|  ... last 15                                                                                                 |
+--------------------------------------------------------------------------------------------------------------+
```

**Notes**

1. The discipline scope chip filters every section below the headline to one discipline.
2. Every number is a link to the filtered list that produced it, computed by the same code, so the numbers always reconcile.
3. Attention items show severity, rule, item, reason, owner, and age. **Snooze** (PM or DL) asks for 1–30 days and a note (C15). Items disappear on their own when the condition clears; nothing can be dismissed.
4. Discipline status uses the project health rules scoped to that discipline's items (§16.6). Rows open the Task List filtered to the discipline.

- Primary actions (PM): quick-create task, open Weekly Coordination, change the health override (C16), change status (B20), edit the project.
- There are no charts for their own sake: the milestone strip and progress bars are the only visuals.
- Setup and On Hold projects show a banner explaining why evaluation is paused (C12).

### B5 Weekly Coordination (§12.13, §13.9)

The screen the PM projects to run the weekly coordination meeting. Normal view:

```text
+-------------------------------------------------------------------------------------------------------------+
| (project header and tabs, A2)                                                                               |
| WEEKLY COORDINATION   Week of Mon 2027-01-11 (coordination day: Monday)     Meeting mode [ OFF | on ]       |
| Since last review: Mon 2027-01-04 09:30 by Priya    Scope [All disciplines v]   [Copy summary] [Print]      |
+--------------------+----------------------------------------------------------------------------------------+
| JUMP TO            | 1. HEADLINE                                                                            |
|  1 Headline        |    Reported [Y] Yellow (computed [R] Red)   Next: 30% Design Sub. Fri 01-15 (4 d)      |
|  2 PM attention  9 |    Overdue tasks 7 | Blocked 3 | Decisions overdue 1 | Deliverables at risk 2          |
|  3 Milestones    1 | 2. PM ATTENTION, WARNING AND ABOVE (9)            same rows as the dashboard (B4)      |
|  4 Deliverables  5 | 3. MILESTONES APPROACHING (1)                                                          |
|  5 Decisions     2 |    1234-M03 30% Design Submission  Fri 01-15  <!> At Risk  2/5 issued  Why?            |
|  6 Blocked       3 | 4. DELIVERABLES DUE THIS WEEK OR NEXT, OR OVERDUE (5), by discipline                   |
|  7 Overdue       7 |    Civil       1234-D012 30% Civil Drawing Pkg  Tue 01-12  (In Progress) 55% [At Risk] |
|  8 Due this week 8 |    Proj Mgmt   1234-D014 Prelim. Cost Estimate  Wed 01-13  (In Progress) 40%           |
|  9 Disciplines   6 |    Electrical  1234-D021 Elec. Utility Coord.   Fri 01-08  (Not Started) [Overdue 3d]  |
| 10 Issues/risks P2 | 5. DECISIONS REQUIRED (2)                                                              |
| 11 Completed    12 |    1234-DEC02 Confirm pavement structure  Client PM  req. 01-08 [Overdue 3d] High      |
| 12 Upcoming     10 |    1234-DEC03 Approve closure staging     Priya Nair req. 01-20  Due soon   Medium     |
| 13 Held          1 | 6. BLOCKED WORK, GROUPED BY BLOCKER (discuss each cause once)                   (1)    |
|                    |    Blocker 1234-T0031 Issue pavement recommendations   Wei Zhang   overdue 5 d         |
|                    |       1234-T0042 Update grading for 30%    Alex Chen    blocked 4 d                    |
|                    |       1234-T0043 Road profile 30%          Jill Martin  blocked 4 d                    |
|                    |    Blocker 1234-DEC02 Confirm pavement structure       Client PM   overdue 3 d         |
|                    |       1234-T0044 Pavement layer design     Jill Martin  blocked 3 d                    |
|                    | 7. OVERDUE WORK (7), by discipline, most overdue first                                 |
|                    | 8. DUE THIS WEEK (8), by discipline                                                    |
|                    | 9. DISCIPLINE ROUND (6)                                                        (2)     |
|                    |    +-- Civil  Marc Dubois  [R]  open 14  overdue 4  blocked 3  due in 14 d: 2 ----+    |
|                    |    | Top: 1234-D012 at risk | 1234-T0039 overdue 3 d | 1234-T0052 in review 7 d   |    |
|                    |    +------------------------------------------------------------------------------+    |
|                    |    +-- Geotechnical  Wei Zhang  [R]  open 3  overdue 1  blocked 0  due in 14 d: 0 +    |
|                    |    | Top: 1234-T0031 overdue 5 d and blocking 2 tasks                             |    |
|                    |    +------------------------------------------------------------------------------+    |
|                    |    ... Electrical, Project Management, Survey, Environmental                           |
|                    | 10. OPEN ISSUES / HIGH RISKS  P2                                                       |
|                    | 11. RECENTLY COMPLETED SINCE LAST REVIEW (12)                                          |
|                    | 12. UPCOMING: next week's tasks; deliverables due in the 14 days after this week       |
|                    | 13. HELD ITEMS (1)  1234-T0070 Tree survey  On Hold 12 d  "Waiting on arborist access" |
|                    |                                                           [ Mark as reviewed ]  (3)    |
+--------------------+----------------------------------------------------------------------------------------+
```

Meeting mode:

```text
+------------------------------------------------------------------------------------------------------------+
| MEETING MODE   1234 DCC Dundurn Roads      Section 6 of 13: BLOCKED WORK      [<- Prev]  [Next ->]  [Exit] |
|                                                                                                            |
|   BLOCKER  1234-T0031  Issue pavement recommendations      Wei Zhang       OVERDUE 5 DAYS                  |
|            1234-T0042  Update grading for 30%              Alex Chen       blocked 4 days                  |
|            1234-T0043  Road profile 30%                    Jill Martin     blocked 4 days                  |
|            [Change due date]  [Reassign]  [Comment]  [Set block]  [Create task]  [Record decision]         |
|                                                                                                            |
|   BLOCKER  1234-DEC02  Confirm pavement structure          Client PM       OVERDUE 3 DAYS                  |
|            1234-T0044  Pavement layer design               Jill Martin     blocked 3 days                  |
|            [Record decision]  [Defer...]  [Comment]  [Create task]                                         |
|                                                                                                            |
|   [ ] Hide items already discussed                                                                  (4)    |
+------------------------------------------------------------------------------------------------------------+
| CHANGES MADE IN THIS MEETING (3)                                                                           |
|  - 1234-T0031 due 2027-01-06 -> 2027-01-13  "Lab results delayed"                       Priya  09:41       |
|  - 1234-DEC02 deferred to 2027-01-15  "Client committed on the call"                    Priya  09:44       |
|  - Created 1234-T0072 "Chase client for pavement decision", assigned to Priya           Priya  09:45       |
+------------------------------------------------------------------------------------------------------------+
```

**Copy summary** puts plain text on the clipboard for the Teams meeting chat:

```text
1234 DCC Dundurn Roads - coordination 2027-01-11
Health: Yellow (computed Red). Next submission: 30% Design Submission, Fri 2027-01-15 (4 days).
Milestones: 1234-M03 30% Design Submission - At Risk (2/5 deliverables issued).
Decisions: 1234-DEC02 Confirm pavement structure - Client PM - deferred to 2027-01-15.
Blocked: 1234-T0042, 1234-T0043 by 1234-T0031 (new due 2027-01-13); 1234-T0044 by 1234-DEC02.
Overdue: 1234-T0039 (3 d), 1234-D021 (3 d), ...
```

**Notes**

1. Blocked work is grouped by its cause, so the meeting discusses each blocker once rather than once per task (AC-WC-02).
2. The discipline round has a fixed rhythm (lead, status, counts, top three items), so the meeting has the same shape every week. A Discipline Lead can open the whole view scoped to their discipline before the meeting (AC-WC-07).
3. **Mark as reviewed** (PM or DL) stamps `last_coordination_reviewed_at`, which resets "since last review".
4. Meeting mode enlarges type, hides filters, steps through sections with the arrow keys, allows inline actions on every row, and collects every change in the tray. Every inline change is a normal change: logged and notified.

- Two people editing the same item during the meeting get the conflict prompt (C10). **Print** gives a print-friendly layout.

### B6 Task List (§13.3)

```text
+--------------------------------------------------------------------------------------------------------------+
| (project header and tabs, A2)                                                                                |
| TASKS   [List]  Board                                                       [ + Task  (c) ]   Export v       |
| Quick  [Mine] [Overdue] [Blocked] [Blocking others] [Due this week] [Unassigned] [Ready for review]          |
| [+ Filter]  [Discipline: Civil x]  [Status: open x]  Clear          Group [Deliverable v]   Columns v        |
+--------------------------------------------------------------------------------------------------------------+
| [ ] Key         Task                       Assignee Reviewer Status             Indicators    Due       Prog |
| v 1234-D012  30% Civil Drawing Package   (In Progress)  [#####-----] 6/11 tasks   due Tue 01-12  [At Risk]   |
| [ ] 1234-T0042  Update grading for 30%     Alex C.  -        (In Progress)      [Blocked]     Wed 01-13 60%  |
| [x] 1234-T0043  Road profile 30%           Jill M.  -        (Not Started)      [Blocked]     Wed 01-13 0%   |
| [ ] 1234-T0051  CAD QA 30% package         Alex C.  Marc D.  (Not Started)      [Waiting]     Wed 01-13 0%   |
| [ ] 1234-T0052  Technical review 30%       Marc D.  Diane R. (Ready for Review) [Stale 7d]    Tue 01-12 100% |
| [x] 1234-T0039  Existing utilities overlay Alex C.  -        (In Progress)      [Overdue 3d]  Fri 01-08 80%  |
|     + Add task to 1234-D012                                                                                  |
| > 1234-D010  Preliminary Servicing Plan  (In Review)    [##########] 7/7 tasks    due Fri 01-15              |
| v Other tasks (no deliverable)                                                                               |
| [ ] 1234-T0066  Traffic count review       --       -        (Not Started)      [Unassigned]  Wed 01-13 0%   |
+--------------------------------------------------------------------------------------------------------------+
| 2 selected:  [Assign v] [Due date: set / shift v] [Priority v] [Deliverable v] [On Hold...] [Cancel...] (x)  |
+--------------------------------------------------------------------------------------------------------------+
```

**Notes**

- Default columns also include **Deliverable**, **Discipline**, **Priority**, **Start**, **Est. hrs**, and **Last activity**. The column chooser adds Milestone, Created, Completed, Blocking count, Review round, and Due changes.
- Grouping: Deliverable (default), Discipline, Milestone, Assignee, Status, or Due bucket. Group headers show counts and, for deliverables, the status pill and progress.
- Default sort: due date ascending with empty dates last, then priority. Every column sorts, with due date then key as the tie-breakers.
- Rows edit in place: status, assignee, due date, priority, progress. Hovering a row reveals quick actions. Clicking the **Blocked** chip opens the blockers popover (C4) without opening the panel.
- **Bulk actions** apply row by row with a permission check on each row and end with a summary toast: "12 updated, 2 skipped (no permission)" (C11). Changing or shifting dates in bulk asks for a reason (G-09).
- Export writes CSV or XLSX of the filtered view. Large projects use virtualised rows; the header and group headers stay pinned.
- Empty state for the Blocked filter: "No tasks are blocked. Blocked tasks appear here when a predecessor is incomplete or a manual block is set."

### B7 Task Detail Panel (§12.5, §12.6, §13.3.1)

```text
+-- 1234-T0042 -------------------------------------------------------- [Open full page]  [x]-----+
| Update grading for 30%                                                     (click to edit)      |
| (In Progress v)   [Blocked]  [Due in 2d]                                                 (1)    |
|                                                                                                 |
| +-- BLOCKED BY ---------------------------------------------------------------------+           |
| | 1234-T0031  Issue pavement recommendations   Wei Zhang   (In Progress)            |           |
| |             due Wed 2027-01-06   OVERDUE 5 d                                      |           |
| | 1234-DEC02  Confirm pavement structure       Client PM   overdue 3 d              |           |
| | Affects:    1234-M03 30% Design Submission (Fri 01-15)                            |           |
| | You can still start work: blocked is a warning, not a lock.                       |           |
| +-----------------------------------------------------------------------------------+           |
|                                                                                                 |
|  Assignee      [ Alex Chen v ]            Reviewer        [ none v ]                            |
|  Priority      [ High v ]                 Requires review [ ]                                   |
|  Discipline    Civil                      Deliverable     [ 1234-D012 30% Civil Drawing Pkg v ] |
|  Milestone     1234-M03 (from deliverable)                                                      |
|  Start         [ 2027-01-04 ]             Due             [ 2027-01-13 ]  changed once          |
|  Progress      [######----] 60 %          Est. hours      [ 24 ]                                |
|  Description   Update grading to the approved pavement structure; see geotech memo draft.       |
|                                                                                                 |
|  DEPENDENCIES                                                                 [Show chain] (2)  |
|  Depends on  1234-T0031 Issue pavement recommendations  (In Progress)  ! not satisfied  [x]     |
|              [ + Add: search tasks in this project ...  ]                                       |
|  Blocks      1234-T0051 CAD QA 30% package  (Not Started, waiting)                    [x]       |
|                                                                                                 |
|  MANUAL BLOCK   none   [ Set block ]                                                     (3)    |
|  LINKED DECISIONS   1234-DEC02 Confirm pavement structure (blocked by)   [ + Link decision ]    |
|  PEOPLE   Collaborators: Jill Martin [+]      Watchers: Priya Nair, Marc Dubois [+]             |
|  DOCUMENTS   30% package folder (SharePoint)   inherited from 1234-D012                         |
|              \\fs01\projects\1234\civil\grading   [Copy path]                  [ + Add link ]   |
|                                                                                                 |
|  [ Comments (4) ]  History                                                                      |
|  Marc Dubois   Fri 01-08 14:02   @Alex use the Dec 18 geotech draft for now.                    |
|  Alex Chen     Fri 01-08 15:10   Started on the draft; will redo once T0031 is issued.          |
|  [ Write a comment. Type @ to mention someone.                        ]   Ctrl+Enter to post    |
+-------------------------------------------------------------------------------------------------+
```

**Notes**

1. The status pill opens the transition menu (C5). Indicator chips come from derived state; the Blocked chip opens the blockers box above.
2. **Show chain** opens the dependency tree, up to `chain_depth_limit` levels:

```text
+-- Chain: 1234-T0042 -------------------------------------------------------------------------+
| 1234-T0012  Issue topographic base plan          Omar     Complete                           |
|  1234-T0020  Existing conditions plan            Alex     Complete                           |
|   1234-T0031  Issue pavement recommendations     Wei      In Progress  OVERDUE 5d            |
|    > 1234-T0042  Update grading for 30%          Alex     In Progress  BLOCKED   (this task) |
|      1234-T0051  CAD QA 30% package              Alex     Not Started  waiting               |
|       1234-T0052  Technical review 30% package   Marc     Ready for Review                   |
+----------------------------------------------------------------------------------------------+
```

3. **Set block** asks for a type (Client, External Party, Internal, Decision, Information, Other) and a reason (C17). While set, the task shows "blocked for N days".

- **Add predecessor** searches tasks in the same project. Tasks that would create a cycle are listed but disabled: "1234-T0051 CAD QA 30% package (would create a cycle)". Rejected edits show the loop: "T0042 -> T0051 -> T0042" (D-03).
- **Reviewer view.** When the task is In Review, the reviewer sees two buttons under the header: `[ Approve ]` and `[ Request revision ]`. Request revision requires a comment, which is posted as "Review, round 2" (R-03).
- **History** lists field-level changes with old and new values (B13). Comments are the conversation; History is the facts.
- Every field saves on its own with a small confirmation. Changing a due date as someone other than the PM or DL asks for a reason (T-16, C9). The due-date change count shows once it reaches 3.
- Deliverable and decision panels (B10, B12) use the same structure.

### B8 Kanban Board (§13.4)

```text
+--------------------------------------------------------------------------------------------------------------+
| TASKS   List  [Board]     Swimlanes [Discipline v]     [Deliverable: 1234-D012 x]  [+ Filter]                |
+--------------------------------------------------------------------------------------------------------------+
| NOT STARTED (3)  | IN PROGRESS (2)  | READY FOR REVIEW | IN REVIEW (1)    | REVISION REQ (1) | COMPLETE  >   |
|                  |                  | (1)              |                  |                  | last 14 d     |
| == Civil ================================================================================================    |
| +--------------+ | +--------------+ | +--------------+ | +--------------+ | +--------------+ |               |
| | T0043        | | | T0042        | | | T0052        | | | T0047        | | | T0061        | | On Hold 1 >   |
| | Road profile | | | Update grad. | | | Tech review  | | | Signage plan | | | Stormwater   | | Cancelled 0 > |
| | JM  Wed 01-13| | | AC  Wed  60% | | | MD  Tue 01-12| | | JM  Thu 01-14| | | AC  Wed 01-13| |               |
| | [Blocked]    | | | [Blocked]    | | | [Stale 7d]   | | |              | | | [Review r2]  | |               |
| +--------------+ | +--------------+ | +--------------+ | +--------------+ | +--------------+ |               |
| +--------------+ | +--------------+ |                  |                  |                  |               |
| | T0051 CAD QA | | | T0039 Util.  | |                  |                  |                  |               |
| | AC  Wed 01-13| | | AC  Fri 01-08| |                  |                  |                  |               |
| | [Waiting]    | | | [Overdue 3d] | |                  |                  |                  |               |
| +--------------+ | +--------------+ |                  |                  |                  |               |
| [+ Card]         | [+ Card]         |                  |                  |                  |               |
+--------------------------------------------------------------------------------------------------------------+
```

**Notes**

- Columns are the task statuses. Complete collapses to the last 14 days; On Hold and Cancelled are collapsed side columns.
- Cards show key, name, assignee initials, due date (coloured when overdue or due soon), indicator icons, deliverable chip, and priority marker.
- Dragging a card makes a status transition, so the transition rules apply. An invalid drop snaps back and says why ("Only the reviewer can move a task out of In Review"). A transition that needs a reason opens the reason dialog on drop (C6).
- Swimlanes: none, Discipline, Deliverable, or Assignee. The board and the list share filters, so switching views keeps context.
- There are no WIP limits, custom card colours, or automations. Above 500 cards the board suggests filtering (E-22).

### B9 Timeline (§12.16, §13.5)

Read-only in the MVP: milestones and deliverables in time, to spot crowding before a submission.

```text
+------------------------------------------------------------------------------------------------------------+
| (project header and tabs, A2)                                                                              |
| TIMELINE  (read-only)     Zoom [Week] [Month] Quarter    Discipline [All v]  Milestone [All v]             |
|                           [x] Hide completed   Range [2026-12-01 .. 2027-03-31]         [Print] (1)        |
+-----------------------------+-----------------+-----------------+-----------------+------------------------+
|                             | Dec 2026        | Jan 2027        | Feb 2027        | Mar 2027               |
|                             |                 |   ^ today 01-11 |                 |                        |
| MILESTONES                  |                 | (o)  <!>M03     |                 |    <>M04 03-12         |
|                             |                 | 01-08  01-15    |                 |                        |
| v Civil                     |                 |                 |                 |                        |
|   D009 Existing Conditions  | ======[Issued]  |                 |                 |                        |
|   D012 30% Civil Package    |        [////////|/--]  At Risk  |                 |                          |
|   D015 Grading Design       |                 |       [---------|-----------------|--]                     |
| v Geotechnical              |                 |                 |                 |                        |
|   D003 Geotech Report       | =====[Issued]   |                 |                 |                        |
|   D013 Pavement Recs.       |           [/////|XXX]  overdue  |                 |                          |
| v Electrical                |                 |                 |                 |                        |
|   D021 Elec. Utility Coord. |           [-----|XXX]  overdue  |                 |                          |
| > Project Management (2)    |                 |                 |                 |                        |
+-----------------------------+-----------------+-----------------+-----------------+------------------------+
| Legend: = issued/complete   / progress   - remaining   X overdue (hatched, extends to today)               |
|         <> milestone  (o) original date when slipped   M03 slipped +7 d from 01-08                         |
+------------------------------------------------------------------------------------------------------------+
```

**Notes**

1. Week, month, and quarter zoom; the today line always shows; labels never overlap (long ones truncate with a tooltip). **Print** uses print-friendly CSS.

- Deliverable bars run from start date (or creation date when there is no start) to due date, grouped by discipline (collapsible), with progress fill and status colour. Overdue bars extend to today with a hatched segment.
- Clicking any bar or diamond opens its detail panel.
- **First release (§36.4)** adds task bars under each deliverable, dependency arrows (highlighted when unsatisfied and overdue), drag to change dates with a confirmation dialog, ghost bars at baseline dates, and multiple project groups. Automatic scheduling, critical path, float, and levelling are out of scope.

### B10 Deliverables Register (§12.4, §13.6)

```text
+--------------------------------------------------------------------------------------------------------------+
| (project header and tabs, A2)                                                                                |
| DELIVERABLES                               [ + Deliverable ]  (PM, or DL in own discipline)   Export v       |
| [+ Filter]  [Status: not issued x]   Group [Discipline v]                                    Columns v       |
+--------------------------------------------------------------------------------------------------------------+
| Key         Deliverable                Owner     Mstone Due        Status        Progress       Indicators   |
| v CIVIL (Marc Dubois)                                                                                        |
| 1234-D009   Existing Conditions Plan   Marc D.   M03    Fri 12-18  (Issued)      11/11          Rev 0        |
| v 1234-D012 30% Civil Drawing Package  Marc D.   M03    Tue 01-12  (In Progress) [#####--] 6/11 [At Risk]    |
|    T0042    Update grading for 30%     Alex C.          Wed 01-13  (In Progress)                [Blocked]    |
|    T0043    Road profile 30%           Jill M.          Wed 01-13  (Not Started)                [Blocked]    |
|    ...  9 more tasks                                                    [ + Add task ]                       |
| 1234-D010   Prelim. Servicing Plan     Marc D.   M03    Fri 01-15  (In Review)   [#######] 7/7  Due soon     |
| v GEOTECHNICAL (Wei Zhang)                                                                                   |
| 1234-D013   Pavement Design Recs.      Wei Z.    M04    Wed 01-06  (In Progress) [###----] 3/5  [Overdue 5d] |
| v ELECTRICAL (Nadia Rahman)                                                                                  |
| 1234-D021   Elec. Utility Coordination Nadia R.  M05    Fri 01-08  (Not Started) -              [Overdue 3d] |
+--------------------------------------------------------------------------------------------------------------+
| 1 selected:  [Set milestone v]  [Shift due dates...]  [Set owner v]                                    (x)   |
+--------------------------------------------------------------------------------------------------------------+
```

**Deliverable detail panel**

```text
+-- 1234-D012 ---------------------------------------------------------- [Open full page]  [x]-------+
| 30% Civil Drawing Package                                                                          |
| (In Progress v)   [At Risk]  [Due tomorrow]                          [ Issue deliverable ]         |
|                                                                                                    |
|  Type        Drawing Package          Discipline   Civil                                           |
|  Owner       [ Marc Dubois v ]        Reviewer     [ Diane Roy v ]                                 |
|  Milestone   [ 1234-M03 30% Design Submission v ]    Due [ 2027-01-12 ]  (milestone 01-15)         |
|  Start       [ 2026-12-14 ]           Priority     [ High v ]      Requires review [x]             |
|  Revision    -                        Issued       -  to  -        Accepted  -                     |
|                                                                                                    |
|  PROGRESS  [#####-----] 55 %   6 of 11 tasks complete, 2 blocked, 1 overdue       (from tasks) (1) |
|  Estimated 96 h, remaining 41 h                                                                    |
|                                                                                                    |
|  [ Tasks (11) ]  Dependencies  Links  Comments  History                                            |
|  1234-T0042  Update grading for 30%     Alex C.   (In Progress)   Wed 01-13   [Blocked]            |
|  1234-T0043  Road profile 30%           Jill M.   (Not Started)   Wed 01-13   [Blocked]            |
|  1234-T0051  CAD QA 30% package         Alex C.   (Not Started)   Wed 01-13   [Waiting]            |
|  ...                                                                          [ + Add task ]       |
|                                                                                                    |
|  Dependencies tab (derived from task links, read-only):                                   (2)      |
|  Depends on  1234-D013 Pavement Design Recs. (Geotechnical)   via T0031 -> T0042, T0043            |
+----------------------------------------------------------------------------------------------------+
```

**Issue deliverable dialog**

```text
+-- Issue deliverable: 1234-D010 Preliminary Servicing Plan ----------------------------------------+
|                                                                                                   |
|  Issued date *     [ 2027-01-11 ]                                                                 |
|  Revision *        [ Rev A       ]    free text; conventions vary by client                       |
|  Issued to         [ City of Hamilton - transmittal T-014                  ]                      |
|  Transmittal link  [ https://company.sharepoint.com/.../T-014.pdf            ]                    |
|  Note              [                                                        ]                     |
|                                                                                                   |
|  ! 1 task is still open:  1234-T0058 File record copy (Alex Chen, Not Started)           (3)      |
|    [x] Issue anyway. The open task stays open and is flagged "Deliverable issued with task open". |
|                                                                                                   |
|                                                          [ Cancel ]   [ Issue deliverable ]       |
+---------------------------------------------------------------------------------------------------+
```

**Notes**

1. Progress is never typed on a deliverable: it is complete tasks over non-cancelled tasks, rounded down to 5 %, or weighted by hours when every task has an estimate (DL-07).
2. Deliverable-to-deliverable dependencies are derived from task dependencies in the MVP (D-18); explicit ones are Phase 2.
3. Issuing with open tasks needs confirmation and lists them (DL-05).

- Register columns also include **Type**, **Reviewer**, **Revision**, and **Issued date** (off-screen here). Grouping: Discipline (default), Milestone, Status, or Owner. Expanding a row shows its tasks inline.
- Status guards explain themselves: "Cannot set Ready to Issue: this deliverable requires review and has not been In Review" (DL-04).
- A deliverable that already has tasks cannot be deleted; the menu offers **Cancel** with a reason instead (DL-09). Returning an issued deliverable to Revision Required keeps its issued date and revision (E-24).

### B11 Milestones (§12.3, §13.7)

```text
+-------------------------------------------------------------------------------------------------------------+
| (project header and tabs, A2)                                                                               |
| MILESTONES                                [ + Milestone ] (PM)    Type [All v]  [ ] Show completed          |
|                                                                                                             |
|  2026-10       2026-11        2026-12        2027-01               2027-03    2027-05    2027-06            |
|  <v>M01        <v>M02                         ^ (o)  <!>M03          <>M04      <>M05      <>M06 ...        |
|  Kickoff       Field compl.            today 01-11  01-15         03-12      05-14      06-25               |
+-------------------------------------------------------------------------------------------------------------+
| Key        Milestone               Date       Original   Slip  Status       Days Deliv. Tasks under it      |
| v 1234-M03 30% Design Submission   2027-01-15 2027-01-08 +7 d  <!> At Risk  4    2/5    30/38, 1 ovd, 2 blk |
|       D009  Existing Conditions Plan   Marc D.   Fri 12-18   (Issued)          tasks 11/11                  |
|       D010  Prelim. Servicing Plan     Marc D.   Fri 01-15   (In Review)       tasks 7/7                    |
|       D012  30% Civil Drawing Pkg      Marc D.   Tue 01-12   (In Progress)     tasks 6/11, 1 ovd, 2 blk     |
|       D014  Prelim. Cost Estimate      Priya N.  Wed 01-13   (In Progress)     tasks 2/5                    |
|       D003  Geotech Report             Wei Z.    Fri 12-18   (Issued)          tasks 4/4                    |
| 1234-M04   60% Design Submission   2027-03-12 2027-03-12 -     <> On Track  60   0/7    3/38                |
| 1234-M02   Field Invest. Complete  2026-11-19 2026-11-19 -     <v> Complete -    3/3    21/21               |
+-------------------------------------------------------------------------------------------------------------+
```

**Change milestone date** (PM)

```text
+-- Change date: 1234-M04 60% Design Submission -------------------------------------------------+
|                                                                                                |
|  Current date 2027-03-12      New date * [ 2027-03-26 ]      Slip after change: +14 d          |
|  Reason *  [ Client extended the 30% review period by two weeks                        ]       |
|                                                                                                |
|  [x] Also shift the due dates of the 7 deliverables targeting this milestone by +14 d      (1) |
|      1234-D015  Grading Design                03-09 -> 03-23                                   |
|      1234-D016  Stormwater Management Report  03-09 -> 03-23                                   |
|      1234-D017  Road Design (Plan & Profile)  03-09 -> 03-23                                   |
|      ... 4 more                                                                                |
|                                                                                                |
|  The PM and all Discipline Leads will be notified. Each shifted date is logged.                |
|                                                              [ Cancel ]   [ Change date ]      |
+------------------------------------------------------------------------------------------------+
```

**Complete milestone** (PM)

```text
+-- Mark 1234-M03 30% Design Submission complete -----------------------------------------------+
|                                                                                               |
|  Completed date [ 2027-01-15 ]   (today by default; can be earlier, never later)              |
|                                                                                               |
|  ! 1 targeted deliverable is not Issued, Accepted, or Cancelled:                          (2) |
|      1234-D014 Preliminary Cost Estimate   (In Progress)   Priya Nair                         |
|    It will show "issued after milestone" when it is issued. This confirmation is logged.      |
|                                                                                               |
|  This milestone completes the phase "Preliminary Design".                                     |
|  Advance the project phase to "Detailed Design"?   (o) Yes   ( ) No, keep the current phase   |
|                                                                                               |
|                                                          [ Cancel ]   [ Mark complete ]       |
+-----------------------------------------------------------------------------------------------+
```

**Notes**

1. The optional cascade shifts targeted deliverables by the same number of days after a preview (M-04). Moving a milestone earlier flags deliverables that now fall after it (Date Inconsistent).
2. Completing with un-issued deliverables needs confirmation (M-05). Phase is only ever suggested, never advanced automatically. A milestone more than 7 days in the future cannot be completed until its date is moved (M-09).

- The status `Why?` popover names the rule that fired: "At Risk: 3 of 5 deliverables not issued with 4 days remaining; 1234-T0031 overdue" (C3).
- **Cancel milestone** keeps its links but removes it from evaluation; deliverables that targeted it are flagged "Milestone cancelled: retarget", and the PM gets a bulk retarget dialog (E-17).
- Filters: type, status, discipline, show completed. Sorted by date.

### B12 Decision Register (§12.9, §13.8)

```text
+--------------------------------------------------------------------------------------------------------------+
| (project header and tabs, A2)                                                                                |
| DECISIONS                                              [ + Raise decision ]                    Export v      |
| [Status: open x]  [Owner type: All v]  [Impact v]  [Required by v]  [ ] Blocking work only                   |
+--------------------------------------------------------------------------------------------------------------+
| Key        Subject                    Owner                Req. by   Days     Impact Status         Blocking |
| v DEC02    Confirm pavement structure EXT Client PM        Fri 01-08 3 d over High   (Pending)      2 tasks  |
|            for Dundurn St             Dundurn County Corp.                                                   |
|            Blocks 1234-T0042, 1234-T0044  |  Affects 1234-M03 30% Design Submission                          |
| DEC03      Approve closure staging    Priya Nair           Wed 01-20 in 9 d   Medium (Under Review) -        |
| DEC04      Streetlight standard       EXT City Transport.  Fri 02-05 in 25 d  Low    (Deferred)     -        |
| DEC01      Confirm survey limits      EXT Client PM        Tue 10-20 -        Low    (Decided)      -        |
+--------------------------------------------------------------------------------------------------------------+
```

**Raise decision**

```text
+-- Raise decision -----------------------------------------------------------------------------+
|  Subject *            [ Confirm pavement structure for Dundurn St                        ]    |
|  Description *        [ Options: A) 100 mm HL3 on 300 mm granular; B) full-depth ...     ]    |
|  Requested by *       [ Priya Nair v ]            Date requested [ 2026-12-18 ]               |
|  Owner *              (o) External party [ Client PM - Dundurn County Corp. v ] [+ New party] |
|                       ( ) Internal user  [                                v ]                 |
|  Required by *        [ 2027-01-08 ]                                                          |
|  Impact if delayed *  [ High v ]  [ Grading and pavement design for 30% cannot finish ]       |
|  Blocks tasks         [ 1234-T0042 x ] [ 1234-T0044 x ]  [ + link task ]   (blocked by)       |
|  Related items        [ 1234-M03 x ]  [ + link deliverable or milestone ]                     |
|                                                               [ Cancel ]   [ Raise ]          |
+-----------------------------------------------------------------------------------------------+
```

**Record decision** (owner or PM) and **Defer**

```text
+-- Record decision: 1234-DEC02 ---------------------+  +-- Defer: 1234-DEC02 ------------------+
|  Decision *  [ Option A: 100 mm HL3 on 300 mm   ]  |  |  New required-by date *  [ 01-15 ]    |
|              [ granular, per client email 01-14 ]  |  |  (must be later than today)           |
|  Decision date *  [ 2027-01-14 ]                   |  |  Reason *  [ Client committed on the  |
|  Decided by       [ Client PM - Dundurn County ]   |  |             call to decide by Fri ]   |
|  [x] Notify assignees of linked tasks (2)          |  |  Previous date 01-08 stays in history |
|                  [ Cancel ]  [ Record decision ]   |  |           [ Cancel ]  [ Defer ]       |
+----------------------------------------------------+  +---------------------------------------+
```

**Notes**

- External owners are marked `EXT` with their organisation. Nothing is ever sent to external parties in the MVP; the requester and the PM get the attention items and digest lines instead (DEC-06).
- An overdue decision blocks every task linked as "blocked by" (D-15) and fires attention rule A-04. A High-impact decision that is due soon also fires A-04 as a Warning.
- Sort: overdue first, then required-by date, then impact. The **Blocking** count opens the Task List filtered to tasks blocked by that decision.
- The register doubles as the agenda for the client call: "Decisions we need from you, with dates."
- The decision panel has the fields above plus Links, Comments, and History tabs. History shows every status and date change (Pending, Deferred with the old and new date, Decided).

### B13 Activity History (§13.14, §20)

```text
+--------------------------------------------------------------------------------------------------------------+
| (project header and tabs, A2)                                                                                |
| ACTIVITY    [Date range v] [Actor v] [Item type v] [Action v] [Discipline v]  [x] Important only  Export CSV |
+--------------------------------------------------------------------------------------------------------------+
| When              Actor        Action          Item                    Change                                |
| 2027-01-11 09:42  Diane Roy    Status          1234-T0061 Stormwater   In Review -> Revision Required (r2)   |
| 2027-01-11 09:10  Priya Nair   Due date        1234-T0060 Cost est.    2027-01-12 -> 2027-01-13              |
|                                                                        Reason: "Waiting on unit rates"       |
| 2027-01-11 00:05  System       Health override Project 1234            Override expired (was Yellow)         |
| 2027-01-08 16:20  Priya Nair   Deleted         1234-T0063 Dup. task    Snapshot: Omar, Not Started, 01-20    |
| 2027-01-08 11:02  Marc Dubois  Dependency      1234-T0042              Added: depends on 1234-T0031          |
| 2027-01-07 15:40  Priya Nair   Milestone date  1234-M03 30% Design     2027-01-08 -> 2027-01-15 (slip +7 d)  |
| 2027-01-07 15:40  System       Cascade (4)     M03 deliverables        Due +7 d, from M03 change [expand]    |
|                                                                    Source on hover: UI | API | Job           |
+--------------------------------------------------------------------------------------------------------------+
```

**Notes**

- Reverse chronological only. Every entry names the actor (or System), the item, and the field-level change with old and new values, plus the reason where one was required.
- "Important only" keeps status, assignment, date, deletion, decision, and health changes.
- Deletions show a snapshot of the deleted item and the dependencies removed with it (AC-AUD-02). System entries caused by a user action link to it through a correlation id.
- Nothing on this screen can be edited or deleted by anyone (AC-AUD-04). Every item panel has a **History** tab with the same list scoped to that item.

### B14 Notification Centre (§13.17, §17, §12.18)

Personal notifications and the Following feed are two tabs of the same centre.

```text
+------------------------------------------------------------------------------------------------------------+
| NOTIFICATIONS     [ Notifications (4) ]   Following (17)          [ ] Unread only  [Type v]  [Project v]   |
|                                                                   [ Mark all read ]   [ Preferences ]      |
+------------------------------------------------------------------------------------------------------------+
| TODAY                                                                                                      |
|  *  (assign)   Marc Dubois assigned you 1234-T0066 Traffic count review                 1234     09:55     |
|  *  (review)   Diane Roy requested revision on 1234-T0061 Stormwater concept (round 2)  1234     09:42     |
|  *  (unblock)  1301-T0005 is complete: 1301-T0007 Site grading sketch can start          1301     08:30    |
|  *  (mention)  Priya Nair mentioned you on 1234-D012 30% Civil Drawing Package          1234     08:05     |
| YESTERDAY                                                                                                  |
|     (blocked)  Your task 1234-T0042 is blocked by 1234-T0031 (overdue, Wei Zhang)       1234     Sun       |
|     (review)   You were set as reviewer on 1234-T0058 Utility conflict table           1234     Sun        |
+------------------------------------------------------------------------------------------------------------+
```

**Following tab**

```text
+------------------------------------------------------------------------------------------------------------+
| NOTIFICATIONS      Notifications (4)   [ Following (17) ]               [ ] Important only  [Project v]    |
+------------------------------------------------------------------------------------------------------------+
| v 1234 DCC Dundurn Roads      12 unread                                          [ Mark project as read ]  |
|   TODAY                                                                                                    |
|   09:42  Diane Roy    1234-T0061 status In Review -> Revision Required (r2)         notified   (1)         |
|   09:10  Priya Nair   1234-T0060 due 2027-01-12 -> 2027-01-13 "Waiting on unit rates"                      |
|   08:47  Priya Nair   Bulk: 6 due dates shifted +7 d on 1234-D015                    [ expand 6 ] (2)      |
|   08:15  Wei Zhang    1234-T0031 comment: "Lab results due Wednesday"                                      |
|   YESTERDAY ...                                                                                            |
| > 1187 Mall Parking Reno       5 unread                                                                    |
|                                                                                                    (3)     |
+------------------------------------------------------------------------------------------------------------+
```

**Preferences page**

```text
+------------------------------------------------------------------------------------------------------------+
| NOTIFICATION PREFERENCES                                                              [ Save changes ]     |
|  EVENT                                                                   IN-APP     EMAIL                  |
|  Task assigned or reassigned to you                                      [x]        [x]                    |
|  You were set as reviewer / review requested                             [x]        [x]                    |
|  Review outcome on your task                                             [x]        [x]                    |
|  Your task became blocked (digest)                                       [x]        digest                 |
|  Your task became unblocked                                              [x]        [x]                    |
|  Comment on an item you own or watch                                     [x]        [ ]                    |
|  @mention                                                                [x]        [x]                    |
|  ... every event in section 17.2                                                                           |
|                                                                                                            |
|  DAILY DIGEST   [x] On    Time [ 07:00 v ]    Weekends suppressed                                          |
|                                                                                                            |
|  PROJECT FOLLOW LEVELS                                                                            (4)      |
|  1234 DCC Dundurn Roads          [ All activity  v ]    set by assignment                                  |
|  1301 King St Watermain          [ My items only v ]    set by you                                         |
|  1187 Mall Parking Reno          [ All activity  v ]    set by assignment                                  |
|  [ + Follow another project ]                                                                              |
+------------------------------------------------------------------------------------------------------------+
```

**Notes**

1. An event that already produced a personal notification is marked "notified" and not counted again (ASG-06).
2. Changes made together (bulk actions, cascades, template set-up) collapse into one entry (ASG-05).
3. Only projects followed at **All activity** appear here. The feed is read from the activity log, filtered by what the user may see right now.
4. The system sets the level on assignment; any change by the user sticks (ASG-01, ASG-02). **Muted** keeps only direct assignments and @mentions.

- Updates never arrive as one email per change: they appear here and in the daily digest (E3). Several changes to one item by one person within 5 minutes collapse into one notification ("Marc updated 1234-T0042, 3 changes").
- Unread counts refresh every 60 seconds.

### B15 My Staff (§12.18, §13.19)

A manager's view of their direct reports, and where they staff them on projects.

```text
+------------------------------------------------------------------------------------------------------------+
| MY STAFF      Scope (o) My direct reports   ( ) All staff (Executives, Admins)    [ ] Show inactive (1)    |
| [Office v] [Discipline v] [Project v]  [Has overdue] [Has blocked] [Blocking others] [Reviews waiting]     |
+-----------------+---------------------+-----------------+-----------------+--------------------------------+
| STAFF           | PROJECT ASSIGNMENTS | WITH OVERDUE    | WITH BLOCKED    | REVIEWS WAITING > 5 D          |
| 3               | 13                  | 2               | 2               | 1                              |
+-----------------+---------------------+-----------------+-----------------+--------------------------------+
| Name            Title        Proj Roles               Open Ovd Blk Blkg  Revw Due14d Last activity         |
| v Alex Chen     EIT          3    Team 3              9    1   1   0     1    0      today 09:05           |
|      1234 DCC Dundurn Roads  [R]  Team, Civil  since 2026-10-05  open 6, overdue 1  next T0042 Wed         |
|      1301 King St Watermain  [Y]  Team, Civil  since 2026-11-12  open 2              next T0007 Fri        |
|      1187 Mall Parking Reno  [G]  Team, Civil  since 2026-08-30  open 1              next T0042 01-19      |
|      [ + Assign to project ]   [ Remove from project v ]   [ Open Alex's My Work ]           (2)           |
| > Jill Martin   Designer     2    Team 2              5    0   2   0     0    0      today 08:40           |
| > Marc Dubois   Sr. Engineer 8    PM 1, DL 6, Rev 2   21   4   0   2     3    3      today 09:31           |
+------------------------------------------------------------------------------------------------------------+
```

**Assign to project**

```text
+-- Assign Alex Chen to a project -------------------------------------------------------------+
|                                                                                              |
|  Project *              [ Search projects you can view ...              ]              (3)   |
|                           1340  Barton St Reconstruction   (Setup)    PM Priya Nair          |
|                           1320  Hwy 6 Culverts             (Setup)    PM Priya Nair          |
|                           1301  King St Watermain          already assigned                  |
|  Role                   Team Member   (other roles and Discipline Leads are set by the PM)   |
|  Primary discipline *   [ Civil v ]   (disciplines on the chosen project)                    |
|                                                                                              |
|  Alex will follow the project (All activity). Priya Nair (PM) will be notified.              |
|                                                              [ Cancel ]   [ Assign ]         |
+----------------------------------------------------------------------------------------------+
```

**Remove from project** (when the person owns open work)

```text
+-- Remove Alex Chen from 1301 King St Watermain ----------------------------------------------+
|                                                                                              |
|  Alex owns 2 open items on this project:                                                (4)  |
|    1301-T0007  Site grading sketch       (In Progress)   due Fri 01-15                       |
|    1301-T0009  Curb return profiles      (Not Started)   due Fri 01-22                       |
|                                                                                              |
|  ( ) Reassign both to  [ Jill Martin v ]   (members of this project)                         |
|  (o) Leave them assigned, flagged "Inactive on project"                                      |
|  Reason *  [ Moving Alex to 1340 for February                                        ]       |
|                                                                                              |
|  Marc Dubois (PM) will be notified with the list of affected items.                          |
|                                                              [ Cancel ]   [ Remove ]         |
+----------------------------------------------------------------------------------------------+
```

**Notes**

1. Supervisors see their direct reports only (Q19, decided). Executives and Admins can switch to All staff. "Show inactive" reveals leavers, whose open work is still flagged by A-18.
2. Supervisors can **Assign to project** and **Remove from project** for their direct reports, as Team Member only (ASG-10). Every count links to that person's My Work, filtered.
3. The picker lists projects the supervisor can view in Setup, Active, or On Hold. Restricted projects they cannot see, and their existing assignments, are absent from lists and counts (ASG-09, §36.1).
4. The same reassignment prompt the PM gets (TM-04). The PM is told who removed the person and which items were affected (E-29).

- Column key: **Proj** projects, **Ovd** overdue, **Blk** blocked, **Blking** blocking others, **Revw** reviews waiting on the person, **Due14d** owned deliverables due within 14 days. Default sort: overdue, then blocked, then name.
- Counts only, no hours on this screen. Hours and capacity appear in the first-release Resource View (B16), which reuses this scope.
- Phones show the table as a read-only card list; the staffing actions are desktop and tablet only.

### B16 Resource / Workload View, first release (§12.15, §13.11, §36.8)

```text
+------------------------------------------------------------------------------------------------------------+
| RESOURCES        Supervisor [Sam Okafor v]  Discipline [Civil v]  Office [All v]  8 weeks  Sort [Load v]   |
| Method (?): remaining = estimate x (1 - progress), spread Mon-Fri from max(start, today) to the due date.  |
|             Overdue work lands in the current week; work with no due date is counted separately.  Export v |
+------------------------------------------------------------------------------------------------------------+
| Person           W02     W03     W04     W05     W06     W07     W08     W09     Unest Ovd Flags           |
|                  01-11   01-18   01-25   02-01   02-08   02-15   02-22   03-01                             |
| v Alex Chen      56/40   44/40   30/40   20/40   12/40   8/40    0/40    0/40    2     1   OVER, CLUSTER   |
|                  140%### 110%##  75%#    50%     30%     20%     0%      0%                                |
|   1234 Dundurn   40 h    30 h    22 h    12 h    6 h     2 h                                               |
|     T0042 grade  14 h                                                                      due Wed         |
|   1301 King St   16 h    14 h    8 h     8 h     6 h     6 h                                               |
| > Jill Martin    10/40   8/40    6/40    4/40    0/40    0/40    0/40    0/40    0     0   UNDER           |
|                  25%     20%     15%     10%     0%      0%      0%      0%                                |
| > Marc Dubois    42/40   38/40   36/40   30/40   28/40   20/40   16/40   10/40   5     4   -               |
+------------------------------------------------------------------------------------------------------------+
```

- Cells show assigned hours over capacity, with heat shading (`#` marks here) and the number always visible. **Over-assigned**: above 110 % in the current or next week. **Under-assigned**: below 40 % for the next two weeks **and** no unestimated tasks. **Deadline cluster**: 3 or more tasks across 2 or more projects due within any 3-day window in the next 14 days.
- The unestimated count always sits next to the hours so nobody over-reads the numbers. Rows expand to Person, then Project, then Task.
- Actions: reassign a task (Supervisor for their staff, PM within the project) and set a person's capacity (Supervisor, Admin). Logged actual task hours appear in Time (§36.8) but do not change the workload calculation; no leave calendars or levelling.

### B17 Portfolio Dashboard, first release (§13.12, §36.6)

```text
+--------------------------------------------------------------------------------------------------------------+
| PORTFOLIO           [PM v] [Discipline v] [Client v] [Office v] [Status v] [Phase v] [Health v] [Type v]     |
|                     [ ] Submission within [14] days                                            Export v      |
+-----------------+-----------+-----------+-----------+-----------------------------+--------------------------+
| ACTIVE          | RED       | YELLOW    | GREEN     | SUBMISSIONS IN 14 D         | DECISIONS OVERDUE        |
| 47              | 6         | 11        | 30        | 9                           | 5                        |
+-----------------+-----------+-----------+-----------+-----------------------------+--------------------------+
| Project              PM       Health computed / reported Next submission    Ovd Blk DecO IssH Attn  Trend    |
| 1234 DCC Dundurn Rds Priya N. [R] Red / [Y] Yellow  Why? 30% Dsn Fri 01-15  7   3   1    0    3/6   __--^^^^ |
|                               Reported by PM 3 d ago                                                         |
| 1301 King St Watermn Marc D.  [Y] Yellow                 Tender Tue 02-02   2   0   0    0    0/2   ____--__ |
| 1187 Mall Parking Rn Priya N. [G] Green                  IFC Mon 03-01      0   0   0    0    0/0   ________ |
+--------------------------------------------------------------------------------------------------------------+
```

- When reported health differs from computed, both pills show with the PM's note and its age, so optimism is visible rather than hidden.
- Default sort: health severity, then next submission date. Rows open the project dashboard or its Weekly Coordination. The trend sparkline comes from daily health snapshots.

### B18 Risk Register, Issue Register, Meetings and Actions, Phase 2 (§12.10, §12.11, §13.13)

```text
+--------------------------------------------------------------------------------------------------------------+
| RISKS  P2                                                         [ + Raise risk ]   [Status: open x]        |
| Key      Risk                              Owner        P  I  Severity   Review by   Status       Links      |
| 1234-R03 Env. approval may delay fieldwork Chloe T.     3  2  [R] 6 High 01-08 !     (Open)       M04        |
| 1234-R05 Utility relocation may slip       Nadia R.     2  2  [Y] 4 Med  02-01       (Monitoring) D021       |
+--------------------------------------------------------------------------------------------------------------+
| ISSUES  P2                                                        [ + Raise issue ]  [Status: open x]        |
| Key      Issue                                 Owner        Severity  Target     Status                      |
| 1234-I04 Survey crew locked out of site        Omar H.      High      Wed 01-13  (In Progress)               |
+--------------------------------------------------------------------------------------------------------------+
| MEETINGS AND ACTIONS  P2                                          [ + Meeting ]                              |
| v Weekly Coordination - 2027-01-11   (Coordination)   Minutes: SharePoint link                               |
| Key      Action                            Owner                    Due        Status                        |
| 1234-A07 Chase client on pavement decision Priya Nair               Wed 01-13  (Open)      [Convert to task] |
| 1234-A08 Provide updated utility base      EXT Hydro One            Fri 01-22  (Open)                        |
| 1234-A09 Confirm staging with Transport.   Discipline: Civil (Marc) Fri 01-15  (In Progress)                 |
+--------------------------------------------------------------------------------------------------------------+
```

**Risk scoring (3 x 3)**

```text
+-- Probability x Impact ------------------------------+
|              Impact 1 Low   2 Medium   3 High        |
|  Prob 3 High      3 Med      6 High     9 High       |
|  Prob 2 Med       2 Low      4 Med      6 High       |
|  Prob 1 Low       1 Low      2 Low      3 Med        |
+------------------------------------------------------+
```

- Each level has a plain-language anchor, for example Impact High = "would move a submission milestone or require rework of an issued deliverable". There is no monetary value or Monte Carlo.
- A risk past its review date shows "Review overdue". Marking a risk Realised requires creating or linking an issue. High-severity open issues fire attention rule A-07 and turn project health Red.
- Actions owned by a discipline route to its lead's My Work. External-party actions send nothing outside and appear under "Waiting on client / external" in Weekly Coordination. **Convert to task** creates a linked task, and the action then follows that task's completion. Meeting mode can create actions against the current meeting.

### B19 Reports (§13.18, §19)

```text
+------------------------------------------------------------------------------------------------------------+
| REPORTS                                                                                                    |
| +-- CATALOGUE -------------------------------+ +-- OVERDUE TASKS ----------------------------------------+ |
| | Tasks Due This Week                        | | Projects [ 1234, 1301 v ]  Discipline [ All v ]         | |
| | > Overdue Tasks                            | | Assignee [ Any v ]   Min days overdue [ 1 ]  [ Run ]    | |
| | Blocked Tasks                              | |                                                         | |
| | Tasks Blocking Others                      | | Key         Task               Assignee  Days  Blocking | |
| | Upcoming Deliverables                      | | 1234-T0031  Pavement recs.     Wei Z.       5         2 | |
| | Deliverable Status by Project              | | 1234-T0039  Utilities overlay  Alex C.      3         0 | |
| | Upcoming Milestones                        | | 1301-T0011  Hydrant spacing    Jill M.      1         0 | |
| | Open Decisions                             | |                                                         | |
| | Review Queue                               | | 3 rows   [ Export CSV ] [ Export XLSX ]                 | |
| | Stale Work                                 | |          [ Open as filtered list ]                      | |
| | Project Activity Log                       | +---------------------------------------------------------+ |
| | Staff Assignments                          |                                                             |
| | P2: Projects At Risk, Open Issues / High   |                                                             |
| |     Risks, Meeting Actions Outstanding,    |                                                             |
| |     Workload by Employee / Discipline,     |                                                             |
| |     Health History, Attention Items        |                                                             |
| +--------------------------------------------+                                                             |
+------------------------------------------------------------------------------------------------------------+
```

- Every report is a fixed, rule-based query with a description, a parameter form, and a results table built from the same components as the lists. Reports respect permissions.
- XLSX exports have a header row, a frozen pane, typed dates, and a "Parameters" sheet recording the filters and generation time. CSV is UTF-8 with a BOM so Excel opens it cleanly. Exports cap at 50,000 rows and are logged (who exported what, and when).
- Not built: a custom report builder, pivot tables, or scheduled report emails.

### B20 Project Team and Settings (§12.2, §13.16)

**Team tab**

```text
+------------------------------------------------------------------------------------------------------------+
| (project header and tabs, A2)                                                                              |
| TEAM AND DISCIPLINES                                                           [ + Add discipline ] (PM)   |
| +-- DISCIPLINES ---------------------------------------------------------------------------------------+   |
| | Discipline         Lead                        Open  Overdue  Blocked                                |   |
| | Project Mgmt       [ Priya Nair        v ]        3        1        0                                |   |
| | Civil              [ Marc Dubois       v ]       14        4        3                                |   |
| | Geotechnical       [ Wei Zhang         v ]        3        1        0                                |   |
| | Electrical         [ Nadia Rahman      v ]        4        1        0                                |   |
| | Transportation     [ (no lead)         v ]  ! a lead is needed on an Active project                  |   |
| +------------------------------------------------------------------------------------------------------+   |
| +-- MEMBERS (14) -------------------------------------------------------------- [ + Add people ] ------+   |
| | Name           Roles                          Primary discipline   Added by            Follow level  |   |
| | Priya Nair     PM (primary)                   Project Mgmt         creator             All activity  |   |
| | Marc Dubois    DL Civil, Team                 Civil                Priya, 10-05        All activity  |   |
| | Alex Chen      [x] Team [ ] Reviewer [ ] View [ Civil v ]          Sam Okafor (supv.)  All activity  |   |
| | Diane Roy      Reviewer (auto-added)          -                    system, 12-20       My items only |   |
| | Jill Martin    [x] Team [ ] Reviewer [ ] View [ Civil v ]          Priya, 10-05        My items only |   |
| +------------------------------------------------------------------------------------------------------+   |
+------------------------------------------------------------------------------------------------------------+
```

- Only the PM changes the team, except that Supervisors may add or remove their own direct reports as Team Members (ASG-10); "Added by" shows who did it. The primary PM cannot be removed until the PM is changed (TM-03).
- Removing someone who owns open items opens the reassignment prompt (TM-04, as in B15). A discipline with non-cancelled work cannot be removed, only deactivated (TM-05).
- The follow level is shown for information only; each member controls their own.

**Settings tab** (PM)

```text
+------------------------------------------------------------------------------------------------------------+
| SETTINGS                                                                                                   |
|  Project information   Name, client, client reference, office, type, location, dates, phase   [ Edit ]     |
|  Important links       SharePoint site | Teams channel | \\fs01\projects\1234          [ Manage links ]    |
|  Coordination day      [ Monday v ]  drives the "this week" window in Weekly Coordination                  |
|  Visibility            (o) Open to all staff   ( ) Restricted to members   (shown only if enabled)         |
|  Status                (Active)   [ Put on hold... ]  [ Mark complete... ]                                 |
|  Health override       none       [ Set override... ]                                        (C16)         |
|  Archive               available once the project is Complete                                              |
|  DANGER ZONE           [ Cancel project... ]   Read-only afterwards; Admin can reverse.                    |
+------------------------------------------------------------------------------------------------------------+
```

**Status change with consequence**

```text
+-- Put 1234 DCC Dundurn Roads on hold -----------------------------------------------------------------+
|                                                                                                       |
|  Putting this project on hold stops overdue and attention evaluation for 42 open tasks, removes it    |
|  from digests, and turns its health Grey. Dates are kept; when you resume, a banner lists items       |
|  whose dates passed while it was on hold.                                                             |
|                                                                                                       |
|  Reason *  [ Client paused design pending council budget vote                                   ]     |
|                                                              [ Cancel ]   [ Put on hold ]             |
+-------------------------------------------------------------------------------------------------------+
```

**Closeout checklist** (Mark complete)

```text
+-- Mark 1234 DCC Dundurn Roads complete ---------------------------------------------------------------+
|                                                                                                       |
|  Open tasks (3)                        (o) Cancel all with reason   ( ) Leave open                    |
|  Deliverables not issued/accepted (1)  (o) Cancel all with reason   ( ) Leave open                    |
|  Open decisions (0)                                                                                   |
|  Open issues / risks / actions  P2     -                                                              |
|                                                                                                       |
|  Reason for completion *  [ Record drawings issued; closeout meeting held 2028-07-02            ]     |
|  The project stays editable by the PM for 30 days, then archiving is suggested.                       |
|                                                              [ Cancel ]   [ Mark complete ]           |
+-------------------------------------------------------------------------------------------------------+
```

- **Archive** (Complete projects only): read-only for everyone, hidden from default lists, searchable with "include archived", comments disabled. A full export (CSV per entity plus the activity log) is offered at archive time. An Admin can unarchive to correct records (E-11).

### B21 Administration (§13.15)

```text
+------------------------------------------------------------------------------------------------------------+
| ADMIN   [Users & roles]  Disciplines  Clients  Offices  Deliverable types  Phases  Project types           |
|         Settings  Templates P2                                                                             |
+------------------------------------------------------------------------------------------------------------+
| USERS & ROLES                          [ Search users ... ]   [ ] Show inactive   [ ] No supervisor only   |
| Name        System roles        Office     Supervisor   Status          Last sign-in Actions               |
| Sam Okafor  Supervisor (group)  Hamilton   Lena Brooks  Active          today        [Edit]                |
| Alex Chen   Standard            Hamilton   Sam Okafor   Active          today        [Edit]                |
| Tom Reid    Standard            Kitchener  (none)       Inactive (sync) 2026-12-02   [Reassign work]       |
| Jordan Lee  Admin (group)       Hamilton   Lena Brooks  Active          today        [Edit]                |
+------------------------------------------------------------------------------------------------------------+
```

**Reassign work** (leaver tool)

```text
+-- Reassign work: Tom Reid (Inactive) ---------------------------------------------------------------+
|  Tom owns 9 open items across 3 projects:                                                           |
|  [x] 1150-T0012  Retaining wall check      (In Progress)  Arena Expansion   -> [ Jill Martin v ]    |
|  [x] 1150-D004   Structural memo           (Not Started)  Arena Expansion   -> [ Marc Dubois v ]    |
|  [x] 1234-T0071  Survey control check      (reviewer)     DCC Dundurn Roads -> [ Omar Haddad v ]    |
|  ... 6 more                                                   Set all to [ ... v ]                  |
|  Each change is logged, and the new owners and PMs are notified.     [ Cancel ]  [ Reassign 9 ]     |
+-----------------------------------------------------------------------------------------------------+
```

**Reference data** (same pattern for disciplines, clients, offices, deliverable types, phases, project types)

```text
+------------------------------------------------------------------------------------------------------------+
| DISCIPLINES                                                                           [ + Add discipline ] |
|  Order  Name              Code   Colour    Active   Used by                                                |
|  1      Project Mgmt      PM     (slate)   [x]      61 projects                       [Edit]  [ :: drag ]  |
|  2      Survey            SUR    (teal)    [x]      44 projects                       [Edit]  [ :: drag ]  |
|  3      Civil             CIV    (blue)    [x]      58 projects                       [Edit]  [ :: drag ]  |
|  9      Architecture      ARC    (plum)    [ ]      2 projects   deactivated: hidden from pickers only     |
+------------------------------------------------------------------------------------------------------------+
```

**Settings**

```text
+------------------------------------------------------------------------------------------------------------+
| SETTINGS                                                                            [ Save ]  (logged)     |
|  THRESHOLDS                              VALUE    USED BY                                                  |
|  Task due soon (days)                    [  5 ]   Due Soon indicator, digest                               |
|  Deliverable due soon (days)             [ 10 ]   Due Soon, rule A-06                                      |
|  Milestone approaching (days)            [ 14 ]   Milestone At Risk, rule A-05                             |
|  Decision due soon (days)                [  5 ]   Decision Due Soon, digest                                |
|  Task stale (days)                       [ 10 ]   Stale indicator, rule A-10                               |
|  Review stale (days)                     [  5 ]   Stalled review, rule A-11                                |
|  Blocked before attention (days)         [  0 ]   Rule A-02                                                |
|  Health: overdue % yellow / red          [ 10 ] / [ 25 ]   minimum count [ 3 ]                             |
|  Health: blocked days for yellow         [  5 ]                                                            |
|  Health override expiry (days)           [ 14 ]                                                            |
|  Dependency chain depth limit            [ 10 ]                                                            |
|  Complete project edit window (days)     [ 30 ]                                                            |
|  Allow self-review                       [ ]                                                               |
|  ORGANISATION   Time zone [ America/Toronto v ]   Date format [ 2027-01-11 v ]                             |
|                 Project number format (regex) [ ^\d{4}(-\d{2})?$ ]   Digest time [ 07:00 ]                 |
|  ATTENTION RULES   A-01 [x]  A-02 [x]  A-03 [x] ... A-20 [x]   (severities are fixed in the MVP)           |
|  NOTIFICATION DEFAULTS for new users   [ Edit defaults ]                                                   |
+------------------------------------------------------------------------------------------------------------+
```

**Templates (P2)**

```text
+------------------------------------------------------------------------------------------------------------+
| TEMPLATE: Municipal Infrastructure Design    v3 (Published)       [ Preview instantiation ]  [ Retire ]    |
|  [Disciplines]  Milestones  Deliverables  Tasks  Dependencies                          [ New draft v4 ]    |
|  Discipline          Included by default                                                                   |
|  Project Mgmt        [x]                                                                                   |
|  Survey              [x]                                                                                   |
|  Environmental       [x]                                                                                   |
|  Structural          [ ]                                                                                   |
|  Editing a published template creates a draft; projects already created never change.                      |
+------------------------------------------------------------------------------------------------------------+
```

- Reference data is never hard-deleted once used: it is deactivated, which hides it from pickers but keeps existing records valid (E-18). Admin sees how many projects would be affected before deactivating.
- System roles come from Entra ID groups (source "group") with in-app additions (source "manual"). Supervisor links come from the Entra `manager` attribute where available, otherwise they are maintained here. The "No supervisor only" filter finds the gaps that would keep people out of My Staff (E-26).
- Every admin change is written to the activity log with the actor.

### B22 Global Search (§18)

```text
+------------------------------------------------------------------------------------------------------------+
| [ dundurn                                             ]                                                    |
| +-- PROJECTS ----------------------------------------------------------------------------------------+     |
| |  1234  DCC Dundurn Roads                 Dundurn County Corporation   (Active)   [R] Red           |     |
| +-- TASKS -------------------------------------------------------------------------------------------+     |
| |  1234-T0080  Dundurn St tree inventory   Survey      (Not Started)                                 |     |
| +-- DECISIONS ---------------------------------------------------------------------------------------+     |
| |  1234-DEC02  Confirm pavement structure for Dundurn St         (Pending)  overdue 3 d              |     |
| +-- PEOPLE ------------------------------------------------------------------------------------------+     |
| |  (no matches)                                                                                      |     |
| |  See all results >                                     [ ] Include archived projects               |     |
| +----------------------------------------------------------------------------------------------------+     |
+------------------------------------------------------------------------------------------------------------+
```

- Typing a key such as `1234-T0042` and pressing `Enter` opens that item directly (AC-SRCH-01).
- Results are grouped by type (top 5 each) and ranked: exact key, project-number prefix, name prefix, then text relevance, with active projects first. They only include what the user may see. Archived projects appear only with the toggle.
- **See all results** opens a results page with type tabs: Projects, Tasks, Deliverables, Milestones, Decisions, People (and the Phase 2 registers).
- The MVP searches numbers, names, keys, subjects, and people; searching descriptions and comments is Phase 2.

---

## C. Shared components and states

Built once in the design system and reused on every screen (§13.0, §23.4).

### C1 Filter bar and URL state

```text
+------------------------------------------------------------------------------------------------------------+
| Quick  [Mine] [Overdue] [Blocked] [Due this week] [Unassigned]                 Group [Deliverable v]       |
| [+ Filter v]  [Status: In Progress, Ready for Review x]  [Due: to 2027-01-17 x]  [Blocked x]   Clear       |
|               +-- Add filter ----------------+                                                             |
|               | Status        Assignee       |    URL: /projects/1234/tasks?status=In Progress,            |
|               | Reviewer      Discipline     |         Ready for Review&dueTo=2027-01-17&blocked=true      |
|               | Deliverable   Milestone      |                                                             |
|               | Priority      Due range      |                                                             |
|               | Start range   Indicators     |                                                             |
|               | Requires review              |                                                             |
|               | Has dependencies  Created by |                                                             |
|               +------------------------------+                                                             |
+------------------------------------------------------------------------------------------------------------+
```

- Filters combine with AND; several values within one field combine with OR. Active filters show as removable tokens. The filter state lives in the URL, so a copied link reproduces the view exactly (AC-SRCH-03). Saved views are first-release requirements under §36.7.

### C2 Health "Why?" popover (§16)

```text
+-- Why is 1234 Red? --------------------------------------------------------------+
|  Computed health: [R] Red                                                        |
|  RED because:                                                                    |
|   - Decision overdue and blocking work: 1234-DEC02 (3 d, blocks 2 tasks)         |
|  YELLOW contributors:                                                            |
|   - Milestone at risk: 30% Design Submission (4 d, 2/5 deliverables issued)      |
|   - Deliverables overdue: 1234-D013 (5 d), 1234-D021 (3 d)                       |
|   - Overdue tasks: 7 of 47 open (15 %; yellow from 10 % with at least 3)         |
|  Reported health: [Y] Yellow by Priya Nair, 3 d ago, expires in 11 d             |
|   "Client agreed a one-week extension to 30%; revised dates being entered."      |
|  Health summarises these indicators. It is not a prediction.                     |
+----------------------------------------------------------------------------------+
```

- Every input that is not zero is listed with its value and the colour it contributes. Management screens always show both computed and reported values when they differ.

### C3 Milestone status "Why?" popover (§16.2)

```text
+-- Why is 1234-M03 At Risk? ------------------------------------------------------+
|  4 days remaining (approaching threshold: 14 days)                               |
|   - 3 of 5 targeted deliverables are not issued: D010, D012, D014                |
|   - Tasks under targeted deliverables are overdue or blocked: T0039 (overdue),   |
|     T0042 and T0043 (blocked by T0031)                                           |
|  Rule: At Risk when a targeted deliverable is overdue, a task under it is        |
|  overdue or blocked, or it is within 14 days with unfinished deliverables.       |
+----------------------------------------------------------------------------------+
```

### C4 Blockers popover, opened from any Blocked chip (§12.6)

```text
+-- 1234-T0042 is blocked by ------------------------------------------------------+
|  TASK      1234-T0031 Issue pavement recommendations                             |
|            Wei Zhang  (In Progress)  due 2027-01-06  OVERDUE 5 d     [Open]      |
|  DECISION  1234-DEC02 Confirm pavement structure                                 |
|            Client PM, Dundurn County  required 2027-01-08  OVERDUE 3 d  [Open]   |
|  Affected milestone: 1234-M03 30% Design Submission, Fri 01-15                   |
+----------------------------------------------------------------------------------+
```

- A manual block appears in the same list with its type, reason, and "blocked for N days". A cancelled predecessor satisfies the dependency and shows the note "Predecessor cancelled".

### C5 Status transition menu (§12.5 T-10 to T-14)

```text
+-- (In Progress v) --------------------------------------------------+
|  -> Ready for Review         requires a reviewer; you will be asked |
|  -> On Hold...               reason required                        |
|  .. Complete                 not available: this task requires      |
|                              review (hover explains)                |
|  .. Cancelled                only the PM or Discipline Lead         |
+---------------------------------------------------------------------+
```

- Only transitions this user may make are active; the rest are greyed out with the reason on hover. The server returns `allowedTransitions` for each item, so the interface never duplicates the rules. Starting a blocked task asks "This task is blocked by 1234-T0031. Start anyway?"; it is a warning, not a lock.

### C6 Reason dialog (G-09)

```text
+-- Put 1234-T0070 Tree survey on hold ------------------------------+
|  Reason *  [ Waiting on arborist site access                    ]  |
|            at least 5 characters; stored in the activity log       |
|  The previous status is kept and restored when the hold ends.      |
|                                   [ Cancel ]   [ Put on hold ]     |
+--------------------------------------------------------------------+
```

- The same dialog appears for every change that needs a reason: project status changes, On Hold and Cancelled, reopening, milestone date changes, due-date changes by someone other than the PM or DL, health overrides, decision deferral or cancellation, PM comment deletion, and bulk date shifts.

### C7 Delete or cancel, with the consequence spelled out (§13.0, T-08, T-09)

```text
+-- Delete 1234-T0051 CAD QA 30% package? ---------------------------------------+
|  This task has comments and 2 dependencies:                                    |
|    depends on 1234-T0042;  blocks 1234-T0052                                   |
|  Deleting removes both dependencies and notifies 2 assignees and the PM.       |
|  Cancelling keeps the history and is usually better once work has started.     |
|                         [ Keep task ]   [ Cancel task... ]   [ Delete ]        |
+--------------------------------------------------------------------------------+
```

- Deletes are always soft: the item is logged with a snapshot, and an Admin or PM can restore it.

### C8 People picker

```text
+-- Assignee -----------------------------------------+
|  [ ali                                           ]  |
|   (AC) Alex Chen      EIT, Civil        Hamilton    |
|   (AL) Alice Lam      Technologist      Kitchener   |
|   Not on this project: assigning adds them as a     |
|   Team Member and notifies the PM (TM-06).          |
+-----------------------------------------------------+
```

- Searches active users by name or email and shows office and job title; inactive users are never offered (G-11). Project members are listed first.

### C9 Due-date change with a reason (T-16)

```text
+-- Change due date: 1234-T0039 ----------------------------------+
|  2027-01-08  ->  [ 2027-01-15 ]                                 |
|  Reason *  [ Utility records arrived late from the city       ] |
|  This is the 3rd change to this date. The assignee and          |
|  reviewer are notified; Priya Nair (PM) is notified.            |
|                                      [ Cancel ]   [ Save ]      |
+-----------------------------------------------------------------+
```

### C10 Edit conflict (§25.8)

```text
+-------------------------------------------------------------------------------------------------------------+
| !  This item was changed by Marc Dubois 2 minutes ago: Due date 2027-01-13 -> 2027-01-15.                   |
|    Your change (Priority -> Critical) was not saved.              [ Reload and compare ]   [ Discard mine ] |
+-------------------------------------------------------------------------------------------------------------+
```

- Every item carries a version; a save made against an old version is rejected with what changed and who changed it. There is no locking; saving each field on its own keeps collisions rare.

### C11 Toasts

```text
  +-- Saved -----------------------------------------------+
  |  Reassigned 1234-T0066 to Jill Martin.        [ Undo ] |   (10 seconds; skipped where it would
  +--------------------------------------------------------+    confuse notifications)
  +-- Bulk update ------------------------------------------+
  |  12 updated, 2 skipped (no permission).   [ Details ]   |
  +---------------------------------------------------------+
  +-- Saved with a warning ---------------------------------------------+
  |  Saved. 1234-T0042 is blocked by 1234-T0031; you started it anyway. |
  +---------------------------------------------------------------------+
```

### C12 Project banners (§13.1, G-05, E-05)

```text
+-------------------------------------------------------------------------------------------------------------+
| i  SETUP. This project is being set up: evaluation, digests, and attention items start when it is Active.   |
|    Checklist (advisory): [x] at least 1 milestone   [ ] every discipline has a lead (Transportation)        |
|    [ ] every submission milestone has a deliverable (Tender Close)                        [ Activate ]      |
+-------------------------------------------------------------------------------------------------------------+
| ||  ON HOLD since 2027-01-04: "Client paused design pending council budget vote". Nothing is counted as     |
|     overdue or blocked, and there are no digests, until the project resumes.               [ Resume ]       |
+-------------------------------------------------------------------------------------------------------------+
| i  DATE REVIEW. 9 items had dates pass while the project was on hold.   [ Review ]  [ Shift all by N days ] |
+-------------------------------------------------------------------------------------------------------------+
| v  COMPLETE since 2028-07-02. The PM can correct records for 28 more days; then archiving is suggested.     |
+-------------------------------------------------------------------------------------------------------------+
| #  ARCHIVED. Read-only for everyone. An Admin can unarchive to correct records.                             |
+-------------------------------------------------------------------------------------------------------------+
```

### C13 Empty and loading states

```text
+-- Blocked tasks -------------------------------------------------------------------------------------------+
|                                                                                                            |
|   No tasks are blocked.                                                                                    |
|   Blocked tasks appear here when a predecessor is incomplete, a linked decision is overdue,                |
|   or a manual block is set.                                                  [ Show all tasks ]            |
|                                                                                                            |
+-- Loading -------------------------------------------------------------------------------------------------+
|   ==========   ======================   ========   ============                                            |
|   ==========   ==================       ========   ============      (skeleton rows)                       |
|   ==========   ========================  ========  ============                                            |
+-- Error ---------------------------------------------------------------------------------------------------+
|   !  Could not load tasks. Check your connection.                                          [ Retry ]       |
+------------------------------------------------------------------------------------------------------------+
```

- Every empty list says what would appear there and offers the main action.

### C14 Permission explanations (§8.9)

```text
   [ Change date ]  (greyed out)
   +--------------------------------------------------------+
   | Only the Project Manager can change milestone dates.   |
   +--------------------------------------------------------+
```

- The interface hides or disables what a user cannot do and says why on hover. The server enforces the same rules and returns the same wording with a 403.

### C15 Snooze an attention item (§12.12)

```text
+-- Snooze: A-11 Review stalled on 1234-T0052 --------------------------+
|  Snooze for  [ 3 days v ]   (1-30)                                    |
|  Note *      [ Diane back from site visit Thursday; review Friday   ] |
|  It comes back when the snooze ends, or sooner if it becomes more     |
|  severe. The snooze is logged.                [ Cancel ]  [ Snooze ]  |
+-----------------------------------------------------------------------+
```

### C16 Health override (§16.4)

```text
+-- Override reported health: 1234 ----------------------------------------+
|  Computed [R] Red.   Report as  ( ) Green  (o) Yellow  ( ) Red           |
|  Note *  [ Client agreed a one-week extension to 30%; revised dates   ]  |
|          [ being entered.                                             ]  |
|  Expires automatically in 14 days. Management always sees both values.   |
|                                    [ Cancel ]   [ Set override ]         |
+--------------------------------------------------------------------------+
```

### C17 Set a manual block (D-14)

```text
+-- Set block: 1234-T0070 Tree survey --------------------------------------+
|  Blocked by   ( ) Client  ( ) External party  ( ) Internal                |
|               ( ) Decision  (o) Information  ( ) Other                    |
|  Reason *     [ Waiting on arborist access permit from the county      ]  |
|  The task shows as Blocked, with this reason, until the block is cleared. |
|                                         [ Cancel ]   [ Set block ]        |
+---------------------------------------------------------------------------+
```

### C18 Add a document link (§12.7)

```text
+-- Add document link -------------------------------------------------------+
|  URL or path *  [ \\fs01\projects\1234\civil\30pct                      ]  |
|  Title          [ 30pct ]              detected: Network folder            |
|  Type           [ Network folder v ]   SharePoint | OneDrive | Teams |     |
|                                        Network folder | External DMS       |
|  The Hub stores the link only, never the file.   [ Cancel ]  [ Add link ]  |
+----------------------------------------------------------------------------+
```

- Only http(s) links and UNC paths are accepted (DOC-01). Links on a deliverable also appear, read-only, on each of its tasks.

---

## D. Phone and tablet

Phones are for reading and quick updates (§13.0). Everything below fits a 375 px screen.

### D1 Phone: My Work

```text
+--------------------------------------+
| My Work                  (bell) 4    |
| NEEDS MY ATTENTION (2)               |
| +----------------------------------+ |
| | !! 1234-T0042                    | |
| | Update grading for 30%           | |
| | Blocked 4 d by T0031 (Wei)       | |
| +----------------------------------+ |
| [ My Tasks 7 ]  My Reviews 1         |
| OVERDUE                              |
| +----------------------------------+ |
| | 1234-T0039  Utilities overlay    | |
| | (In Progress) 80%  [Overdue 3d]  | |
| +----------------------------------+ |
| THIS WEEK                            |
| +----------------------------------+ |
| | 1234-T0042  Update grading 30%   | |
| | (In Progress) 60%  [Blocked]     | |
| | Due Wed 01-13                    | |
| +----------------------------------+ |
|  ...                                 |
+--------------------------------------+
| [My Work]     Search    Notifications|
+--------------------------------------+
```

### D2 Phone: quick update on a task

```text
+--------------------------------------+
| < Back               1234-T0042      |
| Update grading for 30%               |
| (In Progress v)  [Blocked]           |
| Due Wed 01-13 | Civil | Alex Chen    |
| +-- Blocked by ---------------------+|
| | T0031 Pavement recs. (Wei)        ||
| | overdue 5 d                       ||
| | DEC02 Pavement structure          ||
| | (Client) overdue 3 d              ||
| +-----------------------------------+|
| Progress                             |
| [ 0 10 20 30 40 50 [60] 70 80 90 100]|
| Status  [ Ready for Review v ]       |
| [ Add comment...               ]     |
|                       [ Post ]       |
+--------------------------------------+
```

- On phones the supported actions are: read, change status, update progress, comment, and view blockers.

### D3 Phone: notifications

```text
+--------------------------------------+
| Notifications                        |
| [ Notifications 4 ]  Following 17    |
| * Marc assigned you 1234-T0066       |
|   Traffic count review      09:55    |
| * Diane requested revision on        |
|   1234-T0061 (round 2)      09:42    |
| * 1301-T0005 complete: T0007 can     |
|   start                     08:30    |
|              [ Mark all read ]       |
+--------------------------------------+
```

### D4 Phone: search

```text
+--------------------------------------+
| [ 1234-t0042                   ] (x) |
| Opens the task directly on Enter.    |
| TASKS                                |
|  1234-T0042 Update grading for 30%   |
| PROJECTS                             |
|  1234 DCC Dundurn Roads              |
+--------------------------------------+
```

### D5 Phone: desktop-only notice

```text
+--------------------------------------+
| Milestones                           |
|                                      |
|  Editing milestones needs a tablet   |
|  or desktop. You can still view      |
|  them here.                          |
|                                      |
|  30% Design Submission  Fri 01-15    |
|  <!> At Risk    2/5 issued           |
+--------------------------------------+
```

- Desktop and tablet only: creating projects, editing milestones, templates, administration, My Staff staffing actions, Resource View, and Portfolio.

### D6 Tablet (768–1279 px)

- The rail collapses to icons and panels open as full-width overlays. Tables drop low-priority columns by default, which the column chooser can bring back. My Work works well at tablet width because most actions are status and progress changes. Weekly Coordination meeting mode works on a tablet connected to a projector.

---

## E. Emails (§17)

Plain and minimally branded, with the item key in the subject and deep links. No reply-by-email. Sent from one shared service mailbox.

### E1 Immediate: assignment

```text
From:     Coordination Hub <hub@company.example>
Subject:  [1234-T0066] Marc Dubois assigned you: Traffic count review

Marc Dubois assigned you a task on 1234 DCC Dundurn Roads.

  1234-T0066  Traffic count review
  Discipline: Civil      Due: Wed 2027-01-13      Priority: Medium
  Deliverable: 1234-D012 30% Civil Drawing Package

  Open in the Hub: https://hub.company.example/projects/1234/tasks/1234-T0066

You receive this because the task was assigned to you. Change email settings:
https://hub.company.example/me/preferences
```

### E2 Immediate: you can start

```text
Subject:  [1234-T0042] You can start: Update grading for 30%

1234-T0031 Issue pavement recommendations is complete (Wei Zhang, 2027-01-13).
Your task 1234-T0042 Update grading for 30% is no longer blocked. Due Wed 2027-01-13.

  Open: https://hub.company.example/projects/1234/tasks/1234-T0042
```

### E3 Daily digest, 07:00 (§17.3)

```text
Subject:  Hub digest - 1 overdue, 1 review, 1 blocked - 14 project updates

Good morning Alex. Monday 2027-01-11.

OVERDUE (1)
  1234-T0039  Existing utilities overlay        due Fri 01-08   3 d overdue

DUE IN THE NEXT 5 DAYS (4)
  1234-T0042  Update grading for 30%            Wed 01-13
  1234-T0061  Stormwater concept                Wed 01-13   revision required (r2)
  1234-T0051  CAD QA 30% package                Wed 01-13
  1301-T0007  Site grading sketch               Fri 01-15

BLOCKED (1)
  1234-T0042  blocked by 1234-T0031 (Wei Zhang, overdue 5 d) and 1234-DEC02 (client, overdue 3 d)

REVIEWS WAITING ON YOU (1)
  1234-T0058  Utility conflict table (Jill Martin)   waiting 3 d

MILESTONES APPROACHING IN YOUR PROJECTS
  Fri 01-15  1234-M03 30% Design Submission   At Risk

PROJECT UPDATES (projects you follow at All activity)
  1234 DCC Dundurn Roads: 12 changes - 6 status, 3 due date, 2 comments, 1 deliverable issued
    1234-D009 Existing Conditions Plan issued Rev 0 (Marc Dubois)
    1234-T0060 due 01-12 -> 01-13 (Priya Nair): "Waiting on unit rates"
    ... see all in the Following tab
  1187 Mall Parking Reno: 2 changes

  Open My Work: https://hub.company.example/me/work
```

- Sections appear only when they have content, each capped at 10 rows with "and n more". There is no digest when every section is empty, and weekend digests are off by default. Setup and On Hold projects are left out.
- A PM or Discipline Lead also gets **ATTENTION ITEMS** (Critical and Warning, per project). A Supervisor also gets **MY STAFF**, as below.

```text
MY STAFF (your direct reports)
  Alex Chen     1 overdue, 1 blocked
  Marc Dubois   4 overdue; 1 review waiting longer than 5 days
  Assignment changes made by others: Priya Nair removed Jill Martin from 1187 Mall Parking Reno
```

### E4 Other immediate emails (defaults from §17.2; users can switch each off)

| Event | Subject line |
|---|---|
| Set as reviewer / review requested | `[1234-T0052] Review requested: Technical review 30% package` |
| Review outcome | `[1234-T0061] Revision required (round 2): Stormwater concept` |
| Overdue task blocking others (first detection only) | `[1234-T0031] Overdue and blocking 2 tasks: Issue pavement recommendations` |
| @mention | `[1234-D012] Priya Nair mentioned you on 30% Civil Drawing Package` |
| Decision assigned to you | `[1234-DEC03] You own a decision: Approve road closure staging` |
| Decision recorded on a decision linked to your task | `[1234-DEC02] Decision recorded: Confirm pavement structure` |
| Added to a project or role changed | `[1340] You were added to Barton St Reconstruction as Team Member` |
| You became Discipline Lead | `[1340] You are now Civil Discipline Lead on Barton St Reconstruction` |

---

## F. Coverage checklist

Every screen, dialog, and flow in the specification, and where it is drawn here.

### F1 Screens (§13)

| Specification | Mockup |
|---|---|
| §13.0 Global UI conventions (layout, density, colour language, interaction, filters, sorting, grouping, empty states, loading and errors, keyboard, responsive, accessibility, time display) | A1–A4, C1, C11–C14, D1–D6, legend |
| §13.1 Project Dashboard | B4, C2, C12 |
| §13.2 Project List | B2 |
| §13.3 Task List | B6, C1 |
| §13.3.1 Task Detail Panel | B7, C4, C5, C9, C17, C18 |
| §13.4 Kanban Board | B8 |
| §13.5 Timeline | B9 |
| §13.6 Deliverables Register and §13.6.1 Deliverable Detail Panel | B10 |
| §13.7 Milestone View | B11, C3 |
| §13.8 Decision Register | B12 |
| §13.9 Weekly Coordination (with meeting mode, mark reviewed, copy summary, print) | B5 |
| §13.10 My Work (including the read-only view for Supervisors and PMs) | B1 |
| §13.11 Resource / Workload View (first release) | B16, six-view image |
| §13.12 Portfolio Dashboard (first release) | B17, six-view image |
| §13.13 Risk Register, Issue Register, Meetings and Actions (P2) | B18 |
| §13.14 Activity History | B13 |
| §13.15 Administration (users and roles, reference data, settings, templates, reassign work) | B21 |
| §13.16 Project Team and Settings | B20 |
| §13.17 Notification Centre and preferences (Notifications and Following tabs) | B14, D3 |
| §13.18 Reports | B19 |
| §13.19 My Staff (with Assign to project and Remove from project) | B15 |

### F2 Dialogs and states required by the modules and rules

| Specification | Mockup |
|---|---|
| Create project, two steps (§12.1); copy structure (§27.1); template wizard (§12.14, P2) | B3 |
| Project status change with consequence and reason (P-03); closeout checklist (P-04); archive and unarchive (P-06, E-11); cancel project | B20, C12 |
| Health override with expiry (§16.4) | C16, C2 |
| Milestone date change with cascade preview (M-02 to M-04); complete with phase suggestion (M-05 to M-09); cancel milestone (M-07) | B11 |
| Issue deliverable, with open-task confirmation (DL-05, DL-06); status guard messages (DL-04) | B10 |
| Task transitions (T-10 to T-14), review approve and request revision (R-01 to R-05) | B7, C5 |
| Reasons for changes (G-09); due-date change (T-16) | C6, C9 |
| Dependencies: add with cycle prevention (D-03), chain view (FR-DEP-07), blockers (D-05 to D-15) | B7, C4 |
| Manual block (D-14) | C17 |
| Raise, record, defer decision (DEC-01 to DEC-07) | B12 |
| Attention items, ranking, snooze (§12.12, ATT-01 to ATT-05) | B4, B5, C15 |
| Comments with @mentions, edit window, deletion placeholder (§12.8) | B7 |
| Document links, UNC copy path (§12.7) | A2, B7, C18 |
| Bulk actions with per-row results (T-23, E-20) | B6, B10, C11 |
| Delete versus cancel, soft delete (T-08, T-09, G-06) | C7 |
| Edit conflicts (G-07, §25.8) | C10 |
| Setup, On Hold, date review, Complete, Archived banners (G-05, E-05) | C12 |
| Follow levels and the Following feed (§12.18 ASG-01 to ASG-07) | A2, B1, B14, B20 |
| Manager staffing and staff visibility (§12.18 ASG-08 to ASG-11) | B15, B20, E3 |
| Reassign work for leavers (FR-ADM-02, E-01) | B21 |
| Global search and key lookup (§18) | B22, D4 |
| Emails: immediate and daily digest (§17) | E1–E4 |

### F3 Workflows (§14)

| Workflow | Screens, in order |
|---|---|
| 1. PM creates a project without a template | B3 (steps 1–2), then B4 with the Setup banner (C12), B11 to add milestones, B10, B6 |
| 2. PM creates a project from a template (P2) | B3 template wizard, then B4 |
| 3. Discipline Lead creates deliverables | B10 (register, create, inline tasks), B7 for dependencies |
| 4. PM assigns a task | B6 (`c` or `+ Task`), B7, C8 |
| 5. Employee updates assigned work | B1, B7 (status, progress, comment, block), C5, C17 |
| 6. Task becomes blocked by another task | B4 attention, B5 section 6, B7 blockers, C4, E2 when it clears |
| 7. Task submitted for review | B7, B1 My Reviews |
| 8. Reviewer requests revisions | B7 reviewer view (Request revision), B14 notification |
| 9. PM runs the weekly coordination meeting | B5 normal view, then meeting mode, then Copy summary |
| 10. Supervisor reviews staff workload (first release) | B16, then B15 and B1 read-only view |
| 11. Decision becomes overdue and blocks work | B12, B4 attention, C4, B5 section 5, Defer and Record dialogs |
| 12. Project reaches a design submission milestone | B11, B10 Issue dialog, B11 Complete with phase suggestion |
| 13. Project is closed and archived | B20 closeout checklist, C12 Complete and Archived banners |
| 14. Manager assigns a direct report to a project | B15 Assign to project, B14 PM notification, B1 follow level, B15 Remove from project |

---

## G. Six-view first-release image coverage

The user confirmed the six view types and task-hour entry for the first release. The [six-panel image](docs/reference/six-view-workspace.png) records those functional cues, while [Coordination Hub V2](docs/reference/coordination-hub-v2/README.md) is the only UI prototype to consult for visual inspiration. Neither is an approved design. The product specification §36 defines behavior and resolves differences from the older wireframes above.

| Image panel | Functional cues captured in §36 | Requirement and acceptance |
|---|---|---|
| 1. Modern project board | Left navigation, search and quick-create, project view tabs, Active and Upcoming/Planning groups with counts, owner, status, priority, due, progress, selection/filter/group controls | §36.1–§36.2; FR-VIS-01–03; AC-VIS-01, AC-VIS-07 |
| 2. Kanban | Four status lanes with counts, project-labelled cards, assignee and due date, review/indicator detail, add task in lane | §36.3; FR-VIS-04; AC-VIS-02 |
| 3. Timeline/Gantt | List/Board/Timeline/Calendar/Workload switcher, project/task hierarchy, dated bars, dependency arrows, milestones, today, zoom/date navigation | §36.4; FR-VIS-05; AC-VIS-03 |
| 4. Team calendar | Week/Month/Agenda, date navigation, four type toggles, timed events and all-day deadlines, New Event | §36.5; FR-VIS-06; AC-VIS-04; packet 023 |
| 5. Overview dashboard | Five metric tiles, tasks by status/project charts, upcoming deadlines, date range, Edit Dashboard | §36.6; FR-VIS-07; AC-VIS-05 |
| 6. My Work | Inbox, Today/Upcoming/Overdue/Completed, My Tasks/Assigned to Me/Created by Me, task/project/due/priority rows, Add Task | §36.7; FR-VIS-08; AC-VIS-06 |
| Shared Files, Workload, and Time links | Working destinations for visible navigation; Files is a document-link library, Workload is the resource grid, and Time records actual task hours. | §36.8; FR-VIS-09–10; AC-VIS-07–08; packet 024 |

### G1 Time route (not pictured beyond navigation)

The user confirmed that Time means task-hour entry. Its first-release screen has an Add Time control, Today/This week totals, and rows for work date, project, task, hours, note, and owner. Filters and export respect §36.8. The image does not specify a timer, billable rate, approval, or payroll submission; those are not inferred from the menu label.
