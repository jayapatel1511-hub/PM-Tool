# Packets 052–053 build goal

User authorization: “once spec is done start build with goal” on 2026-10-31.
Implementation may proceed dark under recommended defaults. Open decision statuses remain
unchanged, and no pilot flag may be activated. No commit, push or deployment is authorized.

## Checkout and input boundary

- Build branch: `codex/build-052-053`.
- Isolated checkout: `/Users/jaypatel/.codex/worktrees/ef94/PM-Tool/.build-052-053`.
- Application baseline: `ac2fa89429653144b3258e80f102057ed8c918f9`, the integrated 039–051 runtime.
- Specification inputs: completed §56/§57 and packets 052/053 from `/Users/jaypatel/PM-Tool`.
- Existing integrated amendments to earlier spec parts were retained, not replaced by the
  older primary checkout. The primary checkout and both prototypes remain intact.
- Primary branch's later calm-My-Work/fixture commits are divergent; no wholesale merge was
  performed. Reconcile applicable presentation work when its corresponding slice is built.

## First batch

| Slice | State | Evidence |
|---|---|---|
| 053 S01: seven disabled screen flags | Implemented locally; focused checks PASS | Catalog + domain/API pair tests |
| 052 S01: visual flag, scoped tokens, self-hosted fonts and root switch | Implemented locally; focused checks PASS | Domain/API flags + same-document Admin off/on/off browser checks |

The isolated frontend is available at `http://127.0.0.1:8853/` while its local Vite process
runs. It is a development app, not the static prototype at 8851. Browser fixtures use
controlled synthetic responses. Runtime API tests create and drop their own `hub_test_*`
database; existing review data must not be modified.

## Completion gates

For each batch: focused tests, frontend type/build/lint where relevant, actual-browser
verification for rendered changes, independent diff review, and updated packet verification.
Do not mark all requirements or whole packets complete based on foundation checks.

Next after verified foundation: 052 S02 band/buttons/header, then colour/count/navigation
slices; 053 S02 exact-record context before source-use/reviewer/delivery presentation.
Full scope remains the packet task lists. Cleanup/activation follows a separate user decision.

## First-batch verification

- Focused flag domain/API tests: **30 passed**, 0 failed/skipped. The new catalog entries
  all default false; non-admin changes are refused and independent Admin changes re-read
  through `/me`. Tests used disposable `hub_test_*` databases only.
- Playwright/Google Chrome on 127.0.0.1:8853: **PASS** actual Admin switch off → on → off
  within the same document, scoped Sans/Mono utilities, real same-origin font requests,
  increased-contrast preference, legacy restoration and zero page/console errors. All API
  responses and updates were synthetic; no live setting was changed.
- Frontend build and lint: **PASS** (existing baseline warnings retained).
- Independent foundation diff review: three findings corrected and re-reviewed; no
  remaining actionable findings in that scope. Sign-out/unmount cleanup inspected in
  source; its dedicated browser assertion remains unrun.
- Spec build freshness and traceability: **PASS** on the integrated specification inputs:
  1,546 IDs, 438 sections, no gaps or unknown IDs. This differs from the older primary
  spec checkout because existing integrated amendments were preserved.

Commands: `dotnet test ... --filter FlagsTests/FlagsApiTests/PrototypeAmendmentFlag`,
`npm run build`, `npm run lint`, and `TUESDAY_UI_BASE_URL=http://127.0.0.1:8853
CHROME_EXECUTABLE_PATH=... node tests/visual-system.cjs`. Full logs are under `/private/tmp/`
where recorded. These checks establish the two foundations, not all 203 new FR/AC definitions.

Separate local test container: `codex-pm-052-053-tests`, loopback port 55432, no persistent
volume. Existing review database `pm-tuesday-preview-db` on 55433 was preserved. The test
container may be stopped after the build's integration checks; no review data is in it.

Work in progress: 053 S02 pure next-action evaluator is being corrected before API/UI
integration and has **not** been accepted or marked complete.

## Second batch · local implementation checkpoint

| Slice | Current state | Verification / remainder |
|---|---|---|
| 052 S02 band, buttons, project header | Header and button foundation implemented; slice remains unchecked | Chrome off/on, 72 px long-name header, keyboard-only focus/tooltip, unclipped phone metadata, readable topbar and real project Page primary/outline buttons PASS. Count-card overlap and every-route band census remain. |
| 052 S03 semantic presentation | Partial | Canonical pill tones reuse the existing mapping; exact v2 pill/lane tokens and three item shapes are present. Home lane bars use lane tokens, On Hold uses its named hatch. Metric/count-card and capacity/time-away integration plus the census remain. |
| 052 S04 project identity | Implemented locally | Five-colour assignment/backfill, PM edit, audit, concurrency/refusal, omitted-off reads and retained-on-rollback storage; Home/Timeline/Calendar marks and settings browser checks PASS. |
| 052 S05 title block and response time | Implemented locally | Home plus seven new computed-screen response stamps. Planner grid and list each use their own read. All times use the organisation zone; the Home browser check shifts the viewer clock to 2040. |
| 053 S02 exact-record context | Pure evaluator only; slice remains unchecked | Domain regression cases and independent review PASS. API hydration, permission-safe related links, panels and Weekly Coordination are NOT implemented yet. |

Combined backend check: **40 passed**, 0 failed/skipped. This includes context/identity
rules, identity API lifecycle/audit/concurrency/refusals, nine timestamp reads across eight
screens, registry freshness and OpenAPI freshness. Each API factory used an isolated
`hub_test_*` database. New timestamps are omitted off, populated from the test server clock
on, and omitted again after rollback; Home's pre-existing stamp stays in all states.

The project-colour migration was regenerated by EF as
`20261031200512_ProjectIdentityColour`, with its full designer and snapshot. Fresh test
hosts applied it; `dotnet ef migrations has-pending-model-changes --no-build` reports no
model drift. No existing review database was migrated.

Frontend build, lint, and fixture type checking PASS with existing baseline warnings.
Browser scripts: `visual-system.cjs`, `visual-layout.cjs`, `semantic-colours.cjs`,
`identity-colours.cjs`, and `title-block.cjs`. These run real React components and selected
routes with synthetic API responses, not hosted or human acceptance. The identity route
check covers Home, Gantt, Calendar agenda/month, Settings, shared-component rollback and
Calendar's old off-state chip text/derived stripe. Calendar week and full route census are
still pending. The title test fixes the browser to Tokyo/2040 and checks Halifax response
and reference times. The source/live API contracts stay separate from these UI fixtures.

Independent review corrected lifecycle colour counting, missing/partial actor lookups,
unknown and terminal states, gate/hold transitions, migration discovery, cached colour
rollback, item-mark propagation, readable shell/page buttons, clipped metadata, permanent
focus outlines, Planner layout timestamp mixing and a Calendar flag-off text leak.

Next useful lanes: finish 052 S02/S03/S06 together (metric cards and overlap), then S07/S08
navigation; wire 053 S02 into canonical readers and panels before S03–S05. Screen work in
053 S06 onward can use separate file owners where its stated dependencies permit it.
Do not mark a whole packet complete from this checkpoint; most screen slices remain.

Final logs for this batch are `/private/tmp/tuesday-second-batch-tests.log`,
`/private/tmp/tuesday-second-batch-build.log`, `/private/tmp/tuesday-second-batch-lint.log`,
`/private/tmp/tuesday-test-typecheck.log`, `/private/tmp/tuesday-model-drift.log` and
`/private/tmp/tuesday-final-*.log`. Temporary logs may disappear; the durable facts and
commands are recorded here. No commit, push, deployment, pilot activation or decision
closure has occurred. The goal remains active.


## Third batch · Home, navigation and task-context integration

The implementation remains local and dark. Both original HTML prototypes and the parent
checkout are preserved. The parent still has only its two untracked working folders;
all application edits are in this isolated build checkout.

| Slice | Current state | Verification / remainder |
|---|---|---|
| 053 S06 Home count context | Implemented locally; focused verification PASS | Independent visual/Home flags, full scope denominator, deterministic top-project counts, exact due-window split/busiest day, distinct nullable discipline IDs, critical project links, zero-valued undated link and real scope/date controls. |
| 052 S06 count cards | Home implemented; packet slice remains partial | Metric colours, definition tooltip on focus, accessible unknown/zero names, responsive 6/3/2 grid and overlap verified on Home. Other computed screens still use their previous cards. |
| 052 S07 sidebar and tabs | Implemented locally; route census remains pending | Main/foot capability filtering, legacy More, workspace/project/parent-area tabs, real Projects board mode, Portfolio filter propagation, Board loading/empty states and phone tab containment verified. |
| 052 S08 phone drawer | Implemented locally; broader screen census remains pending | Visible menu labels, focus containment/return, Escape/backdrop dismissal, actual search-result navigation closing its Sheet, topbar controls, bottom links and 375 px checks PASS. |
| 053 S02 exact-record context | Task API/panel slice implemented; whole slice remains unchecked | Owner and next actor stay separate; recorded predecessor actors, missing/inactive/external identities, no date, terminal/lag/unknown blockers, malformed JSON, canonical deduplication and unchanged permission responses verified. Other eleven record detail readers and Weekly Coordination remain. |

The new Due card opens a deadline-only Home view with the exact window and project-status
cohort. Its read pages the SQL-concatenated task/deliverable/milestone query in a stable
order, 100 rows at most per page. The original widget keeps its old cap/order when the
new paging query is absent. A 105-item same-day cohort exposes all 105 IDs across two
pages; mixed-type boundaries and oversized page clamping also pass. No unbounded
client-requested row load was added.

The task context hydrator follows only stored, same-project, non-deleted inputs after the
existing project authorization. Its descriptive context grants no commands. Handoff and
Constraint links restore the original filtered/sorted task URL, open task panel and actual
main-content scroll position. Restoration observes async content size changes and stops
on success, navigation or user input. The browser test reaches each destination dialog,
closes it explicitly, then checks the origin and scroll; it does not merely wait for a URL
that already matched before navigation.

### Verification

- Combined API/domain/contract run: 51 of 52 passed initially. The sole failure attempted
  to store syntactically invalid JSON in a PostgreSQL JSONB column, which correctly refused
  it before the reader ran. The fixture was changed to valid JSON with the wrong structure;
  all five malformed-input theory cases then passed. All 52 focused cases are covered in
  their corrected final state across those runs.
- Additional restricted-scope and generated-registry check: **6 passed**, 0 failed/skipped.
  Home's new context omitted a restricted project's ID, number and counts even when its ID
  was explicitly requested. Every API test used a disposable test database.
- Frontend production build, lint and fixture type check: **PASS**, retaining baseline
  warnings. OpenAPI/types were regenerated and the committed-contract freshness check passed.
- Final Chrome scripts PASS: `home-overview.cjs`, `visual-tabs.cjs`, `visual-layout.cjs`,
  `visual-drawer.cjs`, `visual-navigation.cjs` and the strengthened `task-context.cjs`.
  These use actual routed React components with synthetic API responses, not live data.
  Home includes all four flag combinations, stale-context omission, focus tooltip, unknown
  ratio/zero alert state, 100+5 pagination, real filters and phone containment. Tabs include
  actual Portfolio request parameters and clearing transferred filters from an empty board.
- Independent source review: targeted findings closed after fixes. This is not full packet,
  hosted, human or accessibility acceptance.
- Spec freshness/trace: **PASS**, 1,546 IDs, 438 sections, zero gaps/unknown IDs.

