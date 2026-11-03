# S30 allocation preview/comparison — early integration notes

Scope: FR/AC-SCR-51/52 plus root's keyboard-scroll repair of Workload discipline context. No schema migration is presently needed; defaults/flags/confirmation remain unchanged.

## Allocation preview reader ownership / additive contract

The existing §52 preview is `Features/AllocationPreview.cs` (`AllocationPreviewFilter`), not an Infrastructure adapter. It returns only after-change capacity/planned/free/band. The canonical `AllocationEndpoints.Capacity` already computes each day's **ConfirmedHours (before)** and **ResultingHours (after)**; S30 should reuse that calculation rather than create new load math. Please confirm this existing feature reader is loaned to Capacity for the bounded additive preview contract, or root can own the small glue change. It is not named in the original maintained-file ownership list.

Proposed additive implementation: a capacity_screens-only weekly comparison DTO with available/before/after/over flags, current planner people-scope enforcement, partial/provisional unavailable states, and exact input dates. Where allocation_preview is on, a valid Propose/Fill form uses the existing rolled-back §52 command preview with this additive comparison. While inputs are incomplete, or §52 is off, a narrow read-only capacity preview in Allocations.cs can show authorized weekly baseline/availability and assess after only when the same canonical proposal validation succeeds. Unsaved day/work-link inputs must feed the existing Capacity routine without DB writes. Canonical save, confirmation, rowVersion and daily over-capacity rules remain unchanged. New requests clear stale UI figures immediately.

## Attribution seam in FR-SCR-52 (no silent hour splitting)

ResourceAllocation has **no stored discipline field**, and current validation permits multiple task/review links across disciplines. The Fill role may name a discipline, but can disagree with its work links. A proposed safe local read is:

- Attribute reservation hours only when all current valid same-project task/review/open-role links resolve to one project discipline.
- Put mixed, missing, foreign or inconsistent links in a separate **Unattributed allocations** group with the known confirmed/proposed hours. Never assign an arbitrary discipline, duplicate the full reservation into multiple disciplines, or invent an hour split. Unknown calendar spreads stay unavailable.
- Reuse the existing allocation list visibility query, independent of table paging. PM/Admin totals can be complete; other viewers' figures are explicitly visible/partial allocation hours, never an asserted project-wide zero. Task remaining estimates use the existing Workload.Remaining function and are placed by due week as FR-SCR-52 explicitly requests; unestimated/no-date work is disclosed and never treated as zero. On Hold remains excluded in the same active-work basis as canonical capacity reads, stated in copy.
- Confirmed allocations, Proposed allocations and remaining task estimates stay three separate measures, never summed. The 60/16/90 acceptance fixture uses unambiguous Civil-linked records.

Please resolve the read-only attribution default with root before final integration; no decision status is changed here. This does not require adding a stored discipline or changing allocation commands.

Root owns shared locale/API/registry generation. Exact locale JSON and meaningful API/browser cases follow with implementation. No lane build/test/generator is run.

## Coordinator resolution and implementation follow-up

Root explicitly loaned AllocationPreview.cs for this S30 additive create/Fill comparison and approved the Unattributed read rule. Root subsequently corrected FR52 open-task context: **On Hold is included**; only Complete/Cancelled are terminal. The Compare due-week query now follows that correction, with a held-task check (90→110 without changing 60/16). Canonical proposal/confirmation load retains its existing On Hold exclusion. Root owns the corresponding comparison copy adjustment.

The additive preview strings are in **batch-3-preview-locales.json** (do not overwrite comparison keys already applied). New contracts to generate from maintained C#: ProposalCapacityBody/ProposalCapacityWeek/ProposalCapacityRead, DisciplineComparison/ComparisonDiscipline/ComparisonWeek, additive AllocationPreviewResponse.comparison and POST allocations/capacity-preview + GET allocations/discipline-context. No schema/registry additions.

Provisional weeks have availableHours/overCapacity null; hidden-project work suppresses all before/after/overload values with Partial state and no hidden metadata. Unestimated/no-date active work yields Incomplete state, known subtotals and a nullable daily no-overload assessment. Outside planner scope returns no personId or weeks and no UI section. Existing §52 create preview returns comparison while retaining its always-rollback transaction; its legacy weeks are suppressed for non-complete new S30 states so raw payloads cannot undermine those boundaries. capacity_screens off omits comparison and preserves original preview JSON. Edit/confirm previews retain the existing behavior.

The pure draft read also checks the existing active person/work/day unique-index invariant after canonical Validate, so it cannot display a duplicate work reservation that the normal create command would reject at save. This is confined to the new read and does not alter commands or load arithmetic. The exact-date/day-overlay test covers that refusal with durable-state equality.

Runtime commands for root: `dotnet test tests/Hub.Tests/Hub.Tests.csproj --filter FullyQualifiedName~CapacityAllocationScreenTests` (8 Facts); adjacent `AllocationApiTests`, `VerifiableWorkflowTests`, `CapacityPlannerHeaderTests`, `CapacityScreenTests` as appropriate; after root generation/frontend checks, `TUESDAY_UI_BASE_URL=<built-local-preview> node web/tests/capacity-allocation-screen.cjs`, existing capacity header/workflow scripts, and the real-API reflow census retaining the scrollable-region-focusable rule. No runtime/build/generator has run in this lane.

## Independent source review P2 repair handoff

READY; all source held. Root authorized only AllocationScreenReads.cs / CapacityAllocationScreenTests.cs and own notes. Attribution now matches existing current-round/nonterminal assignment/package eligibility, existing task eligibility, and purpose/work-type/review-hours/person consistency. Historical/ineligible/missing/foreign/conflicting evidence stays Unattributed with known reservation hours retained. No command, formula, schema or UI changes. Open due-week estimates continue to include held tasks.

Two additional Facts bring CapacityAllocationScreenTests to **10**: a normal API superseded-round regression (Electrical12→Unattributed12 while reservation/link remain, canonical new proposal rejects old assignment) and a legacy evidence + normal role-conflict matrix (Civil4+Unattributed26=known30, then task-ineligible control→Unattributed30). Both assert pure read durable state and preservation. Run the same class filter; no builds/tests/gens/browser were executed in the lane.
