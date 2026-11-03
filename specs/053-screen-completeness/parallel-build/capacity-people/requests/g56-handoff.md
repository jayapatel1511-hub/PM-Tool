# G56 / checkpoint28 — D Admin phase1 handoff

**READY / HOLD, 2026-11-02.** All thirteen delivered application, test and coverage sources are authored and frozen. The C# subset was frozen and handed to root earlier, before root compilation. No further source edits by D without a new exact loan. This is partial S49 implementation, not a runtime PASS or acceptance verdict.

Implementation root: `/Users/jaypatel/.codex/worktrees/ef94/PM-Tool/.build-052-053`, branch `codex/build-052-053`. The [phase1 freeze](../../g56-admin-phase1-freeze.md) defines the scope. Root owns shared mounts, DTO integration, PeopleScope extraction, Text/locales, cache, schema/registry/generated contracts, execution and final verification. D ran no compiler, test, parser, coverage checker, browser, build, generator, EF command, service or helper. No operational flag, commit, push, deployment or acceptance status changed.

## Exact delivered sources

Hashes were reread from disk for this handoff, including the root-owned mechanical compiler repair described below. Earlier dirty changes in Admin.tsx predate this loan; this hash identifies the integrated file, not exclusive D ownership of every line.

| File | Lines | SHA-256 |
|---|---:|---|
| `src/Hub.Api/Features/AdminUserScreen.cs` | 61 | `2f834f6fe7bdbcf5e2f18b3cb76b04075e6fd6a75aee55732cd688ebf94c4e1f` |
| `src/Hub.Api/Features/AdminRoleExplanations.cs` | 40 | `881eb8e6c35772d0d1ac292e89d1b8b8adf2f3923e5301294aa5511b4ee4dfb9` |
| `src/Hub.Domain/PermissionExplanationCatalog.cs` | 176 | `3f3170da83c2ca0b7fc9d4b0812b3bc7e7601fd09732729bac012d70cd2b85c0` |
| `tests/Hub.Tests/Api/AdminUserScreenTests.cs` | 99 | `1eb4b559f77912cbd59aacb29a1dd845a3add466bd4ad426ea5099dc08393f08` |
| `tests/Hub.Tests/Api/AdminRoleExplanationTests.cs` | 35 | `171d6e624cf067e2ca2c36902cbc5f914885c22940ec1b7f4e72fa4316622b51` |
| `tests/Hub.Tests/Domain/PermissionExplanationTests.cs` | 61 | `4e884af06a85118b5e7c90ef7ac94896ab8c5919f142f0a571063db83c55d810` |
| `web/src/pages/admin/Admin.tsx` | 482 | `967dd17b7083e00c7994dc42a2e421ac8ad3652a5e4423463d6713623c8413fd` |
| `web/src/pages/admin/admin-role-explanations.tsx` | 42 | `75297c9a2aac3b26c5a7aad3b1d30ab00b551a0cafa7126cb3f8d5caaf3572eb` |
| `web/tests/admin-screen.cjs` | 131 | `c172729931453d9a311dacb6e413198370e6a5d539bdfb668c2cd136ead88b16` |
| `web/tests/admin-screen.fixture.cjs` | 45 | `311784e644eea534789acc51c1044ca9b8090456dcea78eb2ffdf0819a423a7a` |
| `tools/check_permission_explainer_coverage.py` | 117 | `96bf8ca68c55a9bde7f3ef49872455fcfbdda89b213f9a69c583c7d70f894c94` |
| `tools/test_permission_explainer_coverage.py` | 57 | `df8b7d30f309033d6f1ea93c1a32e2c8da62d24c789f9bfb2df0eab1c873c70f` |
| `tests/Hub.Tests/Snapshots/permission-explainer-bindings.json` | 1300 | `415b05468b9e803f3bac1af4701e4af09d7c5c4d6ffc292421379b6465e9e9c6` |
| `specs/053-screen-completeness/parallel-build/capacity-people/requests/g56-admin-phase1-labels.json` | 143 | `e7eb4bb18dc0a1514b1c3d5e82951a23a1d22924d56e2ae12eb211cbf3e72488` |

Read-only source, JSON metadata, final-newline, trailing-whitespace and hash checks passed. These checks do not establish C#, Python, CJS, generated-contract or browser correctness.

