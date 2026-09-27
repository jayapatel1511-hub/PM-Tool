# Runbook: environments

Development, test, review and production are separate Azure resource groups with separate Entra app registrations (§21, FR-007).
Production data is never copied into development, test or review; the synthetic data in `tools/scale/seed.sql` exists for load
tests. Keep the review database when releasing new preview builds.

For a review preview, deploy `infra/env/review.bicepparam` to a dedicated review resource group. Its
`hub-review-pg` database persists across preview releases; `infra/main.bicep` sets `Seed__ReviewDemo=true` only there. The app adds
clearly marked fictional people and projects once and keeps reviewer edits on later releases. `Seed:ReviewDemo` is
refused in Production; do not restore the review database into production. The review app uses `LocalPassword`
authentication with a separate ID and verifier for each reviewer until company Entra sign-in is available. Development
identity headers are never accepted in Staging. Review credentials map to existing active `AppUser` IDs; they cannot
create users or grant project roles. The review-only `review-users` Key Vault secret contains a JSON `users` array in
the format produced by `scripts/add-review-credential.py`; create and verify that secret before starting the review app.
The secret must not enter Git, terminal logs or a deployment package. The Azure template denies every inbound
review IP by default; supply approved `reviewAllowedCidrs` before reviewers access it. Bootstrap the new vault with
an owner-only file containing `{ "users": [] }` using `az keyvault secret set --vault-name hub-review-kv --name review-users --file <private-file>`.
This lets the app seed the separate review database while no login is possible. Query the seeded active `AppUser` IDs
through the authorised database admin connection, then run `scripts/add-review-credential.py` for each reviewer using
an owner-only JSON file outside the repository. Replace the Key Vault secret from that file and restart the app so it
loads the new verifiers. Persist `/home/hub-review-keys` across preview releases so signed-in sessions survive an app
restart. The final Azure pilot must switch to Entra
and exercise real tenant sign-in. Synthetic `@hub.test` people are reserved records. Keep
`pm.engcalchub.com` unassigned until DNS, hosting, and access controls are verified for the intended environment.

## What defines an environment

| Part | Where |
|---|---|
| Azure resources: App Service, PostgreSQL (Entra authentication only, 14-day point-in-time restore), Key Vault, Log Analytics, Application Insights, optional alert rules | `infra/main.bicep`, `infra/env/<env>.bicepparam` |
| Database schema | EF Core migrations in `src/Hub.Api/Data/Migrations`, applied when the app starts |
| Reference data (disciplines, types, settings defaults) | `src/Hub.Api/Data/Seed.cs`, applied at start |
| Application | the CI build of `main` (`.github/workflows/ci.yml`) |
| Secrets | Key Vault for review password verifiers and any mail relay password; the database has no password |

## Rebuild

1. Create the resource group and fill `infra/env/<env>.bicepparam` (tenant, app registrations, database admin group,
   `operatorEmail` if alerts are wanted).
2. `az deployment group create -g <rg> -f infra/main.bicep -p infra/env/<env>.bicepparam`
3. Signed in as the database administrators' group, create the app's role for its managed identity:
   `SELECT * FROM pgaadauth_create_principal('hub-<env>-app', false, false);` and grant it `CREATE` on the `hub` database.
4. Put any mail secret into the Key Vault, and set `Graph:DirectorySync`, `Graph:Mail` / `Email:*` app settings.
5. Deploy the build (`az webapp deploy` with the published zip). On start the app migrates the schema and seeds reference data.
6. Assign the first administrator the Entra app role `Hub.Admin` (group-assigned roles sync at each sign-in), sign in, and check `/health` and Admin → Operations.
7. For production only: restore data from the latest backup instead of starting empty (see `restore.md`).

Typical time: under two hours without data; restore time adds to it.
