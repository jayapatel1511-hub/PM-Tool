# Remaining052 completion audit — current source after checkpoint14

Read-only independent explorer audit, not runtime acceptance. All unchecked visual
slices remain in the full goal. Known Workload focus and Dashboard spacing repairs are
implemented but await rerun.

| Requirement | Concrete remaining source gap | Owning sources / next boundary |
|---|---|---|
| FR-VSN-15 / S06 | CountCard currently only Home. SummaryTile outside Home lacks v2 icon/metric/where/whole-card semantics. | Project Dashboard, Portfolio (Projects lane); Staff/Time (Capacity lane), shared CountCard root/Home integration. |
| FR-VSN-07/09 | Old hashed data-accent palette remains on project dots, avatars, Sections and SummaryTiles. Stored identity is not used by every project-bearing path. | index.css/common/people and project-bearing call sites; shared root plus explicit lane ownership. |
| FR-VSN-08 | Board identity/key rows lack task ItemMark although Calendar/Timeline use shapes. | Work lane Board.tsx; preserve exact record/link behavior. |
| FR-VSN-06 | Timeline overdue uses red striped hatch reserved by new semantics for away/on-hold; actual Planner/Vacation cells do not consistently use named time-away hatch. | Work Timeline; Capacity Planner grids/Vacation. Retain old off behavior and visible equivalent/legend. |
| FR-VSN-01/02/09/21 | v2 shell/tab/title CSS retains direct white/rgba literals; uppercase tracking remains on some enabled surfaces. | Shared visual CSS and Workload/Notifications/ProjectList call sites, root/lane loans. |

Next bounded presentation batch should pair Portfolio count-card migration with its
S20/S21 work, then Dashboard/Staff/Time in owned batches; project identity/neutral
person accent treatment and hatches need separate scoped integration. Rendered token
value equality does not prove named-token source usage or correct semantic meaning.
Do not expand the allowed palette simply to turn the census green.
