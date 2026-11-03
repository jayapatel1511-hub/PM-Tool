# 053 S05 — exact next-batch proposal

Prepared read-only during freeze-all generation 10, 2026-10-31. This is a proposal, not implementation or acceptance evidence. No application/test files changed; no checks, builds, generators or helpers ran. All sources remain frozen.

## Outcome and boundaries

Deliver FR-SCR-05/AC-SCR-05 and FR-SCR-06/AC-SCR-06 under `screen_patterns`. Submissions leads with actual blocking checks and exact actions; Readiness lists the full selected work cohort in canonical readiness order. Both retain current commands, permissions, concurrency, issue fingerprint checks and immutable issue snapshots. No percentage, new stored state, assignment rule, Task Waiting/Blocked materialization, schema or migration is required.

Implement as two reviewable increments: S05-A Submissions, then S05-B Readiness. S05 stays unchecked until both increments and the full AC evidence pass root verification.

## Observed source seams

| Current source | What exists | Remaining gap |
|---|---|---|
| `Submissions.cs:400–437` | List rows derive status with `SubmissionReadiness.Evaluate`; `BlockerCount` is raw blocker-reason count. Ordinary list pages are selected before live evaluation. Draft/terminal rows use zero when readiness is not evaluated. | Global blocker-first ordering cannot be obtained by sorting one browser page. A summary count is not a complete checklist or proof of zero blockers. |
| `Submissions.cs:452–477` | Detail returns current manifest/check/evidence rows, one issue record, existing command capabilities; enabled checks have `EffectiveStatus` and `Context`. | No explicit complete/partial presentation contract, current-head manifest comparison or deliverable-status counts. Enabled new facts should be hydrated in one authorized snapshot. |
| `SubmissionReadiness.cs:39–47` | Each live blocker already carries internal `ContextInfo.CheckId`, exact source kind/id and knownness. `AssessedCheckIds` guards derived Pass. These fields are intentionally JSON-ignored. | Expose a narrow enabled presentation projection, not inference from `Kind`/raw totals or serialization changes to the legacy blocker/fingerprint contract. |
| `SubmissionContextData.cs:61–83`, `RecordContextReader.cs:290–326` | Canonical derived check state, checking owner eligibility and exact source contexts already exist. Missing sources remain unavailable. A finding source can be the exact `ReviewFinding`, not its package. | Use these facts to sort/group checks and link the source's exact action. Existing raw `SourcePath` sometimes points to an enclosing review/change package and cannot substitute for the exact source. |
| `Submissions.tsx:152–179` | Raw blockers grouped by discipline/owner; manifest appears first, then raw blocker groups, then unsorted checks. Check context/effective state and exact child recovery exist. | Reorder enabled presentation, group by checking owner then discipline, add reason/action links and compact header, move counts/manifest after checks. |
| `Readiness.cs:800–829` | Detail evaluates detached canonical checks and returns checks/reasons, exact source records, unknown/blocked codes and exceptions. Allocation facts already apply allocation visibility. | Does not return state-based source action contexts or named applicability actors as a screen grouping contract. |
| `Readiness.cs:864–932` | The shared window reader finds dated open Task/Deliverable work, evaluates assessments and **returns only Ready outputs**, plus active constraints. It keeps separate paging/totals. | Not Ready, Needs Assessment, Proceed under Assumption and work with no assessment are absent from the output list. Sorting existing `readyOutputs` cannot meet AC-SCR-06. |
| `Readiness.tsx:112–120,159–200`; `ReadinessForms.tsx:77–151` | Window, constraints, Ready outputs and weekly promises remain distinct; inspector has check reasons/source links and guarded applicability/constraint commands. | Add the full work-state list before the retained constraints/Ready/promise sections, plus actor/discipline grouping and exact next-step links in the inspector. |
| `WorkReadinessInputs.cs:18–75` | S03's detached evaluation/source-context projection already handles Task sources, direct Handoff versus constraint provenance, missing assessments and unknown facts. | Task-only and lacks per-check reason/all-check rows. Generalize its internal target read for Deliverables and reason projection without changing existing S03 JSON or canonical rules. |

## Narrow file ownership

S05-A lane edits, only after an edit window opens:

- `src/Hub.Api/Features/Submissions.cs`: enabled list/detail presentation adapter integration and authorized repeatable-read snapshot; retain all command code.
- New `src/Hub.Api/Features/DeliveryScreenReads.cs`: read-only, same-project check-to-blocker mapping; explicit availability; manifest/head comparison; status counts over actual manifest members. Reuse existing canonical state/context helpers.
- `web/src/pages/projects/Submissions.tsx` and new `web/src/pages/projects/delivery-presentation.tsx`: enabled ordering/groups/header/action links, inline manifest, unavailable presentation and status counts. Disabled JSX behavior retained.
- New `tests/Hub.Tests/Api/WorkDeliverySubmissionTests.cs` and `web/tests/work-delivery-submissions.cjs`.