Logs: `/private/tmp/tuesday-third-api-final.log`, `tuesday-malformed-final.log`,
`tuesday-third-scope-registry.log`, `tuesday-third-build.log`, `tuesday-third-lint.log`,
`tuesday-third-fixture-types.log` and `tuesday-third-*.log`. Temporary logs may disappear;
this checkpoint records the durable outcomes and limitations.

### Open acceptance notes and next work

- Sidebar permission clarification (resolved by Jay, 2026-10-31): a person without Workload access can still be permitted
  Vacation & Coverage, but Resources must stay hidden. Jay chose to keep Resources hidden and highlight Time for these users. The role-specific
  alias is implemented and has a focused actual Vacation route check; users with Workload
  access retain the Resources parent. No decision status or permission changed. Full route
  census remains unchecked.
- Home On Track keeps its two required Green/evaluated links (FR-SCR-08) in a card article.
  It cannot also be one enclosing link without invalid nested links; the more specific
  drilldown behavior is preserved. Resolve that wording in final design acceptance.
- Continue 053 S02 with the remaining canonical readers/panels and Weekly Coordination,
  then its dependent blocking-source/reviewer/delivery slices. Finish 052 shared count cards,
  remaining semantic/capacity uses, route/contrast/token censuses and axe checks. The other
  053 screen slices remain the full authorized build scope; this is not a reduced endpoint.

No commit, push, deployment, pilot activation, flag cleanup or decision closure occurred.
The goal remains active.


### Continuation brief

The next bounded integration is the existing Deliverable, Milestone and Decision readers
and their registered panels, followed by Risk/Issue. Relevant sources are
`Features/Deliverables.cs` / `DeliverableDetail` / `pages/projects/Deliverables.tsx`,
`Features/Milestones.cs` / `MilestoneDetail` / `pages/projects/Milestones.tsx`, and
`Features/Decisions.cs` / its detail / `pages/projects/Decisions.tsx`. Extend the single
`Infrastructure/RecordContextReader.cs` and pure `RecordContextRules`; do not introduce a
second evaluator. Preserve review-gate/previous-state semantics and use the authorized
project PM for milestone accountability/next transition. Verify actual canonical fields
before implementing; the read-only exploration is a lead, not final authority.

After those, Action discipline/external ownership, Change assessments/follow-ups, nested
Review findings, Submission checks, Handoff and Readiness need their own precise adapters.
The actor resolver must include each newly used canonical role (PM, coordinator, checker,
originator/verifier) and preserve a missing actor instead of substituting another user.
Keep off reads free of added queries/fields. Related input order must satisfy the full
kind/key/id contract separately from the primary blocking-input priority. Additive API
fields need contract/types regeneration; no schema migration is planned for these readers.
Use the same disposable database and existing Chrome/Vite fixture workflow.


## Fourth batch · Deliverable, Milestone and Decision context

The existing authorized detail reads and registered panels now include the shared,
optional context for these three kinds. Together with Task, four of the twelve named
item-panel kinds are integrated. **053 S02 remains unchecked**: Risk, Issue, Action,
Handoff, ChangeNotice, WorkConstraint, ReviewFinding, SubmissionPackage, full explicit
relationship presentation and Weekly Coordination still require integration.

- Deliverable owner remains the accountable next actor under FR-SCR-01, including In
  Review; the reviewer is not substituted. Workflow review requirements and a required
  review package remain independent. Package assessment reuses `ReviewEndpoints.Gate`,
  including current/published source and revision checks; stored Approved is not enough.
  Structural transition candidates also obey the existing workflow guard, missing-reviewer
  prerequisite and required-package assessment. No command or permission was added.
- Milestone accountability uses its recorded project PM, since the entity has no owner
  field. Explicit Complete then Cancelled flags take precedence over derived status.
  Missing nonterminal evaluation remains unassessed. The next-action wording is Review
  prerequisites; M-09 completion refusals and the existing command dialog remain intact.
- Decision owner may be the recorded internal user or project-scoped external party.
  Missing/inactive/external identities retain their labels. Terminal states keep owner/date,
  hide the next actor and say No next action.
- New context fields are omitted with `screen_patterns` off; the shared reader returns
  before loading context data. Existing detail permissions and command behavior are retained.
  Input presentation is ordered by kind, key and ID separately from blocking priority.

### Verification

- Shared rules/task API run: **43 passed**, with one expected contract-update failure.
  This regenerated the additive OpenAPI fields. The subsequent contract freshness test passed.
- Three-detail API/contract run: 9 of 11 passed initially. Two test setups were corrected:
  a Not Started deliverable cannot become Ready to Issue, and the privacy fixture had not
  marked its project Restricted. The two corrected cases then passed. Final coverage is
  **10 detail cases plus contract freshness**, including independent package-gate approval /
  revision mismatch, missing/inactive/terminal owners, milestone state/PM behavior,
  flag-off omission, unchanged permissions and restricted/deleted Deliverable refusal.
- Production frontend build, lint and fixture type check: **PASS**, with baseline warnings.
- Actual Chrome: `record-context-details.cjs` **PASS** for all three panels, stale-context
  hiding, canonical actor presentation, independent package/review text, terminal omission
  and the visible `422 too_early` milestone dialog refusal. `task-context.cjs` regression
  **PASS**, including actual linked-record navigation and scroll return. All responses and
  command refusals in browser tests were synthetic; API tests used disposable databases.
- Independent review: two integration findings (missing translations and an invalid
  milestone-type test fixture) were corrected and closed in source. No remaining actionable
  findings in that bounded review; no claim of whole-S02 or hosted/human acceptance.
- A helper initially wrote into the outer checkout. Its exact patches were backed up,
  reversed there, then integrated against this checkout's typed contracts. The outer
  checkout was rechecked: only `.build-052-053/` and `design-preview/` remain untracked.
  No prototype or unrelated source was overwritten.

Deliverable context reuses the canonical review-package gate without saving changes.
Its manifest validation calls `Coordination.Target` with default locking enabled, issuing
`FOR SHARE` reads for manifest deliverables. Concurrent-update contention and large-package
read performance were not tested; passing functional checks do not establish those properties.

Logs: `/private/tmp/tuesday-fourth-contract.log`, `tuesday-fourth-api-details.log`,
`tuesday-fourth-api-corrections.log`, `tuesday-fourth-build.log`, `tuesday-fourth-lint.log`,
`tuesday-fourth-fixture-types.log`, `tuesday-fourth-details-ui.log` and
`tuesday-fourth-task-ui.log`.

Next bounded S02 integration: Risk and Issue through their existing registered panels,
then Action with its canonical user/discipline/external owner semantics. Verify the actual
ownership fields before extending the single reader/evaluator. All remaining packet tasks
remain in the goal. No commit, push, deployment, activation or decision closure occurred.


## Fifth batch · Risk, Issue and Action context

Seven of twelve named item-panel kinds now supply the shared context through their
existing authorized detail readers and panels. S02 remains unchecked. Handoff,
WorkConstraint, ChangeNotice, ReviewFinding, SubmissionPackage, the full relationship
presentation and Weekly Coordination remain required.

- Risk and Issue retain their recorded owner and review / target-resolution date.
- Action retains its accountable user, external party or project discipline. A discipline's
  recorded lead is the next actor; missing lead is Unassigned, inactive lead remains named,
  and a discipline is never silently replaced by its lead as accountable owner.
- Converted actions follow their one valid explicit same-project Task. The linked task's
  canonical actor supplies the next actor; the action keeps its owner and date. Missing,
  deleted, cross-project, duplicate or malformed conversion metadata stays unassessed
  without leaking target details or advertising a direct Complete command.
- Terminal states retain owner/date and omit next actor/action. Commands and permissions
  remain those of the existing server responses. Context is omitted with the flag off.

Verification: **47 backend checks passed** (30 pure-rule, 16 operational API and contract
freshness). Covers inactive/external/discipline ownership, all terminal states, exact task
conversion, refusal of direct action transitions, restricted/deleted authorization and
invalid conversion metadata. Generated contracts/types, production frontend build, lint,
app and fixture typechecks passed with baseline warnings. Actual Chrome
`operational-context.cjs` passed with synthetic responses, including waiting for the
**distinct linked Task's rendered heading** before closing, and retained filter/sort.
This extra wait exposed incomplete comments/link mocks; those fixtures were corrected
before the final passing run. It is not evidence of production API behavior by itself.

Independent review's canonical External Party discriminator and conversion-detection
findings were repaired and closed. The outer checkout was restored after one helper
wrote there, retaining only its two preexisting untracked directories. No unrelated work
or prototype was replaced. Logs: `/private/tmp/tuesday-fifth-api-final.log`,
`tuesday-fifth-build.log`, `tuesday-fifth-lint.log`, `tuesday-fifth-typecheck.log`,
`tuesday-fifth-fixture-types.log`, and `tuesday-fifth-operational-ui.log`.

No commit, push, deployment, pilot activation, flag cleanup or decision closure occurred.
The full build goal remains active.


## Sixth batch · Handoff and WorkConstraint context

Nine of twelve named item kinds now expose shared context through their existing
project-authorized readers and dialogs. **S02 stays unchecked.** ChangeNotice,
ReviewFinding, SubmissionPackage, complete explicit relationship presentation and
Weekly Coordination remain. The full packet scope remains in the active goal.

- Handoff accountability stays with its sending owner. Draft needs sender submission;
  Submitted needs receiving owner acceptance/clarification/return; Accepted needs receiving
  owner incorporation; Clarification Requested and Returned need sender response/resubmission.
  Incorporated and Cancelled retain owner/date and omit next actor/action.
- WorkConstraint accountability stays with the removal owner; Resolution Proposed needs
  its recorded affected owner. Verified Removed/Cancelled retain owner/date with no actor.
  The existing lookup now includes optional context/title only with the flag enabled.
  The original four-field response and ordering are preserved when disabled.
- Exact constraint context appears **inside** the existing readiness inspector, names the
  exact key/description and highlights its row among other constraints. Its work selection
  is fixed and its inspector remounts on linked constraint changes, preventing another
  task's commands from appearing beneath the original constraint's actor/date.
- Existing commands, refusals and authorization remain. No context-derived command was
  added. Returning from task-linked Handoff/Constraint routes preserves origin filters,
  sort and real scroll position through the existing return-state handling.

### Verification

- Final focused run: **11/11 passed**: six new coordination API cases, four existing
  readiness contract cases and OpenAPI freshness. Includes all handoff/constraint states,
  exact owner/actor/date retention, inactive actors, restricted/deleted/cross-project
  refusals and exact flag-off response keys. The earlier shared run also passed all
  **15 TaskContext API regression cases**.
- Corrected test setups: inactive users cannot authenticate, so the inactive-owner read
  uses the active PM; a nonexistent sender violates the real foreign key and was removed
  rather than disabling database constraints. Unknown-actor behavior remains covered by
  the pure evaluator and existing shared UI checks. Intermediate contract drift was
  regenerated deliberately; the final freshness check ran without an update variable.
- Production frontend build, lint and fixture typecheck: **PASS**, baseline warnings only.
  A leftover unused import and nullable title integration were corrected before final build.
- Actual Chrome `coordination-context.cjs`: **PASS**, using canonical state/action fixtures,
  four facts inside dialogs, flag-off stale-context hiding, terminal retention, exact
  constraint identity/focus among two rows and fixed work selection.
