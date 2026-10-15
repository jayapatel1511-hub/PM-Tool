# Microsoft and Google sign-in handoff

Implemented on `codex/social-sign-in` from release baseline `b83e9bd3`, in the isolated `social-sign-in/PM-Tool` worktree. Other active UI/worktrees were not modified. Prepared for a local commit and homedev review activation; nothing has been pushed or activated yet.

Jay chose unrestricted, immediate registration on 2026-10-14. Social mode provides Microsoft (personal/work/school) and Google sign-in without a Tuesday username/password or company-domain filter. New profiles are active with no elevated roles. Stable provider identities preserve account ownership; existing matching emails are refused until safely mapped. Preferences can connect the second provider to one profile. Local/Entra modes remain available to earlier environments and for rollback, but their sign-in endpoints are unavailable in Social mode.

## Verification

- PASS: 22/22 focused OAuth/account/session PostgreSQL tests (`SocialAuthTests`, `LocalSessionTests`, `LocalAccountsTests`). Actual ASP.NET handlers use synthetic provider token/userinfo responses; no real provider account was used.
- PASS: 15/15 existing `FoundationsTests`, including provisioning, roles, unauthenticated denial and refusal of development identity headers in production.
- PASS: final frontend build and lint (warnings remain).
- PASS: `npm run test:sign-in`: both buttons, password form absent, return route, errors, phone overflow, configured-provider visibility, POST-based account connection, connected-provider feedback, and desktop/account-method accessibility. No browser errors or detected WCAG AA violations. API responses are synthetic.
- PASS: additive identity migration/model consistency and `git diff --check`.
- PASS: 5 deployment-mode/probe regressions, including refusal of wrong authorities/callbacks/PKCE, secrets in redirect URLs and unexpected verification origins. Password-mode activation remains the default unless `--social-auth` is supplied.
- PASS: both provider registrations created with exact HTTPS callbacks; credentials saved owner-only outside Git with Jay's approval. Google is External/Testing with basic identity scopes only. Microsoft accepts personal and organisational accounts; its secret expires 2027-04-12.
- UNPROVEN: real Microsoft/Google consent and hosted callbacks. Hosted review was verified still on `77b1b25c` / LocalPassword before activation. Its old copied-cookie logout issue is fixed by this release baseline and covered by focused tests.

The full application suite, full coverage gate, hosted activation, and human acceptance were not run. Temporary local PostgreSQL/test VM are task-owned and removed after verification; the original Docker context is restored.

See [setup and existing-account mapping](../runbooks/social-sign-in.md). The homedev compose override is an explicit opt-in; existing stacks stay on their configured mode until an authorised activation. Immediate registration gives new active users visibility of projects marked Open under the existing permission model. Select the intended dataset before activation.
