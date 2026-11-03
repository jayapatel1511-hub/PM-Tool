# Generation11 exact-date adapter — READY

All owned source held. No edit or process in flight. No lane build/test/browser/generator/migration/helper/goal ran. Root owns compilation, the six new Facts, S30/adjacent regressions and final review. No slice/packet acceptance claim.

## Implemented maintained files

Only `src/Hub.Api/Features/AllocationScreenReads.cs` and new `tests/Hub.Tests/Api/CapacityExactDateReservationTests.cs` changed in this batch. No OpenRoles change was needed; commands, Allocation/Preview/Planning routines, old S30 tests, UI, schema, settings, permissions, locales and generated files remain held/untouched.

The final callable is **AllocationScreenReads.ConfirmedByDiscipline** with ExactDateReservations.FromDate/ThroughDate, retaining the FIRST published contract that Dashboard is already wired to. The final own `s22-exact-date-contract.md` is authoritative and matches source/C's actual call. An interim ConfirmedInWindow name was removed before build; no alias or duplicate implementation exists. Per root final review, its result uses the minimal six-field discipline record with no exposed UnattributedAllocations count. Existing S30 counts are unchanged. It is public for focused adapter integration tests and creates no HTTP route or alternate permission boundary.

The single private ReadReservations read now owns the existing reviewed current task/review/purpose/person/filled-role attribution plus full-range canonical person-calendar/day-override spread. S30 Compare consumes those same facts while retaining its original weekly clipping/sums, status split, coverage/unknown fields, task due-week logic and JSON/record shape. The exact reader selects only Confirmed reservations from the supplied authorized query, additionally constrained by project and exact inclusive range, then clips each usable daily spread before decimal aggregation. It never sums weeks or adds Proposed/planning/task forecasts/role hours. No new staffing formula.

VisibleOnly has ConfirmedHours=null and a usable known subtotal, including known0. Calendar failure remains null + visible unavailable count; it is not silently clipped away or converted to0. Unattributed retains known/unknown hours and only selected visible unavailable counts. As root clarified, a usable Unattributed row clipped to known0 is not by itself a gap; nonzero/unknown Unattributed or VisibleOnly coverage prevents a definitive discipline/excess assertion. Active disciplines have stable known-zero buckets under complete coverage; selected inactive disciplines remain represented. No hidden record/person/other-project metadata/cardinality is read behind the supplied query. Caller owns access, completeness, snapshot and read clock.

## Six prepared Facts — runtime UNRUN here

1. Exact Tue10→Tue17 partial-week window from a100h Mon9→Fri20 allocation with Wed11 override28:68h (28+5×8), single-day28, full100, usable overlapping weekend0 and outside-range0. Complete read/dates/AsOf and durable-state equality. The helper works with capacity_screens off while the HTTP comparison route remains dark.
2. A10h/three-working-day reservation retains exact decimal last-day remainder; full10 and clipped10−10/3. Existing S30 weekly JSON is byte-identical before/after pure adapter calls, with a fixed test clock.
3. A normal full-range calendar failure (the only scheduled weekday becomes a holiday) remains unknown in an overlapping Tuesday clip; another person's usable reservation contributes known10, unavailable1, totalnull. A usable full-range reservation overlapping a weekend contributes a genuine0. No write.
4. Obtain Marc's authorized allocation IDs from the real current allocation list, pass only that query with VisibleOnly, and retain known40/null exact while a hidden99 reservation remains absent. Complete supplied scope gives139; Proposed777, Completed888, another project's333 and a Confirmed planning entry never enter reservations. Empty visible-only buckets stay null/known0; inverted input is400 and leaves state unchanged.
5. Real source/package/start/allocation API fixture gives current Electrical12. A normal round replacement leaves the old Pending assignment/link but supersedes its round: exactly12 moves to Unattributed, no loss or write.
6. A normal Fill command with conflicting role/work disciplines gives exact4 Unattributed. Explicit stale cancelled-role evidence no longer overrides valid Civil work. Released current work evidence retains the reservation, and an overlapping weekend clip has Unattributed known0/exact0/unavailable0, matching root's harmless-zero semantics.

The shared attribution extraction preserves the earlier S30 ten-Fact stale-review, task-state, purpose/person and missing/foreign/role-conflict regressions; those remain an important adjacent root run. Direct stored status changes in new fixtures are clearly synthetic setup, not assertions of newly authorized workflow transitions. No operational data is touched.

## Checks and root verification

PASS static `git diff --check` and direct whitespace/final-newline checks on both owned C# files. No C# compile/type/EF query translation/runtime claim. Root commands: new `CapacityExactDateReservationTests` class filter; existing `CapacityAllocationScreenTests` and relevant allocation/preview/planner/contract checks. C separately owns Dashboard target selection, caller scope/snapshot, unknown composition and120/80/24/40 acceptance. No locale/schema/registry addition is requested by this helper itself.
