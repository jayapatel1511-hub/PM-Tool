# Tuesday release review loop — 4 October 2026

Worktree: `/Users/jaypatel/PM-Tool/.claude/worktrees/tuesday-visual-batch1`; branch `claude/tuesday-visual-batch1`; baseline `9d3e9b9103081f75b7af580313f4fb17bc87778f`. **Review/fix/software verification PASS. Synthetic review commit/deployment now authorized; preparing release, not yet deployed.** Earlier local-only checkpoint details are retained below.

## Deployment authorization — 4 October, evening

Jay returned home and explicitly answered **“Yes, commit and deploy synthetic review.”** This supersedes the earlier uncommitted/no-deployment hold for the reviewed candidate. Current preparation is for the existing review stack only, preserving unrelated edits and all pilot/operational data. Host preflight verified the current review and pilot pointers at `29b3d7036f529b513fdc2ad47cff62a0b75f0b59`, review origin health HTTP 200 with the configured proxy headers, and the existing tunnel active. That release is an ancestor of the candidate. The candidate's 114 selected files and 315 frozen application/test/build sources had zero drift since final checks. GitHub authentication is valid when checked outside the network sandbox.

Prepare a reviewed commit, incorporate the approved Paper hero commit from origin/main, and pass the required draft-PR CI before transferring the exact committed tree. The approved candidate implementation is committed as `c829f0e6`. Hero merge resolution retains the approved main hero/component/assets and the reviewed sign-in/12 px fixes; two unused incoming Task translation keys are preserved, giving 3,563 unique English keys. No UI flow or authenticated API behavior changes in this integration. Interactive homedev sudo remains a user step. Deployment is not yet claimed complete. T023 stays BLOCKED/UNRUN and human acceptance/pilot remains pending; Jay authorized this synthetic review deployment with that limitation disclosed. Historical local-only status statements below describe the earlier snapshot.

## Repairs and independent review

The first independent review found three planner defects: failed GET reloads could overwrite unsaved fields with TanStack's cached data; visibility/Still valid actions replaced unrelated unsaved fields; and time-away 409 conflicts had no working version reload. Added regressions reproduced draft loss on the original candidate. A stronger regression also reproduced an enabled stale Save immediately after 409.

EntryPanel now checks refetch errors before using data, retains drafts on failed reads, disables writes until an explicit fresh read succeeds, and suppresses the editor after 403/404. Metadata actions update returned versions/status/permissions while retaining other unsaved fields. A subsequent independent review caught stale Retry closures after editing a failed draft. All field changes, including Source and Project, now discard ordinary stale retry callbacks; the current Save/Copy action uses the revised fields. Conflicts retain their recovery action. Inputs and writes are guarded during requests and reloads.

TimeAwayDialog preserves its date/category/hours/action inputs through conflicts and failed reads, keeps Reload available after draft changes, and requires a successful fresh availability read before another command. An unchanged ordinary 500 retry executes the exact same body/idempotency key; revised input or refreshed versions uses a new identity. Pending inputs are frozen.

The planner suite is included in CI. The shared Dockerfile's synthetic banner flag defaults false; only the review Compose build enables it. The independent reviewer caught the initial hardcoded flag because the company pilot shares this Dockerfile; that candidate was corrected before any host action. Dummy-only Compose parsing confirms review=true and pilot=false, LocalPassword unchanged in both, review seed=true and pilot seed=false. The visible copy says “Synthetic preview” so it is accurate locally and in fictional hosted review. No pilot settings were changed.

The final independent source re-review found no actionable defect in the bounded planner/test/CI/build changes. The backend/privacy/permission/migration review also found no actionable defect. Those reviews were source evidence; the parent independently executed the checks below.

## Final checks

