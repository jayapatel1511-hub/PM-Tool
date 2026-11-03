# G56 S41 Issue root-cause contract freeze

Root2026-11-02. Reviewed C plan g56-issue-root-cause-plan.md governs with these explicit refinements. No implementation/runtime acceptance yet.35/49 local slices checked; no new permission or §10 behavior. `register_screens` owns the addition.053-D14 stays Open; no starter catalogue seed, Other default or historical backfill.

## Schema and one catalogue

Root adds `IssueRootCauseCategory : RefData`, `HubDb.IssueRootCauseCategories`, nullable `Issue.RootCauseCategoryId` and `RootCauseLabelSnapshot` max120 with both-null-or-complete/nonblank pair constraint. Nullable Restrict FK. No Resolved-implies-nonnull constraint: off/legacy resolutions remain valid. Root owns Data/migration/registry/audit labels.

Root registers `/api/v1/admin/issueRootCauses` with existing private Admin.Ref, same RegisterVocabularyCreateGuard, canonical Admin/version/audit/usage/retirement; no second CRUD implementation or C Vocabulary.cs. Root enforces trimmed/nonblank/no-control name120 and case-insensitive unique names, normal validation/duplicate409 responses. Usage includes retained/deleted historical issue references, distinct projects. No hard delete. Inactive labels remain readable, but cannot be newly chosen.

## Resolution, history, atomicity and replay

Append only optional ignored-null RootCauseCategoryId to IssueMove END; exact original request property order/IssueMoved reply remains. New read values append optional null-omitted metadata off; IssueRootCauseValue(CategoryId,RecordedName) is resolution-time label, not current catalogue rename. Root appends optional `IssueRootCauses` choices to Me.ReferenceView; C supplies IssueRootCauseChoice(Id,Name,IsActive,SortOrder).

Fresh flag-on Resolved needs explicit active valid nonzero cause ID; missing/null/unknown/wrong-kind/retired400. Non-null cause on other transitions400, non-null feature body off404. Omitted/null old bodies off unchanged. Never bypass canonical version/state/edit/correction/text/date/Coordination-reference/evidence/current independent-verification/pending-impact gates.

Enabled resolution transaction must hold a PostgreSQL **FOR SHARE** lock (or explicitly reviewed equivalent blocking all category UPDATE/DELETE) on the validated category row through issue SaveChanges/commit. FOR KEY SHARE is insufficient for non-key retirement. Validate actual Name/IsActive under that lock; concurrency test proves atomic validated snapshot or refusal, no partial issue/pair/activity/version. Reuse existing transaction if present; no global transaction/framework changes. Exactly one normal Resolved activity/version increment, no new cause/approval event.

Reopen retains last Resolution+cause pair as historical facts and clears ResolvedDate as today; it immediately leaves resolved counts. New on re-resolution requires explicit active selection. Fresh off re-resolution clears pair to unknown, avoiding old classification revival; canonical audit retains prior facts. Ordinary text edit retains pair; no resolved cause-correction endpoint this batch. Show retained cause on reopened rows as last-resolution history.

C adds marked narrow IssueRootCauseReplay auth-only guard; root mounts in IdempotencyMiddleware. Charset-aware buffered JSON/stream restoration; non-null feature body checks current flag/current issue/project edit authority before claim/replay. Do NOT rerun stale version or legal transition from current state on successful receipt. Omitted/null legacy receipt behavior and unchanged generic no-body-hash semantics remain. UI new-resolution retry freezes route/body/key/observed version after uncertainty/in-progress; never fresh version/key promotion. Validate actual three-field reply/id/state/version before clearing. Cancel and leave guards remain.

## Counts, list, views and exports

Project-wide summary `/projects/{id:guid}/issues/root-causes`: all live currently Resolved issues in exactly the current authorized project, independent of row filters/page/group expansion. Repeatable snapshot for counts/labels. Known category counts+NotRecordedCount=ResolvedTotal; corrupt pair/FK cannot silently disappear. Summary labels use actual current catalogue name/retirement; each issue retains RecordedName. Unknown legacy separate, never Other. No business/Seen/receipt writes.

Append raw optional RootCause to RegisterQuery; on accepts one nonempty Guid or not-recorded, malformed400, unknown well-formed zero without name leakage. Off ignores new raw value, including malformed. Only IssueQuery uses it; RiskQuery does not. Exact resolved predicate is shared by list and export. No new export column: **all existing Issue export columns/order remain exact on AND off**, only requested cohort changes.

Summary click builds status=Resolved&rootCause=id|not-recorded and deliberately clears all conflicting ordinary/source filters, page, panel and active saved view; preserve only grouping/sort/column preferences. Token removable/Clear; no invisible person/location/verification constraint remains. Root owns issue-only ScreenLists/Views/root export-query propagation; no global filter loosening. Give root exact required key/seams after source read.

## Exact ownership and UI

C owns existing `src/Hub.Api/Features/Registers.cs` and `web/src/pages/projects/Registers.tsx` (issue-cause seams only; preserve Risk and other issue behavior).
NEW `src/Hub.Api/Features/IssueRootCauses.cs`, `IssueRootCauseReplay.cs`; NEW `tests/Hub.Tests/Api/IssueRootCauseTests.cs`, `IssueRootCauseReadTests.cs`, `IssueRootCauseVocabularyTests.cs`; NEW `web/src/pages/projects/issue-root-cause.tsx`, `web/tests/issue-root-cause.cjs` and fixture. Own handoff/exact locale proposal docs allowed. NO new duplicated vocabulary endpoint or Admin/Me/Views/ScreenLists/ListExports/global middleware/schema edits.

Root owns shared schema/registry/catalogue/ref reads/view whitelist/export query/middleware/locale/cache/generated/build/runtime/Chrome. Supply exact public helper/marker signatures and locale keys+English copy early. Unique generated IssueRootCause-prefixed DTOs only, no shadows/any. New read cache keys include viewer/project/flag; hide enhanced cached counts/choices/permissions on refused current key. Keep existing actual inspector/source URL/nonzero-scroll/focus/phone/dirty-form behavior.

No execution/helpers/services/flags/dependencies/commit/push/deploy/Accepted. Test real AC71 plus >50/group duplication, typed IDs/unknown/retired/renamed snapshots, exact list/export drill cohort, canonical Coordination refusals, row-lock race, atomic mixed failure, UTF8/UTF16/off/revoked receipt replay, old wire/legacy stored data and actual-SPA strict synthetic interaction. All authored checks UNRUN until root source freeze/execution.
