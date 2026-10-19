# Verification: Task Pickup

Local implementation and focused evidence are ready for integration. No human Accepted decision is asserted.

| AC ID | Local result | Evidence and remaining boundary |
|---|---|---|
| AC-PKP-01 | PASS API / partial UI | Offer preserves task fields; task marker present only flag-on; recipients/catalogue reviewed. Exhaustive notification recipient matrix remains unproven. |
| AC-PKP-02 | PASS | Same-team pickup reassigns, watches prior assignee and closes once; task old/new audit and normal team/authorship safeguards reused. |
| AC-PKP-03 | PASS | Outside-team and ReadOnly mutation rejected; org unit matching overrides manager fallback. |
| AC-PKP-04 | PASS | Two simultaneous pickers: one winner and one conflict. |
| AC-PKP-05 | PASS | Completion, reassignment, project Complete/Archived auto-close; deleted-task history remains in report. |
| AC-PKP-06 | PARTIAL | Visible-project filtering is shared by list/report and source reviewed; dedicated Restricted pickup API matrix remains a combined acceptance check. |
| AC-PKP-07 | PASS | Manager target constrained to same team; roleless direct-manager cannot offer/withdraw/assign; normal write permissions unchanged. |
| AC-PKP-08 | PASS focused / combined gate pending | Default route dark and marker omitted; existing full flag-off suites remain integration gate. |

UI: real Chromium pick-up flow and versioned POST pass with mocked API. TaskPanel offering, withdrawing and assigning are wired to server capabilities. The companion completes the own load-band hint beside each pick-up action for its needed-by week (current week when no needed-by date is set). It reuses Workload.Grid with a server-only own-person boundary and the same committed-hours basis as the band. A normal picker gains no workspace Workload access. Hidden work yields Partial view with suppressed hours/band. Picking up refreshes the cached hint. The list includes project/discipline/offerer/age/hours/date context; the detail form accepts optional hours/date/note and exposes self-pickup through server capability.

## Final local evidence (2026-10-18)

- PASS: focused domain/API/flag-metadata run, 57 passed, 0 failed/skipped. Full case list: `../048-verifiable-records/evidence/focused-tests.json`. PostgreSQL factories create/drop their own synthetic databases on localhost:55432.
- PASS: final API compilation through that test command. Compiler warnings and analyzers were suppressed for this focused run; this is not a clean analyzer claim.
- PASS: TypeScript app/node checks with incremental disabled; direct Vite build. Workspace dependencies are reused through a read-only node_modules symlink, so the ordinary incremental npm build could not write its cache; the explicit no-incremental checks and Vite build passed.
- PASS: actual Chromium with mocked API, six workflows, 72 calls, zero page errors, zero unknown GETs and zero axe violations in six scopes. `../048-verifiable-records/evidence/mocked-chromium.json`. This proves the exercised UI/requests against fixtures, not live integration or human acceptance.
- PASS: EF `has-pending-model-changes` reports none. Fresh disposable databases apply all migrations. Original recovered TemplateOffsetDayKind migration and Designer are byte-for-byte identical to the saved handoff (SHA256 f31c0544e0f4a9541096bbd95c17a9720e7d09c163b06ec6a7ac08f91f5d747d / d697cfbb6d1ac70cc71bcd198f04c1f40533e2c3f50e9d4cea8deebd273b7008).
- PASS: independent source review after fixes; `git diff --check`; combined-spec freshness and trace (1299 IDs, 405 sections, no gaps).
- UNPROVEN here: full combined Release/regression/coverage gates, existing populated schema upgrade/Down/previous-image compatibility, hosted transport, real identity, native/iOS, comprehensive phone/keyboard/screen-reader and Jay's acceptance. Integration owns the combined release checks. Flags remain dark in maintained settings; only disposable tests enable them. No operational DB migration, push, PR, deployment, pilot activation or Accepted status.

## Reviewed final companion

Core local commit: `4f92c6ea6376b777d51ce7218bd3739f9132bff6`. Companion API checks PASS21/21 (20 relevant retests plus one additional own-load case), no failures/skips, in `../048-verifiable-records/evidence/companion-tests.json`. Needed-by week and lack of broader Workload access are asserted at the API. The final Chromium suite additionally asserts the needed-by week query and own-load refresh after a pick. No new migration/model changes in this companion.

## Recovery-time boundary rerun

After the interrupted session, the local Docker daemon and PostgreSQL listener were unavailable. The latest two strengthened assertions (invalid hours and own-load suppression for a synthetic hidden-project assignment) compile but could not reach assertions: both test hosts fail with Connection refused at 127.0.0.1:55432. This is BLOCKED_ENVIRONMENT, not a new application failure or a passing result. `../048-verifiable-records/evidence/boundary-rerun.json` preserves actual runner outcomes and the reason. Re-run `FullyQualifiedName~Own_load_hint|FullyQualifiedName~Offer_leaves_task_fields` when the disposable test DB is restored. The earlier 57 core tests, 21 companion API checks and final six Chromium workflows remain separately recorded passing evidence; the production source did not change after that companion API run, only these assertions were strengthened. No shared runtime was started or changed to bypass the missing environment.
