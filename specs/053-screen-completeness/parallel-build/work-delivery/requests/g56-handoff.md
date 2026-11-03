# G56 / checkpoint28 — B S40/S48 source handoff

2026-11-02. **READY for independent root review, then HOLD.** Source authored only; no compile, provider, type, browser or acceptance PASS is claimed.

Checkout `/Users/jaypatel/.codex/worktrees/ef94/PM-Tool/.build-052-053`, branch `codex/build-052-053`. Original authoring used B's exact13-path loan. The browser-only repair used generation56, `generation56-B-browser-D-phase1-authoring-root-source-review-A-next-plan`, and changed only the two browser files and this handoff, preserving the other11 hashes at that handoff. The subsequent provider1 repair below used the new exact two-test loan. The root freeze governs; prior dirty work/runtimes are preserved. No helper was started.

## Implemented source

- `register_screens`: typed risk seeds, strict new nested-field/score/length/ref validation, omitted preservation versus explicit clear, hidden seed copying and initial-project-only snapshot. Actual p.ProjectManagerId owns ordinary Open risks with normal keys/activity and nullable actual TemplateRiskId. Shared Instantiate, AddPack, project-copy and ordinary risk commands are not extended to seed risks. The existing outer project transaction owns rollback.
- `people_screens`: exact version project usage with current visible counts, including the existing List.Projects field, visible archived/cancelled history and stable exact-count pages. Separate `/projects/{id}/template-provenance` reads existing exact source ID/version and actual highest numbered Published/Retired/current Published facts; no ProjectDetail/Projects.cs change, apply action or project write.
- NewDraft records typed schemaV1 source content plus actual clone map only while people_screens is on. New off clones leave nullable lineage unknown. Existing hidden baselines and risk seeds survive saves/clones; no legacy inference/backfill/GET repair. Returning an existing Draft does not recapture its origin.
- Draft diff covers Header, Discipline, Milestone, Deliverable, Task, Dependency, Role, Basis and Risk. Real identity/relationship normalization ignores fresh clone IDs and audit/version timestamps, preserves duplicate titles, compares semantic decimals invariantly, pages changes with exact visible totals, and explicitly marks hidden sections incomplete. Unknown/unsupported/incoherent stored baselines have null counts. Snapshot fields have finite per-kind validation.
- All existing family mutators (NewDraft/Publish/Save/basis add/remove/retire/discard) share the local family advisory transaction lock and recheck state/version within it. No global transaction/receipt/permission helper is changed. Existing risk-only publish-content guard stays.
- Enhanced GET template/list/project usage/provenance reads use repeatable snapshots. Template detail reloads/re-authorizes within the snapshot. New page controls are raw strings, ignored on the old detail route off; enabled invalid/overflow offsets refuse normally.
- **All old POST replies stay legacy-shaped** for S40/S48. Preview retains old date/role wire; risk preview reads the actual immutable source GET. PUT structure save can return current Risks but intentionally omits Screen/family usage additions; invalidate and authoritative GET obtain them. No middleware/replay marker/global receipt rewrite.

## UI and shared root seams

Existing Templates and FromTemplate source/settings seams now mount the generated-type risk editor/preview, version usage, paged saved-draft changes and informational source/newer-version section. Viewer/flag/version keys separate data; enhanced facts/actions are withheld on current-key read errors or missing metadata. Saved diff is tied to observed row version; dirty/uncertain input preserves its original version and intent instead of accepting a background refreshed version. Explicit Cancel discards local input and refetches. Established data-entry navigation guard is used only for the enabled additions. DesktopOnly template phone notice remains; project settings information wraps at375px.

Exact locale keys/text are in `g56-template-locales.md` (including `tpl.field.*`, `tpl.riskChoose`, `tpl.dataUnavailable`). Root owns integration into Text/i18n; no locale source was edited here. The new fixture uses future generated records, not shadow DTOs/any.

Root cache seam: query prefix `['template-provenance']` must invalidate on relevant Template publication/retirement and Project/Member/User access changes. It is partitioned by project, viewer and people flag. Usage/diff/detail use `['template',versionId,...]`; list uses `['templates',viewer,people,roles]`. Root owns cache/generated/fixture inventory/package registration.

