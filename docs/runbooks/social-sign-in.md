# Microsoft and Google sign-in

Jay approved unrestricted Microsoft and Google sign-in on 2026-10-14: any account can register immediately, without a company domain filter or approval queue. This replaces the company-only Entra sign-in requirement for `Auth:Mode=Social`. Existing Entra, development and review-password modes remain available for earlier environments and rollback; Social mode does not expose their login endpoints.

New users get an active Tuesday profile with no elevated system roles. Existing project visibility and permissions still apply: an active user can view projects marked Open. Review the target database before enabling public registration; do not expose a company database accidentally. Provider claims never grant Tuesday roles.

## Provider registrations

Register a confidential **Web** application, not a SPA:

| Provider | Registration | Callback |
|---|---|---|
| Microsoft | Entra app registration accepting accounts in any organisational directory and personal Microsoft accounts. The platform handler uses the common endpoint and delegated Microsoft Graph `User.Read` to obtain the stable account ID and basic profile. | `https://pm.engcalchub.com/signin-microsoft` |
| Google | Google Auth Platform OAuth client of type Web application, External audience, basic profile and email scopes only. | `https://pm.engcalchub.com/signin-google` |

Use the exact localhost HTTPS origin and the same callback paths for local provider testing. The designated hostname is a project setting, not evidence of a live deployment. Setup references: [Microsoft](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/social/microsoft-logins?view=aspnetcore-10.0), [Google](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/social/google-logins?view=aspnetcore-10.0).

The Tuesday registrations were created on 2026-10-14 in the personal Microsoft directory and Google project `tuesday-sign-in`. Private registration metadata and credentials live in `.runtime/`, outside Git. Microsoft's initial secret expires on **2027-04-12**; replace it before then. Google remains External/Testing: [Google's basic-identity exception](https://developers.google.com/identity/protocols/oauth2/production-readiness/overview) allows any user when only basic identity scopes are requested, with a testing notice. Brand verification and organisation-admin approval are separate; corporate account policies may still block consent.

## Server configuration

Set these through the deployment's private secret/environment store. Never put real values in tracked JSON, chat, logs or frontend code:

```text
Auth__Mode=Social
Auth__Social__KeyDirectory=<persistent private directory for data-protection keys>
Auth__Social__Microsoft__ClientId=<Microsoft application ID>
Auth__Social__Microsoft__ClientSecret=<Microsoft secret value>
Auth__Social__Google__ClientId=<Google OAuth client ID>
Auth__Social__Google__ClientSecret=<Google secret>
```

At least one provider must be configured; a partly configured provider fails startup. Configure both for the requested two-button login. Public `/api/v1/config` exposes provider names only, never credentials. The API must receive the correct public HTTPS scheme and host through a trusted proxy, with an explicit host allowlist. Keep `/signin-google` and `/signin-microsoft` routed to the API. For local OAuth use the built SPA served by the API's HTTPS origin, rather than the HTTP Vite development proxy.

The existing homedev compose files explicitly select `LocalPassword`, so server JSON or an env file alone will not switch them. Put the four provider settings in private `.runtime/social-auth.env` (mode 0600) and add `-f hosting/social-auth.override.yml` after the existing compose file when an activation is authorised. This opt-in override selects Social and reuses the persistent `.runtime/keys` directory; it does not alter the database, hostname, port, or seed settings. For the pilot, retain its migration/grant procedure and separate data. Azure's existing Bicep auth settings also remain unchanged; Social activation there needs the corresponding explicit app-setting update.

For the public synthetic review, run `bash scripts/activate-homedev-review.sh <full-commit-sha> --social-auth` from the prepared `releases/<full-commit-sha>` directory on homedev. Jay enters sudo in his own terminal. The existing activation builds, backs up the database, preserves its volume, then checks both provider redirects before updating `current`. Repeat `python3 scripts/verify-homedev-review.py https://pm.engcalchub.com --social` through the public tunnel. The social verifier does not read password credentials; it checks HTTPS authorities, exact callbacks, protected state, PKCE and secure correlation cookies without logging redirect URLs or secrets.

The schema migration adds `hub.external_identity` without changing user IDs, roles, assignments or history. Persist the existing PostgreSQL-backed session store and data-protection keys across restarts. Sessions use a Secure, HttpOnly, SameSite=Lax host cookie, an eight-hour absolute expiry, same-origin checks for writes, and server-side logout revocation. Provider redirects use protected state, a browser correlation cookie and PKCE. Provider access/refresh tokens are not stored in Tuesday sessions.

## Existing users and a second provider

Identity is keyed by `(provider, subject)`, not email. A new provider account with an existing profile's email is refused instead of inheriting that profile's permissions. Signed-in social users can connect their other provider in **Preferences → Sign-in methods**; this begins an authenticated same-origin POST and binds the target profile into protected OAuth state. One provider account cannot be moved to another profile.

For a pre-existing local/Entra profile, an operator must verify the provider's stable subject and insert its `external_identity` mapping to the existing user ID before switching that person to Social mode. Obtain the subject from Microsoft's authenticated Graph `/me` `id`, or Google's authenticated userinfo `id`. Never infer it from an email address. Use a database transaction and check both uniqueness constraints. Existing IDs and roles remain intact. Grant the initial administrator through an explicitly verified existing profile/mapping; the first registrant is never made an administrator automatically.

## Acceptance

Focused tests exercise the actual framework OAuth handlers with synthetic provider token/userinfo responses and real PostgreSQL. They cover both providers, PKCE/state/correlation, account creation, identity conflicts, second-provider linking, inactive accounts, and revocation. They do not prove provider-console configuration or real-account consent.

After staged activation, complete a real Microsoft and Google sign-in at the target HTTPS origin, confirm callback URLs and consent, connect both to one profile, confirm route return and logout, and verify that newly registered users see only the intended dataset before treating Social mode as ready. No deployment is implied by local verification.
