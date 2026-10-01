# Production readiness: Azure

Checklist for the Azure production environment that follows the homedev pilot ([pilot readiness][pilot]). Nothing
here has been deployed: `infra/main.bicep` has only been compiled ([implementation status][status]). Owners and
status words are the same as in the pilot checklist: PASS or FAIL (cited record), BLOCKED (waits on something outside
the repository), OPEN (known gap in the repository, cited), UNPROVEN (no evidence yet).

## 1. What the template provisions

[`infra/main.bicep`][bicep] deploys one environment into one resource group, with names `hub-<env>-*`.

| Resource | Name | Settings |
|---|---|---|
| Log Analytics | `hub-<env>-logs` | retention 90 days in prod, 30 elsewhere |
| Application Insights | `hub-<env>-insights` | workspace-based; connection string set as an app setting |
| Key Vault | `hub-<env>-kv` | standard, RBAC, purge protection; tenant is the subscription's tenant |
| PostgreSQL Flexible Server | `hub-<env>-pg`, database `hub` | version 17, `Standard_D2ds_v5` General Purpose, 128 GB with auto-grow, 14-day point-in-time restore (PITR), no geo-redundant backup, no high availability, Entra authentication only (password authentication disabled), administrator is an Entra group; `CITEXT` and `PG_TRGM` allow-listed (the migrations use both) |
| App Service plan and app | `hub-<env>-plan`, `hub-<env>-app` | Linux P1v3, .NET 10, system-assigned identity, HTTPS only, TLS 1.2, FTPS off, Always On, health check `/health`; reaches the database with its managed identity, no password |
| Role assignment | app identity → Key Vault Secrets User | the only role assignment in the template |
| Alerts | action group `hub-<env>-operators` and three log rules (OpsAlert, heartbeat, 5xx above 5 %) | deployed only when `operatorEmail` is set |
| Outputs | `appHostName` (the default `*.azurewebsites.net` host), `appPrincipalId` | — |

| Setting by environment | dev | test | review | prod |
|---|---|---|---|---|
| `ASPNETCORE_ENVIRONMENT` | Staging | Staging | Staging | Production |
| `Auth__Mode` | Entra | Entra | LocalPassword (verifiers from secret `review-users`) | Entra |
| `Seed__ReviewDemo` | false | false | true | false |
| Inbound default | Allow | Allow | Deny except `reviewAllowedCidrs` | Allow |
| Log retention | 30 days | 30 days | 30 days | 90 days |

The four parameter files in `infra/env/` set `env` and leave `entraTenantId`, `apiAudience`, `spaClientId`, `apiScope`,
`dbAdminGroupObjectId` and `dbAdminGroupName` empty. None sets `operatorEmail` (so no alerts are deployed), SKUs,
retention or location, so the defaults above apply.

**Local validation, 2026-10-01:** Bicep CLI 0.47.16, `bicep build infra/main.bicep --stdout` and `bicep build-params`
for all four parameter files compiled with no errors or warnings. This is a compile check only: no `what-if`, no
deployment.

## 2. Template gaps

| # | Gap | Source | Owner | Status |
|---|---|---|---|---|
| T1 | No PostgreSQL firewall rule, VNet integration or private endpoint, so neither the app nor the administrators' group has a network path to the server. §21 requires private networking or a firewall limited to App Service outbound addresses; §23.7 names private endpoint or VNet integration | `db` resource; [§21, §23.7][spec09] | agent | OPEN |
| T2 | No custom hostname or certificate. `pm.engcalchub.com` appears nowhere in `infra/`; the app answers only on its default host | `app` resource, `appHostName` output | agent | OPEN |
| T3 | Production app settings missing: `Email__BaseUrl` (empty, so email links are relative, [Notify.cs][notify] line 40), `AllowedHosts` (`*` in [appsettings.json][appsettings]), the mail route and directory sync (`Email__*`, `Graph__*`). A template deployment sets the whole app-settings list, so values set by hand ([environments][env] rebuild step 4, [restore] step 5) are replaced on the next deployment. Make them parameters | `app` `appSettings` | agent | OPEN |
| T4 | No availability test on `/health` and no database CPU alert ([§22 monitoring, §23.8][spec09]); only the three log alerts | alert section | agent | OPEN |
| T5 | Operators have no Key Vault role: the RBAC vault grants only the app's identity, so writing `review-users` or a mail secret needs Key Vault Secrets Officer assigned outside the template | `vaultReader` | agent + company IT | OPEN |
| T6 | Fixed names `hub-<env>-app`, `-pg` and `-kv` must be globally unique and may already be taken; purge protection keeps a deleted vault's name reserved through soft-delete retention. Add a name suffix parameter if needed | `name` variable | agent | UNPROVEN |
| T7 | No deployment slot (blue/green is a recommendation in §23.7); the cutover below assumes none | `app` | agent | OPEN |
| T8 | Key Vault uses `subscription().tenantId`; PostgreSQL and the API use `entraTenantId`. Deploy into a subscription of the company tenant | `vault`, `db` | company IT | UNPROVEN |

