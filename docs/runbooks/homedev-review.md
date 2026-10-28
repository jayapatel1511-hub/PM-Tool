# Homedev review release

Homedev hosts review, company pilot and production under Jay's 2026-10-01 clarification; no Azure deployment or new paid service is part of this release. This review stack is a **Staging** deployment with clearly fictional seed people/projects and individual local-password IDs. The separate `pm-tool-review-db` volume survives image replacements. Never attach it to a company app or restore its dump into pilot or production. Company hosting and authentication acceptance remain required; Azure and Entra are optional future work.

## Boundaries

- Public address: `https://pm.engcalchub.com`, only after DNS, tunnel, origin and sign-in checks pass. The Cloudflare tunnel terminates TLS and connects to `127.0.0.1:3080` on homedev. Docker port forwarding presents the review bridge gateway `172.30.245.1` to the API; it is the only non-loopback address trusted for one `X-Forwarded-Proto` hop in Staging. Verify that the configured subnet remains unused before creating the stack. `AllowedHosts` is restricted to the public hostname.
- Docker Compose publishes only the API on loopback. PostgreSQL has no host port. Its volume is dedicated to review. The API key directory and reviewer verifier file are owner-only under `.runtime/`, outside Git and image builds.
- Use `scripts/init-homedev-review.py` once in the private host directory. It creates a random database password, an empty verifier list and the persistent key directory without printing the password. Add credentials only for existing active AppUser IDs. The initial three-user bootstrap uses `scripts/bootstrap-review-credentials.py`; it writes PBKDF2 verifiers and a temporary owner-only password handoff file without printing passwords. Deliver those passwords privately and remove the handoff file after distribution. For later individual additions, use `scripts/add-review-credential.py`.
- The user running Compose needs Docker administrator access. Jay's homedev account currently cannot reach the Docker socket without `sudo`; do not add it to the Docker group or loosen socket permissions. A host administrator enters sudo credentials in their own terminal when starting or backing up this stack.

## Build and activate a reviewed commit

1. On the Mac, finish code review, focused checks and CI, then run `scripts/prepare-homedev-review.sh <full-commit-sha>`. It transfers only that committed Git tree into a new private `releases/<full-sha>` directory and links the shared `.runtime` and `data` directories. It leaves the existing `current` release untouched; Mac's local review database and dirty files are not transferred.
2. On homedev, run `python3 scripts/init-homedev-review.py` from the prepared release only if the shared `.runtime/` has never been initialized. The preparation script makes the shared directory links **before** initialization. Keep `releases/` outside each release and protect the private paths with mode 700.
3. From the prepared release, set `revision=$(basename "$PWD")` and require it to equal the reviewed full commit SHA. Run `sudo env RELEASE_SHA="$revision" docker compose --env-file .runtime/review.env -f hosting/homedev.compose.yml build api`. Check `docker compose config` with dummy values in development only; printing the real expanded config reveals the database password.
4. Before changing the app, run `scripts/backup-homedev-review.sh` and verify the dump is nonempty. Then activate the new image with `sudo env RELEASE_SHA="$revision" docker compose --env-file .runtime/review.env -f hosting/homedev.compose.yml up -d --no-build`. Confirm the database volume identity and that the migration/seed finishes. Keep the prior tagged image and release path for rollback.
5. Probe the private origin with `Host: pm.engcalchub.com` and `X-Forwarded-Proto: https` from homedev. Check `/health`, anonymous `/api/v1/me` denial, unsafe request Origin denial, and local sign-in for a designated synthetic reviewer. A wrong password must fail; never print a cookie or verifier.
6. Only after origin checks, create a dedicated `pm-tool` Cloudflare named tunnel with ingress for `pm.engcalchub.com` to `http://127.0.0.1:3080`, a final `http_status:404` rule, and a user systemd service. Route DNS with `cloudflared tunnel route dns pm-tool pm.engcalchub.com`. Verify the resulting CNAME, HTTPS, host restriction, unauthenticated response, individual login, logout and browser-to-API writes from outside the origin. Do not edit another app's tunnel or DNS route.
7. Verify the version, migration list, logs without secrets, resource limits, and review data persistence after an API restart and a later image replacement. Run `scripts/restore-homedev-review-drill.sh <private-dump-path>`; it restores into a new temporary database, checks the project table, and removes that temporary database. The Workspace path, including private dumps and the key directory, is in the encrypted Mac restic backup scope; test retrieval. Install and prove the daily database timer below. Record times and results in the release gates.