| Check | Observed result |
|---|---|
| Exact app type-check | PASS: `npx tsc -p tsconfig.app.json --noEmit --incremental false`. `/private/tmp/tuesday-tsc-release-final.log`. |
| Required lint | PASS: 87 baseline/current warnings, zero new file/rule/message diagnostics. `/private/tmp/tuesday-lint-release-final.{log,json}`. |
| Review frontend build | PASS: `VITE_SYNTHETIC_PREVIEW=true npm run build`. `/private/tmp/tuesday-build-release-final.log`. Existing Vite native-loader/chunk notices remain. |
| Handoffs, Coordination, Design Basis (5186), Resources | PASS, actual exit 0 for each, final built assets and required Chromium executable. `/private/tmp/tuesday-{handoffs,coordination,design-basis,resources}-release-final.log`. Mocked API; no live writes. |
| Planner | PASS: 14 base flows plus six named recovery regressions, 180 API calls, three created entries, two notices, seven axe scans; zero violations/page errors/unknown requests. `/private/tmp/tuesday-planner-release-final.log`. Immediate conflict lock and edited-conflict Reload retention are also asserted. |
| Live affected-route recheck | PASS: 96 cold checks, all six actual preview roles, 1440/1024/768/375. Combined with the retained UX final2 full pass: 1,956 unique combinations, zero findings. `/private/tmp/tuesday-sweep-release-final/{first-pass,rechecks,results,summary}.json`. This is a fresh affected-route recheck, not another full fresh sweep. |
| Source stability | PASS: all 315 application/test/build source hashes unchanged through final verification. `/private/tmp/tuesday-release-source-freeze.json`. |
| English integration | PASS: 3,561 unique keys, zero duplicates; area files/glob/frame tokens remain removed. |
| Local Release publish | PASS: `dotnet publish src/Hub.Api -c Release --no-restore -o /private/tmp/tuesday-release-preflight/api`. API DLL and matching synthetic web assets present. `/private/tmp/tuesday-publish-release-final.log`. Two nullable warnings in unchanged Allocations.cs remain. No app startup/database connection. |
| Deployment configuration | PASS: local dummy-only Compose parsing confirms review/pilot flag and auth/seed separation. `/private/tmp/tuesday-release-compose-check.json`. Activation/preparation/backup shell syntax passes. |
| Spec build/trace/whitespace | PASS after final evidence update; source/trace remain 660 IDs, 252 sections, zero uncited/unknown. `/private/tmp/tuesday-{spec,trace}-release-final.log`. |
| Docker image, Linux runtime, exact-commit GitHub CI, hosted sign-in | UNRUN for this uncommitted candidate. Local Mac publish/configuration checks do not establish these. |

Prior full backend evidence remains 1,036 passed, zero failed/skipped, with coverage/model/migration/scale checks recorded in packet verification. No backend source changed during this loop, so the full database suite was not repeated. Readiness.cs and Allocations.cs diffs remain empty. The separate Readiness.cs:108 defect remains unchanged.

## Concrete candidate and preview

Candidate scope is the Tuesday UI overhaul, packet 034 domain/API/migrations/tests/UI and its maintained evidence, plus planner CI and the review-only banner build configuration. `/private/tmp/tuesday-release-candidate-manifest.json` records proposed paths and hashes; it is not a staged or committed tree. Preserve concurrent `.claude/launch.json`, `CLAUDE.md`, and `docs/SPEC-KIT-WORKFLOW.md`; they are excluded from that proposed release list. Refresh the manifest after further evidence edits before selecting any commit hunks.

[Local preview](http://localhost:5173/login), API `http://localhost:5080`; Development picker users Priya, Sam, Lena, Jordan, Alex and Rita, no local passwords. All preview records are fictional and clearly labelled. The local API remains on `pm-tuesday-preview-db` at 55433. No seed or database write was performed during this loop. The final IAB check opened Sam's long-label planner panel without saving; the title/Close/fields retained the Tuesday layout. Proof: `/Users/jaypatel/.codex/visualizations/2026/10/04/01a104af-b670-70b0-872e-f9915f405535/tuesday-release-planner-review.png`.

## Precise remaining blockers

Jay requested deployment after this loop, but his earlier “leave remaining work uncommitted” and no-homedev-authentication instructions remain unresolved. The [established runbook](../runbooks/homedev-review.md) step 1 transfers only a committed Git tree. Preparing baseline 9d3e9b91 would omit this implementation and all repairs, so it was not used. The [activation script](../../scripts/activate-homedev-review.sh) requires the exact reviewed SHA and interactive `sudo -v` in Jay's own terminal. No SSH/authentication attempt or privilege workaround was made. The intended destination is the existing **synthetic review** stack at `pm.engcalchub.com`; its current hosted state was not reverified. A company-pilot or production release is not implied.

A local commit needs Jay's clarification against his explicit earlier restriction. Host preparation/activation stays held while he is away; activation also needs the reviewed commit/CI, pre-release backup, dedicated-volume check and private/public hosted verification in the existing runbook. Local software passing does not waive these gates.

Packet T023's required manual screen-reader validation remains BLOCKED/UNRUN because native VoiceOver control timed out earlier. It remains unticked; no screen-reader acceptance is claimed. Jay's visual acceptance and the actual-supervisor spreadsheet pilot also remain pending human evidence. These are not worked around by axe or synthetic timing.

The four product decisions remain with Jay: brand mark; hero-board recapture after approval; confirmations for holiday deletion/manual-role removal; per-notification Mark read. They were not silently decided.
