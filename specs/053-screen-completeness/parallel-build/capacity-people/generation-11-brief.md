# Generation11 — exact-date allocation discipline read for S22

Implement only the shared read dependency in Projects requests/s22-next-batch-proposal.md,
not S29 schema/people UI yet. Own AllocationScreenReads.cs (or a narrow new Infrastructure
helper), optional narrowly necessary pure extraction from OpenRoles.cs spread reader,
and a new uniquely named exact-date API/domain test file. All commands, existing S30
contracts, Allocations/AllocationPreview/Planning rules and Capacity UI/tests remain held.
No entity/migration/settings/permission changes.

FIRST save a precise method/result contract in own requests/s22-exact-date-contract.md
for C/root. Accept current authorized scope/coverage and exact inclusive dates. Reuse
one canonical daily spread/calendar/overrides and current-valid discipline attribution
implementation; expose exact-date confirmed reservations/known subtotals/unknown and
Unattributed, independent of the capacity_screens HTTP guard. Never copy/spread weekly
results, include Proposed/planning entries as confirmed, widen allocation read access,
or silently clip/lose calendar failures. Shared public DTO shape is an implementation
choice, but C must have the exact method/records before wiring.

Preserve S30 byte/calculation behavior and its existing current-review/purpose/person/
role attribution regressions. Prepare exact partial-week/day-override/calendar, decimal,
current-vs-stale review/role, hidden/visible-only/Unattributed and read-no-write cases.
120remaining/80confirmed/24role/40excess composition stays C's Dashboard test boundary;
this helper returns exact reservation facts, not a new staffing formula.

All shared/root requests through own notes; no builds/tests/browser/generation/helpers/
goals. Mark READY and hold. S29 proposal remains saved and deferred for a later root
schema window.
