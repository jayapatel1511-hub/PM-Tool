# PM-Tool pause and restart handoff — 2026-10-01

Jay explicitly requested a stop for usage limits and a restart handoff with multiple agents. Pause the goal; do not mark it complete. This file is a checkpoint, not a release acceptance claim. Reverify drift-prone facts before resuming.

## Authority and agreed outcome

Finish packets 028–033 and unresolved 025–027 defects, deliver a safe working synthetic review release, then prepare final company pilot and production deployment. Jay chose **homedev for prototype and through pilot**, designated hostname **pm.engcalchub.com**, and individual local-password accounts until Azure is available. Azure/real Entra remains a later gate. Do not claim synthetic checks prove real company acceptance. Keep one dedicated persistent review database; never copy it into production. User now explicitly requests multi-agent work on restart.

Read the original checkout's `AGENTS.md`, `/Users/jaypatel/Personal Server/AGENT-GUIDE.md` before operations, current `spec-parts/` and Spec Kit packets, each packet's verification record, and `docs/reviews/2026-09-27-review-release-gates.md`. Earlier entries in verification/release records describe earlier slices; use the newer dated checkpoints without treating old claims as current proof.

## Git and preservation

- Original checkout: `/Users/jaypatel/PM-Tool`, local `main` at `2b3cff7`. Untracked `.specify/`, `AGENTS.md`, `docs/`, `src/`, `tests/`, `web/` were preserved. Do not reset, clean, stash, switch or overwrite this checkout.
- Reuse isolated managed worktree: `/Users/jaypatel/.codex/worktrees/pm-review-release/PM-Tool`, branch `codex/pm-review-release`.
- Last implementation head: **`cdbdc483715f029bb6d1b863e3461bffab37fe56`**, pushed and clean before this handoff-only commit.
- Draft PR **#13**, open and unmerged: https://github.com/jayapatel1511-hub/PM-Tool/pull/13 . Attach it in a new chat when continuing it.
- Remote main reverified through GitHub at `239ac521bf7fd0d00626cc0c4f54a9129b32d196`; local `origin/main` matches. The Tuesday merges are present. `44dd33b` is the earlier review seed lead, not latest implementation.
- Other worktrees (`landing-login`, `tuesday-branding`, `tuesday-pr-review`) exist; preserve them.
- This handoff commit follows the implementation head. Check `git log`/status and exact PR CI again; do not reuse an earlier PASS for a newer head.

## Latest implementation and evidence

| Commit | Bounded result | Checks actually run |
|---|---|---|
| `a865b48` | Weekly promise proposal, performer signature, PM snapshot and outcome browser commands; scoped detail API with action permissions/history | Affected readiness PostgreSQL API class 10/10; frontend build/lint; Chrome chair/performer separation, unassessed signature HTTP 400, unchanged Proposed state/history; settled refusal dialog axe 0 violations/0 incomplete; CI PASS run 36916631541 |
| `897304d` | Work-scoped readiness inspector, owner output definition and PM/lead applicability forms | Build/lint/diff; Chrome withheld chair output creation, owner-created assessment, mandatory Production Owner, reason/evidence persistence after reload; unknown inputs retained Needs Assessment; axe 0/0; CI PASS run 36921511742 |
| `cdbdc48` | Separate removal-owner evidence proposal and affected-owner verification browser workflow | Build/lint/diff; Chrome created constraint, proposed resolution remained Not Ready, removal owner could not verify, affected owner verified and retained evidence/attribution; remaining unknown inputs returned Needs Assessment; axe 0 violations/1 incomplete on an inspected aria-hidden non-text status glyph |

CI for `cdbdc48`: https://github.com/jayapatel1511-hub/PM-Tool/actions/runs/36922432139 . It was **IN PROGRESS** at handoff preparation. Recheck its conclusion and the later handoff commit's run.

Earlier committed design-basis fixes are `8702d35` (guarded Proposed edit), `dfcc293`/`0713ebc` (preserve and navigate source decision), `f8c0c4a` (duplicate inspection for edits/replacements), `b85d8ef` (optional source-decision selector). Focused basis API tests passed 7/7 and exact-head CI passed on `b85d8ef`; the latest browser evidence is in packet 031 verification. Earlier combined local/CI suites have dated records; this pause did not rerun the full suite.

## Local preview and synthetic data

- Local dedicated PostgreSQL: existing `pm-tool-db-1` container, loopback port `55432`, DB `hub_review_local`, user `hub`. Colima/container were started without replacing/deleting volumes.
- The API owned by this run on **5085 was stopped** for the pause. Do not stop or replace another agent/user preview on 5083.
- To restart the reviewed local preview after verifying DB health, run from the isolated worktree:

