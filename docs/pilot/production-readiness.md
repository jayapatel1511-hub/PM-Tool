# Production readiness: homedev

Jay clarified on 2026-10-01 that review, company pilot and production stay on homedev. No Azure deployment or new
paid service is authorized for this release. Existing Azure templates remain unused; their compile checks are not
production gates. Use `pm.engcalchub.com`, the existing Cloudflare tunnel infrastructure and individual passwords
with credentials outside Git. This document records preparation, not deployment or company acceptance.

## Deployment and data boundary

First complete the persistent synthetic review release, then use the separate company pilot stack described in
[pilot readiness](pilot-readiness.md). Never attach company services to the review database volume or restore a
review dump into company data. Production must retain the accepted pilot data or start fresh according to Jay and
the company's recorded decision; do not create a third stack simply to rename an accepted pilot.

The existing local-password hosting configuration uses ASP.NET Core `Staging`: the application currently refuses
local passwords in `Production`. A business production release and that runtime environment name are separate
facts. Keep the reviewed configuration until its security controls and company acceptance are proven; setting the
environment to `Production` would currently break sign-in. No Azure identity is required to use local accounts.

## Release gates

| Gate | Evidence required | Current status |
|---|---|---|
| Exact code revision | Passing CI and reviewed changes for the deployed full SHA; merge status recorded separately | Earlier security/mobile candidate `6becc79` merged through PR #23 at `27fa370`; exact-head/main CI passed 838/838 and the exact image passed isolated fresh-database safety checks. It is staged but not yet activated. Final company production revision/acceptance remains UNPROVEN |
| Review deployment | Current release pointer, database migration and preserved synthetic records, trusted HTTPS and browser workflows | PASS for bounded synthetic review gates on `02ca7cd`: exact image/volume and current pointer, private/public probes, HTTP 307/HTTPS and preserved marker/session. Earlier security/mobile candidate `6becc79` activation and hosted narrow-screen retest remain open; full company acceptance is UNPROVEN. See the combined checkpoint |
| Company approval | Approved homedev hosting, data classification, hostname and individual-password authentication; sponsor and support owner | UNPROVEN; see pilot approvals |
| Pilot acceptance | Real participants complete the agreed workflows and record go/no-go | UNPROVEN |
| Accounts | First Admin bootstrap and audit entry, Admin-created users, private delivery and handoff deletion, wrong-password denial, non-Admin 403, inactive-user 401, and rotation/removal rejecting an existing cookie | UNPROVEN on hosted company stack |
| Network | Loopback-only origin/database, trusted public TLS, hostile Host and unsafe Origin rejection, and client-IP headers trusted only from the configured proxy bridge | UNPROVEN for final hosted release |
| Persistence | Separate named company database/volume and key ring survive recreation; both seed flags false, database queries show zero @hub.test users and zero ReviewDemo projects | UNPROVEN on homedev company stack |
| Backup and recovery | Scheduled owner-only company dumps, approved encrypted off-host copy, retrieval and isolated restore drill; measured RPO/RTO accepted | UNPROVEN; do not equate a manual dump with scheduled recovery |
| Operations | Existing host monitoring, actionable notification route, disk/capacity checks, daily Operations review and outage procedure | UNPROVEN |
| Notifications | In-app delivery accepted; email remains Log mode unless an existing approved relay is configured | UNPROVEN acceptance; no email delivery claimed |
| Security and accessibility | Hosted role/privacy checks, cookie revocation, threat-model review and required accessibility checks | UNPROVEN final hosted acceptance |
| Cutover | Reviewed SHA, pre-release dump, preserved previous image, health/browser verification and rollback rehearsal | UNPROVEN |

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

Proposed homedev targets are a 24-hour local RPO, 24-hour off-host RPO only while the scheduled copy actually runs,
and an 8-hour RTO with the host intact. Keep daily dumps for 14 days; company approval must record whether these
targets and longer retention are acceptable. Jay approved the review recovery payload in the existing encrypted Mac restic repository, and retrieval plus isolated restore passed. This approval/evidence applies to review data; company-data approval, a working off-host schedule and host-loss recovery remain unproven. Verify and record those gates before go-live. Azure PITR,
managed identity, paid monitoring and Azure mail integrations are not assumed capabilities of this deployment.
