# Verification and Acceptance Plan: Staffing Signals and Trustworthy Counts

**State**: Acceptance plan written 2026-10-11; nothing run yet. Every row below starts UNRUN. PASS means the evidence column names a test run or a screenshot from a stated commit. A ticked task is never evidence.

## 1. Definition of accepted

Packet 035 is accepted only when all of the following hold:

1. Every AC in section 4 is PASS with evidence from the same release commit.
2. SC-001 to SC-005 in section 3 are PASS. SC-001 needs real people, not agents.
3. The regression guard in section 5 shows no change to packet 029 or 034 behaviour.
4. The wording check finds no third-party product names in the packet diff, and the merge into the base has no conflicts.
5. Jay signs off in section 8.

A later release commit voids earlier PASS rows unless they are re-run, or the diff shows the row's files are untouched.

## 2. Decisions (T001)

| # | Decision | Jay's answer | Date |
|---|---|---|---|
| D1–D9 | See spec.md | Pending | |

## 3. Success criteria

| ID | Criterion | How measured | Who | Status |
|---|---|---|---|---|
| SC-001 | A PM staffs one real upcoming proposal with at least 3 open roles and fills at least 1 through the normal proposal and Supervisor confirmation, in under 15 minutes, without help. | Timed session on the review stack with synthetic copies of a real proposal. | One PM and one Supervisor (not Jay). | UNRUN |
| SC-002 | Every count on Home, the Project Dashboard and Portfolio equals the rows of the list it opens: 0 mismatches. | `web/tests/counts.cjs` (T021) clicks every tile as Admin, PM and Supervisor on 3 fixture projects and compares the count to the row total. | Automated. | UNRUN |
| SC-003 | On the dependency fixture project (2 links of each type, one over a holiday), the Waiting, Blocked and date-inconsistency flags match a hand-worked table for 3 dates. | Table in section 6, checked by `DependencyLagTests` and one browser pass on the Timeline. | Automated plus agent browser. | UNRUN |
| SC-004 | Open roles and picker capacity reveal nothing to viewers outside scope: no names, hours or existence. | API privacy tests, plus one browser check per role (Admin, PM, Supervisor, Team member, Read Only). | Automated plus agent browser. | UNRUN |
| SC-005 | Nothing already built changes. | Section 5. | Automated. | UNRUN |

## 4. Acceptance criteria and evidence map

| AC | Verified by | Status | Evidence |
|---|---|---|---|
| AC-ROLE-01 | `OpenRoleApiTests` validation, audit and row version cases | UNRUN | |
| AC-ROLE-02 | `WorkloadApiTests` unfilledRoles; person totals byte-identical | UNRUN | |
| AC-ROLE-03 | `OpenRoleApiTests` one-transaction fill and refused second fill; browser Fill flow | UNRUN | |
| AC-ROLE-04 | `OpenRoleApiTests` decline, cancel and complete | UNRUN | |
| AC-ROLE-05 | `OpenRoleApiTests` privacy: list, export, search, count | UNRUN | |
| AC-DEPT-01 | API add with each type; existing rows read as Finish-to-Start after migration | UNRUN | |
| AC-DEPT-02 | `DependencyLagTests` Start-to-Start waiting → satisfied with working-day lag | UNRUN | |
| AC-DEPT-03 | `DependencyLagTests` Finish-to-Finish never waits; date flag; finished-before-predecessor | UNRUN | |
| AC-DEPT-04 | `DependencyLagTests` Start-to-Start date check, including missing start | UNRUN | |
| AC-DEPT-05 | Readiness, dependency list and deliverable list all agree with the evaluator on the fixture; no client rule left (grep) | UNRUN | |
| AC-DEPT-06 | Browser: arrow endpoints and tooltip for each type on Timeline and Gantt | UNRUN | |
| AC-STALE-01 | `EvaluatorTests` A-21 fires when there was activity but status is unchanged | UNRUN | |
| AC-STALE-02 | `EvaluatorTests` A-10 only, when there is no activity | UNRUN | |
| AC-STALE-03 | Settings range and rule toggle tests; Admin screen shows the setting | UNRUN | |
| AC-GRP-01 | Admin PATCH test and history row; browser edit | UNRUN | |
| AC-GRP-02 | Browser: each group-by on Workload and the Planner; "Not set" bucket; subtotal = sum of rows | UNRUN | |
| AC-PICK-01 | Browser as Supervisor: free hours and away chip for the task's week | UNRUN | |
| AC-PICK-02 | `PlanningApiTests` out-of-scope ids return nothing; browser as PM shows no chip for non-members | UNRUN | |
| AC-PICK-03 | `PlanningApiTests` figure equals the cell endpoint; network panel shows one request per open | UNRUN | |
| AC-CNT-01 | SC-002 script; Portfolio tile link test | UNRUN | |
| AC-CNT-02 | API `onHold=false` test; SC-002 | UNRUN | |
| AC-CNT-03 | Deliverables filter test (Issued and Cancelled excluded) | UNRUN | |
| AC-CNT-04 | Workspace test with more than 101 items of one type; soonest present | UNRUN | |
| AC-CNT-05 | Home `onHold` count test; lanes plus On hold = Open Tasks | UNRUN | |
| AC-CNT-06 | Browser and axe: every tile has a hint reachable by keyboard and screen reader | UNRUN | |
| AC-CNT-07 | Browser: discipline scope shows "Whole project" on decisions, issues and risks | UNRUN | |
| AC-TL-01 | Browser: legend, faded assumed start, tooltip and aria text | UNRUN | |
| AC-TL-02 | Browser and fixture: dated task under undated deliverable appears on the project Timeline | UNRUN | |
| AC-TL-03 | API and browser: undated milestone listed under Unscheduled | UNRUN | |
| AC-TL-04 | Fixture created near midnight UTC shows the local creation date | UNRUN | |

