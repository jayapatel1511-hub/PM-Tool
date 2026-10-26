# Verification: User Feedback Round 1

Slice builds in progress; see the build notes below. Scope approved by Jay on 2026-10-25 (051-D1).

| AC ID | Area | Slice | Status |
|---|---|---|---|
| AC-SAV-01 | Draft restored after Escape | S03 | UNRUN |
| AC-SAV-02 | Typed value kept and retried offline | S02 | UNRUN |
| AC-SAV-03 | Conflict shows both values, Keep mine | S02 | UNRUN |
| AC-SAV-04 | Comment survives sign-in expiry | S04 | UNRUN |
| AC-SAV-05 | Reload notice protects input | S03 | UNRUN |
| AC-SAV-06 | Flag off changes nothing (retry stays) | S01 to S04 | UNRUN |
| AC-BKL-01 | Backlog count, list, column agree; indicators exclude | S05, S06 | UNRUN |
| AC-BKL-02 | Quick add defaults and one log entry | S06 | UNRUN |
| AC-BKL-03 | Date in and out of the Backlog | S06 | UNRUN |
| AC-BKL-04 | My Work No date across projects | S07 | UNRUN |
| AC-BKL-05 | Workload weeks unchanged | S05 | UNRUN |
| AC-BKL-06 | Flag off changes nothing | S05 to S07 | UNRUN |
| AC-ADR-01 | Counts equal their lists | S08 | PASS (local), API level; the page that shows the counts is S09 |
| AC-ADR-02 | Recorded review, then-and-now | S08, S09 | PARTIAL: S08 API level PASS (local); the page is S09 |
| AC-ADR-03 | Digest line when overdue | S10 | UNRUN |
| AC-ADR-04 | Non-Admin refused | S08 | PARTIAL: 403 PASS (local); hiding the page from navigation is S09 |
| AC-ADR-05 | Export rows, no person list | S09 | UNRUN |
| AC-ADR-06 | Flag off changes nothing | S08 to S10 | PARTIAL: S08 API level PASS (local); page S09, digest line and Portfolio strip S10 |

## Build 051-S08

Admin review checks and record behind `flag.admin_review` (declared in `Flags.cs`). `src/Hub.Api/Features/AdminReview.cs` holds the nine FR-ADR-01 checks. `GET /admin/review` returns each check's count, rule and list URL, the last review, the due date and an overdue flag; each count is the length of the list `GET /admin/review/checks/{key}` returns. The "no home unit" check appears only while `flag.org_structure` is on. `POST /admin/review` takes every count on the server at that moment, keeps an optional note per check and one for the review, and writes one activity entry. New `AdminReview` table (migration `20261026012843_AdminReview`, adds the table only, Tier B); `HubDb` refuses any edit or delete of a recorded review. Settings `admin_review_interval_days` and `admin_review_idle_days` (both 90, security group). Non-Admins get 403 whatever the flag; with the flag off an Admin gets 404 (FR-DLV-08). Check text is in `Text.cs`; `en.ts` gains the two setting labels, the AdminReview item type and the "Recorded" activity action. `openapi.json`, `api.d.ts` and `tests/Hub.Tests/Snapshots/shapes.json` are regenerated for the new endpoints only.

- Checks on the merged tree (clean merge, no conflicts; `openapi.json`, `api.d.ts` and `en.ts` merged with the 049-D7 pickup change): `dotnet build Hub.slnx` 0 errors; `dotnet test --filter AdminReviewTests|TaskPickupTests|ContractTests|ContractCoverageTests|DecisionContractTests|PlanningContractTests|FlagsApiTests|FlagsTests|ActivityTests|SchemaResponseShapeTests|PlatformViewKeysTests|Migration|Setting|TextTests` 157 passed, 0 failed; `npm run gen` leaves `api.d.ts` unchanged; EF `has-pending-model-changes` none; `npm run build`, `npm run lint` exit 0; `npm run test:keys` 7/7; `tools/ratchet.py` exit 0. The builder ran the full suite on the slice branch (1521 passed, 1 skipped, 0 failed), the shape check (811 response shapes, PASS) and a local browser check against a real API on a throwaway database (404 flag off; 8 checks and 201 on record flag on; both setting labels on /admin/settings; "Recorded · Admin review" in /admin/activity with no raw keys).
- AC-ADR-01: PASS (local), API level. `Counts_equal_their_lists_and_show_exactly_the_arranged_records`: two inactive users still on teams, one active user with no manager and one setting changed yesterday show 2, 1 and 1, and each list holds exactly those records. `Every_check_counts_what_its_list_shows` covers all checks. The page is S09.
- AC-ADR-02: PARTIAL. API level PASS (local): `A_recorded_review_keeps_its_counts_and_notes_and_is_never_changed` (one `AdminReview` row and one activity entry; GET returns it as the last review with the old counts beside the current ones; edit and delete refused by the API, 404, and by the database layer). The page is S09.
- AC-ADR-04: PARTIAL. `Only_a_System_Administrator_may_open_or_record_it`: Sam, the PM and Lena get 403 on GET, the list endpoint and POST. Hiding the page from navigation is S09.
- AC-ADR-06: PARTIAL. `With_the_flag_off_an_Admin_finds_no_review_and_others_are_refused_as_on_every_admin_route`: with the flag off an Admin gets 404 and the record endpoint stores nothing; the existing API suite passes unchanged. Page S09; digest line and Portfolio strip S10.
- AC-ADR-03 (S10) and AC-ADR-05 (S09) UNRUN. S08 supplies the `dueOn` and `overdue` values S10 will use.
- Assumptions: default 051-D8 (exactly the nine FR-ADR-01 checks; any other check key is refused with 400 and has no list, 404). The "no sign-in" check uses the creation date when a user has never signed in. A live project is Setup, Active or On Hold; the idle-projects check covers only Active and Setup, measuring activity as the latest activity-log entry for the project, or its creation date if there is none. "Settings changed since the last review" counts every setting or flag change except the seeded "Created" entries, and every change when no review has been recorded. "In-app role grants" are `UserRole` rows with Source Manual held by active users, listed with the grant date. A review is overdue when none has been recorded, or when the last review's local date plus `admin_review_interval_days` is before today. The `Location` header on POST points to `/api/v1/admin/review/{id}`, which has no GET route yet, as other create endpoints do.
- Left for Jay: nothing for this slice. The migration only adds a table (Tier B) and runs on the next deploy; when to turn `flag.admin_review` on in review and pilot is his call.
