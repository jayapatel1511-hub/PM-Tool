# Verification and Acceptance Plan: Staffing Signals and Trustworthy Counts

**State**: Full acceptance plan written 2026-10-11, before any build. Nothing has run; every row starts UNRUN.

**Evidence rule**: PASS means the evidence column names a test run, script output or screenshot from a stated commit. A ticked task, an agent's summary or "looks right" is not evidence.

## 1. Definition of accepted

Packet 036 is accepted only when **all** of these hold for one release commit:

1. Every row in section 6 (acceptance criteria) is PASS.
2. SC-001 to SC-005 in section 5 are PASS. SC-001 is run by real people, not agents.
3. Every row in section 9 (regression guard) is PASS: packet 029 and 034 behaviour is unchanged.
4. No open S1 or S2 defects (section 12). Any deferred S3 defect is listed and approved by Jay.
5. The wording check finds no third-party product names in the packet diff, and the packet merges into the base with no conflicts.
6. Jay signs off in section 13.

A newer release commit voids earlier PASS rows, except rows whose files the diff shows are untouched. Section 12 sets the re-test rule.

## 2. Entry criteria (before any acceptance run)

| # | Condition | Status |
|---|---|---|
| E1 | D1–D9 answered and recorded in section 3. | UNRUN |
| E2 | T003 done: `codex/tuesday-ui-consistency` merged or recorded as abandoned, the pending `Planning.cs` edit committed, and the base commit recorded here. | UNRUN |
| E3 | T004–T019 complete on the packet branch, and the build is green. | UNRUN |
| E4 | Section 8 fixtures load on a fresh local preview database with zero refused steps. | UNRUN |
| E5 | Human session participants booked (section 10), with dates written here. | UNRUN |

## 3. Decisions

| # | Decision | Jay's answer | Date |
|---|---|---|---|
| D1 | Fill through allocation proposal | Pending | |
| D2 | Open role editors = allocation proposers | Pending | |
| D3 | Open roles on the Allocations tab and Workload only | Pending | |
| D4 | `StartedAt` anchors Start-to-Start lag | Pending | |
| D5 | Finish-to-Finish never waits or blocks | Pending | |
| D6 | A-21 at 15 days | Pending | |
| D7 | Home discipline edited by an Admin | Pending | |
| D8 | Picker capacity only within existing scope | Pending | |
| D9 | Home lane renamed "Review and revision" | Pending | |

A different answer to any decision updates spec.md, this plan and the affected tables before the build starts.

## 4. Environments and people

**Environments**

| Environment | Use | How |
|---|---|---|
| Unit and API | Domain and API tests | `dotnet test tests/Hub.Tests/Hub.Tests.csproj --no-restore`, using the existing PostgreSQL test setup |
| Local preview | Browser checks, SC-002 to SC-004, keyboard and VoiceOver | Isolated `pm-tuesday-preview-db` (127.0.0.1:55433) with development authentication. Load fixtures with the `tools/preview/` seed scripts plus `seed_staffing.py` (T020). |
| Review stack | SC-001, the dependency review, Jay's visual review | `pm.engcalchub.com/?review=<sha>`, review-only database, ten individual accounts. Deploy only after Jay's go-ahead, following the Personal Server agent guide and the hosting runbook. |

**Personas** (existing fixture accounts)

| Persona | System role | Reports to | Project roles |
|---|---|---|---|
| Jordan Lee | Admin | — | — |
| Lena Brooks | Executive, Supervisor | — | — |
| Sam Patel | Supervisor | Lena | — (supervises Marc, Alex, Jill) |
| Priya Nair | Project Manager | Lena | PM of SYN-101 and SYN-104 |
| Marc Dubois | Project Manager, Supervisor | Sam | PM of SYN-102 and SYN-103; CIV lead on SYN-101 |
| Alex Chen | — | Sam | Team member on SYN-101 and SYN-102 |
| Jill Martin | — | Sam | Team member on SYN-101, SYN-102 and SYN-103 (restricted) |
| Diane Roy | — | Lena | Reviewer on SYN-101 |
| Omar Haddad | — | Lena | GEO lead and member on SYN-101 |
| Rita Gomez | Read Only | — | — |

## 5. Success criteria

