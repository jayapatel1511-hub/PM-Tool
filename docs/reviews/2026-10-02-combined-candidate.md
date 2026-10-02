# Combined review release candidate — 2026-10-02

Application code: `7c412b0` on `codex/pm-review-release`, draft PR #13. This record distinguishes candidate checks from activation and company acceptance. Homedev remains the target for review, pilot and production; individual passwords and existing services are used. Azure and new paid services are not release prerequisites.

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
| Latest exact-head CI | UNPROVEN | Must check the run for the published candidate; prior `bad0e7a` CI is not evidence for this head. |
| Merge | Not performed | PR #13 remains draft until combined review/CI gates pass. |
| Homedev activation | Not performed | Live pointer remains `b155601`; SSH works, Docker requires interactive sudo. The live review volume and private runtime are preserved. |
| Automatic recovery | UNPROVEN / FAIL | Review timer's first automatic run is scheduled for 2026-10-03 22:00 UTC (19:00 Halifax). Manual restore is not an automatic-run result. Existing whole-server Mac export last failed with SSH 255; this PM-only snapshot does not repair that separate job. |
| Monitoring | Prepared, not saved | Uptime Kuma health/Healthy monitor is ready; specific UI configuration approval remains pending. No notification destination is configured. |
| Company pilot / production | Prepared only | Separate clean pilot stack/scripts and recovery checks exist. Real participant identities/roles, company workflow acceptance and final go/no-go remain required. Review data must never populate the company database. |

Raw logs are retained locally under `/private/tmp/pm-final-combined-*`, `/private/tmp/pm-handoff-final-focused.log`, `/private/tmp/pm-readiness-precedence-focused.log`, `/private/tmp/pm-final-web-*`, and the private off-host evidence JSON files. No credentials or dumps are committed.
