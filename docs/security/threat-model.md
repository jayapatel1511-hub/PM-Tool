# Threat model (STRIDE)

Prepared by engineering for the pre-pilot walkthrough (§21, packet 011 FR-011). The walkthrough itself — the team going
through this list, adding what it finds, and resolving or accepting each item — is to be held and recorded below before
the pilot starts.

## Scope

**Assets:** project information (internal-confidential), people data (name, email, job title, office, supervisor), the
activity history, notification and digest content, session cookies, individual password verifiers, database credentials and cookie-protection keys. The application does not store project files. Temporary operator credential handoffs contain plaintext passwords, remain owner-only outside Git, and must be removed after private delivery.

**Current boundaries:** browser ↔ Cloudflare edge ↔ named tunnel ↔ loopback container API · API ↔ PostgreSQL on the private container network, using a private database password · operator ↔ homedev over key-only SSH and interactive sudo · homedev dump/runtime/key material ↔ approved encrypted Mac restic repository · GitHub Actions ↔ committed source and synthetic test artefacts. Cloudflare terminates public TLS; the private tunnel-to-origin leg uses HTTP. Company acceptance of this boundary is pending.

Homedev is the review, pilot and production target under Jay’s 2026-10-01 decision. Local-password authentication uses ASP.NET Core Staging; the runtime currently refuses that mode in Production. Graph mail/directory sync are disabled and email is Log mode. App Service, managed identity, Key Vault, Application Insights and Entra are unused future options, not current controls or release prerequisites.

## Homedev review and pilot boundary

For Staging on homedev: browser ↔ Cloudflare edge (TLS) ↔ dedicated named tunnel ↔ loopback Docker API ↔ dedicated
PostgreSQL volume. The review and company pilot use separate volumes, runtime files, ports, subnets and backups.
The API runs as uid 1000. Cookie sign-in trusts forwarded protocol and Cloudflare client IP only from the configured
bridge gateway; the public hostname and unsafe-request Origin are constrained. Individual password verifiers and the
persistent cookie key ring stay outside Git. Rotation/removal of a verifier invalidates that person's existing sessions.
A configured local Admin bootstrap is an operator privilege: use it only in the private runtime file and remove it once
the first Admin has been created. Company data requires company approval for the home host and Cloudflare boundary.

Repository checks cover project auto-membership authority, project-scoped notification/outbox filtering, current source
and review discipline authority, stale appointed basis approvers, and private time/calendar activity exports. These are
local regression results; an operator/company walkthrough, deployment verification and any required penetration test
remain separate gates. The root dump helpers use directory/file descriptors and reject symlink paths before privileged
writes; local regression tests do not prove the installed timer or off-host recovery.

## Findings

Control descriptions below are repository evidence, not company acceptance. Corrected candidate `211bd88` merged
through PR #25 at `328e88a` with identical Git trees. Candidate and main CI passed 858/858 with coverage gates,
three mocked Chromium suites and 14 backup/pilot helper checks. Independent changed-path review reported no further
confirmed findings. Fresh seed-free and restored synthetic review image checks passed locally; see the
[completion checkpoint](../reviews/2026-10-03-completion-audit.md). Hosted review still selects `02ca7cd` at this
checkpoint. Final activation/browser checks, the walkthrough and company sign-off remain open. T-09 below remains
an organisation-wide rollout gate; container hardening does not give the application's database role least privilege.

