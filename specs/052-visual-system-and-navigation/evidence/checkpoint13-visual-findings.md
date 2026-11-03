# Checkpoint 13 — real 17-screen census findings

Scope: final checkpoint-13 compiled API and production UI, a new disposable local QA database,
17 routes × PM/Supervisor × visual/home flags off/on = 68 observations. No source builds
ran during generation-5 lane edits. New invented SYN-101..103 records only. Axe used DevTools
evaluation so the real application CSP remained unchanged. The API and QA database were
stopped/dropped after the run. Raw evidence: checkpoint13-visual-census.json.

## Verified facts

- Flag off: zero serious/critical axe violations, zero measured first-row positions above 405 px.
- Flag on: color-contrast violations on Home, Boards, Tasks, Calendar and Team for both roles.
- No uncaught page errors and no font request to another origin in either mode.
- My Work has no assigned work for the two census personas: its first-data measure is UNPROVEN,
  not PASS. Extend the fixture with assigned PM/Supervisor work before final acceptance.

## Contrast defects — flag on only

- /home `['.md\\:inline-flex']`: Fix any of the following:   Element has insufficient color contrast of 2.71 (foreground color: #646d81, background color: #1b2a4e, font size: 10.5pt (14px), font weight: normal). Expected contrast ratio of 4.5:1
- /boards `['.min-h-\\[calc\\(var\\(--control-row-h\\)-4px\\)\\].rounded-\\[4px\\][href$="tasks"]']`: Fix any of the following:   Element has insufficient color contrast of 1.09 (foreground color: #ffffff, background color: #f3f4f7, font size: 10.5pt (14px), font weight: normal). Expected contrast ratio of 4.5:1
- /boards `['.shadow-\\[0_1px_2px_rgba\\(25\\,27\\,32\\,0\\.12\\)\\]']`: Fix any of the following:   Element has insufficient color contrast of 1.09 (foreground color: #ffffff, background color: #f3f4f7, font size: 10.5pt (14px), font weight: normal). Expected contrast ratio of 4.5:1
- /tasks `['.min-h-\\[calc\\(var\\(--control-row-h\\)-4px\\)\\].rounded-\\[4px\\][href$="boards"]']`: Fix any of the following:   Element has insufficient color contrast of 1.09 (foreground color: #ffffff, background color: #f3f4f7, font size: 10.5pt (14px), font weight: normal). Expected contrast ratio of 4.5:1

## First data position — flag on only

Both roles have the same measured position:

- /portfolio: 522 px (maximum 405).
- /workload: 422 px (maximum 405).
- /planner: 454 px (maximum 405).
- /projects/SYN-101/tasks: 425 px (maximum 405).
- /projects/SYN-101/deliverables: 423 px (maximum 405).
- /projects/SYN-101/allocations: 423 px (maximum 405).

## Other measurements need interpretation before a verdict

Raw chrome/control/fill/tab/sidebar measurements are retained. In particular, Home’s
new count cards currently do not match the older census data selector; do not remove
metric facts or weaken their links just to reduce an incorrectly classified chrome count.
The proposed one-list sidebar has 9 entries and project tab rows have 8 links. Compare
these with the explicit §56 proposed navigation and still-open 052 decisions before
treating the old 8-item / 7-tab limits as permission to hide required destinations.
The old six-fill budget versus the proposed token census also remains an open decision.

This run is an actionable baseline, not S09 or whole-packet acceptance. Full token census,
rendered contrast coverage, zoom/text-spacing and final changed-state scans remain required.