Steps 3–5 are scripted: from `releases/<full-sha>` on homedev run `bash scripts/activate-homedev-review.sh <full-sha> [--install-timer]` in your own terminal (sudo prompts there). It builds the image, dumps the database, starts the API on the existing volume, prints migration/error log lines without secrets, runs `scripts/verify-homedev-review.py` (health, anonymous/Host/Origin denial, wrong-password denial, individual sign-in, `/me`, sign-out) and only then moves `current`. `--install-timer` also installs the daily dump timer, runs it once and restores that dump in an isolated drill database. `python3 scripts/verify-homedev-review.py https://pm.engcalchub.com` repeats the probes through the tunnel; the user unit is `hosting/pm-tool-tunnel.service`. Sign-in throttling behind the tunnel is keyed on Cloudflare's client address only when the request comes from the trusted bridge gateway.

Before activating, count evaluation events already stuck at the worker's 10-attempt limit:

```bash
sudo docker exec pm-tool-review-db-1 psql -U hub_review -d hub_review -Atc "SELECT count(*) FROM hub.outbox_event WHERE processed_at IS NULL AND attempts >= 10;" </dev/null
```

The first release with migration `20261018214033_OutboxDeadLetter` dead-letters those events while it starts, so a nonzero count means the `OutboxDeadLettered` problem appears on Admin → Operations as soon as the upgrade lands. That is expected, not a failed release: follow the `OutboxDeadLettered` row in `docs/runbooks/jobs.md` and retry after reading the errors. Record the count with the release.

### Tuesday hosted review fixture activation

Local-password sessions live in `hub.local_auth_session` on the existing review database. Tickets are protected with
the persistent Data Protection keys. Logout deletes only the current session; copied-cookie replay must return 401.
Other sign-ins remain valid, and verifier rotation/removal still invalidates the person's sessions. Activation of this
session-store version rejects old stateless cookies, requiring a fresh sign-in. Each new sign-in removes at most 100
expired session rows. Preserve the database and key directory across replacement; never restore a pre-logout dump
into the active stack, which could restore revoked sessions. Rollback to a stateless version reopens the logout risk.

After the exact new review release has been activated and its private health/auth checks pass, run the operator wrapper from `releases/<full-sha>` in Jay's own terminal:

```bash
python3 scripts/seed-homedev-tuesday-review.py <full-review-sha>
```

The wrapper rechecks the exact hosted release before any SQL, credential or API mutation; validates all 13 active reserved AppUsers (`email`, reserved `EntraObjectId`, UUID); preserves the legacy Taylor/Jay/Yagmur verifier credentials; and creates the owner-only `.runtime/tuesday-review-login-handoff.json` for the ten Tuesday personas. It force-recreates the API at the exact SHA, waits for the proxy health response, verifies each local-password sign-in as the same person, waits between rate-limited phases, and runs `seed_resources.py`, `seed_modules.py` and `seed_planning.py` through the hosted API transport. It writes only private handoff/log files and `data/tuesday-fixtures-<sha>.json`; passwords, hashes and cookies must never be logged. A rerun validates the current verifier, handoff, reserved IDs and actual fixture counts before treating the manifest as complete. Do not use a database import, touch pilot data, or run this before exact release activation.

#### Packet 036 staffing fixtures (opt-in)

Add `--with-staffing-fixtures` only before Jay's S1 (staffing) and S2 (dependency review) sessions of packet 036 (`specs/036-staffing-and-signal-trust/verification.md` section 10):