- Actual Chrome `task-context.cjs`: **PASS**, including linked Handoff/Constraint destination
  dialogs and actual nonzero source-list scroll restoration. Browser APIs were synthetic;
  API checks used disposable local databases. No hosted/human acceptance is claimed.
- Independent review's stale-work-selection and flag-off title findings were fixed and
  closed. The exact old-key contract test independently verifies the latter. Parent
  checkout still contains only `.build-052-053/` and `design-preview/` as untracked folders.
- Combined spec and traceability remain fresh: **1,546 IDs; 438 sections; no gaps/unknowns**.

Logs: `/private/tmp/tuesday-sixth-api-final-verified.log`, `tuesday-sixth-api.log` (Task
regressions), `tuesday-sixth-contract-update.log`, `tuesday-sixth-build.log`,
`tuesday-sixth-lint.log`, `tuesday-sixth-fixture-types.log`,
`tuesday-sixth-coordination-ui.log` and `tuesday-sixth-task-ui.log`.

Next source-grounded adapter brief is saved in
`specs/053-screen-completeness/coordination-context-handoff.md`. It records exact child
routing needs, review withdrawal acknowledgement, effective submission checks and
canonical change follow-up ownership. It is exploration, not implementation or acceptance.
Only root owns the build lane: avoid source edits during compilation as well as concurrent
.NET and production frontend builds; an intermediate compile observed a pre-fix snapshot
and was explicitly recompiled before final passing verification.

No commit, push, deployment, pilot activation, flag cleanup or decision closure occurred.
The build goal remains active.


## Seventh batch · Change notices and exact assessments

Ten of twelve named item kinds now expose the shared context. ChangeNotice also has
an exact ChangeAssessment child route; an assessment is an existing canonical record,
not a new stored model. **S02 remains unchecked**: ReviewFinding, SubmissionPackage,
complete explicit relationships and Weekly Coordination still require work. All other
packet slices remain in the active goal.

- Notice owner and assessment due date stay recorded facts. Pending and Clarification
  steps select their recorded affected owners, while unfinished correction work uses
  that exact Task's canonical actor (including its reviewer). Exact Complete correction
  selects the recorded assessment verifier only after evidence, retention, eligibility
  and independence checks. Missing reviewer stays Unassigned; no fallback person is used.
- Retention approval has no uniquely recorded pending approver. That step remains
  Unassigned / Not assessed, rather than inventing a PM or lead. Required adoption is
  a separate owner step. Incomplete Resolved rows, cancelled/deleted/cross-project
  corrections, stale ownership and unavailable eligibility stay unassessed.
- Completion membership reuses the canonical `ChangeEndpoints.Incomplete` projection.
  Detail completion reads are batched and passed to the context reader; target/owner
  eligibility remains a separate fact. Only same-project visible relationships are
  hydrated. The reader does not call the locking/throwing `Coordination.Target` command
  helper. SQL-complete rows cannot silently override an unavailable or mismatched target.
- A notice lists exact assessment IDs and their actor/date/action. Known actors plus
  unknown work explicitly retain that uncertainty (`Partial`). Input ActorState now
  travels with the evaluated result, so Unassigned approval and Unknown input remain
  distinct in both parent and exact-child views.
- The additive, flag-gated project-authorized child GET also requires a visible,
  nondeleted notice. `changes?panel=ChangeAssessment:{id}` resolves the exact child;
  missing links remain in a recoverable dialog and never fall back to the parent.
  Assessments have no intrinsic key or date: labels use their authorized target's key
  and title, and the date is explicitly labelled **Notice assessment due**.
- Shared context navigation now carries nested return state through Task/Decision panels
  and custom dialogs. Same-register child links retain filters/sort; cross-register
  destinations keep their own route parameters. Close restores the exact originating
  record, its earlier return chain and scroll. Legacy non-context panel opens are retained.
- New context is omitted when the flag is off; the new child route returns 404. Existing
  commands and permission fields are retained. No migration, command or permission grant
  was introduced.

### Verification

- Initial combined run after compilation: **60 passed** (42 pure-rule cases and 18
  ChangeContext API cases); its only test failure deliberately updated the additive API
  document. After carrying input ActorState, **33 API cases passed** (18 ChangeContext
  plus 15 TaskContext); again only the explicit contract-update test failed. A subsequent
  run **without** the update variable passed contract freshness, **1/1**.
- New API cases cover flag-off omission/route refusal and permission parity, pending and
  multiple owners, exact child identity/date, correction review/verification, missing and
  inactive verifier, independence, retention/adoption, incomplete Resolved, cancelled /
  deleted / foreign corrections, stale/inactive/deleted targets and restricted/deleted
  parent authorization. Pure tests cover repeated correction references without false
  cycles, unknown work, terminal retention and incomplete/unsafe states.
- Final production frontend build, forced app typecheck, fixture typecheck, lint and
  diff whitespace checks: **PASS**, baseline warnings only. Spec/traceability remain
  fresh at 1,546 IDs, 438 sections, zero gaps/unknown IDs.
- Actual Chrome `change-context.cjs`: **PASS** for parent and exact-child four facts,
  dark route behavior, copied child URLs, distinct actor states, canonical correction /
  retention / verification / terminal stages, missing-child error without parent fallback,
  parent-row Unassigned, and **notice → assessment → correction → assessment → notice**.
  The final path retains filters/sort and restores actual source-list scroll of 160px.
- Actual Chrome Task context regression and converted Action context regression: **PASS**.
  Task main-fact locators were scoped after input rows gained their own actor/date facts.
  Converted Action now verifies returning to the actual Action before closing it.
- Independent review's lost nested-return and Unknown/Unassigned findings were repaired
  and closed. The browser also exposed same-register query loss; it was corrected and
  verified. Initial browser drafts had noncanonical fixture states and approval/closure
  contradictions; those were replaced before the passing final run. No inference of
  production behavior or hosted/human acceptance is made from mocked browser APIs.

Logs: `/private/tmp/tuesday-seventh-api.log`, `tuesday-seventh-api-input-state.log`,
`tuesday-seventh-contract.log`, `tuesday-seventh-build.log`, `tuesday-seventh-types.log`,
`tuesday-seventh-fixture-types.log`, `tuesday-seventh-lint.log`,
`tuesday-seventh-change-ui.log`, `tuesday-seventh-task-ui.log`, and
`tuesday-seventh-operational-ui.log`.

Next: ReviewFinding exact routing and context, including the recorded verifier,
withdrawn-blocking acknowledgement and historical-round command restrictions; then
SubmissionPackage using its one live effective-readiness result. The source brief in
`specs/053-screen-completeness/coordination-context-handoff.md` remains the starting map.
Neither finishing the twelve panel kinds nor these focused checks completes S02 or the
whole goal. No commit, push, deployment, pilot activation or decision closure occurred.


## Eighth batch · Review comments and historical round links

Eleven of twelve named item kinds now expose shared context. ReviewFinding uses an
exact current/historical child route and also shows the four facts in normal package
finding articles. **S02 stays unchecked.** SubmissionPackage, full explicit related
records and Weekly Coordination remain, along with all other unchecked packet tasks.

- Owner is the recorded resolver. Responded comments select their recorded verifier,
  with no originator fallback. There is no finding due-date field: Needed by stays null
  and the UI says No date rather than borrowing the discipline review's deadline.
- Withdrawn Blocking comments without acknowledgement still require the recorded package
  coordinator. Acknowledged or Advisory withdrawal and Verified Closed retain owner/date
  with No next action. Historical nonterminal comments and Cancelled/Superseded packages
  remain unassessed; a finding carried into the current Draft round can still respond.
- Verifier independence includes captured and current authors of **all** manifest outputs
  in each carried ancestor's round, every ancestor's resolver and prior response authors.
  The reader batches package data and caches round authors. Missing/cyclic ancestry,
  deleted sources/outputs and malformed foreign-project task authorship remain unknown,
  including when self-review is allowed. Foreign actors/records are not exposed.
- Actions that would make a started package Approved reuse canonical ValidateRound
  read validation with row locking disabled. Responses that keep a blocker, and current
  Draft actions, avoid a blanket approval gate. Commands keep their existing default
  locks, eligibility, concurrency and transaction behavior; the context never calls
  Recompute or saves changes. A tested final-blocker refusal leaves the finding unchanged.
- Named actor eligibility also uses the existing Person and NamedCoordinationAction
  policies, including active membership, read-only and project write restrictions.
  Inactive people stay named while their next action is unassessed.
- Package actor IDs are seeded once into the shared reader; person hydration queries only
  unresolved IDs, caches negative results and skips empty identity buckets. This removes
  the per-comment repeated people queries found in review. Large-package performance is
  not benchmarked by the functional checks.
- `reviews?panel=ReviewFinding:{id}` resolves its visible same-project package and round.
  Copied links select the exact historical round/comment, lock round selection and retain
  only that comment's history. Missing children stay in an error dialog without a package
  fallback. Normal articles have an Open comment link with exact return state.
- With the flag enabled, package round selection is URL-backed. Ordinary close/open clears
  package-specific round state; captured return URLs retain it. This prevents package A's
  historical round from breaking a later open of package B. Flag-off row shapes, round
  selection behavior and existing commands remain unchanged; the new child GET is dark.

### Verification

- Final focused backend run: **76/76 passed** (30 shared-rule, 14 new finding-rule,
  16 ReviewFinding API, 15 TaskContext API and contract freshness). Tests cover the
  canonical actor/withdrawal/terminal matrix, provenance, cyclic ancestry, deliberate
  malformed foreign references, inactive actors, exact historical versus current Draft
  behavior, approval-gate refusal/rollback, missing/deleted/restricted parent and flag scope.
- Existing exact Reviews list/options/detail/property-order/null/permission contract and
  final API document freshness **without** an update variable: **2/2 passed**.
- Initial run passed 57 of 60; one expected contract-update failure and two invalid test
  setups were corrected. Captured-evidence corruption now uses an explicitly labelled
  bypass fixture, while the current-author case changes the self-review policy for the
  permitted write and then restores it before the read. Production guards were retained.
- Final production frontend build, forced app typecheck, fixture typecheck, lint and
  whitespace checks: **PASS**, with baseline warnings. Combined spec and traceability
  remain fresh at 1,546 IDs / 438 sections with no gaps or unknown IDs.
- Actual Chrome `review-finding-context.cjs`: **PASS** for generic and exact-child four
  facts, dark route suppression, current Draft, recorded active/inactive verifier,
  withdrawal acknowledgement, terminal retention, exact historical round and event history,
  missing child without fallback, and historical package → comment → package return with
  real 160px scroll restoration. It also proves ordinary close → package B does not reuse
  package A's round. A date assertion was corrected to match the combined Needed by text.
- Actual Chrome `change-context.cjs` regression: **PASS**, including exact assessments,
  actor states and the nested correction return chain after shared register changes.
- Independent review's repeated-person-query, foreign-authorship and cross-package-round
  findings were repaired and closed. Generated API/types include optional per-finding
  context and the exact lookup. No hosted/human acceptance or production behavior is
  inferred from synthetic browser fixtures and disposable test databases.

Logs: `/private/tmp/tuesday-eighth-api.log`, `tuesday-eighth-api-final.log`,
`tuesday-eighth-contract.log`, `tuesday-eighth-gen.log`, `tuesday-eighth-types.log`,
`tuesday-eighth-build.log`, `tuesday-eighth-fixture-types.log`, `tuesday-eighth-lint.log`,
`tuesday-eighth-finding-ui.log` and `tuesday-eighth-change-ui.log`.

