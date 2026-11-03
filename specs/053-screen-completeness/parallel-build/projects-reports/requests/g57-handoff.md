# G57 C S36 source handoff

2026-11-02. **READY for root source review; all eleven C source paths now HOLD.** Implementation is authored only. Compilation, generated contracts, migrations, provider/browser/runtime checks and acceptance are **UNRUN by this lane**. No parser, compiler, tests, typecheck, generation, EF, browser, services, helpers, flags, dependencies, commit/push/deploy or Accepted operation ran.

Checkout `/Users/jaypatel/.codex/worktrees/ef94/PM-Tool/.build-052-053`, branch `codex/build-052-053`. Governing contract `g57-reviews-freeze.md`; prior tested source baseline `.review/checkpoint29-baseline`. Coordinator chat `01a192dd-0b3b-7d31-81f0-69243cd8be2c`; C lane `01a19462-65cb-7ca2-9228-79a3c15c3549`. No source loan beyond the eleven listed files was used. Existing R1/R2, S41 and D Admin user/role/vocabulary work remains preserved.

## Shared integration seams

- Exact static mounts are `ReviewDisciplineSummaryEndpoints.Map(RouteGroupBuilder api)`, `ReviewDisciplineDefaults.MapAdmin(RouteGroupBuilder admin)` and `ReviewDisciplineDefaults.MapProject(RouteGroupBuilder api)`. Root mounts these in its existing API/Admin groups; C edits no Modules.cs/Admin.cs/backend shared file.
- Top-level generated DTOs: `ReviewDisciplineSummary`, `ReviewDisciplineSummaryRow`, `ReviewDefaultSettings`, `ReviewDefaultSettingsWrite`, `ReviewDefaultSuggestions`, `ReviewDefaultSourceType`, `ReviewDefaultSuggestion`. The two new backend files are the authoritative definitions; all handlers return typed `Ok<T>` for root contract discovery. No schema shadows/any types are introduced in the SPA or typed fixture.
- Route parameter names: `{projectId}` for `/projects/{projectId}/reviews/discipline-summary` and `/projects/{projectId}/reviews/default-disciplines?sourceRevisionIds=...`; `{id}` for existing Admin group's GET/PUT `/deliverableTypes/{id}/review-defaults`.
- All new behavior is `register_screens`; it works independently of screen_patterns/visual_system. Existing generic type/reference DTOs, the single DefaultDisciplineId and old review command body/receipt shape remain unchanged. No new required server review disciplines or named signature rights.
- Root supplies its already authored plain `DeliverableReviewDefault` join/DbSet/composite key/two Restrict FKs, shared migrations/metadata, field label/read-side off mask and generation. C uses the actual join class properties only.
- Exact English server/UI additions and cache prefixes are in `g57-locales.md`. Root must merge them before generation/build. Summary prefix `review-discipline-summary`; source-keyed defaults prefix `review-default-suggestions`; settings prefix `['admin','deliverable-review-defaults']`, with viewer/flag/type/project/source parts as appropriate. Root invalidates after current approval/scope/publication, default/type/reference/discipline, visibility/member/user and settings changes, as frozen. No C cache/shared locale edits.

## Authored summary behavior

The project read explicitly selects live/non-deleted currently Approved packages and all their actual rounds/assignments in one repeatable snapshot. It authorizes the project, accepts no query filters, and performs no writes, evaluator refresh, receipt or Seen operation. It returns all actual project disciplines, including honest non-applicable/zero and inactive historical rows. It does not read the paged package/finding register or aggregate findings/manifest/reviewer rows.

Per discipline: eligible+nonapplicable=all current Approved packages; recorded package samples+unavailable=eligible. Only current required disciplines are eligible. Counts use distinct actual started required rounds at/before that current round, preserve superseded started history and exclude unstarted drafts. Even medians are arithmetic midpoint; no sample is null, never guessed0. Timing counts one qualifying assignment for that discipline per started round: timed+unavailable timing assignment-rounds=started assignment-rounds.

Timing uses actual ReviewRound.StartedAt to that DisciplineReview's current latest terminal Approved/Changes Required DecidedAt, UTC ticks divided by TimeSpan.TicksPerDay. Exact0 is valid. Pending/In Review/missing/negative times are unavailable, not response timing or a round-end inference. UI label is Median days to discipline decision, with explicit current stored latest-result disclosure and sample denominators. No new CompletedAt or per-reviewer field/metric.

