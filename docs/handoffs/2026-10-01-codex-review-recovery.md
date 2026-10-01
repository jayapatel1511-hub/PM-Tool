# PM-Tool Codex recovery and activation checkpoint — 2026-10-01

Continue from the isolated `codex/pm-review-release` worktree and draft PR #13. The original checkout remains on
`1639968` with its untracked `.specify`, source, tests and web work preserved. The current session used two helper
slots, the runtime limit, with bounded ownership and main integration. No merge or host activation was performed.

## Implemented and verified locally

Recovered template basis suggestions/draft handling, coordination action reuse and affected-work grouping, constraint
and promise keys/links/exports/search, browser focus/validation fixes and the user/admin guides. Integrated audited
Staging local-Admin bootstrap and create-user UI/API, verifier rotation/session invalidation, trusted client-IP parsing,
restricted auto-membership/notification boundaries, current source/review discipline authority, removed basis-approver
checks, and privacy filters across activity/history/exports/dashboard/following/digests. Eight simultaneous first
project evaluations reproduced `23505 pk_task_state`; the per-project transaction lock fixes that race.

Verification on source head `186065fd6369c286b3f84c20aa0c3b31449a54fd`:

- **PASS:** combined PostgreSQL suite 568/568, no skipped tests. Rules branch coverage 96.5% (95% gate), service line
  coverage 93.2% (70% gate). Existing compiler/lint warnings remain.
- **PASS:** frontend build/lint and three Chrome mocked-API suites (handoff, review/change, design basis). No page
  errors or settled-dialog axe violations in the handoff and review/change scenarios checked. Contrast measurement
  waits for dialog animation completion. These do not prove all manual accessibility requirements.
- **PASS:** live Chrome/API on an isolated populated local review copy: constraint key/Verified Removed history,
  prerequisite inspector/focus return, chair cannot sign another person's promise; action reuse survives reload and
  creates no additional action; coordination CSV reconciles all 4 records in the tested projection, print controls
  disappear and a PDF is generated (full visual print review remains unproven).
- **PASS:** live performer promise signatures, Ready-at-signature, frozen snapshot, Met/Not Met/Withdrawn outcomes,
  reload/history/completion evidence. Withdrawal preserves the original denominator of 3. Prerequisite applicability
  was explicitly synthetic fixture setup; this is workflow evidence, not engineering validation or company acceptance.
- **PASS:** complete old homedev-dump migration rehearsal: 20 to 27 migrations, project/user/task/issue counts preserved
  at 3/3/6/0. A separate newer local review copy migrated 22 to 27 with 3 projects, 7 tasks, 1 constraint and 1 promise
  preserved. No pending model changes. Both temporary copies and owned APIs were removed afterward.
- **PASS:** original persistent local review DB remains at 3 projects, 7 tasks, 1 constraint, 1 promise and 22 migrations;
  no migration or rehearsal write was applied there. A fresh private dump was retained outside Git before copying it.
- **PASS:** specification traceability 612 IDs / 236 sections, none missing or unknown; no critical NuGet advisories and
  npm reports 0 vulnerabilities; backup-descriptor hardening tests 3/3 and separate pilot initializer guards 3/3.
- **BLOCKED:** separate HTTPS local-password browser rehearsal stopped at an untrusted localhost certificate. No
  warning bypass or trust change was made. Its isolated API/database/credentials were cleaned up. Integrated account
  API/credential regressions pass, but that is not current hosted password-browser acceptance.

CI passed on earlier `a8cd339` ([run 36937504036](https://github.com/jayapatel1511-hub/PM-Tool/actions/runs/36937504036)).
That run predates the final privacy and pilot-init/CI additions. Require CI on the latest full commit before activation;
check GitHub rather than treating this historical link as exact-head evidence.

## Host and release gates

| Gate | Verdict | Evidence / next action |
|---|---|---|
| Local code checks | PASS | Scoped results above; full application/company acceptance is separate |
| Full packet acceptance | UNPROVEN | Remaining populated/deployed/manual acceptance is in packet verification records |
| Draft PR #13 | Open, unmerged | Preserve draft until acceptance/review gates warrant changing it |
| Review activation | BLOCKED | SSH reachable; `current` remains `releases/1c59e334b42822510dd0181f83e5dadc7bbe8282`; Docker requires Jay's interactive sudo |
| Public review hostname | BLOCKED | `pm.engcalchub.com` has no DNS answer; `pm-tool-tunnel.service` is inactive |
| Backup timer / off-host schedule | UNPROVEN / FAIL | Root helper code hardened; installed timer, automatic run and retrieval still need proof; prior off-host job failed |
| Company pilot | Prepared, not accepted | Separate runtime initializer/volume/stack; real users, approvals, backup/operations and acceptance remain open |
| Production | Prepared, not deployed | Bicep main + all 4 parameter files compile; network/hostname/mail/monitoring contracts added; company inputs and live Azure gates remain blocked |

## Concrete activation sequence

After latest exact-head CI passes, stage only that full committed tree with `scripts/prepare-homedev-review.sh <sha>`.
It preserves shared `.runtime` and `data` and leaves `current` unchanged. In Jay's own homedev terminal, from
`/home/jaypatel04/Workspace/Projects/pm-tool/releases/<sha>`, run:

```bash
bash scripts/activate-homedev-review.sh <sha> --install-timer
```

Sudo credentials must be entered by Jay there; do not request or collect the password, loosen Docker permissions,
or bypass the host authorization boundary. The script builds, takes a fresh private dump, uses the existing review
volume, probes the private origin, installs/tests the root-owned timer and isolated restore, then moves `current`.
Verify the deployed persistence marker from the earlier session, migration count, individual sign-in, version and logs.
Only after those gates pass, activate the dedicated user tunnel and DNS route and check real browser sign-in at
`https://pm.engcalchub.com`, privacy, accessibility and persistence externally. Do not claim public deployment now.

For company pilot preparation use `docs/pilot/pilot-readiness.md` and the separate initializer; never copy review DB,
verifiers or key ring into the pilot or production. Azure template/slot preparation does not prove identity grants,
network access, certificate binding, mail delivery, what-if, recovery or cutover. Keep those gates explicit.