S05-B lane edits:

- `src/Hub.Api/Features/Readiness.cs`: additive enabled full-work read (or omitted-null full-work section on the existing window), evaluated/sorted before independent paging, keeping existing Ready/constraint/promise contracts.
- `src/Hub.Api/Features/WorkReadinessInputs.cs`: shared internal Task/Deliverable source hydration, preserving existing Task wrapper and serialized fields; append only deliberately specified enabled presentation data.
- `web/src/pages/projects/Readiness.tsx`, `ReadinessForms.tsx` and the new delivery presentation helper: full work-state list, actor/discipline groups, exact inspector/check URLs. Reuse `WorkInputs.tsx` facts if needed; no broad component rewrite.
- New `tests/Hub.Tests/Api/WorkDeliveryReadinessTests.cs` and `web/tests/work-delivery-readiness-order.cjs`.

Root-owned requests only: locales, generated API/registry, saved-view/query metadata and invalidation keys for any new list query. If named applicability-actor hydration needs a core context adapter, request that exact adapter from root; do not edit `RecordContextReader`, `RecordContextRules`, permissions or registry from this lane. No EF/entity change is proposed. Existing canonical evaluators and commands remain untouched except a separately reviewed query-translation repair, if needed.

## Canonical projection and display

### Submissions

1. Use `SubmissionReadiness.Evaluate`, `AssessedCheckIds`, `ContextInfo.CheckId` and `reader.SubmissionCheckStatus(check.Id)`/check context from the **same** snapshot. Map each reason only to its recorded check and exact source. Never match a blocker to every deliverable sharing a kind, all findings in a package, or every manifest row.
2. Header: effective state, target date, coordinator and explicitly defined blocker count. Sort Fail, Pending, Pass, Not Applicable; place unavailable/unassessed rows explicitly before successful rows, never treat them as Pass. Preserve checking owner as the grouping owner, even when a source's next actor is a receiving owner/verifier. Failed/Pending groups are owner then project discipline; unknown/inactive/unassigned identities stay labelled.
3. Render reason(s) and link the action using each exact source `RecordContextInputView` and `recordContextActionLabel`. Example: a Responded finding links `ReviewFinding:<id>` with Verify closure; a Submitted Handoff links `Handoff:<id>` with Accept. Manual applicability links the exact `SubmissionCheck:<id>` and retains existing sign/reassign permissions. Unknown source/action says unavailable/not assessed, without substituting its package.
4. After checks, count **actual current manifest-member deliverables** by current status, naming the basis. Missing members have an explicit unavailable count. No completion percentage or implication that a status count establishes Ready.
5. Manifest opens inline. Resolve each stored manifest revision and its current registered head by exact same-project source identity (`Coordination.Identity`/`Head`), checking that the head revision belongs to that deliverable/source. Show manifest revision, current registered revision and mismatch independently. Do not use `changes/options.sources` as proof of full manifest membership or current-head completeness.
6. Availability is explicit for checklist, manifest, current revision, status counts and issue data. Missing fields in a summary-only/partial capture produce unavailable sections, not `[]`, zero, Pass or invented membership. Distinguish a fully read unissued package's known no-issue fact from an Issued package whose issue record is absent/unavailable. Keep immutable issue snapshots separate from the labelled current comparison; do not invent issue history from current package totals.
7. Enabled list ordering must evaluate the filtered full package cohort **before** paging; preserve all existing filters and counts. Keep any existing ceiling explicit and fail/disclose unavailable rather than silently sorting a sampled subset. Preserve off query shape/order/count behavior.

### Readiness

1. Reuse `WindowData`'s authorized date/cohort selection and `EvaluateCurrent`; evaluate detached canonical check records. Include selected work with no assessment as HasAssessment=false/Needs Assessment, never create an assessment during GET. Preserve unknown/malformed/removed-owner handling.
2. Order Not Ready, Needs Assessment, Proceed under Assumption, Ready, with deterministic due date/key/type/id ties. Sort before paging and keep independent work paging/totals distinct from constraints, Ready outputs and promises. Do not drop existing sections, weekly snapshots, reliability, exports or signature actions.
3. Hydrate exact canonical source actors/actions through the existing context reader. A Submitted Handoff groups under its receiving owner. A Resolution Proposed constraint groups under its affected work owner and keeps its removal owner separately visible. A pending Decision groups under its recorded owner. An external party remains external; no viewer fallback.
4. For missing applicability, `RecordedBy` is historical provenance, **not** its next actor. Use the current PM/discipline-lead permission/role context to present an explicit eligible-role/multiple-actor group. A missing eligible actor stays unknown; do not store an invented check owner. Source-less unknowns remain visible alongside known blocking checks.
5. Link exact sources through registered routes and existing capture/return helpers. Applicability links the existing work inspector plus the canonical check code (a UI selection, not a new entity kind), preserving window/filter/page/scroll and current permission guards. An aggregate check failure must not label each source individually failed where the evaluator only knows the aggregate result.