Missing/foreign/duplicate structural graph relationships refuse integrity409 rather than losing samples. A valid current round with missing StartedAt yields an unavailable eligible package sample. A missing/null/foreign CurrentRound relationship cannot establish which disciplines are required and is treated as structural integrity409, not invented eligibility/non-applicability. This boundary is recorded in the locale/seam proposal too.

## Authored configuration, suggestions and forms

Settings GET/PUT recheck canonical Admin and flag. Nullable write fields allow ordinary binding and canonical version/manual validation. PUT requires observed If-Match/body version, at most50 distinct known nonzero discipline IDs, active new IDs, subset-only changes on inactive types, and unchanged inactive-link retention/removal. Equal set validates version then returns without save/version/audit. Other changes apply only set differences.

One transaction marks the tracked parent's existing RowVersion property modified without manually incrementing it or editing Name. Normal HubDb.SaveChanges increments version/UpdatedAt/By once. One `db.LogEvent("ReferenceData", ..., "Updated", "admin", key:nameof(DeliverableType), name:type.Name, changes:...)` records sorted removed then added scalar `RequiredReviewDisciplineId` old/new changes. There is no Audit.Note, fake scalar Name edit, pseudo-property registry or event per link. Late failure/concurrency rolls back links/parent/audit together.

Suggestions validate1..200 distinct real current published same-project source revisions, resolve actual deliverables/types, union distinct type configuration and map to actual same-project discipline records. Malformed/duplicate400, missing/foreign404, known unavailable/obsolete/unpublished400, broken FK/ambiguous mapping409. Empty defaults are known empty. All configured suggestions remain in the bounded union; total union>50 is explicitly flagged without truncation/automatic submission. Unmapped, retired organisation/project discipline and inactive-only type origins are distinguished; no person/project/discipline/membership is created or inferred.

Only a pristine new package receives automatic available assignment rows, with blank reviewers and the existing visible due-date default. A new round never applies defaults. Any manual assignment/reviewer/date change or draft restoration prevents automatic reconciliation. Current source-key matching excludes late old-source results. Add suggested disciplines explicitly unions rows without replacing filled values, and refuses an over50 resulting set. Failed current reads hide cached suggestions and permit explicit manual choices. Original CreateBody/RoundBody and independent source/author/self-review/Start/PM-removal rules remain.

The new existing-type settings dialog uses a stable original settings/version snapshot and dirty close guard. Stale refusals retain selection/version. Uncertainty locks inputs and offers a current GET reconciliation; it never submits a fresh version or invents a receipt engine. Fresh PUT replies must match exact ID/three-field shape/set and expected no-op-or-one-step version. Reconciliation reports that current saved settings match the original selected set. Catalog read refusal suppresses new controls rather than displaying cached options. Inactive types/organisation values cannot be newly selected.

Existing UI changes were reviewed against checkpoint29 baseline: Reviews.tsx changes only the S36 summary/form imports/mounts and default/manual-assignment reconciliation; Admin.tsx changes only the import/flag and deliverable-type review-default action. R1/R2 finding/detail/export code and D users/roles/root KINDS remain unchanged.

## Authored verification, all unrun

Provider/pure source cases cover actual canonical three-package1/2/3-round approval (median2/max3), even/zero/no median samples, exact package/non-applicable and assignment-round timing denominators, missing timestamps/inactive historical labels,55 additional explicitly synthetic historical packages beyond the browser page, source replacement removing current approval, actual response versus independent closure, no read writes, dark/read-only/restricted/project-integrity/query boundaries.

Defaults provider source cases cover actual settings version1/audit1/scalar IDs/no-op, unchanged single default/name, missing/stale version/Admin gates, inactive type/organisation set changes, invalid/duplicate/wrong-kind/over50 inputs, real two Drawing Package revisions with Civil+Geotechnical configured exactly once, explicit coordinator override through the unchanged ordinary review create, unchanged existing assignments, off persistence/reenable, missing/inactive mapping states, complete51-discipline mixed-type union, obsolete and genuinely unpublished source refusals, concurrent ordinary type PATCH versus defaults PUT, and a scoped disposable-provider fault rolling back links/parent/audit. These use the existing private ReviewResponseFixture; the preserved runtime is not touched.

