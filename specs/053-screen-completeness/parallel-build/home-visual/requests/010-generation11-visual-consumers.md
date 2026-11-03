# Remaining consumers after the shared generation11 foundation

Shared source is prepared, not runtime accepted. No page/backend/DTO migration was attempted in this batch. All later loans need explicit ownership and root integration; these requests do not open them.

## Stable roles for later owned consumers

- `semantic-colours.ts` exports `LANE_FILLS`, `laneFill(lane)`, `TIME_AWAY_FILL`, `NONWORKING_FILL`; laneClass is unchanged. Input is the server's canonical lane, not an inferred task/deliverable status. Unknown laneFill is undefined and the generic semantic-lane border is neutral ink2. Known classes use the exact four full-strength lane colours; On Hold uses the named away hatch.
- `semantic-colours.css` has `semantic-time-away` and `semantic-nonworking-day` for recorded away/on-hold facts and verified non-working calendar days. Each consumer must keep numeric text/accessible equivalents and a named legend. These classes do not infer availability from zero hours, dates or missing data.
- `index.css` declares on-navy text/secondary/muted/boundary/hover/active/focus roles derived only from existing card/brand-tint tokens. Shared band/tab/reference CSS now uses these roles; do not reintroduce raw white/rgba/focus hues in later surface work.
- Person avatars and common Section headers carry explicit decorative markers and become neutral only in v2. No blanket data-accent override was added. Remaining SummaryTiles/category/source accents still need their own reviewed migration; their colours have not been erased by this foundation.
- `identityColourToken(colour)` maps the existing five canonical stored values to named project tokens. IdentityDot's valid stored data attribute stays the canonical value; enabled missing/invalid identity becomes a neutral line3 ring with data-identity-state=missing and no hash. Off still returns the previous AccentDot. ItemMark's validated wrapper uses the project token; missing/invalid project colour gives the required ink2 shape. The pure shape helper imports its own CSS and remains10px.

## Work loan, later

Board.tsx: use neutral Task circle by the key; stored project swatch requires the TaskQueries.Row identity field first. Keep reviewer facts, record links and exact open/return.

projects/Timeline.tsx and Gantt.tsx: replace the enabled overdue striped extension and its screen/print legend with an explicit critical/tint treatment; retain legacy HATCH for off behavior. Do not replace an overdue stripe with TIME_AWAY_FILL. Migrate task-bar lane semantics from tone(status) only with the canonical bucket mapping; leave pill/deliverable semantics distinct. Calendar.tsx's timed-event hash stripe remains for a separate stored-identity read bridge.

## Capacity loan, later

PlannerGrid.tsx / PlannerMonthGrid.tsx / ProjectPlanningGrid.tsx / PlanningBandBar.tsx, Planner.tsx and Vacation.tsx: consume time-away roles only from existing positive timeAway/leave facts, preserve exact capacity math/partial/provisional truth and numeric labels. Review source-category accents and confidence roles explicitly; no arbitrary source→metric-hue mapping. The legacy `.planner-draft` stripe in index.css328 still needs a separately coordinated v2 dashed/outline, labelled private-draft treatment; the shared foundation intentionally did not change that page-specific meaning.

Time.cs/Time.tsx still lack verified per-day away/non-working facts. Zero entry hours are not leave. Staff assignment identity comes from Work-owned MyWork.cs, so a DTO loan and Capacity UI changes must not overlap that writer. Workload/Notifications uppercase group-header styles are page-owned and remain untouched.

## Root / Projects / Home follow-ons

ProjectList/Portfolio already receive stored identity in ProjectRow but still use legacy AccentDots/stripes in some rows; Projects owns those call sites. Dashboard's CountCard migration is already assigned separately to Projects; do not replace its concurrent work or convert all SummaryTile callers globally. Staff and Time card migrations remain Capacity-owned. `Shell.tsx`'s legacy mint brand-mark accent was outside this exact loan and remains a separate source consumer.

Workspace.cs needs flagged nullable identity projection for Home deadline, Team project and Files project rows before those UIs can show it. Remaining TaskQueries, Time, Workload, Notifications/Preferences and MyStaff identity bridges are listed in next-052-semantic-batch.md. Root owns the metadata/omission policy and generation. No new schema migration is implied by adding existing stored values to current permitted reads.

§41 budget/sidebar decisions stay open, flag defaults stay off, and S10 remains conditional on pilot authorization. Shared roles plus a passing component harness do not close S03/S06/S09 or prove all enabled source semantics/census/zoom behavior.