## 3. Hostname and Entra registrations

`AGENTS.md` designates `pm.engcalchub.com` for PM-Tool's deployment, application URLs and authentication callbacks. It
exists only in the original checkout's local `main` (commit `1639968`), not in this branch's history. The infrastructure
assumes no other custom name; it has none (T2). The README uses the placeholder `<app-host>`, and the homedev compose
file hard-codes `pm.engcalchub.com` for the review stack. The name serves homedev until cutover, so production tests
before cutover use the default host.

| # | Item | Values for `prod.bicepparam` | Owner | Status |
|---|---|---|---|---|
| R1 | Hub API registration: expose `api://<api-app-id>` with scope `access_as_user`; `requestedAccessTokenVersion` 2; app roles `Hub.Admin`, `Hub.Executive`, `Hub.Supervisor`, `Hub.ProjectManager`, `Hub.ReadOnly` assigned to IT-managed groups ([README], §23.6) | `apiAudience` (API client ID), `apiScope` (`api://<api-app-id>/access_as_user`) | company IT | BLOCKED: no tenant access ([gates], Azure pilot Entra sign-in) |
| R2 | Hub SPA registration: single-page application platform, delegated `access_as_user`, admin consent. MSAL sends `window.location.origin` as the redirect URI, with no trailing slash ([auth.tsx][msal] line 77); register `https://pm.engcalchub.com` in the form actually sent (the README shows `https://<app-host>/`). Add the default host only for tests before cutover and remove it afterwards | `spaClientId` | company IT | BLOCKED |
| R3 | Tenant and the database administrators' group | `entraTenantId`, `dbAdminGroupObjectId`, `dbAdminGroupName` | company IT | BLOCKED |
| R4 | First administrator holds `Hub.Admin` through a group ([environments][env] rebuild step 6) | — | company IT | BLOCKED |
| R5 | Optional Graph permissions for the app identity: `User.Read.All` for directory sync and `Mail.Send` restricted to one mailbox by an application access policy (Q3, T-16) | `Graph__*` app settings (T3) | company IT | BLOCKED |
| R6 | Separate registrations and hostnames for dev, test and review (§21); not decided | their parameter files | company IT + Jay | UNPROVEN |
| R7 | The company accepts `pm.engcalchub.com` (a zone in Jay's Cloudflare account) for production or names another; DNS, binding and certificate follow | — | company IT + Jay | UNPROVEN |

## 4. Key Vault secrets

| Secret | Environment | Purpose | Status |
|---|---|---|---|
| `review-users` | review only | JSON verifier list read through `Auth__Local__UsersJson`; the app fails to start if the reference does not resolve | UNPROVEN: Key Vault resolution unverified (T-12) |
| SMTP relay password | any, only with `Email__Mode=Smtp` | relay login; no template setting references it yet (T3) | UNPROVEN |
| none for the database or Entra | prod | the database uses the managed identity; Entra IDs are public values | — |

A rotation schedule for any secret (FR-009; §21 TBD) is owned by company IT and UNPROVEN.

## 5. Database recovery

| # | Item | Owner | Status |
|---|---|---|---|
| D1 | PITR for 14 days without geo-redundant backup (Q9). Geo-redundant backup can be chosen only when the server is created, so decide before the first deployment | company IT | UNPROVEN |
| D2 | No high availability (Q9 default: 99.5 % of business hours) | company IT | UNPROVEN |
| D3 | Restore drill before go-live and every quarter, against RPO 15 minutes and RTO 8 hours ([restore], FR-005) | agent + company IT | BLOCKED: no Azure environment; the drill record is empty |
| D4 | [restore] step 5 sets `ConnectionStrings__Hub` by hand; the next template deployment resets it to the original server (T3) | agent | OPEN |

## 6. Pilot data: migrate or start fresh

Jay and company IT decide; status UNPROVEN. The review database is never a source, and Production refuses the review
seed and local passwords ([Program.cs][program] lines 159–161, [Auth.cs][auth] line 62).

| | A. Migrate the pilot database (only if approved) | B. Fresh start |
|---|---|---|
| Steps | Freeze pilot writes. Take a final `pg_dump -Fc --no-owner --no-privileges` of the pilot database. Confirm it holds no review data (0 `@hub.test` users, 0 `ReviewDemo` projects). Restore into `hub` as the administrators' group over TLS. Give the app role ownership or the privileges its start-up migrations need (T-09). Start the same application version, so migrations are a no-op. Compare row counts and spot-check projects | Production starts with reference data only. PMs recreate live projects (templates and copy-structure help). The pilot database stays read-only as go/no-go evidence, then is deleted as the company decides |
| Conditions | Pilot user emails equal their Entra UPNs ([pilot readiness][pilot] U1), so each first Entra sign-in claims the existing record ([Auth.cs][auth] line 150). PostgreSQL 17 on both sides | None |
| Keeps | Activity history, attributed to the same people | Nothing from the pilot |
| Costs | A rehearsal in an isolated, operator-only resource group first ([restore] drill rule) | Re-entry effort; pilot history stays outside production |

## 7. Cutover and rollback

Cutover, after the gates in §8 pass:

1. Rehearse option A or B end to end in an isolated, operator-only resource group.
2. Add the `asuid.pm.engcalchub.com` TXT record so App Service accepts the name while its CNAME still points at the
   homedev tunnel.
3. Announce the window. Stop the pilot API (keep its database), take the final dump, then restore it (A) or prepare the
   empty environment (B), and verify.
4. Point the `pm.engcalchub.com` CNAME at the production app's default host (`appHostName`) and bind a certificate.
   App Service managed certificates need the name to resolve to the app, so check the Cloudflare proxy setting first.
5. Check HTTPS, `/health`, Entra sign-in for one person in each system role and Admin → Operations; then open and
   announce.
6. Remove `pm.engcalchub.com` from the homedev tunnel ingress. Keep the pilot database, its last dump and the local
   logins until the rollback window closes.

Rollback:

- Before step 4: restart the pilot API on homedev and discard the Azure copy.
- After step 4, before any production write: point the CNAME back at the tunnel and restart the pilot API.
- After production writes: nothing syncs back. Fix forward on Azure (previous build, or PITR per [restore]). State this
  point of no return in the cutover announcement.

## 8. Gates

| # | Gate | Owner | Status and record |
|---|---|---|---|
| P1 | Company subscription and resource groups | company IT | BLOCKED: unavailable ([gates], Azure pilot Entra sign-in) |
| P2 | Entra registrations, groups and consent (R1–R5) | company IT | BLOCKED |
| P3 | Parameter values filled, including `operatorEmail` | company IT + agent | BLOCKED: every value is empty in `infra/env/*.bicepparam` |
| P4 | Template gaps T1–T8 fixed and reviewed | agent | OPEN |
| P5 | Template compiles | agent | PASS: local compile 2026-10-01 (§1); also [packet 011 verification][v011] |
| P6 | `what-if` and deployment; managed-identity database access; Key Vault reference resolution (T-12) | agent + company IT | BLOCKED: needs P1–P3; deployment unverified ([implementation status][status]) |
| P7 | HTTPS redirection, HSTS and host handling behind App Service; the homedev forwarded-header handling is Staging-only ([Program.cs][program] lines 79–92) | agent | UNPROVEN ([gates], homedev proxy origin handling) |
| P8 | Real tenant sign-in, AC-AUTH-01 to AC-AUTH-04 | Jay + company IT | BLOCKED ([gates]) |
| P9 | Directory sync and a mail route (Q3, T-16); a digest received in Outlook | company IT | BLOCKED |
| P10 | Alert rules deployed and each fired once (FR-006) | agent + company IT | BLOCKED: no operator address |
| P11 | PITR restore drill (FR-005, D3) | agent + company IT | BLOCKED |
| P12 | Performance repeated against the Azure database (FR-013) | agent | BLOCKED ([implementation status][status]) |
| P13 | Threat-model walkthrough; T-09 migration role; T-18 penetration test decision | Jay + company IT | UNPROVEN ([threat model][threats]) |
| P14 | Hostname accepted (R7), DNS and certificate | Jay + company IT | UNPROVEN |
| P15 | Pilot go/no-go recorded and the §6 option decided | Jay + company IT | UNPROVEN ([pilot readiness][pilot] X4–X5) |
| P16 | CI/CD choice (§23.7 TBD; manual `az` sequence today) and log retention (30–90 days TBD) | company IT | UNPROVEN |

[pilot]: pilot-readiness.md
[gates]: ../reviews/2026-09-27-review-release-gates.md
[env]: ../runbooks/environments.md
[restore]: ../runbooks/restore.md
[threats]: ../security/threat-model.md
[status]: ../IMPLEMENTATION-STATUS.md
[v011]: ../../specs/011-hardening-and-pilot/verification.md
[README]: ../../README.md
[bicep]: ../../infra/main.bicep
[appsettings]: ../../src/Hub.Api/appsettings.json
[notify]: ../../src/Hub.Api/Infrastructure/Notify.cs
[auth]: ../../src/Hub.Api/Infrastructure/Auth.cs
[program]: ../../src/Hub.Api/Program.cs
[msal]: ../../web/src/lib/auth.tsx
[spec09]: ../../spec-parts/09-security-nfr-architecture-db-api-integration.md
