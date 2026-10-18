# Verification: Delivery Process

Slices S01, S02, S09 and S10 built and merged into the integration branch; other slices not started.

Per FR-DLV-24, this file records checks until `trace_spec.py --report` (slice S11) is published on main; after that, this packet's verification is the report of the CI run for the deployed commit, and human checks go in `acceptance.md`.

| AC | Slice | Status |
|---|---|---|
| AC-DLV-01 | S05 | UNRUN (human-only: Jay applies the settings with the `gh api` commands in `docs/runbooks/delivery.md` and records the readback in `acceptance.md`) |
| AC-DLV-02 | S01 | PARTIAL: passes locally against a temporary git repo (350 hand-written lines plus an 11,000-line Designer file pass with no warning, 450 warns and passes, 1,050 fails, 1,050 with label `large` passes); not yet run in GitHub CI on a real pull request |
| AC-DLV-03 | S01 | PASS (local): `git check-attr` reports `linguist-generated` set and `diff` unset for a Designer file, the snapshot, both combined spec files, `TRACEABILITY.md` and `package-lock.json`; nothing else in `git ls-files` is marked |
| AC-DLV-04 | S09 | PASS (local): FlagsTests: a one-entry list gives `flag.demo` as an on/off setting, default off, group `flags`; the empty list adds nothing to `OrgSettings.Defs` |
| AC-DLV-05 | S09 | PASS (local): FlagsApiTests: `/me` keys are the existing ones plus an empty `flags` map; Admin settings keys equal `OrgSettings.Defs` and each row has only key, kind, group, default and value |
| AC-DLV-06 | S09 | PARTIAL: guard off/on logic (404 while off or missing, passes when on) tested on settings built in the test; endpoint wrapper not exercised through HTTP because no flag is declared; client hook treats a missing map or name as off, but browser suites not run (no port assigned) |
| AC-DLV-07 | S09 | PASS (local): retirement check names a flag whose `acceptance.md` records its pilot sign-off and passes without one; also run on the real (empty) list against `specs/<packet>-*/acceptance.md` |
| AC-DLV-08 | S09 | PASS (local): pair check names the flag and the missing tag (`flag-off` or `flag-on`) and passes when both exist; also scans the test assembly traits for the real list |
| AC-DLV-09 | S09 | PARTIAL: list is empty, full backend suite passes (1164/1164 on the slice), no existing route, setting or response gains a flag check; browser suites and coverage gates not run |
| AC-DLV-10 | S02 | PARTIAL: `tools/test_ci_tests.py` shows every API class in exactly one of 3 shards, a new class file lands in a shard without edits, and a new `ci_steps.txt` line runs in its own job only; on the merged tree domain 635 + shards 162, 227 and 159 = 1183 = `dotnet test --list-tests`; three consecutive green GitHub runs not yet done |
| AC-DLV-11 | S02 | PASS (local): `tools/test_coverage_gate.py`: a line hit only in the second file is counted once; one file gives the same result as before on a fixture and on real coverage files; merging the 4 real files from the builder run clears both gates (96.3 % branches, 95.0 % lines) |
| AC-DLV-12 | S03 | PARTIAL: `tools/test_ci_tests.py` (11 count tests) covers a missed shard (fails and names the cases), a case run twice, a missing test list, a drop below main for backend and browser suites, the `replace-test` label, a first run that passes and publishes, and the real step list; locally 1183 listed = 1183 run (635 + 162 + 227 + 159) and removing api-3 names the 159 missing cases; the GitHub run, main artifact download and label event not yet proven |
| AC-DLV-13 | S07 | PARTIAL: on the Mac the label and `HUB_BUILD` check (`tools/ci_tests.py image`) passed with fake inspect data and with real local images built with and without `RELEASE_SHA`, and the image built without the argument starts and answers `/health` 200 with build `dev`; the image job on a green main commit (GHCR push, attestation and `gh attestation verify`) not yet run, needs GitHub |
| AC-DLV-14 | S04 | PARTIAL: `tools/test_pr_checks.py` fixtures pass: additive changes, guard triggers and the `contract` label pass; drop, rename, alter, dropped check constraint, destructive `Sql` and unreadable `Sql` (concatenation, identifier, second positional argument, dynamic EXECUTE) fail; not yet run as a GitHub check on a real pull request |
| AC-DLV-15 | S08 | UNRUN |
| AC-DLV-16 | S14 | UNRUN |
| AC-DLV-17 | S14 | UNRUN |
| AC-DLV-18 | S15 | UNRUN |
| AC-DLV-19 | S11 | PARTIAL: `tools/test_trace_report.py` fixture passes (AC cited by a passing test, a failing test, no test, and a human-only AC, plus a browser suite whose step failed); the report shows pass, fail, unrun and human with the run URL and prints the records ratio; `--check` exit 0 with unchanged output and TRACEABILITY.md on the current tree; publishing on a real main CI run not yet verified |
| AC-DLV-20 | S12 | UNRUN |
| AC-DLV-21 | S10 | PASS (local) |
| AC-DLV-22 | S13 | UNRUN |
| AC-DLV-23 | S16 | UNRUN |
| AC-DLV-24 | S17 | UNRUN |
| AC-DLV-25 | S06 | UNRUN |
| AC-DLV-26 | S18 | UNRUN |
| AC-DLV-27 | S13 | UNRUN |