Root owns schema, FK/checks/registry and migration. B consumes the supplied TemplateRisk/DbSet, Risk.TemplateRiskId and ProjectTemplate copied-from pair; those shared files were not edited by B. No Domain/Templates.cs, Registers.cs, Projects.cs, middleware or shared configuration edit.

## Authored checks (all UNRUN)

22 provider cases:16 facts and two3-case theories across the three owned test files. Cases exercise actual two-risk PM/Open creation and source/project edit isolation; unticked-discipline global risk preservation; successful AddPack/project-copy exclusion; one keyed project/two risks once; provider trigger fault rolling back the whole create; strict score/forged/off/version/role refusal; hidden stored-on→off saves/clones/instantiation and no backfill; real source v3/project250101/newer v5; exact currently visible usage versus unrelated same-version family and a Template Editor without restricted-project access; visible historical status/paging; inconsistent/null provenance; old POST replay after flags turn off; all nine diff kinds, duplicate names, basis remove+add, decimal equivalence, ordering/relationships and actual source rather than latest; null/future baselines, masked sections, exact change pages and enabled invalid/overflow controls; concurrent new-draft serialization and published save refusal.

The rollback test authors a trigger/function **only inside its HubFactory disposable database**, with finally cleanup; no trigger was executed by B. Other scale/identity setup is labelled synthetic. Canonical commands own ordinary authority/persistence assertions. Read-state capture includes source/clone/risk/project/structure/versions/counters and activity/notices/mail/outbox/receipts/Seen counts.

The new actual-SPA synthetic driver authors: two risk fields,51 paged changes, original-version repeated409 body/input retention, explicit Cancel without command, deliberate post-Cancel save at actually loaded version,21-project version paging, source3/newer5 informational settings at375px, real15-second QueryClient stale-cache refusal (verified current staleTime in main.tsx), existing phone notice and flag-off absence. It has finite GET/method/body guards and rejects unexpected commands, schema violations and page errors. No fake cache/history/focus injection; provider tests remain the real copy/authority proof.

## Browser acceptance repair after independent source review

Root reported bounded frontend and backend source-review PASS. Runtime, schema, generated contracts and browser behavior remain UNPROVEN. The browser-only repair adds the following authored cases; none was executed by B:

- Nonempty riskON/provenanceOFF and riskOFF/provenanceON scenarios. The first preserves actual stored seed IDs/content/version and forbids S48 reads/controls. The inverse preserves49 actual visible Task changes, omits risk fields, and checks the visible incomplete-sections notice and actual v3/v5 project provenance.
- Stored-on→bothOFF→bothON projection checks restore the same two risk IDs/content, row version, recorded source-v3 baseline and nonzero total without another business write. Provider tests continue to own real database preservation while saving/cloning off.
- The actual Create project dialog selects the Published v5 source, reaches its existing summary, and observes two immutable GET risk seeds as Open/PM. The old preview POST is checked against its actual Civil discipline, office, start date, one milestone/date and zero other structure counts. Cancel closes the real dialog; every project-create command is refused. Published versions now have distinct child refs and a real milestone, rather than empty published structure/default preview counts.
- Separate synthetic legacy and unavailable Draft records have their own families and child identities. Their visible NotRecorded/Unavailable baseline messages must have null source/counts/total, no successful zero-count comparison and no pager. The known source-v3 Draft is never temporarily rewritten into an unknown-history fixture.
- A project with a persisted source ID but missing version exercises the actual Unavailable response and generic UI, with all provenance facts null and no stale v3/v5 claim. A genuine no-source project has both origin fields null and naturally issues no provenance GET or section. Root explicitly accepted this boundary: the existing settings component mounts enhanced provenance only when a source ID exists; provider tests own API NotRecorded coverage. No forced NotRecorded response on a known-source record or TSX broadening was added.
- Existing repeated409/Cancel, usage pagination, phone, stale-cache/error and bothOFF cases remain. Finite template/project IDs, supported preview-body fields, exact paging values, nested risk-save fields and unexpected-method/command guards remain strict.

## Provider1 exact observed assertion repairs

Root's stopped provider1 run recorded70 PASS,6 FAIL overall in `specs/053-screen-completeness/evidence/checkpoint28-runtime/focused-api1.log`. Two failures were in B's tests. CONTROL was rechecked at generation56, `generation56_PROVIDER1_STOPPED_EXACT_TEST_REPAIRS`; B changed only `TemplateVersionProvenanceTests.cs`, `TemplateDraftDiffTests.cs` and this handoff. All other11 frozen app/test/browser hashes remain unchanged.