Next: SubmissionPackage context from its already-computed live EffectiveStatus and
SubmissionReadiness result, with deterministic exact failed/pending check routing. Do
not select stored derived Pending checks, invent owners or route heterogeneous SourceIds
by Kind alone. Then finish S02 relationship/Weekly Coordination scope and the remaining
packet slices. No commit, push, deployment, pilot activation or decision closure occurred.


## Ninth batch · submission packages and exact checks

SubmissionPackage is now the twelfth named reader/panel kind behind `screen_patterns`.
The package detail seeds its already-computed live readiness result into the shared
adapter. Draft remains Draft; Checking/Ready follow canonical live readiness, and
issued/superseded/cancelled packages keep their terminal state. Optional applicability
Fail/Pending does not override Ready. The coordinator remains the owner; the next actor
is the recorded coordinator for Draft, the selected checking owner for Checking, or the
recorded primary PM for Ready, with actual named-person/permission eligibility. A display
context never grants Start, Check or Issue, nor bypasses fingerprint revalidation.

Blocker provenance is internal and JSON-ignored. Code-specific tags distinguish a finding,
assessment, deliverable, revision, input use and basis reference; exact check binding uses
the manifest output, not a heterogeneous SourceId guess. Failed checks precede Pending
checks with stable ID ties and no invented child date. Derived rows can remain stored
Pending while their evaluated display state is Pass. Evaluation coverage is tracked
separately: skipped dependent/review/access checks stay Not assessed, while explicit
known blockers can still show Fail/Pending. Missing required reviews/current rounds,
deleted outputs and mismatched revisions cannot produce a green result by absence.
Canonical blockers, Ready and fingerprint construction remain unchanged, including the
serialized flag-off/on readiness equality covered by the API test.

The flag-gated exact `submission-checks/{id}` locator validates project access, package
and current manifest version. The dialog shows the chosen check once, handles missing
or obsolete results in place, and restores nested source navigation/filter/scroll state.
Check dates stay null; the package date is never copied into a check. Unknown source
locators produce an explicit unavailable-input notice, never a guessed parent link in
record context. SourceRevision, InputUse and basis-child exact locators are still needed
for S05; the legacy readiness-blocker list and full checklist ordering/presentation also
remain S05 work. Parent/check queries invalidate with coordination changes. Shared
Deliverable review-gate reads now opt out of command row locks, retaining command defaults.

Verification:

- Focused backend run: **71/71 PASS** (submission pure/API, existing Submission API,
  shared record rules, Task context and fresh contract). After making the new unavailable
  input count truly optional in JSON/schema, **25 API regressions PASS** in the update
  run with its one deliberate schema-update failure; the final freshness-only run without
  the update variable is **1/1 PASS**. The real unavailable-source serialization/count
  assertion additionally passed **1/1**. Initial new-fixture status/selection assumptions
  were corrected; production workflow/immutability guards were preserved.
- Final frontend build, forced app types, fixture types and lint: **PASS**, with baseline
  build warnings. The optional metadata is absent at zero; internal provenance/coverage
  is absent from public JSON/OpenAPI.
- Actual Chrome `submission-context.cjs`: **PASS** for dark legacy behavior, package/check
  facts, effective states, no check date inheritance, unknown-input notice, exact current
  child, obsolete version and missing-child recovery without parent fallback, nested
  Deliverable return, retained filter/sort, actual 160px background scroll and independent
  80px dialog-body scroll restoration. Mock route
  and status-text selectors were corrected before the final run.
- Actual Chrome ChangeAssessment and ReviewFinding regressions: **PASS**, including nested
  correction/history returns and cross-package round isolation.
- Independent production-source review identified incomplete evaluation coverage; both
  the original and expanded cases were repaired and re-reviewed with no remaining P1/P2.
  Independent UI review also found that dialog scroll needed its own restoration state.
  Origin capture now occurs at click time and remembers the real dialog-body or sheet
  scroll surface; Shell observes delayed portal/content layout and restores both scroll
  positions, then cleans up on success, user input or route change. Closed/hidden portals
  are excluded. The fix passed the stronger browser assertion and source re-review with
  no remaining actionable P1/P2 in this bounded slice.

Logs: `/private/tmp/tuesday-ninth-api.log`, `tuesday-ninth-api-final.log`,
`tuesday-ninth-contract-update.log`, `tuesday-ninth-contract.log`,
`tuesday-ninth-build-final.log`, `tuesday-ninth-types-final.log`,
`tuesday-ninth-fixture-types-final.log`, `tuesday-ninth-lint-final.log`,
`tuesday-ninth-submission-ui.log`, `tuesday-ninth-change-ui.log`,
`tuesday-ninth-finding-ui.log`, `tuesday-ninth-unavailable-api.log`.

S02 and S05 remain unchecked. Twelve named detail integrations do not complete explicit
relationships, Weekly Coordination or blocker-first delivery acceptance. Next bounded
S02 seam is `Features/DisciplineCoordination.cs` and both project/workspace coordination
UIs; reuse its existing submission readiness result and preserve paging/counts. All other
unchecked packet slices remain in scope. No commit, push, deployment, pilot activation,
open-decision closure or hosted/human acceptance occurred.


## Tenth batch · discipline coordination projection and workspace cards

The project discipline-coordination projection and workspace coordination now attach
optional shared context to their visible handoffs, blocker groups, changes, reviews,
issues, input uses, linked actions, submissions, staffing constraints and assessed work.
Each collection retains its own existing page clamp/counts/order. A project-scoped reader
and kind/ID context map deduplicate repeated rows; input-use target graphs and missing
targets are cached separately. Submission contexts reuse the readiness result already
computed by Build and batch only visible packages' current-version checks. No extra
readiness evaluation, stored context or command permission was introduced.

Review-package summaries have a coordinator but no package date; only Cancelled and
Superseded are terminal. Approved does not imply no further round. Source uses retain
their actual owner and no date, with an unassessed next action because the canonical item
table has no independent provenance command. Explicit target relationships are hydrated
by type, ID and project. InputUse is deliberately not registered as a submission source
locator: that exact route is still S05 work. The existing input-use display joins now
also constrain target/source project IDs; foreign imported references return unavailable
metadata, with local sources or targets remaining available independently.

Both UIs show the four facts above item rows. Workspace Using/Reviews/Issues and incoming
submission/staffing rows expose their facts when enabled; legacy summary links remain
when disabled. Exact item links capture origins at click time, preserve modified clicks,
and restore source view/scroll. Staffing keys open the exact WorkConstraint on the flag-on
path. Meeting capture, inline controls, filters, paging, print and CSV behavior remain.

Verification:

- **37 existing coordination behavior/byte-shape and shared pure-rule cases PASS**, plus
  **8/8 new API cases and 1/1 fresh OpenAPI contract PASS** in the final corrected run.
  New cases cover Draft/Submitted actors, retained owner/date, both root projections,
  restricted-project refusal, paging counts, adopted source-use owner/target, independent
  foreign source/target redaction, and Draft/Cancelled review summaries. The first run's
  one contract-update failure was deliberate; a later fixture created a task with the
  wrong discipline permission and was corrected without weakening the guard.
- Final frontend build, forced app types, fixture types, lint and whitespace: **PASS**,
  with baseline warnings. Optional contexts preserve the existing flag-off row bytes.
- Actual Chrome `weekly-coordination-context.cjs`: **PASS** for both project/workspace
  roots, dark legacy behavior, canonical actor distinctions, unknown dates/actions,
  unchanged row count, exact handoff open/close, retained scope/filter/sort and actual
  nonzero 80px scroll restoration in both roots. Synthetic request routes and encoded
  panel selectors were corrected; four real-shaped draft submission rows supply overflow.
- Actual Chrome Submission/check nested source/dialog-scroll regression: **PASS**.
- Independent source review's repeated target hydration, JSX mismatch and missing
  workspace Using context findings were fixed and closed. No remaining actionable P1/P2
  in this bounded projection/UI slice. No hosted/human acceptance follows from fixtures.

Logs: `/private/tmp/tuesday-tenth-api.log`, `tuesday-tenth-api-final.log`,
`tuesday-tenth-api-corrected.log`, `tuesday-tenth-build-final.log`,
`tuesday-tenth-types.log`, `tuesday-tenth-fixture-types.log`, `tuesday-tenth-lint.log`,
`tuesday-tenth-weekly-ui.log`, `tuesday-tenth-submission-ui.log`.

S02 remains unchecked. The original Weekly Coordination agenda is a separate projection:
`Features/Dashboard.cs` / `pages/projects/Coordination.tsx` still need context for their
Task/Deliverable/Milestone/Decision/Issue/Risk/meeting-action cards, including completed,
upcoming and held sections. `AttentionPanel` uses an independent Evaluation endpoint and
also remains explicit follow-up scope. Full relationships and S03/S05 acceptance remain.
No commit, push, deployment, pilot activation or open-decision closure occurred.


## Eleventh batch · original Weekly Coordination agenda

The original Dashboard.cs WeeklyCoordination response now appends a nullable, omitted-off
`Contexts` index keyed by exact record kind/ID. It covers all selected task and deliverable
subsets, discipline top rows, milestones/headline next milestone, open/completed decisions,
issues, risks and waiting external-party actions. Shared task/deliverable/action row DTOs,
selection, order, grouping, counts and window math are unchanged. The enabled read starts
a RepeatableRead snapshot only after EnsureFresh finishes, refreshes project/access context
inside it, then hydrates distinct returned IDs with one scoped reader/ReadLoaded. Context
is read-only; no meeting/edit permission is derived from it.

Coordination.tsx indexes this data once and renders the shared four facts above the rows,
including completed/upcoming/held sections. Opening keys, names, comments or blocker causes
captures the exact source origin through the existing registered panel hook. Completed
decisions become openable on the enabled path; the disabled path retains existing text.
Meeting edits, capture, discussed state/logging, section expansion and exports remain.
The completed decision needed date is RequiredByDate, never its DecisionDate.

Verification:

- **7/7 focused backend checks PASS**: five new agenda API cases, VisualAsOf regression
  and fresh OpenAPI contract. A stronger full serialized old-response comparison (remove
  only Contexts from the enabled response) additionally passed **1/1**. Cases cover all
  selected record kinds, duplicate task contexts, discipline scope, terminal records,
  external-party ownership and restricted-project refusal.
- The initial run had one deliberate contract-update failure and one fixture that had
  not explicitly evaluated newly created derived decision state. The fixture now evaluates
  before asserting membership; no production selection or freshness policy was changed.
- Frontend production build, forced app types, fixture types, lint and whitespace: **PASS**,
  with baseline warnings. Actual Chrome `weekly-agenda-context.cjs`: **PASS** for normal,
  completed/upcoming/held rows, truthful terminal states and dates, internal/external
  actors, section expansion, meeting comment open/close, real nonzero scroll return and
  preserved meeting/filter/sort URL. Synthetic states/selectors were corrected first.
- Actual Chrome discipline/workspace weekly regression: **PASS** after the parent agenda
  integration. Independent production/UI source review: no remaining actionable P1/P2
  in this bounded agenda slice.

Logs: `/private/tmp/tuesday-eleventh-api.log`, `tuesday-eleventh-api-final.log`,
`tuesday-eleventh-off-shape.log`, `tuesday-eleventh-build.log`,
`tuesday-eleventh-types.log`, `tuesday-eleventh-fixture-types.log`,
`tuesday-eleventh-lint.log`, `tuesday-eleventh-agenda-ui.log`,
`tuesday-eleventh-weekly-ui.log`.