```sh
npm --prefix web run build
ASPNETCORE_ENVIRONMENT=Development Seed__ReviewDemo=true \
  ConnectionStrings__Hub='Host=127.0.0.1;Port=55432;Database=hub_review_local;Username=hub' \
  dotnet run --project src/Hub.Api --urls http://127.0.0.1:5085
```

Do not run frontend builds concurrently with .NET compilation: both touch generated `wwwroot`, and earlier overlap caused missing static asset failures. Build sequentially. Build/test writes in the managed worktree may require sandbox escalation.

Synthetic project DEMO-101 ID: `01a0e4a0-be49-7f07-8731-6bb1e3d506f2`.

- Jay: `01a0e4a0-be39-7a74-bd6f-9b7cf072cc65`, `jay@hub.test`.
- Taylor: `01a0e4a0-be38-73a2-a7c2-1f37bf10f4d0`, `taylor@hub.test`.
- Yagmur: `01a0e4a0-be3a-70ec-af84-701cfef42b4d`, `yagmur@hub.test`.
- Jay task T0001: `01a0e4a0-be8f-7ca8-9217-4dba0b1ac373`.
- Taylor-chaired Jay promise: `01a0f8fd-27e0-7796-b608-fb9a6a361185`, remains Proposed; signing was correctly refused. Do not silently change its original output/criteria.
- T0001 now has a synthetic assessment and a reasoned Not Applicable Submission Gate. Other checks remain unknown; it is not Ready. Applicability alone is not proof of satisfaction.
- Constraint `01a0f929-0d3f-7b2d-86ae-8d6ab3a43724` is Verified Removed, Taylor removal owner/Jay verifier, with synthetic `example.test` evidence.
- B001–B004 and prior synthetic handoff/review/submission/allocation edits remain in the database. Inspect current records; do not reseed/reset to erase reviewer edits.
- Private ignored backup `data/backups/hub-review-local-20260928T085540Z.dump` is mode 600, 476,963 bytes, earlier isolated restore PASS. It **predates** later basis/readiness edits; take a fresh dump before risky work.

Temporary local rehearsal scripts exist at `/tmp/pm-weekly-browser.cjs`, `/tmp/pm-readiness-browser.cjs`, `/tmp/pm-constraint-browser.cjs`, `/tmp/pm-constraint-axe.cjs`. They were adapted to resume existing synthetic records after harness selector/CSP failures; do not blindly rerun them as fresh acceptance tests. Playwright/axe are in web/node_modules; Chrome executable `/Applications/Google Chrome.app/Contents/MacOS/Google Chrome`. Development auth uses sessionStorage `hub.devUser` plus `hub.lastActivity`. Instrument axe through browser context `addInitScript`; the app correctly rejects inline script injection under CSP. These are local Development-auth checks, not real login acceptance.

## Homedev and secrets

Read `docs/runbooks/homedev-review.md` and existing scripts before acting. Host base `/home/jaypatel04/Workspace/Projects/pm-tool`; shared `.runtime` and `data`, release directories and `current` symlink. Last verified active release was **`1c59e334b42822510dd0181f83e5dadc7bbe8282`**, private loopback API 3080 and dedicated review DB container. `840edb8` was staged inactive earlier. Neither old activation command is the latest reviewed release. Never run an old sudo command as though it deploys this head.

- `.runtime/review.env`, `.runtime/review-users.json`, persistent `.runtime/keys`, and the generated private password handoff file are outside Git. The handoff filename must be discovered privately on host; do not print contents or commit passwords/verifiers. Synthetic emails/IDs may be in source; working passwords may not.
- Earlier correct-password probe against the old running image returned 401: the verifier file was prepared after startup and not loaded. Requires activation/restart of the reviewed image, then fresh successful sign-in, wrong-password denial, logout and browser/API tests.
- Jay's account requires interactive sudo for Docker; never ask for its password, loosen the socket, or add Docker privileges to avoid the gate.
- LAN alias `homedev` returned **Network is unreachable** on Oct 1. Alternate `homedev-ts` reached a Tailscale SSH approval gate then timed out. A user approval question was sent; **no approval was received** before handoff. Its one-time URL may expire. Reconnect and request fresh approval only if still required. Keep host-key checks; do not guess that Tailscale authentication succeeded.
- Dedicated Cloudflare tunnel configuration was created/validated but last observation had no active tunnel or DNS route. `pm.engcalchub.com` last DNS probe had no answer; reverify before stating it is live.
- Manual homedev DB dump/isolated restore and encrypted off-host retrieval PASS earlier. Dedicated daily timer was last observed not installed; Mac off-host scheduled restic export failed (SSH 255). Scheduled recovery remains FAIL/UNPROVEN. Prepared root-owned helper/service/timer are in `hosting/`; exercise and prove automatic execution, fresh restore and encrypted retrieval.
- Stage only exact reviewed committed source with `scripts/prepare-homedev-review.sh`. Preserve shared review data. Take a fresh private dump before activation; verify migration compatibility, version, health/host/origin/auth, persistence and rollback. Publish DNS/tunnel only after private gates pass.

