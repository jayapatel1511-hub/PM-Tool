# Home charts/runway integration request

Batch: 053 S07 (partial until capacity S09 is integrated), 052 S03 Home chart semantics.
Source writer owns Workspace.cs and Home.tsx plus new home-visual helpers/tests. No schema or permission changes required.

## Coordinator changes requested

1. Regenerate the OpenAPI/TypeScript contract after the C# freeze. Workspace.cs adds optional omitted-off `HomeRead.runway`, containing `HomeRunway`, `HomeRunwayDay`, `HomeRunwayItem` DTOs. `tasksByProject` gains an On Hold lane and includes zero-task scoped projects only when home_overview is on. New default order is metrics, Projects, Status, Deadlines; saved widget order/visibility stays intact. No generated files were edited in the lane.
2. Add the following English strings in root-owned `web/src/i18n/en.ts` before the frontend compile:

```ts
'home.chartTotal': 'Total tasks',
'home.runway': 'Deadline runway',
'home.beforeToday': 'Before today',
'home.runwayDay': '{date}: {n} deadlines',
'home.runwayItem': '{project}, {type} {key}: {name}, due {date}',
'home.runwayMore': '{n} more deadlines on {date}',
'home.runwayLimited': 'Runway shows {from} – {to}; the list keeps the full selected range',
'home.nonWorkingScope': 'Non-working across the selected projects',
'home.runwayToday': 'Today',
'home.runwayOverdue': '{n} overdue tasks before today',
```

3. Run the named tests/checks once READY and after freezes. API changes require the normal contract snapshot refresh/fresh recheck. Existing `HomeOverviewTests` and `WorkspaceTests` are regressions. New API/browser test commands will be recorded in status.md.
4. The owned `index.css` adds the §56 faint `--nonworking-hatch` token. New Home charts use the existing click-time record-context origin helper without changing that shared helper. Please review the new API read-side union/aggregation under PostgreSQL during the focused tests; this lane deliberately did not run a competing compiler/server.

## Evidence boundaries

No existing calendar, board, capacity or count-card source is changed in other lanes. Exact day counts must come from the server runway, never from the capped Upcoming Deadlines payload. Capacity, trends, deadline notes, role presets and the full 17-screen visual census remain pending; this batch does not claim S07/052 S03 complete.
