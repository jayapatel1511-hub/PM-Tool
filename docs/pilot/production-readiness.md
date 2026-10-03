# Production readiness: homedev

Jay clarified on 2026-10-01 that review, company pilot and production stay on homedev. No Azure deployment or new
paid service is authorized for this release. Existing Azure templates remain unused; their compile checks are not
production gates. Use `pm.engcalchub.com`, the existing Cloudflare tunnel infrastructure and individual passwords
with credentials outside Git. This document records preparation, not deployment or company acceptance.

Both public review (`3080`) and the separate private synthetic pilot (`3081`) use executable
`ac494a8ba70d50517f540ad7e0acced85d84897e`. The current location checkpoint below records merged CI,
activation and live browser/export evidence. The 18:24 30-user/10-project rehearsal on `3bfea232` passed
its bounded sign-in, project-access, logical-restore and paused native-PITR checks. That historical drill was
not repeated for this client form release. Neither result makes production live or establishes company acceptance.

## Deployment and data boundary

First complete the persistent synthetic review release, then use the separate company pilot stack described in
[pilot readiness](pilot-readiness.md). Never attach company services to the review database volume or restore a
review dump into company data. Production must retain the accepted pilot data or start fresh according to Jay and
the company's recorded decision; do not create a third stack simply to rename an accepted pilot.

The existing local-password hosting configuration uses ASP.NET Core `Staging`: the application currently refuses
local passwords in `Production`. A business production release and that runtime environment name are separate
facts. Keep the reviewed configuration until its security controls and company acceptance are proven; setting the
environment to `Production` would currently break sign-in. No Azure identity is required to use local accounts.

## Current location release checkpoint — 2026-10-03 19:33 UTC

Both isolated homedev runtimes now use executable `ac494a8ba70d50517f540ad7e0acced85d84897e`: public synthetic review on loopback `3080` at `https://pm.engcalchub.com`, and private synthetic pilot on loopback `3081`. PR #31 merged at `1175598d732ab890b48fef18ada32ae22b86e200`; candidate/main trees equal `3d8ac30d508340c7fea4aae57636f5634321d633`. Candidate CI `37146135878` and merged-main CI `37146643841` passed, including 858 API/rules tests and the three browser suites.

The operator log `data/location-activation-ac494a8b-20261003T191817077097641Z.log` records review private probes 10/10, review public probes 9/9, pilot private probes 10/10, and both final current pointers. A subsequent read-only public HTTPS check confirmed the original review persistence marker and the two original task progress/versions/dates unchanged (`pm-tool/data/hosted-preservation-ac494a8b.json`). The pilot remains 30 active fictional users and 10 Setup projects in 10 actual categories. The six original password verifiers, the 24 newly prepared password verifiers and both runtime configurations are preserved. No review database, credentials or protection keys were copied into the pilot.

Public signed-in browser acceptance on this executable created only the labelled synthetic issue `SYNTH-GATES-1003-I01`. Building (site area, building, level, room and asset), Alignment (12.25–18.75 m), Coordinate (fictional decimal X/Y/Z, EPSG:26920, m) and SiteArea saved and survived full reload. A fresh HTTPS API session then checked the exact persisted values and null inapplicable fields; CSV and Excel each retained all four locations in one complete cell. Credential-free evidence is `pm-tool/data/hosted-location-ac494a8b.json`. The browser download-event capture timed out; these export PASS results cover actual HTTPS export responses, not a confirmed native browser download.

Both system-level backup timers are active, due at 22:00 UTC (review) and 22:15 UTC (pilot) on October 3; their `LastTriggerUSec` values are still empty. First automatic execution remains **UNPROVEN**. This client form release did not repeat the 18:24 native pilot PITR drill or the earlier review workflow slices. Full packet acceptance, native print, off-host WAL replay/full application DR, accepted RPO/RTO, real company acceptance and production deployment remain **UNPROVEN**. No Azure deployment or new paid service was introduced.

## Release gates

