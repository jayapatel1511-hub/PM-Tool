# Implementation Plan: Project Templates

**Branch**: `012-project-templates` | **Date**: 2026-09-25 | **Spec**: [spec.md](spec.md)

## Summary

Template families with one Draft and numbered published versions; a whole-structure editor for Drafts; publish, retire,
new-draft and discard; a preview of what a template would create; creating a project from a template in the project
wizard (disciplines and leads, dates from offsets or typed, a summary with undated warnings); adding one discipline's
pack to an existing project with milestones matched by name; and the Appendix A reference template.

## Technical Context

As packet 001. Tables from the initial schema (`project_template` with `family_id`, and its discipline, milestone,
deliverable, task and dependency tables; `template_*_id` origin columns on project items; `created_from_template_id`,
`template_version` on projects); no migration. `CreateBody.TemplateId/Template` existed for this. New: `Hub.Domain`
`TemplatePlanner` (pure, with worked examples), `Features/Templates.cs` (endpoints, `TemplateHook : IProjectCreateHook`,
discipline packs), `Data/ReferenceTemplate.cs` (seeded in development and tests), web `pages/Templates.tsx`,
`pages/projects/FromTemplate.tsx` (wizard source, summary, pack dialog, settings section).

Endpoints: `GET/POST /templates`, `GET /templates/{id}`, `PUT /templates/{id}/structure`, `POST /templates/{id}/draft`,
`/publish`, `/retire`, `/preview`, `DELETE /templates/{id}` (drafts), `GET/POST /projects/{id}/template-packs`.

## Constitution Check

| Principle | Check | Result |
|---|---|---|
| I | Dates are arithmetic on offsets; no suggestion or learning | Pass |
| II | The wizard's summary, the preview and the created project come from the same planner | Pass |
| III | Every created task has one assignee or none by the template's role; deliverables belong to their discipline's lead | Pass |
| IV | Items are created Not Started, the project in Setup; template status is its own Draft/Published/Retired lifecycle | Pass |
| V | One "CreatedFromTemplate" entry plus each item's creation; publishing, retiring, drafts, saves and packs logged | Pass |
| VI | Editing needs Admin or the Template Editor flag (`ManageTemplates`); non-editors see published versions only; packs need `ManageTeam` | Pass |
| VII | No new infrastructure | Pass |

## Design notes

- A Draft is saved as one structure (rows refer to each other by `ref`), so references, discipline membership and the
  absence of dependency loops are checked together; published rows never change, so projects' origin ids stay valid.
- Publishing gives the next version and retires the previous published one; retiring the current one hides the family.
- A retired or replaced template is refused at creation with an explanation, and the whole creation rolls back.
- A task's dates: its deliverable's due date plus its offset, or the project start plus its offset when it has no
  deliverable; milestones left blank leave their deliverables and tasks undated.
- A pack uses the project's own milestone dates for the mapped template milestones; unmapped ones leave items undated.
- Assignment notices during Setup are held and batched at activation by the existing rules (§17.5).

## Complexity Tracking

| Deviation | Why | Simpler alternative rejected because |
|---|---|---|
| Template child rows carry no row version | A Draft's children are replaced as one structure under the header's version | Per-row versions would guard edits that never happen one row at a time |