| ID | Criterion | Method | Pass threshold | Status |
|---|---|---|---|---|
| SC-001 | A PM staffs a real-shaped proposal with open roles and fills one through the normal flow. | Session S1 (section 10) on the review stack. | Under 15 minutes; no facilitator help; both participants confirm the hours and weeks shown are right. | UNRUN |
| SC-002 | Every dashboard count equals its list. | `npm --prefix web run test:counts` (section 7). | 0 mismatches across 3 personas × all tiles; script exit code 0. | UNRUN |
| SC-003 | Dependency flags are correct by type. | `DependencyLagTests` encode section 8.2 exactly, plus a browser pass on the SYN-105 Timeline. | Every cell in the section 8.2 table matches on all 3 dates. | UNRUN |
| SC-004 | No out-of-scope leaks. | Section 6.8 privacy matrix (API and browser). | 0 cells where a persona sees a name, hours, count or existence they should not. | UNRUN |
| SC-005 | Nothing already built changes. | Section 9. | All rows PASS. | UNRUN |

## 6. Acceptance criteria: test cases

Each row lists its proof type: **A** = automated test (file and test name filled in when written), **B** = agent browser check with a screenshot, **H** = human session.

### 6.1 Open roles (FR-ROLE)

| ID | Case | Steps and data | Expected | Proof | Status | Evidence |
|---|---|---|---|---|---|---|
| AC-ROLE-01a | Valid create | Priya creates "Civil technologist" on SYN-104, from Mon of week+2 through Fri of week+5, 80 h. | 201; Status Open; row version set; activity row "Created" with the label, dates and hours. | A | UNRUN | |
| AC-ROLE-01b | Validation | Empty label; 81-character label; through before from; 0 h; −5 h. | 400 with field errors `roleLabel`, `throughDate`, `plannedHours`; nothing written. | A | UNRUN | |
| AC-ROLE-01c | Permissions | Create as each persona on SYN-101. | Allowed: Priya (PM), Marc (CIV lead), Omar (GEO lead). Refused 403: Jordan, Lena, Sam, Alex, Jill, Diane, Rita (same result as allocation proposals). | A | UNRUN | |
| AC-ROLE-01d | Stale edit | Two PATCHes with the same row version. | First 200; second 409 with the current version; no partial write. | A | UNRUN | |
| AC-ROLE-02a | Workload spread | Role of 80 h over 4 full working weeks (20 working days, no holidays). | Unfilled roles shows 20 h in each of the 4 weeks and 0 elsewhere. | A, B | UNRUN | |
| AC-ROLE-02b | Holiday in range | Same role, with an org holiday on Wed of week+3 (19 working days). | Exact 80/19 h per working day. Weeks +2, +4 and +5 show 21.1 h and week +3 shows 16.8 h at one decimal; the exact stored sum is 80 h (last-day remainder rule). | A | UNRUN | |
| AC-ROLE-02c | No person impact | Compare the `/workload` person rows before and after creating the role. | Identical JSON; no indicator changes. | A | UNRUN | |
| AC-ROLE-03a | Fill | Priya opens Fill on the role, picks Jill, links one of Jill's SYN-104 tasks, submits. | One Proposed allocation with `openRoleId`; the role is Filled with `FilledAllocationId` set, in the same transaction (activity rows share one correlation id). | A, B | UNRUN | |
| AC-ROLE-03b | Fill refused when filled | A second proposal with the same `openRoleId`. | 409 `openRole.filled`; no allocation created. | A | UNRUN | |
| AC-ROLE-03c | Concurrent fill | Two proposals for the same Open role sent in parallel. | Exactly one 201 and one 409; one allocation exists. | A | UNRUN | |
| AC-ROLE-03d | Range check | Fill with an allocation range outside the role's range, or on another project. | 400; the role stays Open. | A | UNRUN | |
| AC-ROLE-04a | Decline | Sam declines Jill's proposed allocation. | The role returns to Open; `FilledAllocationId` is cleared; activity is recorded. | A | UNRUN | |
| AC-ROLE-04b | Cancel | Priya cancels the proposed allocation. | The role returns to Open. | A | UNRUN | |
| AC-ROLE-04c | Confirm and complete | Sam confirms; the allocation is later completed. | The role stays Filled throughout. | A | UNRUN | |
| AC-ROLE-05 | Privacy | See section 6.8 rows R1–R4. | As stated there. | A, B | UNRUN | |