## Build 039-S01

Checks on the merged tree: `python3 tools/test_pr_checks.py` 8/8 pass; `dotnet build Hub.slnx` 0 errors; `python3 tools/trace_spec.py --check` clean; `python3 tools/build_spec.py --check` up to date; `pr_checks.py size --base HEAD^1` on the slice commit: 218 hand-written lines, pass. No flag: CI tooling only, no product behaviour change. Web not changed, so web build, lint and browser suites were not run.

Assumptions:
- 039-D4 default: warn above 400 and fail above 1,000 hand-written lines, label `large` overrides; exactly 400 and 1,000 do not trigger.
- CI base is `HEAD^1` of the pull request merge commit, with checkout `fetch-depth: 2`.
- The `pull_request` trigger gains `labeled` and `unlabeled` so the `large` label takes effect; this re-runs the whole serial job on any label change until S02 splits CI into jobs.
- Tests use `contextlib.chdir` (Python 3.11 or later; the Mac and `ubuntu-latest` meet this).

Left for Jay:
- Confirm the size step and the `large` label on the first real GitHub CI pull request run.
- Make the size check a required status check on protected main (S05).
- Create the `large` label in the repository.

## Build 039-S10

Checks on the merged tree: `python3 tools/test_decisions.py` 8/8 pass (the validator also caught three deliberate log edits in the builder run: a dropped source row, a renamed decision ID, a status left as "Open"); `dotnet build Hub.slnx` 0 errors; `python3 tools/trace_spec.py --check` clean; `python3 tools/build_spec.py --check` up to date. No flag: decision log and tooling only, no product behaviour change. Web not changed, so web build, lint and browser suites were not run.

AC-DLV-21: PASS (local). `tools/test_decisions.py` checks unique IDs in the three allowed forms, the four statuses, filled Last change, Flag and Source, each source row (036 verification section 14, 037 verification section 8, the release-candidate handoff behaviour list) present exactly once, and a row for every decision in packet 039+ `spec.md` files.

