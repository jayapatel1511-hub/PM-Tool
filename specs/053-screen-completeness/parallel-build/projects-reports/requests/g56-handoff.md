# G56 C S41 source handoff

2026-11-02. **READY for root source review; HOLD for integration, compilation, migrations, tests, runtime and acceptance.** This lane has stopped authoring its ten source paths at the hashes below. No commit, push, deployment, flag activation, helper or runtime execution was performed.

Build checkout: `/Users/jaypatel/.codex/worktrees/ef94/PM-Tool/.build-052-053`, branch `codex/build-052-053`. Read `CONTROL.json` and `g56-issue-root-cause-freeze.md` before resuming. The coordinator remains chat `01a192dd-0b3b-7d31-81f0-69243cd8be2c`; this lane is `01a19462-65cb-7ca2-9228-79a3c15c3549`.

## Authored behavior

- Existing issue transitions retain their canonical version, legal step, edit authority, completed-project reason, resolution/date, Coordination reference/availability/independent verification and pending-impact gates. The new flag-on Resolved transition requires an explicit active organisation category; no default, D14 seed or historical backfill. Non-resolution non-null category is refused; non-null feature input off is 404.
- Resolution obtains `FOR SHARE` on the selected category and holds it through the existing issue save and owning transaction commit. It captures the validated category ID and actual name together. Reopening retains the last pair and resolution but clears resolved date; a fresh off resolution clears the pair. Text edits retain it. There is one canonical Resolved activity/version, no separate classification event or correction workflow.
- The optional appended IssueMove property is omitted when null; existing property order and the three-field IssueMoved response remain. New IssueRow metadata is null-omitted off. No Risk behavior, export columns or new vocabulary CRUD implementation was authored.
- The summary reads all live currently Resolved issues in one authorized project in a repeatable-read snapshot. It reconciles known counts plus separately unknown history, rejects inconsistent stored pairs/FKs, and shows current names/inactivity. Per-issue RecordedName remains the historical resolution label.
- The issue query owns the raw rootCause filter and is already shared by the existing issue list/export endpoint. Counts are independent of filter, pagination and expanded visual groups. Drilldown starts a new query preserving only group/sort/cols, sets Resolved and the cause, and clears all prior filters/page/panel/view. The token is removable and Clear includes it.
- A narrow charset-aware buffered JSON replay guard checks current flag and canonical project/issue edit authority for non-null feature bodies before the generic receipt lookup. It deliberately does not rerun version or state feasibility. Omitted/null legacy receipt behavior stays unchanged.
- The enhanced resolution form has no selected default and exposes the canonical correction reason. Uncertain/in-progress or malformed success replies preserve the original route, body, key and shown version. Only the correct three-field ID/Resolved/integer-newer-version reply closes it. The legacy flag-off form is preserved. Existing dirty-form guard is reused.
- Summary, choices and issue detail/list reads partition their relevant viewer/flag/project keys; current-key errors suppress enhanced cached counts/choices/details. Empty active catalogue has an explicit explanation rather than a fallback category.

## Root integration still required

Root owns all shared source, schema, registry, locale, generation, cache, build and execution. The source read during this handoff found the entity/DbSet/record, Admin.Ref, optional Me.ReferenceView choices and middleware guard mount present; other shared seams were still being authored. Root subsequently announced the shared cache prefix `issue-root-causes`; the C summary key now matches that exact prefix. Verify the actual final shared state rather than treating this list as completed work.

