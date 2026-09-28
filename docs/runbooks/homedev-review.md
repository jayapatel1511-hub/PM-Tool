# Homedev review release

This is the temporary review and pilot host chosen while Azure is unavailable. It is a **Staging** deployment with clearly fictional seed people/projects and individual local-password IDs. The separate `pm-tool-review-db` volume survives image replacements. Never attach it to a production app or restore its dump into production. Azure infrastructure and real Entra sign-in remain later pilot/production gates.

## Boundaries

- Public address: `https://pm.engcalchub.com`, only after DNS, tunnel, origin and sign-in checks pass. The Cloudflare tunnel terminates TLS and connects to `127.0.0.1:3080` on homedev. Docker port forwarding presents the review bridge gateway `172.30.245.1` to the API; it is the only non-loopback address trusted for one `X-Forwarded-Proto` hop in Staging. Verify that the configured subnet remains unused before creating the stack. `AllowedHosts` is restricted to the public hostname.
- Docker Compose publishes only the API on loopback. PostgreSQL has no host port. Its volume is dedicated to review. The API key directory and reviewer verifier file are owner-only under `.runtime/`, outside Git and image builds.
- Use `scripts/init-homedev-review.py` once in the private host directory. It creates a random database password, an empty verifier list and the persistent key directory without printing the password. Add credentials only for existing active AppUser IDs with `scripts/add-review-credential.py`; never store plaintext passwords.
- The user running Compose needs Docker administrator access. Jay's homedev account currently cannot reach the Docker socket without `sudo`; do not add it to the Docker group or loosen socket permissions. A host administrator enters sudo credentials in their own terminal when starting or backing up this stack.

## Build and activate a reviewed commit

1. On the Mac, finish code review, focused checks and CI, then run `scripts/prepare-homedev-review.sh <full-commit-sha>`. It transfers only that committed Git tree into a new private `releases/<full-sha>` directory and links the shared `.runtime` and `data` directories. It leaves the existing `current` release untouched; Mac's local review database and dirty files are not transferred.
2. On homedev, run `python3 scripts/init-homedev-review.py` from the prepared release only if the shared `.runtime/` has never been initialized. The preparation script makes the shared directory links **before** initialization. Keep `releases/` outside each release and protect the private paths with mode 700.
3. From the prepared release, set `revision=$(basename "$PWD")` and require it to equal the reviewed full commit SHA. Run `sudo env RELEASE_SHA="$revision" docker compose --env-file .runtime/review.env -f hosting/homedev.compose.yml build api`. Check `docker compose config` with dummy values in development only; printing the real expanded config reveals the database password.
4. Before changing the app, run `scripts/backup-homedev-review.sh` and verify the dump is nonempty. Then activate the new image with `sudo env RELEASE_SHA="$revision" docker compose --env-file .runtime/review.env -f hosting/homedev.compose.yml up -d --no-build`. Confirm the database volume identity and that the migration/seed finishes. Keep the prior tagged image and release path for rollback.
5. Probe the private origin with `Host: pm.engcalchub.com` and `X-Forwarded-Proto: https` from homedev. Check `/health`, anonymous `/api/v1/me` denial, unsafe request Origin denial, and local sign-in for a designated synthetic reviewer. A wrong password must fail; never print a cookie or verifier.
6. Only after origin checks, create a dedicated `pm-tool` Cloudflare named tunnel with ingress for `pm.engcalchub.com` to `http://127.0.0.1:3080`, a final `http_status:404` rule, and a user systemd service. Route DNS with `cloudflared tunnel route dns pm-tool pm.engcalchub.com`. Verify the resulting CNAME, HTTPS, host restriction, unauthenticated response, individual login, logout and browser-to-API writes from outside the origin. Do not edit another app's tunnel or DNS route.
7. Verify the version, migration list, logs without secrets, resource limits, and review data persistence after an API restart and a later image replacement. Run `scripts/restore-homedev-review-drill.sh <private-dump-path>`; it restores into a new temporary database, checks the project table, and removes that temporary database. Add the review dump and key directory to the encrypted homedev backup scope and test retrieval. Record times and results in the release gates.

The current directory on homedev contains only empty `hosting`, `.runtime`, `data`, `incoming` and `releases` directories; no PM-Tool process or DNS route existed when this runbook was written. Recheck before using it.

## Rollback and recovery

If a release fails, keep writes closed, switch to the previous image/release and recheck its schema compatibility. An additive migration may be safe for old code but this must be verified; never delete the database volume as rollback. For a data restore, stop writes, restore a verified dump into a **new isolated review database**, test there, then choose a controlled cutover. Retain the original database and dump as evidence. A successful `pg_restore --list` checks dump structure only; it is not a restore drill.

## Gate status at authoring

Compose syntax and API compilation passed on the Mac with dummy settings. Homedev Docker startup, backup/restore, tunnel, DNS, browser flow, access control, uptime and company pilot are **UNPROVEN**. Azure Entra sign-in is **BLOCKED** until company tenant and Azure hosting are available.