Assumptions:
- Status mapping per FR-DLV-26: Open became default-active, Decided and Done became confirmed, Deferred stayed deferred. No open row was changed; all keep their defaults.
- Packet 036 already uses 036-D1 to 036-D18 for defects, so new decision rows are 036-D20 to 036-D28; packet 037 rows are 037-D1 and 037-D2.
- The 59 rows identical in 036 section 14 and 037 section 8 are listed once ("036 §14 and 037 §8"). Non-behaviour rows are grouped into "Accepted as built" rows; the 5 handoff behaviour rows (A5, A6, F1, E3, the reading behind 036-D2) and the visual changes B3, F14 and M8 each have their own row.
- The handoff's partial-view follow-up (036-D25) and proposed S3 defect severities (036-D26) became default-active rows.
- "Release-candidate handoff behaviour rows" means the list of earlier rows that change behaviour in `docs/handoffs/2026-10-12-packets-036-037-release-candidate.md`.
- Flag column: the flag a row names; else the packet's single flag; else "the packet's flags (NNN spec.md)"; "none" for RPT rows, deferred rows, and packets 036 to 040 and 044.
- Last change: the date in the decision text, else git blame; new rows dated 2026-10-18.
- "Written under FR-DLV-21" means packets 039 onwards; decisions read from each spec.md Decisions section (table rows, bullets, or ranges).
- A third-party product name was removed from the 042-DEF1 question.

Left for Jay:
- Confirm or overrule new rows 036-D20 to 036-D28 and 037-D1 to 037-D2 (all default-active).
- Add `python3 tools/test_decisions.py` to `.github/workflows/ci.yml` (with S02 or by Jay; Tier B file).

## Build 039-S09

Checks on the merged tree (clean merge): `dotnet build Hub.slnx` 0 errors; `dotnet test tests/Hub.Tests --filter` Flags, Me, Admin and Settings classes 456/456 pass; `web npm run build` succeeds; `web npm run lint` exit 0 (one new fast-refresh warning on the `useFlag` hook, same as `useMe` and `useAuth`); `python3 tools/trace_spec.py --check` clean. Builder ran the full backend suite on the slice: 1164/1164. The flag list is empty, so with every flag off behaviour is unchanged; `/me` gains only an empty `flags` map. Browser suites not run (no port assigned).

Assumptions:
- 039-D7 default: a flag is retired after its pilot sign-off is recorded in the packet's `acceptance.md`; the check reads no date.
- 039-D8 default: every signed-in user receives flag names and on/off through `/me`; the packet appears only on Admin settings rows.
- Sign-off line format (not defined by the packet): a line in `specs/<packet>-*/acceptance.md` containing "pilot sign-off" (any case) and the flag in backticks as `` `flag.<name>` ``. `FlagDef.Packet` is the packet number prefix, for example "048".
- Test tags are xUnit traits: `[Trait("flag-off", "<name>")]` and `[Trait("flag-on", "<name>")]`.
- Retirement and pair-check logic lives in `FlagsTests.cs`, not production code.
- `OrgSettings.Flags` is built only from the declared list; stored `flag.*` rows for undeclared names are ignored, so a removed flag's leftover row is never read (FR-DLV-09).
- The packet field is added only to flag rows, so the Admin settings response is unchanged while no flag is declared.

Left for Jay:
- Run every browser suite and the coverage gates on a port assigned to this branch to finish AC-DLV-06 and AC-DLV-09.

## Build 039-S02

Checks on the merged tree (clean merge): `dotnet build Hub.slnx -c Release` 0 errors; `python3 tools/test_ci_tests.py`, `tools/test_coverage_gate.py` and `tools/test_decisions.py` OK; domain filter 635 passed and API shards 1 to 3 run one after another 162, 227 and 159 passed, 1183 in all, equal to `dotnet test --list-tests`; `python3 tools/trace_spec.py --check` clean. Builder on the slice: coverage collected per shard and merged by `gate` (96.3 % branches, 95.0 % lines); `ci_tests.py steps checks` OK; web `npm run build`, `npm run lint` and `test:ui-review` pass. No flag: CI tooling only, no product behaviour change. Integration step added `python3 tools/test_decisions.py` to the `checks` line of `tools/ci_steps.txt` (left for S02 by S10).

