# Verification: Projects and Teams

Date: 2026-09-24.

## Automated checks

| Check | Command | Result |
|---|---|---|
| Build | `dotnet build Hub.slnx` | Pass |
| Tests | `dotnet test tests/Hub.Tests` | Pass (64/64 at the end of this packet) |
| SPA type check | `npx tsc -b` in `web/` | Pass |

`PermissionMatrixTests` (pure, `Hub.Domain`): every §8.5.1 row for the six system roles; every §8.5.2 row for PM,
Discipline Lead, Team Member, Reviewer and Viewer, including ownership extensions (§8.6), Read Only (AC-PERM-04),
restricted visibility (AC-PERM-05), Archived/Cancelled read-only (P-06) and Complete PM-only (P-05).

`ProjectsTests` (API): duplicate numbers in any case refused with a link (AC-PRJ-01); creator is primary PM and member,
creation logged, leads notified and followed (AC-PRJ-02, AC-TEAM-02, ASG-01); non-PM create refused (AC-AUTH-03);
lifecycle with reasons, illegal transitions, closeout, Complete PM-only edits with reason, archive read-only, default
list excludes archived, Admin unarchive, reasons in the log (AC-PRJ-03..06, P-02, P-03, P-05); Read Only and DL edit
refusal naming the PM role (AC-PERM-04, §8.5.2); restricted project hidden from list, number lookup and direct links
(AC-PERM-05); primary PM cannot be removed, duplicate membership merges roles, empty discipline removable (AC-TEAM-04,
TM-01, TM-05); reviewer-only members follow at My items only (ASG-01); supervisor staffing limited to direct reports
as Team Members with PM notified (AC-ASG-09); number change Admin-only (P-07); primary PM change keeps the old PM as
Team Member (E-02).

## Manual checks (browser pane, 738 px wide, dev sign-in as Priya)

| Check | Result |
|---|---|
| Two-step create dialog; discipline lead picker searches active people | Pass |
| After create: header with number, name, client, PM, Setup and Grey pills; Setup checklist banner | Pass |
| Team tab: disciplines with lead, missing lead flagged; members with role checkboxes and primary discipline | Pass |

## Not run / deferred

- Health, counts and next-milestone columns are empty until packet 005 evaluates projects (cross-packet rule).
- The On Hold "items whose dates passed" banner with bulk shift (E-05) was not picked up by packet 004; it was found
  and built in the final audit (2026-09-25): `GET /projects/{id}/date-review` reads the hold window from the logged
  status changes and lists open tasks and deliverables due within it; the banner shifts them with the existing bulk
  date shifts. Test: `ProjectsTests.A_resumed_project_lists_the_work_whose_dates_passed_while_on_hold` (held 2026-09-14
  to 10-05: one task and one deliverable listed, shifted by 21 days, then none; nothing while on hold or 30 days after).
  Browser: 2026-0417 held and resumed, "Back from hold: 1 task was due between …", shifted by 2 days, banner cleared.
