# S24 capacity chart: proposed source and rendering contract

**Approved for C46 implementation after the following corrections; exact bridge signatures below.** The initial generation45 pass changed only this contract. Checkpoint20/S25 success is root-reported evidence for the preceding work, not verification of S24.

C46 ALL SOURCE READY / HELD: owned backend, UI/CSS, Task-return key preservation and new browser script are complete, with no mutation/process in flight. Final inventory, root-reported backend evidence and unrun frontend/browser scope: `../generation-46-handoff.md`. The final unit abbreviation request is merged in current en.ts. Only a root-named bounded repair may reopen source.

C46 BACKEND READY / C# HELD: new ReportCapacityComparison.cs, Team-only capture in CapacityReports.cs, and new ReportCapacityComparisonTests.cs are authored. No C# mutation/process is in flight. Exactly11 prepared/unrun cases:6 pure projector Facts and5 normal-API Facts. Root's Reports.cs bridge is acknowledged. Root locale request is `g46-report-chart-text.json` (2server keys plus UI copy). No compiler/check/test/build/browser/generator/migration/helper/goal/status command ran. Only approved frontend/script/notes continue; root owns independent review and verification.

After root's initial33-UI-key merge, one further unit abbreviation key is added to that request: `reportChart.hours = h`. Please merge this extra UI key before frontend type checking. Root's ZIP-container timestamp test-only repair is acknowledged; all C# stays held and this lane will not overwrite it.

Root accepts the supplemental typed table, current home-discipline classification of all visible planning work, and monthly sum of positive weekly group shortages. `PersonRow.Partial` is horizon-wide: conservatively suppress complete planning demand/shortfall for **every included week and period containing that person**, with no invented unaffected private weeks. `CellRow.Provisional` describes that week's availability, not membership. Missing estimates remain separate S23 context. Capacity already includes away and Confirmed already includes approved coverage; neither is counted twice.

The hard cap is **2000 expanded comparison rows**, not2000 groups: retain at most `floor(2000 / actualPeriods.Count)` complete, stably ordered groups. Return all periods for each retained group. Expose separate ReturnedGroups/TotalGroups/Truncated; client pagination reaches only ReturnedGroups, with true total and refine-scope guidance separate. No inaccessible next pages.

### Exact root bridge and owned source signatures

Root appends optional/null-omitted `ReportCapacityComparison? CapacityComparison = null` to ReportView; adds `public bool CaptureCapacityComparison { get; set; }` and `public ReportCapacityComparison? CapacityComparison { get; set; }` to Ctx; enabled `team-capacity` GET Run sets the bool before `def.Run`, then appends `ctx.CapacityComparison` to ReportView. Export and metric contexts leave the bool false. The capture method is private in the same partial class; root does not need to call it directly.

Owned helper `ReportCapacityComparison.cs`, all records/methods nested in partial ReportEndpoints:

```csharp
public sealed record ReportCapacityComparison(string GroupBy, string Basis, string Unit,
    DateTimeOffset AsOf, string ScopeCoverage, IReadOnlyList<ReportPeriod> Periods,
    IReadOnlyList<ReportCapacityComparisonRow> Rows,
    int ReturnedGroups, int TotalGroups, bool Truncated);
public sealed record ReportCapacityComparisonRow(string Key, Guid? DisciplineId, string Discipline,
    string PeriodKey, ReportUnitCell Capacity, ReportUnitCell Confirmed,
    ReportUnitCell Expected, ReportUnitCell Possible, ReportUnitCell Demand,
    ReportUnitCell Deficit, int PartialPeople, int ProvisionalPeople);
// Pure projector input, never serialized by an endpoint or included in the response DTO:
public sealed record CapacityComparisonMember(Guid Id, Guid? DisciplineId,
    decimal? Capacity, decimal? Confirmed, decimal? Expected, decimal? Possible,
    bool Partial, bool Provisional);
public static ReportCapacityComparison ProjectCapacityComparison(
    IReadOnlyList<DateOnly> weeks,
    IReadOnlyList<IReadOnlyList<CapacityComparisonMember>> membersByWeek,
    IReadOnlyDictionary<Guid, string> disciplineNames,
    string unit, string period, decimal? standardWeeklyHours,
    DateTimeOffset asOf, string scopeCoverage);
static Task CaptureTeamCapacityComparison(Ctx c, PlanningEndpoints.GridResult grid,
    IReadOnlyList<IReadOnlyList<PlanningEndpoints.PersonRow>> membersByWeek);
```

The capture helper handles its guard/name lookup/projector and writes `c.CapacityComparison`; TeamCapacity calls it once with actual per-week members, after its scoped input context is established. Source ids stay only in projector input, for distinct affected-person counts; they are absent from response rows. The new public response has ReturnedGroups in addition to the original proposal's total/truncation. This signature block is authoritative for root's bridge.

