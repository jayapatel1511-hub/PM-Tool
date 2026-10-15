## 43. Delivery Process Amendment

Approved scope: on 2026-10-14 Jay approved the build-approach report (kept outside the repository) and directed building all of it. Features are fixed and additive only: nothing in this section removes, hides, narrows or freezes a feature, screen, route, report, export, setting or rule, and no existing feature is put behind a flag. Packet 039 delivers this section. It changes how Tuesday is built, tested, merged, released and recorded: one protected trunk, small pull requests, flags for unreleased additions only, parallel CI, one image built by CI and deployed automatically to the review stack only, migrations that old images can still run on, generated verification and one decision log.

Where this section adds to §21 (dependency management), §22 (quality gates and CI), §23.4 (feature flags), §23.7 (CI/CD and configuration) and §24.6 (migrations), this section governs. Pilot and production deployment stay manual. Users see no change except an Admin settings group that appears once a flag is declared, and a `flags` field on `/me`.

### 43.1 Scope and boundary

The trunk and branch rules are §43.2 and §43.3; flags are §43.4; CI is §43.5; the release image is §43.6; migrations are §43.7; deployment and rollback are §43.8; records are §43.9 and §43.10; agents and merge tiers are §43.11; the nightly run and dependency updates are §43.12.

Outside this amendment: any change to product behaviour; the typed contract and entity registry (§44); the build shown in `/health`, the `X-Hub-Build` header and the reload message (§46); the off-host backup and outbox recovery (§48); per-PR preview stacks; release branches; automatic deployment to pilot or production. On Jay's direction the Development Workflow section of the constitution was amended to version 1.1.0 (§43.14 D11); the amendment takes effect when Jay merges this packet's spec pull request.

### 43.2 Trunk and branches

- **FR-DLV-01.** `origin/main` MUST be the only integration branch and MUST be protected. Changes arrive only through pull requests and merge by squash. Direct pushes and force pushes are refused. A merge needs every required check of §43.5 and, for Tier B, the label of §43.11. The head branch is deleted on merge. "Require branches to be up to date" stays off, because main's own CI run gates the release image. A release is a green main commit; there are no release branches.

- **FR-DLV-02.** A work branch MUST start from `origin/main`, be named `a/<packet>-<slice>` (for example `a/039-s03`), carry one slice and merge `origin/main` in before each push. The pull request title starts with the packet and slice (`039 S03: ...`), so records join to code (FR-DLV-25). A branch that falls behind `origin/main` by more than the drift limit (§43.14 D3) is merged dark behind its flag, or pushed as `archive/<branch>` and closed. The limit counts commits on main, not time. No long-lived `claude/*` or `codex/*` integration branch is created.

- **FR-DLV-03.** A failed CI run on main MUST stop the line: the next change merged is a revert or a forward fix of that failure. No release image is published from a red commit (FR-DLV-14), so the review stack never receives it.

- **FR-DLV-04.** Once, before the trunk rules apply, every branch and worktree not merged into main MUST be inventoried without loss: list its commits missing from main (for example the unlanded landing work on `codex/landing-polish`), push it as `archive/<branch>`, and delete a branch or remove a worktree only after Jay confirms that item. Afterwards at most 6 worktrees are live, each removed when its branch merges.

### 43.3 Pull request size and generated files

- **FR-DLV-05.** A `.gitattributes` file MUST mark generated files `linguist-generated -diff`: the EF migration Designer files and `HubDbModelSnapshot.cs` under `src/Hub.Api/Data/Migrations/`, the combined specification `.md` and `.html`, `specs/TRACEABILITY.md` and `web/package-lock.json`. They stay committed, so EF, `tools/build_spec.py` and `tools/trace_spec.py` work unchanged.

- **FR-DLV-06.** A required PR-size check MUST count hand-written changed lines (added plus deleted, skipping paths marked generated). Above 400 it warns. Above 1,000 it fails unless Jay applies the label `large`. A pull request template asks for the spec IDs, whether `spec-parts/` changes, the flag name, the migration kind (none, expand or contract) and the tests added with their AC IDs.