| ID | Threat | Where | Mitigation | Status |
|---|---|---|---|---|
| T-01 | Spoofing: forged, expired or replayed tokens | API | Signed/encrypted eight-hour cookie with persistent protection keys; active-user and current-verifier-stamp checks; HTTP redirect and HSTS. A copied cookie remains usable after sign-out until expiry or verifier rotation/removal | Implemented; copied-cookie replay is a residual risk for company acceptance |
| T-02 | Spoofing: the development sign-in header used outside development | API | Accepted only in Development/Testing with Development auth enabled; never mapped in Staging/Production (`AuthSetup.DevAuthAllowed`); homedev uses Staging LocalPassword with no development-header route | Mitigated |
| T-03 | Elevation: acting on items beyond one's role (insecure direct object reference) | API | Deny-by-default permission check on every request (`Access`, `Permissions`), permission sweep tests, admin routes behind one filter | Mitigated |
| T-04 | Disclosure: restricted projects leaking through lists, search, aggregates, calendars, boards, exports | API | One visibility filter behind every query; invisible items answer 404; tests cover search, calendar, workspace views, reports and exports | Mitigated |
| T-05 | Tampering: formula injection through exported CSV | Exports | Formula-like text, even behind leading spaces, is prefixed with an apostrophe and any separator a spreadsheet may split on (comma, semicolon, tab) is quoted, by the same rules on the server and in client-built CSV; XLSX writes inline strings only | Fixed |
| T-06 | Cross-site scripting through names, comments or links | SPA | React escaping, no raw HTML rendering anywhere, links limited to http(s) and whole UNC paths (no `javascript:`), Content-Security-Policy. A UNC link may name any server: there is no host allow-list setting, so a link to an outside server could make Windows offer credentials when opened; outbound SMB should stay blocked at the network edge | Mitigated; UNC host restriction is a network control |
| T-07 | Cross-site request forgery | API | The individual-password cookie is HttpOnly, Secure and SameSite Strict; unsafe requests require an exact same-origin `Origin`. Forwarded protocol/client address is trusted only from the configured bridge gateway. | PASS for bounded review probes on `02ca7cd` and isolated candidate image; final candidate/company hosted acceptance UNPROVEN |
| T-08 | Repudiation: denying a change | Data | Append-only activity log (database triggers refuse UPDATE, DELETE and TRUNCATE), actor, time, before and after; exports and successful sign-ins logged. Local authentication saves its event before issuing a cookie; the browser callback does not duplicate it | Mitigated |
| T-09 | Tampering with the log by the app's own database role, which owns the schema because migrations run at start | Data | Triggers stop ordinary changes; a role that owns the table could drop them | Open: before organisation-wide rollout, run migrations from the release pipeline as the database administrators' group and give the app role data access only |
| T-10 | Disclosure through logs and errors | API, telemetry | Unexpected errors outside Development/Testing omit exception details and include a trace ID; application operation logs avoid passwords and comment text. There is no deployed Application Insights redaction control; review sanitized logs before sharing | Mitigated (request paths show item keys, which is accepted) |
| T-11 | Denial of service by a runaway client | API | 600 requests a minute per person; request bodies at most 1 MB; page size at most 200; export cap 50,000 rows, and report queries read at most one row more; date ranges (task hours 370 days) and Gantt scope capped | Mitigated |
| T-12 | Secrets in code, configuration or logs | All | Database password, individual verifiers and independent protection keys are in owner-only private runtime files outside Git. Operator handoffs are temporary; backups containing access material require approved encrypted storage | Repository/private-file checks and approved review encrypted retrieval passed; company host encryption, access and recovery acceptance UNPROVEN |
| T-13 | Vulnerable or malicious dependencies | Build | Dependabot monthly; CI fails on critical NuGet or npm advisories; lockfiles committed | Mitigated |
| T-14 | Unscanned code weaknesses | Build | All .NET security analyser rules run in every build and a finding fails it (`Directory.Build.props`); the one finding (key-reservation SQL built by interpolation) was fixed | Mitigated; CodeQL can be added (needs GitHub Advanced Security on a private repository) if the organisation requires a dedicated scanner |
| T-15 | Session left open on a shared computer | SPA | Idle sign-out (`idle_timeout_hours`); review cookie expires after eight hours without sliding renewal and sign-out clears it. | Local browser check passed; deployed check open |
| T-23 | Review password guessing and identity mix-up | Review API | Individual PBKDF2 verifiers, one active `AppUser` per ID, unknown-user decoy derivation, five sign-in attempts a minute per client address (IPv6 by /64) and per login name, no role sync from local credentials | Local verifier/browser checks passed; hosting and operations walkthrough open |
| T-16 | Over-broad Graph permissions | Graph | Directory read and Mail.Send only; Mail.Send limited to the Hub's mailbox with an application access policy | Open: tenant administrator to confirm when enabling Graph |
| T-17 | Data loss | Data | Daily logical dumps with a proposed 14-day retention target; helpers currently preserve archives without pruning. Approved encrypted off-host review copy and isolated restore drills. No homedev point-in-time recovery is implemented; see `docs/runbooks/restore.md` | Manual review retrieval/restore PASS; first automatic review trigger at 2026-10-03 22:00 UTC UNPROVEN. Company schedule, recovery targets and host-loss recovery UNPROVEN |
| T-18 | Unknown weaknesses | All | Penetration test before organisation-wide rollout if the organisation requires one (§21) | Organisation decision |
| T-19 | Disclosure: internal notes in the file a PM sends to the client (Phase 2) | Client decision export | The export reads only subject, what must be decided, required-by, timing, impact, status and owner; party notes and comments are never read; the export is logged | Mitigated |
| T-20 | Disclosure: mail to people outside the organisation about their actions or decisions | Notifications | External parties have no notification path at all; only internal people are recipients | Mitigated |
| T-21 | Disclosure: comment text found through search by someone who may not see the project, or after deletion | Search (Phase 2) | Comment and description search uses the same visible-project filter; deleted comments are excluded in the query and the index | Mitigated |
| T-22 | Tampering: calendars or templates changed by the wrong people | Admin, templates | Holiday calendars are behind the Admin filter; templates need Admin or the Template editing flag; both are logged | Mitigated |

## Walkthrough record

| Date | Attendees | Items added | Items resolved or accepted | Sign-off |
|---|---|---|---|---|
| | | | | |