Assumptions:
- No Open decision in `specs/DECISIONS.md` applies to S02; Q15 (GitHub-hosted runners) is settled in the spec.
- The migration lint, `tier` check, AC-to-test ratchet and test-count ratchet belong to S04, S13, S12 and S03; each adds its own line to `tools/ci_steps.txt`.
- `gate` needs only `domain` and the API shards for coverage; S03 can add `web` and `checks` when it adds test counts.
- `test_pr_checks` and the PR-size check still run only on `pull_request` events, now guarded by `GITHUB_EVENT_NAME` in the step.
- The API jobs are one matrix with display names `api-1`, `api-2` and `api-3`.
- Browser evidence, formerly two artifacts, is under `ci-artifacts/browser-evidence/` in the `ci-artifacts-web` artifact.
- Running the three API shards at once in one worktree corrupts coverage files (shared test binaries); on GitHub each shard has its own runner.

Left for Jay:
- Push and open the pull request; get three consecutive green GitHub Actions runs whose domain plus API shard totals equal the last serial run's total (1183 on the integration branch now). Add the `large` label if the size check needs it.
- When S05 protects main, the required checks become `domain`, `api-1`, `api-2`, `api-3`, `web`, `checks` and `gate` instead of `build`.
- Run the port-bound browser suites (handoffs, sign-in, coordination, design-basis, planner, resources, supervisor-capacity) in the `web` job; not run locally.

## Build 039-S03

Checks on the merged tree (clean merge): `dotnet build Hub.slnx` 0 errors; `python3 tools/test_ci_tests.py` 21 tests OK; `tools/test_coverage_gate.py` OK; `.github/workflows/ci.yml` parses; `python3 tools/trace_spec.py --check` clean. Builder on the slice: real domain filter and API shards 1 to 3 against Postgres, 1183 listed and 1183 run, 8 browser suites counted; `ci_tests.py count` exited 0 and, with api-3 removed, reported 159 cases not run. No web code changed. No flag: CI tooling only.

Assumptions:
- The drop-below-main check fails only on `pull_request` events; on a main push it only reports, since the merged pull request already passed or carried `replace-test`. The missed-shard check fails on every event.
- The browser-suite count is the number of `npm run test:` commands on web lines of `tools/ci_steps.txt` (8 now); `gate` does not wait for `web`, whose failure already fails CI.
- Cases are compared as a multiset of display names from `--list-tests` and TRX (data rows count separately); skipped tests count as run.
- "Latest green main run" is the newest successful push run of workflow `ci` on main; earlier main runs without a `test-counts` artifact count as no earlier count, so the gate passes.
- No Open decision default from `specs/DECISIONS.md` was relied on.

Left for Jay:
- First remote CI runs on a pull request and on main: confirm `gh run list` / `gh run download` work with `actions: read`, main publishes `test-counts`, and the `replace-test` label exists and re-runs `gate` when applied.
- Making `gate` a required check belongs to S05.

## Build 039-S04

Checks on the merged tree (clean merge; only `tools/pr_checks.py`, `tools/test_pr_checks.py` and `tools/ci_steps.txt` changed): `dotnet build Hub.slnx` 0 errors; `python3 -m unittest tools/test_pr_checks.py` 15 tests OK; `python3 tools/test_ci_tests.py` OK; `python3 tools/pr_checks.py migrations --base HEAD^1` exit 0; `python3 tools/trace_spec.py --check` clean. No web code changed. No flag: the lint runs only in the pull_request `checks` step, so app behaviour is unchanged.

Assumptions:
- No Open decision default from `specs/DECISIONS.md` was relied on.
- A literal followed by `, suppressTransaction:` counts as readable; a second positional argument or `+` concatenation counts as unreadable and fails without the `contract` label.
- Interpolated strings (`$"...{x}..."`) are read as literals; values inside the braces are not checked.
- Trigger-event clauses (BEFORE/AFTER/INSTEAD OF ... ON) are ignored like ON DELETE/ON UPDATE, so the five existing guard-trigger migrations report no operations; existing migrations are never linted.