```bash
python3 scripts/seed-homedev-tuesday-review.py <full-review-sha> --with-staffing-fixtures
```

The flag runs `tools/preview/seed_staffing.py` through the same hosted transport after the three default seeds; on a release whose fixture manifest is already complete it runs only that seed, after the usual rerun checks, and records it in the manifest. It is off by default because it changes organisation-wide data: it turns on working days for the whole organisation and adds a Riverside office with one holiday, besides SYN-104, SYN-105, the open roles and the stalled items (verification section 8).

Run it on the day of S2, and hold S1 the same day where possible. SYN-105's lag states (section 8.2, browser version) are true only on the day it is seeded: every start and finish happens that day and the holiday falls two working days later. The seed creates SYN-104 and SYN-105 once per review database and a later run makes no writes, so a second run on another day does not refresh them: on any later day the states have moved on from the table.

## Daily review database dump

The timer runs at 22:00 UTC, before the Mac's 20:30 Halifax off-host restic schedule in either daylight or standard time. It is a separate local dump; its success does **not** prove that the off-host schedule ran. The helper is copied to a root-owned path because systemd must not execute a script from the user-writable release tree as root. It targets only the `pm-tool-review-db-1` container and writes a mode-600 archive into the existing user-owned, mode-700 `data/backups` directory.

After reviewing the helper in the exact release, install and exercise it from homedev with Jay's interactive sudo. Do this only after the private review database is healthy:

```bash
sudo install -D -o root -g root -m 0755 hosting/pm-tool-review-backup-root.sh /usr/local/libexec/pm-tool-review-backup
sudo install -o root -g root -m 0644 hosting/pm-tool-review-backup.service /etc/systemd/system/pm-tool-review-backup.service
sudo install -o root -g root -m 0644 hosting/pm-tool-review-backup.timer /etc/systemd/system/pm-tool-review-backup.timer
sudo systemctl daemon-reload
sudo systemd-analyze verify /etc/systemd/system/pm-tool-review-backup.service /etc/systemd/system/pm-tool-review-backup.timer
sudo systemctl start pm-tool-review-backup.service
sudo systemctl status pm-tool-review-backup.service --no-pager
sudo systemctl enable --now pm-tool-review-backup.timer
systemctl list-timers pm-tool-review-backup.timer --all
```

Check that the new archive is nonempty, owner-only, and listed by `pg_restore`; then restore **that archive** into an isolated temporary review database with the drill script. Confirm the timer's next run and inspect the first automatic run. Keep manual pre-release dumps regardless of the timer. The existing Mac restic job is currently failing, so fix and recheck that job and retrieve a newly scheduled dump from the encrypted repository before counting off-host backup as operational. Preserve archives until a retention policy and recovery point target are approved; do not copy them into production.

At the first private activation, revision `1c59e334b42822510dd0181f83e5dadc7bbe8282` started the API on loopback port 3080 and a healthy dedicated PostgreSQL container. The named Cloudflare tunnel configuration was validated, but the tunnel was not started and DNS was not routed. Three synthetic verifier mappings and a temporary private handoff file were prepared afterward; the API still needs a restart to load them. Recheck this state before using the runbook.

## Verifiable activity log (packet 048)

`flag.verifiable_log` stays **off** on the review stack. Turning it on is Jay's decision and is one-way: once a chain exists the Admin setting refuses to turn it off. The Admin setting also refuses to turn it on while older activity-log rows are unsealed, so the switch-on is the one-off migration-only run in `specs/048-verifiable-records/audit-initialization.md`. The compose file has no service for it; run it from the release that is currently active (`current`), with the same `RELEASE_SHA`, in Jay's own terminal. With social sign-in active, add `-f hosting/social-auth.override.yml` to `compose` and `--social` to the probe.

