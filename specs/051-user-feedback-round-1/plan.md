# Plan: User Feedback Round 1

Implements §55 (FR-SAV-01 to FR-SAV-08, FR-BKL-01 to FR-BKL-09, FR-ADR-01 to FR-ADR-08) with defaults D2 to D8. Each slice in [tasks.md](tasks.md) is one PR that merges dark behind its part's flag, except S02, an unflagged Tier B correction (D4).

## Approach

Reuse first. New storage is one table (`AdminReview`) and three organisation settings. No new library.

### Part A, data-entry safety

- **One field hook.** The task panel's per-field commit (`TaskPanel.tsx`, `onBlur`/`commit`) becomes a shared `useAutosaveField` in `web/src/components/hub/fields.tsx`, with the debounce (`autosave_delay_ms`), the retry schedule (2, 4, 8 s) and the "Not saved · Retry" state. Every edit panel's `Field` adopts it; the task panel's own behaviour is unchanged.
- **Never revert.** The hook keeps the typed value in local state on any error; the conflict branch reuses the existing `concurrency_conflict` text (`fields.tsx:35`) and adds "Keep mine" (resend with the fresh version from the error body) and "Take theirs".
- **Drafts.** One `useDraft(key)` hook over `localStorage` with the same try/catch pattern the rail and table columns use (`Shell.tsx:26`, `table.tsx:17`); key `draft.<form>.<context>` plus the user id; stamp and prune at `draft_keep_days`. Adopted by `new-item.tsx`'s dialogs, `CreateProject.tsx`, `CreateTask` (`Tasks.tsx`), the register forms (`CoordinationForms.tsx`, `ReadinessForms.tsx`, `Registers.tsx`, `Decisions.tsx`, `Milestones.tsx`, `Deliverables.tsx`, `Meetings.tsx`), `comments.tsx` and `richtext.tsx`.
- **Leave prompt.** One `useUnsavedGuard(dirty)` hook: `beforeunload` for tab close and reload, the router's blocker for navigation, and the dialog's `onOpenChange` for close. When a draft exists the guard is silent (FR-SAV-04).
- **Sign-in expiry.** `api.ts` already routes 401 to `onUnauthorized`; the hook stores the pending field or draft before that callback runs and `auth.tsx` reopens the same route after sign-in. One retry after `ready` flips back.
- **Reload notice.** `main.tsx`'s Reload action calls the guard before `location.reload()`.
- **Server.** No change beyond two settings (`autosave_delay_ms`, `draft_keep_days`) in `src/Hub.Domain/Settings.cs` `Defs`, and the flag entry in `Flags.cs`.

### Part B, backlog

- **Rule in one place.** `TaskRules.IsBacklog(task)` in `src/Hub.Domain` (open, `DueDate == null`, `StartDate == null`). `TaskQueries.cs` adds `backlog=true` as a filter and a `backlog` field on task rows; `Evaluation` and `Workload` already skip undated tasks for date indicators, so FR-BKL-05 is a test, not a change.
- **Views.** Task List view `backlog` (`Tasks.tsx`), Board first column (`Board.tsx`, reusing the manual order of §36.3), dashboard count (`Dashboard.cs`, `Dashboard.tsx`) linking to `?view=backlog`.
- **Quick add.** One input at the top of the Backlog view calling the existing task create with defaults from `useMe` (home discipline) and the project's disciplines; no new endpoint.
- **Scheduling.** Setting a date on the row or dragging a card out of the column uses the existing PATCH and the existing date-confirmation dialog.
- **My Work.** `MyWork.cs` adds the `nodate` view beside `today`, `upcoming`, `overdue`; `MyWork.tsx` adds the tab and a saved-view definition (`views.tsx`).
- **Discipline summary and export.** `Team.cs` adds `backlog` to the per-discipline counts; `ListExports.cs` adds the `backlog` column.

### Part C, admin review