### 43.4 Feature flags

- **FR-DLV-07.** Feature flags MUST be organisation settings declared in one static list, `src/Hub.Domain/Flags.cs`, each entry giving a name and its packet. `OrgSettings.Defs` generates `flag.<name>` as a Bool setting, default off, group `flags`, the same way it generates `rule_enabled.*` (`src/Hub.Domain/Settings.cs:88`). Startup seeding creates each row (`src/Hub.Api/Data/Seed.cs:38`). Admin switches flags on the existing settings page (`src/Hub.Api/Features/Admin.cs:71-94`), where the Flags group shows each flag's name and packet. Each change writes the existing setting audit entry (`src/Hub.Api/Infrastructure/Audit.cs:48`). Each environment's database holds its own state, so review and pilot are switched separately. No library or service is added.

- **FR-DLV-08.** `GET /me` MUST return `flags`, a map from each declared flag name to on or off, beside its existing fields, which stay unchanged (`src/Hub.Api/Features/Me.cs:13-51`). The client reads it through one hook beside `useMe` (`web/src/lib/auth.tsx:38`) and treats a missing map or name as off. The server enforces every flag: while it is off, the addition's endpoints answer 404 through one shared guard and its fields are absent from existing responses. The client hides its routes and sections.

- **FR-DLV-09.** A flag MUST gate only an addition that has never been on in pilot. No existing or already-released feature, screen, route, report, export, setting or rule is ever put behind a flag. This includes packet 038 and everything merged before this section. An addition that changes what pilot users see ships dark behind a flag, unless its packet lists it as an unflagged correction or fix, each with a decision row; a slice that ships such a change unflagged is Tier B (FR-DLV-28). A flag is a release toggle only: a control that must outlive the cleanup, such as an organisation's choice to collect something, is an ordinary organisation setting declared by its packet. The list starts empty. Lifecycle: Jay turns a flag on in review after looking at the addition, then on in pilot at promotion; a cleanup pull request then removes the entry and its checks and keeps the addition on. Every packet that declares a flag ends with that cleanup slice. The stored setting row is left unread.

- **FR-DLV-10.** Two automated checks MUST guard flags. A retirement test fails when a flag's packet `acceptance.md` records pilot sign-off for that flag and its entry is still in `Flags.cs`. Its remedy is the cleanup pull request. The test reads no date. A pair check fails unless every flag has at least one test tagged as its flag-off test, proving today's behaviour and responses unchanged, and one tagged as its flag-on test, proving the addition.

### 43.5 Continuous integration

- **FR-DLV-11.** CI MUST run as parallel jobs on GitHub-hosted runners (Q15), replacing the single serial job (`.github/workflows/ci.yml:8-83`) without dropping any step, threshold or evidence artifact. The jobs are: `domain` (build, Domain tests, flag tests); `api-1`, `api-2` and `api-3` (build and API tests, each with its own PostgreSQL service); `web` (build, lint, every browser suite CI runs today, the renderer regressions in `web/tests/ui-review.cjs` and the npm audit); `checks` (the existing Python safety regressions, the NuGet vulnerability scan, `trace_spec.py --check`, the PR-size check, the migration lint, the `tier` check and the AC-to-test ratchet); and `gate` (coverage and test counts). Each job runs its steps from one list outside `.github/`, `tools/ci_steps.txt`, one line per step naming its job and command (for example `web: npm run test:interaction`), through `python3 tools/ci_tests.py steps <job>`, and uploads whatever its steps write under `ci-artifacts/` as a run artifact. A packet that adds a browser suite or a check adds a line there and does not edit `.github/`; a job's services and permissions stay in the workflow file. The nightly job of FR-DLV-30 reads its `nightly:` lines from the same file. All API test classes share one xUnit collection (`tests/Hub.Tests/Api/Factory.cs:101`), so the API tests split by class: a script lists the API test classes from the test files and assigns each to exactly one shard, so a new class always runs. A pull request's evidence is its remote CI run; local "CI parity" records are not evidence.