S02 remains unchecked. AttentionPanel is backed by a separate Evaluation endpoint and
still needs Weekly card context. Full explicit relationship coverage and S03/S05 work
also remain. All other packet slices stay in scope; no commit, push, deployment, pilot
activation, open-decision closure or hosted/human acceptance occurred.


## Twelfth batch · project attention cards and unavailable facts

Project Attention GET now enriches only the already-ranked/filtered alert rows when
`screen_patterns` is enabled. Evaluation finishes before the enabled read snapshot and
project reauthorization. A project-scoped reader/context cache resolves actual entity
facts by kind/ID; duplicate rules reuse a source context. Stale, deleted, foreign or
unsupported references retain their ranked alert with an explicit unavailable context:
owner/actor/deadline Unknown, next action Not assessed. Alert owner and due metadata are
never substituted for current record facts. Review-package source facts use the existing
conservative owner/terminal mapping. No context or availability is stored.

AttentionRow.Context and RecordContextView.Available are optional and omitted where not
supplied. Actual attention source contexts report Available=true; unavailable ones use
one pure-rule Unknown result with Available=false. The shared UI distinguishes an
unavailable deadline (Unknown) from a known undated record (No date). Project attention
rows opt into a full-width context block, label historical metadata as Alert owner, and
capture exact typed source origins. Missing response context has an explicit unavailable
message. Ranking, counts, severity/discipline filters, snoozes, Why evidence and actions
stay in the existing Items()/controls. My Work's caller and default-false AttentionRows
opt-in retain its legacy presentation; cross-project attention enrichment is not silently
introduced through this Weekly card slice.

Verification:

- **32 focused backend checks PASS**: 3 new attention API cases, 14 evaluation cases and
  15 TaskContext regressions. The one deliberate golden-update failure was followed by
  **1/1 fresh contract PASS** without the update variable. Tests prove current Alex/Diane
  task facts despite stale PM/old-date alert fields, exact old-response equality after
  stripping only Context, missing/foreign/unsupported recovery and restricted-project 404.
- Final frontend build, forced app types, fixture types, lint and whitespace: **PASS**,
  with baseline warnings. Existing My Work/source-helper behavior remains unchanged.
- Actual Chrome `attention-context.cjs`: **PASS** for dark behavior, current owner/reviewer
  and date, distinct alert-owner wording, full unknown facts/unavailable notice with the
  rest of the feed retained, Why/Snooze controls, exact task open/close and preserved
  meeting/filter/sort plus real nonzero scroll. Scope data and owner/source-button
  selectors were corrected before the final run.
- Actual Chrome original agenda regression: **PASS**. Independent production/UI source
  review: no remaining actionable P1/P2 in this bounded attention slice.

Logs: `/private/tmp/tuesday-twelfth-api.log`, `tuesday-twelfth-contract.log`,
`tuesday-twelfth-build-final.log`, `tuesday-twelfth-types.log`,
`tuesday-twelfth-fixture-types.log`, `tuesday-twelfth-lint.log`,
`tuesday-twelfth-attention-ui.log`, `tuesday-twelfth-agenda-ui.log`.

S02 still requires closure of legacy related-record navigation/order/dedup acceptance.
The audit has identified Decision/Risk/Issue/Action related opens without return origins,
Handoff source/target and finding-linked Issue opens without origins, and an existing
created-time ItemLink order instead of relationship/key/ID. Inspect and fix those existing
surfaces rather than assume a new relation framework is needed. S03/S05 source-use and
blocker-specific requirements remain separate. The full packet task lists stay in scope;
no commit, push, deployment, activation, open-decision closure or hosted/human acceptance.


## Parallel chat dispatch · 2026-10-31

Jay explicitly requested splitting work into separate chats to fast-track the remaining
full goal. Four same-directory chats were created and dispatched with exclusive maintained
file ownership: Home/visual system; Work/delivery; Projects/registers/reports; Capacity /
people/utilities. This chat remains the integrator and persistent-goal owner. They share
the exact dirty build checkout rather than rebuilding from an old branch. All four were
confirmed active; initial Home and Work source-inspection progress was observed.

The ownership matrix, precise briefs, source-freeze/build protocol, S02 audit and child
thread identities/cursors are persisted under
[parallel-build](../053-screen-completeness/parallel-build/README.md). Shared schema,
pure rules/permissions, locales/keys, registry/generated contracts and final verification
remain with root. Child lanes implement small batches and report READY before integration.
The full objective is unchanged; no commit, push, deployment, activation or completed-slice
claim follows from dispatch. Current verified implementation checkpoint remains twelve.


## Thirteenth batch — first four-chat integration checkpoint

All four independent chat lanes delivered small owned batches. Root enforced acknowledged
C# and then full-source freezes, integrated shared locale/contract changes, and ran the
combined final checks. No source writers ran builds or generators.

- **Exact record context (053 S02):** existing Task related/dependency, Handoff, Readiness,
  Deliverable member/dependency/review-package, Decision, Risk/Issue, Action and finding
  links now preserve their actual origin. The normal Readiness inspector and Deliverable
  tab are reconstructible under `screen_patterns`. Legacy relation reads are project/type
  bound; flag-on lists sort by relationship/key/ID and deduplicate kind/ID, retaining a
  deterministic stored relationship. Ordinary duplicate Action references appear once.
  Missing/deleted/foreign malformed targets reveal no metadata. Valid disabled ordering
  and presentation stay unchanged. This closes the concrete S02 audit gaps on top of the
  twelve earlier context/actor/exact-child checkpoints.
- **Home charts and runway (053 S07 partial):** adjacent 2:1 project/status charts, status
  donut with visible count equivalent, held-work hatching/zero projects, and an exact
  daily runway with four stable item locators plus overflow. Runway counts are independent
  of the 100-row deadline-list cap. Only the runway is bounded to 14 days; selected dates,
  access/status scope, project calendars and saved widget order/visibility remain correct.
  S07 stays unchecked until This Week's Capacity can be hidden via Edit Dashboard.
- **Find room (053 S26):** a read-only, permission-guarded endpoint/dialog uses canonical
  PLN-09 remaining capacity across every requested week, including the viewer's own
  drafts. Optional home discipline and name ordering never rank people by free hours.
  Partial/provisional availability is separate and never counted as verified room.
- **Workload context/scenario (053 S28):** separate home-discipline signed free-to-plan,
  open-role and unassigned-estimate demand, with unknown supply/estimates retained;
  40 h free + 30 h roles + 20 h estimates yields 10 h short without changing person rows.
  The Proposed-as-confirmed scenario reuses canonical committed/linked-work arithmetic,
  labels affected cells and XLSX parameters, reverses without saving, and is absent off.

Independent reviews found and then closed two P2 return-state gaps: the generic Readiness
inspector's local selection and the Deliverable Dependencies tab. The repaired browser
cases verify exact URL, selected view, real nonzero main/panel scroll and reload. Source
re-review found no remaining actionable P1/P2 in these bounded batches. Home and capacity
source reviews also found no actionable P1/P2. This is a local implementation checkpoint,
not human/hosted acceptance or full-packet acceptance.

### Executed final verification

- **22/22 new API cases passed** (HomeVisualLayoutTests 5, CapacityScreenTests 6,
  ProjectRelationContextTests 11 after theory expansion). The same initial run had the
  one intentional OpenAPI refresh failure.
- **85/85 adjacent/fresh-contract cases passed** without the update variable: Workspace,
  HomeOverview, Workload, Planning, PlanningPrivacy, Decisions, MeetingActions, Registers,
  Milestones/Deliverables, DecisionContract and the fresh OpenAPI check.
- API generation, production frontend build, forced app TypeScript, fixture TypeScript,
  lint and final nested-checkout diff whitespace checks passed. Existing bundle/import,
  nullable-allocation and lint warnings remain; no new blocking compilation failure.
- Actual Chrome scripts **passed**: home-visual-layout, work-delivery-related-return,
  project-relations-context and capacity-screen-workflows. Existing home-overview,
  operational-context and review-finding-context regressions also passed.
- A real API/Chrome copied Task URL check (no API mocks) was repeated on the final compiled
  API and production UI in separate PM, authorized Alex and denied Rita sessions. Owner
  resolves as You for Alex; all four facts/date render; context disappears off; denied
  access returns 404 without the record name/facts. The temporary synthetic QA API and
  database were stopped/dropped. Operational/review databases and flags were untouched.
- Spec build and traceability freshness passed: 1546 IDs / 438 sections, no gaps/unknowns.

Two initial browser assertions were repaired without app changes: ICU's optional weekday
comma and Playwright's immediate checkbox check during a URL-driven transition. The final
scenario test explicitly checks URL, checkbox, rendered load, changed-cell label, API
query basis and reversible state; it does not weaken the functional assertion.

Evidence logs under `/private/tmp/`: `tuesday-parallel-first-api.log`,
`tuesday-parallel-first-regressions.log`, `tuesday-parallel-first-gen.log`,
`tuesday-parallel-first-build.log`, `tuesday-parallel-first-types.log`,
`tuesday-parallel-first-fixture-types.log`, `tuesday-parallel-first-lint.log`,
`tuesday-parallel-first-home-ui-final.log`, `tuesday-parallel-first-work-ui.log`,
`tuesday-parallel-first-projects-ui.log`, `tuesday-parallel-first-capacity-ui-final.log`,
`tuesday-parallel-first-{home-overview,operational-context,review-finding-context}-regression.log`,
`tuesday-recipient-final-browser.log`. Lane handoffs live in `parallel-build/*/status.md`.

S02, S26 and S28 are now checked as locally implemented/verified; S07 remains partial.
All other unchecked 052/053 slices remain in the full goal. New feature defaults remain
false; no commit, push, deployment, pilot activation or open-decision closure occurred.


### Generation 5 continuation

After checkpoint thirteen, all four user-requested chats were explicitly resumed with
exclusive ownership and confirmed active: Home capacity widget; S03 blockers/source uses;
S17/S18 project staffing/filter/peek; S27 planner summaries/deadlines/zoom. CONTROL is now
`editing`, generation 5. Root must not compile/generate/build until the next acknowledged
freeze. Latest fully verified source checkpoint remains thirteen; new work is unverified
until integration. Child cursors are persisted in parallel-build/threads.json.


## Fourteenth batch — second four-chat integration checkpoint

The second parallel batch locally completes Home chart/layout acceptance (S07), project
staffing/filter/group/peek (S17/S18) and planner headers/deadlines/unit/month views (S27).
Home capacity uses the canonical permitted planner read: 648 gross minus 36 away gives
612 available; 529 planned / 612 rounds to 86%, with 83 free only for a complete verified
view. Literal Above 100 is separate from configurable OverPlanned. Project staffing uses
active permitted team members and their visible cross-project load; a specific calendar
conflict is isolated without losing unrelated rows. Fresh project peek links return to
its original filters and both scroll positions. Planner monthly percentages use summed
P/A, ISO-week Thursday determines the month, and raw exports retain weekly-hour scope.
A successful correction-grid read now commits its required access audit.

S03 presentation now shows exact source-use comparisons, existing materialized effects
and one shared source row across Blocked and Waiting members, preserving both memberships
and canonical blocked counts. **S03 stays unchecked:** the normal-command handoff/readiness
source scenario remains required; injected legacy blocker JSON does not establish it.
The persisted canonical-evidence-gap note records the remaining integration boundary.
Home S09 also stays partial: capacity is verified; deadline notes are still required.

