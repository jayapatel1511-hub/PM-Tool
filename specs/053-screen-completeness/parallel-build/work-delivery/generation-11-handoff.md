# Generation 11 — S05-A Submissions handoff

Status: READY/HELD. Backend was already declared BACKEND READY; all six assigned application/test files are now held. No mutation or lane process is in flight. No application/test changes were made outside this S05-A boundary; previous Work slices remain held.

Generation 13 runtime-script repair: mismatch assertions now target `.semantic-pill[data-semantic-tone="warn"]` with the exact Revision mismatch label and optional aria-hidden triangle. D001 recorded A/current B and D002 recorded A/current A are explicitly compared; D002 mismatch count remains zero and manifest total remains one. Other exact-text assertions were inspected; none repeats this pill-label pattern. `node --check web/tests/work-delivery-submissions.cjs` and scoped whitespace PASS. No product/C# changes or browser/runtime/build/test execution; root reruns the browser acceptance. All sources/scripts READY/HELD again.

Generation 12 review repair: root fixed the Supersede disabled expression to preserve useOffer refusal as well as enabled incomplete-data protection. Under a script-only loan, this lane added issued-package read-only-account/Complete-project denied-offer cases in both flags, allowed positive controls, and an allowed-but-incomplete issued capture. Disabled/title/no-form/no-write assertions preserve the original partial-data and exact delivery acceptance. Script syntax and whitespace PASS; browser remains root-owned/UNRUN here. No product/C# edit was made in the loan. All sources/scripts held again; C# FROZEN generation 12 acknowledged.

## Implemented

| Owned file | Final behavior |
|---|---|
| `src/Hub.Api/Features/Submissions.cs` | Omitted-null `ListRow.Delivery` and `SubmissionDetail.Delivery`; enabled list evaluates the complete filtered cohort before blocker-first order/paging, with explicit existing Export.MaxRows ceiling. Enabled list/detail use authorized RepeatableRead snapshots. Existing raw `BlockerCount`, canonical readiness, command guards, fingerprint/issue commands and off response properties/order stay unchanged. |
| `src/Hub.Api/Features/DeliveryScreenReads.cs` | Reuses seeded canonical SubmissionContextData/readiness/check mapping; distinct actual blocking check IDs, separate package/unmapped/unknown problems, checking-owner/discipline facts, exact reason/source context, current manifest status counts, recorded/current revision comparison and explicit issue availability. Manifest/head/revisions/deliverables are scoped to the project and exact source identity; missing/foreign revision metadata is never substituted. No new evaluator/owner/state/persistence model. |
| `web/src/pages/projects/Submissions.tsx` | Enabled summary uses the distinct-check unit; blockers/checks lead before status counts and inline manifest. Existing applicability controls and exact child-version recovery retained. Partial form prefill cannot open with absent manifest/check arrays. Disabled JSX keeps original manifest-first layout and raw count behavior. |
| `web/src/pages/projects/delivery-presentation.tsx` | Fail/Pending owner+discipline groups, unavailable before successful states, exact registered Handoff/Finding/check action links with origin/return, optional nonblocking label, current status counts after checks, keyboard inline manifest A/current B mismatch, preserved issue author/time/destination/transmittal and explicit frozen-vs-live basis. Missing capture/check/manifest/status/issue parts remain unavailable; no percentage or fabricated member/failure/Pass. |
| `tests/Hub.Tests/Api/WorkDeliverySubmissionTests.cs` | Six authored API facts: normal 1Fail/2Pending/5Pass plus distinct3blockers; optional Fail and immutable issued evidence; exact D001/D002 membership; missing/foreign revision/member recovery; global sort-before-paging/filter/readonly; exact off parity. No direct rewriting of required flags or derived statuses to manufacture the AC. Damaged imported-reference cases are explicitly synthetic fixture corruption. |
| `web/tests/work-delivery-submissions.cjs` | Authored actual-app/mock-API acceptance: exact8states/header3, checking-owner groups, no percentage, keyboard manifest comparison/D002 isolation, exact Handoff/Finding action clicks/returns, summary-only/partial capture recovery, 375/768/1280 dialog reflow and off layout. Live events fixture is explicitly consistent. |

## Canonical boundaries

- `SubmissionReadiness.cs`, `SubmissionContextData.cs`, domain rules, entities, command handlers, permissions, generated files and other Work UI were read-only.
- The read adapter accepts the detail's existing readiness/checks and seeds the canonical context adapter, avoiding a second evaluation of the same detail. The enabled list does not call legacy Rows and then evaluate again; each package has one live readiness evaluation. Existing context and delivery adapters may separately hydrate actor metadata inside the same snapshot; no broad performance refactor or performance proof is claimed.
- Checking owner remains the check owner even when the source's next actor is its receiving owner/verifier. Optional failure remains visible without becoming a required issue blocker.
- New summary is a display projection; the legacy raw blocker-reason field and existing export binding are retained. Root was informed that an additional display-unit CSV column, if desired, belongs in shared Records/export metadata with an explicit distinct-check label, rather than replacing the raw-reason definition.

## Shared integration

- Root accepted `requests/g11-delivery-contract.md` and applied backend Text `delivery.scope_limit`.
- Root applied the first29 keys in `requests/g11-delivery-locales.json`. One additional key was appended and reported: `delivery.countBasis` (30 total) = `Distinct checks currently blocking issue; package problems are listed separately`.
- Root must refresh API/schema/registry artifacts. No new query or saved-view parameter is required for this increment.

## Verification boundary

PASS: `node --check web/tests/work-delivery-submissions.cjs`.

PASS: parse-only TypeScript `createSourceFile` diagnostics for the two assigned TSX files; no transpilation, typechecking, build or application execution.

PASS: scoped `git diff --check` for the two tracked source files and separate trailing-whitespace scan of the four new files.

UNRUN here: backend compilation/tests; generated API/registry; forced frontend/app/fixture type checks; lint/production build; browser actions/reflow; API-backed or hosted behavior. No helpers/goals, migrations, commits, push, deployment or flag activation occurred.

Root's next focused execution: `FullyQualifiedName~WorkDeliverySubmissionTests` plus affected `SubmissionContextTests`, `SubmissionApiTests`, `SubmissionPermissionMatrixTests`; retain exact child/source-return and immutable issue/fingerprint regressions. After refreshed contracts/build, run `CHROME_EXECUTABLE_PATH='/Applications/Google Chrome.app/Contents/MacOS/Google Chrome' TUESDAY_UI_BASE_URL=http://127.0.0.1:8853 node tests/work-delivery-submissions.cjs` from web, plus existing `submission-context.cjs` and relevant source-return/reflow coverage. Choose a proportionate numeric/cohort check for the new full-scope ordering; the 50k ceiling is explicit, not evidence of acceptable latency.

FR/AC-SCR-05 implementation is authored, not accepted or marked complete. S05 remains unchecked: the later S05-B full Readiness state-ordering increment is not implemented or authorized by this handoff. No user decision status was closed.