| Gate | Evidence required | Current status |
|---|---|---|
| Exact code revision | Passing CI and reviewed changes for the deployed full SHA; merge status recorded separately | Both current executables `ac494a8ba70d50517f540ad7e0acced85d84897e`, PR #31 merged at `1175598d732ab890b48fef18ada32ae22b86e200`, equal trees; candidate CI `37146135878` and main CI `37146643841` PASS including 858 tests. The `211bd88` / `3bfea232` workflow and recovery results below are historical. Full company acceptance remains UNPROVEN |
| Review deployment | Current release pointer, database migration and preserved synthetic records, trusted HTTPS and browser workflows | PASS for current `ac494a8b` pointers, 10/10 private and 9/9 public probes, and the live location checks above. Historical `211bd88` checks covered exact image/volume, HTTP 307/HTTPS, preserved original marker/tasks, signed-in narrow-screen controls and populated workflow slices. Native print and full company acceptance remain UNPROVEN. See the completion checkpoint |
| Company approval | Approved homedev hosting, data classification, hostname and individual-password authentication; sponsor and support owner | UNPROVEN; see pilot approvals |
| Pilot acceptance | Real participants complete the agreed workflows and record go/no-go | UNPROVEN |
| Accounts | First Admin bootstrap and audit entry, Admin-created users, private delivery and handoff deletion, wrong-password denial, non-Admin 403, inactive-user 401, and rotation/removal rejecting an existing cookie | UNPROVEN on hosted company stack |
| Network | Loopback-only origin/database, trusted public TLS, hostile Host and unsafe Origin rejection, and client-IP headers trusted only from the configured proxy bridge | UNPROVEN for final hosted release |
| Persistence | Separate named company database/volume and key ring survive recreation; both seed flags false, database queries show zero @hub.test users and zero ReviewDemo projects | UNPROVEN on homedev company stack |
| Backup and recovery | Scheduled owner-only company dumps, approved encrypted off-host copy, retrieval and isolated restore drill; measured RPO/RTO accepted | UNPROVEN; do not equate a manual dump with scheduled recovery |
| Operations | Existing host monitoring, actionable notification route, disk/capacity checks, daily Operations review and outage procedure | UNPROVEN |
| Notifications | In-app delivery accepted; email remains Log mode unless an existing approved relay is configured | UNPROVEN acceptance; no email delivery claimed |
| Database runtime | Migration/bootstrap account isolated from the ordinary API; observed app sessions/ACLs deny schema/trigger/audit mutation | PASS for the bounded private pilot activation: 29 migrations, deployed least-privilege `hub_pilot_app` ordinary role, and activation gates passed. Broader company-hosted acceptance remains UNPROVEN |
| Security and accessibility | Hosted role/privacy checks, cookie revocation, threat-model review and required accessibility checks | UNPROVEN final hosted acceptance |
| Cutover | Reviewed SHA, pre-release dump, preserved previous image, health/browser verification and rollback rehearsal | UNPROVEN |

Jay requested a separate **synthetic pilot rehearsal** with fictional users/projects. Its private runtime is activated and its bounded rehearsal passed; the result cannot close company approval, real pilot acceptance or production deployment.

## Cutover and rollback

1. Record pilot acceptance and the decision to retain pilot data or start fresh. Keep the synthetic review stack
   private when `pm.engcalchub.com` moves to company use.
2. Verify the exact candidate's CI, take and restore-check a company dump, and retain the previous image and runtime
   configuration. Never copy review credentials or its key ring into the company stack.
3. Deploy to the company stack, verify migrations and preserved records, then route the existing dedicated tunnel
   to `127.0.0.1:3081` for the company stack; verify the actual ingress and remove the public review route to
   `127.0.0.1:3080`. Keep review loopback-only. Verify trusted HTTPS, individual sign-in, role restrictions and core workflows.
4. Record the deployed SHA and company go/no-go before declaring production accepted.

Before company writes, roll back only to the known compatible previous image and origin. After writes or schema
changes, do not overwrite current data with an old dump: stop writes and assess migration compatibility, then fix
forward or perform a controlled restore with an explicit data-loss decision. Keep the latest company dump and logs.

The specification's 15-minute RPO is not met by daily logical dumps. The alternative targets below are a proposal, not an approved relaxation or a production PASS. Scheduled off-host recovery and measured host-loss recovery also remain unproven.

Proposed homedev targets are a 24-hour local RPO, 24-hour off-host RPO only while the scheduled copy actually runs,
and an 8-hour RTO with the host intact. Keep daily dumps for 14 days; company approval must record whether these
targets and longer retention are acceptable. Jay approved the review recovery payload in the existing encrypted Mac restic repository, and retrieval plus isolated restore passed. This approval/evidence applies to review data; company-data approval, a working off-host schedule and host-loss recovery remain unproven. Verify and record those gates before go-live. Azure PITR,
managed identity, paid monitoring and Azure mail integrations are not assumed capabilities of this deployment.

### Pilot recovery preparation checkpoint — current bounded evidence (2026-10-03 18:24 UTC)

The [restore runbook](../runbooks/restore.md#pilot-pitr-bounded-on-host-proof-off-host-recovery-unproven) records
locally verified PostgreSQL 17 WAL/base/replay preparation. The current private pilot rehearsal passed native paused
PITR with `before=1` and `after=0` at 18:24 UTC, plus local logical restore of 10 projects. The timer is active for
22:15 UTC but `LastTriggerUSec` is empty, so automatic operation remains UNPROVEN. Bounded off-host transfer,
retrieved replay, full application recovery, the 15-minute RPO and the 8-hour RTO remain unproven. The
30-person/10-project fixture is synthetic, not company participant acceptance or permission to cut over production.
