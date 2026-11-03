# G53 / checkpoint25 R1 review finding register — fixed read contract

Status: **ALL SOURCE READY / HOLD; UNCOMPILED / UNRUN.** C owns only NEW `src/Hub.Api/Features/ReviewRegister.cs`, NEW `tests/Hub.Tests/Api/ReviewRegisterTests.cs` and this document. All existing source/UI/schema/shared files stay HOLD. Root owns `ReviewRegister.Map(api)`, the register-only exact locator/optional Context seam, frontend integration, Text/i18n, generated contracts and execution. No execution/helpers/build/generation/schema/dependencies/service/QA24 mutation is authorized. The preserved QA24 API/database must remain untouched.

## Request

GET `/projects/{projectId:guid}/review-findings` through the separate public `ReviewRegister.Map(api)` class. It requires register_screens in both handler and route metadata, plus normal project visibility. It has no write/Seen/receipt/storage side effect. Existing Reviews routes are unchanged by this lane.

Query DTO `ReviewRegisterQuery`: `disciplineId?` (project-discipline ID), `severity?` (Blocking/Advisory), `originatorKind?` (User/ExternalParty), `originatorId?`, `resolverId?`, `allRounds?`, `page?`, `pageSize?`. Omitted originatorKind means User when filtering an ID. ExternalParty is a recognized discriminant returning no current rows; no party/user is fabricated. Empty/foreign filter IDs never expand scope. Normal paging is page>=1, size1–200, default50. Stable order is package key, round number, finding CreatedAt, finding ID. Four filters combine with AND.

Default scope is **current-round unresolved** for every authorized non-deleted package: Open, Responded, and Blocking Withdrawn with no coordinator acknowledgement. The last case has `pendingWithdrawalAcknowledgement:true`. Approved advisory/unresolved rows and Cancelled/Superseded-package rows remain visible, with genuine packageStatus and no terminal-package action. `allRounds=true` explicitly includes the same unresolved statuses from preserved earlier rounds, each with its actual finding/round/carriedFromId. Default counts never double count an old finding and its current carried copy. Verified Closed and fully acknowledged/Advisory Withdrawn are outside this unresolved register.

## Response

All new public DTO names begin `ReviewRegister`.

`ReviewRegisterPage`:

```text
{items:ReviewRegisterRow[],page,pageSize,totalCount,allRounds,
 filters:{disciplineId,severity,originatorKind,originatorId,resolverId},
 options:{disciplines:ReviewRegisterDiscipline[],severities:string[],
          originators:ReviewRegisterOriginator[],resolutionOwners:ReviewRegisterPerson[]}}
```

`ReviewRegisterRow`:

```text
{id,packageId,packageKey,packageTitle,packageStatus,roundId,roundNumber,isCurrentRound,
 projectDisciplineId,disciplineName,disciplineState,
 source:{id,key,revision,deliverableId,available},text,severity,status,response,rowVersion,
 createdAt,carriedFromId,pendingWithdrawalAcknowledgement,
 originator:{kind,userId,externalPartyId,name,state},
 resolutionOwner:{id,name,state},verifier:{id,name,state},coordinator:{id,name,state},
 actionability:{isCurrentRound,isPackageLive,canAct,nextAction,nextActor,reason}}
```

`ReviewRegisterPerson` is `{id:Guid?,name:string?,state:"Active"|"Inactive"|"Unknown"}`; known real IDs and inactive names are retained, never replaced by a parent/coordinator or guessed display-name match. `ReviewRegisterOriginator` has the discriminated userId/externalPartyId pair, currently kind User with only its actual User ID. No email, private user facts or external sign-in identity is returned. `ReviewRegisterDiscipline` is `{id,name,state}` for actual project disciplines; inactive ones remain labelled.

`options` comes only from the full authorized unresolved query for the selected current/all-rounds scope, before applying the four selected filters and before paging. It is not a directory and does not shrink to the current page. `totalCount` comes from the fully filtered query before paging. Parent/package, actual round/package/project, source/project/declared deliverable and finding-discipline/project relationships must agree; malformed foreign joins contribute no rows, counts or options. A deleted same-project deliverable can leave a valid immutable source reference available as history; its live deliverable ID is withheld and `source.available=false`.

Actionability describes the **canonical primary next action**, not an exhaustive set of command permissions or a new command capability. Reuse the read-only ReviewFindingContextData facts and RecordContextRules evaluator, adding actual person states and today's viewer authority. `canAct` is true only for that named eligible viewer on a live current-round primary action. Historical/terminal/read-only/ineligible/unknown/provenance-blocked rows are non-actionable. Reasons are stable codes: `historical_round`, `package_terminal`, existing permission reason keys, `not_assessed`, `not_your_action`, or `person_unavailable`. `nextAction` uses existing RecordContextAction strings; nextActor uses ReviewRegisterPerson. Opening a row/detail stays available independently of canAct. Root's existing detail commands remain the authority for return/withdraw/reassignment and every actual mutation.