Left for Jay:
- Run the migrations check on a real GitHub pull request (and create the `contract` label) so AC-DLV-14 can be marked PASS.
- Making it a required check in branch protection belongs to S05.

## Build 039-S05

Checks on the merged tree (clean merge; only `docs/runbooks/delivery.md` and `specs/human-only.txt` added): `dotnet build Hub.slnx` 0 errors; `python3 -m unittest tools/test_pr_checks.py` OK; `python3 tools/build_spec.py --check` up to date; `python3 tools/trace_spec.py --check` clean. Documentation only: no flag, no code change, app behaviour unchanged.

Assumptions:
- 039-D3 default: the drift limit is 50 commits on origin/main since the branch started.
- 039-D2 default: no machine account, so protection requires a pull request with 0 approvals and Tier B stays a label control.
- Required check names are the `ci.yml` job names (domain, api-1, api-2, api-3, web, checks, gate); the trace check is required through `checks`, which runs `trace_spec.py --check`.
- Admins are included in the protection, so direct pushes by Jay are refused, as AC-DLV-01 needs.
- The `tier` and `review` checks and automatic merge are left to S13; the runbook says to add them then.
- `acceptance.md` is not created here; S11 owns its template.
- `specs/human-only.txt` format is the ID, a space, then the reason; S12 may tighten it.

Left for Jay:
- Apply the repository merge settings and the protection on main with the two `gh api` commands in `docs/runbooks/delivery.md`, run the readback commands and paste the output into `specs/039-delivery-process/acceptance.md`.
- Confirm AC-DLV-01 by hand: a direct push is refused, a pull request with a failing check cannot merge, and a merge lands as a squash commit with the head branch deleted.

## Build 039-S11

Checks on the merged tree (clean merge): `dotnet build Hub.slnx` 0 errors; `python3 tools/test_trace_report.py` OK; `python3 tools/test_ci_tests.py` OK; `python3 tools/test_coverage_gate.py` OK (1 skipped); `python3 tools/test_decisions.py` OK; `python3 tools/test_pr_checks.py` OK; `python3 tools/build_spec.py --check` up to date; `python3 tools/trace_spec.py --check` clean. No web change. No product flag: tooling, CI and templates only, app behaviour unchanged.

Assumptions:
- Records ratio: code lines are lines added by non-merge commits whose subject names a slice of the packet (`039-S11: ...` or `039 S11: ...`); files under `specs/` and `spec-parts/`, `*.md`, `package-lock.json`, `*.Designer.cs` and model snapshots are not code. The ratio is reported, never enforced.
- Records lines are all files under `specs/<packet>/`, the spec-parts file named in the packet spec.md, and the `specs/DECISIONS.md` rows for that packet.
- "ACs tested of total" counts ACs with at least one passing citing test and no failing one, out of the ACs the packet spec.md cites.
- Slices done counts every checkbox line in tasks.md, so older T-numbered packets count too.
- The flag shown is the first backticked `flag.*` name in the packet spec.md, otherwise "none".
- Browser suites and Python test files cite IDs anywhere in the file; their result is the outcome of the CI step that runs them (from `ci-artifacts/step-outcomes.jsonl`), so a combined step gives one outcome for every file in it.
- A test citing a human-only ID leaves it human.
- The report is published on main push runs only, not pull requests.
- `specs/human-only.txt` (created in S05, seeded in S12) is read when present.
- No 039 decision default was needed for this slice.

Left for Jay:
- After this reaches main, confirm on GitHub that the `report` job runs (it downloads all job artifacts and needs a full-history checkout) and uploads the `spec-report` artifact. Until then the FR-DLV-24 transition clause keeps verification.md in use.

## Build 039-S07

