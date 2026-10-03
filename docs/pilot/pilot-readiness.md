# Pilot readiness: homedev company pilot

Checklist for moving from the synthetic review release to the company pilot on homedev. It lists what must be true
and who proves it; it is not a readiness claim. Run the pilot itself with the [pilot plan][plan]; homedev production
is covered by [production readiness][prod]. Homedev runs the synthetic review release `02ca7cd`, activated 2026-10-03 01:03 UTC.
Recheck its state before acting on any row ([gates]); the latest [combined checkpoint](../reviews/2026-10-02-combined-candidate.md) records earlier security/mobile candidate `6becc79` merged through PR #23, exact-head/main CI passing 838/838, fresh-image safety checks and separate pilot source staging. The register-completion candidate replaces that activation request; its activation and hosted retest are pending; the live `02ca7cd` retains the narrow-screen defect.

**Decisions in force (Jay, clarified 2026-10-01):** homedev hosts review, company pilot and production at
`pm.engcalchub.com` through the existing Cloudflare tunnel infrastructure. No Azure deployment or new paid service
is part of this release. Users get individual local-password accounts (PBKDF2 verifiers in an owner-only file outside
Git); Entra is optional future work, not a release prerequisite. The review database is never restored into the pilot
or production. Email is
`Email__Mode: Log` (nothing is delivered).

**Owners:** Jay (product owner and homedev operator) · company IT (company IT, security and privacy approvals) · agent
(repository changes and checks, prepared for Jay's review). **Status:** PASS or FAIL (cited record) · BLOCKED (waits on
something outside the repository, cited) · OPEN (known gap in the repository, cited) · UNPROVEN (no evidence yet).

## 1. Entry gates