```bash
revision=$(basename "$PWD")
compose=(sudo env RELEASE_SHA="$revision" docker compose --env-file .runtime/review.env -f hosting/homedev.compose.yml)
sudo systemctl stop pm-tool-review-deploy.timer   # only if the CI-image timer is installed
bash scripts/backup-homedev-review.sh             # verify the dump is nonempty
"${compose[@]}" stop api </dev/null
"${compose[@]}" run --rm -e Db__MigrateOnly=true -e Db__InitializeAuditChains=true api </dev/null
"${compose[@]}" up -d --no-build --no-deps api </dev/null
python3 scripts/verify-homedev-review.py http://127.0.0.1:3080 --expect-build "$revision"
sudo systemctl start pm-tool-review-deploy.timer  # only if it was stopped above
```

The run migrates and seeds as a normal start would, then turns the setting on and seals existing rows in one transaction and exits without serving requests; a failed seal rolls back and leaves the flag off. Afterwards Admin → Operations must show no `AuditChainBroken` problem.

Limitation: the review stack has one database role, `hub_review`, which owns the tables and is used by migrations, the API and backups alike. The separation the procedure assumes (a migration identity that may UPDATE `hub.activity_log`, and a runtime role limited to SELECT and INSERT) does not exist here, so the review API itself could alter or delete sealed rows. Treat a review-stack chain as a rehearsal of the procedure, not as tamper evidence. The pilot stack's separate runtime role (`hosting/pilot-runtime-role.sql`) is where that boundary holds.

## Rollback and recovery

If a release fails, keep writes closed, switch to the previous image/release and recheck its schema compatibility. An additive migration may be safe for old code but this must be verified; never delete the database volume as rollback. For a data restore, stop writes, restore a verified dump into a **new isolated review database**, test there, then choose a controlled cutover. Retain the original database and dump as evidence. A successful `pg_restore --list` checks dump structure only; it is not a restore drill.

## Gate status at authoring

Homedev Docker startup, loopback health, anonymous denial, host restriction and unsafe Origin denial passed for the first private revision. A private `pg_dump` archive was restored into a separate temporary database on the same homedev PostgreSQL server; live/restored row counts matched, and the archive was retained under the owner-only `data/backups/` directory. That dump was also stored as encrypted Mac restic snapshot `224a6c27`; a temporary retrieval matched its SHA-256 and was removed. The existing scheduled whole-host backup is **FAIL**: its last successful snapshot is 2026-09-20 and the latest fixed-LAN SSH export failed on 2026-09-28. Credential sign-in, public tunnel/DNS, deployed browser flow, uptime and company pilot remain **UNPROVEN**. Azure Entra sign-in is outside the current homedev release scope; individual-password hosted acceptance remains required.


## Verifier replacement in the container

The application reloads a changed verifier file and binds cookies to the current verifier stamp. Credential helpers
atomically replace the host file. Because Compose mounts that individual file, recreate the API container after a
replacement to ensure the new host inode is mounted. From the exact prepared review release (substitute its reviewed
full SHA; do not print the expanded real Compose configuration):

```bash
sudo env RELEASE_SHA=<full-sha> docker compose --env-file .runtime/review.env -f hosting/homedev.compose.yml up -d --no-deps --no-build --force-recreate api </dev/null
```

The database and persistent key directory are retained. Verify the old cookie is refused, the new individual password
works, and other people's sessions still work. Do not mark rotation accepted solely because the JSON was edited.
For the separate pilot, use its own compose file, pilot runtime and pilot release SHA; never pass review settings.

## Current repository checkpoint (2026-10-01 Halifax)

