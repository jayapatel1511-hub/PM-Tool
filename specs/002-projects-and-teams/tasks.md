# Tasks: Projects and Teams

## Phase 1: Foundational

- [x] T001 Permission matrix and project status machine in `src/Hub.Domain/Permissions.cs`, `Workflow.cs` (§8.5, §10.2, FR-017)
- [x] T002 Access resolution (project context, visibility filter, 404 for restricted) in `src/Hub.Api/Infrastructure/Access.cs` (FR-010, §8.7)
- [x] T003 Per-project key sequences in `src/Hub.Api/Infrastructure/Keys.cs` (FR-020, G-08)
- [x] T004 [P] Matrix tests in `tests/Hub.Tests/Domain/PermissionMatrixTests.cs` (SC-002)

## Phase 2: US1 Create project and team (P1)

- [x] T005 `POST /projects` with number format and case-insensitive uniqueness, creator as primary PM, disciplines, leads, members (FR-001, FR-002, FR-012, AC-PRJ-01/02, AC-TEAM-02)
- [x] T006 Setup checklist on the project read model (P-09)
- [x] T007 Create-project wizard (identity → team & disciplines) in `web/src/pages/projects/CreateProject.tsx`

## Phase 3: US2 Roles decide actions (P1)

- [x] T008 Header payload with the caller's permissions (hide/disable with reasons) (FR-018)
- [x] T009 Tests: Read Only cannot edit (AC-PERM-04); restricted project hidden from list/search and 404 (AC-PERM-05); DL refused project edit (§8.5.2)

## Phase 4: US3 Lifecycle (P1)

- [x] T010 `POST /projects/{id}/transition` with reasons, consequence counts, closeout choices, reopen, archive, Admin unarchive (FR-004–FR-008, AC-PRJ-03..06)
- [x] T011 Complete projects: PM-only edits with reason; archive suggestion after the window (P-05)
- [x] T012 Settings tab with status dialog and danger zone in `web/src/pages/projects/Settings.tsx`

## Phase 5: US4 Team and disciplines (P2)

- [x] T013 Members add/update/remove with guards; primary PM change (FR-011, FR-015, FR-016, TM-01, TM-03, TM-08, E-02)
- [x] T014 Disciplines add/lead/deactivate/remove guard (FR-012–FR-014, TM-02, TM-05, TM-07)
- [x] T015 Team tab in `web/src/pages/projects/Team.tsx`

## Phase 6: US5 Project list (P2)

- [x] T016 `GET /projects` with filters, sort, my projects default, include archived, stars (FR-021)
- [x] T017 Project list page and project header/tabs (FR-022)

## Phase 7: Polish

- [x] T018 Admin-only project number change (P-07); verification.md
- [x] T019 Date review after an On Hold project resumes: work whose dates fell during the hold, shifted in one action (E-05, Rec; added in the final audit, 2026-09-25)
