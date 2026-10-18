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

UI: real Chromium pick-up flow and versioned POST pass with mocked API. TaskPanel offering, withdrawing and assigning are wired to server capabilities. Viewer load-band hint remains an explicit small UI follow-up; no invented load calculation or extra write permission is substituted.

## Final local evidence (2026-10-18)

- PASS: focused domain/API/flag-metadata run, 57 passed, 0 failed/skipped. Full case list: `../048-verifiable-records/evidence/focused-tests.json`. PostgreSQL factories create/drop their own synthetic databases on localhost:55432.
- PASS: final API compilation through that test command. Compiler warnings and analyzers were suppressed for this focused run; this is not a clean analyzer claim.
- PASS: TypeScript app/node checks with incremental disabled; direct Vite build. Workspace dependencies are reused through a read-only node_modules symlink, so the ordinary incremental npm build could not write its cache; the explicit no-incremental checks and Vite build passed.
- PASS: actual Chromium with mocked API, six workflows, 49 calls, zero page errors, zero unknown GETs and zero axe violations in six scopes. `../048-verifiable-records/evidence/mocked-chromium.json`. This proves the exercised UI/requests against fixtures, not live integration or human acceptance.
- PASS: EF `has-pending-model-changes` reports none. Fresh disposable databases apply all migrations. Original recovered TemplateOffsetDayKind migration and Designer are byte-for-byte identical to the saved handoff (SHA256 f31c0544e0f4a9541096bbd95c17a9720e7d09c163b06ec6a7ac08f91f5d747d / d697cfbb6d1ac70cc71bcd198f04c1f40533e2c3f50e9d4cea8deebd273b7008).
- PASS: independent source review after fixes; `git diff --check`; combined-spec freshness and trace (1299 IDs, 405 sections, no gaps).
- UNPROVEN here: full combined Release/regression/coverage gates, existing populated schema upgrade/Down/previous-image compatibility, hosted transport, real identity, native/iOS, comprehensive phone/keyboard/screen-reader and Jay's acceptance. Integration owns the combined release checks. Flags remain dark in maintained settings; only disposable tests enable them. No operational DB migration, push, PR, deployment, pilot activation or Accepted status.
