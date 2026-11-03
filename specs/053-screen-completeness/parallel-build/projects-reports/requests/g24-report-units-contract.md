# Generation24 S25 stable period/unit contract — root integration request

Scope: capacity_screens independently of visual_system. Eligible week-matrix forms are workload-employee, workload-discipline, team-capacity and allocations-by-source show=hours-by-source. Workload remains8 displayed weeks anchored by its existing read; Capacity keeps its actual1..26 horizon/default. Task Hours, pilot/staff/count reports and allocations entries-without-project are not period/FTE reports. No chart, deficit or canonical calculation changes.

## Parameters and S23 compatibility

Proposed exact report keys: `unit=hours|fte`, `period=weeks|months`; defaults hours/weeks. Add them to currently supported ReportParams and static SavedViewParameterKeys only for the four eligible codes while capacity_screens is on (registration union includes them for dark view validation). Current strict Param select/repetition validation handles them on Run, export and metric GET; unknown values/repeated values refuse. They never become TaskQuery/people/source predicates. Keep the complete canonical selected report parameters, including these selectors, in Save/Copy/exports and S23 hrefs/metric parameters so opening a task-input list does not reset the chosen report display. Every metric source/count/row ID remains invariant under all four unit/period pairs. No report-owned TaskInputs/Workload/Planning/Time algorithm or existing suite edit by C.

Entries-without-project keeps its dates and h/week rate. It accepts only omitted/default hours/weeks selectors, rejects nondefault fte/months instead of implying conversion, omits period metadata and does not store irrelevant selectors in its canonical map. UI switching to this form clears unit/period; all other selected data scope remains. Saved-view cross-validation needs the same rule. Root please extend ReportViewParameters for this cross-field refusal; normal select validation already handles eligible options. Noneligible codes never advertise these selectors.

## Exact optional response DTO

Append null-omitted `ReportView.UnitView : ReportUnitView?`, present only for eligible enabled matrix forms. Existing TaskInputs stays unchanged. Intended public records:

```csharp
public sealed record ReportPeriod(string Key, string Label,
    IReadOnlyList<DateOnly> Weeks, int WeekCount, decimal? DenominatorHours);
public sealed record ReportUnitCell(string Path, string Kind,
    decimal? Value, decimal? KnownHours, int MissingInputs, string Coverage);
public sealed record ReportUnitRow(int RowIndex, string Kind,
    IReadOnlyList<ReportUnitCell> Cells);
public sealed record ReportUnitView(string Unit, string Period,
    decimal? StandardWeeklyHours, int DisplayedWeekCount,
    IReadOnlyList<ReportPeriod> Periods,
    IReadOnlyList<ReportColumn> Columns, IReadOnlyList<ReportUnitRow> Rows,
    string ScopeCoverage, int UnestimatedTasks);
```

Final schema refinement before shared integration: keep the existing ReportView.Rows/Columns result entirely intact and carry the typed selected display matrix in UnitView. This preserves S23 result wire fields while avoiding ambiguous per-row mixed hour/count kinds and untyped parallel path conventions. UnitView.Rows references the corresponding original row by RowIndex and overlays only maintained cells; identity/count/rate context can reuse original values. UnitView.Columns are the actual displayed column set. Kind is Hours, Count, WeeklyRate, UndatedHours or NotApplicable (ratio context stays Ratio). Each typed cell carries the exact selected decimal/null, exact known hours when relevant, unavailable-input count and coverage. UI formats only; the same typed matrix is flattened for export without feeding rounded original Rows back into the transform. Run caps typed rows to the same screen range; the export uses the complete authorised source.

Null quantity stays unavailable with its known subtotal rather than becoming0; NotApplicable remains distinct from Unavailable. Standard0/null remains recorded and FTE unavailable. Coverage is retained from the authorised source, including VisibleOnly/provisional/unestimated context; no personal-capacity denominator. This is the final requested DTO contract; it supersedes the initial parallel-path sketch and needs no widening of existing ReportColumn.

RowIndex=-1/Kind=Total is reserved for a Workload Employee/Discipline aggregate footer. It sums exact same-measure source hours across the complete authorised source before conversion; it never sums FTE labels or unrelated context/count/ratio fields. Existing ReportView.Rows stays unchanged; UI/export supplies the typed footer separately. Capacity source totals already exist and are not summed across unrelated measures. Root raw Discipline seam and existing ExportFile.Send header opt-in are acknowledged and wired; no shared export change is needed. CSV header includes an explicit applied-parameter summary; XLSX retains normal per-parameter rows plus unit basis without duplicating every parameter as another header line. Settled server/UI copy is in g24-report-unit-text.json.

Monthly Team Capacity hour columns are distinct from explicitly labelled original weekly people-count columns retained beside them. Its over/under/partial rows keep their actual weekly count cells and no monthly count sum/max/distinct invention; hour rows have nonapplicable weekly-count cells, count rows have nonapplicable month-hour cells. Row/column kind is carried by maintained descriptors rather than parsing translated labels. Weekly FTE similarly converts only the six hour measures; people rows remain people. Employee capacity(h/week), undated hours and all task/person counts stay in their original context/units.

