# Runbook: environments

Review, company pilot and production stay on homedev under Jay's 2026-10-01 decision. Use individual local passwords and existing infrastructure. Azure, Entra and new paid services are optional future work, not this release's prerequisites. See [homedev review](homedev-review.md), [pilot readiness](../pilot/pilot-readiness.md) and [production readiness](../pilot/production-readiness.md).

## Environment boundaries

| Environment | Runtime and data boundary |
|---|---|
| Mac development/tests | Development or Testing authentication; synthetic test databases. Development identity headers are never registered in the hosted Staging runtime. |
| Persistent synthetic review | `/home/jaypatel04/Workspace/Projects/pm-tool`, `hosting/homedev.compose.yml`, dedicated review database/volume, runtime and key ring. Preserve reviewer edits across releases. |
| Company pilot and accepted production | `/home/jaypatel04/Workspace/Projects/pm-tool-pilot`, `hosting/homedev-pilot.compose.yml`, separate company database/volume, runtime and key ring. Keep both seed flags false. Retain accepted pilot data or start fresh according to the recorded company decision. |

The local-password provider currently permits ASP.NET Core Development, Staging and Testing, and refuses the runtime name Production. Homedev uses the reviewed Staging configuration, including secure cookies, HTTPS redirection, hostile Host/Origin denial and explicit trusted-proxy limits. A business production cutover is a separate acceptance decision; changing the runtime name to Production would break this authentication mode. Do not enable development authentication, restore review data or turn on either seed flag to provision company users.

`pm.engcalchub.com` is the chosen hostname. Its current review route is not company acceptance. Move the existing dedicated tunnel to the separately verified company origin only at the authorised cutover; leave the review origin private. Never route both environments as though they are the same database.

## Release and recovery

1. Build and test the exact committed source on the Mac. Record review, CI and merge separately. Prepare only a Git archive with `scripts/prepare-homedev-review.sh`; uncommitted output and credentials are not release input.
2. Keep database passwords, salted password verifiers, runtime configuration and persistent protection keys in owner-only `.runtime/` files outside Git. Create real users through the existing bootstrap/Admin flows; credentials map to active account IDs and grant no project roles themselves.
3. Take a fresh private dump before activation. Rehearse fresh and populated migrations and previous-image compatibility. Activate through the existing release script with interactive operator sudo in the homedev terminal.
4. Verify the exact release, migrations, TLS/authentication, project/role restrictions, browser workflows, persistent marker and key/cookie behavior. A healthy endpoint alone is not acceptance.
5. Verify scheduled dumps, approved encrypted off-host retrieval and isolated restore. Review recovery approval applies to the review payload only; company recovery requires its own approved data scope and measured recovery targets. Consult [restore](restore.md) before rollback or recovery.

Current daily dumps and manually verified encrypted retrieval do not establish the specification's 15-minute production RPO or prove an automatic backup. These remain explicit production gates. Do not overwrite newer operational data with an older dump to roll back application code.

## Maintained sources

| Part | Source |
|---|---|
| Review/company container isolation and hardening | `hosting/homedev.compose.yml`, `hosting/homedev-pilot.compose.yml`, `hosting/Dockerfile.review` |
| Backup helpers, units and private recovery | `hosting/`, host `.runtime/` and `data/`, [restore runbook](restore.md) |
| Schema | EF Core migrations under `src/Hub.Api/Data/Migrations`; applied on startup |
| Reference data | `src/Hub.Api/Data/Seed.cs`; company demo/test flags stay false |
| CI | `.github/workflows/ci.yml`; checks code but does not activate homedev |
| Historical Azure design | `infra/main.bicep`, `infra/env/`; unused for this release. Template compilation proves no hosting, identity, mail or recovery capability. |