### 6.2 Dependency types (FR-DEPT)

| ID | Case | Steps and data | Expected | Proof | Status | Evidence |
|---|---|---|---|---|---|---|
| AC-DEPT-01a | Default and existing rows | After migration, read every existing task and deliverable link. | Type = Finish-to-Start for all; evaluator output on the fixture equals the base output (section 9). | A | UNRUN | |
| AC-DEPT-01b | Add each type | Add a link with each type, through the API and the dialog. | Stored type matches; the list chip shows "FS", "SS +2d", "FF" and so on. | A, B | UNRUN | |
| AC-DEPT-01c | Invalid type | Type "StartToFinish" or "xyz". | 400 `dep.type`. | A | UNRUN | |
| AC-DEPT-01d | One link per pair | An SS link exists for (P, S); add an FF link for (P, S). | 409 duplicate; the existing link is unchanged. | A | UNRUN | |
| AC-DEPT-02 to -04 | Rules by type | Section 8.2 table, links L1–L6 on dates A, B and C. | Every cell matches. | A, B | UNRUN | |
| AC-DEPT-02b | Deliverable links | Repeat L2 and L4 with deliverables (Issued counts as finish; `StartedAt` = left Not Started). | Same Waiting, Blocked and date results as L2 and L4. | A | UNRUN | |
| AC-DEPT-05a | One satisfaction function | On SYN-105 at date B, compare the evaluator, `GET /tasks/{id}/dependencies` `satisfied`, the deliverable dependency payload `satisfied`, and Readiness predecessor results. | All four agree for every link. | A | UNRUN | |
| AC-DEPT-05b | No client rule | `git grep -n "Issued\|Accepted" web/src/pages/projects/Deliverables.tsx` around ExplicitDeps. | No status-based satisfaction left in the client. | A | UNRUN | |
| AC-DEPT-05c | Readiness | S3 (SS) with P3 started; S5 (FF) with P5 incomplete. | S3's predecessor check passes; S5 is not held back by its FF link. | A | UNRUN | |
| AC-DEPT-06 | Arrows | SYN-105 Timeline and Gantt. | FS end → start, SS start → start, FF end → end; the tooltip names the type and lag; the screenshot shows all three. | B | UNRUN | |
| AC-DEPT-07 | Copy and templates | Copy SYN-105's structure; instantiate a template with dependencies. | The copy keeps each type and lag; template links are Finish-to-Start. | A | UNRUN | |

### 6.3 Status not moving, A-21 (FR-STALE)

| ID | Case | Data | Expected | Proof | Status | Evidence |
|---|---|---|---|---|---|---|
| AC-STALE-01 | Fires | Task T21a: In Progress; status changed 16 days ago (working days when the calendar is on); a comment yesterday. | One A-21 item; reason includes "16 days" and threshold 15; severity Info (Warning if due within 5 days). | A | UNRUN | |
| AC-STALE-02 | A-10 wins | Task T21b: In Progress; no activity at all for 16 days. | A-10 only; no A-21. | A | UNRUN | |
| AC-STALE-02b | A-11 wins | Task in review, review requested 16 days ago, a comment yesterday. | A-11 only; no A-21. | A | UNRUN | |
| AC-STALE-02c | Deliverable | Deliverable D21: In Review; status changed 16 days ago; edited yesterday. | One A-21 item for the deliverable. | A | UNRUN | |
| AC-STALE-02d | Boundary | Status changed exactly 15 days ago. | No A-21 (strictly greater than the threshold). | A | UNRUN | |
| AC-STALE-03a | Toggle | `rule_enabled.A-21` = false. | No A-21 items; existing A-21 items are removed on the next evaluation. | A | UNRUN | |
| AC-STALE-03b | Setting range | `status_stale_days` = 0, 1, 365, 366. | 0 and 366 refused; 1 and 365 saved; the Admin screen shows the setting with its label. | A, B | UNRUN | |
| AC-STALE-04 | Snooze | Snooze T21a's A-21 item. | Hidden until the snooze ends, using the existing snooze rule. | A | UNRUN | |

### 6.4 Capacity grouping (FR-GRP)

