# This Week's Capacity — shared integration request

Generation 5 batch: FR-SCR-11 / AC-SCR-11, completing S07's widget visibility only after root verifies the combined result. No deadline-note or trend work in this batch.

## Planner drill-downs requiring coordinator/file-owner integration

Home will reuse `PlanningEndpoints.ReportGrid(new(From: current ISO week, Weeks: 1, IncludeMyDrafts: true, Sort: "name"))`. It will not filter by Home projects, tasks, statuses or deadlines. Its grouped rows are **home discipline**, not the planner's ordinary work-discipline filter.

Two narrow read-only planner filters are needed to make the widget's count/group links reproduce their exact cohorts without the existing 50-person `personIds` cap. Please coordinate these with the capacity-people owner; this lane will not edit Planning.cs or Planner.tsx:

1. Under `home_overview`, support `indicator=Above100` in the grid: retain people whose displayed current selected week has non-provisional Confirmed+Expected > Capacity (including P>0/A=0, as PLN-10 handles zero). This is an exact above-100 read filter, **not** the configurable canonical OverPlanned indicator (default 105%). Do not rename/change PLN-10 or its thresholds. Exclude unknown/provisional capacity from this exact count, matching Home's disclosure. The new Home link is `/planner?from=YYYY-MM-DD&weeks=1&indicator=Above100&includeMyDrafts=true&sort=name`.
2. Under `home_overview`, add a nullable `homeDisciplineId` grid query filter applied to authorized people before aggregation, with Guid.Empty meaning Not set. Pass it from Planner URL filters and show its active label distinctly from work discipline. Home's group links add it to the one-week planner URL. The existing `disciplineId` remains unchanged. Off ignores this new filter, preserving old behavior. Please ensure exports/grid view carry the filter, or explicitly scope the link to grid and label any unsupported alternate layout.

The literal “above 100%” requirement and its reference to PLN-10's configurable 105% are an existing wording seam. This implementation uses the literal count with a distinct read filter, preserving the canonical indicator. Please review this default in integration; no decision status change is claimed.

## Contracts/locales

Workspace.cs adds omitted-off `HomeRead.weeklyCapacity`, plus HomeCapacity/HomeCapacityTotals/HomeCapacityDiscipline read DTOs. No schema change. Generate after freeze. Root applied `002-home-capacity-locales.json`. Please additionally apply `003-home-capacity-copy.json`: eight replacements fix singular count grammar and keep every UI occurrence of Confirmed qualified by Confidence (AC-PLN-14).

## Truth boundaries

- Partial visible P/A percentage may be shown with the PLN-15 label; Free is unavailable, never spare capacity.
- Missing/provisional capacity makes gross/available/percentage/free unavailable and discloses people/weeks; no leave reasons or hidden work identity/hours are returned.
- Possible is separate; own drafts count as the canonical grid does; time away explains A, not a second subtraction.
- Existing saved weeklyCapacity preference will survive a flag/capability-off layout save without appearing in the off response.
