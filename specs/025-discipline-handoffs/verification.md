# Verification: Discipline Handoffs and Acceptance

**Date**: 2026-09-27
**State**: Foundation implementation written; verification in progress. Not deployed or pilot accepted.

Implemented the handoff workflow, immutable submission/source snapshots, receiver evidence, owner reassignment, transactional command receipts, project UI, search, filters, saved views, CSV/XLSX export, audit history and scoped notifications/digests. Generated the additive `DisciplineHandoffs` EF migration; existing projects start the handoff sequence at 1.

Executed locally on .NET SDK 10.0.401 / runtime 10.0.12 and Node 24.19.0:

- Backend solution compilation passed (existing test-analyser warnings remain).
- Domain test run passed: 193 tests, zero failures, zero skipped. This includes the handoff transition/permission tests and the pre-existing domain suite.
- `npm run build` and `npm run lint` passed. Existing bundle-size, CSS selector and lint warnings remain.
- EF migration generation passed; `migrations has-pending-model-changes` reports no model drift. PostgreSQL migration execution is confirmed by the first CI test run; API integration validation is in progress.

The local environment has no usable PostgreSQL service and cannot switch operating-system users. The repository's CI PostgreSQL service is the intended integration validation environment for this branch. API tests cover AC-HND-01/02/04/05, duplicate requests, stale versions, immutable evidence, lifecycle/access refusals and scoped email suppression. Their existence is not a passing result.

AC-HND-03 remains deferred until packet 027 supplies explicit revision supersession and impact assessments. A changed source-record warning and preserved old snapshot do not satisfy that scenario. Full browser/accessibility, concurrency load, performance, operational hardening and pilot acceptance remain pending until their actual results are added. No deployment or production-readiness claim is made.

The earlier documentation-only validation in `docs/coordination-spec-validation.md` is historical and does not validate this runtime change.

First CI run (36287819927) migrated PostgreSQL and ran 360 tests: 358 passed, two failed because the new test fixture incorrectly supplied visibility to the create endpoint. The fixture now explicitly sets and asserts restricted visibility; the next CI run must validate that repair. No permission assertion was weakened.

A pinned Playwright workflow test now runs in CI (`npm run test:handoffs`) using mocked API responses. It exercises form prefills, create/submit/accept/incorporate, command IDs, JavaScript errors and WCAG 2.1 A/AA axe checks on the two dialogs. Local Chromium downloads returned truncated archives, so no local browser result is claimed. Full browser-to-real-API testing and manual keyboard/screen-reader review remain outstanding.

Second CI run (36288410995): 359/360 tests passed. Restricted query/export/search and revoked-access checks passed. The email assertion exposed a shared-fixture batch issue: over 100 earlier messages could precede the fixture, so one worker run had not processed it. The fixture now sorts first; the worker suppression assertion remains unchanged.
