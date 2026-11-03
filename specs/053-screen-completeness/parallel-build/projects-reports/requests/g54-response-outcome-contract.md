# G54 R2 — response outcome and package response sheet

**READY contract only, 2026-11-02.** This is C's only changed file for the next-batch planning loan. Current application/test/schema inputs and runtime stay frozen; no code/test/schema writes, execution, generation or helpers. This contract addresses FR/AC-SCR-61's remaining response/outcome/export work. R3 external provenance and FR/AC-SCR-62 remain separate and OPEN; R2 alone cannot close S35.

## Smallest data change: reuse the existing explanation

**Do not add OutcomeReason or another response-text column.** [FindingCommand](/Users/jaypatel/.codex/worktrees/ef94/PM-Tool/.build-052-053/src/Hub.Api/Features/Reviews.cs:340) already calls [Check.Reason](/Users/jaypatel/.codex/worktrees/ef94/PM-Tool/.build-052-053/src/Hub.Api/Infrastructure/Errors.cs:40), requiring at least five trimmed characters, then writes the result to `ReviewFinding.Response` and `FindingEvent.Reason`. FR-SCR-61 requires a reason for Not incorporated, not a second, distinct explanation. The existing response text satisfies that requirement. Label that same input “Why it was not incorporated” for this outcome; keep normal response text mandatory for the other outcomes too. Do not invent semantic/NLP checks or weaken the existing reason/evidence rules.

Add exactly two nullable string properties, each max32, default NULL:

| Entity | Property | Meaning |
|---|---|---|
| `ReviewFinding` | `ResponseOutcome` | Outcome belonging to its currently retained Response/EvidenceUrl. |
| `FindingEvent` | `ResponseOutcome` | Immutable outcome of that particular Responded event; NULL on every other action. |

Allowed exact values: **Incorporated, Not incorporated, Clarified, Noted**. Root's single schema lane owns nullable columns, enum-value checks, event check “non-null only when Action=Responded,” and audit registry entries. A current-finding check may require Response non-null when outcome is non-null; it must not require Status=Responded because return/verification retain the last response. Existing NULLs remain unknown. No default enum, inferred legacy result, backfill, new workflow state, extra event or new receipt table.

## Existing command and legacy-hash contract

Keep POST `/projects/{projectId}/reviews/{id}/findings/{findingId}/action`, its `Coordination.AtomicCommand` metadata, result `{id,rowVersion}`, request-ID/version/permission checks and existing operation envelope. Append only this optional property to the existing FindingAction record, after all six old properties:

```csharp
[property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
string? ResponseOutcome = null
```

JSON is `responseOutcome`; keep the established six properties (`requestId,rowVersion,action,reason,evidenceUrl,ownerId`) and their order/null serialization exactly as before. [Coordination.Hash/Run](/Users/jaypatel/.codex/worktrees/ef94/PM-Tool/.build-052-053/src/Hub.Api/Infrastructure/Coordination.cs:15) hashes the serialized typed envelope `{operation:"review.finding.action",id,findingId,body}`. **Omitted or explicit-null new property must serialize identically to the old body.** Apply WhenWritingNull only to the new property; changing global JSON ignore policy would remove old null fields and break hashes. Do not reorder/rebuild the envelope, change operation, or normalize old Reason/EvidenceUrl/OwnerId/RequestId before hashing. In particular, old raw whitespace in Reason stays in the receipt hash even though Check.Reason trims its stored response inside the callback. Reject invalid non-null outcome spelling rather than silently trimming/case-folding it before hashing.