Root independently matched all six original C# hashes, then reported bootstrap1 found CS0819 in AdminRoleExplanations.cs: two declarators in one `var` statement. With the compiler stopped, root split `roleKey` and `scenarioKey` into separate declarations without changing the signature or behavior. Root recorded D-compile-repair1.json and D-csharp-source-ready2.json and began bootstrap2. The current hash above preserves that root repair; D made no C# edits after HOLD. Root's latest execution logs, not this source handoff, determine compilation/test status.

## Behavior and shared seams

- AdminUserScreenReads.Read uses actual returned user IDs, a common timestamp and target user row versions inside root's flag-on RepeatableRead list envelope. Home discipline retains retired labels and distinguishes unset from unknown. Base/configured capacity is the override or configured organisation default, preserving true zero; it is not schedule-aware availability or free capacity.
- Only target users with the actual Supervisor role receive counts. Structure off counts active direct reports explicitly, even for mixed Admin/Supervisor roles. Structure on calls root's unchanged-behavior PeopleScope.FromSnapshot over preloaded canonical facts, including target full roles, actual indirect settings and closed-unit semantics. Counts include precisely the canonical deduplicated IDs and explicit self membership. Inactive targets display configured scope, not current access.
- New Users columns use successful current-key rows, viewer/flag/structure-partitioned enhanced keys and coherent user ID/version/home/basis facts. Mismatched or unavailable metadata is withheld rather than invented zero. Off keeps the prior keys, columns and form behavior. Root's decisionCategories/issueRootCauses reference registration and all reference UI remain intact. Existing UserDialog, role writes, deactivation and Reassign commands were not changed by this phase.
- PermissionExplanationCatalog directly invokes each current public Permissions Allow, bool and Allow-tuple method through 79 typed bindings. No copied allowed-role table or runtime workspace-file dependency exists. Allow reasons and EditTime's correction-reason requirement come from the actual result; boolean predicates have no invented reason. Relationship/condition predicates display Yes/No rather than implying a permission grant.
- The Admin-only, people_screens-gated role-explanations read accepts only validated role/scenario choice keys. It rejects forged, duplicated or extra query fields, does no real person/project lookup and performs no write. Internal synthetic GUIDs never reach the wire. Public inactive/restricted-outsider scenarios are withheld until compound access coverage is complete; internal negative cases remain testable.
- The UI selects one labelled role and one labelled condition case at a time, showing human action labels, actual outcomes/reasons and expandable condition facts. It exposes no method IDs, policy debate, build status or new command. Product UI never uses examples to authorize a live action. Current errors suppress old example rows.
- Root confirmed integration of all 141 server label proposal entries, plus the final Role and correction-reason UI labels. The proposal remains a traceable source artifact; D did not edit shared Text or locale files.

## Verification sources and honest remaining gaps

Authored provider tests cover direct/org/indirect/mixed/inactive target scopes, self and closed-unit behavior, true-zero override versus fallback, non-supervisor NA, row coherence, fresh membership, read-only/off/refusal boundaries and canonical-load characterization alongside independent counts. Domain tests use independent expected outcomes and reflection binding completeness, including current System-PM template behavior. API tests cover strict choices, labels, no GUIDs, absence of writes and existing Admin enforcement.

The synthetic SPA driver refuses unarmed API writes and unknown reads. It covers columns and template booleans, retired/unknown home discipline, zero/default capacity, inactive configured scope, role/scenario transitions, actual reason display and condition Yes/No, existing editor Cancel/source focus, Person panel return, org-scope basis/self, version mismatch, current read refusals, tablet, existing phone notice and exact enhanced-off absence. It is UI evidence only and has not been executed by D. Fixtures do not prove canonical authorization, persistence or production data.

The coverage manifest records 83 normalized action rows across sections 8.5.1/8.5.2/8.10/8.11, 79 canonical signatures and 20 explicitly listed guard source files. Section fingerprints include headers/cells/conditions/notes; the checker rejects missing/new actions, signatures, bindings and listed-guard drift. The listed guard set itself still needs independent completeness review. All matrix rows and compound/transport guards remain Pending, with system-pm-template-policy and compound-and-transport-guards completion gaps. The complete gate must refuse a full FR-SCR-83 claim. A default structural pass, if achieved, is not policy equivalence or completed S49.

Guard hashes observed changed during root integration: `src/Hub.Api/Features/Admin.cs`. D did not rebaseline them. Root must inspect those actual source changes and explicitly refresh reviewed metadata, preserving previous evidence; rerunning without review is not a coverage fix.