- **FR-DLV-12.** `tools/coverage_gate.py` MUST merge shard coverage by file and line instead of summing per-file totals (`tools/coverage_gate.py:7-26`). A line is covered when any shard hit it. A line's covered branches are the highest count any shard reports, a lower bound, so the merge never over-reports. The gates stay at 95 % branch coverage for `Hub.Domain` and 70 % line coverage for `Hub.Api` features. With one input file the result equals today's.

- **FR-DLV-13.** The `gate` job MUST fail when (a) the backend test cases executed across `domain` and the API shards differ from the cases the test project lists, which catches a missed shard, or (b) the executed backend cases or the number of browser suites run in CI fall below the latest green main run's counts, unless Jay applies the label `replace-test`. CI publishes the counts as an artifact of each main run. A run with no earlier main count passes and publishes its own.

### 43.6 Release image

- **FR-DLV-14.** On a green main commit, CI MUST build `hosting/Dockerfile.review` once for linux/amd64 and push it to the GitHub container registry (GHCR) as `pm-tool:<full-sha>`. The Dockerfile accepts a `RELEASE_SHA` build argument and records it as the image's OCI revision label and as the `HUB_BUILD` environment variable; without the argument it builds as today. The push carries a build provenance attestation naming this repository's CI workflow on `refs/heads/main`. Only this job may write packages; pull request runs keep read-only permissions (`.github/workflows/ci.yml:6`). The moving `main` tag points at an image only after FR-DLV-17 passes for it. §46 reads `HUB_BUILD` to show the build. Packet 042 slice S01 owns the argument and the compose wiring; this packet adds the argument only if it is still absent when its image slice lands (§43.14 D15). This packet does not change the compose files: a deploy tags the pulled image as `pm-tool-review:<sha>` (`hosting/homedev.compose.yml:27`).

### 43.7 Migrations: expand and contract

- **FR-DLV-15.** A required migration lint MUST read the `Up()` body of every migration a pull request adds under `src/Hub.Api/Data/Migrations/` (Designer files excluded). It fails on drop, rename and alter-column operations, dropped check constraints and foreign keys, and `Sql` calls containing DROP, TRUNCATE, DELETE, RENAME or ALTER ... TYPE, unless Jay applies the label `contract`. Existing migrations are not linted. Backfill updates pass and MUST be safe to re-run. On main, CI records the newest contract migration contained in each image as an image label.

- **FR-DLV-16.** Schema changes MUST follow expand and contract. Expand, the default, adds structure the previous release ignores. A contract migration drops only what no deployed release has read for at least one release; Jay merges it and activates it by hand, never through the review timer. Migrations still run at startup on a single instance (`src/Hub.Api/Program.cs:190-204`) and keep their down methods (§24.6), but a down migration is never run on review or pilot.

- **FR-DLV-17.** On every main build after the image push, a rollback-compatibility job MUST prove that the previous main image runs on the new schema. It starts the previous image on a fresh database, which creates and seeds the older schema; starts the new image, which migrates, and passes the probes; then starts the previous image again on the new schema and passes the probes. It reuses `hosting/homedev.compose.yml`, `scripts/init-homedev-review.py`, a synthetic reviewer credential and `scripts/verify-homedev-review.py`. Without a previous image it passes and says so. A failure leaves the moving `main` tag where it was. The job can also be started by hand on any branch.

### 43.8 Deployment and rollback