| Request / flag condition | Required behavior |
|---|---|
| register_screens off; outcome omitted or null | Existing command behavior and exact old hash/reply shape. A fresh legacy Responded response stores NULL outcome. |
| register_screens off; non-null outcome supplied |404 before the command/receipt path; existing routes as a whole remain available. This guard also hides replay of a body containing the new feature field while off. |
| register_screens on; fresh Responded command | Require one allowed outcome plus the existing Check.Reason and valid evidence URL. Missing/null/unknown outcome or missing/short reason refuses400 with zero finding/event/receipt effects. |
| register_screens on; non-Responded action supplies non-null outcome |400: the field cannot annotate assign/verify/return/withdraw/acknowledge. Omitted/null keeps old behavior. |
| Exact previously committed old body retried after upgrade/flag change | Old hash still matches and returns the stored result without another response/event, stale-version reapplication or metadata backfill. New missing-outcome validation must run **inside** the existing command callback, after prior-receipt lookup, not before it. |
| Retained requestId reused with a non-null/different outcome or changed old field | Existing payload-hash mismatch refusal; never silently rewrite the prior receipt. |

Fresh flag-on commands missing the newly required outcome are refused; an old committed request still replays. Deliver the flag-on response form and these rules in the same frozen batch. Do not claim that omitted new fields authorize a fresh flag-on outcome-less response. Current Coordination.Run replay still uses its existing current project/CoordinationWrite gate; R2 does not silently redesign named-person replay policy or broaden the shared helper.

## Current response, event, carry and wire mapping

- On a successful Responded command, set Response to the existing validated reason, EvidenceUrl through the existing URL validator, and ResponseOutcome to the validated value while on, otherwise NULL. Set only that new Responded FindingEvent's outcome to the same value. All remain in the same canonical transaction/version/audit/recompute/notification flow. No second “outcome recorded” event.
- A response remains **Responded**. Incorporated/Noted never means Verified Closed, Approved, Issued or Accepted. Preserve the named independent verifier, author/carry/responder checks, required evidence and [canonical Recompute](/Users/jaypatel/.codex/worktrees/ef94/PM-Tool/.build-052-053/src/Hub.Api/Features/Reviews.cs:209), including source revalidation and submission invalidation.
- Verify/return/withdraw/acknowledge leave the last response/outcome together as historical facts; their new events have NULL outcome. A later Responded action replaces the current response/outcome and appends a new immutable response event. Do not copy an earlier event's outcome into a new response that did not supply one.
- assignResolver currently clears Response/EvidenceUrl and returns Open: also clear current ResponseOutcome, even if the feature has since been switched off. Keep old events intact. assignVerifier does not replace response metadata.
- Both [manual new-round carry](/Users/jaypatel/.codex/worktrees/ef94/PM-Tool/.build-052-053/src/Hub.Api/Features/Reviews.cs:257) and [published-revision advance](/Users/jaypatel/.codex/worktrees/ef94/PM-Tool/.build-052-053/src/Hub.Api/Features/Reviews.cs:197) already create a new Open finding without response/evidence. The new finding's outcome is also NULL; old finding/event outcomes remain attached to the old revision/round via real CarriedFromId. No approval/response is silently reused for changed content.
- Append optional `responseOutcome` with WhenWritingNull to ReviewFindingRow, ReviewFindingEventRow and R1 ReviewRegisterRow. Populate it only while register_screens is on. When off, omit it even for a stored non-null value; old DTO property order/keys, command reply and package-summary export remain unchanged. When on, legacy NULL is still omitted and the UI displays “Not recorded”; never infer an outcome from status, response wording, issue return code or package approval.
- Root adds the two fields to current/event audit metadata. HubDb's Created logger already skips NULL fields; preserve flag-off legacy created-event changes rather than refactoring audit serialization. FindingEvent remains append-only. Schema constraints must permit old NULL rows and last-response metadata retained after a return or closure.

## Per-package CSV/XLSX sheet

Add GET `/projects/{projectId:guid}/reviews/{id:guid}/response-sheet?format=csv|xlsx`. Gate register_screens in handler and metadata; normal project/package access and deleted-parent404 apply. ReadOnly users who can read the package can download. Do not change the current `/reviews/export` summary route or its columns/golden. No optional round mode in the first slice: **complete package history, all stored findings in all real package rounds**, including closed/withdrawn and carried records. One row per stored finding, with its currently retained/latest response; not one row per response event. Earlier responses remain inspectable in immutable event history. Do not collapse equal comment text or drop historical/carried rows.