## Approved scope

Root approved **Team Capacity only**, behind `capacity_screens` and its existing `capacity_reports`/permission gates, with one comparison chart **grouped by home discipline**, a matching visible comparison table, and the existing measure matrix retained. This implements the real capacity/deficit case in FR/AC-SCR-41. There is no person grouping option. Project grouping is not offered: a person's available capacity cannot be assigned independently to every project without a new allocation rule or double counting.

**Required row adapter:** today's `TeamCapacity` result has nine scalar measure rows (or measure × historical organisation-unit rows), not discipline identities. `UnitView` transforms those quantities but cannot supply discipline groups. `Workload.ReadDisciplineReport` is assigned-task demand grouped by discipline name, not capacity/supply; attaching unrelated supply to those rows would be false. Therefore a chart drawn honestly from the current scalar `ReportView.Rows` is unavailable. Approve a null-omitted `ReportView.CapacityComparison` containing **typed report rows rendered as an adjacent numeric comparison table**; the chart reads exactly those rows. It does not fabricate group names on the client or reinterpret the scalar matrix.

This is a deliberate supplemental table adapter, not a claim that the old scalar rows already satisfy the grouped contract. If “own rows” must mean exclusively the existing `Rows` property, root must instead approve a separately named grouped capacity report form with these same rows; there is no honest chart-only shortcut. Recommendation: the optional comparison section, which preserves the existing report, saved scopes, matrix and exports.

## Actual source seams and boundaries

- `CapacityReports.CapacityGrid(Ctx)` already caches one authorised `PlanningEndpoints.ReportGrid` in `c.InputGrid`, with the current supervisor/discipline/from/weeks/includeMyDrafts scope and captured clock. Run already owns a RepeatableRead snapshot. Consume this result; do not issue another Planner/Workload HTTP or grid read.
- Capture the **actual eligible members per displayed week** in `TeamCapacity`. The no-org branch uses its existing `grid.People`. The org branch must collect members from its existing historical `placements[i]`/selected descendant-unit loops, once under the capacity measure, before home-discipline grouping. This is the exact needed `CapacityReports.cs` loan; a present-day unit filter, union across all weeks, or separate Planner query would change scope.
- Group those actual members by nullable `PersonRow.HomeDisciplineId`, stable GUID identity, with a distinct Not set group. Resolve names only for the IDs already represented, in the same snapshot; missing referenced names retain their ID and an unavailable-name label, not a merged Not set group. No person name/ID is added to the public chart DTO. Stable order: name, then ID; do not rank people or infer staffing suitability.
- Canonical quantities are already unrounded in `Planning.CellRow`. `Hub.Domain/Planning.cs::Combine` makes Confirmed/Expected disjoint from approved coverage; **Remaining = Capacity − Confirmed − Expected**. Possible is deliberately excluded from PLN-09. Thus supply is effective Capacity; planning demand is Confirmed + Expected. Keep Possible separately labelled in the table, excluded from demand/deficit. Do not add Approved again, task estimates, open roles, or proposed allocation requests.
- Add a `Ctx` opt-in set only by the enabled Team Capacity **Run** route. Export/metric routes leave it false. This avoids extra export work and leaves every current export byte, parameter, row, header, audit and rounding path unchanged. Existing `ReportView.Rows/Columns`, UnitView/S23 cohorts and source/math remain intact. Root owns the small ReportView/Run/Ctx bridge and generated metadata; C needs only the named capture/helper and UI loan after approval.

## Exact typed rows, periods and deficit

Proposed records, appended optional/null-omitted on ReportView:

```csharp
ReportCapacityComparison(string GroupBy, string Basis, string Unit,
    DateTimeOffset AsOf, string ScopeCoverage,
    IReadOnlyList<ReportPeriod> Periods,
    IReadOnlyList<ReportCapacityComparisonRow> Rows,
    int ReturnedGroups, int TotalGroups, bool Truncated);
ReportCapacityComparisonRow(string Key, Guid? DisciplineId, string Discipline,
    string PeriodKey, ReportUnitCell Capacity, ReportUnitCell Confirmed,
    ReportUnitCell Expected, ReportUnitCell Possible, ReportUnitCell Demand,
    ReportUnitCell Deficit, int PartialPeople, int ProvisionalPeople);
```

