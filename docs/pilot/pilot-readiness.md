# Pilot readiness: homedev company pilot

Checklist for moving from the synthetic review release to the company pilot on homedev. It lists what must be true
and who proves it; it is not a readiness claim. Run the pilot itself with the [pilot plan][plan]; the later Azure move is
in [production readiness][prod]. Homedev is reachable again at the latest 2026-10-01 checkpoint; it still runs the first private review release.
Recheck its state before acting on any row ([gates]).

**Decisions in force (Jay):** homedev hosts the synthetic review release and the company pilot until Azure is
available, at `pm.engcalchub.com` through a dedicated Cloudflare named tunnel. Pilot users get individual
local-password accounts (PBKDF2 verifiers in an owner-only file outside Git) until Entra is available. Production is
Azure with Entra sign-in. The review database is never restored into the pilot or production. Email is
`Email__Mode: Log` (nothing is delivered).

**Owners:** Jay (product owner and homedev operator) · company IT (company IT, security and privacy approvals) · agent
(repository changes and checks, prepared for Jay's review). **Status:** PASS or FAIL (cited record) · BLOCKED (waits on
something outside the repository, cited) · OPEN (known gap in the repository, cited) · UNPROVEN (no evidence yet).

## 1. Entry gates

| # | Item | Owner | Status and record |
|---|---|---|---|
| E1 | AC-VIS-01 to AC-VIS-08 pass on the exact pilot commit (FR-016, [§35.1][spec10]) | agent | UNPROVEN. Evidence in the `specs/022`–`024` verification records predates packets 025–033 |
| E2 | Threat-model walkthrough held, with the homedev boundary added (TLS ends at Cloudflare's edge, local-password cookie, single host); findings resolved or accepted (FR-011) | Jay + company IT | UNPROVEN. The walkthrough record is empty ([threat model][threats]) |
| E3 | Packets 025–033 enabled only after their own acceptance ([pilot plan][plan]) | agent | FAIL for 028–033, UNPROVEN for 025–027 ([gates], packet rows) |
| E4 | Review-release environment gates pass first | agent + Jay | FAIL: individual sign-in, scheduled backup, security/accessibility/operations. BLOCKED: `pm.engcalchub.com` ([gates]) |
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
| H7 | Private runtime | `.runtime/review.env`, `review-users.json`, `keys/` and `data/backups/`, shared by all review releases | separate owner-only runtime and backup directories | agent | UNPROVEN |
| H8 | Scripts and units | init, activate, backup, restore drill and verify scripts; the root backup helper accepts only `pm-tool-review-db-1` and the review backup path ([helper]) | pilot variants that refuse review containers, volumes and dumps | agent | UNPROVEN: root helper `hosting/pm-tool-pilot-backup-root.sh` (accepts only `pm-tool-pilot-db-1` on volume `pm-tool-pilot-db`, writes `hub-pilot-*.dump` to the pilot directory) with its service and 22:15 UTC timer; activation/verify scripts still assume review names; nothing installed |
| H9 | Hostname | review sets `AllowedHosts` and `Email__BaseUrl` to `pm.engcalchub.com`; one hostname routes to one origin | decide which stack `pm.engcalchub.com` serves during the pilot; the other gets another hostname in the `pm-tool` tunnel or stays loopback-only | Jay | UNPROVEN (undecided) |
| H10 | Capacity | each stack limits the database and the API to 1 GiB each; homedev has about 7.2 GiB RAM shared with other services (platform guide) | measure with both stacks running, or stop the review stack during the pilot | agent + Jay | UNPROVEN |
| H11 | Releases | [homedev runbook][homedev]: reviewed full SHA, CI pass, dump first, previous image kept | the same, announced in the support channel and run outside business hours | agent + Jay | UNPROVEN |

## 4. Pilot accounts

Local-password sign-in maps each login to an existing active `AppUser` and grants no roles. In this mode nothing
creates `AppUser` records for real people or grants the first Admin: records come only from Entra or development
sign-in ([Auth.cs][auth] line 155) or the synthetic seeds; role grants need an Admin ([Admin.cs][admincs] line 154);
and the review seed's three people hold no Admin role.

| # | Item | Owner | Status |
|---|---|---|---|
| U1 | A reviewed provisioning path for real users and the first Admin, written to the activity log. Store each person's Entra sign-in name (UPN) as their email and leave the Entra object ID empty, so a later Entra sign-in claims the same record ([Auth.cs][auth] line 150) | agent (code change after Jay approves) | OPEN: no mechanism exists; blocks the pilot |
| U2 | Participant list (name, UPN, office, system role) from the sponsor | Jay | UNPROVEN |
| U3 | One login per person with a random password: `scripts/bootstrap-review-credentials.py` for the first batch (it requires an empty verifier file), `scripts/add-review-credential.py` afterwards (12+ characters). At most 64 logins ([LocalPasswordStore.cs][store] line 34). Restart the API to load changes | Jay | UNPROVEN |
| U4 | Private delivery: one person per message through a company-approved channel, never group chats, tickets or shared mailboxes. Delete the handoff file after delivery; record the delivery date, not the password | Jay | UNPROVEN |
| U5 | Rotation on suspected exposure or request: remove the entry, add a new one, restart. There is no rotation tool. An existing session stays valid for up to 8 hours (absolute expiry, [Auth.cs][auth] lines 86–87) unless the person is deactivated | Jay (agent may script it) | UNPROVEN |
| U6 | Offboarding: (1) Admin → Users, clear Active; the next request with an existing cookie is refused ([Auth.cs][auth] line 152); (2) Reassign work ([admin guide][admin]); (3) remove the verifier and restart. Reactivating within 8 hours revives an old cookie, so rotate first. Leavers are not detected automatically (`Graph__DirectorySync: "false"`); the sponsor reports them | Jay + company IT | UNPROVEN |
| U7 | Sign-in allows 5 attempts a minute per client address ([Program.cs][program] line 70); people behind one office address share that limit. Confirm egress addresses or accept occasional 429 retries | company IT + agent | UNPROVEN |
| U8 | Walkthrough and user guide explain ID and password sign-in (the [user guide](../user-guide.md) says "organisation account") | agent | UNPROVEN |

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
| B1 | Pilot dump helper and timer installed; the first automatic run leaves a nonempty, owner-only archive | agent + Jay (sudo) | UNPROVEN. The review timer itself was not installed at the last observation ([gates], 2026-10-01) |
| B2 | Restore drill of a timer-produced pilot dump into an isolated temporary database; counts compared and time recorded | agent + Jay | UNPROVEN. Only a manual review dump was drilled (PASS, [gates]) |
| B3 | Off-host encrypted copy. The Mac whole-host restic job fails: last success 2026-09-20, SSH export exit 255 on 2026-09-28 ([gates]); per Jay, its ~50 GB export now exceeds the job's 2-hour limit. Proposal: a separate small job that copies only pilot dumps and the verifier file to an encrypted repository after each dump, independent of the whole-host export | Jay approves destination and schedule (company IT too, for company data); agent prepares | FAIL (whole-host); UNPROVEN (dedicated) |
| B4 | Retrieval: fetch a dump from the off-host copy, compare SHA-256, restore-drill it | agent + Jay | UNPROVEN for the pilot. Manual review retrieval PASS (snapshot `224a6c27`, [gates]) |
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