- **Checks.** One static list of nine checks in `src/Hub.Api/Features/AdminReview.cs` (new), each a `(key, rule text, IQueryable count, list URL)`; the queries reuse `Admin.cs` user queries, `Projects.cs` live-project queries and the `Audit` table for settings changes. `GET /admin/review` returns counts and the last review; `POST /admin/review` records one.
- **Record.** `AdminReview(id, reviewed_at, reviewer_id, note, checks JSON [{key, count, note}])` in `Entities.cs`, one additive migration, one `ActivityLog` entry.
- **Due and digest.** `admin_review_interval_days` and `admin_review_idle_days` in `Settings.cs`; `Digest.cs` adds one line for Admins when `last.reviewed_at + interval < today`.
- **UI.** `web/src/pages/admin/AdminReview.tsx` (new): a table of checks with counts, rule text and links; a Record review form; last-review comparison; Export buttons through `export.tsx`.
- **Export.** `Export.cs` CSV and XLSX from the stored record; no person rows.
- **Portfolio strip.** Last review date and counts on `Portfolio.tsx` for Executives, behind the flag.

## File boundaries

| Slice | Files |
|---|---|
| S01 autosave hook and settings | `web/src/components/hub/fields.tsx`, `web/src/pages/projects/TaskPanel.tsx` (adopt, no behaviour change), `src/Hub.Domain/Settings.cs`, `src/Hub.Domain/Flags.cs`, `web/src/i18n/en.ts` |
| S02 never-revert retry and conflict choice (Tier B, unflagged) | `web/src/components/hub/fields.tsx`, `web/src/lib/api.ts`, `web/tests/data-entry.cjs` (new) |
| S03 drafts and leave prompt | `web/src/components/hub/drafts.ts` (new), `new-item.tsx`, `comments.tsx`, `richtext.tsx`, the create and register forms under `web/src/pages/projects/`, `web/src/app/Shell.tsx`, `web/src/main.tsx` |
| S04 sign-in expiry | `web/src/lib/api.ts`, `web/src/lib/auth.tsx`, `web/src/components/hub/drafts.ts` |
| S05 backlog rule, filter, counts | `src/Hub.Domain/TaskRules.cs` (new or existing rules file), `src/Hub.Api/Features/TaskQueries.cs`, `Dashboard.cs`, `Team.cs`, `ListExports.cs`, `tests/Hub.Tests/Domain/BacklogTests.cs` (new) |
| S06 backlog views and quick add | `web/src/pages/projects/Tasks.tsx`, `Board.tsx`, `Dashboard.tsx`, `web/src/components/hub/views.tsx`, `web/tests/backlog.cjs` (new) |
| S07 My Work No date | `src/Hub.Api/Features/MyWork.cs`, `web/src/pages/MyWork.tsx`, `views.tsx` |
| S08 admin review checks and record | `src/Hub.Api/Features/AdminReview.cs` (new), `src/Hub.Api/Data/Entities.cs`, `HubDb.cs`, one additive migration, `Settings.cs`, `tests/Hub.Tests/Api/AdminReviewTests.cs` (new) |
| S09 admin review page and export | `web/src/pages/admin/AdminReview.tsx` (new), `web/src/pages/admin/Admin.tsx`, `src/Hub.Api/Infrastructure/Export.cs`, `web/tests/admin-review.cjs` (new) |
| S10 digest line and Portfolio strip | `src/Hub.Api/Infrastructure/Digest.cs`, `web/src/pages/Portfolio.tsx` |
| S11 flag cleanup | removes the three flag entries and their checks once Jay has turned them on in pilot |

S03 reach (`web/src/hooks/drafts.ts`, `useSafeForm`): every create dialog and long text editor keeps a keyed draft (FR-SAV-03), including the Readiness raise-constraint, create-assessment and propose-commitment forms, the project status note editor, the project and time-away CSV imports, the manual time-away dialog and the New template dialog. An edit form passes no key: it keeps no draft, but typed changes still ask "Keep editing or discard?" (FR-SAV-04). Deliberately not adopted, because each holds a short reason or a pick rather than typed work, or saves by field (S01 and S02): confirm and reason-only action dialogs (`ConfirmDialog`, task start, readiness prerequisite, constraint move, applicability and exception forms), the add-from-template and bulk-week pickers, the planner entry panel, the admin review note and the organisation structure forms. Any of these joins by passing `safe` or calling `useSafeForm`.

## Risks

- **Draft keys and shared devices.** Drafts are keyed by user id, so a second person on the same browser profile does not see them, but a signed-out shared kiosk keeps them for `draft_keep_days`. D2 accepts this; the setting can be lowered.
- **Autosave on every panel widens the log.** FR-SAV-07 keeps one entry per field change, the same as a button save, so the log grows only where people now save more often.
- **Backlog count and Workload disagree in users' minds.** FR-BKL-05 states the rule on the list, and AC-BKL-05 pins it.
