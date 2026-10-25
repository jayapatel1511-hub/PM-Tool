# Packets 039–050: review fixes, 25 October 2026

A whole-codebase review of `codex/integrate-039-050` at `cf3ce8ee` found twelve problems to fix first. This note records the fixes, the checks that prove them, and what remains. The full review report is outside the repository at `~/PM-Tool/.review/2026-10-25-codebase-review.md`.

- Worktree: `~/.codex/worktrees/integrate-039-050/PM-Tool`; branch `codex/integrate-039-050`; base `cf3ce8ee`.
- **Nothing is committed or pushed.** The fixes are uncommitted edits on top of `cf3ce8ee`.
- The same worktree also holds a second session's uncommitted record updates to `specs/039…050/tasks.md` and `verification.md` (the "Ticketing system for overload tasks" session). The two sets of edits touch different files. Those records cite this run where it applies.

## Fixes

| # | Problem | Fix | Proof |
|---|---|---|---|
| 1 | The Admin page read its own empty `ADMIN_EXTRA`, so the Operations and Holidays tabs had vanished since the 042 route-splitting refactor (`46dc7685`). | `Admin.tsx` imports the registry from `app/extension-slots.tsx`. | `web/tests/operations.cjs` now asserts both tabs; it fails on the old page and passes. `web/tests/registrations.cjs` now fails when any registry is declared more than once. |
| 2 | Project Settings read its own empty `SETTINGS_SECTIONS`, so the template-origin section never rendered. | `ProjectSettings.tsx` imports the registry. | The same single-declaration guard. |
| 3 | `HubDb.SaveChangesAsync` ran the task-pickup lifecycle on every save with `flag.task_pickup` off (FR-PKP-13). | The call runs only while the flag is on. | New `Offers_from_an_enabled_period_are_left_alone_while_the_flag_is_off`; fails on the old code. |
| 4 | `GET /projects/{id}/revision-check` threw (500), and the per-minute recheck job failed, for a deliverable with no revision and no issues. | No crash. Such a deliverable has no marks, since FR-VER-16 marks a revision that does not match and there is none. The domain rule is unchanged: a blank issue revision is still marked. | New `Deliverable_without_a_revision_or_issues_reads_without_marks` (also runs `RevisionRecheckJob`); fails on the old code. |
| 5 | The live-update stream retried a refused connection (429 over the five-stream cap, 401, 403) forever, refreshed every cached query before each retry, never reset its backoff, and reconnected on every project navigation. | A 4xx refusal stops the stream, so that tab keeps today's refresh (FR-LIVE-17). The open project refreshes once after each successful reopen. Backoff resets after a stream that lived at least 60 s. The open project is read through a ref. | `web/tests/live-browser.cjs` adds a 429 case: one request and no retry in 4 s. |
| 6 | The "fill open role" deep link reopened its dialog right after Cancel. | The page opens each `?fill=` link once and removes the parameter on close. | `web/tests/resources.cjs` adds the Cancel case; it fails on the old code. The suite's mock also gained the `allocations/review-options` route that the proposal form reads. |
| 7 | My Work and Workload linked open roles with the legacy `view=roles`. | Both now use the canonical `tab=roles` (packet 040 S09). **Correction to the review:** the old link was not broken; `usePageMode` rewrites `view=roles` to `tab=roles` in place. | Type check and lint. |
| 8 | `dependency-merge.yml` enabled auto-merge on every Dependabot patch or minor update with no in-repo gate; `gh pr merge --auto` merges at once when main requires nothing. | A step reads main's branch protection and enables auto-merge only when it requires all eight CI jobs; otherwise it warns and leaves the update for Jay. | YAML parses; the check script was exercised with complete and partial requirement lists. Not run on GitHub. |
| 9 | `InteractionScaleTests` printed its 500 ms verdict and never asserted it, cited the wrong criterion (AC-INT-21) and ran under coverage in CI. | It now asserts ≤ 500 ms p95 for every phase and path, cites §22 "Performance — API", and runs only with `HUB_SCALE=1`; elsewhere it reports as skipped. `tools/ci_tests.py` counts custom fact attributes, so the class stays sharded and the test-count gate still sees it. | See "Performance measurement" below. `tools/test_ci_tests.py` covers the custom attribute. |
| 10 | The four permission matrices logged old-client/server differences instead of asserting them (AC-FIG-08). | Each difference must be a control today's client offers and the endpoint refuses. | 114 matrix tests pass; captured output shows 21 differences, all in that direction (decision 4 assign 7; decision 13 changes 3; decision 7 task 3 and deliverable 3; decision 11 edit 2, due 2, assign 1). `ProjectsTests` already asserted its own. |
| 11 | A muted user still received "Task assigned from an offer", because mute was bypassed for every recipient of a direct-assignment event (§12.18). | `Notifier.Send` takes the people the work is assigned to; only they bypass a Muted follow. Other events keep today's behaviour. | `TaskPickupTests` now expects the muted offerer to be skipped; it fails on the old code. |
| 12 | The admin Audit chains page re-ran verification, which logs an entry and can alert Admins, on every window focus; the nightly job carried a dead initialisation loop that would stop all verification if a chain ever lacked a head. | Verify runs only when the button is pressed. The loop is removed: chains are sealed once at startup by `AuditChainInitialization`. The endpoint stays a GET, as FR-VER-04 calls it a "verification read". | New `Nightly_and_weekly_jobs_verify_every_chain`, the first test of the job; `web/tests/schema-workflows.cjs` passes on the final build. |