- **FR-DLV-18.** The review stack MUST be deployed by a root-owned script and systemd timer, `pm-tool-review-deploy`, which Jay installs once with sudo in the same way as the backup helper and timer (`docs/runbooks/homedev-review.md:55-69`). His account gets no Docker group membership (`docs/runbooks/homedev-review.md:10`). Each run, at the timer interval (§43.14 D5), the script:
  1. resolves the `main` tag to a digest, and stops if that digest is running or has already failed;
  2. stops if the provenance of FR-DLV-14 does not verify, if the image holds a contract migration the review database has not applied, or if the image's compose file or probe script differ from the installed root-owned copies;
  3. pulls by digest and tags it `pm-tool-review:<sha>`;
  4. dumps the database with the existing root-owned backup helper;
  5. starts the API with `up -d --no-build` on the same `pm-tool-review-db` volume and runs the probes;
  6. on success, records the SHA, digest and time in a root-owned state file that Jay's account can read; on failure, restarts the previous digest, probes it, logs the failure and never retries that digest.

  Every stop is logged with its reason. The timer never touches pilot or production. The manual path, `scripts/activate-homedev-review.sh`, stays as it is. A merge that changes the compose files or a probe script needs Jay to refresh the root-owned copies with sudo before the timer deploys again; until he does, each run stops at step 2 and logs why. Packet 042 slices S01 (compose files) and S03 (probe scripts) are such merges, and the runbook says so.

- **FR-DLV-19.** Pilot and production deployment MUST stay manual. Jay promotes a review-proven image to the pilot when he chooses, by digest, through `scripts/activate-homedev-pilot.sh` with a new optional `--image-digest` argument. With it, the script pulls the CI image, verifies its provenance and tags it `pm-tool-pilot:<sha>` instead of building; without it, the script builds as today (`scripts/activate-homedev-pilot.sh:78-85`). An image is review-proven as §43.14 D6 states. Production activation happens only on request and uses the same digest path once its hosting is approved and Entra sign-in and the §48 off-host backup and restore drill are proven. No per-PR preview stacks are built.

- **FR-DLV-20.** Rollback MUST use the cheapest step that works, in this order: turn the flag off (audited, no deploy); the timer's automatic return to the previous digest; a manual return to a named digest; restoring the pre-deploy dump into a new isolated database, as the runbook already requires (`docs/runbooks/homedev-review.md:75-77`). A down migration is never run and a database volume is never deleted. The daily dump timers and restore drills stay unchanged.

### 43.9 Specification records

- **FR-DLV-21.** A packet written after this section MUST be one spec pull request (Tier B) followed by slice pull requests. The spec pull request holds the `spec-parts/` delta; a `spec.md` of at most about 1,500 words (user outcome, scope with IDs, non-goals, flag name, decisions with defaults, acceptance IDs, dependencies); a short `plan.md` (approach, file boundaries, Constitution Check, and Complexity Tracking only when constitution VII applies); a `tasks.md` that is the slice list, one unchecked line per slice, each one pull request, naming its IDs and tests; and its rows in `specs/DECISIONS.md`. Templates live in `specs/_templates/`; the upstream Spec Kit templates stay unmodified. Constitution IV, `build_spec.py`, `trace_spec.py --check`, every existing ID, packet numbering, human sessions and acceptance sweeps are unchanged. Earlier packets' records are kept and not rewritten.

- **FR-DLV-22.** `tools/trace_spec.py` MUST gain a `--report` mode that joins each FR and AC ID to the tests citing it and to the CI results. A test cites an ID in its method name, in a `[Trait("Spec", "<ID>")]` attribute, or in a comment on its declaration line or its class summary, the convention already in use (`tests/Hub.Tests/Api/DecisionsTests.cs:48`); a browser suite cites IDs in its file. Results come from the test result files of `domain` and the API shards and from each browser suite's step outcome. Per ID the report gives the citing tests, pass, fail or unrun, whether it is human-only, and the run URL. Per packet it gives ACs tested of total, slices done of total, the flag name and the records ratio (FR-DLV-25). It is published as an artifact of every main run and is not committed. `--check` keeps its current behaviour.