## Needed root-owned exact Discipline seam

Employee `PersonLoad.Cells` and Planner `CellRow` are already unrounded; Reports can use their existing same-read results. DisciplineRows currently rounds each week before returning and cannot supply exact sub-tenth numerators. No Workload.cs edit is loaned to C this generation. Please extract the existing authorised DisciplineRows read without changing its math/grouping/legacy output, exposing:

```csharp
internal sealed record DisciplineReportRead(Col[] Columns, JsonArray Rows,
    List<DateOnly> Weeks, IReadOnlyList<decimal[]> ExactHours);
internal static Task<DisciplineReportRead> ReadDisciplineReport(
    Guid? officeId, Access access, HubDb db, CurrentUser me,
    SettingsStore store, TimeProvider clock);
```

ExactHours arrays are in exactly existing Rows order and Weeks order, before Math.Round(…,1). Existing DisciplineRows wraps this read and returns its unchanged Columns/Rows. Reports enabled path calls ReadDisciplineReport once, uses its row counts/identity/unestimated context and exact hours, then establishes the unchanged S23 source. No duplicate grid/load read, no alternate calendar/spread math. Root may choose an equivalently typed narrow callback seam but please publish exact names before integration. C can author the pure period transform while this shared seam is prepared.

## Export and verification contract

The same transform drives screen and export. Converted quantities use exact canonical numerators and denominator (k × standard). Export has selected quantity plus exact known-hours/coverage/missing-input context, explicit week-count/date labels for each period, standard hours, scope and missing task inputs in Parameters. For CSV, enable the existing optional metadata header for these reports only; root owns any narrow ExportFile.Send opt-in seam if its current implementation limits headers to readiness. Existing BOM/formula/escaping/caps/audit/XLSX remain. Off body/export bytes stay exact. Default screen rounding is presentation, never a numeric input to FTE/months.

Root integration: metadata/View union and cross-field cleaning, Text/en keys, cache if needed, generated types/contracts and held legacy assertions for intentional optional UnitView/default selector additions are root-owned. C will not rewrite existing suites. New ReportUnitsTests.cs/report-units.cjs cover exact160/120/40/4weeks=1/.75/1.75 despite custom32, partial month80/2/40=1, Thursday boundaries, sub-tenth inputs, unknown/zero standard/unknown hours, distinct count/rate/ratio fields, all actual four matrices, scope/export/save/link, unchanged S23 inputs/horizons and phone/off. No execution or acceptance claim by C.

## Backend ready/held source inventory

Final public DTOs are exactly the typed ReportPeriod/ReportUnitCell/ReportUnitRow/ReportUnitView records above, in ReportUnits.cs; ReportView.UnitView is appended optional/null-omitted. ReportParams advertises strict default unit/period choices only for eligible codes while capacity_screens on; static SavedViewParameterKeys includes the same registration union for those four codes. Entries-without-project rejects nondefault selectors, and both SavedParameters and visible applied parameter descriptions omit irrelevant defaults. Full canonical report selectors remain intentionally on S23 metric links; no task predicate changed.

ExportFile implementation is untouched. Eligible export uses a RepeatableRead snapshot around the one existing authorised source, typed unit transform, missing-input context and existing ExportFile.Send, then commits so its normal Exported audit is retained. Existing optional header receives unit/standard/period/week-count/coverage/unestimated basis, each period's actual Monday set/denominator and a named applied-parameter summary for CSV. XLSX retains its normal parameter rows plus this basis. The typed row/cell matrix supplies exact values and known-hour/coverage/missing columns; no rounded r.Rows value feeds period arithmetic. Legacy/off/header-null paths remain existing.

ReportUnitsTests.cs has exactly14 Fact methods plus1 Theory with4 InlineData rows:18 total prepared cases, comprising6 pure arithmetic and12 real-API cases. Existing report suites were not edited. Source/tests are BACKEND READY and held; no compilation/syntax/test/browser/generation ran in this lane. Parent-reported text merge and reviewed raw Discipline/View/cache seams are acknowledged. C continues only UI/new script, with no further C# edit outside a root-named repair exception.

UI text refinement only: param.unit should be Report unit so it remains distinct from the organisation Unit label. Reports.tsx also uses index-qualified applied-parameter keys so equal translated labels cannot collide. Five final UI selection/Hours/FTE/Weeks/Months labels were added to g24-report-unit-text.json after root's initial copy merge; please merge these final additions/label correction. All original canonical tokens remain visible, and the unit/period/denominator/rounding explanation is a visible footnote after the table. No new panel, chart or shared UI helper.

AC43 horizon nuance is explicitly covered by the actual API fixture: clock=2026-11-02, canonical Workload8 weeks through2026-12-21, four displayed November ISO weeks with160/120 h/custom32, giving November1/.75/1.75 and separate four-week December0. The browser fixture will use the same8-week/two-month model. No shortened Workload horizon or whole-eight-week denominator is called the four-week example.
