# Generation14 S31 — ALL SOURCE READY, runtime UNRUN

FR/AC-SCR53 source is fully authored and held. No source edit or process is in flight. Backend details and eight normal-command API Facts are in `s31-backend-handoff.md`. Earlier completed capacity sources remain held; S29/S32 remain deferred. Root owns shared integration, review, generation and execution.

## Final maintained source

- `src/Hub.Api/Features/Allocations.cs`: narrow existing single-edit material helper extraction and the two guarded shift routes. Existing canonical edit permissions/validation/status/notification/date-version behavior retained by default; batch reuses that helper.
- New `src/Hub.Api/Features/AllocationShift.cs`: pure checked-week preview and atomic exact-version/cohort apply, preserving hours/person/purpose/work identity and translating overrides/active links. WholeProject spans every editable Proposed/Confirmed row; filled roles and task/review due dates stay unchanged. Standard Coordination.Run receipt returns the stable first ID.
- New `tests/Hub.Tests/Api/CapacityAllocationShiftTests.cs`: eight meaningful API Facts; all setup/transitions use normal commands, DB calls inspect only. Not compiled or executed here.
- `web/src/pages/projects/Allocations.tsx`: project-keyed row selection, explicit Selected/WholeProject menu and dialog. WholeProject action is PM-visible; canonical server permission checks remain authoritative. Terminal rows have no checkbox; selection may cross list pages. Success clears selection and invalidates the whole ResourceAllocation scope while preserving filters/paging. No version inference from the first-ID receipt. New controls/column remain capacity_screens-dark and retain DesktopOnly behavior.
- New `web/src/components/planner/AllocationShiftDialog.tsx`: explicit preview before apply, exact old/new dates and versions, whole-project scope disclosure, Confirmed→Proposed warning, unchanged filled-role range, checked nonzero integer weeks and required reason. Input/project/selection-keyed previews clear immediately when stale. Deterministic 400/403/404/409/422/428 refusal requires a fresh preview. Unknown-result retry retains exact request body/version cohort/requestId; a later preview/version change generates a fresh requestId. Canceled/unmounted preview responses cannot repopulate the dialog.
- New `web/tests/capacity-allocation-shift.cjs`: synthetic rendered selected +2 week/changed +3 week/whole -2 week checks; preview/reason gates, stale clearing, terminal selection, unchanged role and reconfirmation disclosure, keyboard region, cancel, 409 then fresh versions, lost-response exact-receipt retry with one move, complete-scope list refresh with URL preservation, off-page whole-project row, phone notice/containment and off parity. This script proves UI behavior only when root executes it; no real server/atomicity/privacy claim comes from its mock.

## Required root seams

1. Merge exact `s31-server-text.json` and `s31-ui-locales.json` into root-owned Text.cs/en.ts.
2. Regenerate OpenAPI/types from the coordinated source. Dialog uses generated AllocationShiftPreview and typed routes; no local shadow contract was introduced.
3. Review any required registry/route-inventory additions for the new canonical endpoints. No schema/permission/receipt changes are requested.

## Static evidence actually run

- `node --check web/tests/capacity-allocation-shift.cjs` — PASS.
- `git diff --check` scoped to the six maintained S31 files — PASS for tracked diff; new-file syntax was separately inspected.
- TypeScript `createSourceFile(...).parseDiagnostics` for Allocations.tsx and AllocationShiftDialog.tsx — PASS syntax only. This is not a typecheck, generation, compilation or runtime test.

No .NET test/build, TS typecheck, Vite build, browser run, migration, generator, helper, goal, commit, push, deployment or flag activation ran in this lane.

## Coordinator verification to run after freeze/shared integration

- Eight CapacityAllocationShiftTests Facts against disposable normal API fixture; adjacent existing single-edit/confirmation/date-version/idempotency and S30 tests, according to root risk review.
- Normal API/generated contracts/typecheck/build checks, including root-owned new endpoint metadata/inventory.
- `TUESDAY_UI_BASE_URL=<root local built preview> CHROME_EXECUTABLE_PATH=<root Chrome> node web/tests/capacity-allocation-shift.cjs` plus the existing allocation screen/planner/capacity regressions as warranted by the shared extraction and added column/action.
- Independent source review of atomic rollback, canonical confirmation/date invalidation, exact whole-project cohort, filled-role behavior, permission scopes and unknown-result receipt retry. Actual simultaneous races and Complete-project correction remain adjacent risks to verify proportionately; authoring is not acceptance.

Root alone records PASS/FAIL/UNPROVEN and AC/task/packet status. No slice or packet has been marked Accepted here.