Verification on the integrated state:

- All **29 new API/domain cases + 2 fresh contract/registry checks pass**, plus **109
  adjacent cases**. The first runs exposed compiler integration and invalid test-fixture
  assumptions; the final corrected fixture and adjacent run passed 110/110. No production
  permission or validation was relaxed. OpenAPI/registry artifacts were regenerated.
- Production frontend build, forced app types, fixture types, lint and SSR UI review pass.
  Existing lint/dynamic-import warnings remain. The Projects helpers now use their actual
  generated schema without an unsafe refinement cast.
- Four new actual-Chrome scripts pass: Home capacity; Work input/source patterns; Project
  list completeness; Planner headers. Six existing scripts pass: visual layout/navigation/
  tabs, Home overview, Weekly agenda context and related-record return paths. Test-only
  URL/DOM settling fixes retain functional assertions.
- Independent source review/re-review closed all current P1/P2 findings in these batches,
  including Waiting-only/mixed-source membership, peek return, isolated calendar recovery
  and durable correction-access auditing. S03's normal-source gap remains open.
- A real-API 17-screen census made **68 observations** with visual/Home flags off/on for
  PM/Supervisor: zero serious/critical axe violations, page errors or off-origin fonts.
  All six prior flag-on first-data-position failures are fixed. Actual count cards are
  classified as data; original thresholds and raw baseline evidence are retained.
  **My Work remains FAIL:** first task row 558 px off / 518px on (405px limit). Its numeric
  week summary at 215px does not establish Today focus-list-first acceptance. The legacy
  clutter sweep additionally fails My Work chrome words 43 > 40 for both roles; all other
  measured legacy thresholds pass. Raw sidebar/tab/fill counts remain subject to the
  open navigation/colour proposals, without hiding required destinations.
- Spec build/trace freshness and whitespace checks pass (1546 IDs, 438 sections, no
  gaps/unknowns). Full 052 S09 remains unverified for token/contrast census, zoom/text
  spacing and all newly enabled part states.

Durable real-census evidence: `evidence/checkpoint14-visual-census.json` and
`evidence/checkpoint14-visual-findings.md`. Logs: `/private/tmp/tuesday-parallel-second-*`
(API final/regressions, build-final, types/fixture-types/lint/ui-review, four new UI scripts
and six regression scripts); `/private/tmp/tuesday-checkpoint14-clutter-off.log`. The
real synthetic census API was stopped and only its disposable QA database was dropped.
Operational/review databases and flags were untouched.

S07/S17/S18/S27 are now checked for local implementation/verification only. Other unchecked
slices remain in the full goal. Defaults remain false; no commit, push, deployment, pilot
activation, open-decision closure or hosted/human acceptance occurred.


## Fifteenth batch — Home trends, delivery inputs and Portfolio review

The third four-chat integration locally completes 053 S03/S04/S08/S09/S19/S20/S21/S30.
These are local implementation/verification checks only; the remaining slices stay in
the active goal. Formal design/activation decisions remain Open and all defaults stay off.

Home now compares a disclosed common currently visible historical cohort, keeps unknown
and incompatible captures distinct from zero, shows review/baseline deadline evidence,
and preserves explicit role, project and lifecycle scope in every destination. Its
additive HomeMetrics/HomePreset migration has no backfill or existing-row default. The
nightly capture preserves legacy counts and refuses a mixed-day capture across midnight.

Jay's one-target handoff choice is implemented through separately labelled canonical
readiness relationships. Normal commands prove a handoff targets A, a constraint on B
links that handoff, and accepting the handoff does not remove B's constraint. Ordinary
predecessor/decision commands separately prove materialized shared blockers. Source-used
versus current revision facts, exact return and Weekly deduplication remain verified.
Today leads with work and collapses My Week; reviewer and package Start guidance retain
canonical independence, missing/inactive/unknown and allowed-self states.

Project progress shows independent elapsed/progress facts. Bulk project PATCH retains
original versions and distinguishes applied/refused/unknown outcomes, without retrying
uncertain writes. Portfolio reuses canonical scoped staffing and records history coverage
without turning missing payloads into zero. Its six CountCards retain exact destinations;
five overdue decisions reconcile across two contributing projects. Weekly-review print
retains all 72 synthetic projects, all data columns, repeated headers, full status and
health explanations, server time and landscape pages. Long health explanations share a
full-width detail row with status notes; another route still prints portrait after SPA
navigation. Root inspected first/middle/last and combined-note PDF pages.

Allocation reads preserve three separate measures and canonical exact-date capacity.
The tested three-week Fill has availability 40/40/40, before 5/36/0, after 15/46/10;
confirmed/proposed/task estimates remain 60/16/90, never a combined 176. Historical or
invalid attribution keeps known hours Unattributed. Privacy, incomplete/provisional and
unestimated/undated states remain explicit. Supporting context follows primary working
rows and stays keyboard-accessible; planner basis remains available in Details.

Final executed evidence:

- **69 new API/domain cases + 2 fresh contract/registry checks + 135 adjacent cases PASS.**
  Initial runs exposed fixture/compiler issues and one real EF predicate-after-projection
  translation failure. The narrow raw-entity prefilter preserves project/deletion scope
  and canonical missing-source semantics; independent review and normal-command tests pass.
- Final production build, forced app types, fixture types, lint and SSR UI review PASS.
  React/lint and bundle/dynamic-import warnings remain; no warning-free claim is made.
- Eight new Chrome suites PASS: deadline notes; trends/presets and phone keyboard scroll;
  Today/reviewers; readiness links; task preset bridge; project progress/bulk; Portfolio;
  allocation comparison/preview. Seven affected regressions also PASS: capacity workflows,
  planner headers, Home capacity, Weekly agenda, Dashboard reflow, work inputs and returns.
- Independent source review/re-review closed all actionable P1/P2 findings in this batch,
  including midnight capture, historical allocation attribution, transition picker,
  missing history payload, keyboard scroll and print word/column containment. The final
  print test exercises nonempty visible Range bounds and a long unbroken note reference,
  including a project with both status and health notes.
- Real 17-screen/all-eight-flags census: 68 observations, zero serious/critical axe issues,
  page errors or external fonts. All 34 enabled observations are within 405px, maximum
  402px. The overall runner still fails the two preserved legacy-off My Work rows at
  558 px. No threshold or measurement was relaxed; full 052 S09 is not checked.
- Real reflow/text-spacing audit: 68/68 PASS, zero document overflow, visible text below
  11px, clipped controls, serious/critical issues, page/font errors or flag-restore failures.
  800px/DPR2 is a reflow equivalent, not native 200% browser zoom/human acceptance.
- Spec build/trace and whitespace checks PASS: 1546 IDs, 438 sections, no gaps/unknowns.

Raw screen and reflow JSON plus limitations are in `evidence/checkpoint15-findings.md`.
The synthetic print PDF is `../053-screen-completeness/evidence/checkpoint15-portfolio-weekly-review.pdf`.
Logs are `/private/tmp/tuesday-checkpoint15-*`; API final is api-final.log, neighboring
checks api-adjacent.log. The final print-only refinements were separately rebuilt and
verified in Chrome after the screen/reflow scans; screen layout rules were unchanged.
Temporary QA API 5088 and only hub_qa_checkpoint15 were stopped/dropped. Test PostgreSQL,
static preview and the preserved prototypes remain; operational/review state is untouched.

No commit, push, deployment, pilot activation, broader decision closure or hosted/human
acceptance occurred. 052 S06 remains partial (Home/Portfolio cards; other screens pending),
and semantic/project-identity/hatch cleanup plus all other unchecked slices remain.

## Sixteenth checkpoint — delivery, staffing and visual semantics · 2026-11-01

Locally verified dark implementation: **053 S22 is checked**. S05-A Submissions is verified but **S05 remains unchecked** until the separate full Readiness increment. Shared052 semantics/Work consumers and Dashboard CountCards are partial work toward052 S03/S06/S09; those full slices remain unchecked. No open design/activation decision was silently closed.

- **Backend PASS 103**:22 new API cases (delivery 6, Dashboard 8, exact-date reservations 6, canonical Timeline lane 2), two fresh OpenAPI/registry checks and79 adjacent workflow/scope/flag cases. Logs: `/private/tmp/tuesday-checkpoint16-api-final.log` (24/24), `...-api-adjacent.log` (79/79). Contract update was separately rerun fresh without update variables. No schema/migration added in this batch.
- **UI PASS**: production build, forced app types, fixture types, lint and SSR review. Build/analyzer warnings remain; no warning-free claim. Final product build log: `/private/tmp/tuesday-checkpoint16-ui-build-verified-2.log`; final forced/types/lint/SSR logs use `...-ui-*-verified*`.
- **Chrome PASS 14 suites**: new delivery, Dashboard completeness and Work visual consumers; updated semantic/identity component harnesses; visual system/layout/tabs/title block, Home trends/presets, attention context, weekly coordination context, Dashboard reflow, Today/reviewer regressions. Source-import layout/title/semantic/identity checks used the separate Vite development harness 8854; real app/mock fixtures used production preview 8853. Mocked browser actions are distinguished from actual API tests.
- **Real-API census**:68 on/off observations across17 screens × 2 roles, all 34 enabled first-data positions≤405 px (max 402), zero serious/critical axe/page errors/external-font requests. Overall runner exits1 for the two preserved legacy off MyWork positions558 px. These are recorded, not waived.
- **Real-API reflow**:68/68 desktop/375px/800CSSpx-DPR2 reflow-equivalent/text-spacing observations pass, with zero document overflow, sub-11 px text, clipped controls, serious violations, page errors, external fonts or restoration failures. DPR2 equivalence is not native browser 200% zoom/human acceptance.
- The full census/reflow preceded a final Dashboard-only spacing repair. Final targeted supplements pass 4/4 on/off/role census observations (enabled first data 267 px) and4/4 reflow variants. They explicitly record the selected route. Other application sources did not change after the full pass. Evidence is in052/evidence/checkpoint16-*.json.

Independent backend review found no P1/P2. UI review caught Supersede overriding a permission-disabled offer and actual SubmissionPackage targets using an unregistered panel. Both were repaired and tested through denied/allowed/partial controls and exact package navigation/return independent of screen_patterns. Runtime testing also caught print duplication while a screen legend remained open; visual-only print hiding and a print-while-open assertion fixed it. Root screenshot inspection caught Dashboard cards overlapping its title/reference/controls: removed that project page's inherited overlap prop, removed its now-unused flag local, independently reviewed the normal-flow fix and added1440/375 px geometry assertions. Root inspected final Dashboard and Board screenshots, saved in052/evidence. No automatic pass was treated as sufficient visual acceptance.

Fixture-only repairs preserved production guards: actual HealthOverride snapshot fields; explicitly labelled corrupt imported-reference setup outside normal immutable-evidence commands; null-safe Unattributed name; one-time identity-colour backfill before read-only/off baselines. Browser probe/selector/URL-render waits now retain exact values, counts and restoration expectations across modern CSS colour serialization and React commits.

All QA data was synthetic and disposable. API 5088 and dev harness 8854 were stopped after verification; hub_qa_checkpoint16 dropped and no hub_test_* database remains. Existing review DB 55433, preview 8853, prototypes and primary checkout preserved. No commit, push, deployment, pilot activation, hosted/human acceptance or whole-goal completion.