- **FR-DLV-23.** CI MUST enforce an AC-to-test ratchet. The number of AC IDs cited by at least one test never falls below the pull request's base commit. Any AC ID that a pull request adds to `spec-parts/`, or whose text it changes, must be cited by a test at the head commit, unless it is listed in `specs/human-only.txt` (one ID and reason per line) or named by an unchecked slice in its packet's `tasks.md`. Ticking that slice requires the citation. Changes to `specs/human-only.txt` are Tier B. The report lists human-only ACs as human checks.

- **FR-DLV-24.** Each packet written after this section MUST keep one hand-written `acceptance.md` of at most one page: human session results, Jay's visual review, acceptance-sweep defects as rows (ID, severity, where found, fixing pull request, regression test) and sign-off. Sweeps still run before a packet's flag goes on in pilot. Transition: until `--report` is published on main, packets keep recording checks in `verification.md` as today. After that, a new packet's verification is the report of the CI run for the deployed commit, and every earlier `verification.md` is kept unchanged.

- **FR-DLV-25.** Records MUST be measured, not gated. The report prints, per packet, the lines of its records (`specs/<packet>/`, its spec-parts file and its decision rows) against the hand-written code and test lines of pull requests whose titles name that packet, with a soft target of at most 1:1. Packet status is derived the same way: `specs/README.md` points to the report's packet table for packets written after this section, and the hosted commit is read from the deployed image (FR-DLV-18), not from handoff prose.

### 43.10 Decision log

- **FR-DLV-26.** One decision log, `specs/DECISIONS.md`, MUST hold every open product and process decision with: ID (`<packet>-D<n>` for a packet decision, `<packet>-DEF<n>` for a deferred or parked item, `RPT-NN` for a build-approach report decision), packet, question, default, flag, status (default-active, confirmed, overruled or deferred), the date of the last status change, and the source row. The log already exists: the spec pull requests of packets 039 to 047 start it with their rows, §42.5 D1 to D7 and the report's decisions. Packet 039 extends it: it adds the flag, last-change and source columns, maps today's statuses (Open to default-active; Decided and Done to confirmed; Deferred to deferred), and deduplicates into it section 14 of packet 036's `verification.md`, section 8 of packet 037's `verification.md` and the release-candidate handoff's behaviour rows. It records as deferred, each with its trigger, the read-sync sidecar, the TypeScript port and the parked items that would need a §30 or constitution change: hours budgets and actuals import, critical path, successor auto-shift and cross-project links, a client link, custom fields and statuses, and a public API. New packets add rows in their spec pull request. Defaults apply while code is dark. A pending row never blocks a merge or a review deploy; a packet's flag goes on in pilot only when Jay has confirmed its rows or accepted their defaults. Only Jay changes a status. The source rows stay in their files.

### 43.11 Agent workflow and merge tiers

- **FR-DLV-27.** Agents MUST work inside Jay's cap of 10 concurrent agents: one planner; at most five writers, each owning a disjoint set of paths named in its pull request; exactly one schema-lane writer at a time, the only agent that may add an EF migration; one reviewer agent per pull request, in a separate session; the rest read-only. Every agent tool in use follows the same rules, written in `CLAUDE.md`, `AGENTS.md` and `docs/SPEC-KIT-WORKFLOW.md`. `AGENTS.md` is a new tracked file; it carries over the existing project hostname rule from the untracked copy.

- **FR-DLV-28.** Pull requests MUST merge in two tiers. Tier A merges automatically by squash once the required checks pass and the reviewer agent has set the required `review` status for the head commit, which it sets only when it has no open findings. Tier A covers dark additions behind an off flag, fixes and guards that change nothing users see, tests, documentation, refactors with unchanged tests, lines added to `tools/ci_steps.txt`, and patch or minor dependency updates. Tier B is merged by Jay. It covers spec pull requests (`spec-parts/`, packet `spec.md`, decision statuses, `specs/human-only.txt`, `.specify/`), `src/Hub.Domain/Permissions.cs`, `src/Hub.Api/Infrastructure/Auth.cs` and `SocialAuth.cs`, contract migrations, `hosting/`, `scripts/*homedev*`, `.github/`, a removed or changed line in `tools/ci_steps.txt`, any change to approved behaviour, any unflagged change users can see (FR-DLV-09), and anything labelled `large` or `replace-test`. A required `tier` check passes on Tier B paths only with Jay's label `tier-b-ok`. The author of a slice that its packet's `tasks.md` marks Tier B applies the label `tier-b`, and the `tier` check treats that label as a Tier B path. Agents never apply `tier-b-ok`, `contract`, `large` or `replace-test`, never deploy, never turn a flag on in pilot and never change a decision status. Agents push as Jay's account, so labels are a procedural control, not an enforced review (§43.14 D2).