1. Finish the model/migration: nullable ID plus max120 snapshot pair check, nullable Restrict FK; allow both null on legacy/off resolved issues. No starter rows. Ensure actual table name `hub.issue_root_cause_category` matches the lock SQL.
2. Keep the single existing `Admin.Ref<IssueRootCauseCategory, ReferenceRow>` registration at `/admin/issueRootCauses` behind register_screens; trimmed/nonblank/no-control names max120, case-insensitive unique, canonical Admin/version/audit/usage/retirement. Usage includes historical/deleted issue references. No hard delete or duplicated CRUD helper.
3. Append optional null-omitted `IssueRootCauseChoice[]? IssueRootCauses` to Me.ReferenceView, supplied only on register_screens, including active and inactive choices. Use the top-level DTO in Hub.Api.Features.
4. In generic IdempotencyMiddleware, detect `IssueRootCauseReplay.Guard` and call `IssueRootCauseReplay.Authorise(HttpContext, Access, HubDb, SettingsStore)` before claim/replay. RegisterEndpoints.Map already attaches this marker to the existing transition route. Keep the existing vocabulary create guard for the Admin route. Do not add body hashing or current-state/version validation on successful replay.
5. Add lowercase `rootCause` to the issue-only ScreenLists/saved-view contract, with correct flag-off hiding/ignoring. Existing RegisterQuery has an appended raw optional string. `IssueQuery(..., bool rootCauseEnabled = false)` applies the predicate; `IssueList` supplies the actual flag and already feeds both existing list and export. No second export predicate is needed: the existing `/projects/{id}/issues/export` in Registers.Map calls IssueList with the same AsParameters RegisterQuery. Preserve all IssueCols/order on and off and all Risk predicates. Existing `ListExportEndpoints.Filters` currently describes every raw parameter even if ignored off; root must decide/apply the frozen issue/flag-aware metadata handling so a new raw off parameter does not imply an applied filter. Do not loosen filters for other lists.
6. Merge the exact server/UI keys and English copy from `g56-locales.md`. Add normal audit field labels through root shared registration. No dynamic translations or shadow schema/type definitions.
7. Summary now uses `['issue-root-causes', projectId, viewerId, true]`, matching root's announced exact shared invalidation prefix. Verify that prefix is invalidated on issue, reference-data, setting, project, member and user changes as root specified, including generic invalidation. Choices use `['reference', 'issue-root-causes', viewerId, enabled]` and are covered by the reference prefix. Main list uses viewer/register flag; detail appends register flag to its existing viewer/work/people key.
8. Regenerate the actual API/registry contracts after source freezes. New summary handler returns `Task<Ok<IssueRootCauseSummary>>` for typed contract discovery. Fixture and SPA references intentionally depend on the real generated schemas and routes.

Public helpers: `IssueRootCauses.Flag`, `Map(RouteGroupBuilder)`, `Filter(IQueryable<Issue>, string?, bool)`, `Task<IssueRootCauseValue> LockSelection(HubDb, Guid?)`. Replay marker and method are as above. Top-level DTOs: IssueRootCauseChoice(Id,Name,IsActive,SortOrder), IssueRootCauseValue(CategoryId,RecordedName), IssueRootCauseCount(CategoryId,Name,IsActive,Count), IssueRootCauseSummary(ProjectId,ResolvedTotal,NotRecordedCount,Causes).

## Authored verification and its limits

All new checks below are **UNRUN**. They are proposed verification source, not pass evidence.

- Provider/API tests explicitly create the organisation category, require selection, verify exact normal response/activity, refusal atomicity for missing/empty/unknown/wrong-kind/retired, resolution/date/stale/readonly/outsider failures, non-resolution input, text-edit retention, completed-project reason/authority, provider-fault rollback, reopen/on/off history, real Coordination independent verification, UTF8/UTF16 successful stale-state receipt replay and off/revoked authority refusals, old wire/legacy receipt compatibility, actual non-key rename/retirement lock ordering, and nullable pair/FK constraints.
- Read tests use 56 known plus one legacy unknown resolved issues alongside open/cancelled/private-project controls. They compare project-wide counts, current retired/renamed labels and old recorded labels, exact list keys and both CSV/XLSX data keys with all 18 existing export columns, malformed/off/unknown raw filter behavior, empty known zero, restricted/missing-project access and state immutability. Exported access activity is deliberately excluded from read-state comparisons after downloads.
- Vocabulary tests cover initially empty catalogue, Admin gates, stale version, audit, retirement, no deletion, no-control/name length/duplicate validation, inactive reference choices, off shapes and cached create hidden off.
- Strict synthetic browser driver allows only armed issue transition writes and listed reads, checks count versus page/group duplication, full drilldown clearing, unknown cohort, no default and guarded Cancel, stale refusal, abort-after-simulated-commit plus later refusal and exact retry, malformed wrong-ID success plus exact retry, honest empty catalogue, 375px dialog and off form/body/no-summary. It fails on unlisted traffic and page errors. It requires a root-started local SPA and does not start services. It proves synthetic interaction only, never real provider persistence or live authorization.
- Existing actual inspector/source URL, nonzero-scroll and natural focus behavior were preserved by the baseline diff. They have not been rerun by this lane; root should run the relevant existing navigation checks with the final SPA rather than infer that source preservation proves browser behavior.

Suggested root checks after all relevant authors freeze: generate/contracts and standard fixture/type checks; focused `dotnet test tests/Hub.Tests/Hub.Tests.csproj --filter FullyQualifiedName~IssueRootCause`; existing issue/register regressions appropriate to the changed canonical transition; then `TUESDAY_UI_BASE_URL=<root-owned local SPA> node web/tests/issue-root-cause.cjs`. Root selects its established isolated runtime/database and migration procedure. No acceptance claim or packet state change is authorized here.