`GroupBy=homeDiscipline`; `Basis=PLN09ConfirmedExpected`. Cell Paths are the six fixed measure names, Kind=Hours; the existing Value/KnownHours/Coverage shape is reused. People counts are distinct affected visible people within that group's displayed period, never converted into hours/FTE or used as an input to a quantity. Key is `discipline:<guid>` or `discipline:none`; PeriodKey references the returned exact ReportPeriod. Rows are the **numeric comparison table's authoritative rows**, not a second client calculation.

Periods reuse `ReportUnitMath.Periods`: actual displayed Mondays only, existing 1–26 horizon, Thursday month assignment, partial-month k, one organisation standard denominator. Aggregate exact source hours before conversion; FTE is hours/(k × standard). No personal-capacity denominator, summing rounded values, summing weekly FTE, or additional lookahead weeks.

For each discipline and week, `demandHours = Σ(Confirmed + Expected)` and `capacityHours = ΣCapacity`; equivalently their difference reconciles to the **same cohort's canonical Remaining**. `deficitHours = max(0, demandHours − capacityHours)` only if both sources are verified. A period's deficit is the sum of its positive **weekly group shortfalls**, then converted once if FTE is selected. Spare weeks do not cancel overloaded weeks. Label the column/bar **Weekly shortfall** and state this rule visibly; a monthly shortfall need not equal max(0, monthly demand − monthly capacity). No person-level clamping before the discipline-week subtotal.

Each visible group's chart has Capacity, Planning demand and Weekly shortfall bars, direct values and “short by n h” for a positive verified deficit. In FTE mode the bar uses the row's exact FTE value and the annotation also retains its exact source-hour shortfall, e.g. “short by 10 h · 0.25 FTE”. The table shows the identical three values plus Confirmed/Expected/Possible context. Zero is a real zero; no positive-shortfall wording for zero.

Unknowns are not zero. Weekly provisional availability makes affected capacity/shortfall unavailable, with the existing provisional coverage and qualified recorded source hours on capacity. Horizon-wide Partial/hidden planning work makes demand and shortfall unavailable in every included week/period containing that person; visible source bands may remain as explicitly qualified recorded figures. **Do not calculate a numeric deficit from incomplete inputs or present that difference as a known subtotal/lower bound.** Its Value and KnownHours remain null if any required displayed week is unverified. An unavailable standard suppresses FTE Value while keeping valid hour evidence; Hours still works. Missing task estimates remain S23 context but are not silently substituted into planning demand. Coverage/asOf and excluded-Possible/weekly-shortfall notes stay visible.

## Renderer, page cost and reading path

Analytical job: compare capacity with planning demand by discipline and locate shortages. Artifact family: one directly labelled horizontal comparison chart. Primary specialist route: SVG/DOM; **React-owned DOM bars** are sufficient for these simple nonnegative linear scales. No D3, chart library, Canvas, animation or dependency is needed. The router and `d3-data-visualization/SKILL.md` were read, plus mobile-first and SVG polish references. React owns structure/state; a small pure geometry helper maps the already-computed server values onto one shared zero-based scale. Browser arithmetic never supplies table values or deficits.

One mounted `ReportCapacityComparison` component and one figure per open report, not one chart per group/period/card. A fixed page of **12 groups** drives both the figure and its numeric table in identical stable order. Explicit “showing a–b of returned N disciplines”, separate true total/truncation/refine-scope text, and Previous/Next controls bounded to returned groups; no hidden top-N/Other bucket. The adapter retains complete groups within the hard2000 expanded-row budget: floor(2000/actual period count) groups. Use the shared scale over all returned groups for the selected period, including other pages; do not rescale each discipline/page independently. Render keys include both group Key and PeriodKey. Cost is O(visible groups × 3 bars), a few hundred DOM elements, with no continuous listeners or redraw loop. All period rows come from the same response; changing the focused period/page does not fetch again.

Desktop: after the existing counts/S23 summary, keep the original matrix at its current start position; place the one chart and its matching comparison table in a report-local adjacent column. The first existing matrix row must retain the verified ≤405px guard at1440×1000; adding a proxy table row is not a substitute. At insufficient width, use vertical flow: existing matrix, comparison title/period and coverage, chart, matching numeric table. Preserve the catalogue's current collapse behavior. No shared Page/Shell/Grid/theme change is required.

At375/390px the chart uses full-width, vertically stacked group blocks with the discipline name above its three bars and inline numeric labels, rather than shrinking desktop text or placing long names on a tiny axis. The comparison table remains available in a keyboard-focusable contained horizontal region with all columns. Direct text labels are ≥12px using existing Plex and tabular numbers. Pagination/period controls use existing native select/Button and generous mobile hit areas; filters stay in the existing compact disclosure. No tooltip, drag, pinch, wheel capture or focusable decorative bar. Normal browser scrolling remains. Resize affects percentages/layout only, without changing source values or scope.