- **FR-DLV-29.** Boy-scout rule: a pull request MAY convert code in the files it already changes to the current platform patterns, with tests unchanged and no behaviour change. It never adds files to the pull request for that purpose, and the converted lines count toward the size check.

### 43.12 Regression net and dependency updates

- **FR-DLV-30.** A nightly job on main MUST run the browser suites that today run only by hand against the real API: `counts`, `planning-live`, `ui-consistency`, `clutter`, `calm-acceptance` and `calm-clicks` (`web/tests/`). It starts PostgreSQL as `pm-tuesday-preview-db` on loopback port 55433, the API in Development on port 5080 and the web dev server on port 5173, exactly as the guard in `tools/preview/preview_target.py` expects, so the guard stays unchanged. It loads fixtures with the existing `tools/preview` seeders. Reports and screenshots upload as artifacts. A red nightly does not block merges or review deploys; it becomes the next fix task and holds pilot promotion (FR-DLV-19). Suites that still mock some routes run as they are.

- **FR-DLV-31.** Dependency updates MUST be grouped per ecosystem (NuGet, npm, GitHub Actions) at today's monthly cadence (`.github/dependabot.yml`), and security updates still arrive immediately (§21). A grouped patch or minor update merges automatically as Tier A when green. A major update is merged by Jay.

### 43.13 Acceptance criteria

- **AC-DLV-01.** Given the protection settings, then a direct push to main is refused, a pull request with a failing required check cannot merge, the merge is a squash and the head branch is deleted. A readback of the protection settings is recorded in the packet's `acceptance.md`.

- **AC-DLV-02.** Given a pull request with 350 hand-written lines and an 11,000-line Designer file, the size check passes without a warning; with 450 hand-written lines it warns and passes; with 1,050 it fails, and with the label `large` it passes.

- **AC-DLV-03.** Given the attributes file, `git check-attr` reports `linguist-generated` for a Designer file, the model snapshot, both combined specification files, `specs/TRACEABILITY.md` and `web/package-lock.json`, and for no hand-written source file.

- **AC-DLV-04.** Given a flag list with one entry `demo`, the setting definitions include `flag.demo` as a Bool, default off, group `flags`; given an empty list, the definitions equal today's.

- **AC-DLV-05.** Given no declared flags, `GET /me` returns an empty `flags` map with every other field unchanged, and the Admin settings list is unchanged.

- **AC-DLV-06.** Given a flag that is off, the shared server guard answers 404 for the addition's endpoint; given it on, the endpoint answers. The client hook treats a missing map or name as off, so every existing mocked browser suite passes unchanged.

- **AC-DLV-07.** Given an entry whose packet's `acceptance.md` records pilot sign-off for that flag, the retirement test fails and names it; given no such sign-off, it passes.

- **AC-DLV-08.** Given a flag with a flag-on test and no flag-off test, the pair check fails and names the flag and the missing tag; given both tags, it passes.

- **AC-DLV-09.** Given the commit that adds `Flags.cs`, the list is empty, and the full backend suite, every browser suite and the coverage gates pass unchanged; no existing route, screen, setting, report or response gains a flag check.

- **AC-DLV-10.** Given the API test files, the shard script assigns every API test class to exactly one of three shards; a new test class file lands in one shard with no edit to the workflow; a line added to `tools/ci_steps.txt` runs in its job with no edit under `.github/`.