Also changed: `tests/Hub.Tests/Api/Factory.cs` now drops its per-factory database even when shape recording throws. A failed run had left 36 `hub_test_*` databases; those and 10 older leftovers were dropped from the local server.

## Checks run on 25 October

| Check | Result |
|---|---|
| `dotnet build Hub.slnx -c Release` | 0 errors, 645 warnings |
| Backend tests, full suite | **1,516 passed, 1 skipped, 0 failed** (1,517 = 1,514 + 3 new); 7 min 30 s. The skip is the on-request scale test. |
| Old-code checks | The new pickup, revision and mute tests, the Admin tab check and the fill check each fail with the fix removed. |
| Web: build, lint, type check, key tests, fixture type check | Pass (lint keeps its existing warnings) |
| Browser suites | Pass: registrations, registrations-browser, route-splitting, live-browser, resources, schema-workflows, operations, planning-reporting, interaction, recovery-counts |
| `tools/ratchet.py` | Pass (enforced counters at or under baseline) |
| `tools/build_spec.py --check`, `tools/trace_spec.py --check` | Pass; 1,299 IDs, 405 sections, 0 gaps |
| Python tool tests (`tools/`, `scripts/`) | 184 passed |

The browser suites ran with `CHROME_EXECUTABLE_PATH` set to the installed `chromium_headless_shell-1248`, because Playwright 1.61.1 expects build 1228, which is not installed on this machine. CI installs its own browser.

## Performance measurement

`HUB_SCALE=1 dotnet test --filter InteractionScaleTests`, Release, no coverage, local TestServer and PostgreSQL: 500 tasks, 100 concurrent readers, pool 20, two warm-up and three measured batches per phase, phases OFF→ON→ON→OFF. **Passed**: every phase met 500 ms p95.

| Phase | Flags | Task list p95 | Picker p95 |
|---|---|---|---|
| 1 | off | 342.4 ms | 56.6 ms |
| 2 | on | 349.2 ms | 95.2 ms |
| 3 | on | 333.6 ms | 88.1 ms |
| 4 | off | 308.9 ms | 45.9 ms |

This is one warmed local run. It does not establish performance over the corporate network or on pilot hardware; the earlier 912 ms result under coverage stays on record in packet 046.

## Not done in this pass

- The review's other findings (sections 6.1 to 6.6 of the report): text outside `en.ts` and `Text.cs`, flags read two ways, the remaining UI/server permission mismatches, rules in two copies, dead code, and the other test and CI issues.
- Governance and decision text, which agents do not change: the constitution 1.1.0 "in effect when" condition, decision RPT-02's outcome wording, `CLAUDE.md` saying Tier B covers all migrations rather than contract migrations, the `AGENTS.md` differences, the missing `acceptance.md` files, and a third-party name in three spec sources.
- Everything gated on Jay in the [19 October follow-up](2026-10-19-039-050-acceptance-followup.md): branch protection before any push, Tier B review and merge, packet 040 S16, packet 043's verdict, packet 044's off-host backup and drill, flag activation and cleanup, and hosted and human acceptance.
