# Plan: Supervisor Capacity Signals

Implement §42 with defaults D1–D7 under Jay's 2026-10-12 request to build the remaining scope and prepare deployment. Base: `68c56f28`, the integrated local Tuesday candidate. Keep other worktrees untouched. No push, deployment or operational data changes are authorized.

## Scope and checks

| Contract | Maintained sources and callers | Verification |
|---|---|---|
| FR-SUP-01–05 | `web/src/pages/Staff.tsx`; the existing people-capacity batch hook and figure in `components/hub/people.tsx`; `i18n/en.ts` | Browser checks for matching hours, 50-row batches, partial/out-of-scope people, denied access, single signals and phone cards; unchanged Planning and privacy API tests |
| FR-DIG-01–04 | `UserSetting`, its EF mapping and additive migration; digest preferences endpoint/UI; `DigestJob`; operations watchdog; the shared `PersonCapacity.Load` | Real PostgreSQL tests for full/reduced leave, overrides, holidays, nonworking schedules, default/opt-out, return window, immediate notices and false late alerts |
| Deployment readiness | Existing audited fields and activity hooks; Team, project discipline, Workspace and DocumentLink version handling and every UI caller; Following paging and personal mark-read; atomic preference creation; existing publish/review-image paths | Focused audit, privacy, version-conflict and startup-race regressions; required CI gates; additive migration/default checks; container jobs, authentication and signed-in browser smoke; packet 036 local API performance; record external gates separately |

## Constitution check

PASS: deterministic modular monolith; existing endpoints, permissions, calculations and components; no dependency, service or AI feature. The screen-only capacity column leaves exports and visible planner hours unchanged. Jay's approved hidden Confirmed allocation rule changes only partial-view signalling and suppression of Under-planned when the lookahead is incomplete. Three additive migrations add the default-true preference and four concurrency columns (ProjectMember, ProjectDiscipline, Workspace and DocumentLink); existing callers now send expected versions. An image rollback retains the upgraded schema. Person-capacity reads project only the leave fields needed for hours, excluding HR identifiers. Shared digest hold logic keeps monitoring consistent. Atomic creation of first-use preferences preserves concurrent job markers and saves. Human sessions, company approvals, actual mail delivery, hosted activation and off-host recovery remain external acceptance gates.

Existing local-password hosting is retained under the already documented hosting decision; this packet does not change authentication or the older constitution text.
