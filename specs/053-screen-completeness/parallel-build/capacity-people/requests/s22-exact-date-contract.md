# S22 exact-date confirmed-reservation adapter contract

Final integration names retain the FIRST published ConfirmedByDiscipline / FromDate / ThroughDate contract already wired by C, generation11. Root requested the final minimal six-field ExactDateReservationDiscipline record; no exposed UnattributedAllocations count is added. An interim ConfirmedInWindow draft name was removed before build; there is one implementation and no alias. The callable is public solely for focused adapter integration tests; it adds no HTTP route. Method/records below are the exact C/root integration target in `Hub.Api.Features.AllocationScreenReads`. No route, owning-flag check, actor/scope widening, staffing/excess formula or open-role command is added. Dashboard owns its project_screens guard and target window.

```csharp
public sealed record ExactDateReservationDiscipline(
    Guid? ProjectDisciplineId, string? DisciplineName, bool Unattributed,
    decimal? ConfirmedHours, decimal KnownConfirmedHours, int UnavailableConfirmed);
public sealed record ExactDateReservations(
    DateOnly FromDate, DateOnly ThroughDate, string AllocationCoverage,
    ExactDateReservationDiscipline[] Disciplines, DateTimeOffset AsOf);

public static Task<ExactDateReservations> ConfirmedByDiscipline(
    Guid projectId, DateOnly from, DateOnly through,
    IQueryable<ResourceAllocation> visibleAllocations,
    bool completeAllocations, HubDb db, TimeProvider clock);
```

Caller obtains project access and passes the **existing authorized allocation query**, plus its established completeness basis. For Dashboard, use the current AllocationEndpoints.VisibleQuery + managed-project PM/Admin basis, not all db.Allocations for every project reader. The adapter additionally narrows the supplied query to that project, Confirmed status only and allocation ranges overlapping the exact **inclusive** dates. It never expands it or inspects excluded records to infer hidden cardinalities. Inverted dates are rejected as validation, not clamped/swapped. Caller owns the consistent-read transaction/snapshot; the adapter neither begins nor commits a transaction.

`AllocationCoverage` is exactly Complete or VisibleOnly, from completeAllocations. KnownConfirmedHours is an unrounded decimal subtotal of selected visible confirmed reservations in each bucket. ConfirmedHours is null for VisibleOnly coverage or a calendar failure in that bucket; otherwise it is that bucket's known total. This is a reservation total for the named bucket, **not assurance that visible Unattributed hours belong elsewhere**. C must prevent definitive per-discipline staffing/excess claims whenever nonzero/unknown Unattributed reservations exist, in addition to its other completeness checks. Empty full-coverage known buckets are zero; empty VisibleOnly buckets remain null with known0.

All active project disciplines appear, plus any inactive discipline with selected attributed records, using S30's stable name/ID order. Unattributed is a separate final row (null ID/name, Unattributed=true), preserving its known hours/visible unavailable count; no guessed split or duplicate attribution. No allocation/person/other-project IDs/names or hidden record counts are returned; only current project discipline identity/name is exposed. A usable full spread with unresolved attribution can clip to genuine zero. Such a known-zero Unattributed row alone does not prevent an exact comparison, per root clarification. UnavailableConfirmed counts only selected visible records whose **full canonical spread** cannot be computed; none is silently replaced with zero or dropped during clipping.

Implementation factors the reviewed current-valid work/purpose/person/filled-role attribution and person-calendar/day-override spread **once**, shared by existing S30 Compare and this adapter. Full spread is calculated over each allocation's original inclusive range before clipping daily values to fromDate≤date≤throughDate. No weekly sum/proration, linked-task committed-load maximum, task remaining, Proposed, Planner entries, capacity or open-role hours enter the new result.

S30 public JSON, coverage semantics, weekly boundary/rounding, Proposed/Confirmed separation, task due-week logic and current/stale attribution behaviour remain unchanged. Its existing ten-Fact regression class is an adjacent check for root. New exact-date tests are isolated in `CapacityExactDateReservationTests.cs`; no tests/builds/generation run in this lane.

Open roles stay C-owned composition: OpenRoleEndpoints.Calendars/Spread already provide the canonical project-office full spread. A positive role whose **full** spread is empty must be unavailable; a usable full spread with no selected-window hours is a genuine zero. No OpenRoles extraction/change is currently necessary.