## Acceptance checks to author, then root executes

| Case | Required evidence |
|---|---|
| AC-SCR-05 exact ordering/count | One Fail, two Pending, five Pass rendered Fail first then owner-grouped Pending, header 3 blockers under the settled count basis, no percentage. Establish source failures/pending states through normal commands; optional Pass may make the eight-row fixture. No direct rewriting of required flags or derived check status to force it. |
| Exact membership/provenance | Two manifest deliverables: failure explicitly mapped to D001 never appears on D002; unrelated round findings are not reassigned to another deliverable. Multiple reasons/sources retain their exact check IDs and source IDs. |
| Exact action/return | Real clicks on Responded ReviewFinding → Verify closure and Submitted Handoff → Accept open the exact record; closing restores the package/check selection, filters and main/dialog scroll. No enclosing-package fallback. |
| Optional failure boundary | Existing optional applicability Fail remains visible but does not become a required issue blocker. Existing Ready/Issue behavior is unchanged. |
| Manifest/current revision | Normal publish A→B preserves manifest A, shows current B/mismatch, invalidates unissued readiness canonically, and leaves issued snapshots byte-stable. Deleted/foreign/missing head/revision remains unavailable without names from another project. |
| Partial captures | Summary count only; missing checks; absent manifest; missing passing-check detail; Issued with unavailable issue record. Each is explicitly unavailable, never a fabricated empty complete list or per-deliverable failure. |
| AC-SCR-06 state order | Normal setup yields Ready, decision-blocked Not Ready and Needs Assessment work. Not Ready/Needs Assessment precede Proceed under Assumption/Ready across page boundaries; exact Decision appears under owner with Decide. Include a no-assessment item and prove GET writes no assessment/check rows. |
| State-based actors | Submitted Handoff → receiving owner; normal constraint Open→Resolution Proposed changes the action group to affected work owner while retaining removal owner. Handoff acceptance still does not resolve an independent Open constraint. |
| Applicability and unknown | Current PM/DL eligibility, readonly refusal, multiple eligible actors, inactive/removed actor, source-less unknown check and external Decision actor; do not use RecordedBy or viewer as fallback. |
| Parity/guards | Exact disabled list/detail/window JSON and existing UI order remain; extra presentation fields/queries absent off. Existing source fingerprint recheck, immutable issue, review independence, optimistic concurrency, atomic activity and permission refusals retain their tests. |
| Reflow/recovery | Desktop/mobile/zoom order, keyboard manifest expansion, readable group labels, real link/return chains and paging/window reset. No hidden data to satisfy layout. |

Retain relevant existing suites: `SubmissionContextTests`, `SubmissionApiTests`, `SubmissionPermissionMatrixTests`, `ReadinessApiTests`, `ReadinessInvalidOwnerTests`, `ReadinessConstraintLinkTests`, `ReadinessSubmissionGateTests`, `ReadinessExportTests`, `WorkReadinessSourceEvidenceTests`; browser `submission-context.cjs`, `readiness.cjs`, `work-delivery-readiness-links.cjs`, `work-delivery-related-return.cjs`. Run the smallest affected subset at each root freeze, then broaden only for a concrete failure/risk. Current notes do not claim any was run.

## Contract choices for root before writing

1. **What is one header blocker?** Existing `ListRow.BlockerCount` counts raw reasons; multiple reasons may map to one required check, and an optional Fail must not prevent Ready. Recommended enabled presentation: count distinct canonically blocking check IDs; disclose any unmapped package-level problems separately with their explicit completeness. Preserve legacy raw field off. Confirm this basis for source-rich multi-blocker cases instead of silently changing the old count definition.
2. **Which work enters the new Readiness list?** Current window excludes terminal/held work and work without a due date; constraints are independently included when needed by the window end. Recommended bounded default: reuse that dated open-work cohort, include its unassessed work, disclose the date scope, and retain constraints independently. Extending to overdue, undated or held work needs an explicit scope choice, not a hidden widening.
3. **Applicability grouping contract.** No assigned next actor exists for applicability and no registered readiness-check entity exists. Recommended: a read-only eligible PM/discipline-lead group (explicit multiple/unknown states), link the existing inspector to the work/check code, and leave canonical manage permission authoritative. Root should settle the named-actor/role-label shape and exact query key before the lane writes it; no new owner assignment/model is implied.
4. **Issued/live comparison basis.** Recommended: immutable issued manifest/check snapshots remain issue evidence; current registered revision/status is labelled live and timestamped. Missing frozen/live parts remain separately unavailable. No new issue-history endpoint is inferred from the single Issue row.

These are root integration choices for a concrete read-side contract. If any requires changing canonical §10 state/permission/assignment semantics, stop that dependent work and record an open decision for Jay; otherwise use the existing authorized additive implementation boundary without creating a new approval loop.
