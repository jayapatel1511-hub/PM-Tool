# G55 / checkpoint27 — B Files reader handoff

2026-11-02. READY for root source review, then HOLD. Source authored only; compilation, real-provider execution, browser and local acceptance are UNPROVEN.

Checkout: `/Users/jaypatel/.codex/worktrees/ef94/PM-Tool/.build-052-053`, branch `codex/build-052-053`. CONTROL rechecked at generation55, `generation55-S44-source-authoring-no-execution`. Implemented only the frozen B loan. No helpers were started.

## Owned change

- `src/Hub.Api/Features/Workspace.cs`: append null-omitted FilesRead.Screen; add the three raw string query controls and injected clock to the Files handler; dispatch only while people_screens is on. The legacy Files body/order/count meaning stays as the off branch. Prior Home/Team/other changes are preserved.
- New `src/Hub.Api/Features/FileScreenReads.cs`: frozen top-level FileScreen DTOs, one RepeatableRead snapshot and one clock instant; coherent authorized owner query; global discipline, actual AddedBy and organisation-local Monday/week/asOf predicates before the 2000-link cap; title/ID order. Known-user AddedByUser and visual identity colour projections are preserved.
- New `tests/Hub.Tests/Api/FileScreenTests.cs`: nine facts plus 12 theory cases (21 authored provider cases), explicitly tied to FR/AC-SCR-77/84. These are authored, not executed.

Shared dependencies are C's actual top-level DocumentLinkReferenceContext/ReadContexts/Flag and canonical CanAdd/CanChange made internal without body changes. This lane does not edit those files or duplicate reference comparison/registration logic. Root's six schema fields are consumed, not defined here.

## Authority, counts and missing data

Actual same-project live Project/Task/Deliverable owners are checked before link figures/caps and author choices. Unknown, deleted, foreign or mismatched owner rows cannot fill the cap or expose metadata. Task uses its own project discipline; project links have none. The author's ID filter still supports the actual stored orphan AddedBy value, but no User descriptor or choice is invented for it.

ProjectContext inputs match Access.Context: current nonremoved viewer membership Roles/PrimaryDisciplineId and all viewer-led project-discipline IDs, with the same absence of an IsActive predicate on lead facts. Both input sets are batched and grouped; existing permission functions decide project CanAdd, per-link CanChange and missing-row CanAddLink. No per-item/per-link authority queries are added by B. On Hold is not made read-only by this feature.

Choices are scoped and independent of selected link controls: global disciplines represented by coherent scoped work and actual users who authored coherent scoped links. Each has cap200+sentinel, stable name/ID and an explicit truncation flag, including historical/inactive labels. The missing list has its own exact count and stable project-number/key/ID first2000+sentinel. Only scope, global discipline and deliverable key/name search apply to it. The direct-live-link anti-join ignores all link filters and requires the same ProjectId; task/project links do not satisfy it. Issued/Accepted/Cancelled/deleted deliverables and excluded projects are omitted.

There are no SaveChanges, tracked entity changes, Seen/Following acknowledgements, remote requests, email operations or business commands in the reader. C's batch reference context is reused for each shown actual ID/version.

## Authored provider evidence cases (UNRUN)

1. Two projects' Civil disciplines share the global ID; an Electrical task with a Civil parent retains its own discipline. Combined controls use actual AddedBy, not reference registrar. Normal reference commands produce the exact C-versus-D Different context and Manual provenance.
2. Same-name authors remain distinct; deactivation is historical; a real orphan retains null caption/no Person/choice while filtering by its actual ID works. Unknown valid IDs do not disclose labels.
3. Independent missing anti-join includes all six open statuses including On Hold; excludes the three terminal statuses and deleted deliverables. A hidden-by-filter live direct link still suppresses missing; canonical deletion restores missing; task/project links do not satisfy it.
4. PM, author, discipline lead, viewer, nonmember and Read Only permissions reflect real canonical API behavior. Inactive lead facts match Access.Context. Refused role/version/project-state commands leave captured state unchanged; current metadata version changes after a normal successful edit. Archived scope offers no stale rows/actions.
5. Restricted, archived/cancelled projects, deleted/missing owners, wrong-project links and their authors stay scoped. An incorrectly project-tagged direct link cannot suppress the real visible missing deliverable.
6. Eight malformed-ID/boolean cases: ignored with exact old wire off, field-specific refusal with no side effects on.
7. Four actual organisation-zone boundary cases: Halifax DST-change week, UTC Monday while still local Sunday, exact Halifax Monday and Auckland Monday. Previous-boundary/future/registration-time rows do not become current-week AddedAt activity.
8. A late eligible row beyond2000 is still found by each new predicate separately and combined; equal titles use actual ID order. Link total keeps the shown-count meaning and truncation is honest.
9. Missing has exact total2001, items2000 and truncation even when link-only filters yield zero links. Choice fixtures201 prove stable first200 and sentinel flags independently of selected filters.
10. Flag off after stored-on metadata omits screen and AddedByUser while preserving old field order/shape, actual UNC string/indicator and existing visual colour behavior; re-enabling retains manual comparison facts.

