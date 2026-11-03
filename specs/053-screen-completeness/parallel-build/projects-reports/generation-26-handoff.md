# Reports S25 handoff, generations 24–26

**ALL SOURCE READY / HELD.** No C#, API-test, UI, CSS or browser-script mutation is in flight. No lane compiler, check, build, test, browser, generator, migration, helper or goal process is running. Root alone integrates/reviews/regenerates and executes. This is a local authored-source handoff, with no runtime PASS, human acceptance, commit, push, deployment or activation claim. S25 stays pending root evidence.

## Source inventory

- `src/Hub.Api/Features/Reports.cs`: capacity_screens-only unit/period definition/validation, canonical saved/input parameters, optional null-omitted UnitView, exact source capture, matrix export and parameter header integration. Existing Rows/Columns stay intact and dark output stays on its prior path.
- `src/Hub.Api/Features/CapacityReports.cs`: exact values retained within the original actual grid/historical group loops, with explicit non-hour count measures and provisional availability metadata.
- `src/Hub.Api/Features/ReportUnits.cs`: exact displayed-ISO-week/month arithmetic and standard-hours FTE conversion, typed selected matrix, full-source same-measure Workload totals, missing/provisional/standard-unavailable handling and exact export/evidence columns.
- `tests/Hub.Tests/Api/ReportUnitsTests.cs`: 20 prepared/unrun cases: 6 arithmetic Facts, 10 API Facts, and a four-row Theory. Existing 18 retained plus the two specific independent-review repairs.
- `web/src/pages/Reports.tsx` and `reports-catalogue.css`: actual server-described selectors, fixed-rate entry-form clear/hide rule, typed cells/footer and qualified unavailable states, basis after the table, complete filter tokens and unchanged S23 source-return behavior. No chart/deficit or geometry-threshold change.
- `web/tests/report-units.cjs`: completed new mock-browser acceptance script, unrun including syntax/type checks.

Root-authored seams remain root-owned: exact DisciplineReportRead, JSON-ignored PersonLoad.ProvisionalWeeks sourced in its existing availability read, Views validation/registration, keys/cache, Text/en and generated API metadata. This lane changed no Workload/Planning/PersonCapacity/Time/ReportTaskInputs algorithms or earlier report suites in S25.

## Semantics and evidence

All four eligible matrix forms use unit=hours|fte and period=weeks|months. Workload stays eight weeks. The actual four-week November AC case is a month bucket inside that canonical eight-week read: 160/120 exact hours yield 1/.75 FTE and an exact 1.75 same-measure total. A person's 32-hour capacity remains a weekly rate and never replaces the organisation's standard denominator. Thursday assigns ISO weeks to months; a partial two-displayed-week month uses k=2. No rounded wire values or rounded FTEs are summed.

Weekly people counts remain distinct weekly counts; weekly rates and undated hours keep units. Missing estimates, known zero, unknown standard and restricted/partial scope retain their distinctions. Positive undated known subtotal/coverage/missing evidence now survives CSV and XLSX. Provisional availability suppresses complete Employee Available and Team capacity/time-away/remaining quantities, including affected months and totals; recorded exact source hours are qualified. Availability-dependent count coverage is explicit rather than a completeness claim, including the deliberately unassessed canonical lookahead. Details: `requests/g26-report-unit-repair.md`.

The new script checks actual 1/.75/1.75 cells and custom32 rate, Hours/Weeks controls, all four matrix forms, the 80h/two-week partial month, separate weekly counts, typed sub-tenth precision despite rounded legacy0, provisional/undated states, exact save/Copy/export parameters, fixed-rate entry form and incompatible-link refusal, S23 actual Task open/close and metric-focus return retaining selectors, independent visual/capacity gates, and phone containment.

Its 1440×1000 geometry guard measures the real first nonempty table row in all four matrix forms after fonts, main scroll0 and three stable layout samples. It retains **row top ≤405** without polling for a passing coordinate, substituting a row, clipping copy or weakening any bound. A failure needs a named source repair, not an assertion change.

## Integration and verification still owned by root

Use the final contract in `requests/g24-report-units-contract.md`. Ensure final selection-label/report-unit disambiguation from `g24-report-unit-text.json` is merged, plus exactly 3 server and 3 UI keys from `g26-report-unit-text.json`. No additional public DTO field was added during the P2 repair. New coverage codes are string values in the existing contract.

Backend is ready for independent re-review and root's refreshed freeze manifest. Root should run its chosen focused API set including ReportUnitsArithmeticTests and ReportUnitsTests (20 new cases), required generated-contract/frontend checks, and the new report-units browser script with relevant unchanged S23 regressions. No such execution or post-change browser verification was performed in this lane. Prior checkpoint19/S23 PASS remains parent-reported historical evidence, not evidence for S25.

No outstanding lane edit remains. Reopen only by a named bounded root exception.
