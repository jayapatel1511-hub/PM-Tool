# Verification: Company Structure

Local implementation and focused evidence are ready for integration. No human Accepted decision is asserted.

| AC ID | Local result | Evidence and remaining boundary |
|---|---|---|
| AC-UNT-01 | PASS source / partial browser | Server subtree headcount, null-root tree and real rendered Organisation fixture; full live three-level headcount case remains unproven. |
| AC-UNT-02 | PASS | Cycle/level-order refusal; closed parent cannot acquire open API/CSV children; prospective graph validation under shared lock. |
| AC-UNT-03 | PASS | Unit leader without Supervisor sees subtree Staff/My Work but cannot reassign. Explicit direct human read-scope authorization relayed by integration; writes unchanged. |
| AC-UNT-04 | PASS focused | Real Restricted project fixture created with existing PATCH path; its work absent from leader My Work. Existing broader totals matrix remains integration gate. |
| AC-UNT-05 | PASS domain/source | Indirect cycles do not grant scope; direct/reports/unit switcher and Admin-only affected reporting-cycle list; full toggled browser case unproven. |
| AC-UNT-06 | PASS reads / partial UI | Shared descendant filter used by Workload/Planner/Availability and CSV exports; group UI implemented; complex subtree subtotal/browser comparison unproven. |
| AC-UNT-07 | PASS | CSV validates unknown manager/cycle without mutation and corrected file applies all rows; successful preview retained in UI. |
| AC-UNT-08 | INTEGRATION REQUIRED | OrgStructureReports.At reads week-end placement from old/new activity; Staff Assignments uses it; integration owns Team Capacity caller and dedicated historical regression. |
| AC-UNT-09 | PASS focused / combined gate pending | Endpoint/default flag gates; new UnitId/LeadUnitId properties JSON-hidden; all added DTO/report/UI fields guarded; full combined parity remains integration gate. |

Scope is additive for reads: existing authorised people union indirect/led subtree. Availability retains its existing baseline. Shared server filters govern screen and export; placements reveal only people already available through scoped reads. No ActOnStaff, AssignTask, planning writes, approval or Restricted visibility permission is broadened. API placements expose versioned IDs/home units through a separate flagged endpoint; ordinary payloads remain unchanged.

Historical report contract: `OrgStructureReports.At(db, ids, exclusiveWeekEnd)` and `UnitPath` are the canonical integration seam. Team Capacity must call this helper, never current AppUser.UnitId for past weeks. CSV preserves blanks/provenance and never deletes. Atomic level reorder validates the final hierarchy in one transaction.

## Final local evidence (2026-10-18)

- PASS: focused domain/API/flag-metadata run, 57 passed, 0 failed/skipped. Full case list: `../048-verifiable-records/evidence/focused-tests.json`. PostgreSQL factories create/drop their own synthetic databases on localhost:55432.
- PASS: final API compilation through that test command. Compiler warnings and analyzers were suppressed for this focused run; this is not a clean analyzer claim.
- PASS: TypeScript app/node checks with incremental disabled; direct Vite build. Workspace dependencies are reused through a read-only node_modules symlink, so the ordinary incremental npm build could not write its cache; the explicit no-incremental checks and Vite build passed.
- PASS: actual Chromium with mocked API, 8 bounded workflows, 104 total API calls, four explicit My Work view/flag pairs, zero page errors, zero unknown GETs and zero axe violations in six scopes. `../048-verifiable-records/evidence/mocked-chromium.json`. This proves the exercised UI/requests against fixtures, not live integration or human acceptance.
- PASS: EF `has-pending-model-changes` reports none. Fresh disposable databases apply all migrations. Original recovered TemplateOffsetDayKind migration and Designer are byte-for-byte identical to the saved handoff (SHA256 f31c0544e0f4a9541096bbd95c17a9720e7d09c163b06ec6a7ac08f91f5d747d / d697cfbb6d1ac70cc71bcd198f04c1f40533e2c3f50e9d4cea8deebd273b7008).
- PASS: independent source review after fixes; `git diff --check`; combined-spec freshness and trace (1299 IDs, 405 sections, no gaps).
- UNPROVEN here: full combined Release/regression/coverage gates, existing populated schema upgrade/Down/previous-image compatibility, hosted transport, real identity, native/iOS, comprehensive phone/keyboard/screen-reader and Jay's acceptance. Integration owns the combined release checks. Flags remain dark in maintained settings; only disposable tests enable them. No operational DB migration, push, PR, deployment, pilot activation or Accepted status.

## My Work flag regression correction

Integration found the original My Work flag-off check used a paginated-list fixture for me/work rather than its real Work response. An error fallback could satisfy the missing-link assertion, so that earlier check did not prove the My Work action was gated. Both Overview and TaskView now gate only the pickup action with task_pickup and reuse the existing pickup title translation.

The corrected mocked Chromium run renders Overview and Upcoming Tasks with the real Work/tasks/planning fixtures, then asserts the main-content pickup link is absent with the flag off and visible exactly once with it on. The main scope excludes global navigation. The view switcher, saved Views control and New task remain visible in all four cases. PASS artifacts are written only after page-error, unknown-GET and axe checks pass. CHROME_EXECUTABLE_PATH is supported for an installed browser.

PASS: app/node TypeScript checks with incremental disabled, Vite build, corrected Chromium regression and independent source/test review. The replacement ../048-verifiable-records/evidence/mocked-chromium.json contains the explicit four-pair records. No backend/model/schema change is included in this follow-up; the separate database boundary limitation and combined acceptance gates remain open.