- Receipt replay: the log shows initial Location `/api/v1/templates/<draft-id>` versus null on replay. Read-only inspection confirms `Templates.NewDraft` returns that initial Created location, while the established generic IdempotencyMiddleware stores/replays only status, content type and body and adds `Idempotent-Replayed: true`. The test now checks the exact initial Location, null replay Location and existing replay marker. Exact legacy payload keys, repeated201/body, flag-off replay and no-new-write state assertions remain. No app or middleware behavior changed.
- Structural kind order: the log shows Deliverable before Dependency. `TemplateDraftDiff` emits kind/ref order using ordinal comparison. The exact nine-kind expected sequence now uses Basis, Deliverable, Dependency, Discipline, Header, Milestone, Risk, Role, Task, and compares the actual distinct output sequence without re-sorting it. Exact membership/count and deterministic output order remain asserted; basis remove/add and semantic role-hours assertions remain.

Only evidence/source reads, whitespace search, `git diff --check` and SHA256 were performed by B. No compiler, provider/browser test, generator, service, helper or other execution was started. Corrected-test PASS remains UNPROVEN until root rebuilds and reruns the two failed cases.

## Browser1 stopped exact harness repair2

Root stopped browserbatch1 with Chrome closed and loaned only the driver/fixture plus this handoff. CONTROL was rechecked at generation56, `generation56_BROWSER1_STOPPED_EXACT_HARNESS_REPAIRS`. The observed log `specs/053-screen-completeness/evidence/checkpoint28-runtime/browser-template-risk-provenance1.log` reports missing Settings `deliverable-numbering` reads and a timeout locating the Templates navigation link; no repaired-browser PASS is claimed.

- Added a generated-schema `NumberingRead` factory for the three existing owned fixture projects only: null synthetic numbering pattern, the actual project row version, read-only for this template editor who is not their PM. The exact GET route rejects query parameters, asserts register_screens is on, and retains the existing unexpected-method/project-ID refusal. No broader fallback or disabled traffic/flag was added.
- Source inspection confirms Templates is in legacy navigation's collapsed More group. After verifying the actual v3/v5 information remains visible at375px with no horizontal overflow, the driver restores1440px, waits for the real More button, expands it through its normal click if collapsed, then clicks its Templates link. The actual stale-cache/history/error assertions remain. This does not claim mobile Templates navigation.
- All prior independent-flag, preview/source-version, unknown/null-count, dirty409/Cancel, usage, phone and off/on checks remain. Optional screenshots were not added. Only source/log reads, whitespace search, `git diff --check` and SHA256 ran; no parser/compiler/test/browser/generator/helper/service execution by B. All other11 hashes match the provider-repair freeze.

## Browser2 stopped exact driver repair3

Root stopped browser2 with Chrome closed. Its log `specs/053-screen-completeness/evidence/checkpoint28-runtime/browser-template-risk-provenance2.log` timed out on raw fixture detail after the current provenance404. CONTROL was rechecked at generation56, `generation56_A_B_BROWSER2_REPAIR_D_AUDIT_TEST_ROOT_REVIEW`; the loan permits only the driver and this handoff. The input driver hash was root's `65df77e37aa96f19ca061f3d72a65590e981129ad9a714e835264e92eb1a8d7b`; all three optional TUESDAY_EVIDENCE_DIR screenshot hooks remain unchanged.

Source evidence: `web/src/components/hub/common.tsx:98-109` ErrorBanner deliberately substitutes `app.notFound` for every404, and `web/src/i18n/en.ts:1513` defines its nondisclosing text. The assertion now observes the exact current GET response404 for this fixture project after the real15-second stale window, checks no query string, then waits for that canonical error in the Template section's alert. It still asserts no provenance container and now explicitly checks no cached source-v3/newer-v5 facts remain in that section. No raw server detail is required to appear.

All other cases, response/method/ID guards, natural navigation and screenshot hooks are preserved. Fixture hash remains `8798e164f4aefcc40452b334e7c82b6a0df1972fca2d3283d6e8316afec6ec93`. Only reads, whitespace search, diff check and hashes ran; no parser/type/compiler/generator/test/Chrome/helper execution or application/backend/fixture changes. Repaired browser behavior awaits root's focused rerun.