FR-SCR-82's deactivation inventory/confirmation/Reassign work and the separate seven-case command verification were not implemented. Full FR-SCR-82, FR-SCR-83 and S49 remain open/partial. No permission enforcement body, Vocabulary policy or template grant changed. Jay's System-PM/template-policy choice remains pending; the existing actual Admin/template-editor behavior is preserved.

## Root-owned checks, all unrun by D

1. Complete isolated .NET compilation, then the new AdminUserScreenTests, AdminRoleExplanationTests and PermissionExplanationTests with the real provider. Include adjacent Admin, PermissionMatrix/Sweep and OrgTree/PeopleScope checks according to impact; root records exact selections and outcomes.
2. Run root's canonical schema/registry/OpenAPI generation and parity checks, then `node --check web/tests/admin-screen.cjs` and its fixture; `npm run typecheck:tests`, application `tsc -b`, focused oxlint for the two TSX sources and root's production build checks. Root already added the fixture to its test inventory.
3. Run `python3 tools/test_permission_explainer_coverage.py`, followed by `python3 tools/check_permission_explainer_coverage.py`. Preserve listed-guard drift failure until the actual integrated changes are reviewed. Run `python3 tools/check_permission_explainer_coverage.py --require-complete` and record the expected refusal from pending policy/compound coverage; do not label that expected refusal a completed FR83.
4. Through the single browser lane, run `TUESDAY_UI_BASE_URL=<verified-local-base> CHROME_EXECUTABLE_PATH=<verified-Chrome> node web/tests/admin-screen.cjs` against the frozen generated SPA. The driver defaults to local port8853 but does not start any service. Preserve failures and final artifact hashes. Run adjacent legacy Admin/person/off checks required by root's impact review.
5. Root integrates results in packet verification/build-progress records. Jay's acceptance, inventory authorization and deployment are separate gates. QA24 services/data and earlier failed receipts remain preserved.

Application, test, fixture, coverage and label sources now HOLD. This documentation handoff authorizes no new writing or execution by D.

## Provider1 observed fixture repair — authoritative test hash

Root's first focused provider run completed with 70 PASS / 6 FAIL across the shared selection. The four AdminUserScreenTests cases all failed during Seed before their assertions. The preserved focused-api1.log and test-results/g56-focused1.trx show EF's Added AppUser.UnitId / OrgUnit.LeaderId circular insert dependency. That failure does not establish a screen-helper or permission regression; the four cases' behavior assertions remain unproven until rerun.

Under root's exact one-test-file repair loan, D moved the supervisor's UnitId assignment after the first SaveChangesAsync and saved that link before Seed returns. The final leader/unit/user graph and every Settings/Read/test assertion remain unchanged, verified by a read-only substring comparison. Root's explicit DbAsync<Graph> compiler correction remains intact. No application source, permission body, helper, count expectation or role policy was changed. D ran no compiler, parser, tests, browser or other execution.

**READY / HOLD:** `tests/Hub.Tests/Api/AdminUserScreenTests.cs`, 101 lines, SHA-256 `08b31b2e0f2e38271bef12dcb75a9ef3b79993d07711db259be73d449eb43def`. This supersedes the earlier table entry for this test only. Root owns rebuilding and rerunning the four failed provider cases; D has not claimed a PASS. Root separately reported the reviewed integrated guard-fingerprint refresh and structural coverage check PASS at 83 rows / 79 methods; the strict complete gate still refuses pending policy/compound coverage, as intended. Use root's current manifest and logs for those execution claims.

## Browser1 observed selector repair — authoritative driver hash

Root's preserved browser-admin-screen1.log stopped in the columns stage before any completed case: the unknown-discipline row's broad Unavailable text selector matched four cells, including three unrelated screen-reader Missing labels. Writes, unknown reads and page errors were empty at that stop; the log does not establish a full driver PASS.

Under root's exact CJS-only loan, D added a cell helper that finds the exact semantic column header and reads that real header's cellIndex, then selects the corresponding cell in the named user's row. Unknown discipline now asserts Unavailable in Home discipline specifically. Every new-column assertion uses its named column, including true-zero/default capacity, retired discipline, true/false Template editor cells, inactive configured scope and direct count, organisation basis/self and all three withheld mismatch fields. The earlier broad mismatch `.first()` was removed; unrelated missing cells cannot satisfy those assertions. Other role/action/reason, read-refusal/off/phone/editor-Cancel and Person-return checks and all finite API guards remain intact.

