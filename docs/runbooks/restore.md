# Runbook: restore and restore drills

Targets (§22, Q9): lose no more than **15 minutes** of committed changes, and have the service back within **8 hours**.
PostgreSQL Flexible Server keeps continuous point-in-time backups for 14 days (`backupRetentionDays` in
`infra/main.bicep`); there is no zone-redundant standby (Q9 default). Restores are tested before go-live and every quarter.

## Restore production to a point in time

1. **Decide the point**: the last known good time, in UTC. Announce the outage and freeze changes.
2. **Stop writes**: stop the App Service (`az webapp stop -g <rg> -n hub-prod-app`).
3. **Restore to a new server** (the original stays untouched as evidence):
   ```
   az postgres flexible-server restore -g <rg> --name hub-prod-pg-r<yyyymmdd> --source-server hub-prod-pg --restore-time "<UTC time>"
   ```
4. **Grant access**: add the database administrators' Entra group as administrator of the new server, then, signed in as
   that group, create the app's role for its managed identity (`SELECT * FROM pgaadauth_create_principal('hub-prod-app', false, false);`)
   and grant it the same privileges it had on the old server.
5. **Point the app at it**: change `ConnectionStrings__Hub` to the new host (keep `Ssl Mode=Require`), start the app.
   Migrations run at start and are a no-op on a restored database of the same version.
6. **Verify** (checklist below), then reopen and announce. Keep the old server for 14 days, then delete it.

Rebuilding the whole environment is in `environments.md`.

## Verification checklist

- `/health` answers Healthy.
- Sign in as an administrator; Admin → Operations shows no problems (evaluation may run for a few minutes).
- Open a known project: its latest changes before the restore point are there; the activity log's last entry is at or
  just before the point.
- Search finds a known task by key; a report exports.

## Restore drill

Quarterly, and before go-live. Restore **test**, or a copy of production into an isolated resource group that only
operators can reach. Never restore production data into development (§21, FR-007).

1. Note a marker: create a task named `Restore drill <date> <time>` on a test project and write down the time (T).
2. Wait 10 minutes; create a second marker. Choose the restore point R = T + 5 minutes.
3. Restore to R as above (to a new server) and point a test instance of the app at it.
4. Measure: the first marker must exist and the second must not (data loss at most R − T + the backup granularity, under
   15 minutes); record the time from step 3's start to the app answering (under 8 hours).
5. Record the drill below and remove the drill server.

For a local rehearsal of the same steps without Azure, `pg_dump`/`pg_restore` in the development container shows the
application side (a restored database starts, migrates as a no-op and answers).

## Drill record

| Date | Environment | Restore point | Data loss | Time to recover | By | Result |
|---|---|---|---|---|---|---|
| | | | | | | |
