# Verification: Verifiable Records

Integrated locally through runtime source `1b649f57f4f6c28abeb36fabdd9607b51ae13dde`. Final combined verification passes: 1,491 backend tests, coverage gates, 798 observed response shapes, six affected mocked browser commands, real-API browser/live flow, final-image permission checks and 29 old/new/old compatibility checks. See [the integration handoff](../../docs/handoffs/2026-10-18-039-050-integration.md) and its hashed evidence receipt for precise source boundaries and limits. Earlier lane tables below are historical slice-level records; old missing-schema blockers are resolved by the combined runtime checks. No acceptance criterion or packet is marked Accepted; remaining hosted, human and broader workflow gates stay explicit.

| AC ID | Local result | Evidence and remaining boundary |
|---|---|---|
| AC-VER-01 | PASS | 50 parallel edits; gap-free sequence and previous hashes |
| AC-VER-02 | PASS API | Operator tampering detected at exact sequence; email dedup; mocked integrity UI; full live UI remains unproven |
| AC-VER-03 | PASS | Failed canonical hash rolls back task change; immutable application guard and DB triggers |
| AC-VER-04 | PASS | Stored record still verifies after append; altered evidence fails; JSON record API exercised; CSV UI request/source reviewed |
| AC-VER-05 | PASS | 10,000 legacy rows sealed once, ordered, counted retroactively; runtime SELECT/INSERT role cannot initialize or UPDATE seals |
| AC-VER-06 | PASS | Half-up domain vectors, C=0 API null, Chromium Met 78 of 100 (78%) / Watch |
| AC-VER-07 | PASS API | Required active category and immutable event ID; category UI source reviewed, full form browser scenario unproven |
| AC-VER-08 | PASS | Performer cannot confirm; PM confirms; mocked browser confirmation body includes version/requestId |
| AC-VER-09 | PASS API / PARTIAL UI evidence | Real create/confirm preview rolls back, actual confirmation reuses same request; create/edit/confirm UI source reviewed; allocation-specific Chromium scenario unproven |
| AC-VER-10 | PASS contract / INTEGRATION REQUIRED | B1/B2, captured effort, clear preserves history, application/SQL immutability, delta export exercised; integration must attach Capture to reviewed 046 command |
| AC-VER-11 | PASS API/domain / PARTIAL UI evidence | Nonstandard issue saves, marks appear, clear removes; register/detail/filter/settings implemented; settings/issue browser scenario unproven |
| AC-VER-12 | PASS focused / combined gate pending | Dark endpoint/default metadata/source checks; existing full flag-off suites remain integration gate |

## Reviewed seams

`BaselineHistory.Capture` must run after 046 assigns BaselineStartDate/BaselineDueDate and before that command's final SaveChangesAsync, within its existing Tx.Run/project lock. Exact hook: `await BaselineHistory.Capture(db, d, body.Clear == true, Check.Reason(body.Reason), access.Me.Id, clock.GetUtcNow());`. Add TimeProvider injection; no duplicate route or transaction. Reviewed 046 source: 7553f954e82c98a36d998acbfde1c4edd7f88557. Delta export/report helpers and baseline UI are in this lane.

Legacy sealing is explicitly privileged, never a runtime UPDATE grant. See `audit-initialization.md`: migration-only mode validates the migration identity, starts all chains transactionally before hosting, and normal writes refuse uninitialized history. Prefix checks cannot promote unverified prefixes or advertise a full verified head; checkpoint mismatch fails at the saved checkpoint.

Jay directly resolved the preview scope question: "Keep both deferred; use existing capacity warnings". Overlapping Plans and Deliverable Pressure stay deferred; no false/zero substitute is emitted. Integration owns the narrow specification amendment (1867801); this is a resolved scope decision, not an open acceptance conflict.

The original recovered template migration is preserved; the sole new combined migration is `20261018232730_DeliveryWorkflowRecords`. It retains ck_person_schedule unchanged, one template OffsetDayKind and no unsupported per-role daykind fields. It extends the commitment state CHECK and history guard for independent Met confirmation while preserving immutable content and closed outcomes. Tier B approval remains a separate release gate.

## Final local evidence (2026-10-18)