Fixed column order: **Round, Comment, Originator, Discipline, Severity, Status, Response, Outcome**. Response includes the Not incorporated explanation, so no redundant Reason column. Blank response/outcome stays blank; fixed metadata states “Blank outcome: not recorded” and “All rounds; one row per stored finding, latest retained response.” Round and Status make preserved historical/carried records interpretable. Source this dataset directly from the package/round-scoped findings, not R1's unresolved-only list or a paged browser result. Validate declared project/round/source relationships; refuse malformed package source data rather than quietly exporting a purportedly complete partial sheet.

Order by round number, finding CreatedAt, finding ID. Use actual originator/discipline labels; retain inactive names and label genuine missing values unknown, never substitute coordinator/resolver. R2 implements the existing internal-user originator model only; it does not invent R3 party mapping. No account emails/private person facts/raw GUID columns are needed. No PM-only originator restriction from the separate client-deliverable export is imported into this response-sheet requirement.

Use existing ExportFile.Send and Export CSV/XLSX writers, normal size/refusal guard (currently Export.MaxRows50,000), BOM/escaping/text typing, generated timestamp, safe project/package filename and one normal Exported access-log entry. Detect an excess row with MaxRows+1 and refuse, never silently truncate a “complete” sheet. Use one consistent read for labels/responses/outcomes; export makes no business/Seen/receipt mutation. Test actual bytes/content of both formats, including quoted/multiline/Unicode/formula-looking response text, rather than only download status.

## Narrow future ownership and required proof

This contract is not an application-writing loan. Root should freeze exact signatures/fields before assigning:

- **Root single shared/schema lane:** CoordinationEntities.cs, HubDb.cs, one additive migration/model snapshot, Records/ReviewFinding.cs and FindingEvent.cs, exact Reviews.cs DTO/command/carry hooks, R1 optional read field, module registration, Text/i18n/cache/SSE/generated contracts. No global receipt/hash refactor.
- **C bounded new files:** `src/Hub.Api/Features/ReviewResponses.cs` (small pure outcome validation/mapping helper), `ReviewResponseExport.cs` (new export route/columns), `tests/Hub.Tests/Api/ReviewResponseTests.cs`, `ReviewResponseExportTests.cs` and a focused `ReviewResponseReceiptTests.cs`. No parallel mutation engine or new storage layer.
- **UI lane:** NEW `web/src/pages/projects/review-response-form.tsx` plus focused browser fixture/test; root retains exact Reviews.tsx response/history/export mounts. One response-text input and one outcome selector; explicit no-default outcome, evidence retained, same request body/ID on uncertain retry, input retained on refusal, no POST on Cancel, and actual finding/round/filter/focus return. Reuse current form mechanics and typed generated contracts.

Focused checks after review/freeze: (1) exact pre-extension serialized-envelope/hash parity for omitted/null new field, including Unicode/whitespace reasons, null old properties and non-Responded bodies; (2) genuine flag-off six-field response then flag-on same-request replay with one event/no backfill, changed-outcome mismatch, fresh flag-on missing/unknown outcome and Not incorporated missing/short reason zero-write refusals; (3) all four valid outcomes/current+event mapping, stale/named-role/independence/evidence/Complete gates, no approval shortcut, return/respond again, resolver reset and both carry paths retaining old metadata; (4) both exports contain every intended all-round comment/status/outcome, unknown legacy values, exact columns, safe bytes/access logging/no business writes, private/deleted/foreign/flag-off refusals, unchanged old summary export; (5) narrow UI stale/refusal/uncertain-retry/Cancel/history/export/focus proofs. Preserve adjacent legacy ReviewChangeTests/ReviewFindingContextTests and R1 checks; intentional flag-on new validation gets explicit fixtures, not weakened old assertions.

**Verdict: READY contract. R2 implementation and all new checks remain unrun; R3 and full S35 acceptance remain OPEN.**