## 5. Regression guard (SC-005)

| Check | Pass condition | Status |
|---|---|---|
| Full suite | `dotnet test tests/Hub.Tests/Hub.Tests.csproj --no-restore` is green, and the test count is not lower than at the base commit. | UNRUN |
| Packet 029 | Allocation, Readiness and Workload test filters are green with no test edited, except the additive `openRoleId` cases. | UNRUN |
| Packet 034 | `Planning*Tests` are green; the planning-entry code path is unchanged apart from `GridQuery.PersonIds`. | UNRUN |
| Workload numbers | On the review fixture, the `/workload` person rows before and after the release are identical (JSON diff, ignoring the new `unfilledRoles`). | UNRUN |
| Existing links | After migration, every existing link has type Finish-to-Start, and the evaluator output on the fixture equals the base output. | UNRUN |
| Web checks | `npm --prefix web run build`, `lint`, `test:planner` and `test:resources` pass. | UNRUN |
| Spec | `python3 tools/build_spec.py --check` and `python3 tools/trace_spec.py --check` pass. | UNRUN |
| Coverage | `python3 tools/coverage_gate.py`: `Evaluation.cs` branch coverage does not drop. | UNRUN |
| Wording | The packet diff contains no third-party product names. | UNRUN |
| Merge | The packet branch merges into the base with no conflicts; `HubDbModelSnapshot.cs` changes only in the packet's migration commit. | UNRUN |

## 6. Test data

Synthetic only, labelled as preview data, added to the existing Tuesday fixture import (T021):

- **Staffing project**: 3 open roles (one Filled), one allocation proposed against a role and then declined.
- **Dependency project**: 6 links, 2 of each type, with one lag spanning a holiday. Hand-worked expectations:

| Link | Type, lag | Expected on day 1 | Expected on day 5 | Expected on day 10 |
|---|---|---|---|---|
| Filled in when the fixture is written | | | | |

- **Stalled items**: one task In Progress with status unchanged for 16 days and a comment yesterday (A-21), and one with no activity for 16 days (A-10 only).
- **Timeline edge cases**: a task with no start date; a dated task under an undated deliverable; an undated milestone.
- **People**: home disciplines set for most people, and at least one person with none.

## 7. Human acceptance sessions

| Session | Who | Where | Pass condition | Status |
|---|---|---|---|---|
| Staffing exercise (SC-001) | One PM and one Supervisor | Review stack | Under 15 minutes, no help, both say the result is right | UNRUN |
| Dependency review | One PM | Review stack | Agrees the Start-to-Start and Finish-to-Finish behaviour matches how they plan | UNRUN |
| Keyboard and screen reader | Agent for keyboard; a person with VoiceOver on macOS for the screen-reader pass | Local stack | Picker chips, dependency type select and tile hints are announced and operable | UNRUN |
| Visual review | Jay | Review stack, desktop and tablet | Approves every changed screen | UNRUN |

The review stack is deployed only after Jay's go-ahead.

## 8. Sign-off

| Item | Name | Date | Result |
|---|---|---|---|
| Release commit | | | |
| Software acceptance (sections 3–5) | | | |
| Human acceptance (section 7) | | | |
| Final approval | Jay | | |
