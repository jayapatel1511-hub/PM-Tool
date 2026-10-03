# Combined review release candidate — 2026-10-02

Live synthetic review: `02ca7cd9ae097867bcd4c11c0614bf8d0d9d1619`, activated 2026-10-03 01:03:16 UTC (2026-10-02 Halifax). [PR #21](https://github.com/jayapatel1511-hub/PM-Tool/pull/21) merged at `c220353da35bb4e7b56772f2405b483ad97f50d9`; its source tree matches the deployed candidate. Exact-head and merged-main CI passed 809/809. The narrow-screen check below found a remaining shared control-layout defect; a follow-up is being verified before broader release acceptance. Homedev remains the review, pilot and production target, with individual passwords and existing services. Azure and new paid services are not release prerequisites.

## Integrated corrections

- `36b6cd5` / `8b3bdd3`: discipline-scoped count labels, pending versus acknowledged assessments, complete print layout, linked-task counts, and semicolon/tab/formula-safe CSV cells.
- `0694c28`: an audited Admin-role removal survives startup; hidden-project time exports do not write to that project's history.
- `c70cedf`: corrected handoff revision B can be incorporated while A remains the registered head; registering B reuses the same immutable evidence. A newer published C still requires approved retention of B. Existing legacy snapshots retain published lineage without evidence edits. A changed deliverable cannot resubmit stale unpublished evidence under the same revision/link. Unchanged drafts preserve submission sign-offs; handoff changes refresh project/workspace coordination; print excludes saved-view controls.
- `7c412b0`: canonical section 10.8 precedence keeps Needs Assessment when unknown checks coexist with a constraint, while retaining all blocker reasons and preventing an assumption from overriding the constraint. Recovered the prior unfinished writer diff without changing its worktree.
- Previously committed `bad0e7a` / `3c335d8`: unavailable owners/disciplines become assessed readiness failures instead of breaking reads; HTTPS redirect and least-privilege containers are live in the verified `02ca7cd` activation below.

## Verification actually run

| Check | Verdict | Evidence and limit |
|---|---|---|
| Independent handoff/coordination review | PASS | A stale-evidence reuse finding was reproduced, fixed at the shared snapshot boundary, and reviewed again; no remaining actionable findings in that bounded diff. |
| Final handoff/change PostgreSQL regressions | PASS | 52/52, including corrected B, legacy B, overtaken B, stale source evidence, source-scoped assessments and no-op draft preservation. |
| Readiness/task-start/coordination focused regressions | PASS | 266/266, including the state-precedence matrix and API constraint/unknown/start-authorisation case. |
| Final combined PostgreSQL/domain suite | PASS | 808/808 on application code `7c412b0`; rules branch coverage 96.5% (95% gate), service line coverage 93.3% (70% gate). |
| Backup/pilot safety regressions | PASS | 4 backup-helper, 3 pilot-initializer and 7 pilot-operation tests. |
| Frontend build/lint | PASS | Build and lint exited 0; existing warnings remain. |
| Chromium UI regression harnesses | PASS | Handoff, review/change and design-basis workflows; mocked APIs, not hosted acceptance. |
| Spec traceability | PASS | 612 IDs, 236 sections, none uncited or unknown. |
| Encrypted off-host recovery | PASS, manual | Explicitly approved Mac restic snapshot `2582b812`; database dump, verifier/config and protection-key recovery archive retrieved with identical SHA-256. Retrieved dump restored into a separate temporary Mac database: 3 projects, 3 users, 27 migrations. Temporary database/plaintext copies removed. Source dump: `hub-review-20261002T221105084982Z.dump`. |
| Earlier candidate and merged-main CI | PASS, historical | Candidate `7995e88`: [run 37077497095](https://github.com/jayapatel1511-hub/PM-Tool/actions/runs/37077497095). Merged main `db2f09fd`: [run 37078323246](https://github.com/jayapatel1511-hub/PM-Tool/actions/runs/37078323246). |
| Merge | PASS | PR #13 merged 2026-10-02 23:35 UTC; PR #20 and #21 also merged (exact revisions below); original dirty checkout and helper worktrees preserved. |
| Homedev activation | PASS | `02ca7cd` running at 01:03:16 UTC; root activation log verifies exact image and existing `pm-tool-review-db`. Private probes 10/10 and public probes 9/9 passed. Public HTTP redirects 307 to HTTPS; trusted TLS and headers passed. Pre-activation private dump: `hub-review-20261003T010259175345922Z.dump`, 936098 bytes, mode 600. Browser sign-in/session and the original synthetic persistence comment survived activation. |
| Automatic recovery | UNPROVEN | Review timer's first automatic run is scheduled for 2026-10-03 22:00 UTC (19:00 Halifax). Manual restore is not an automatic-run result. The independent whole-server Mac export failed at 2026-10-03 01:30:06 UTC: its export-only SSH command timed out after two hours. No successful whole-server snapshot or restore is claimed. Its earlier growing-archive observation was not completion. This PM-only snapshot is a separate successful manual recovery check. |
| Monitoring | Prepared, not saved | Uptime Kuma health/Healthy monitor is ready; specific UI configuration approval remains pending. No notification destination is configured. |
| Company pilot / production | Prepared only | Exact `02ca7cd` source staged in a separate `pm-tool-pilot` base on homedev, with owner-only runtime/data/backup directories. Runtime remains empty; no company database, account or service was created. Real participant identities/roles, company workflow acceptance and final go/no-go remain required. Review data must never populate the company database. |

Raw logs are retained locally under `/private/tmp/pm-final-combined-*`, `/private/tmp/pm-handoff-final-focused.log`, `/private/tmp/pm-readiness-precedence-focused.log`, `/private/tmp/pm-final-web-*`, and the private off-host evidence JSON files. No credentials or dumps are committed.

## Additional local acceptance after merge

A test-only follow-up adds valid-coordinate persistence coverage (1/1): exact declared X/Y/Z, CRS and units survive API read-back. At that checkpoint, application code and staged release `7995e88` were unchanged; the earlier 808/808 result is not presented as a new 809-test local run.

- Corrected handoff A → B: real browser/API on an isolated copy of the retrieved review dump, Development authentication. Sender submitted A, receiver returned it, sender corrected/resubmitted B, receiver accepted/incorporated B. Read-back kept registered head A, two immutable revisions and the task Not Started. Native date automation did not commit the date, so that fixture was supplied through the API; hosted password/date-entry acceptance remains unproven.
- Review-purpose allocation: real browser/API on a fresh isolated synthetic database, Development authentication. Taylor proposed 4 reserved hours for Yagmur against 3 explicit review hours, confirmed it as supervisor, reloaded the Confirmed record, and followed its source link to DEMO-101-RV001. Preview showed 8 h available, 1.62 h existing, 5.62 h resulting and zero overload. Editing to 5 reserved hours with a reason returned it to Proposed and kept 3 h review effort after reload. No browser errors. Existing native date defaults were used.

These checks extend local product evidence; they do not prove hosted individual-password, company or assistive-technology acceptance. Credential-free local records: `pm-candidate-browser-acceptance.json` and `pm-review-allocation-acceptance.json` under `/private/tmp/`.


## Final keyboard correction and expanded acceptance

The location-grouped issue panel lost focus to BODY on Escape. The shared Sheet now uses the existing Dialog opener-focus behavior, chains supplied handlers, and restores only a connected opener. This covers all PanelHost side panels. The smallest existing Chromium regression fails on the original implementation and passes after the fix. Frontend production build and focused lint passed. Real native keyboard acceptance returned to each exact opening row in both location groups, including a duplicated issue title; zero browser errors. See packet 033's new verification record.

Packet 031's successful Proceed under Assumption and consuming-work forms now pass on an isolated synthetic real browser/API: the native expiry date is recorded; the exact-version use persists; the assumption remains Proposed. See its new verification record. These checks do not prove hosted password authentication or company acceptance.

PR #20 merged at `dd4ad38537254fda11aaf1d5c72cdd5fc7324c14`. Its exact head `4950665` CI [37080450700](https://github.com/jayapatel1511-hub/PM-Tool/actions/runs/37080450700) and merged-main CI [37081136667](https://github.com/jayapatel1511-hub/PM-Tool/actions/runs/37081136667) passed 809/809, rules branch coverage 96.7%, service line coverage 93.7%. Those results precede the UI-only focus fix; its required exact-head/main CI subsequently passed on PR #21, recorded below. No local .NET rerun was needed for this UI-only correction.

The exact `7995e88` image passed a separate local least-privilege fresh-database smoke check: 27 migrations, seed flags off, no review projects/users, synthetic bootstrap Admin, HTTP 307, Host/Origin/authentication denials, correct sign-in, and key/cookie persistence after recreation. Evidence: `/private/tmp/pm-candidate-image-smoke-evidence.json`. It is a pre-focus-fix image check, not a homedev activation.

At the earlier pre-activation host check, live remained `b155601` and no activation log for `7995e88` existed; the completed `02ca7cd` activation below supersedes that observation. SSH works, but sudo still requires the human's private terminal authentication. Review runtime/data stay intact; the clean company pilot runtime remains empty. The separate whole-server Mac backup was verified live by its owning Python process and growing archive (about 23.7 GB); completion and scheduled recovery remain unproven.


## Hosted activation and narrow-screen acceptance — 2026-10-03 01:20 UTC

- PASS — PR #21 exact-head CI [37083421426](https://github.com/jayapatel1511-hub/PM-Tool/actions/runs/37083421426) and merged-main CI [37084090188](https://github.com/jayapatel1511-hub/PM-Tool/actions/runs/37084090188): 809/809, rules branches 96.7%, service lines 93.7%, all three Chromium harnesses and traceability passed.
- PASS — homedev activation log, exact running image, retained review volume, current pointer, private 10/10 and public 9/9 probes. Normal curl verifies HTTP 307, trusted HTTPS health and security headers. Python urllib's default user agent was rejected by Cloudflare 1010; curl, the public verifier and the actual browser passed. Cloudflare protections were unchanged.
- PASS — actual public individual-password sign-in as synthetic Taylor. Reload after activation retained the session and loaded `index-DFLMgm6D.js`. DEMO-101-T0001 retained its original 2026-10-01 persistence comment and 40% progress. Native Tab → Enter → Escape returned to the exact title opener. No existing task values were changed.
- PASS, bounded — 390px task register's document/body/main widths remained 390px; the task panel's native Escape focus return passed.
- FAIL, reproduced — settled 390px task panel Clear controls end at x=410 beyond x=390. At 320px its unchanged native Start date editor spans x=161 to x=337 beyond x=320. The picker takes the full flex row plus the Clear button, and the fixed label column leaves too little room for the date editor. Shared picker and field-row fixes are under verification; these are accessibility defects, not data loss.

Credential-free live checkpoint: `/private/tmp/pm-release-evidence-02ca7cd.json`; public headers: `/private/tmp/pm-live-public-02ca7cd.json`; hosted persistence/focus/mobile screenshots use `pm-live-*-02ca7cd.jpg`. Source credentials remain outside Git and logs. Pilot runtime remains empty and company acceptance is still unproven.

The subsequent settled hosted reads passed for Weekly Coordination, Readiness and Handoffs with no page errors. Handoffs explicitly showed zero records; Readiness explicitly distinguished zero assessments from a Ready result. Separate public API sessions verified non-Admin 403 for Taylor and Yagmur, and Yagmur’s reviewer permission on the existing task, with no role/task writes. Evidence: `/private/tmp/pm-live-role-checks-02ca7cd.json`. These are bounded read/role checks, not the company workflow pilot.

## Shared control follow-up — local verification

Both Clear controls overflowed their shared picker row, and the fixed field-label column left too little room for the native date editor at 320px. The shared picker now flexes within the available row; shared field rows stack below `sm`. The date editor also associates its existing visible FieldRow label through a React label ID, including nested Due editors; standalone register callers retain their existing title-based name. Save handlers and value comparisons are unchanged.

PASS locally: the existing Chromium TaskSheet regression checks both assigned people's full 32px Clear controls and unchanged native Start/Due editors at 1440, 390 and 320px, verifies their actual accessible names, returns keyboard focus to the exact opener and records zero task writes on cancellation. The layout regression failed before the two CSS fixes; the date-name regression failed before the label association. Both pass after correction, with zero page errors and unmocked GETs. Production frontend build, focused lint and diff check pass; existing lint warnings remain. Independent review found no actionable regression in this bounded diff. Raw logs: `/private/tmp/pm-mobile-fields-{before-desktop,after}.log` and `/private/tmp/pm-date-names-{before,after,build,lint}.log`.

This follow-up is not yet deployed. Exact-candidate CI, merge and hosted narrow-screen retest are separate gates. Manual assistive-technology acceptance and company pilot acceptance remain unproven. Concurrent hosted review edits are preserved; the task's 40% observation above describes the earlier activation check, not an immutable current value.

## Homedev activation of 02ca7cd (2026-10-03)

- **PASS, activation:** Jay activated `02ca7cd9ae097867bcd4c11c0614bf8d0d9d1619` (00:57–01:03 UTC). The image built, a fresh pre-release dump verified (`hub-review-20261003T010259175345922Z.dump`), and the API started on the existing `pm-tool-review-db` volume. All private origin probes passed, including wrong-password denial and individual sign-in. `current` points to `releases/02ca7cd…`; the rollback image is `pm-tool-review:b155601…`.
- **PASS, persistence:** the synthetic marker written through `1c59e33` on 2026-10-01 is still present after two image replacements. Endpoints that depend on the newest migrations (task start-readiness, submission prerequisites, constraint link options, readiness window) return 200.
- **PASS, plain-HTTP redirect live:** `http://pm.engcalchub.com/login` now returns 307 to `https://pm.engcalchub.com/login`; HTTPS health is Healthy.
- **PASS, least privilege live:** the running API process has UID 1000, zero permitted and effective capabilities, `NoNewPrivs: 1` and a read-only root mount, read from `/proc` without Docker access. Its age kept increasing across checks, so it is not restart-looping. It was recreated about two minutes after activation, consistent with the documented verifier-reload step.
- **Open:** first automatic `pm-tool-review-backup.timer` run (2026-10-03 22:00 UTC) and the remaining Low security findings at that checkpoint. The subsequent security candidate is being integrated and reviewed below.

## Concurrent security candidate integration

PR #22 at `d8cd5938` adds remaining Low security guards and regressions; its exact-head CI [37086830696](https://github.com/jayapatel1511-hub/PM-Tool/actions/runs/37086830696) passed. It is integrated with the shared control correction for independent review and combined exact-head CI. Its reported local 836/836 is not presented as a new combined local run. Neither follow-up is live in `02ca7cd`.