- **AC-DLV-11.** Given two coverage files in which a `Hub.Domain` line is hit only in the second, the merged gate counts that line covered once; given one file, the merged percentages equal today's script's for that file.

- **AC-DLV-12.** Given shard results whose executed total differs from the listed total, the gate fails and names the difference; given a backend total or browser-suite count below the latest green main counts, it fails; with `replace-test` it passes; with no earlier main count it passes and publishes the counts.

- **AC-DLV-13.** Given a green main push, the registry holds `pm-tool:<sha>` whose revision label and `HUB_BUILD` equal the commit, and its attestation verifies for this repository's workflow on `refs/heads/main`. A red main commit and a pull request run push nothing. A local build without `RELEASE_SHA` builds and starts as today.

- **AC-DLV-14.** Given a new migration that only adds tables, columns or indexes, or runs an idempotent backfill update, the lint passes; given one that drops or renames a column, alters a column, drops a check constraint or runs `DROP TABLE` through `Sql`, it fails and names the file and operation; with `contract` it passes; existing migrations are not linted.

- **AC-DLV-15.** Given a main build with an additive migration, the rollback job starts the previous image on the new schema and its probes pass. Given a manual run on a throwaway branch with a column rename, the job fails and the moving `main` tag does not move.

- **AC-DLV-16.** Given a new green main image, the next timer run dumps the review database to a non-empty archive, starts the image on volume `pm-tool-review-db`, passes the probes and records the SHA and digest. Jay's account stays outside the Docker group.

- **AC-DLV-17.** Given an image whose probes fail, the timer restarts the previous digest, its probes pass, the failure is logged and that digest is not retried. Given an image whose provenance does not verify, whose contract migration is unapplied, or whose compose file or probe script differ from the installed copies, nothing is pulled or started and the reason is logged.

- **AC-DLV-18.** Given `scripts/activate-homedev-pilot.sh <sha>` without `--image-digest`, it builds and activates as today and the existing pilot operation tests pass unchanged; with `--image-digest`, it pulls, verifies and tags the image and never builds. The review timer never changes a pilot container.

- **AC-DLV-19.** Given a fixture specification and test results where one AC is cited by a passing test, one by a failing test, one by no test and one is human-only, the report shows pass, fail, unrun and human, each with the run URL, and prints each packet's records ratio. `--check` gives today's result on today's tree.

- **AC-DLV-20.** Given a pull request that removes the only test citing an AC, the ratchet fails. Given one that adds an AC with no citing test, it fails unless the AC is human-only or named by an unchecked slice of its packet. Ticking that slice without a citing test fails.

- **AC-DLV-21.** Given `specs/DECISIONS.md` after packet 039 extends it, every row ID is unique and has one of the three allowed forms, every status is one of the four allowed, every source row named in FR-DLV-26 appears once or is mapped to the row that absorbed it, and every decision in a packet `spec.md` written under FR-DLV-21 has a row.

- **AC-DLV-22.** Given a pull request that changes `src/Hub.Domain/Permissions.cs`, the `tier` check fails until `tier-b-ok` is applied; a pull request labelled `tier-b` fails until `tier-b-ok` is applied; a documentation-only pull request passes without a label; a Tier A pull request with green checks and a clear `review` status merges by itself.

- **AC-DLV-23.** Given the nightly job on main, the six suites run against the seeded real API and upload their reports. A failing nightly does not block a merge or a review deploy, and a commit without a green nightly is not review-proven.

- **AC-DLV-24.** Given the grouped dependency configuration, one pull request per ecosystem arrives at the monthly cadence; a green patch or minor group merges by itself; a major update waits for Jay; security updates still arrive immediately.

- **AC-DLV-25.** Given the one-time inventory, every unmerged branch is listed with its commits missing from main and has an `archive/` ref before any deletion; nothing is deleted without Jay's confirmation of that item; afterwards at most 6 worktrees remain.

