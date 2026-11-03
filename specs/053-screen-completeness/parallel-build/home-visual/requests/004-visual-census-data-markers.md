# Existing census classification correction — root-owned shared source

Bounded UI/CSS-only S02/S09 window while C# remains frozen6. This is not a new census framework or permission to change an acceptance limit.

`web/tests/clutter.cjs` `inData` recognizes tables/grid/dialog and native article/list items. Its `dataSel` likewise recognizes table rows, role rows/gridcells and articles. `SummaryTile` returns a button, link or div, not an article. Portfolio's six real metric tiles therefore count as chrome and are skipped by the first-data measure, which reports the later table at522px. Source: Portfolio.tsx's first content child is its six real SummaryTiles; common.tsx's SummaryTile return branches have no data marker. No measured metric-card Y is claimed from source inspection.

Please apply this narrow shared correction:

1. In root-owned `common.tsx`, mark SummaryTile's outer button/link/div with `data-count-card` while visual_system is on (e.g. existing Ctx flag hook inside SummaryTile). This keeps off markup/classification unchanged. Preserve all elements, labels, hints and interactions; do not put role=article on an interactive button/link.
2. In root-owned existing census, treat `[data-count-card]` as data in both `inData` and `dataSel`, exactly as the existing comment says real dashboard data cards should be treated. Root's real census imports this function. The lane adds the same marker to owned CountCard's existing link/article, so the Home cards gain correct classification without removing a figure.
3. Rerun changed-state measurements. Real Portfolio first data is its card row, not whichever table happens to follow it. If a data card still begins below405px, retain that real failure and use actual geometry before further compaction.

The lane's shared v2 desktop spacing pass independently reduces excessive band/title/tab/content spacing for Planner/Workload and nested project registers, and fixes the two measured contrast causes. No nav destination or metric fact is hidden to satisfy a legacy budget; pending nav/colour decisions stay pending.