| ID | Case | Data | Expected | Proof | Status | Evidence |
|---|---|---|---|---|---|---|
| AC-GRP-01a | Edit | Jordan sets Alex's home discipline to CIV. | 200; the user history shows old → new; non-Admins get 403. | A, B | UNRUN | |
| AC-GRP-01b | Invalid | An unknown discipline id. | 400 `error.not_found`. | A | UNRUN | |
| AC-GRP-02a | Group options | Workload and Planner as Lena; group by each of Reports to, Office, Job title, Home discipline. | Rows are grouped; the URL keeps `groupBy`; reload restores it. | B | UNRUN | |
| AC-GRP-02b | Not set | Diane has no home discipline. | Diane is listed under "Not set" when grouping by discipline; she is never hidden. | B | UNRUN | |
| AC-GRP-02c | Subtotals | Any group. | The group subtotal equals the sum of its rows for every week (checked in the DOM). | B | UNRUN | |

### 6.5 Capacity in the picker (FR-PICK)

Fixture for week W (section 8.4):
- **Alex**: 40 h capacity, a Confirmed SYN-101 allocation of 20 h in W, and a Published Expected planning entry of 8 h.
- **Jill**: 40 h capacity, time away Mon–Tue of W (0 h overrides), nothing else.

| ID | Case | Viewer and place | Expected | Proof | Status | Evidence |
|---|---|---|---|---|---|---|
| AC-PICK-01a | Figures | Sam, owner picker on a SYN-101 task starting in W. | Alex "12 h free"; Jill "24 h free · 16 h away". | A, B | UNRUN | |
| AC-PICK-01b | Week choice | Task with no start date and due in W+1. | Figures are for W+1. Task with no dates: the current week. | A | UNRUN | |
| AC-PICK-02a | Scope (Supervisor) | Sam on the same picker. | Omar and Diane rows show no figure (they report to Lena); the API response has no entries for them. | A, B | UNRUN | |
| AC-PICK-02b | Scope (PM) | Priya on the SYN-101 picker. | Figures for SYN-101 members only. | A | UNRUN | |
| AC-PICK-02c | No permission | Alex (no workload permission) opens the picker. | No figures; no `planning/grid` request is made. | A, B | UNRUN | |
| AC-PICK-02d | Private drafts | Jordan (Admin) has a private draft of 10 h for Alex in W; Sam opens the picker. | Alex still shows "12 h free" (the draft is excluded). | A | UNRUN | |
| AC-PICK-03a | Same number | Compare the picker figure with `GET /planning/cells/{alex}/{W}` `remaining`. | Equal. | A | UNRUN | |
| AC-PICK-03b | One request | Open the picker with 30 candidates; check the network panel. | Exactly one `planning/grid?personIds=` request per open; no request per row. | B | UNRUN | |
| AC-PICK-03c | Limit | `personIds` with 51 ids. | 400. | A | UNRUN | |

### 6.6 Count reconciliation (FR-CNT)

| ID | Case | Expected | Proof | Status | Evidence |
|---|---|---|---|---|---|
| AC-CNT-01 | Every tile = its list | SC-002 script: 0 mismatches. Portfolio R, Y and G tiles link `computedHealth=X&status=Active`. | A | UNRUN | |
| AC-CNT-02 | Discipline Open | SYN-101 CIV row: Open count = rows behind its link, with the On Hold task present in the fixture. | A | UNRUN | |
| AC-CNT-03 | Unassigned deliverables | The Issued, unowned deliverable in SYN-101 is not in `?indicator=unassigned`; the open, unowned one is. | A | UNRUN | |
| AC-CNT-04 | Upcoming Deadlines | API test with 150 deliverables due within range: the 101 soonest are returned in date order. | A | UNRUN | |
| AC-CNT-05 | On Hold accounted | Home: sum of the non-Done lanes + On hold = Open Tasks for Jordan, Priya and Sam; the On hold link opens exactly those tasks. | A, B | UNRUN | |
| AC-CNT-06 | Definitions | Every tile on Home, Dashboard and Portfolio has a hint; it is reachable by Tab and announced by VoiceOver; axe shows no serious or critical issues. | B, H | UNRUN | |
| AC-CNT-07 | Discipline scope note | Dashboard with CIV selected: decision, issue and risk tiles show "Whole project". | B | UNRUN | |
| AC-CNT-08 | Review lane label | Home shows "Review and revision"; its definition names the three statuses it includes. | B | UNRUN | |

