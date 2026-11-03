# Checkpoint 29 — Calendar, review disciplines and guarded task/notification additions

Local verification completed 2026-11-02. S15 and S36 are locally checked: **41/49** screen slices. S12-A and S45-A advanced, while full S12/S45 and S47 remain open. New maintained flags remain disabled. No commit, push, deployment, pilot activation or formal Accepted transition.

## Implemented boundary

- S15: explicit business layers (including all off), actual source-owner/work-discipline filters, canonical permitted Planner Away cohort, undated totals/page hydration, due-date-only task/deliverable moves, non-cascading milestone preview, phone controls and personal/project saved views. Fresh current authorization guards successful saved-view receipt replay, including a pending-to-completed race.
- S36: complete current Approved review population, discipline/sample/unknown denominators and UTC elapsed decision timing; configured deliverable-type defaults suggest disciplines only for new review packages. No reviewer is selected automatically. Original editor version, selection and uncertain intent survive background catalogue/read failure.
- S12-A only: all seven task toolbar operations across explicit versioned rows, at most200, one actor/request receipt and bounded transaction, per-row refusals, immutable activity, canonical obligations coalesced into count-only notifications, current-visibility replay sanitization. Workspace/project/departure entry points and original uncertain retry/navigation/focus/scroll are covered. No Undo offered; full task/non-task reversal is open.
- S45-A only: current canonical action context across all candidate notifications before paging/filter counts; typed primary/child validation, honest Unknown coverage/delivery, sibling follow menu and current-access stale-row removal. Actual64 distinct simple task contexts are batched; this is not a universal bound for every complex graph. Delivery transport, preferences/away hold and S47 remain open.

## Verification

| Check | Final result and evidence |
| --- | --- |
| API/solution build | PASS with recorded warnings; solution-build4.json and668 final C# source hashes |
| Migration/model | Reviewed additive2tables/3nullable existing columns, constraints/FKs/indexes; no seed, default, backfill or unrelated model drift; applied by provider factories |
| Provider tests |126 distinct final PostgreSQL-backed cases PASS in provider-final-results2.json; Calendar wire19-case rerun PASS overlaps those cases |
| Contract/registry | Reviewed8new paths/37schemas, no old path removal or old schema property/type/required change; generated/reverified golden and registry PASS; private receipt/scope fields excluded |
| Client checks |29 distinct final Node cases:28 original passes plus new meaningful cache regression; final keys21/21, app types5, unchanged fixture types2 and lint PASS with retained warnings |
| Calendar browser4 |PASS:3 simulated writes/9previews; real all-layers-off request, permitted Away, source filters, due-only conflict/restart, saved-view off/on and phone flow |
| Reviews browser3 |PASS9 cases: exact denominators/manual and late-source choices, actual restored draft, real15s native-tab refetch failures, original dirty/uncertain editor, read-only retries and explicit reconciliation |
| Bulk browser5 |PASS:12previews/12attempts/11logical commands/16fixture task changes/1legacy off command; all7ops, actual Back/Forward and blocked navigation, exact retry, local mappings, natural row/list focus and clamped scroll, project/departure paths |
| Notifications browser2 |PASS9 cases including finite combined GET filters, separate follow/open/read commands, Unknown zero-match copy, actual15s native-tab current403 removal and legacy off behavior |
| Combined visual review |4 separate read-only captures,8 images inspected, zero business writes; synthetic visual_system plus each owning feature flag, final SPA5. Static numbered copy in .review/checkpoint29-review/index.html |
| Final artifacts |668 C# and259 web inputs match final build snapshots;143 bundle files and142 public HTTP files match on8864; private .vite manifest is filesystem-only.1143 earlier files unchanged |
| Specification checks |Freshness PASS;1546 IDs/438sections/zero unknown;8decision tests PASS; final whitespace recorded separately |

Runtime evidence is in checkpoint29-runtime. Browser HTTP is finite and synthetic. Provider/API persistence and authorization are separate evidence; there is no new live Kestrel/browser join. Preview8864 proxies unused5099; no5099 API listener. Retained QA24/API5088 and earlier previews are preserved.

## Failures retained and repaired

Initial solution compile used a publication Token as Id; corrected the exact test snapshot. Initial bulk API validation wrongly rejected actual new-record version0; JsonRequired now distinguishes missing from valid0, without changing canonical equality. Three privacy fixtures passed unsupported Visibility to CreateBody; repaired fixtures use canonical PM/versioned Restricted PATCH and assert actual denied visibility. No permission guard was weakened. One frontend key and one typed data fixture were corrected.

Actual Calendar HTTP exposed shared serializer omission of empty types: only the new screen now accepts standalone types=none; mixed/unknown values still refuse, omitted remains all. Its browser driver now awaits real URL/pressed/data transitions and uses attached plus enabled Confirm for a genuinely empty warning-free preview. Existing real warning visibility checks remain strict.

Independent integration review found nonTask action changes left the new notification screen fresh in cache when a muted/unfollowed project pulse did not change. Root added only the enhanced notifications/screen prefix to every canonical/evaluated invalidation, preserving legacy roots. A realQueryClient regression forces fresh action counts and proves old personal caches unchanged.

Actual Reviews browser2 found ErrorBanner Retry buttons submitted their enclosing uncertain form. The exact shared Retry button now has type=button; the base Button default is unchanged. Browser3 proves each Retry performs only its read, preserves the same editor/intent and closes only on explicit matching Check saved defaults. The failure did not establish an unintended PUT.

Other strict-driver fixes preserve semantics: correct existing Confirm label, exact full preview status including its hint, fresh page rows before page-header selection, exact footer Close instead of the identically named close icon, and waits for Radix's real deferred autofocus. No focus/cache/app-clock injection or relaxed expected outcomes was used. All failed logs/preimages remain preserved.

## Open work and user decisions

Remaining slices: S12, S16, S35, S37, S38, S45, S47, S49. R3 legacy unknown-authorship policy and §8 template-management policy still need Jay's choices. Full83 permission equivalence/deactivation, broad052/native/zoom/pilot/human acceptance remain open. New G58 plans are documentation only until an exact root source freeze.

QA24 cleanup remains unchanged: automatic approval review previously rejected stopping API4323 and dropping hub_qa_checkpoint24 without explicit confirmation. That rejected action executed nothing; no workaround/cleanup was attempted.
