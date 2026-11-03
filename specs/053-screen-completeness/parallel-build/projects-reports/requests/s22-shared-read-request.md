# S22 minimal exact-date confirmed reservation contract

Generation11 first publication, before Dashboard staffing wiring. Capacity owns the implementation and final record/method names; C will use its saved `capacity-people/requests/s22-exact-date-contract.md` after agreement through root/files. No copy of its calendar/attribution rules, weekly sums, person-count conversion or placeholder zero is authorized.

Requested callable semantic contract, independent of capacity_screens:

```csharp
internal static Task<ExactDateReservations> ConfirmedInWindow(
    Guid projectId, DateOnly from, DateOnly through,
    IQueryable<ResourceAllocation> visibleAllocations, bool completeAllocations,
    HubDb db, TimeProvider clock);

public sealed record ExactDateReservationDiscipline(
    Guid? ProjectDisciplineId, string? DisciplineName, bool Unattributed,
    decimal? ConfirmedHours, decimal KnownConfirmedHours,
    int UnavailableConfirmed, int UnattributedAllocations);
public sealed record ExactDateReservations(
    DateOnly From, DateOnly Through, string AllocationCoverage,
    ExactDateReservationDiscipline[] Disciplines, DateTimeOffset AsOf);
```

Names may follow Capacity's precise saved contract; fields must retain equivalent distinctions. The caller provides its existing authorized same-project `AllocationEndpoints.VisibleQuery`, managed-project basis, and completeness verdict; the helper must not widen access or infer private cardinalities. An exact attributed/bucket total is nullable when its calendar/spread is unavailable or its allocation coverage is only visible. KnownConfirmedHours is the exact visible usable subtotal, including a real known zero; it never claims full project coverage. UnavailableConfirmed counts only visible unusable confirmed records. UnattributedAllocations counts only visible reservations lacking one valid current discipline attribution.

Only Confirmed reservations enter these facts. Use the single canonical full person-calendar/day-override spread and the S30 reviewed current task/review/purpose/person/filled-role attribution, clipping exact inclusive dates before summing decimals. Proposed, planning entries, linked remaining forecasts and committed-load max arithmetic are excluded. No command/schema/confirmation/S30 public shape or byte/calculation change.

Include explicit Unattributed, never duplicate/split reservation hours. C can then suppress an asserted complete per-discipline/excess comparison whenever an Unattributed bucket exists, retaining all known visible subtotals. Include active project disciplines with exact zero only when coverage is actually complete and no relevant unavailable input exists. No target/inverted range is handled by the Dashboard caller as unavailable and does not call this method. Snapshot ownership stays with the caller; do not start an independent transaction or depend on a feature-gated HTTP route.

C composes remaining estimates and separate open-role hours, handles package/milestone selection, and tests the 120/80/24/40 acceptance example. Capacity tests exact-date spread/overrides/calendar/attribution/privacy/pure-read boundaries. Shared locales/contracts/metadata, builds/tests and integration remain root-owned. This note is a request, not an implemented method or authorization to edit Capacity files from C.