### 6.7 Timeline date source (FR-TL)

| ID | Case | Data | Expected | Proof | Status | Evidence |
|---|---|---|---|---|---|---|
| AC-TL-01 | Assumed start | SYN-105 task "No start date task", created 10 days ago, due in 10 days. | The bar's first segment (created → today) is faded; the tooltip and aria text include "start not set"; the legend shows "Bar: start → due" and "Start not set". | B | UNRUN | |
| AC-TL-02 | No silent drop | A dated task under an undated deliverable. | It appears as a top-level row on the project Timeline and on Gantt. | B | UNRUN | |
| AC-TL-03 | Undated milestone | Milestone "Client workshop", no date. | Listed under Unscheduled on both pages. | A, B | UNRUN | |
| AC-TL-04 | Local creation date | A deliverable created 2026-11-09 22:30 America/Halifax (02:30 UTC on Nov 10), no start date. | The assumed start is 2026-11-09. | A | UNRUN | |

### 6.8 Privacy matrix (SC-004)

| Row | Surface | Persona | Expected |
|---|---|---|---|
| R1 | SYN-103 (restricted) open roles: list, export, search, Workload Unfilled roles | Priya, Sam, Alex, Rita | Nothing: no row, no count, no project name. |
| R2 | Same | Marc (PM of SYN-103), Jill (member), Jordan (Admin, as today's restricted rule allows) | Visible as allowed today for SYN-103 allocations. |
| R3 | Open role notifications and digest | Non-members of SYN-103 | No mention. |
| R4 | Activity log and history of an SYN-103 open role | Non-members | 404. |
| R5 | Picker capacity | All of 6.5's scope cases | As stated in AC-PICK-02a to -02d. |
| R6 | `planning/grid?personIds=` with out-of-scope ids | Sam with Omar's id; Alex with Jill's id | Those ids are absent from the response; no error that reveals they exist. |

## 7. SC-002 count script specification (`web/tests/counts.cjs`)

- Same Playwright setup as `web/tests/planner.cjs`. It runs against the local preview with fixtures loaded.
- For each persona in (Jordan, Priya, Sam):
  - For each page in (Home; Project Dashboard for SYN-101, SYN-102 and SYN-103 where visible; Portfolio):
    - For every tile or segment with a link: read the displayed number, take the link URL, and call the list API for that URL as the same persona, reading the total.
    - Record (persona, page, tile, shown, list total).
- Fails on any shown ≠ total. Also fails if a linked tile from the agreed list below is missing, so a tile cannot vanish and pass.
- Agreed tile list:
  - **Home**: Active Projects; Open Tasks; Overdue; On Track (numerator and denominator links); Team Members; every Tasks by Status lane plus On hold; every Tasks by Project segment.
  - **Dashboard**: the 8 task counts; the 4 deliverable counts; Decisions pending and overdue; Issues; Risks; Due this week; Blocked; the discipline table Open, Overdue and Blocked.
  - **Portfolio**: Red, Yellow, Green; Active; Submissions ≤14 d.
- Output: a JSON report saved next to the evidence, plus a console table.
- Non-linked counts (Upcoming Deadlines, discipline Due ≤14 d) are checked by their API tests instead.

## 8. Test data

All records are fictional and visibly labelled as preview data. Seeded through the API as the person who would make each change, in `tools/preview/seed_staffing.py` (T020). Dates are relative to the organisation's today unless stated.

### 8.1 Staffing project SYN-104 "Lakeside subdivision servicing (proposal)"

- Status Setup; PM Priya; members Alex, Jill, Omar; disciplines CIV (lead Marc) and GEO (lead Omar).
- Tasks: "Servicing concept layout" assigned to Jill, and "Geotechnical desktop study" assigned to Omar. An allocation needs at least one linked task owned by the person.
- Open roles:
  - "Civil technologist": weeks +2 to +5, 80 h, Open.
  - "Senior reviewer": weeks +3 to +4, 20 h, Open.
  - "Geotechnical engineer": weeks +2 to +3, 40 h, Filled through a Proposed allocation for Omar.
  - "Drafting support": one allocation proposed and then declined, so it is back to Open.
- One SYN-103 (restricted) open role, "Hydraulic modeller", for the privacy rows.

### 8.2 Dependency project SYN-105, worked example

Domain tests use absolute dates. Settings: `working_days_enabled` = true, organisation holiday on **Wed 2026-11-11**, `task_due_soon_days` = 5, project Active. Evaluation dates: **A = Mon 2026-11-09**, **B = Thu 2026-11-12**, **C = Mon 2026-11-16**. "Waiting" and "Blocked" follow D-06; "Date flag" is D-12.

| Link | Type, lag | Predecessor | Successor | Date A | Date B | Date C |
|---|---|---|---|---|---|---|
| L1 | FS, 0 | P1 due Fri 11-13. In Progress on A; Complete on 11-10 by B. | S1 start Mon 11-16, due Fri 11-27, Not Started. | Waiting | Clear | Clear. No date flag. |
| L2 | FS, 2 | P2 due Tue 11-10. In Progress on A; Complete on 11-10 by B. | S2 start Thu 11-12, due Fri 11-20, Not Started. | Waiting | **Blocked**: the lag ends Fri 11-13 because the holiday is skipped; the reason names "lag 2 days". | Clear |
| L3 | SS, 0 | P3 start Mon 11-09, due Fri 11-20. Not Started on A; started Tue 11-10 (`StartedAt`). | S3 start Tue 11-10, due Fri 11-20, Not Started. | Waiting | **Clear**. The old FS rule would say Blocked. | Clear. No date flag (11-10 ≥ 11-09). |
| L4 | SS, 2 | P4 start Tue 11-10, due Fri 11-27. Not Started on A; started Tue 11-10. | S4 start Thu 11-12, due Fri 11-27, Not Started. | Waiting | **Blocked**: lag 2 from 11-10 ends Fri 11-13; the reason names the start link and its lag. | Clear |
| L5 | FF, 0 | P5 due Fri 11-20, In Progress throughout. | S5 start Mon 11-09, due Thu 11-19. In Progress on A and B; Complete on 11-16 by C. | Clear, never Waiting | Clear | **"Finished before predecessor"** note |
| L6 | FF, 1 | P6 due Fri 11-13, Not Started throughout. | S6 start Mon 11-09, due Mon 11-16, In Progress. | Clear | Clear | Clear. No flag (11-16 ≥ 11-13 + 1 working day = 11-16). |

Date flags (successor not terminal):
- L2: "Starts 2026-11-12 before P2 is due 2026-11-13" on dates A, B and C. The holiday moves the due-plus-lag date.
- L4: "Starts 2026-11-12 before P4 starts 2026-11-13" (start + 2 working days) on dates A, B and C.
- L5: "Due 2026-11-19 before P5 is due 2026-11-20" on dates A and B. On C the successor is terminal, so the note replaces the flag.

Control: the same table with `working_days_enabled` = false. L2 and L4 lag ends then become Thu 11-12, so both are Clear on date B and have no date flag.

The browser pass on SYN-105 uses relative dates with the same shapes (holiday at today+2 working days) and checks the chips and arrows only.

### 8.3 Stalled items (on SYN-101)

| Item | State | Status changed | Last activity | Expected |
|---|---|---|---|---|
| Task T21a "Utility conflict log" | In Progress, due in 20 days | 25 calendar days ago | Comment yesterday | A-21 |
| Task T21b "Pavement design memo" | In Progress | 25 calendar days ago | 25 calendar days ago | A-10 only |
| Task T21c "Storm profiles review" | In Review, review requested 25 days ago | 25 calendar days ago | Comment yesterday | A-11 only |
| Deliverable D21 "Geotechnical report" | In Review | 25 calendar days ago | Edited yesterday | A-21 |

25 calendar days is more than 15 under both calendar modes.

### 8.4 Capacity and people

- Week W = the next full week.
- Alex: Confirmed SYN-101 allocation, 20 h over W's 5 working days (4 h/day); Published Expected planning entry of 8 h in W with no project (manager entry by Sam, "Proposal support", so it is not netted against the allocation); Jordan's (Admin) private draft of 10 h in W. Home discipline CIV.
- Jill: availability overrides of 0 h, category Unavailable, on Mon and Tue of W. Home discipline CIV.
- Omar: home discipline GEO. Diane: home discipline not set.

### 8.5 Counts and timeline edge cases

- **SYN-101 counts**: one task On Hold (CIV); one deliverable Issued with no owner; one deliverable Not Started with no owner.
- **SYN-105 timeline**:
  - "No start date task" (created 10 days ago, due in 10 days);
  - a dated task under the undated deliverable "Permit drawings";
  - milestone "Client workshop" with no date;
  - for the API test only, a deliverable created 2026-11-09 22:30 America/Halifax.

## 9. Regression guard (SC-005)

| Check | Pass condition | Status |
|---|---|---|
| Full suite | `dotnet test tests/Hub.Tests/Hub.Tests.csproj --no-restore` is green; the test count is not lower than at the base commit (record both). | UNRUN |
| Packet 029 | Allocation, Readiness and Workload test filters are green; no existing test edited, except additive `openRoleId` cases (`git diff --stat` of those files shows additions only). | UNRUN |
| Packet 034 | `Planning*Tests` are green; `git diff <base> -- src/Hub.Api/Features/Planning.cs` touches only `GridQuery`, its filter and person-row fields. | UNRUN |
| Workload numbers | With the section 8 fixtures loaded, `/workload` person rows saved before the release and after it are identical (JSON diff ignoring `unfilledRoles` and the new person fields). | UNRUN |
| Evaluator output | On the base fixtures, attention items and task and deliverable states before and after are identical, except new A-21 items and new `startedAt` fields. | UNRUN |
| Migration up | On a copy of the review database: 0 started tasks or deliverables with null `StartedAt`; 0 Not Started items with `StartedAt` set; every existing link has type Finish-to-Start. | UNRUN |
| Migration down | The down migration on the same copy restores the previous schema; row counts of existing tables are unchanged. | UNRUN |
| Web | `npm --prefix web run build`, `lint`, `test:planner`, `test:resources` and the new `test:counts` pass. | UNRUN |
| Spec | `python3 tools/build_spec.py --check` and `python3 tools/trace_spec.py --check` pass. | UNRUN |
| Coverage | `python3 tools/coverage_gate.py`: `Evaluation.cs` branch coverage does not drop; the open-role code is covered. | UNRUN |
| Performance | On the `tools/scale` fixture: `/workload` p95 and `/planning/grid` p95 no more than 10 % above the base; picker request p95 under 300 ms for 30 ids. Record sample size and machine. | UNRUN |
| Wording | The packet diff contains no third-party product names. | UNRUN |
| Merge | A trial merge of the packet branch into the base has no conflicts; `HubDbModelSnapshot.cs` changes only in the packet migration commit. | UNRUN |

## 10. Human acceptance sessions

| ID | Session | Who | Where | Script | Pass condition | Status |
|---|---|---|---|---|---|---|
| S1 | Staffing exercise (SC-001) | One working PM (playing Priya) and one Supervisor (playing Sam), not Jay | Review stack | Section 10.1 | Under 15 minutes from step 1 to step 6; no help; both confirm the numbers; observation notes recorded | UNRUN |
| S2 | Dependency review | One working PM | Review stack, SYN-105 | Section 10.2 | The PM agrees with at least 5 of 6 links, and calls none "wrong". A "wrong" answer reopens D4 or D5; it is not treated as a bug. | UNRUN |
| S3 | Keyboard and VoiceOver | Agent (keyboard and axe); one person with VoiceOver on macOS | Local preview | Section 10.3 | Every step can be operated and is announced; axe shows 0 serious or critical issues on changed screens | UNRUN |
| S4 | Visual review | Jay | Review stack, 1440 and 1024 widths | Section 10.4 | Jay approves every listed screen | UNRUN |

### 10.1 S1 script (facilitator reads; does not help)

1. "You're preparing a proposal for Lakeside subdivision servicing. Staff it with a civil technologist for weeks 2 to 5, about 20 hours a week, a senior reviewer for weeks 3 and 4, 10 hours a week, and a geotechnical engineer for weeks 2 and 3, 20 hours a week. You don't know names yet."
2. "Check where this demand shows across the team."
3. "Jill can take the civil technologist role. Make that happen."
4. Supervisor: "You have a staffing request waiting. Deal with it the way you normally would."
5. PM: "Is the civil technologist role covered now?"
6. Both: "Is anything on screen wrong or surprising?"

Record: start and end times, each pause over 20 seconds and where it happened, any request for help, and answers to step 6. Then ask "Would you use this for real proposals? What's missing?" and record the answer verbatim as a follow-up input, not as scope.

### 10.2 S2 script

Show the SYN-105 Timeline on date B, using the relative-date version. For each link, the facilitator states the two tasks and the link type in plain words ("Drainage starts 2 working days after Grading starts"). Ask the PM what they expect the successor's state to be, then reveal it. Record agree or disagree, and the reason.

### 10.3 S3 script

1. Keyboard only, from the task list: open a SYN-101 task, open the owner picker, arrow through the candidates, select Alex, save. Each candidate is announced as name, job title and capacity, for example "Alex Chen, Civil Designer (EIT), 12 hours free".
2. Open "Add dependency", choose the type with arrow keys, set lag 2, save. The type and its hint are announced.
3. Tab through the Home tiles. Each tile's definition is announced.
4. Focus a Timeline bar with no start date. The announcement includes "start not set".
5. Open Workload and change the group-by with the keyboard. The new grouping is announced (live region or heading change).
6. Run axe on Allocations, Workload, Planner, TaskPanel, Timeline, Gantt, Home, Dashboard and Portfolio.

### 10.4 S4 screen list

Check each screen at 1440 and 1024 widths; phone widths keep today's notice or behaviour:
- Allocations tab, open roles list and Fill
- Workload: Unfilled roles and group-by
- Planner: group-by
- TaskPanel: dependency type and picker figures
- Deliverable dependency list
- Timeline and Gantt: legend, faded start, arrows
- Home: hints, On hold, renamed lane
- Project Dashboard: hints, "Whole project"
- Portfolio
- Admin user editor: home discipline
- Admin settings: `status_stale_days`
- Attention list with an A-21 item

## 11. Traceability

| Requirement group | Acceptance rows | Main test files |
|---|---|---|
| FR-ROLE-01–05 | AC-ROLE-01a to 05, R1–R4 | `OpenRoleApiTests`, `WorkloadApiTests` |
| FR-DEPT-01–06 | AC-DEPT-01a to 07, section 8.2 | `DependencyLagTests`, `EvaluationTests`, Readiness tests |
| FR-STALE-01–02 | AC-STALE-01 to 04 | `EvaluatorTests`, settings tests |
| FR-GRP-01–02 | AC-GRP-01a to 02c | Admin API tests, browser |
| FR-PICK-01–02 | AC-PICK-01a to 03c, R5–R6 | `PlanningApiTests`, `PlanningPrivacyTests`, browser |
| FR-CNT-01–07 | AC-CNT-01 to 08 | `counts.cjs`; Workspace, Dashboard and Deliverables API tests |
| FR-TL-01–04 | AC-TL-01 to 04 | Timeline API tests, browser |

`python3 tools/trace_spec.py --check` must show every FR in §40 linked to at least one row above.

## 12. Defects and re-testing

| Severity | Meaning | Rule |
|---|---|---|
| S1 | Privacy leak, data loss or corruption, or a wrong number presented as fact | Stop acceptance; fix; re-run sections 6.8 and 9 in full. |
| S2 | An acceptance row fails | Blocks acceptance; fix; re-run that row and every section 9 row whose files changed. |
| S3 | Cosmetic, or copy that doesn't change meaning | May ship only if Jay approves deferring it; list it below as a follow-up. |

Each defect is logged here with an id, its row, the commit found, the commit fixed, and re-test evidence. A defect that comes from a decision rather than a bug (for example S2 disagreement on link behaviour) reopens that decision with Jay instead of being patched.

| Defect | Severity | Row | Found in | Fixed in | Re-test evidence |
|---|---|---|---|---|---|
| | | | | | |

## 13. Sign-off

| Item | Name | Date | Result |
|---|---|---|---|
| Release commit | | | |
| Software acceptance (sections 5, 6, 9) | | | |
| Human acceptance (section 10) | | | |
| Deferred S3 list approved | Jay | | |
| Final approval | Jay | | |