Colour roles: Capacity is neutral outline/context; planning demand follows existing planner Confirmed teal/Expected tint roles; Possible has no demand bar and stays separately labelled. Weekly shortfall uses existing critical/metric-red plus triangle and explicit text, not colour alone. Focus uses the normal focus ring, not another metric hue. Null quantities use an unavailable label/neutral placeholder rather than a zero-length bar or the time-away hatch. Match visual_system on/off tokens independently; colour changes no data. Check grayscale and200% zoom; no glow, gradient decoration or invented status mapping.

Each group has an accessible numeric summary (as in PlanningBandBar's role=img pattern) containing discipline, period, unit, supply, demand, deficit/unknown and coverage; decorative bar marks are aria-hidden. The real table has caption, scoped headers and identical numbers and remains the complete screen-reader/text fallback. Essential values, source/caveat and “short by” text are always visible without hover. A chart rendering failure leaves both tables and a concise unavailable-chart note; an empty authorised cohort stays Empty, not a zero-capacity assertion.

## URL, views, exports and root integration

All **data** selectors remain today's canonical report map: supervisor/discipline/from/weeks/includeMyDrafts/unitId/unit/period. Both tables and chart share that response and clock. No new API data parameter, backend filter, query cache dimension, permission, request or mutable record is introduced.

Recommended chart focus is UI-only `chartPeriod=<returned-period-key>` and `chartPage=<positive integer>`, like catalogue context: exclude them from report/metric/export queries and canonical saved data parameters; invalid/stale values fall back visibly to the first actual period/page1. Copy link appends only validated chart focus to the settled canonical report parameters. Named report views continue saving canonical data scope; opening a view starts chart focus at its first period. Unit/period/data-scope changes clear stale focus; Back/forward/reload preserve a valid focus. This separation is intentional and should be explicit in implementation, rather than silently registering display keys as server filters. No chart toggle/group selector is needed. S23 opens must preserve these UI keys alongside catalogue/view context and retain exact Task/metric origin/focus on return; that needs a narrow report-task-inputs UI loan, not a TaskInput API/source change. Root owns any shared key/ViewMenu integration if it prefers persisting chart focus inside named views as well.

Explicit dark-link edge: exclude the new UI keys in the common `chosen`-parameter filter before its flag branch, so a stale chart URL cannot leak them into legacy/off API requests. They remain ignored with no chart when the feature/report is ineligible.

Exports stay **the current measure-matrix export**, labelled as such near Export; neither chart focus nor the supplemental comparison table is added to CSV/XLSX. Add no chart export format. Numeric labels/cells derive from one typed row; screenshot/print of the screen retains them without interaction. Record freshness comes from the existing report query, not another stream; do not present a cached response as newly calculated or leave an old chart beside a newer matrix during refetch.

## Meaningful verification after root opens implementation

- Pure exact projector:40h capacity/50h Confirmed+Expected =>10h weekly deficit; Possible alone does not produce deficit; approved coverage is not added twice. Two weeks with opposite spare/short capacity retain the positive weekly shortage in their month, even if period totals balance. Sub-tenth, zero, custom32/standard40, Thursday boundary/partial month, and standard0/null follow S25. Unknown/provisional/partial sources never produce numeric deficit or fake0.
- Normal API fixtures: assign home discipline and create ordinary planning entries/availability through existing commands; each chart quantity equals its exact comparison-table row and canonical grid source, with both org branches and historical unit membership/descendant filtering. IDs stay distinct under equal display labels in a pure projection case; normal API tests use only domain-valid identities. Not set remains separate. Same response snapshot/asOf, no added grid call, no person grouping or source disclosure, ordinary403/404/off/independent visual flags, empty scope and bounded truncation.
- Export regression: with identical captured clock/data, actual CSV and XLSX bytes/parameters match the current matrix baseline; chart focus never appears in Run/export/metric requests or changes S23 task IDs/counts. Existing S25 headers/precision/unknown cases stay unchanged.
- Browser: actual capacity/demand/positive deficit and direct “short by10h”; all visible bar values/IDs match comparison-table rows; no person option; period/page URL/reload/Copy/Back and invalid focus; named view canonical scope, exact S23 Task/metric return/focus; original real matrix first-row≤405; no header/field removal;375/390 containment, keyboard/native select,200% zoom, grayscale/reduced-motion, stale/error/empty and table-only fallback. Root joins at least one real disposable API/UI case; mocked shape alone is not acceptance.

**C46 implementation is now READY / HELD**, with the inventory and verification limits recorded in generation-46-handoff.md. Shared locales/cache/generated files and all execution remain root-owned. S24 stays pending root's independent review and evidence; no implementation acceptance or decision-status change is claimed.
