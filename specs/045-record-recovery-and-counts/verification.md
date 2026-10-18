# Verification: Record Recovery and Counts

Slice builds in progress; see the build notes below.

| AC | Delivered by | Status |
|---|---|---|
| AC-RCV-01 | S01, S02 | PARTIAL: S01 API level PASS (local); interface is S02 |
| AC-RCV-02 | S01 | PASS (local) |
| AC-RCV-03 | S01, S02 | PARTIAL: S01 API level PASS (local); interface is S02 |
| AC-RCV-04 | S01, S02 | PARTIAL: S01 API level PASS (local); interface is S02 |
| AC-RCV-05 | S01 | PASS (local) |
| AC-RCV-06 | S01, S02, S12 | PARTIAL: server side PASS (local); dialog and toast are S02, flag removal S12 |
| AC-RCV-07 | S01 | PASS (local) |
| AC-RCV-08 | S03 | PARTIAL: API level PASS (local) in `CountLinkTests`; browser counts suite not run |
| AC-RCV-09 | S03 | PARTIAL: API level PASS (local); `counts.cjs` Milestones check added, not run |
| AC-RCV-10 | S04 | UNRUN |
| AC-RCV-11 | S05 | UNRUN |
| AC-RCV-12 | S05 | UNRUN |
| AC-RCV-13 | S03, S04 | UNRUN: S03 API tests show new values equal the existing fields; counts suite against packet 036 fixtures not run |
| AC-RCV-14 | S06, S07 | PARTIAL: S06 domain level PASS (local); office calendar wiring is S07 |
| AC-RCV-15 | S06 | PASS (local), domain level |
| AC-RCV-16 | S06 | PASS (local), domain level |
| AC-RCV-17 | S07, S08, S12 | UNRUN |
| AC-RCV-18 | S09, S10, S12 | PARTIAL: S09 API level PASS (local); interface is S10, flag removal S12 |
| AC-RCV-19 | S11, S12 | UNRUN |
| AC-RCV-20 | S09, S11 | PARTIAL: S09 API level PASS (local); export columns are S11 |
| AC-RCV-21 | S09 | PASS (local), API level |

## Build 045-S01

