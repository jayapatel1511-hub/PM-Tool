# Verification: Dependencies and Rules Engine

Date: 2026-09-25.

## Automated checks

| Check | Result |
|---|---|
| `dotnet test tests/Hub.Tests` | Pass (210/210) |
| `python3 tools/coverage_gate.py` on the coverage run | Pass: rules engine branch coverage 96.5 % (gate 95 %), service line coverage 74.5 % (gate 70 %) |
| `npm run build` (web) | Pass |

Domain tests (pure, no database):

| Suite | Covers |
|---|---|
| `EvaluatorTests` (44) | §15.12 worked examples 1–3; AC-DEP-03/05/06/08/09; D-04, D-12, D-13, D-14, lag; AC-TSK-01/04/05/06/10; AC-ATT-01..07; A-09, A-16, A-18, A-19; AC-REV-03; AC-MS-01..03 and §16.2 b/d/e; A-05; AC-DEL-02/06/07, DL-07 by hours, DL-12, A-13; AC-HLT-01/02/04, deliverable overdue > 5 days Red; §16.6 discipline status; A-15; decision blocks D-15 and DEC-05; A-07; counts and next milestone; working days; determinism (FR-033) |
| `EvaluatorEdgeTests` (21) | Edges to missing items ignored; working days without a holiday calendar; T-03 missing due date under a dated deliverable; D-12 due-before; cancelled decision note; held and finished successors not blocked, cancelled milestones not affected; overdue decision counts only open blocked tasks; derived deliverable dependencies (D-18); explicit deliverable dependency waits or blocks (five conditions); DL-08 with overdue tasks and overdue milestone; undated and slipped milestones; A-05 with blocked work; A-18 for reviewers, owners and decision owners; A-06/A-13 edge cases; issues, risks and meeting-action routing (MTG-01); discipline with inactive lead; expired snoozes |
| `WorkflowTests` (18) | Every status pair for projects, tasks (with and without review, stored previous status), deliverables, decisions, risks, issues and actions against the rule tables; task paths through In Progress; reasons; board lanes |
| `PermissionSweepTests` (16) | §8.5 matrix for 13 personas on Setup/Active/On Hold projects; Archived/Cancelled refuse every write; Complete projects PM-only (P-05); Read Only never writes even as PM (AC-PERM-04); viewer comments; status changes and unarchive; restricted visibility; supervisor staffing; comment delete and 15-minute edit; time entries; system rights; T-16 reasons; saved views |
| `DomainHelpersTests` (17) | Link validation, type and title (DOC-01/02); working-day calendar; settings parsing and validation by kind; vocabulary classifications and rankings |

`EvaluationTests` (8, through the API and database):

| Test | Covers |
|---|---|
| Dependency sides, cycle refusal and chain | AC-DEP-01, AC-DEP-02 (409 with path A → B → C → A, candidate disabled), AC-DEP-07 |
| Overdue predecessor blocks successor until complete | AC-DEP-04 (Blocked with A as blocker, Blocking 1, A-03 Critical, A-02, affected milestone), AC-DEP-05 (cleared, "unblocked" notice), BlockingOverdue notice |
| Snoozed item leaves the list, is logged and returns | AC-ATT-01 ranking, AC-ATT-06 "why", AC-ATT-02 (1–30 days, PM or lead only, logged, returns after expiry) |
| Setup and On Hold projects | AC-ATT-04, AC-PRJ-03, G-05 (no attention, Grey "Project on hold", nothing overdue) |
| Health override shows both values and expires nightly | AC-HLT-03, FR-HLT-02 (not Grey, PM only, Reported/Computed in detail and list, System expiry logged, PM notified, A-15) |
| Nightly run writes one snapshot per Active project and rolls the date | AC-HLT-05 (twice in a night still one row), §16.7 (due yesterday becomes Overdue with no user action) |
| Outbox worker evaluates changed projects | §23.5 steps 2–3 |
| Large project re-evaluates within two seconds | SC-002 / §22 with 2,000 tasks and 1,000 dependencies |

## Manual checks (browser pane, project 2026-0417)

| Check | Result |
|---|---|
| Header health: reported colour with "Why?" listing each contributing input and threshold; PM sees Override | Pass (popover content read from the page; the pane's screenshots do not capture overlays) |
| Task panel blockers box: overdue predecessor with status, due date and Overdue chip; "Start anyway" note; affected milestone | Pass |
| Dependencies section with Depends on / Blocks, remove buttons, Show chain | Pass |
| Putting a successor On Hold re-evaluated within a second: its Waiting chip and the predecessor's Blocking chip cleared | Pass |

## Defects found and fixed while verifying

- Transition notices compared new state with itself: the previous task states were read after the upsert had
  overwritten them, so "became blocked" and "unblocked" notices never fired. The service now snapshots the previous
  flags first (caught by `Overdue_predecessor_blocks_successor_until_complete`).
- `Permissions.ManageSavedProjectView` let a Read Only account that is also a project PM share views; it now refuses
  Read Only (caught by the sweep; the permission is used by packet 019).
- Activity export (packet 009 groundwork) returned 500 on every call because it read row fields by the wrong names,
  and a project's own history listed every dependency in the project because dependency keys contain the project
  number. Both fixed with `ActivityTests` added; packet 009 keeps those tests.
- The health reason for overdue tasks below the minimum count now says so ("fewer than 3, so not counted") instead of
  "below the thresholds".

## Spec inconsistency for Jay

The Appendix C task diagram has no In Review → On Hold or In Review → Cancelled arrows, while the §12.5 transition
table says "any non-terminal → On Hold / Cancelled". The code follows §12.5 (earlier and explicit); the diagram
should probably gain the two arrows.

## Deferred (cross-packet rule)

- The attention list (`AttentionPanel`) is shown on the Project Dashboard and My Work in packet 007.
- Notification rows are created here; delivery, the bell and digests are packet 006.
- Issue inputs (A-07, health) come from packet 014; decision blocks need decision records from packet 008 (the engine
  already handles `blocked_by_decision` links).