| # | Item | Owner | Status and record |
|---|---|---|---|
| E1 | AC-VIS-01 to AC-VIS-08 pass on the exact pilot commit (FR-016, [§35.1][spec10]) | agent | UNPROVEN. Evidence in the `specs/022`–`024` verification records predates packets 025–033 |
| E2 | Threat-model walkthrough held, with the homedev boundary added (TLS ends at Cloudflare's edge, local-password cookie, single host); findings resolved or accepted (FR-011) | Jay + company IT | UNPROVEN. The walkthrough record is empty ([threat model][threats]) |
| E3 | Packets 025–033 enabled only after their own acceptance ([pilot plan][plan]) | agent | UNPROVEN full acceptance for 025–033. Implementation and combined checks pass; current hosted/browser/company limits are recorded in the combined checkpoint and packet verification records |
| E4 | Review-release environment gates pass first | agent + Jay | PARTIAL: `02ca7cd` activation/private probes 10/10, public probes 9/9, HTTP 307/HTTPS, browser sign-in/session, original review marker and desktop keyboard return passed. The register-completion candidate includes the earlier locally proven narrow-screen fixes; its activation/hosted retest are pending; first automatic review backup is unproven |
| E5 | 50 participants named (Q16); the proposed 3 PMs and 6 projects confirmed; sponsor named | Jay | UNPROVEN |

## 2. Company approvals

| # | Approval | Owner | Status |
|---|---|---|---|
| A1 | Data classification. Project data is internal-confidential ([§21][spec09]) and includes employee personal data. Approve holding it on homedev (Jay's personal laptop server on his home network), passing it through Cloudflare's edge in Jay's account (the tunnel terminates TLS there, [homedev runbook][homedev]) and keeping encrypted backups on Jay's Mac or another approved store | company IT | UNPROVEN |
| A2 | Authentication exception. §21 and goal T2 require Entra sign-in with no Hub passwords. The pilot uses Hub passwords issued by Jay, with no MFA and no self-service change or reset: only sign-in and sign-out exist ([Program.cs][program] lines 135–143) | company IT | UNPROVEN |
| A3 | Privacy review of 50 employees' names, emails, job titles, office, supervisor and activity history; notice to participants | company IT | UNPROVEN |
| A4 | No pilot project's client contract forbids holding its coordination data outside company systems | Jay (with the pilot PMs) | UNPROVEN |
| A5 | Host baseline for IT review: disk encryption, OS patching, physical security, administrator access (Jay only), key-only SSH | Jay documents, company IT accepts | UNPROVEN |
| A6 | Hostname. The `engcalchub.com` zone is in Jay's Cloudflare account (homedev platform guide, outside this repository); the company accepts it for company data | company IT | UNPROVEN |
| A7 | Optional access gate in front of the app (for example Cloudflare Access limited to company addresses). None is configured | company IT + Jay | UNPROVEN |
| A8 | Penetration test before organisation-wide rollout if the organisation requires one (T-18); not a pilot entry gate | company IT | UNPROVEN |

## 3. Separate pilot stack

The review stack is wired to review data ([compose]). Reusing it unchanged would reuse the review volume, synthetic
seed and secrets. The pilot needs its own copy of each part below. Never attach the pilot to `pm-tool-review-db` or
restore a `hub-review-*` dump into it.

| # | Part | Review value | Pilot requirement | Owner | Status |
|---|---|---|---|---|---|
| H1 | Compose project | `name: pm-tool-review`, image `pm-tool-review:<sha>` | own file and project, for example `pm-tool-pilot` | agent | UNPROVEN: written as `hosting/homedev-pilot.compose.yml` (project `pm-tool-pilot`, image `pm-tool-pilot:<sha>`); `docker compose config` validated with dummy values; not run |
| H2 | Database | volume `pm-tool-review-db`; database and user `hub_review` | new volume, database, user and password | agent | UNPROVEN: volume `pm-tool-pilot-db`, database/user `hub_pilot`, password from `.runtime/pilot.env`; not run |
| H3 | Seed | `Seed__ReviewDemo: "true"` | `"false"`. After first start, `SELECT count(*) FROM hub.app_user WHERE email LIKE '%@hub.test'` and `SELECT count(*) FROM hub.project WHERE external_source = 'ReviewDemo'` both return 0 | agent | UNPROVEN |
| H4 | Environment | `ASPNETCORE_ENVIRONMENT: Staging` | stays Staging: local-password sign-in is refused in Production ([Auth.cs][auth] line 62) | agent | UNPROVEN: set in the pilot compose file |
| H5 | Bridge network and trusted proxy | subnet `172.30.245.0/28`; its gateway `172.30.245.1` is also `Hosting__LocalTunnelProxyAddress` | own unused /28; proxy address equals the new gateway | agent | UNPROVEN: `172.30.246.0/28`, proxy `172.30.246.1`; no homedev route used that subnet on 2026-10-01 |
| H6 | Loopback port | `127.0.0.1:3080` | own free loopback port | agent | UNPROVEN: `127.0.0.1:3081`, free on homedev on 2026-10-01 |
| H7 | Private runtime | `.runtime/review.env`, `review-users.json`, `keys/` and `data/backups/`, shared by all review releases | separate owner-only runtime and backup directories | agent | PREPARED: dedicated pilot directories are mode 700; runtime is empty awaiting approved Admin/participants; no review secrets/data copied |
| H8 | Scripts and units | init, activate, backup, restore drill and verify scripts; the root backup helper accepts only `pm-tool-review-db-1` and the review backup path ([helper]) | pilot variants that refuse review containers, volumes and dumps | agent | PARTIAL: isolated initializer `scripts/init-homedev-pilot.py` passed private/no-overwrite/review-runtime refusal checks; root helper `hosting/pm-tool-pilot-backup-root.sh` (accepts only `pm-tool-pilot-db-1` on volume `pm-tool-pilot-db`, writes `hub-pilot-*.dump` to the pilot directory) with its service and 22:15 UTC timer; pilot activation, backup, restore-drill and private-verification variants are now written; focused safety checks pass on Mac and homedev Ubuntu (14 total across backup helpers, initializer and operations, with fake Docker/sudo only); no company stack or timer installed |
| H9 | Hostname | review sets `AllowedHosts` and `Email__BaseUrl` to `pm.engcalchub.com`; one hostname routes to one origin | decide which stack `pm.engcalchub.com` serves during the pilot; the other gets another hostname in the `pm-tool` tunnel or stays loopback-only | Jay | DECIDED in homedev production gates: route the hostname to company port 3081 after acceptance, retain review on private loopback 3080; cutover remains UNPROVEN |
| H10 | Capacity | each stack limits the database and the API to 1 GiB each; homedev has about 7.2 GiB RAM shared with other services (platform guide) | measure with both stacks running, or stop the review stack during the pilot | agent + Jay | UNPROVEN |
| H11 | Releases | [homedev runbook][homedev]: reviewed full SHA, CI pass, dump first, previous image kept | the same, announced in the support channel and run outside business hours | agent + Jay | UNPROVEN |

## 4. Pilot accounts

Local-password sign-in maps each login to an existing active `AppUser` and grants no roles. The reviewed Staging bootstrap provisions the approved first Admin with an audit entry; that Admin creates real people through Users & roles. Bootstrap no longer re-grants an explicitly removed Admin role or reactivates a disabled person. The synthetic review identities are never copied to the company stack. Hosted provisioning remains unproven until the first Admin and participant roles are supplied.

| # | Item | Owner | Status |
|---|---|---|---|
| U1 | A reviewed provisioning path for real users and the first Admin, written to the activity log. Store each person's Entra sign-in name (UPN) as their email and leave the Entra object ID empty, so a later Entra sign-in claims the same record ([Auth.cs][auth] line 150) | agent | IMPLEMENTED LOCALLY: audited Staging local-Admin bootstrap and Admin create-user endpoint/UI; live browser/company provisioning acceptance remains UNPROVEN |
| U2 | Participant list (name, UPN, office, system role) from the sponsor | Jay | UNPROVEN |
| U3 | One login per person with a random password: `scripts/bootstrap-review-credentials.py` for the first batch (it requires an empty verifier file), `scripts/add-review-credential.py` afterwards (12+ characters). At most 64 logins ([LocalPasswordStore.cs][store] line 34). File changes are reloaded; recreate the API container after atomic host-file replacement so the new inode is mounted. Verifier rotation/removal then ends prior sessions on their next request | Jay | UNPROVEN |
| U4 | Private delivery: one person per message through a company-approved channel, never group chats, tickets or shared mailboxes. Delete the handoff file after delivery; record the delivery date, not the password | Jay | UNPROVEN |
| U5 | Rotation on suspected exposure or request: remove the entry, add a new one with the existing credential helper, then recreate the API container. Once the new verifier is visible, existing cookies are refused on their next request by the verifier-stamp check ([Auth.cs][auth]); eight hours is the absolute expiry, not a revocation delay | Jay (agent may script it) | UNPROVEN |
| U6 | Offboarding: (1) Admin → Users, clear Active; the next request with an existing cookie is refused ([Auth.cs][auth] line 152); (2) Reassign work ([admin guide][admin]); (3) remove the verifier and recreate the API container. Reactivation can revive an unexpired cookie if the same verifier remains; remove or rotate the verifier before reactivation. Leavers are not detected automatically (`Graph__DirectorySync: "false"`); the sponsor reports them | Jay + company IT | UNPROVEN |
| U7 | Sign-in allows five attempts a minute per client address and per normalised login name ([Program.cs][program]); IPv6 clients share a /64 bucket. People behind one office address share the address limit, and distributed attempts against one login share its name limit. Confirm egress addresses and the one-minute retry tradeoff | company IT + agent | UNPROVEN |
| U8 | Walkthrough and user guide explain ID and password sign-in (the [user guide](../user-guide.md) now explains individual local IDs and passwords) | agent | UNPROVEN |

## 5. Backup and recovery

Proposal for Jay and company acceptance. The 15-minute RPO in §22 is the Azure point-in-time target, not homedev's.

| Measure | Pilot proposal | Basis |
|---|---|---|
| RPO, local | 24 h: daily dump at 22:00 UTC plus a dump before each release; every 4 business hours if the company requires less | [timer], [homedev runbook][homedev] |
| RPO, off-host | 24 h while a daily off-host copy runs; longer while the Mac is away from the home network | platform guide: the Mac job needs the Mac awake on the LAN |
| RTO, host intact | 8 h: restore the latest verified dump into a new isolated database, check it, switch over | [homedev runbook][homedev] rollback; §22 |
| RTO, host lost | rebuild on another machine from Git and the off-host copy; not rehearsed | none |
| Retention | daily dumps for 14 days, weekly dumps until the exit decision, then disposal (X5) | proposal |

| # | Must be proven | Owner | Status and record |
|---|---|---|---|
| B1 | Pilot dump helper and timer installed; the first automatic run leaves a nonempty, owner-only archive | agent + Jay (sudo) | UNPROVEN for pilot. Review timer is installed; its first automatic run is due 2026-10-03 22:00 UTC and remains unproven ([combined checkpoint](../reviews/2026-10-02-combined-candidate.md)) |
| B2 | Restore drill of a timer-produced pilot dump into an isolated temporary database; counts compared and time recorded | agent + Jay | UNPROVEN for pilot. A manually invoked review backup-service dump and the separately retrieved review dump restored successfully; neither proves the first automatic timer run |
| B3 | Off-host encrypted copy. The independent whole-server Mac export failed at 2026-10-03 01:30 UTC after its two-hour SSH timeout. The approved manual PM-only review recovery snapshot passed. A dedicated pilot copy schedule and destination still require company acceptance; it must include dumps, verifier/configuration and protection keys, independently of the whole-host export | Jay approves destination and schedule (company IT too, for company data); agent prepares | FAIL (independent whole-host export); PASS (manual PM-only review recovery); UNPROVEN (scheduled pilot copy) |
| B4 | Retrieval: fetch a dump from the off-host copy, compare SHA-256, restore-drill it | agent + Jay | UNPROVEN for the pilot. Manual review retrieval PASS (snapshot `2582b812`, [combined checkpoint](../reviews/2026-10-02-combined-candidate.md)); byte hashes matched and isolated restore kept three projects, three users and 27 migrations |
| B5 | Dumps cannot cross over: separate directories and file prefixes; the pilot helper refuses review containers; pilot dumps never go into review, development or test (FR-007) | agent | UNPROVEN |

## 6. Monitoring

| # | Item | Owner | Status |
|---|---|---|---|
| M1 | Proposed Uptime Kuma monitor (Kuma listens on homedev `127.0.0.1:3001`; not configured): HTTP(s) keyword check of `https://<pilot hostname>/health` for `Healthy`, 60 s interval, 10 s timeout, 2 retries (the existing monitors' settings). It covers DNS, Cloudflare, the tunnel, the API and the database: `/health` reports `Degraded` after 10 minutes of evaluation lag and answers 503 when the database fails ([Program.cs][program] lines 120–130) | Jay | UNPROVEN |
| M2 | A Kuma notification destination; none was configured at the platform's last recorded status | Jay | UNPROVEN |
| M3 | Kuma runs on the same laptop, so a power, host or home-network outage silences it. An off-host check is a separate decision | Jay | UNPROVEN |
| M4 | Homedev has no Application Insights, so the [job alerts][jobs] (nightly missed, job failed, digest late, heartbeat) are log lines only. Check Admin → Operations each business day | Jay | UNPROVEN |
| M5 | Business-hours uptime recorded weekly in the pilot log; no availability claim for the pilot | Jay | UNPROVEN |

## 7. Mail

| # | Item | Owner | Status |
|---|---|---|---|
| L1 | Current: `Email__Mode: Log` and `Graph__Mail: "false"` ([compose]). No daily digest, weekly summary or email notification is delivered; in-app notifications only. Accept this for the pilot and note it beside G4, or approve L2 or L3 | Jay + company IT | UNPROVEN (undecided) |
| L2 | SMTP relay: `Email__Mode=Smtp`, host, port 587 with TLS, user and password kept owner-only on homedev; the company relay or an approved provider | company IT | UNPROVEN |
| L3 | Microsoft Graph from a service mailbox: a company app registration with `Mail.Send` limited by an application access policy (T-16); on homedev its credential would be a client secret or certificate in the environment | company IT | UNPROVEN |
| L4 | With a route approved: `Email__BaseUrl` set to the pilot address; a digest and a weekly summary received in Outlook (unverified, [implementation status][status]) | agent + Jay | UNPROVEN |

## 8. Support and incidents

| # | Item | Owner | Status |
|---|---|---|---|
| S1 | Sponsor and one local champion per office named ([pilot plan][plan]) | Jay | UNPROVEN |
| S2 | One support channel answered the same day; tickets carry the screen, item key and time, never comment text or client documents | Jay | UNPROVEN |
| S3 | Operator: Jay only; Docker on homedev needs his interactive sudo ([homedev runbook][homedev]). The company accepts this or names a second operator | Jay + company IT | UNPROVEN |
| S4 | Security contact and reporting deadline for a suspected credential leak or data exposure | company IT | UNPROVEN |
| S5 | Incident steps written: outage notice in the support channel (no email); compromised login → deactivate, then rotate; sign everyone out → replace the pilot key ring and restart; data error → stop writes, restore into a new isolated database, controlled cutover ([homedev runbook][homedev]); save logs promptly (Compose keeps 3 × 10 MB per container) | agent drafts, Jay approves | UNPROVEN |

## 9. Exit after 8 weeks

| # | Criterion | Source | Owner | Status |
|---|---|---|---|---|
| X1 | G1–G6 read weekly for 8 weeks in the weekly log | [pilot plan][plan]; FR-001 | Jay | UNPROVEN |
| X2 | Targets: G1 every project every week; G2 trending to zero after week 4; G3 same day; G4 everyone weekly without being chased; G5 no surprise submission; G6 all walkthrough users unaided | [pilot plan][plan] measures; §5.1 | Jay | UNPROVEN |
| X3 | Threshold changes logged with reasons; enabled coordination capabilities and scenario feedback recorded each week | [pilot plan][plan] | Jay | UNPROVEN |
| X4 | Go/no-go recorded as a Decision on the "Hub rollout" project: measures, options (roll out, extend, stop), outcome and conditions such as T-18 and open threat-model items | [pilot plan][plan]; [§35.1][spec10] | Jay (the sponsor decides) | UNPROVEN |
| X5 | Data disposition: migrate or start fresh ([production readiness][prod] §6), then delete homedev pilot data, dumps and off-host copies as the company decides, and retire local logins | proposal, not in the specification | Jay + company IT | UNPROVEN |

The participant count does not certify 50 simultaneous sessions or a server size (Q16).

[plan]: pilot-plan.md
[prod]: production-readiness.md
[gates]: ../reviews/2026-09-27-review-release-gates.md
[homedev]: ../runbooks/homedev-review.md
[jobs]: ../runbooks/jobs.md
[threats]: ../security/threat-model.md
[admin]: ../admin-guide.md
[status]: ../IMPLEMENTATION-STATUS.md
[compose]: ../../hosting/homedev.compose.yml
[helper]: ../../hosting/pm-tool-review-backup-root.sh
[timer]: ../../hosting/pm-tool-review-backup.timer
[auth]: ../../src/Hub.Api/Infrastructure/Auth.cs
[store]: ../../src/Hub.Api/Infrastructure/LocalPasswordStore.cs
[program]: ../../src/Hub.Api/Program.cs
[admincs]: ../../src/Hub.Api/Features/Admin.cs
[spec09]: ../../spec-parts/09-security-nfr-architecture-db-api-integration.md
[spec10]: ../../spec-parts/10-scope-acceptance-edge-cases-closing.md


## Prepare the first pilot Admin (operator step, after company gates)

Keep the pilot base separate from `/home/jaypatel04/Workspace/Projects/pm-tool`; use the dedicated
`pm-tool-pilot` base, its own `.runtime`, data and release links. Run `scripts/init-homedev-pilot.py` with
`--runtime` pointing to that new private `.runtime` and the approved first Admin's `--admin-email` (UPN) and
`--admin-name`. It creates `pilot.env`, an empty `pilot-users.json`, and an independent key ring; it refuses existing
review/pilot credential files or keys. The hostname is the designated `pm.engcalchub.com`; no DNS cutover is performed.

Start only the separate pilot Compose stack after the review and company gates pass. Startup audits the first Admin
and provides its GUID in the database. Use `scripts/add-review-credential.py <pilot-users-file> <user-guid> <individual-id>`
to set that person's password privately; despite the helper name it writes only the explicit file passed. Sign in as
Admin and create the remaining people in Users & roles, provisioning each GUID independently. Remove
`Auth__Local__BootstrapAdmins` from the private env file after bootstrap. Never restore a review dump or copy its
verifier/key files. Initializer regressions pass locally; the real company database, installed timer, pilot activation,
mail and browser-to-host acceptance remain UNPROVEN.

Once the first Admin has an individual credential, run the following from the separate pilot release directory:

```bash
bash scripts/activate-homedev-pilot.sh <full-sha> --verify-file <private-file> [--install-timer]
```

 The private verification
file contains only that operator-selected account's `login` and `password`, is owner-only, and must stay outside
Git and the release source tree. Delete it after verification; it is not the verifier list. An empty first-start
verifier list cannot prove authentication: a run without explicit verification credentials must stop before
claiming activation acceptance or updating the current pointer. Initial bootstrap and credential provisioning
are operator steps, not permission to import review identities. These scripts perform private origin checks;
trusted public HTTPS and real browser acceptance remain separate gates.
