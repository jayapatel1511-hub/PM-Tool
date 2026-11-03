# Checkpoint15 visual and reflow evidence

Local isolated synthetic API only; no hosted or human acceptance. The actual browser
measurements and hashes are in the sibling JSON files. No threshold or measurement
was relaxed. Default flags and operational/review databases were not changed.

## Screen positions and accessibility

Each real census contains 68 observations:17 screens × PM / Supervisor × all 8 amendment
flags off / on. The first run exposed supporting capacity context before working rows.
The layout repair moved that context below the primary data and kept its controls/facts.

| Enabled screen | First run PM / Supervisor | Verified PM / Supervisor | Limit |
|---|---:|---:|---:|
| Today |285 /285|285 /285|405|
| Workload |527 /527|370 /370|405|
| Planner |550 /550|402 /402|405|
| Project allocations |612 /636|391 /391|405|

`checkpoint15-visual-census-verified.json` has 34 enabled observations, all within405 px.
Across all 68 observations there are0 serious/critical axe findings,0 page errors and
0 off-origin fonts. The overall runner exits1 for 2 preserved legacy-off MyWork observations
at 558 px. These are recorded failures, not suppressed or a full §41/052 S09 pass. The
intermediate `checkpoint15-visual-census.json` and `checkpoint15-visual-census-final.json`
retain the actual failed measurements before the repairs.

## Reflow and text spacing

`checkpoint15-reflow-census.json`:68/68 observations pass across the same 17 routes at
1600 px desktop,375 px phone,800 px/DPR2 reflow equivalent, and that equivalent with
injected text spacing. There is0 document overflow,0 visible text below 11 px,0 clipped
controls,0 serious/critical violations,0 page errors,0 external fonts and0 flag-restore
failures. The Workload scroll-region focus and Dashboard task-name spacing regressions
from checkpoint14 are resolved.

800 px/DPR2 is explicitly a reflow equivalent, not a claim of native browser200%zoom or
human inspection. Rendered values matching token values do not prove named-token source
usage or correct colour meaning. Full semantic/source/contrast/chrome acceptance remains
open for 052 S02/S03/S06/S07/S08/S09 and the unimplemented053 slices.

## Print

The final synthetic Portfolio PDF is retained in053/evidence. Actual Chrome verifies
72 projects, all data columns, exact5 decision/2 project context, full status and health
notes, repeated headers, server time, landscape pages, nonempty Range containment and
portrait output after SPA navigation to Projects. Root rendered/inspected first, middle,
last and health-note pages. Notes retain their full text; long health explanations use
a full-width detail row. Earlier blank-last-page and crossed-column text were repaired.
This is local synthetic print evidence, not human acceptance of operational records.

The screen census predates the final print-only wrapping/note-layout refinements; no
screen layout rule changed afterward. The final reflow ran on the print-wrap build.
Final print-only note-row JSX was rebuilt/typechecked and checked in actual Chrome.
Temporary QA API5088 and only hub_qa_checkpoint15 were stopped/dropped after measurement.
