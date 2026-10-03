# Threat model (STRIDE)

Prepared by engineering for the pre-pilot walkthrough (§21, packet 011 FR-011). The walkthrough itself — the team going
through this list, adding what it finds, and resolving or accepting each item — is to be held and recorded below before
the pilot starts.

## Scope

**Assets:** project information (internal-confidential), people data (name, email, job title, office, supervisor), the
activity history, notification and digest content, access tokens, and review-only password verifiers. The Hub stores no plaintext passwords or project files.

**Boundaries:** browser ↔ App Service (TLS 1.2+, Entra bearer token or review-only secure cookie) · App ↔ PostgreSQL (TLS, managed-identity token, no
password) · App ↔ Microsoft Graph (directory read, mail send) · App ↔ Key Vault (RBAC) · App → Application Insights ·
GitHub Actions → build artefacts.

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

| ID | Threat | Where | Mitigation | Status |
|---|---|---|---|---|
| T-01 | Spoofing: forged, expired or replayed tokens | API | Entra JWT validation (issuer, audience, lifetime, signature); HTTPS only with HSTS | Mitigated |
| T-02 | Spoofing: the development sign-in header used outside development | API | Accepted only in the Development environment with development sign-in enabled; never mapped in production (`AuthSetup.DevAuthAllowed`); production sets `Auth__Mode=Entra` | Mitigated |
| T-03 | Elevation: acting on items beyond one's role (insecure direct object reference) | API | Deny-by-default permission check on every request (`Access`, `Permissions`), permission sweep tests, admin routes behind one filter | Mitigated |
| T-04 | Disclosure: restricted projects leaking through lists, search, aggregates, calendars, boards, exports | API | One visibility filter behind every query; invisible items answer 404; tests cover search, calendar, workspace views, reports and exports | Mitigated |
| T-05 | Tampering: formula injection through exported CSV | Exports | Formula-like text is prefixed with an apostrophe (this packet); XLSX writes inline strings only | Fixed |
| T-06 | Cross-site scripting through names, comments or links | SPA | React escaping, no raw HTML rendering anywhere, links limited to http(s) and UNC paths (no `javascript:`), Content-Security-Policy | Mitigated |
| T-07 | Cross-site request forgery | API | The review cookie is HttpOnly, Secure and SameSite Strict; unsafe API requests in LocalPassword mode require an exact same-origin `Origin` header. Entra calls use bearer tokens. | Implemented locally; deployed proxy and host behavior unverified |
| T-08 | Repudiation: denying a change | Data | Append-only activity log (database triggers refuse UPDATE, DELETE and TRUNCATE), actor, time, before and after; exports and sign-ins logged | Mitigated |
| T-09 | Tampering with the log by the app's own database role, which owns the schema because migrations run at start | Data | Triggers stop ordinary changes; a role that owns the table could drop them | Open: before organisation-wide rollout, run migrations from the release pipeline as the database administrators' group and give the app role data access only |
| T-10 | Disclosure through logs and errors | API, telemetry | Production errors return a code and trace ID only; telemetry redacts query strings; tokens and comment text are never logged; people are logged by internal ID | Mitigated (request paths show item keys, which is accepted) |
| T-11 | Denial of service by a runaway client | API | 600 requests a minute per person; request bodies at most 1 MB; page size at most 200; export cap 50,000 rows, and report queries read at most one row more; date ranges (task hours 370 days) and Gantt scope capped | Mitigated |
| T-12 | Secrets in code, configuration or logs | All | No secrets in the repository; review verifier JSON is a Key Vault secret referenced by an app setting, and database access uses managed identity. Local verifier files must be owner-only. | Local check passed; Azure Key Vault resolution and key-ring protection at rest unverified |
| T-13 | Vulnerable or malicious dependencies | Build | Dependabot monthly; CI fails on critical NuGet or npm advisories; lockfiles committed | Mitigated |
| T-14 | Unscanned code weaknesses | Build | All .NET security analyser rules run in every build and a finding fails it (`Directory.Build.props`); the one finding (key-reservation SQL built by interpolation) was fixed | Mitigated; CodeQL can be added (needs GitHub Advanced Security on a private repository) if the organisation requires a dedicated scanner |
| T-15 | Session left open on a shared computer | SPA | Idle sign-out (`idle_timeout_hours`); review cookie expires after eight hours without sliding renewal and sign-out clears it. | Local browser check passed; deployed check open |
| T-23 | Review password guessing and identity mix-up | Review API | Individual PBKDF2 verifiers, one active `AppUser` per ID, unknown-user decoy derivation, five sign-in attempts a minute per client address (IPv6 by /64) and per login name, no role sync from local credentials | Local verifier/browser checks passed; hosting and operations walkthrough open |
| T-16 | Over-broad Graph permissions | Graph | Directory read and Mail.Send only; Mail.Send limited to the Hub's mailbox with an application access policy | Open: tenant administrator to confirm when enabling Graph |
| T-17 | Data loss | Data | 14-day point-in-time restore; restore runbook and quarterly drills (`docs/runbooks/restore.md`) | Procedure ready; first drill due before go-live |
| T-18 | Unknown weaknesses | All | Penetration test before organisation-wide rollout if the organisation requires one (§21) | Organisation decision |
| T-19 | Disclosure: internal notes in the file a PM sends to the client (Phase 2) | Client decision export | The export reads only subject, what must be decided, required-by, timing, impact, status and owner; party notes and comments are never read; the export is logged | Mitigated |
| T-20 | Disclosure: mail to people outside the organisation about their actions or decisions | Notifications | External parties have no notification path at all; only internal people are recipients | Mitigated |
| T-21 | Disclosure: comment text found through search by someone who may not see the project, or after deletion | Search (Phase 2) | Comment and description search uses the same visible-project filter; deleted comments are excluded in the query and the index | Mitigated |
| T-22 | Tampering: calendars or templates changed by the wrong people | Admin, templates | Holiday calendars are behind the Admin filter; templates need Admin or the Template editing flag; both are logged | Mitigated |

## Walkthrough record

| Date | Attendees | Items added | Items resolved or accepted | Sign-off |
|---|---|---|---|---|
| | | | | |
