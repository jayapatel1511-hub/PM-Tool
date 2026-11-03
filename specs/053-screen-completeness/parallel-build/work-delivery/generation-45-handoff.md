# Generation45 S13 Board completeness

ALL SOURCE READY / HELD. Backend separately returned READY and C# remains held. No source/test edit or process is in flight. Root alone owns review/integration, execution, generation and acceptance ledgers. No lane checks, compilation, types/lint, API/SQL/browser, runtime, helper/agent, goal, migration, commit/push/deploy or activation was performed. Authored cases below are not PASS claims.

## Owned source

- `Features/BoardScreenReads.cs` (new): final approved `BoardFact` and `LeadFact`, `Apply(HubDb,IReadOnlyList<TaskQueries.Row>,OrgSettings,DateTimeOffset)`. Four bounded batched reads over returned page task/project/discipline/lead IDs. Same-project mapping; exact current recorded people; actual null DL is Unassigned, missing/foreign/damaged reference Unavailable. No DL→PM substitution, active-person eligibility change, broad directory query, evaluator or mutation. Derived ActivityRow retained through with copy.
- `projects/Board.tsx`: work_screens-gated Ready/In Review reviewer and canonical waiting-age rendering, independent of screen_patterns. Lane count remains actual displayed drag-provider cards; adds known hours/unestimated/known-overdue/unavailable-state facts. Project grouped headers add actual DL when grouping discipline, plus shown-row metrics. Off headings/lane markup retained; no loader/filter/order/drop/command change.
- `WorkspaceTasks.tsx`: only Board import/presentation seam changed in this loan: caption, project-swimlane PM and shown metrics/count labels. Existing S11 chooser/date/saved-view/list sources are held unchanged. Workspace/project grouping identities and order remain their existing actual IDs.
- `projects/board-screen-presentation.tsx` (new): small pure formatter. Integer-tenths sums, estimated0 distinct from null; no inferred zero overdue when state absent. Lead fingerprints across separately read rows must agree or show unavailable. Reviewer null ID is Unassigned; actual ID without name is Unavailable. Missing age/threshold/timestamp remains unavailable.
- `WorkBoardCompletenessTests.cs` (new):6 API Facts +4 work_screens×screen_patterns Theory cases, authored/unrun.
- `web/tests/work-board-completeness.cjs` (new): dedicated actual-app/mock-API browser source, authored/unrun; uses existing preview env/fixtures and starts no runtime.
- Exact11 locale requests: `requests/g45-board-locales.json`. Root owns merge/other locales/generated files.

Root owns existing Tasks.cs/TaskQueries.cs optional null-omitted Row.Board and Page/settings/now bridges within both list GET snapshots. Final C# name is **BoardScreenReads.BoardFact**, not the superseded TaskBoardFacts suggestion. Public shape:

`{reviewRequestedAt,reviewWaitingDays,reviewStaleDays,disciplineLead:{state,id,name},projectManager:{state,id,name}}`.

Lead states exact Known/Unassigned/Unavailable. Days are integer organisation-local calendar days since canonical ReviewRequestedAt using captured server now, only actual Ready for Review/In Review; missing/future timestamp gives null. In Review never resets the source time. FR21 amber/triangle is UI >=threshold; canonical Evaluation's > rule remains unchanged. No general Rows date/lead query and no disabled JSON addition.

## Scope and coverage

Every enabled board states that figures describe shown cards only and exclude hidden/unloaded cards. Lane metrics use the exact currently displayed drag data, and swimlane metrics the actual filtered visible rows. No full-cohort total is claimed. Existing200-row page loader and MAX_CARDS500 remain untouched, as do Done-since14days, side lanes, collapsed Backlog, manual project/workspace order, review/start guards, card marks, panels and return URLs. No new schema, task predicates or cross-project grouping semantics.

Grouping by discipline uses actual ProjectDisciplineId; project uses actual ProjectId. Identical names cannot merge those IDs. For assignee/deliverable swimlanes no fictional DL/PM attribution is introduced. A current lead disagreement between separately read pages is explicitly unavailable.

## Authored acceptance

API10 cases:

1. Normal Ready entry, new comment at5days, then normal In Review transition and6days: original timestamp retained;6-day reviewer age; canonical state at5 remains not stalled while display threshold is5.
2. Five normal In Progress cards estimates0/2/3/3/4=12h, one overdue; actual DL/PM facts. Sixth null-estimate card retains known12h plus one unestimated.
3. Same discipline label on two actual projects/PD IDs with distinct PMs; normal team lead removal yields Unassigned without changing the other project's lead.
4. Labelled damaged-import null/future timestamps and missing materialized state remain unavailable, with0 GET writes to tasks/state/activity.
5. Labelled foreign PD/blank referenced lead-name recovery stays Unavailable, with actual PM separate.
6. Normal manual order PUT/read parity, read-only transition refusal, permitted normal transition and restricted scope/404.
7–10. Independent flag pairs: list-only optional Board fact on both list GETs; general MyWork Rows omitted; off restores exact previous list JSON; empty list remains empty.

Browser source:

- Five In Progress cards/12h/one overdue; Ready and In Review cards both name reviewer and ▲6days waiting despite fresh activity/statusChangedAt.
- Keyboard Enter exact item open and close restores source filters/sort/scope URL.
- Same-name distinct discipline headers with proper DLs; three project headers with their actual PMs.
- Unknown reviewer vs unassigned; exact threshold5 amber versus4 ordinary; omitted/future age unavailable;0 versus null estimate; unknown overdue subtotal.375/768/1280 body containment and readable card/board scroll.
- All four work_screens×screen_patterns pairs, including stale on-shaped mock facts with work flag off.
-201 rows load past the first page;501 matched rows preserve existing cap500 and visible truncation notice. Header500h/count explicitly shown, never501 total metrics.
- Empty workspace state. Existing keyboard pick-up/cancel, ordinary pointer drop through detail-authorised status command (exact payload), and keyboard same-lane manual reorder through normal PUT (exact taskIds order).

Suggested root next step: independently review helper/bridge/formatter/browser cases, integrate11 labels and metadata, freeze exact sources, then run focused WorkBoardCompletenessTests with relevant unchanged Tasks/Views/grouping/columns guards and required types/lint/build/fixture checks. Run work-board-completeness.cjs against root's built preview and inspect settled layouts. API tests own real command/clock proof; browser fixtures own presentation and actual interaction proof. Neither replaces the other's evidence. No S13 checkbox or decision was changed here.

S10 seen state, S12 bulk/Undo, Timeline/Calendar and all other unchecked slices remain outside this exact loan. Canonical single-target Handoff and earlier work are preserved.
