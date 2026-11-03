# Generation 26 exact report-unit review repair

BACKEND READY / C# HELD. Only the named `ReportUnits.cs`, `CapacityReports.cs`, and new `ReportUnitsTests.cs` changed in this repair. Root's already-loaded, JSON-ignored `PersonLoad.ProvisionalWeeks` seam is consumed without another read or a Workload/Planner/calendar calculation change. No C# mutation or lane compiler, check, test, browser, build, generator, migration, helper or goal process is in flight. Root owns independent re-review and execution.

## P2a: preserve positive undated evidence in exports

`UnitExport` now emits exact source hours, coverage and missing-input evidence for `UndatedHours` as well as period `Hours`. Undated context is never divided by the FTE denominator. A new real API fixture has an 8-hour undated task and a separate unestimated undated task: total unavailable, recorded known subtotal 8 h, missing count 1. Both actual CSV and XLSX are read as column/row tables, with assertions against the specific person's undated quantity and all three evidence columns rather than loose whole-file number searches.

## P2b: preserve provisional availability

The report-local exact measure retains per-displayed-week provisional metadata alongside the canonical decimal source. Employee Available consumes root's `ProvisionalWeeks`; null metadata fails closed, empty means assessed with no provisional weeks. Same-measure aggregate metadata propagates uncertainty before period conversion. An affected month or week has no complete quantity, coverage `ProvisionalAvailability`, and its exact recorded source hours retained with a qualified label. Unaffected periods and work estimates retain their values. Public DTOs and original Rows/Columns stay unchanged.

Both Team Capacity branches now retain provisional state for capacity, time away, remaining and over-planned counts; under-planned counts are unavailable throughout a source person's displayed window when any of that person's displayed cells is provisional, since the canonical indicator can depend on neighbouring weeks. No indicator is recomputed. Other over/under indicator values carry `AvailabilityDependentCount` rather than an assertion of complete availability: their canonical producer uses a lookahead which is deliberately not queried or expanded by reports. Partial-view people counts retain their original meaning. Count exports gain a coverage column; they never gain source-hour or FTE fields. Screen labels show the same qualifications.

The new normal-API Reduced override fixture exercises Employee Available and exact aggregate, month propagation, unaffected December/demand, legacy weekly source unchanged, Hours and FTE states, and both exports. It exercises Team Capacity with `org_structure` off and on, capacity/time-away/remaining and affected/other weekly count coverage, and both export formats. Existing 18 cases remain; total prepared cases are now **20**: 6 arithmetic Facts, 10 API Facts and 1 API Theory with 4 InlineData rows. These are authored and **unrun** by this lane.

## Root integration

Merge exactly the three server and three UI keys in `g26-report-unit-text.json` before compilation/UI verification. `KnownHours` retains the existing contract name but is labelled **recorded provisional source hours** whenever coverage is provisional; it is not presented as verified availability. For a column containing any provisional source, the export source-hours column uses the qualified recorded-source heading and every cell retains its own coverage.

The UI-only `Reports.tsx` qualification edit is ready/held. Only `web/tests/report-units.cjs` and lane notes remain open. No shared Text/en/generated/cache/metadata edit was made by this lane. No test or runtime PASS, acceptance, flag activation, commit, push or deployment is claimed.
