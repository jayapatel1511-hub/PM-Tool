# Remaining coordination context: source exploration

This is an implementation brief from current-source review, not implementation or acceptance.
The build goal still includes every outstanding packet 052/053 task.

## ChangeNotice · integrated in seventh checkpoint

The implementation and focused verification are now recorded in the seventh build-progress
checkpoint. The following exploration describes the canonical basis; it is no longer an
outstanding adapter. Exact child route is `changes?panel=ChangeAssessment:{id}` with a
flag-gated authorized lookup. Shared nested-return handling and explicit input ActorState
are now available for the remaining adapters.


Owner `OwnerId`; needed date `AssessmentDueDate`. Existing typed reader
`Features/Changes.cs` `Detail` returns `ChangeDetail`, actual UI is local ChangeDetail in
ChangesTab/CoordinationRegister, selected by `changes?panel=ChangeNotice:{id}`.
Reuse the existing `Incomplete` query (around line262), not just `ChangeRules.Complete`:
completion additionally needs rationale/evidence, approved retention or adopted revision,
and completed correction plus independent verification for Resolved. Batch this read;
detail already calculates completion per assessment and should not duplicate it.
Pending/Clarification actor is recorded assessment OwnerId. Update Required follows its
recorded correction Task owner until completion, then recorded ReviewerId to verify.
Retention approval has management permission but no uniquely recorded pending actor;
leave unassigned/unassessed, never invent PM. An assessment whose target owner changed
retains the recorded owner and cannot advertise a refused command. Draft publication and
all-assessments-complete closure are presently unassessed by the generic pure rule.
ChangeAssessment now has an authorized exact child route. Keep using its stable ID;
do not substitute its parent notice when linking a recorded assessment.

## ReviewFinding · integrated in eighth checkpoint

The adapter, exact route and focused checks are complete locally; see the eighth
build-progress checkpoint. `Infrastructure/ReviewFindingContextData.cs` batches package
provenance, caches authors, handles foreign-reference uncertainty and evaluates the
post-action approval prerequisite without command locks. The one public pure evaluator
remains RecordContextRules.Evaluate. Shared people hydration now batches seeded actor IDs
and caches negative lookups. The text below records the canonical basis for this adapter.


Owner `ResolverId`; **needed by null** (no due-date field). Open actor resolver; Responded
actor recorded nonnullable `VerifierId`, initialized to originator at creation then
independently reassignable. Do not fall back to originator for an invalid recorded verifier.
`Features/Reviews.cs` Detail currently returns all package findings; Reviews.tsx renders
inline articles for a locally selected round. The new flag-gated `review-findings/{id}` read and `reviews?panel=ReviewFinding:{id}`
route now select the exact historical/current comment, with no missing-child fallback.
`itemHref(ReviewFinding)` maps to that route. Ordinary package navigation clears `round`;
captured return URLs retain it, filters/sort and scroll.

Critical exception: Withdrawn **Blocking** finding with null WithdrawalAcknowledgedBy
still blocks (`ReviewRules.BlockingOpen`). Its recorded package coordinator must
acknowledge withdrawal. The pure rule now includes this precise acknowledgement exception. Old rounds and Cancelled /
Superseded packages cannot advertise current-round commands. Preserve historical facts.
Verifier independence includes captured/current manifest authors, resolver, response
authors and carried ancestry (`FindingAuthors` / `Independent`). Do not invoke Recompute
or commands on read; cache/batch ancestry/manifest lookup rather than multiply queries.

## SubmissionPackage · integrated at ninth checkpoint

Owner `CoordinatorId`; needed date `TargetDate`. Existing typed Detail in
`Features/Submissions.cs` returns SubmissionDetail; local dialog route is
`submissions?panel=SubmissionPackage:{id}`. Reuse **one** live
SubmissionReadiness.Evaluate result and EffectiveStatus already computed by this read:
Draft remains Draft, nonterminal Checking/Ready derives from current readiness, terminal
packages skip evaluation. Never choose a first stored Pending derived check: derived
rows remain stored Pending until issue, while readiness computes their effective result.
Draft actor coordinator; Checking actor exact effective failed/pending checking owner,
otherwise coordinator; Ready actor project PM. Issue remains subject to server recheck
and fingerprint match; display context grants no command.

SubmissionBlocker.SourceId meaning depends on Code (finding/deliverable/revision/check/
InputUse/basis-impact/version), not Kind alone. Map explicit proven child routes; do not
route every blocker to its parent/register. A required Fail may have both failed/pending
blocker entries: deduplicate presentation without changing evaluator/fingerprint.
Primary selection requires Fail before Pending, known needed date then stable kind/ID.

## Remaining shared work

All twelve named reader/panel kinds are integrated at the ninth checkpoint.
Full explicit relationship presentation and both project/workspace Weekly Coordination
views remain. SubmissionCheck exact lookup is current-version-only. Unsupported revision,
input-use and basis-child locators stay explicitly unavailable in record context until S05
adds their exact authorized routes. Evaluation coverage is JSON-ignored; preserve it when
changing readiness so skipped source/review checks never acquire a false Pass. FR-SCR02 adds source uses, exact blocker
actors/dates, held-up task counts and weekly deduplication. Do not check S02 complete
merely after twelve panel integrations. All other packet screen slices remain in scope.