Strict browser driver source allows only listed reads and explicitly armed defaults PUT/ordinary review POST; unexpected requests/page errors fail. It checks exact known/null summary values independent of list filters, two-source preselection/no reviewer guessing, manual removal/reviewer preservation and ordinary-body payload refusal, a delayed old-source response that actually completes before assertions, explicit union, unavailable/empty/refused suggestions, independent screen_patterns gating,375px natural selectors, actual localStorage draft restoration/manual retention, current summary refusal, original-version stale/uncertain settings behavior with GET-only reconciliation, real dirty Cancel/Discard zero writes and flag-off absence/no new reads. Optional exclusive screenshots use TUESDAY_EVIDENCE_DIR; no prior file is overwritten. The fixture is generated-schema typed and numeric helpers are annotated.

These are authored checks, not PASS evidence. The driver starts no services. It exercises synthetic API interception only; actual provider persistence, runtime authorization, complete existing inspector return/scroll/focus behavior and human acceptance remain separate root checks. No new navigation/focus implementation was authored. The strict driver does not by itself prove all cached-data/focus cases or every inactive/partial state; root should select the relevant adjacent existing checks and final SPA observations.

Suggested root order after all relevant authors HOLD: independent source review; root mounts/locales/cache/model/migration; actual contract generation and standard fixture/types checks; focused provider filter `FullyQualifiedName~ReviewDiscipline`; appropriate existing review permission/independence/source-replacement/R1/R2/type-reference regressions; one root-owned SPA/Chrome lane running `TUESDAY_UI_BASE_URL=<local SPA> node web/tests/review-discipline-patterns.cjs`. Root uses its established disposable runtime/database boundaries. Do not infer S35/R3, S49, full goal, packet or human Accepted status from S36 source readiness.

Static evidence actually obtained: inspected branch/status/freeze/current maintained sources; reviewed both existing-file diffs against checkpoint29; no whitespace diagnostics on scoped `git diff --no-index --check /dev/null <file>` checks (exit1 is expected content difference); SHA256 below. Only source/file operations ran.

## Exact source freeze hashes

| Owned path | SHA256 |
|---|---|
| src/Hub.Api/Features/ReviewDisciplineSummary.cs | 84576f45ac9dd15f6d660b278947e7dbd436b702cb416f8f15461a32385f544a |
| src/Hub.Api/Features/ReviewDisciplineDefaults.cs | 9f678bd37812fa18384860ca2d1871b83fee57fe5a185070215d95c7df05dffb |
| tests/Hub.Tests/Api/ReviewDisciplineSummaryTests.cs | d42c189323b31cefaae2cb3582fa21b2d72b5438b8cb6fe10f0dcf09585bd621 |
| tests/Hub.Tests/Api/ReviewDisciplineDefaultsTests.cs | 9ef97e19d45c3a50a0b90537ace54ca49d8d6e6cac0a08430f8088104a40c74c |
| web/src/pages/projects/review-discipline-summary.tsx | fbe42aa21743fadbc11710ebdef8cc4fe0f4abeabf6ec439652dbc04fb965bf0 |
| web/src/pages/projects/review-discipline-defaults.tsx | fda608c56b5c4c5cd173dd583d8d7916f5d0bc04660ec5cc77627107a29c5140 |
| web/src/pages/admin/deliverable-review-defaults.tsx | 178e3b1a9053d3e057c095e1a1a4bb4f12dc78f88fd3526ddcba640f95a4d614 |
| web/tests/review-discipline-patterns.cjs | ec64b0bb2a525a7576971b43e50e8a3a64efa0f17710f896dd13f30f8cdc3d90 |
| web/tests/review-discipline-patterns.fixture.cjs | 613c6ef29c4ee29e544e5eabfd1f11daf173a70d6165ee19b759d3d784451a4b |
| web/src/pages/projects/Reviews.tsx | bed350fb457f1671ec355e7330d400546ca7a49b6ca38bc192788bf89d1967ce |
| web/src/pages/admin/Admin.tsx | 7027c9c45dde1afaff114b45d3f6c54f565136d16d230e5604f3d40e14c70ba6 |
| projects-reports/requests/g57-locales.md | 7f845dfd9a8f133610ecf24a93dc579ad06616e1f11088e1ebc245509139ea06 |

**READY/HOLD:** all C source authoring stopped at these hashes; root review/integration/execution required. Existing dirty and untracked work is preserved. No checkpoint/task checkbox, specification decision or acceptance state was changed by C.
