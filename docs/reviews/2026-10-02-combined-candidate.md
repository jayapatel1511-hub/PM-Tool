# Combined review release candidate — 2026-10-02

Application code: `7c412b0`; staged release: `7995e88b95d4a31bc5895faa788026667316f9ad`. [PR #13](https://github.com/jayapatel1511-hub/PM-Tool/pull/13) merged at `db2f09fd93f81b2c7c3718175320e5101083e089`. Latest observation: 2026-10-03 00:02 UTC (2026-10-02 Halifax). This record distinguishes candidate checks from activation and company acceptance. Homedev remains the target for review, pilot and production; individual passwords and existing services are used. Azure and new paid services are not release prerequisites.

## Integrated corrections

- `36b6cd5` / `8b3bdd3`: discipline-scoped count labels, pending versus acknowledged assessments, complete print layout, linked-task counts, and semicolon/tab/formula-safe CSV cells.
- `0694c28`: an audited Admin-role removal survives startup; hidden-project time exports do not write to that project's history.
- `c70cedf`: corrected handoff revision B can be incorporated while A remains the registered head; registering B reuses the same immutable evidence. A newer published C still requires approved retention of B. Existing legacy snapshots retain published lineage without evidence edits. A changed deliverable cannot resubmit stale unpublished evidence under the same revision/link. Unchanged drafts preserve submission sign-offs; handoff changes refresh project/workspace coordination; print excludes saved-view controls.
- `7c412b0`: canonical section 10.8 precedence keeps Needs Assessment when unknown checks coexist with a constraint, while retaining all blocker reasons and preventing an assumption from overriding the constraint. Recovered the prior unfinished writer diff without changing its worktree.
- Previously committed `bad0e7a` / `3c335d8`: unavailable owners/disciplines become assessed readiness failures instead of breaking reads; HTTPS redirect and least-privilege containers await activation.

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
| Exact candidate and merged-main CI | PASS | Candidate `7995e88`: [run 37077497095](https://github.com/jayapatel1511-hub/PM-Tool/actions/runs/37077497095). Merged main `db2f09fd`: [run 37078323246](https://github.com/jayapatel1511-hub/PM-Tool/actions/runs/37078323246). |
| Merge | PASS | PR #13 merged 2026-10-02 23:35 UTC; original dirty checkout and helper worktrees preserved. |
| Homedev activation | Not performed | Live pointer remains `b155601`; SSH works, Docker requires interactive sudo. The live review volume and private runtime are preserved. |
| Automatic recovery | UNPROVEN / FAIL | Review timer's first automatic run is scheduled for 2026-10-03 22:00 UTC (19:00 Halifax). Manual restore is not an automatic-run result. A new whole-server Mac export is currently running (job lock held, about 13.5 GB exported at 00:02 UTC); its completion and scheduled recovery remain unproven. This PM-only snapshot is a separate successful manual recovery check. |
| Monitoring | Prepared, not saved | Uptime Kuma health/Healthy monitor is ready; specific UI configuration approval remains pending. No notification destination is configured. |
| Company pilot / production | Prepared only | Exact `7995e88` source staged in a separate `pm-tool-pilot` base on homedev, with owner-only runtime/data/backup directories. Runtime remains empty; no company database, account or service was created. Real participant identities/roles, company workflow acceptance and final go/no-go remain required. Review data must never populate the company database. |

Raw logs are retained locally under `/private/tmp/pm-final-combined-*`, `/private/tmp/pm-handoff-final-focused.log`, `/private/tmp/pm-readiness-precedence-focused.log`, `/private/tmp/pm-final-web-*`, and the private off-host evidence JSON files. No credentials or dumps are committed.

## Additional local acceptance after merge

A test-only follow-up adds valid-coordinate persistence coverage (1/1): exact declared X/Y/Z, CRS and units survive API read-back. Application code and staged release `7995e88` are unchanged; the earlier 808/808 result is not presented as a new 809-test local run.

- Corrected handoff A → B: real browser/API on an isolated copy of the retrieved review dump, Development authentication. Sender submitted A, receiver returned it, sender corrected/resubmitted B, receiver accepted/incorporated B. Read-back kept registered head A, two immutable revisions and the task Not Started. Native date automation did not commit the date, so that fixture was supplied through the API; hosted password/date-entry acceptance remains unproven.
- Review-purpose allocation: real browser/API on a fresh isolated synthetic database, Development authentication. Taylor proposed 4 reserved hours for Yagmur against 3 explicit review hours, confirmed it as supervisor, reloaded the Confirmed record, and followed its source link to DEMO-101-RV001. Preview showed 8 h available, 1.62 h existing, 5.62 h resulting and zero overload. Editing to 5 reserved hours with a reason returned it to Proposed and kept 3 h review effort after reload. No browser errors. Existing native date defaults were used.

These checks extend local product evidence; they do not prove hosted individual-password, company or assistive-technology acceptance. Credential-free local records: `pm-candidate-browser-acceptance.json` and `pm-review-allocation-acceptance.json` under `/private/tmp/`.
