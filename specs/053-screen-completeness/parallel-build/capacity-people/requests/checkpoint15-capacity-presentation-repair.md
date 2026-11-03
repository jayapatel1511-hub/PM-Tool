# Checkpoint15 capacity presentation exception — READY

**RE-ACK ALL SOURCE FROZEN generation10.** No edit/process is in flight. Root alone rebuilds/runs browser and the unchanged real census. All C# and shared source remain untouched/held. No runtime/browser/test/build/generator ran in this lane.

Observed from root evidence: all-parts firstDataY was Workload527, Planner550 and Allocations612/636; the same census reports zero serious/critical/page errors. Source contained supporting blocks above the actual working rows. The405px threshold and census measurement are unchanged. The source rearrangement below is not yet a measured pass.

## Narrow changes

- `Workload.tsx`: primary person/week grid now precedes the full discipline context. The view-only Show proposed as confirmed checkbox moves into the existing labelled Filters popover/sheet; it keeps its label, URL/read/checkbox semantics. Includes proposed remains a visible role=status in the toolbar while active; changed cell markings remain. Material scope/error information is retained.
- `Planner.tsx`: the undated deadline tray and explanatory method text follow the working grid. Method text is in native labelled Team Planner · Details. Date/type/count/exact-record deadline header markers, numeric subtotals and band bars remain in place. Units/Zoom remain visible quick controls; under capacity_screens the three indicator chips use the existing Filters panel, with active URL tokens retained. Off preserves their original quick placement.
- `PlannerGrid.tsx`: optional `basisInDetails` defaultsfalse, used only by Planner with capacity_screens on. In that case the existing grid aria-description remains screen-reader accessible and its visible full text lives in the below-grid Details disclosure. MyWeekStrip/other callers retain the original visible basis because they do not pass the prop; MyWeekStrip itself was not edited. Off layout remains the original class/placement.
- `projects/Allocations.tsx`: actual allocation/open-role rows and pager now precede the full deadline strip and discipline comparison. Neither supporting dataset is dropped, collapsed by default or calculated differently. All existing links/actions/forms/exports remain.
- Owned capacity browser scripts now cover working-grid-before-context ordering, Filters access to the scenario checkbox, and opening the Planner details text. They retain numeric/unknown/permission/return assertions.

The earlier allocation script failure also had an invalid `has` inner locator that repeated its outer comparison region. It now matches a row-relative Civil rowheader and awaits the exact60h/16h/90h rendered text before the unchanged exact-array/no176 assertions. Root's log already showed those values in the DOM and no API/page errors. No production calculation change was justified by that log.

## Static checks and root rerun

PASS static `git diff --check`, `node --check` on capacity-allocation-screen.cjs / capacity-screen-workflows.cjs / capacity-planner-headers.cjs, and TypeScript syntax parse only on the three pages plus PlannerGrid.tsx. No typecheck/bundle/runtime/census claim.

Root should rebuild once, run all three capacity browser scripts, then the same17-route/all-parts real-API census for PM/Supervisor with the unchanged405 limit and serious/critical scan. Check actual primary allocation/person/week rows remain early, all context is reachable below, Filters retains its scenario/indicator controls, the Planner method disclosure opens, header markers still open/return to exact records, standalone My Work basis is retained, and capacity_screens off matches the earlier layout. Any remaining placement issue needs measured evidence rather than a changed limit.

S29 read-only schema/ownership/acceptance proposal is complete separately in `batch-4-S29-person-facts-proposal.md`; no S29 source/test work was started.