Checks on the merged tree (clean merge): `dotnet build Hub.slnx` 0 errors; `python3 tools/test_ci_tests.py` 24 tests OK (`ImageTests` cites AC-DLV-13); `.github/workflows/ci.yml` parses as YAML; `python3 tools/trace_spec.py --check` clean. No web or C# change. No product flag: CI and image only, app behaviour unchanged.

Assumptions:
- 039-D15 default: packet 042 S01 already added the `RELEASE_SHA` argument and `HUB_BUILD`, so S07 only adds the `org.opencontainers.image.revision` label (FR-DLV-14) in the final stage of `hosting/Dockerfile.review`.
- 039-D1 default: the GHCR package is private; the workflow changes no visibility setting.
- Image name is `ghcr.io/<lowercased repository owner>/pm-tool:<full sha>`; the checks run against repository@digest, the exact image pushed.
- The `image` job runs only on a push to main after domain, api, web, checks and gate pass, and is the only job with `packages: write`; top-level permissions stay read-only.
- Action versions: docker/login-action@v3, docker/build-push-action@v6, actions/attest-build-provenance@v2.
- The probe check for the build without the argument was done locally (`/health`); `scripts/verify-homedev-review.py` only accepts the homedev origins.
- The moving `main` tag and the contract-migration label are left to S08.

Left for Jay:
- After this reaches main, let the image job run once: confirm it pushes `pm-tool:<sha>` to GHCR and that the label, `HUB_BUILD` and `gh attestation verify` steps pass.
- Confirm the GHCR package is private (D1) and that the repository's Actions settings let `GITHUB_TOKEN` write packages and attestations.
- Changes under `hosting/` and `.github/` are Tier B: the pull request needs the tier-b label or Jay's review.
- On homedev, run `scripts/verify-homedev-review.py` against an image built without `RELEASE_SHA`.

## Build 039-S12 / S13 / S16 / S17

Focused local checks: `/opt/homebrew/bin/python3.13 -m unittest tools.test_pr_checks tools.test_trace_report tools.test_auto_merge -v` (35
tests PASS); `python3 tools/trace_spec.py --check` (1299 IDs, 405 sections, zero gaps);
`python3 tools/trace_spec.py --ratchet --base HEAD^1` (PASS, 211 base cited and 216 head cited); `git diff --check`
(PASS). The ratchet fixtures cover removed citations, new uncited ACs, unchecked and ticked slice exemptions, and
human-only exemptions. Tier fixtures cover Tier B paths, the `tier-b` label, `tier-b-ok`, documentation-only changes,
and added versus removed `tools/ci_steps.txt` lines.

S12 adds `specs/human-only.txt` candidates and the base/head AC-to-test ratchet, including lost unchecked-slice
exemptions and full continuation-text comparison. S13 adds the deterministic Tier B check, trusted Tier A auto-merge
workflow and the agent/hostname rules in `AGENTS.md`, `CLAUDE.md` and this workflow guide. S16 adds the non-required
nightly job, six real-API suite lines, preview database/API/web startup, existing seeders and artifact upload; the
first nightly skips comparison-only calm checks when no green base exists, and later runs use the last green nightly
UI. S17 adds monthly per-ecosystem Dependabot groups and a Dependabot-only automatic squash merge workflow for
patch/minor updates; major updates remain for Jay.

Remaining evidence: GitHub branch protection, review status behavior, first and second scheduled nightly runs,
Dependabot pull requests and hosted preview execution remain UNPROVEN locally. The nightly workflow is intentionally
not a required check and no activation, merge, deploy or timer installation was performed. Auto-merge makes no GitHub
mutation during local checks; it requires exact-head green CI plus an actual reviewer `review` status.

## Codex takeover local completion — 18 October 2026

This section supersedes the earlier UNRUN entries only for the local evidence described here. It is not a
remote CI run, deployment, Tier B merge, decision confirmation or acceptance sign-off.