## Root integration and proof boundary

Root already reports the uncompiled Map/locator/parent-reader seam authored. Tests in this lane will cover register-only, pattern-only, both-off and both-on locator behavior without editing root source. New filter/severity validation can reuse existing `error.invalid`/`error.one_of` Text keys; no new server messages are needed for this read contract. UI reason-code wording is root-owned.

Focused source tests will cover two packages, >1page and full options/counts, four filters and combinations, current/carried historical IDs, pending acknowledgement, advisory/terminal read-only cases, genuine active/inactive/unknown labels, deleted/mismatched parents/rounds/sources/disciplines, restricted cross-project privacy, flag-off404, ReadOnly read parity and zero business/Seen/receipt writes. Tests are authored only, UNCOMPILED/UNRUN until root freezes all writers and executes independently. S35 remains OPEN: R2 response outcomes/export and R3 external provenance plus UI/runtime acceptance are still required.

## Source handoff

Both new source files are now READY/HOLD. The only application/test files authored by C for this R1 loan are [ReviewRegister.cs](/Users/jaypatel/.codex/worktrees/ef94/PM-Tool/.build-052-053/src/Hub.Api/Features/ReviewRegister.cs) and [ReviewRegisterTests.cs](/Users/jaypatel/.codex/worktrees/ef94/PM-Tool/.build-052-053/tests/Hub.Tests/Api/ReviewRegisterTests.cs). No previous Reviews/import/parser/export/UI/schema/shared file was edited. No application/runtime/test/compiler/browser/API/database/generator/helper execution occurred, and no service/QA24 producer, database or artifacts were stopped, deleted, restarted or overwritten. Two no-index whitespace inspections emitted no diagnostics (exit1 denotes the new-file differences); readback/type inventory and SHA256 are source checks only.

| Owned file | SHA256 |
|---|---|
| `src/Hub.Api/Features/ReviewRegister.cs` | `b0063c687f8927ce461aef53c26e4297a595a27b6ad8803504c57bc9ce8aaee1` |
| `tests/Hub.Tests/Api/ReviewRegisterTests.cs` | `1bfad4cf6edc0fe70f76027c982e2809d99dc646f378c6987e3c1f2f2efbae53` |

Implementation uses a consistent read snapshot. Its relational scope validates finding/package/project, actual round/package/project, source/project/declared deliverable and finding-discipline/project before counts/options/paging. The private member-initialized join projection keeps subsequent filters/projections server-side. It uses distinct authorized option projections rather than materializing every package detail. Huge page offsets return an empty page without integer overflow. Page actionability reuses the read-only package-batched ReviewFindingContextData and pure RecordContextRules, then checks actual person states/current named viewer eligibility. It invokes no command/Recompute/SaveChanges/Seen writer and holds no write lock.

Authored coverage is twelve methods / twenty-four xUnit cases. Genuine command fixtures create two packages and six findings across three pages; filter/options/count/order assertions cover all four filters and combinations. The test clock advances deliberately for source CreatedAt order, avoiding UUID-timing assumptions. Other cases cover current versus carried historical identity, Draft current-round response eligibility, pending Blocking withdrawal acknowledgement, actual Responded verifier and Verified Closed exclusion, Approved advisory findings, Cancelled/Superseded read-only rows, inactive and missing-name real people, ReadOnly primary-action denial versus resolver/coordinator authority, malformed foreign round/source/discipline/declared-deliverable joins, deleted package and source-work history, restricted-project non-disclosure, four register/screens-pattern locator combinations, invalid filters and business/private Seen/receipt counter parity. Terminal legacy-state, missing-name/deletion and malformed-FK changes are isolated synthetic rule/corruption fixtures, not claims about normal command behavior or real-QA evidence.

Remaining root work: independently review these files and the root-owned Map/locator integration; wait for all C# writers to hold; compile in the isolated-output plan that protects preserved QA24; run FullyQualifiedName~ReviewRegisterTests and the affected existing ReviewFindingContextTests/ReviewChangeTests cases. Root alone generates contracts and integrates/tests the UI, cache/query-key invalidation, exact focus/filter/scroll return, flag combinations and real disposable-QA joins. No new server Text keys are required. This partial backend read cannot close S35, FR/AC-SCR-61 or62; R2/R3 and full UI/runtime/authority proof remain required.
