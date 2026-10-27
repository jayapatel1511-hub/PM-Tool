# Tasks: User Feedback Round 1

Each slice is one PR. Slices in Parts A, B and C merge dark behind `flag.data_entry_safety`, `flag.backlog` and `flag.admin_review` and add the flag pair test (off: identical to before; on: present), covering AC-SAV-06, AC-BKL-06 and AC-ADR-06 for their area. S02 is an unflagged Tier B correction (051-D4). Every slice needs packet 039's flags slice (S09) first; Jay approved 051-D1 on 2026-10-25.

## Part A, data-entry safety

- [x] S01 Autosave hook and settings (S).
  - `useAutosaveField` from the task panel's commit; `autosave_delay_ms`, `draft_keep_days` settings; flag entry; every edit panel's `Field` adopts the hook.
  - Covers FR-SAV-01, FR-SAV-07, FR-SAV-08.
  - Tests: `web/tests/data-entry.cjs` (panel saves per field with one log entry each; task panel unchanged).
- [x] S02 Never-revert retry and conflict choice (S, Tier B, unflagged). After S01.
  - Typed value kept on any error; retry at 2, 4, 8 s; "Not saved · Retry"; conflict shows both values with "Keep mine" and "Take theirs".
  - Covers FR-SAV-02; AC-SAV-02, AC-SAV-03.
  - Tests: `data-entry.cjs` with a blocked network and a seeded conflict.
- [ ] S03 Drafts and leave prompt (M). After S01.
  - `useDraft`, `useUnsavedGuard`; adopted by every create dialog, register form, comment and rich-text editor; the Reload action of FR-LIVE-03 goes through the guard.
  - Covers FR-SAV-03, FR-SAV-04, FR-SAV-06; AC-SAV-01, AC-SAV-05.
  - Tests: `data-entry.cjs` (Escape then reopen restores; Discard clears; prompt on navigation without a draft; no draft for an empty form; pruned after `draft_keep_days`).
- [ ] S04 Sign-in expiry (S). After S03.
  - Pending field or draft stored before `onUnauthorized`; same route reopened after sign-in; one retry.
  - Covers FR-SAV-05; AC-SAV-04.
  - Tests: `data-entry.cjs` with an expired dev session.

## Part B, backlog

- [x] S05 Backlog rule, filter and counts (S).
  - `IsBacklog`; `backlog` filter and row field; dashboard, discipline summary and export columns; flag entry.
  - Covers FR-BKL-01, FR-BKL-05, FR-BKL-07, FR-BKL-08, FR-BKL-09; AC-BKL-01 (API), AC-BKL-05.
  - Tests: `tests/Hub.Tests/Domain/BacklogTests.cs` (rule over every status and date combination; excluded from Overdue, Due Soon, Slipped, Date Inconsistent, Workload weeks, readiness, health); `tests/Hub.Tests/Api/TaskQueriesTests.cs` (filter, counts, export column).
- [ ] S06 Backlog views and quick add (M). After S05.
  - Task List view, Board column with manual order, dashboard count link, quick add with visible defaults, scheduling by date cell, drag and bulk date.
  - Covers FR-BKL-02, FR-BKL-03, FR-BKL-04; AC-BKL-01 (UI), AC-BKL-02, AC-BKL-03.
  - Tests: `web/tests/backlog.cjs`.
- [x] S07 My Work No date (S). After S05.
  - `nodate` view and saved view.
  - Covers FR-BKL-06; AC-BKL-04.
  - Tests: `MyWorkTests.cs` (membership across projects, Restricted projects excluded as today); `backlog.cjs`.

## Part C, admin review

- [x] S08 Checks and record (M, Tier B: migration).
  - Nine checks with counts and list URLs; `AdminReview` table; record endpoint; activity entry; `admin_review_interval_days`, `admin_review_idle_days`; flag entry; Admin-only guard (403).
  - Covers FR-ADR-01, FR-ADR-02, FR-ADR-03, FR-ADR-05, FR-ADR-07 (API), FR-ADR-08.
  - Tests: `tests/Hub.Tests/Api/AdminReviewTests.cs` (each check's count equals its list; record is immutable; non-Admin 403; "no home unit" check absent with `flag.org_structure` off). AC-ADR-01, AC-ADR-02, AC-ADR-04.
- [ ] S09 Page and export (M). After S08.
  - Admin review page; record form; last-review comparison; CSV and XLSX export with no person rows.
  - Covers FR-ADR-04 (page), FR-ADR-06, FR-ADR-07 (navigation).
  - Tests: `web/tests/admin-review.cjs`; `ExportTests.cs` (header block, nine rows, reviewer only). AC-ADR-05.
- [ ] S10 Digest line and Portfolio strip (S). After S08.
  - Covers FR-ADR-04 (digest); AC-ADR-03.
  - Tests: `DigestTests.cs` (line present when overdue, absent after a review, Admins only).

## Cleanup

- [ ] S11 Flag cleanup (S). After Jay turns each flag on in pilot: remove the entry and its checks, keep the addition on (§43 FR-DLV-09).