Server side of Show deleted and Restore behind `flag.show_deleted` (declared in `Flags.cs` beside 041's `figure_names`): `GET /projects/{id}/{tasks|deliverables|milestones}/deleted` and `/deleted/export` (CSV, XLSX through `ExportFile.Send`) in `Features/Recovery.cs`; `POST /deliverables/{id}/restore` and `POST /milestones/{id}/restore`; the shared `RecoveryEndpoints.Guard` (409 on a stale `If-Match` when sent, 422 `parent_deleted` naming the parent key); `permissions.restore` on the project only while the flag is on. The existing task restore stays live and runs the guard only while the flag is on.

- Checks on the merged tree: `dotnet build Hub.slnx` 0 errors; `dotnet test --filter RecoveryApiTests|FlagsTests|FlagsApiTests|TasksTests|DeliverablesTests|MilestonesTests|ProjectsTests` 59 passed; full `dotnet test tests/Hub.Tests` 1211 passed, 0 failed. The builder ran the full suite (1194 passed) and the coverage gate (rules engine 96.6% branch, services 95.1% line) on the slice branch. The merge conflict was only in `Flags.cs` and the two flag-framework tests; both flags are kept and the integration branch's generalised flag tests cover them.
- AC-RCV-01: PASS (local), API level. `Deleted_list_shows_who_and_when_and_restore_brings_dependencies_back`.
- AC-RCV-02: PASS (local). `Deliverable_and_milestone_restore_log_Restored_and_reevaluate`: same key, Restored entry, queued ProjectChanged.
- AC-RCV-03: PASS (local), API level. `A_child_of_a_deleted_parent_is_refused_naming_it`: task under a deleted deliverable, deliverable under a deleted milestone; reason on the list and in the 422.
- AC-RCV-04: PASS (local), API level. `Only_PMs_and_Admins_read_or_restore`: Discipline Lead, Team Member, Read Only, a Cancelled project and a hidden Restricted project. Archived not exercised separately; the same permission rule covers it.
- AC-RCV-05: PASS (local). `Stale_version_conflicts_and_a_second_restore_is_404`.
- AC-RCV-06: PARTIAL. Server side PASS (local): `Lists_counts_search_and_exports_never_show_deleted_items` with the flag on and off, new endpoints 404 with it off, `Task_restore_with_the_flag_off_behaves_as_today`. Dialog and toast parts are S02.
- AC-RCV-07: PASS (local). `Deleted_list_export_matches_the_screen_and_is_logged`: CSV rows equal the screen, XLSX content type, both logged without content.
- Assumptions: defaults 045-D1 (RestoreItem: project PM or Admin), 045-D2 (tasks, deliverables and milestones only), 045-D4 (refuse naming the parent, no cascade), 045-D5 (no reason; `If-Match` checked only when sent), 045-D13 (Part A behind `flag.show_deleted`; task restore unchanged with the flag off). Route shape `/projects/{id}/{tasks|deliverables|milestones}/deleted` and `/deleted/export` is the builder's choice; S02 uses these paths. Milestone status at deletion is Complete, Cancelled or its last evaluated state. Rows deleted at the same moment order by key ascending. Pages hold 200 rows (`pageSize` capped at 200). `permissions.restore` has the `{ok, reason}` shape and is absent, not null, with the flag off.
- Left for Jay: when to turn `flag.show_deleted` on in review and pilot (S12 cleanup).

## Build 045-S03

Project Dashboard and Milestones counts as `{value, link}`, unflagged (Tier B, 045-D13). `RiskQuery` and `IssueQuery` in `Features/Registers.cs` are shared by the register lists and the dashboard. The dashboard response gains `registers` (`issuesOpen`, `issuesHigh`, `risksHigh`) and `disciplineDue14` beside the unchanged `issues`, `risks` and `disciplines[].deliverablesDue14`. `Dashboard.tsx` uses the server links; every count on `Milestones.tsx` (table and phone cards) links to its list through one helper, `milestoneLinks`.

- Checks on the merged tree (clean merge, no conflicts): `dotnet build Hub.slnx` 0 errors; `dotnet test --filter CountLinkTests|RegistersApiTests|SignalTrustApiTests|MilestonesDeliverablesTests|SurfacesTests` 25 passed, 0 failed; `npm run build` succeeded; `npm run lint` no errors (warnings only, none new apart from the existing export-components rule on the exported status list and helper). The builder ran the full suite on the slice branch (1200 passed).
- AC-RCV-08: PARTIAL. `Dashboard_issue_and_risk_counts_equal_their_register_lists` and `Discipline_due_14_count_equals_its_deliverable_list` pass for a PM, a Discipline Lead and a Supervisor, with and without a discipline scope. Browser counts suite not run.
- AC-RCV-09: PARTIAL. `Milestone_counts_equal_the_lists_they_open` passes, using the same parameters as `milestoneLinks`, including cancelled items and tasks attached through a deliverable. The `counts.cjs` Milestones check was added but not run.
- AC-RCV-13: UNRUN. The API tests assert the new values equal the existing snapshot fields, but `npm run test:counts` against the packet 036 fixtures was not run.
- Assumptions: defaults 045-D6 (a summed count gets a one-line definition plus a link; none of this slice's counts is a sum), 045-D8 (existing count values and definitions unchanged; new fields sit beside old ones; deliverable links carry an explicit status list without Cancelled), 045-D13 (Part B unflagged, Tier B). Issues and Risks counts stay project-wide under a discipline scope, so their links carry no `disciplineId` (AC-CNT-07, the tile's "Whole project" chip). Field names `registers` and `disciplineDue14` are the builder's choice. "n/total issued" and complete/total tasks each became two links, one per number.
- Left for Jay: run `npm run test:counts` against the local Development preview with the packet 036 fixtures (set `TUESDAY_UI_BASE_URL`) to finish AC-RCV-13 and the browser parts of AC-RCV-08 and AC-RCV-09; Tier B review of the merge into claude/build-039-050.

## Build 045-S06

Domain only, no flag (nothing passes a calendar until S07). `WorkCalendar.Add` in `Calendar.cs` now steps both ways: a negative count goes back to a working day, 0 keeps the date. Its only existing caller (`Evaluation.After`) already skips lag of 0 or less, so dependency lag is unchanged. `TemplatePlanner.Plan` in `Templates.cs` takes an optional `WorkCalendar? calendar = null`; null keeps calendar days.

- Checks on the merged tree (clean merge, no conflicts): `dotnet build Hub.slnx` 0 errors; `dotnet test --filter TemplatePlannerTests|DependencyLag|Calendar` 39 passed, 0 failed. The builder ran the full suite on the slice branch (1207 passed) and the coverage gate (rules engine 96.6% branch, services 94.8% line, PASS); `Calendar.cs` and `Templates.cs` have no uncovered branches.
- AC-RCV-14: PARTIAL. Domain level PASS (local): `Calendar_days_are_unchanged_without_a_calendar`, also with an all-working-days calendar. Building the calendar from the office is S07.
- AC-RCV-15: PASS (local), domain level. `Working_days_skip_weekends_and_office_holidays`: Friday +1 lands on Monday, or Tuesday when that Monday is a holiday; previous-milestone anchors, deliverables and tasks also checked.
- AC-RCV-16: PASS (local), domain level. `Negative_offsets_count_back_to_a_working_day` (Tuesday -2 lands on the Friday before) and `Offset_zero_and_typed_dates_never_move` (a typed Saturday and offsets of 0 or none stay on it).
- Assumptions: default 045-D9 (the template working-day calendar is weekdays without organisation and office holidays, independent of `working_days_enabled`; the domain function takes whatever `WorkCalendar` the caller builds, and S07 builds it). `WorkCalendar.Add` was changed in place rather than adding a second method.
- Left for Jay: nothing for this slice.

## Build 045-S09

Server side of the status rollups behind `flag.status_rollups` (declared in `Flags.cs`; the constant is `DashboardEndpoints.RollupsFlag`, reused by Portfolio). `/projects/{id}/dashboard` gains `disciplineStatus`, keyed by discipline id like `disciplineDue14`, with four `{Value, Link}` segments (`notStarted`, `inProgress`, `issued`, `onHold`, in bar order), each counted by the same deliverable list query its link opens. Portfolio rows gain `overdueDeliverables`, one grouped query using the deliverable list's `indicator=overdue` filter over the rows the viewer can see; saved reports reusing Portfolio rows pick it up while the flag is on. With the flag off neither field appears.

- Checks on the merged tree (clean merge, no conflicts): `dotnet build Hub.slnx` 0 errors; `dotnet test --filter CountLinkTests|PortfolioTests|DashboardTests|ReportsTests|Flag` 37 passed, 0 failed. The builder ran the full suite on the slice branch (1222 passed); the coverage gate was not run. No web changes.
- AC-RCV-18: PARTIAL. API level PASS (local): `Status_segments_equal_their_filtered_registers_and_leave_out_Cancelled` (segments 2, 3, 4 and 1, Cancelled left out; each link opens exactly that many rows for the PM, a Discipline Lead and a Supervisor, with and without a discipline scope; field absent with the flag off). Interface is S10; flag removal S12.
- AC-RCV-20: PARTIAL. API level PASS (local): `Overdue_deliverables_counts_overdue_not_at_risk` (3 overdue, equal to the `indicator=overdue` list; the At Risk list has 5; Issued never counted; absent with the flag off). Export columns are S11.
- AC-RCV-21: PASS (local), API level. `Rollups_ignore_projects_the_viewer_cannot_see`: a Supervisor not on a Restricted project gets no row or figure from it and its dashboard answers 404; an Executive sees both, each counting only its own deliverables.
- Assumptions: defaults 045-D10 (status groups: Not started; In progress = In Progress, In Review, Revision Required, Ready to Issue; Issued = Issued, Accepted; On hold; Cancelled left out), 045-D12 (Portfolio figure counts Overdue, G-01, not At Risk), 045-D13 (Part D behind `flag.status_rollups`; new fields absent, not null, with the flag off). Field names `disciplineStatus` and `overdueDeliverables` are the builder's choice. `overdueDeliverables` is a plain integer with no server link, since Portfolio row links are built by the client (FR-RCV-08). The server does not return the "n of m issued" total; the client sums the four segments.
- Left for Jay: when to turn `flag.status_rollups` on in review and pilot (flag removal is S12).
