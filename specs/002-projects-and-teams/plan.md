# Implementation Plan: Projects and Teams

**Branch**: `002-projects-and-teams` | **Date**: 2026-09-24 | **Spec**: [spec.md](spec.md)

## Summary

Projects with the §12.1 fields and the §10.2 lifecycle (closeout, archive, unarchive, reopen), the
permission matrix as pure functions (`Hub.Domain/Permissions.cs`) enforced on every endpoint, project
disciplines and leads, the team with project roles, per-project item keys, stars, the project list and
the project header. Stack and decisions: [docs/decisions.md](../../docs/decisions.md) (Q4, Q5, Q13).

## Technical Context

Same as packet 001. New tables were created in the initial schema (`project`, `project_link`,
`project_discipline`, `project_member`, `project_star`); no new infrastructure.

## Constitution Check

| Principle | Check | Result |
|---|---|---|
| I | Only project identity, lifecycle, team | Pass |
| II | No computed values here; health/counts come from packet 005 state tables | Pass |
| III | One primary PM (`project_manager_id`), always holding the PM role (P-08) | Pass |
| IV | Canonical statuses and role codes from `Vocabulary.cs` | Pass |
| V | Every create, field change, status change, membership change logged by `SaveChanges`; reasons stored (P-03, G-09) | Pass |
| VI | `Permissions.*` evaluated server-side for every command; Restricted projects 404 to non-members (AC-PERM-05); matrix rows become tests | Pass |
| VII | No new services | Pass |

## Design notes

- Item keys come from `UPDATE project SET next_x_seq = next_x_seq + 1 … RETURNING` inside the saving
  transaction, so keys are gap-free and never reused (G-08, §24.5). Keys store the project number at
  creation (§9.5).
- Uniqueness of `project_number` is a `citext` unique index; the create endpoint checks first so the
  error can link to the existing project (AC-PRJ-01).
- Team changes go through `TeamService`: adding a member creates the follow (ASG-01, used by packet 006),
  sends "Added to project"/"You are Discipline Lead" (AC-TEAM-02) and the supervisor notice (ASG-11).
- Completing a project runs the closeout choices (cancel or leave per group) in the same save; archiving
  offers the project export (packet 009). Complete projects accept PM edits only, with a reason (P-05).
- Changing the primary PM grants PM to the new person and turns the old PM into a Team Member (E-02).
- Setup checklist is advisory and computed on read (P-09).

## Project Structure

```text
src/Hub.Api/Features/Projects.cs    projects, lifecycle, links, stars, list, header
src/Hub.Api/Features/Team.cs        members, disciplines, leads, TeamService
src/Hub.Api/Infrastructure/Keys.cs  per-project sequences
tests/Hub.Tests/Domain/PermissionMatrixTests.cs, tests/Hub.Tests/Api/ProjectsTests.cs
web/src/pages/projects/*            list, create wizard, workspace frame, header, team, settings
```

## Checks

- Permission matrix rows (§8.5.1, §8.5.2) as pure tests; API tests for AC-PRJ-01..06, AC-PERM-04/05,
  AC-TEAM-02/04, TM-01/02/05, P-02/P-03, P-07.
- Browser: create a project with disciplines and members; activate; hold; complete with closeout; archive.

## Complexity Tracking

None.