- Baseline `a6e0104b0b00ea58b9098e41b2f155305063aeed`, isolated branch `codex/delivery-ops-039-044`.
  Recovered the six-file `b-039-S08/tracked.patch` byte-preserved snapshot cleanly; inspected S12, whose saved
  patch is empty and whose HEAD is the baseline. Original source worktrees were not reset or edited by this lane.
- S06: [read-only inventory](branch-inventory.txt) records exact local tips, missing commits and worktrees.
  Archive refs were not pushed and nothing was removed; the six-worktree target and each deletion remain Jay gates.
- S08: rollback probes previous/new/previous against one fresh disposable review schema, contract label and
  gated main-tag move prepared; only the image job has package write. Older delayed main runs cannot move the
  tag over a newer head. Actual registry job and the throwaway rename failure remain UNPROVEN.
- S12/S13: complete-AC-text and checked-slice ratchet, human-only IDs, Tier B paths/labels, named `tier` job,
  reviewer commit status and trusted exact-head auto-merge sources prepared. Auto-merge handles reviewer status
  posted after CI, refuses incomplete >100-file inventories and follows the newest check result. No reviewer
  status, label, protection or remote merge was created by this lane.
- S14/S15: root-owned deploy helper plus optional operator installation and pilot digest path prepared. Mocks
  cover provenance/contract/copy guards, dump-before-start, immutable same-SHA rollback, failed digest refusal,
  a different digest succeeding after rollback, and pilot digest verification without a build. Root Docker
  credentials are installed privately with token stdin. These sources have not run on homedev.
- S16/S17: named seeded preview/guard arguments, Playwright prerequisites, previous-green-nightly comparison,
  uploaded reports/screenshots and monthly ecosystem groups with positive patch/minor merge selection prepared.
  First scheduled runs, major-update waiting and actual grouped PR behavior remain UNPROVEN. GitHub Actions
  dependency edits also cross the general `.github/` Tier B path rule; retain that conservative path gate
  until Jay resolves any desired dependency exception.
- Focused local safety checks: `/opt/homebrew/bin/python3.13 -m unittest tools.test_review_deploy
  tools.test_ci_tests tools.test_pr_checks tools.test_trace_report tools.test_auto_merge tools.test_served_assets
  tools.test_backup_helpers tools.test_pilot_operations scripts.test_check_offhost_backup` passed **93 tests**
  before two final auto-merge boundary tests; those two tests also pass separately. Spec freshness and trace
  checks pass (1,299 IDs / 405 sections, no gaps), AC ratchet against baseline passes (211 base / 220 head citations
  at that checkpoint), five changed workflow/config YAML files parse, and `git diff --check` is clean.
- Independent reviewer examined the actual diff and reproduced same-SHA rollback followed by a new successful
  digest using fake commands. Earlier review findings were corrected; the last review had no open source
  findings before the final reporter extension in packet 044. Combined regression/coverage belongs to integration.

[Acceptance gates](acceptance.md) remain pending. S18's real packet journey is human-only and has not occurred.

Final follow-up: **97/97** focused Python safety tests pass on the final state (including the two nightly/config
contract checks). The AC ratchet passes with newly ticked local slices (211 base / 229 head cited ACs). The named
`tier` job also uses `ci_tests.py steps tier` and uploads its evidence; the runbook's prepared protection command
now includes `tier`, actual reviewer `review` and auto-merge enablement. No command was applied to GitHub.

Recovered-S08 follow-up: the saved patch opened the synthetic verifier to mode 644 and key ring to 777. That
would violate `LocalPasswordStore`'s existing owner-only start-up check. The disposable CI runner now changes
ownership to the image's UID 1000 while keeping 600/700 modes, briefly returning the verifier to the runner
only for synthetic credential bootstrap. The fake-runner test enforces private modes at each start/restart.
No sudo/Docker operation from this compatibility path was executed locally or on homedev.
