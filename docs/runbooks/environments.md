# Runbook: environments

Development, test and production are separate Azure resource groups with separate Entra app registrations (§21, FR-007).
Production data is never copied into development or test; the synthetic data in `tools/scale/seed.sql` exists for load
tests. Each environment can be rebuilt from its definitions in under a day.

For a review preview, use a dedicated non-production database and explicitly set `Seed__ReviewDemo=true`. The app adds
clearly marked fictional people and projects once and keeps reviewer edits on later releases. `Seed:ReviewDemo` is
refused in Production; do not restore the review database into production. Hosted Staging still uses Entra sign-in for
real reviewers, while the `@hub.test` people are sample records only. Development sign-in must stay local or behind a
separate access gate, because its identity header is not suitable for an open Internet site.

## What defines an environment

| Part | Where |
|---|---|
| Azure resources: App Service, PostgreSQL (Entra authentication only, 14-day point-in-time restore), Key Vault, Log Analytics, Application Insights, optional alert rules | `infra/main.bicep`, `infra/env/<env>.bicepparam` |
| Database schema | EF Core migrations in `src/Hub.Api/Data/Migrations`, applied when the app starts |
| Reference data (disciplines, types, settings defaults) | `src/Hub.Api/Data/Seed.cs`, applied at start |
| Application | the CI build of `main` (`.github/workflows/ci.yml`) |
| Secrets | Key Vault only (for example an SMTP relay password if that mail route is used); the database has no password |

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
