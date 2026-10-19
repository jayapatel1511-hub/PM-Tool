# Verification: Task Pickup

Runtime source `566fe5187d72be56a14fd16d0428e8b8e6e34d72` passes **1,514/1,514 backend tests** (zero failures/skips). Rules branch coverage is **3,032/3,155 (96.1%)** and service line coverage **13,084/13,665 (95.7%)**. The 799-response shape gate passes from one full-run manifest. See the [acceptance follow-up](../../docs/handoffs/2026-10-19-039-050-acceptance-followup.md) and its hashed receipt for current browser, image and source boundaries. Earlier slice runs below are historical; their missing-schema/startup blockers are resolved by the combined run. Prior compatibility evidence retains source `1b649f5`; no schema changed afterward. No packet is Accepted; hosted, pilot and human gates remain separate.

| AC ID | Local result | Evidence and remaining boundary |
|---|---|---|
| AC-PKP-01 | PASS API / partial UI | Offer preserves task fields; task marker present only flag-on; recipients/catalogue reviewed. Follow-up persisted-recipient matrix passes: offer, pickup, manager assignment, withdrawal and automatic closure; actor exclusion, overlapping roles, inactive users, current Restricted membership, preferences and mute policy are asserted. |
| AC-PKP-02 | PASS | Same-team pickup reassigns, watches prior assignee and closes once; task old/new audit and normal team/authorship safeguards reused. |
| AC-PKP-03 | PASS | Outside-team and ReadOnly mutation rejected; org unit matching overrides manager fallback. |
| AC-PKP-04 | PASS | Two simultaneous pickers: one winner and one conflict. |
| AC-PKP-05 | PASS | Completion, reassignment, project Complete/Archived auto-close; deleted-task history remains in report. |
| AC-PKP-06 | PASS API | Dedicated Restricted matrix proves member visibility, same-team nonmember/removed-member omission from list/report (including hours, reason and note), and rejected pickup with unchanged task/offer state. |
| AC-PKP-07 | PASS | Manager target constrained to same team; roleless direct-manager cannot offer/withdraw/assign; normal write permissions unchanged. |
| AC-PKP-08 | PASS local API / bounded UI | Default route dark and marker omitted; full combined regression passes, including all 28 TaskPickup cases. The corrected four My Work view/flag browser pairs retain their recorded source boundary. |

UI: real Chromium pick-up flow and versioned POST pass with mocked API. TaskPanel offering, withdrawing and assigning are wired to server capabilities. The companion completes the own load-band hint beside each pick-up action for its needed-by week (current week when no needed-by date is set). It reuses Workload.Grid with a server-only own-person boundary and the same committed-hours basis as the band. A normal picker gains no workspace Workload access. Hidden work yields Partial view with suppressed hours/band. Picking up refreshes the cached hint. The list includes project/discipline/offerer/age/hours/date context; the detail form accepts optional hours/date/note and exposes self-pickup through server capability.

## Final local evidence (2026-10-18)

- PASS: focused domain/API/flag-metadata run, 57 passed, 0 failed/skipped. Full case list: `../048-verifiable-records/evidence/focused-tests.json`. PostgreSQL factories create/drop their own synthetic databases on localhost:55432.
- PASS: final API compilation through that test command. Compiler warnings and analyzers were suppressed for this focused run; this is not a clean analyzer claim.
- PASS: TypeScript app/node checks with incremental disabled; direct Vite build. Workspace dependencies are reused through a read-only node_modules symlink, so the ordinary incremental npm build could not write its cache; the explicit no-incremental checks and Vite build passed.
- PASS: actual Chromium with mocked API, 8 bounded workflows, 104 total API calls, four explicit My Work view/flag pairs, zero page errors, zero unknown GETs and zero axe violations in six scopes. `../048-verifiable-records/evidence/mocked-chromium.json`. This proves the exercised UI/requests against fixtures, not live integration or human acceptance.
- PASS: EF `has-pending-model-changes` reports none. Fresh disposable databases apply all migrations. Original recovered TemplateOffsetDayKind migration and Designer are byte-for-byte identical to the saved handoff (SHA256 f31c0544e0f4a9541096bbd95c17a9720e7d09c163b06ec6a7ac08f91f5d747d / d697cfbb6d1ac70cc71bcd198f04c1f40533e2c3f50e9d4cea8deebd273b7008).
- PASS: independent source review after fixes; `git diff --check`; combined-spec freshness and trace (1299 IDs, 405 sections, no gaps).
- UNPROVEN here: full combined Release/regression/coverage gates, existing populated schema upgrade/Down/previous-image compatibility, hosted transport, real identity, native/iOS, comprehensive phone/keyboard/screen-reader and Jay's acceptance. Integration owns the combined release checks. Flags remain dark in maintained settings; only disposable tests enable them. No operational DB migration, push, PR, deployment, pilot activation or Accepted status.

