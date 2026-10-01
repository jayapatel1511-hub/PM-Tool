# Homedev review release

This is the temporary review and pilot host chosen while Azure is unavailable. It is a **Staging** deployment with clearly fictional seed people/projects and individual local-password IDs. The separate `pm-tool-review-db` volume survives image replacements. Never attach it to a production app or restore its dump into production. Azure infrastructure and real Entra sign-in remain later pilot/production gates.

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

## Rollback and recovery

If a release fails, keep writes closed, switch to the previous image/release and recheck its schema compatibility. An additive migration may be safe for old code but this must be verified; never delete the database volume as rollback. For a data restore, stop writes, restore a verified dump into a **new isolated review database**, test there, then choose a controlled cutover. Retain the original database and dump as evidence. A successful `pg_restore --list` checks dump structure only; it is not a restore drill.

## Gate status at authoring

Homedev Docker startup, loopback health, anonymous denial, host restriction and unsafe Origin denial passed for the first private revision. A private `pg_dump` archive was restored into a separate temporary database on the same homedev PostgreSQL server; live/restored row counts matched, and the archive was retained under the owner-only `data/backups/` directory. That dump was also stored as encrypted Mac restic snapshot `224a6c27`; a temporary retrieval matched its SHA-256 and was removed. The existing scheduled whole-host backup is **FAIL**: its last successful snapshot is 2026-09-20 and the latest fixed-LAN SSH export failed on 2026-09-28. Credential sign-in, public tunnel/DNS, deployed browser flow, uptime and company pilot remain **UNPROVEN**. Azure Entra sign-in is **BLOCKED** until company tenant and Azure hosting are available.