Static work actually performed: read the frozen contract/current shared seams; reviewed both existing Registers files against `.review/checkpoint28-baseline` (only S41 issue seams differ); ran scoped `git diff --check` on the two tracked files; ran `git diff --no-index --check /dev/null <path>` for each of the ten paths with no whitespace diagnostics (exit 1 denotes content difference in this mode); computed SHA256 below. No .NET, EF, generation, typecheck, Node, Vite, browser, API or service operation ran.

## Source freeze hashes

| Owned path | SHA256 |
|---|---|
| src/Hub.Api/Features/Registers.cs | 6c52da67e6f4e16e877014c3d6045c1380227a2f9aae15e0064819b3ffd52034 |
| src/Hub.Api/Features/IssueRootCauses.cs | b5116407cd722efb117c57589739967e176da7005dce1b532cfe084eb4d5c6c2 |
| src/Hub.Api/Features/IssueRootCauseReplay.cs | dc9958eae1dc1204065fa8a93f31ffc7cc77ee5eca035f9f97ac600e1f1a6ac5 |
| tests/Hub.Tests/Api/IssueRootCauseTests.cs | 9572bca47652ce418959d7de5860b74332ac211a6fbf962980f072bd1783033a |
| tests/Hub.Tests/Api/IssueRootCauseReadTests.cs | c1c0955546a71c785879c93506acd859468b7042f053cb85ffdd97ba563e14cf |
| tests/Hub.Tests/Api/IssueRootCauseVocabularyTests.cs | 38eb26e0d43b00953b009ecfd8c2accb98b19e674ad4b8d8648e1d7dd5b5371b |
| web/src/pages/projects/Registers.tsx | 6a38613008c63fa8b9cff675c66c3c60bd386d6e165fc1f3196e8aaf1d5d7a49 |
| web/src/pages/projects/issue-root-cause.tsx | 99934936f6e3fba76d20a58c5e2e743dcb58e9c8c6db44b05fa57353f8fc6739 |
| web/tests/issue-root-cause.cjs | 9f05716b099ef39cb6248d4ecf20fffc0ed7ed8fa2db35d8eb89b76480fe50d4 |
| web/tests/issue-root-cause.fixture.cjs | 1606cf08efe35a6baa29a1a57ad74cdd1146962c4518434ff552f03017fc7295 |
| projects-reports/requests/g56-locales.md | cf4b24ce6825b188388926c141fe411155e8645936cb6e84e12138d893f168ef |

Open product policy 053-D14 remains open. Synthetic category names above are fixture-only and do not settle that decision. All existing unrelated dirty/untracked work remains preserved.

## Browser-only repair after root batch1

2026-11-02 follow-up: **READY repaired driver/fixture; HOLD for root rerun.** The earlier authoring-stage verification statements above are historical. Root subsequently reported its provider cohort145PASS and that browserbatch1 reached the interaction end but failed the strict unknown-GET check. This lane did not execute those checks or claim that the browser suite passed.

Observed source: existing `IssueMetadata` in `web/src/pages/projects/Registers.tsx` reads `/api/v1/projects/{projectId}/changes/options` using exactly `issue.projectId` and no query. The server returns `ChangeEndpoints.CoordinationOptions`; its generated schema is `Schemas['CoordinationOptions']`. Added one generated-typed fixture object with this actor and actual fixture project disciplines, no extra source/work/person records, and one finite GET branch requiring `projects/${f.projectId}/changes/options`, the configured local base origin and an empty query. Other project/query/origin cases remain unlisted501; business mutations still require the original exact armed POST. There is no broad platform fallback or feature disablement. Unknown diagnostics now retain the query string.

Preserved root's `/** @param {number} n */` numeric ID annotation from `evidence/checkpoint28-runtime/C-fixture-type-repair1.json` (root-repaired fixture hash61095561a815a16c9f4360eb36fc77044f10f68df5860a758fcce96761303b59 before this loan).

Optional root-only capture: when `TUESDAY_EVIDENCE_DIR` is set, the same ordinary UI flow saves `issue-root-cause-desktop-project-summary.png` at1440px and `issue-root-cause-375-resolution-selector.png` at375px. Screenshots use the naturally rendered page after its existing interactions, without DOM/focus injection. Files are written exclusively (`wx`), so a prior artifact is not overwritten. Paths are included in the final synthetic JSON. This lane created no images and ran no browser, parser, typecheck, generator, compiler or service. Root should choose a fresh evidence directory for its rerun.

Only the two browser files and this own handoff changed under the repair loan. The table above records their replacement freeze hashes; all application/backend sources remain HOLD. Static whitespace checks for these three files have no diagnostics. Source/type/runtime correctness of the repaired fixture remains unverified until root's allowed checks.
