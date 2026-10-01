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
| T1 | Optional App Service VNet integration and PostgreSQL private endpoint/private DNS resources are now supported, with public database access disabled only when the app subnet, private endpoint subnet and private DNS zone are supplied together. The environment files intentionally leave those company network inputs empty, so no path is claimed yet | `appSubnetResourceId`, `dbPrivateEndpointSubnetResourceId`, `dbPrivateDnsZoneResourceId`, `db` resources; [§21, §23.7][spec09] | agent + company IT | BLOCKED: network IDs and private DNS design are required |
| T2 | Optional verified hostname binding with an existing App Service certificate thumbprint is now supported. `pm.engcalchub.com` remains unbound until DNS, certificate and hostname verification are approved | `appHostname`, `appCertificateThumbprint`, `appHostnameBinding` | agent + company IT | BLOCKED: DNS and certificate inputs are required |
| T3 | The complete production app-setting contract is now template-owned: `AllowedHosts`, `Email__*`, `Graph__*`, and the Key Vault reference for an SMTP password. Values remain empty or disabled until the company supplies the approved hostname, mail route and Graph registration | `appSettings` and parameters | agent + company IT | BLOCKED: company configuration and secret names are required |
| T4 | Added an Application Insights `/health` availability test, availability query alert and PostgreSQL CPU-above-80% metric alert. They are gated on `operatorEmail`, which remains empty until an operator route is approved | `availabilityTest`, `dbCpuAlert`, alert section | agent + company IT | BLOCKED: operator route and deployed-fire evidence are required |
| T5 | Added optional Key Vault Secrets Officer assignment for an operator Entra group; the group ID remains empty so no access is granted by default | `operatorGroupObjectId`, `operatorVaultReader` | agent + company IT | BLOCKED: company operator group is required |
| T6 | Added an optional `nameSuffix` parameter for globally unique App Service, PostgreSQL, Key Vault, plan, and alert names. The default remains unchanged and uniqueness is still unverified | `nameSuffix`, `name` variable | agent + company IT | UNPROVEN |
| T7 | Added a disabled-by-default `staging` App Service slot using the same reviewed settings and health check. Enabling it still requires the deployment/cutover decision and managed-identity database access review | `enableDeploymentSlot`, `appSlot` | agent + company IT | BLOCKED: CI/CD and cutover choice are required |
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
| D4 | The template now owns `ConnectionStrings__Hub`; a restore still requires the approved target host to be supplied through the deployment parameters or a reviewed deployment override | app `appSettings` | agent + company IT | BLOCKED: restore target and deployment procedure are not approved |

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
| P4 | Template hooks for T1–T7 are implemented and compile-checked; T1–T5 and T7 remain blocked on company inputs, T6/T8 remain unproven, and deployment review is outstanding | agent + company IT | BLOCKED |
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