Use absolute paths under `.build-052-053`; preserve the outer checkout and prototypes.
One build lane: no production frontend build concurrent with .NET builds/tests, and no
source mutation during compilation. Only root runs build/contract generation. Disabled
flags, no commit/push/deploy/activation remain the authorized boundary.


Ninth verified evidence and test/log pointers are in the build-progress ledger. Next S02
file boundary: `Features/DisciplineCoordination.cs` (project and workspace GETs share
Build), `pages/projects/DisciplineCoordinationView.tsx` and `pages/WorkspaceCoordination.tsx`.
Attach optional contexts only to visible rows, project-scope the reader, and reuse each
submission's live readiness result from Build rather than evaluating it again. Preserve
counts, filters, paging, authorization and the RepeatableRead snapshot. Existing explicit
relationship inputs include BlockerGroups, ChangeTargets and ItemLinks/LinkedActions.


Context origins now carry an optional typed dialog-body/dialog-content/sheet-content
scroll surface in addition to background #content scroll and the nested return chain.
Capture origins at click time using the source element. Shell restores after asynchronous
portal/content layout and stops on success or user input/route cleanup. Submission Chrome
coverage asserts 80px inside the real dialog independently of 160px on the background;
Change/Review navigation regressions also pass. Preserve this behavior in the next slices.


## Tenth checkpoint and next original-agenda boundary

DisciplineCoordination.cs plus DisciplineCoordinationView/WorkspaceCoordination are now
locally integrated and verified, with context maps, cached target hydration, reused
submission readiness and project-safe source/target display. Full evidence is in the
build-progress ledger. These views are appended to the original Weekly Coordination;
they do not replace it or complete S02.

Next: Features/Dashboard.cs WeeklyCoordination DTO / Coordination read (around lines53,
204–302) and pages/projects/Coordination.tsx. Prefer one optional null-omitted
WeeklyRecordContext(Kind,Id,Context) list on WeeklyCoordination instead of modifying
shared TaskQueries.Row, DeliverableRow or MeetingActionRow. Collect distinct exact IDs
from the existing selected arrays: blocked/overdue/due tasks, discipline top, completed /
upcoming / held tasks and deliverables, next milestone, open and completed decisions,
issues, risks and waiting external-party actions. Index it in the UI; render context at
row tops and capture origin for key/name/comment/blocker/open controls while retaining
meeting edits, capture, local discussed state and logs. Owner/date come from canonical
records, not decided/completed event timestamps. Preserve all window math, especially
project CoordinationDay and the intentionally different upcoming task/deliverable ranges.

After eval.EnsureFresh, use a read snapshot or deliberate missing-record handling for
added hydration so concurrent deletion cannot turn the whole agenda into 404. AttentionPanel
fetches a separate Evaluation.cs endpoint and cannot be covered by only the Weekly map;
keep that remaining boundary explicit. All source relationship and S03/S05 work remains.


## Eleventh checkpoint

Dashboard.WeeklyCoordination / Coordination.tsx original agenda context is integrated
and locally verified. The optional sorted Contexts index covers every existing returned
item array without changing shared row DTOs. Enabled reads start their snapshot after
EnsureFresh, then reauthorize project/access inside it. Source opens use the existing
panel hook with click-time origins. Preserve all date-window and meeting behavior above.

Next independent Weekly card boundary: EvaluationEndpoints project Attention GET and
components/hub/attention.tsx. Stored attention rows can name stale/deleted or unsupported
entities; handle unavailable context explicitly rather than failing the entire list or
using historical Attention.OwnerUserId/DueDate as authoritative current record facts.
Hydrate only rows actually shown after severity/discipline/snooze/ranking filters and
validate each exact source against its authorized project. Keep ranking, counts, snoozes,
Why evidence and actions intact. Items() is also used by MyWork; inspect both callers and
choose an explicit flag-off-compatible scope, not accidental cross-project enrichment.
Full relationship/S03/S05 acceptance remains after this boundary.


## Twelfth checkpoint and S02 closure audit

Project attention cards are integrated/verified; Items() and MyWork remain unchanged.
RecordContextView.Available is optional; only attention enrichment explicitly supplies it.
Unavailable facts have Unknown owner/actor/deadline and NotAssessed action, not historical
alert ownership/dates or a guessed parent. Project AttentionRows opts in explicitly; its
source button still uses the exact registered kind/ID through the existing panel hook.

Next S02 boundary from current source audit: Decisions.tsx related open, Registers.tsx
related open, Meetings.tsx related work buttons, Handoffs source/target and Reviews finding
linked-Issue routes still need click-time return origins. Decisions.cs ItemLink rows are
created-time ordered; the requirement is relationship kind/key/ID with kind/ID dedup and
project validation. Finish the actual audit matrix before claiming S02 complete. Reuse
existing relation APIs/UI; don't infer that every FK requires a new framework. Keep S03's
source-use/held-up counts and S05's exact source/check delivery work separate and pending.