Root visual review then found the raw `tpl.field.name` label because the separate39-row locale proposal suffix table had not yet been integrated. Root owns and reported integrating those exact translations. Under the same driver loan, B added a positive visible assertion for an actual `dt` field label exactly `Name` inside `[data-template-diff]`, before pagination. All three screenshot hooks and prior cases remain. No locale/component edit or runtime execution by B; root plans a fresh isolated SPA for its rerun.

## Frozen hashes

| Path | SHA256 |
| --- | --- |
| src/Hub.Api/Features/Templates.cs | `1167514c0a512d0d36cf867302ff6e9715ea8d5045b4c9e11a0a15bcb9ff15c2` |
| src/Hub.Api/Features/TemplateRiskSeeds.cs | `d7c3e8b9618c37be40bbd5062defb0ecf22fba617001d9c8683554a0fae3f535` |
| src/Hub.Api/Features/TemplateVersionProvenance.cs | `7cdc93de3fccb0773511191c08fc098f2f1e95381884669bcb1291cf4c7dbb60` |
| src/Hub.Api/Features/TemplateDraftDiff.cs | `547fe7013eef251ef5bb6005d9b6f69dd5cd0d97e6e0832ae2b8904960358f1f` |
| tests/Hub.Tests/Api/TemplateRiskSeedTests.cs | `a4632e3408af26adbe35d3cf7aadac48d18eecb0a762c9f3db87cdc675ac35b6` |
| tests/Hub.Tests/Api/TemplateVersionProvenanceTests.cs | `e855d4f50656fb167923c117b0e10f9de838a472169f479054c44b2e2fc2ce6e` |
| tests/Hub.Tests/Api/TemplateDraftDiffTests.cs | `cbe596654eeb79240e46e1c21ab59c15a889764ebf6c191cff0d96d213c1a183` |
| web/src/pages/Templates.tsx | `8fe4f56451e4eef0acd5ab6a78e95fd2b3edf3a56349ca3338824f92d7fb029f` |
| web/src/pages/projects/FromTemplate.tsx | `e94fb314f8d20e83cf0133a6d2d11ef5a5fdf9b1d042bb08a4fdfb4dfc9747b4` |
| web/src/pages/template-risk-seeds.tsx | `46f1e6fe78263beebabd0cad17be857119e23340edf620c6e055f1442eb08c84` |
| web/src/pages/template-version-provenance.tsx | `b9229725d6011a520e7a33f8ad04f005ef92e311722882d367b32db754bca5a3` |
| web/tests/template-risk-provenance.cjs | `75e8f3d2633c3e95689eeabf4189e63b1fc0e9183724492cbe80c87966344a05` |
| web/tests/template-risk-provenance.fixture.cjs | `8798e164f4aefcc40452b334e7c82b6a0df1972fca2d3283d6e8316afec6ec93` |

Executed only source/spec reads, whitespace search, `git diff --check` on the three existing owned files and SHA256. No .NET/Node/build/test/type/lint/EF/generator/Chrome/API/service/flag/dependency/helper/commit/push/deploy/Accepted operation by B. Schema application, runtime translation/serialization, generated types, locale/cache integration and actual browser behavior remain UNPROVEN.

Browser-only repair verification: source reads, whitespace search (no matches), `git diff --check` and SHA256 only. All11 held implementation/test hashes match the prior freeze. The browser files are untracked, so `git diff --check` alone does not inspect their content; the separate whitespace search covers those files. No syntax/type/fixture/browser/provider check was run.

Root unrun checks after all relevant source freezes: isolated API compile/migration; filter `FullyQualifiedName~TemplateRiskSeedTests|FullyQualifiedName~TemplateVersionProvenanceTests|FullyQualifiedName~TemplateDraftDiffTests`; adjacent TemplatesTests and ordinary project/risk snapshot/shape checks; additive golden diff/generation/plain recheck; exact locale/cache/fixture inventory integration then web+fixture type/lint; focused template-risk-provenance Chrome against an explicitly selected isolated TUESDAY_UI_BASE_URL, plus existing template/working-day/roles/basis/ProjectSettings regressions and visual captures as warranted. Preserve old QA24/runtime/output artifacts. No task checkboxes or human Accepted change here.

All13 app/test/browser files now HOLD. Repairs require a new exact root loan after execution stops.