- **AC-DLV-26.** Given the first packet after this one, it travels the whole path: a Tier B spec pull request, Tier A slice pull requests merged dark, an automatic review deploy, Jay turning the flag on in review, and Jay promoting the digest to pilot. Its `acceptance.md` fits on one page.

- **AC-DLV-27.** Given `CLAUDE.md`, the new tracked `AGENTS.md` and `docs/SPEC-KIT-WORKFLOW.md`, they state the branch rules, roles, tiers, forbidden labels and boy-scout rule of FR-DLV-02 and FR-DLV-27 to FR-DLV-29, `AGENTS.md` keeps the existing hostname rule, and no line contradicts the amended constitution.

### 43.14 Decisions and deferred proposals

Each decision uses its default until Jay decides otherwise.

| # | Decision | Default |
|---|---|---|
| D1 | Visibility of the container registry package | Private. Homedev keeps a read-only package token, root-owned, outside the release tree. |
| D2 | A machine account for agents, so GitHub enforces Tier B review | None. Tier B stays a label control. |
| D3 | Branch drift limit | 50 commits on `origin/main` since the branch started. A count, not a time. |
| D4 | Pull request size limits | Warn above 400 and fail above 1,000 hand-written lines. |
| D5 | Review timer interval | 5 minutes. |
| D6 | When an image is review-proven for the pilot | Its probes passed on review, it has run on review for the soak period (a release parameter, default 2 days, as the report proposed), and one nightly run on its commit is green. Jay promotes when he chooses. |
| D7 | When a flag is retired | After its pilot sign-off is recorded in the packet's `acceptance.md`, by the cleanup pull request. No date is set. |
| D8 | Who sees flag states | Every signed-in user receives names and on or off through `/me`. The packet shows only on Admin settings. |
| D9 | Branch coverage across shards | The highest per-line count any shard reports. |
| D10 | Nightly checks that compare with a base UI (`calm-acceptance` quick, filters and deep links; `calm-clicks`) | Compare with the last green nightly commit. The first nightly runs the other checks only. |
| D11 | Constitution amendment | Decided by Jay on 2026-10-14: the Development Workflow amendment is adopted as constitution 1.1.0 and takes effect when Jay merges this packet's spec pull request. Automatic merge (S13) then depends only on that merge, and the review timer (S14) also on D12. |
| D12 | Timer installation | The code merges; Jay installs it once with sudo when he chooses. Until then, activation stays manual as today. |
| D13 | Major dependency updates | Tier B; Jay merges. |
| D14 | Where the deployed commit is recorded | The running container's revision label and the timer's state file. The `current` link keeps meaning the last manual activation. |
| D15 | Dockerfile argument shared with §46 | `RELEASE_SHA` becomes `HUB_BUILD`. Packet 042 slice S01 adds it with the compose wiring; this packet's image slice adds it only if it is still absent. |
| D16 | `plan.md` in every packet | Kept, short, in every packet (FR-DLV-21), as Jay's constitution 1.1.0 amendment says. The report proposed `plan.md` only where constitution VII complexity tracking applies. |

Deferred proposals, outside packet 039: a merge queue (not available for user-owned repositories when written, and the pull request rate does not need one); per-PR preview stacks (homedev's memory and ext4 file system rule out instant database clones); per-user or per-role flag targeting, added when the pilot needs a subset; exposure logging and experiments; a SQL linter over generated migration scripts; publishing the combined HTML only as a CI artifact, which needs a one-line change to constitution IV. The read-sync sidecar and the TypeScript port stay deferred behind the triggers recorded in `specs/DECISIONS.md`: the sidecar only if offline reads become a requirement or the server-sent event feed measurably misses changes; the port only if a client-side what-if cannot be served by a server dry run in about 250 ms through the tunnel, local-first becomes a requirement, or measured agent friction with .NET remains after CI sharding. Either needs a constitution VII entry that Jay approves.