- PASS: focused domain/API/flag-metadata run, 57 passed, 0 failed/skipped. Full case list: `../048-verifiable-records/evidence/focused-tests.json`. PostgreSQL factories create/drop their own synthetic databases on localhost:55432.
- PASS: final API compilation through that test command. Compiler warnings and analyzers were suppressed for this focused run; this is not a clean analyzer claim.
- PASS: TypeScript app/node checks with incremental disabled; direct Vite build. Workspace dependencies are reused through a read-only node_modules symlink, so the ordinary incremental npm build could not write its cache; the explicit no-incremental checks and Vite build passed.
- PASS: actual Chromium with mocked API, 8 bounded workflows, 104 total API calls, four explicit My Work view/flag pairs, zero page errors, zero unknown GETs and zero axe violations in six scopes. `../048-verifiable-records/evidence/mocked-chromium.json`. This proves the exercised UI/requests against fixtures, not live integration or human acceptance.
- PASS: EF `has-pending-model-changes` reports none. Fresh disposable databases apply all migrations. Original recovered TemplateOffsetDayKind migration and Designer are byte-for-byte identical to the saved handoff (SHA256 f31c0544e0f4a9541096bbd95c17a9720e7d09c163b06ec6a7ac08f91f5d747d / d697cfbb6d1ac70cc71bcd198f04c1f40533e2c3f50e9d4cea8deebd273b7008).
- PASS: independent source review after fixes; `git diff --check`; combined-spec freshness and trace (1299 IDs, 405 sections, no gaps).
- UNPROVEN here: full combined Release/regression/coverage gates, existing populated schema upgrade/Down/previous-image compatibility, hosted transport, real identity, native/iOS, comprehensive phone/keyboard/screen-reader and Jay's acceptance. Integration owns the combined release checks. Flags remain dark in maintained settings; only disposable tests enable them. No operational DB migration, push, PR, deployment, pilot activation or Accepted status.

## Reviewed final companion

Core local commit: `4f92c6ea6376b777d51ce7218bd3739f9132bff6`. PASS21/21 focused companion API tests, including final audit/preview/reliability regressions; evidence in `evidence/companion-tests.json`. The Activity Log report now renders the same full-head/pending/failed integrity status through its existing parameter metadata, shared by screen and export descriptions; prefix-only verification remains Pending. A focused API assertion checks that line and its flag-off omission. The privileged initializer records activation old/new inside the same transaction, once; runtime grants stay unchanged. Existing combined release gates remain open.

## Recovery-time boundary rerun

After the interrupted session, the local Docker daemon and PostgreSQL listener were unavailable. The latest two strengthened assertions (invalid hours and own-load suppression for a synthetic hidden-project assignment) compile but could not reach assertions: both test hosts fail with Connection refused at 127.0.0.1:55432. This is BLOCKED_ENVIRONMENT, not a new application failure or a passing result. `evidence/boundary-rerun.json` preserves actual runner outcomes and the reason. Re-run `FullyQualifiedName~Own_load_hint|FullyQualifiedName~Offer_leaves_task_fields` when the disposable test DB is restored. The earlier 57 core tests, 21 companion API checks and previous companion six-workflow Chromium run remain separately recorded passing evidence; the backend production source did not change after that companion API run, only these assertions were strengthened. No shared runtime was started or changed to bypass the missing environment.

## My Work flag regression correction

Integration found the original My Work flag-off check used a paginated-list fixture for me/work rather than its real Work response. An error fallback could satisfy the missing-link assertion, so that earlier check did not prove the My Work action was gated. Both Overview and TaskView now gate only the pickup action with task_pickup and reuse the existing pickup title translation.

The corrected mocked Chromium run renders Overview and Upcoming Tasks with the real Work/tasks/planning fixtures, then asserts the main-content pickup link is absent with the flag off and visible exactly once with it on. The main scope excludes global navigation. The view switcher, saved Views control and New task remain visible in all four cases. PASS artifacts are written only after page-error, unknown-GET and axe checks pass. CHROME_EXECUTABLE_PATH is supported for an installed browser.

PASS: app/node TypeScript checks with incremental disabled, Vite build, corrected Chromium regression and independent source/test review. The replacement evidence/mocked-chromium.json contains the explicit four-pair records. No backend/model/schema change is included in this follow-up; the separate database boundary limitation and combined acceptance gates remain open.