CI [run 36943469659](https://github.com/jayapatel1511-hub/PM-Tool/actions/runs/36943469659) passed for
`07fe142362bb477f0d0042ba3b17c11f7619f5b9`: 569 tests, zero failures/skips, coverage gates, seven backup/pilot
initializer checks, frontend build/lint, three mocked Chromium suites, dependency and trace checks. Lint reported
88 warnings and zero errors. Later homedev-scope documentation and pilot operation changes require their own
verification; this run is evidence for its exact SHA only.

The d4dc1ef review candidate image built, but activation was observed stopped at the pre-release database dump
with a stopped Docker Compose process. The current pointer still selected `1c59e334b42822510dd0181f83e5dadc7bbe8282`
and its private health endpoint answered Healthy. This is not evidence of candidate deployment. The operator must
interrupt the stopped foreground pipeline and rerun activation with `</dev/null`; sudo reads the controlling
terminal, while batch Docker commands receive no terminal input. Recheck the log, current pointer and all private
gates before starting the dedicated tunnel or routing DNS.

Pilot operations preparation: the separate activation/backup/restore/private verifier scripts are now integrated
with the existing pilot initializer and root backup helper. Four backup-helper, three initializer and seven pilot
operation regressions passed on both Mac and homedev Ubuntu using fake Docker/sudo commands. They cover private
backup/restore inputs, timer dump names, stdin isolation, preflight credential refusal, cookie replay and transport
failure. This does not prove real pilot activation, installed timers, public TLS or company acceptance.

## Optional CI-image review timer (packet 039)

Source and mocked tests are prepared; **not installed or activated**. Jay first provisions
`/etc/pm-tool-review-deploy/ghcr-token` as a root-owned mode-600 file containing a read-only package token,
with root-owned mode-755 parent. Never put it in a release or print it. From the prepared, manually verified
release, the explicit `--install-deploy-timer` option on `activate-homedev-review.sh` copies the compose and
probe to root-owned paths, keeps review runtime credentials private, authenticates root Docker with the token
on stdin into `/etc/pm-tool-review-deploy/docker`, installs the root helper/service/timer and enables the
five-minute timer. The review user list and key ring are linked to the release's live `.runtime` files rather than
copied, so a reviewer added later reaches the next timer deployment, which recreates the API container. This needs
Jay's sudo and is a deployment action, outside local source verification.
The ordinary activation path and existing `--install-timer` daily backup option keep their previous meaning.
The installer refuses to continue until the daily backup helper is installed (`--install-timer`, in the same or an
earlier run), because the timer uses it for every pre-deployment dump.

The timer verifies CI provenance for the exact digest, installed compose/probe hashes and any newest contract
migration before pulling. It checks the backup helper is installed, preserves the running immutable image under a
separate rollback tag, pulls, takes a verified dump, starts only review API without building, then runs the installed
private probe. The dump waits for a good pull, so a registry outage does not leave a new dump every five minutes. After a
successful deployment it deletes older `pm-tool-review` images beyond the newest three (the running image, its
rollback image and one more); images with any other tag are kept. A failed
candidate is recorded and never retried; a different digest can deploy after rollback. Root-owned state at
`/var/lib/pm-tool-review-deploy/state.json` is readable by Jay and records SHA, digest and UTC time. `current`
continues to mean the last manual activation. No database volume or down migration is used by rollback.

After a compose, social override, probe or credential change, repeat the explicit installation option to
refresh root-owned copies. Packet 042 compose/probe changes require this refresh. Until then the hash guard
stops the timer. A contract migration also stops it until Jay applies that contract manually. Check
`journalctl -u pm-tool-review-deploy.service` for stop reasons; the helper suppresses subprocess output that
could include secrets. A failed rollback needs manual recovery; do not retry a failed digest by editing state.

For manual pilot promotion, choose a review-proven digest (review probes passed, default two-day soak and a
green nightly for the same commit) and use the pilot activation script's optional
`--image-digest ghcr.io/jayapatel1511-hub/pm-tool@sha256:<digest>`. It verifies CI provenance, the SHA/build
labels and tags the pulled image; without that option it builds as before. Pilot/production remain manual.
Live registry/timer installation, same-volume deployment and failed-probe rehearsal are UNPROVEN locally.

Registry metadata reads use Docker's documented [`imagetools inspect` JSON configuration fields](https://docs.docker.com/reference/cli/docker/buildx/imagetools/inspect/#json-output), before the candidate image is pulled. Registry-backed execution remains a separate operator check.