Optional natural page screenshots now use the root-created `TUESDAY_EVIDENCE_DIR`: admin-screen-desktop-columns.png after column assertions and admin-screen-role-examples.png after the selected negative discipline example. The driver does not create the evidence directory, inject DOM success state or mutate application facts. No fixture, application, C# or other source changed for this repair.

**READY / HOLD:** `web/tests/admin-screen.cjs`, 146 lines, SHA-256 `314a347859c1fd86deee73cfdbd16a144c4b32efab69f619a46098bc85b0b09a`. This supersedes the earlier table entry for this driver only. Source-only review, final newline, trailing whitespace and hashes were checked; D ran no parser, test, browser, type checker, generation or helper. Root owns parsing and rerunning the repaired driver after its source freeze.

## Observed activity-history flag projection — regression source handoff

Independent review identified that new registry fields, null-valued deletion keys and new catalogue names could survive owning-flag deactivation in shared activity reads. The [audit projection freeze](../../g56-audit-projection-freeze.md) gives root ownership of ActivityScreenFields.cs, Activity.cs and Admin.cs. D authored only the new ActivityScreenFlagProjectionTests.cs and this addendum. B's [registry-diff inventory](../../work-delivery/requests/g56-audit-field-inventory.md) independently identifies 22 fields in 11 existing item types and six whole-event additions, across register_screens, people_screens, capacity_screens and visual_system.

Five authored facts provide these distinct checks:

1. The complete expected inventory exercises all 16 independent flag combinations through the public ActivityScreenFields helper. It checks exact snapshot keys even when values are null, exact per-type/per-field ownership, case-insensitive field matching, both Project flags independently, the six whole-event rules including variable UserCertification keys, and Visible's query expression. Existing Project status-note fields, User home discipline/capacity/template-editor and ordinary reference fields remain visible. An ordinary ProjectType whose display name is DecisionCategory is not mistaken for that new catalogue.
2. Real stored provider rows cover mixed legacy/new updates, new-only updates and deletion snapshots for the five initial G56/G55 families: Decision, Issue, Risk, Template and DocumentLink. Actual declared DB flags are changed through the Admin setting command in both-on / register-off / people-off / both-off / both-on order. Current rendered change fields, null snapshot keys, SnapshotLabels and summaries are compared to independent expectations. A hidden-only update remains a generic event with empty changes/summary. Synthetic markers and GUID-valued new references cannot leak through rendered output. The final enhanced rendering must equal its original rendering.
3. A real project and Decision anchor exercise project history, item history, Admin history with project-inclusive category selection, the project-activity-log report, and both list/report CSV exports. The same stored target event hides and restores its rationale while retaining its legacy subject. JSON fields/action/ID, CSV type/BOM and the legacy list-export header are checked; ordinary Admin authorization is still enforced.
4. Real POST commands create a legacy ProjectType and the two G56 catalogues, proving actual stored ReferenceData ItemKeys. With a controlled clock and Admin actor/date/type filter, pageSize=1 checks exact totals, order, every page and the first empty page. When register_screens is off, the newest catalogue events are excluded before paging and the older ordinary reference fills page1. people_screens does not control that exclusion. Default Render agrees with the permitted row set.
5. InternalNotes values remain redacted in changes, summaries and deletion snapshots, while the existing User template-editor boolean and true-zero configured capacity remain visible with the enhanced flags on or off.

Every provider/read loop compares the same target raw Changes/Snapshot strings, stored ItemKey/ItemName and ChainId/ChainSeq/PrevHash/RowHash before and after reads and flag changes. Flag-setting and export-access audit rows are intentionally outside that immutable target cohort. No source audit row is edited to create a passing projection, and these synthetic rows are explicitly labelled fixtures rather than operational data.

**READY / HOLD:** `tests/Hub.Tests/Api/ActivityScreenFlagProjectionTests.cs`, 252 lines, SHA-256 `a818dc6d349d6fc51d21a6388f007c9505b224b72872791654294ddbab2f32cb`. The five facts are authored and unrun by D; only source/whitespace/newline/hash reads were performed. No compiler, parser, test, browser, generator, helper or service ran in this lane. Root owns compiling this test alongside its held shared repair, running ActivityScreenFlagProjectionTests plus the existing activity/privacy/report/export checks appropriate to its impact review, preserving failures, and recording final verification. This addition does not close S49, the full permission-matrix gate or any human/deployment gate.