## Seventeenth checkpoint — Readiness, allocation shifts and report views · 2026-11-01

Locally verified dark implementation: **053 S05 and S31 are checked** after their full functional increments and independent source/runtime review. S05 combines checkpoint16 Submissions and checkpoint17 Readiness. S23-A catalogue/saved parameters is verified; S23 stays unchecked pending exact task-input counts/drill-downs. 052 S02/S03/S06/S07/S08/S09 remain incomplete. No decision or activation gate was closed.

- **Backend PASS227 distinct API checks**:28 focused and209 affected, with10 shared checks. The focused set includes6 Readiness,8 shift,8 report catalogue/views,4 workspace identity and2 fresh OpenAPI/registry goldens. Node QueryClient/cache suite9PASS. No migration/schema change in this checkpoint.
- **UI PASS**:production build, forced application types, fixture types, lint and SSR review. Build/analyzer warnings remain. The final browser-script-only fixes passed syntax/fixture checks without changing the built product.
- **Chrome PASS15 suites**:8 new/navigation checks and7 affected workflows, including final planning-reporting (16 mocked posts; import true; no errors/unknown requests/axe findings). These mocked interaction checks are distinct from the real API cases below.
- **Real API census**:68 observations over the standard17 routes ×2roles ×on/off. All34 enabled observations≤405px (max402); zero serious/critical axe/page/font errors. Runner still exits1 for two preserved legacy-off MyWork558px observations. That fixed-route result does not prove every report/project subroute is compact.
- **Real reflow**:68/68 observations PASS at desktop,375px,800CSSpx/DPR2 equivalent and text-spacing variant; zero overflow/sub11px text/clipped controls/serious/page/font/restore failures. Reflow equivalent is not native browser200% zoom or human acceptance.
- **Three real UI-to-API journeys PASS**, using a separate synthetic project:3 allocations shift+2weeks with task dates/person/hours/links/provenance preserved and Confirmed→Proposed;3 unassessed Readiness rows with no assessment write and expected absent-read404;Priya/Sam report rows and a personal view containing only projectId/week, invisible to Sam. Two disposable identical personal views resulted from selector retries and are disclosed. Raw evidence and9 PNGs:053/evidence/checkpoint17-real-journeys*.

Independent source review closed material issues in actual Team cache invalidation, report Views bootstrap/capture safety, Vacation capability breadcrumb and an earlier-goal legacy-off alias leak. The cancellation fixture now supplies its required ordinary reason. Browser-only repairs retain exact values and postconditions while awaiting controlled input/response settlement and decoding panel URLs.

**Visual follow-up remains required.** Root inspected the main joined screens, shift preview/after and inspector/menu captures; an independent reviewer agreed functional evidence does not close universal layout acceptance. Allocations/reports visibly put actual data below405px in these joined routes. The helper's Readiness firstWorkingRowY1611 points to a later element and is rejected as first-card evidence; its first work card is visibly much earlier but still low. These are outside what the fixed-route census establishes. Do not infer exact replacement coordinates from screenshots. Add exact card/row selectors and these joined routes to subsequent layout verification. Inspector/menu PNGs caught a transition frame, so their visual styling is UNPROVEN; settled mocked browser checks/real action+URL postconditions establish interactions, not those screenshots' final opacity. No threshold/allowlist was relaxed and052S09 stays unchecked.

Evidence originally written to an outer review folder was moved intact (10files) to the nested implementation worktree; the outer tracked tree remains untouched. Spec freshness/trace PASS1546IDs/438sections, zero uncited/unknown. QA API5088 and dev8854 stopped after all runtime actors finished; only hub_qa_checkpoint17 dropped; no hub_test_* remains. Test PostgreSQL, preview8853, prototypes and review DB55433 preserved.

Logs: /private/tmp/tuesday-checkpoint17-api-final.log, api-adjacent.log, cache-keys.log, ui-*.log and browser/regression-*.log. No commit,push,deploy,pilot activation,hosted/human acceptance or whole-goal completion.


## Eighteenth checkpoint — activity, report inputs and own-Time week · 2026-11-01

Locally verified dark implementation: **053 S32 is checked** after full FR/AC54–55 source, API, browser and real UI-to-API evidence. S10-A activity/Comments is verified, but S10 remains incomplete for seen/New and the separately scoped generic follow-up streams. S23-B verifies exact input cohorts for six task-row reports; S23 remains incomplete for the remaining Workload/Capacity/Time forms. Planner presentation is further work toward052 S03/S09, not full visual acceptance. Formal decisions/default activation remain unchanged.

- **Backend139 distinct PASS**: focused41 (31 new cases,2 fresh OpenAPI/registry goldens and8 existing catalogue/view checks) plus98 affected checks, zero overlap. Node QueryClient/cache11PASS. No schema/migration change. Logs: `/private/tmp/tuesday-checkpoint18-api-final.log`, `...-api-adjacent.log`, `...-cache-keys.log`.
- **Frontend PASS**: final production build5, forced app types before the final Time layout-only repair (build5 also typechecks it), fixture types, lint after the final layout, and SSR review. Analyzer/bundle/lint warnings remain. Logs use `/private/tmp/tuesday-checkpoint18-ui-*`.
- **Eight Chrome suites PASS**: planner semantic source harness; Work activity/Comments; own-Time (rerun after final layout); six report task inputs; existing workspace; full planner workflows; planner headers plus actual entry-list on/off supplement; report catalogue/views. These real rendered UI checks use explicit mocked APIs, separately from actual backend and joined checks. Source-import semantic harness uses dev8854; built app checks use preview8853.
- **Real Work journey PASS**: three normal seeded comments plus Priya's fourth through the actual UI, actor/count refresh in the originating project row, exact Task Comments and original filtered URL return. Real source scroll was0; nonzero scroll/focus return is established only by the mocked case.
- **Real own-Time journey PASS**: normal synthetic40h schedule and8h Wednesday ManualHR reference give32h capacity. Three assigned tasks plus preceding-week actual union deduplicate to4 rows. Blank load writes nothing; one Enter on a fully-away Wednesday records exactly one2h entry, owned by Alex, and real totals show2h/32h. This is synthetic local HR-reference fixture data, not an external-provider interaction.
- **Real report journeys PASS**: three normal tasks have null estimates. The nonfirst main-table Task returns to the exact report URL; the separate3-unestimated metric opens a3-row list, its nonfirst Task returns to the exact metric URL, and closing returns to the base report. Actual main first rowY379.5px. Real metric-return focus was not measured; the exact nonfirst-row/count focus case passes in the mocked suite. The raw followup metric firstRowBox repeats the main row coordinate and is not accepted as dialog geometry evidence.
- **Final Time visual check PASS** after root screenshot review exposed the old totals sidebar squeezing Saturday/Sunday off desktop. Own-week grid is now full width, week/filter controls share one row, and totals follow it. Independent source re-review found no P1/P2. Final real1440px region1134/1134px, first taskY399.5px;375px document375px with a1065px table inside named keyboard-scroll region341px. Same32/8/2 facts and4rows, zero errors/mutations in this final read-only check. Root inspected both settled PNGs.

Other material source/runtime findings were repaired and independently re-reviewed: actual CommentPanel project-cache refresh; Workspace page selection incorrectly resetting to page1; Planner EntryList hook scope; Time known-save/refetch double-create race; report malformed scalar GUID/overflow paging; exact main-report origin navigation; optional calendar non-working truth independent of zero hours/leave. No guard was weakened. Append-only activity fixtures clone a labelled legacy imported Task rather than deleting history. Report raw scalar parsing stays consistent with each real report parser.

Six nonempty report mock geometries are379.5px when both relevant flags are on. The preserved visual-off expanded controls remain653.5px, without a waiver. This checkpoint did not rerun the whole68-observation fixed-route census/reflow; checkpoint17 remains the latest broad scan. That route set excludes own-Time and open-report/joined project subroutes.052 S02/S03/S06/S07/S08/S09 and earlier joined Readiness/Allocations density/transition-capture limits remain open. Phone containment is not a native200%zoom/human acceptance claim.

Evidence:053/evidence/checkpoint18-real-journeys* (raw initial PARTIAL retained), checkpoint18-time-layout-final* (final settled view),052/evidence/checkpoint18-report-geometry.json. All are in the nested implementation worktree. The initial40h/0-unestimated/wrong report-return findings are superseded only by the labelled setup/repair/followup evidence, never overwritten.

Spec/trace PASS1546IDs/438sections, zero uncited/unknown. After all runtime actors finished, verified root API5088/dev8854 were stopped and only hub_qa_checkpoint18 dropped; no hub_test_* remains. Test PostgreSQL, preview8853, prototypes, reviewDB55433 and primary checkout preserved. No commit, push, deployment, pilot activation, hosted/human acceptance or whole-goal completion.053 now has21 locally checked slices; all other unchecked slices remain in the active goal.


## Nineteenth checkpoint — grouping, complete report inputs and optional person facts · 2026-11-01

**053 S23 is locally checked.** Its catalogue/views and all twelve applicable report input forms now have source, actual-database and rendered-browser evidence. S11 remains open for selectable columns/Submission date. S29 remains open for custom/native picker notice bridges; directory-backed notices and the facts editor are verified. The full goal and universal052 acceptance remain incomplete. Formal decisions and default-off flags are unchanged.

- **Backend92 distinct PASS:**42 focused (13 grouping,18 remaining report inputs,8 person facts,3 shared foundation) plus50 adjacent including fresh OpenAPI/registry goldens. Node cache12PASS. The initial privacy failure was a fixture inadvertently creating an Open project; a normal restricted-project correction repaired it without changing guards. Final single-process build passed with0errors; the earlier stalled sandbox compiler is not a pass. All542 frozen C# files remain unchanged after frontend verification.
- **Schema/contracts:** additive ScreenPersonFacts migration adds3nullable user facts and2tables, with no defaults/backfill. Independent migration review and real database constraint tests passed. Contract delta:8new paths,10new schemas, two task-list responses now ListPage, optional scoped person-facts/notices; no endpoint removal. Generated app/fixture types pass. New audit types and setting vocabulary remain flag-owned.
- **Frontend PASS:** final production build3, application types, fixture types, lint and SSR review. Existing analyzer/lint/bundle warnings remain. Independent reviews closed SQL bounded grouping, report pager validation, person-editor stale-version promotion/malformed receipts (valid child version0), unavailable Add-here and final compact report spacing. Shared FilterBar gained an optional leading control after type checking caught the missing integration.
- **Twelve Chrome suites PASS:** grouping; person facts; drawer; route matrix; layout/contrast/long names; navigation; tabs; workspace; Team; first-six task reports; remaining-six report forms; catalogue/views. The route matrix covers449 shell/group observations and9 supplied capability profiles using sentinel leaves; it does not prove actual destination admission. Person-facts checks preserve exact key/body on uncertain retries, no duplicate records, actual disabled inputs, stale-save refusal and flag independence.
- **Report density PASS:** after retaining full filters/formula/coverage and reducing compact spacing, first-six mock rows are363.5px; remaining forms are363.5/379.5/391.5/379.5/379.5/401.5px. The405 bound is unchanged. Font-ready, unscrolled stable measurement repaired false transition captures. Earlier451.5/409.5 failures are retained. Visual-off expanded controls remain653.5px, without a waiver.
- **Actual API/UI joins PASS:** four same-discipline tasks total40h/50% across two2-row server pages; normal grouped Task return preserves its exact source URL. Omar created a certification through Preferences with a real201/version0; Jill's Admin UI correction to Co-op, starts2026-12-01/ends2026-12-18 preserves activation/capacity. All-staff and the standard project-Team picker show the date notice; picker contains no certification chip. Nonfirst report metric Task returns to the exact metric query and report. Exact real metric-return focus remains unmeasured; mocked focus/scroll checks pass.
- **Final joined layout:** actual Employee/Discipline/TeamCapacity first rows379.5px; TaskHours as Alex has an actual2h Task row at363.5px. The earlier Priya Total0 capture is not counted as nonempty TaskHours evidence. All four1440/375 documents contain their width; overflow stays in named inner table regions. Root inspected final desktop TeamCapacity, actual TaskHours and phone Employee screenshots. Final read-only actor saw0mutations/console errors.