Read-only state captures activity, notifications, email/outbox/receipts, Seen state and private visit preferences, links/reference provenance/versions, tasks, deliverables, follows and source/head/input counts. Bulk fixtures are labelled synthetic history/scale fixtures; real permission/reference/refusal assertions use canonical API commands.

## Checks and handoff hashes

Executed: source reads, owned whitespace inspection, `git diff --check -- src/Hub.Api/Features/Workspace.cs`, source hashes. No build/test/typecheck/EF/generator/browser/API runtime/dependency/flag/commit/push/deploy operation was run by B. Test bodies contain disposable data/flag operations for later root execution only.

SHA256 at handoff:

| File | SHA256 |
| --- | --- |
| `src/Hub.Api/Features/Workspace.cs` | `9a859e9ff88ee6110ced47b327fc7720d21561b009baf40c1f84e8525e51b19b` |
| `src/Hub.Api/Features/FileScreenReads.cs` | `264751a6b7898ed9416488096fbd72eb4ab0728217f82660a1a46f9e13bd6080` |
| `tests/Hub.Tests/Api/FileScreenTests.cs` | `625f1284f6b57b0e9ddd0f4d194b22167b2105c4d8c1acef14614cfb954dbe21` |

Root has already acknowledged ownership of the narrow WorkspaceFilePersonTests.WithoutPeople change stripping only the intentional new top-level screen, preserving the existing Person and legacy assertions. B did not touch that test.

Exact unrun checks for root, after all C# authors HOLD: isolated API compile; real PostgreSQL `FullyQualifiedName~FileScreenTests`; C's DocumentLinkReferenceTests/DocumentLinkReferenceReadTests; adjacent WorkspaceFilePersonTests/WorkspaceContractTests/WorkspaceTests and CollaborationTests document-link cases; additive DTO golden generation/diff/plain recheck and generated TS; affected web/fixture types and focused A/D Chrome/off/375/Person-return checks. Root owns output locations, disposable DB lifecycle and the exact execution commands. Provider translation, current schema, browser behavior and end-to-end local acceptance remain unverified until those checks run.

No specification/decision/task status, flag activation, old QA24 cleanup or Accepted change. B app/test sources now HOLD; repairs require a specific root loan after execution stops.

## Source-review repair — anonymous provider projection

Root's independent source review identified that constructing positional MissingRow before subsequent IQueryable member predicates/order could prevent provider translation. Root stopped execution and issued the exact reader-plus-handoff repair loan; CONTROL was verified at `generation55-B-observed-provider-projection-repair-no-execution`.

Replaced only the missing query's positional record projection with an anonymous scalar projection, retained through the same discipline/search predicates, exact CountAsync, stable ordering, sentinel Take and ToListAsync. The existing public FileScreenMissingDeliverable construction stays after materialization. Removed the now-unused private MissingRow record. No membership, status, anti-join, scope, count, cap, permission, time, public DTO or test behavior was changed.

Pre-repair helper hash: `3c07619366bb8a190610e338d334cf21e4f593ba9b3cd9d01b7c46ce17635878`; repaired hash is in the current handoff table. Workspace and FileScreenTests hashes remain identical. Executed only source inspection, whitespace search and SHA256; no compile/provider tests/generation/helpers or runtime operations. Existing provider tests, especially missing search/discipline/cap cases, remain the unrun translation proof. READY for root re-review, then HOLD again.