## Remaining implementation/acceptance: audit before assigning

These are concrete leads, not an exhaustive completed audit:

1. **032:** successful browser signature/snapshot/Met/Not Met/Withdrawn lifecycle has not been rehearsed. Scoped assumption-exception UI is absent. Source-derived Submission Gate and FR-RDY-02 direct/bulk task-start warning/PM-lead authorisation remain unresolved; pending product decisions concern canonical submission relationships and which non-Ready states may be authorised. Do not invent those decisions or create a circular gate using the output's own submission. Constraint record links/cancellation browser flow, exports/search and notifications remain open.
2. **Newly identified next bounded deliverable:** readiness/constraint and weekly promise APIs currently have no notifier calls. Inspect shared FR-MDC requirements and catalogue before adding transactional, retry-safe, permission-filtered notifications. Reuse `src/Hub.Api/Infrastructure/Notify.cs`, `NotificationEvents` in `src/Hub.Domain/Settings.cs`, and established allocations/submission/issue patterns. No notification change was started before pause.
3. **031:** complete browser/concurrency/permissions acceptance for impacts/adoption, conflicts, assumptions, source-decision reopening, withdrawal, template copies and export parity; distinguish existing implementation from remaining verification.
4. **033:** pending issue classification/product decision, full multiple-location/document-revision grouping and verification workflows, actual notice/email behavior, manual accessibility and all acceptance/edge cases.
5. **025–030:** integration acceptance and unresolved source/lifecycle/concurrency/migration defects. Handoff/review/submission/allocation/coordination slices have earlier evidence; a responding preview or mocked browser CI does not prove all scenarios. Audit current specs and actual diff before concluding.
6. **Environment:** current-image private password sign-in, deployed browser-to-API flows, populated migration rehearsal, persistent review edits across release, backup schedule/restore/retrieval, security walkthrough, accessibility, monitoring/mail/resource/rollback checks and hostname access controls.
7. **Later:** real Entra/company tenant acceptance, final company pilot and consequential production deployment require external resources/approval. Do not count synthetic evidence as pilot acceptance or restore review data into production.

## Gate snapshot at pause

| Gate | Status | Limit |
|---|---|---|
| Latest three code slices | PASS locally | Scoped tests/browser evidence above; full 025–033 code/acceptance still incomplete |
| Merge | UNPROVEN / not performed | Draft PR remains open |
| Current code CI | UNPROVEN pending run | `cdbdc48` in progress at preparation; recheck exact newer handoff head |
| Intended review environment | BLOCKED | Connectivity/auth approval and interactive sudo; current release state unverified |
| Individual deployed sign-in | FAIL at last actual probe | Old image returned 401 for prepared credential; fresh image untested |
| Hostname deployment | BLOCKED | DNS/tunnel/access not established |
| Scheduled backup | FAIL / UNPROVEN | Manual restore passed; scheduling not proved |
| Security/accessibility/operations | UNPROVEN overall | Scoped guards/axe/restore evidence only |
| Pilot accepted / production deployed | BLOCKED / not performed | Company/tenant resources and approvals remain |

## Multi-agent restart discipline

Use only configured available slots, with one writer per subsystem and isolated worktrees where parallel writes need them. Main agent owns integration and combined acceptance. Give each helper a bounded task, explicit files and acceptance checks. Start by rederiving Git, CI, specs, tests and host state. Useful parallel lanes: read-only remaining acceptance audit; bounded 032 implementation; operations/source review. Do not let two writers edit the same readiness/notification files. Follow the deliverable loop: acceptance checks, implement, smallest meaningful tests, actual diff/behavior review (permissions/concurrency/data/migrations/browser), fix/retest, integrate/review together. Finish independent work before asking only for specific external blockers. Never mark the goal complete based on usage limits.