Evidence:053/evidence/checkpoint19-api-results.json, checkpoint19-real-* (initial partial records retained), checkpoint19-layout-final-build3*;052/evidence/checkpoint19-report-geometry.json and checkpoint19-header-*. Logs use/private/tmp/tuesday-checkpoint19-*.

Limits: S29 FR50 custom/native chooser coverage, S11-B columns, S10 seen/generic streams, the utility navigation policy awaiting Jay, other unchecked slices and052 broad colour/contrast/native200%zoom/human acceptance remain open. No full census rerun; checkpoint17 is still the latest broad scan with its documented exclusions. Global DL07's zero-estimate fallback discrepancy is recorded as an existing build gap; grouping follows its exact new specified formula without changing canonical evaluation.

After every actor finished, verified root API5088(PID48593)/dev8854(PID46930) were stopped and only hub_qa_checkpoint19 dropped; no hub_test_* remains. Test PostgreSQL55432, preview8853, reviewDB55433, prototypes and primary checkout are preserved. No commit, push, deployment, pilot activation, hosted/human acceptance or whole-goal completion.053 now has22 locally checked slices.

## Checkpoint20 — columns, report units and person notices locally checked

S11/S25/S29 are locally checked;053 has25 checked slices. Workspace selectable columns/current submission dates, exact report FTE/months and optional person-fact/native/custom notice coverage passed independent review and scoped verification. Staff/Time CountCards were completed for this batch with a measured Staff-only density repair.133 distinct .NET cases pass, with final30 report/catalogue/golden checks after the final unit-label repair; Node13, types/fixtures/lint/SSR/builds and12 Chrome suites pass. Real normal-command/API/browser joins substantiate dates/views, positive notices/custom verifier/HR stale409, Staff399.5px, Time4h and exact visible November1/.75/1.75 FTE with160h denominator. See [checkpoint20 findings](../053-screen-completeness/evidence/checkpoint20-findings.md) and its durable raw artifacts.

All new behavior stays default-off; no commit/push/deploy/activation. Physical SSE transport and native OS popup/device appearance are unmeasured; broader052 visual/CountCard acceptance, project-task density and all unchecked slices remain active. QA20 API/dev/database were cleaned up after actors finished; production preview8853 and test PostgreSQL55432 retained.


## Checkpoint21 — Board, capacity chart and Team locally checked

**S13/S24/S42 are locally checked;053 now has28 checked slices.** All source and final acceptance reviews closed. New behavior remains default-off in maintained settings; no formal decision was closed, commit/push/deployment performed or pilot activated. See [checkpoint21 findings](../053-screen-completeness/evidence/checkpoint21-findings.md) for self-contained evidence and preserved failures.

- Backend:120 distinct latest .NET cases pass (29 focused,91 adjacent); Node14 passes. Six new schemas/four optional properties, no route or required-field change, no migration. Final556 C# and403 web hashes match the recorded manifests. Types, fixtures, lint, SSR and production build2 pass with existing warnings.
- Browser: four focused and seven adjacent suites pass. Actual project header64px and first child364.5px ungrouped/401.5px grouped; source explanation remains complete on screen and in print. Full override/keyboard/focus/phone/off checks pass. Original report matrix379.5px remains within405.
- Real Board: five cards/12h/one canonical overdue, current Priya PM/Marc DL, actual reviewers and zero-day Ready/In Review ages; Ready timestamp survives In Review. Four project/workspace desktop/phone joins pass. Initial disabled-worker null-state evidence is preserved; enabling only the disposable evaluation worker closed that gap while scheduled jobs/email stayed off.
- Real capacity comparison: Civil40h capacity,45 Confirmed+5 Expected=50 demand, Possible20 excluded,10 shortfall; one standard40h displayed week gives FTE1/1.25/.25. Four real API/chart/table desktop/phone joins pass with no person IDs in the public comparison. Canonical historical unit membership/partial/provisional/cap semantics are independently tested.
- Real Team: unchanged4 membership IDs/2 open roles; workspace/project desktop/phone Alex Civil+Jill Not set Halifax-filtered cohorts; Diane's normal approved8h day+Expected20 gives12h free/8h away, while Omar has no scoped figure. Final option/figure rectangles fit the phone. No allocation fill or membership save occurred.

All raw initial failures and reviewed fixes remain in checkpoint21-runtime and real artifacts. Native select popup/arrow/device behavior, physical SSE transport, universal052 visual/card admission and hosted/human/pilot gates remain unproven. The utility-navigation choice remains pending. Runtime actors closed; verified owned API5088/source8854 stopped, onlyhub_qa_checkpoint21 dropped, nohub_test_* remained. TestPG55432/productionpreview8853 retained; primary/prototypes preserved. Final spec checks pass1546IDs/438sections/zero unknown; git diff whitespace check passes. Full build goal remains active for every other unchecked buildable slice; S10/S43 proposals are preparation only.

## Checkpoint22 closed; next source preparation

053 S14/S33 are locally checked,30 of49. Final Dashboard/Staff CountCard flow overlap and adjacent header/Dashboard regressions PASS:72px desktop/64px phone, uncovered content, desktop header64px/phone cap72px and actual grouped Task child401.5px. This does not close universal052 S02/S03/S06/S07/S08/S09 censuses or native zoom.126 distinct .NET,Node16,11 latest Chrome suites and22 real API/UI joins pass. See [checkpoint22 findings](../053-screen-completeness/evidence/checkpoint22-findings.md). S43 core is verified but global passive names remain open. QA22 API/database cleaned after ownership checks; PG/preview retained. Four restarted chats prepare visual completion/S10/S34/S43 in separate files. Source defaults remain off; no commit/push/deploy/pilot/decision closure/full-goal acceptance.


### Checkpoint23 generation51

Time week-grid now uses the named v2 time-away hatch only under visual_system+capacity_screens; computed browser off/on pairing and retained interactions PASS. Broader052 S03/S09 remains unchecked. The confirmed OnTrack dual-link and utility-sidebar exceptions are recorded as052-D11/D12; other proposed decisions remain Open. Four focused Chrome suites and realQA23 joins support local053 S10, now31/49; see053 evidence/checkpoint23-findings.md. No pilot, deployment or formal acceptance.


## Twenty-fourth checkpoint — final passive-name inventory and drawing intake · 2026-11-02

053 S34/S43 local acceptance PASS;33/49 locally checked. Default-off Search/Files
known-User metadata and passive Person triggers now cover final inventory including
Pickup. Actual Account focus return repaired by excluding closed content while
allowing its visible closed trigger. Four focused Chrome suites plus actual QA24
UI/API/SQL joins pass; real Search keeps no main-sidebar selection. Home separate
Green/all-evaluated links and utility-selection choices remain confirmed.

137.NET/7Node/types/lintwarnings/SSR/build3PASS;602 C# inputs unchanged and129
served files byte-identical. [Checkpoint24 findings](../053-screen-completeness/evidence/checkpoint24-findings.md)
records source/interaction/API proof, preserved failures and limits.052 universal
visual/native/zoom gates remain open. Cleanup of disposable QA24 API/database was
blocked by automatic approval review and awaits explicit confirmation; nothing was
stopped/deleted. No activation, deployment, commit, push or formal Accepted status.


## Checkpoint25 generation53 — Search complete locally, Reviews partial

053 S46 local PASS;34/49 checked. Separate Green/all-evaluated Home links and no
utility-sidebar selection remain confirmed. New Search filters/recents preserve
Person return, counts, privacy and flag-off behavior. R1 open-findings register
passes read-side checks; full S35 remains open. 99distinct.NET/25Node/types/lint
warnings/SSR/isolated solution2+SPA1/four functional Chrome suites PASS. Desktop/
375px final screens inspected;129 served bytes match. Browser reads are synthetic,
backend tests use PostgreSQL; no new live Kestrel join. Broader052 visual/native/
zoom gates stay open. [Checkpoint25 findings](../053-screen-completeness/evidence/checkpoint25-findings.md)
records all initial failures and metadata-only AC linkage repair. OldQA24 cleanup
was rejected by automatic review and awaits explicit confirmation; runtime is
preserved. No commit/push/deploy/pilot/formal acceptance.


## Checkpoint26 generation54 — review outcomes and sheets

FR/AC-SCR-61 locally verified with previousR1; full053S35 remains unchecked for
external provenance, so34/49 unchanged. The new response form keeps the accepted
visual system, explicit outcome/one explanation/evidence, natural layered-dialog
return and375px controls. Current/history outcomes distinguish Not recorded;
reader-accessible full-package response sheet is explicit.149distinct.NET,
types/fixtures/lintwarnings/SSR/SPA2/final10caseChrome+2adjacent/visual4PASS.
Source613C#/446web and143servedbytes match,401earlieroutputs+oldAPIpreserved.
[Checkpoint26 findings](../053-screen-completeness/evidence/checkpoint26-findings.md)
records precise synthetic-browser/provider boundaries, all repairs and pending
legacy-authorship/QA24cleanup questions. Broader052/native/zoom/human acceptance
remains open. Noactivation/commit/push/deploy/pilot/Accepted claim.


## Checkpoint27 generation55 — Files and manual reference context

053 S44 is locally verified;35/49 checked. The existing visual system remains, with scoped Files filters, a missing-direct-link section, labelled C/D warnings and neutral manual comparisons, plus a phone-safe reference form.103 distinct API tests/27 Node checks/types/fixtures/lint warnings/UI review/isolated SPA2/four feature-adjacent Chrome suites pass. Four final desktop/375px captures inspected.622 C#/451 web/142 static and715 retained earlier outputs match. [Checkpoint27 findings](../053-screen-completeness/evidence/checkpoint27-findings.md) records raw failures and repairs, synthetic-browser/provider separation and unanswered external-authorship/QA24 cleanup choices. Broader052/native/zoom/human acceptance remains open. No activation, commit, push, deployment or formal Accepted claim.


## Checkpoint28 generation56 — scoped metadata additions

053S39/S40/S41/S48 locally checked;39/49. New metadata/template/root-cause/Admin-phase1 UI works with visual_system off.170distinct .NET/28client-state/final4syntheticbrowser suites PASS. Missing39templatefieldlabels fixed after visual review and finalSPA rebuilt;10desktop/375px captures inspected. Shared audit read projection now hides exact owned additions while off, preserving immutable records. [Checkpoint28 findings](../053-screen-completeness/evidence/checkpoint28-findings.md) and numbered `.review/checkpoint28-review/index.html` retain proof/limits. Broader052/native/zoom/human acceptance remains open; no flag activation/deployment/Accepted.