## Reviewed final companion

Core local commit: `4f92c6ea6376b777d51ce7218bd3739f9132bff6`. Companion API checks PASS21/21 (20 relevant retests plus one additional own-load case), no failures/skips, in `../048-verifiable-records/evidence/companion-tests.json`. Needed-by week and lack of broader Workload access are asserted at the API. The final Chromium suite additionally asserts the needed-by week query and own-load refresh after a pick. No new migration/model changes in this companion.

## Historical recovery-time boundary rerun — resolved

After the interrupted session, the local Docker daemon and PostgreSQL listener were unavailable. The latest two strengthened assertions (invalid hours and own-load suppression for a synthetic hidden-project assignment) compile but could not reach assertions: both test hosts fail with Connection refused at 127.0.0.1:55432. This is BLOCKED_ENVIRONMENT, not a new application failure or a passing result. `../048-verifiable-records/evidence/boundary-rerun.json` preserves actual runner outcomes and the reason. Re-run `FullyQualifiedName~Own_load_hint|FullyQualifiedName~Offer_leaves_task_fields` when the disposable test DB is restored. The earlier 57 core tests, 21 companion API checks and previous companion six-workflow Chromium run remain separately recorded passing evidence; the backend production source did not change after that companion API run, only these assertions were strengthened. No shared runtime was started or changed to bypass the missing environment.

## My Work flag regression correction

Integration found the original My Work flag-off check used a paginated-list fixture for me/work rather than its real Work response. An error fallback could satisfy the missing-link assertion, so that earlier check did not prove the My Work action was gated. Both Overview and TaskView now gate only the pickup action with task_pickup and reuse the existing pickup title translation.

The corrected mocked Chromium run renders Overview and Upcoming Tasks with the real Work/tasks/planning fixtures, then asserts the main-content pickup link is absent with the flag off and visible exactly once with it on. The main scope excludes global navigation. The view switcher, saved Views control and New task remain visible in all four cases. PASS artifacts are written only after page-error, unknown-GET and axe checks pass. CHROME_EXECUTABLE_PATH is supported for an installed browser.

PASS: app/node TypeScript checks with incremental disabled, Vite build, corrected Chromium regression and independent source/test review. The replacement ../048-verifiable-records/evidence/mocked-chromium.json contains the explicit four-pair records. No backend/model/schema change is included in this follow-up; that historical database/combined-test limitation is resolved by the current 1,514-test run. Human, hosted and pilot acceptance remain open.

## Acceptance follow-up (2026-10-19)

Local source commit `566fe51` fixes automatic closure notifications that bypassed the shared `Notifier`. Eight inactive/preference/removed-member/muted task/project-close cases failed before the fix. The shared notifier now applies the same delivery rules as explicit pickup commands. `TaskPickupTests` passes all 28 cases in `ci-artifacts/integration/acceptance-followup/fourth.trx`; the three source/caller files also pass independent review. The full follow-up passes 1,514 tests, including all 28 TaskPickup tests and the formerly blocked own-load and offer-preservation cases. Earlier 1,491-test evidence belongs to the prior runtime.

The new exact recipient-set checks inspect stored users and count=1, including manager/lead/peer overlap. Assignment retains the existing direct-assignment exception to muted follows. Automatic closure is re-saved to prove no duplicate notice. Tests use disposable synthetic databases; this is neither actual email delivery nor operational acceptance.
