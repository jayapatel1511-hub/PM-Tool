# S33 deliverable issue fields — contract for root review

**Contract-only / production and tests held.** Owner is `register_screens`, independently of visual_system, revision_check and delivery_screens. FR/AC-SCR-56/57/58 and proposed defaults D12/D17 govern this slice; no formal decision status is changed. S34 import/client export and later review/external-party features remain out of scope. Only this new file is authored under C50.

Root subsequently approved C50 source implementation. Exact signatures and acknowledged shared integrations are in g50-deliverable-shared-seams.md. Root supplied real IVersioned issue metadata and CheckBodyVersion for the independent child baseline. **Presentation is layout=dates|register; view remains exclusively the saved-view UUID.** This corrects the proposal's initial URL name without changing ViewMenu.

## Existing paths and gaps

`DeliverableEndpoints.Issue` already locks the project in a transaction, checks the deliverable version and canonical issue permission/workflow, asks about open tasks, calls Review.Gate, creates a real DeliverableIssue, updates normal current issue fields and writes one Issued audit entry. Keep that path and every guard. A purpose must never be saved by creating a synthetic issue or bypassing revision/review/open-task checks.

DeliverableIssue currently has immutable date/revision/recipient/transmittal/note/issuer/CreatedAt and inherits Entity only, with **no existing row version**, purpose, return code or response date. Root supplies IVersioned/JsonIgnored int RowVersion with the normal explicit concurrency-token/SaveChanges increment path for the approved dual baseline. History is sorted by IssuedDate then CreatedAt, which is **not** a reliable current-issue selector when a later command records a backdated issue or timestamps tie. The register has no dedicated date view; the existing project DateReview is only the On Hold/resume correction cohort and must not be reused as a general undated tray.

## Root-owned model/reference seams

Proposed additive fields, all nullable for existing records:

| Maintained model | Fields |
|---|---|
| New `IssuePurpose : RefData` | Standard organisation Name/IsActive/SortOrder/version/audit identity |
| New `IssueReturnCode : RefData` | Same, plus `Code` (trimmed max10, unique case-insensitively within this list) |
| Deliverable | `ClientDrawingNumber` (trimmed optional one-line max120); internal `CurrentIssueId` (nullable, never a client-writable field) |
| Project | `ClientDrawingNumberPattern` (trimmed optional one-line max120) |
| DeliverableIssue | `PurposeOfIssueId`, `PurposeLabelSnapshot`; `ReturnCodeId`, `ReturnCodeSnapshot`, `ReturnLabelSnapshot`, `ResponseDate`, `ResponseRecordedBy`, `ResponseRecordedAt` |

Reference FKs restrict deletion; retain existing issue/project/deliverable scope consistency. No NOT NULL purpose constraint or historical purpose/date/code backfill. Missing metadata stays unknown. Snapshot names/codes preserve what was recorded if an Admin later renames/deactivates a reference. Current reference activity can be shown separately; it must not rewrite historical text.

Reuse Admin's `Ref<T,TRow>` list/create/PATCH/usage/deactivate infrastructure, guarded by register_screens. Proposed kinds: `/admin/issuePurposes` and `/admin/issueReturnCodes`. Only Admin manages them; no project-specific duplicate list. Usage counts distinct projects referencing the entry. Normal name/order/version/audit behavior applies; there is no DELETE or client-controlled audit identity. Return-code rows need an extra Code property/validation just as the existing discipline reference has extra Code. Root also owns the two Admin reference tabs/registration.

Append null-omitted `issuePurposes` and `issueReturnCodes` arrays to `/reference` **only while the owner flag is on**, using existing IDs/name/IsActive/order conventions and Code on returns. Pickers allow active entries; selected historical inactive entries remain visible and explicitly inactive/read-only. Unknown GUIDs never silently become null. Filter options may include known inactive references to find recorded history.

D12 seeds/defaults are exactly: purposes **For review, For approval, For construction, For tender, For information**; codes **A Accepted, B Accepted with comments, C Revise and resubmit, D Rejected**. They are editable organisation references, not a fixed enum or package-purpose list. Do not automatically select For review. An empty active-purpose list explains that an Admin must configure it and disables the new issue submission; an empty active-return list blocks a new response, not ordinary issuance. No fake fallback options.

## Issuance and actual current register selection

Append optional `Guid? PurposeOfIssueId = null` to the existing IssueBody, preserving existing positional callers. When register_screens is on, every **new real issue** requires a currently active purpose, revalidated in the existing issuance transaction, and captures its ID/label on the newly created history row. All existing status, review, revision, date, permission, version and open-task behavior stays. Do not accept return code/date in IssueBody: those describe a later recipient response, not issuance. Package purpose and deliverable status semantics are unchanged.

**Recommended exact current selector:** set internal `Deliverable.CurrentIssueId` to the newly created real issue ID atomically in the canonical Issue command. This housekeeping must run even for ordinary flag-off issuance; otherwise issuing while dark leaves a stale pointer when the flag returns. The pointer is JSON-ignored/internal, not a new audit field/event or client payload. The old off status/result/audit fields remain byte-compatible. Root must approve/provide this small internal seam rather than allowing a date-based guess.

For old records with no pointer, permit a **read-only fallback only when the most recent CreatedAt has one unambiguous issue** in that same project/deliverable. No sort by IssuedDate, no mixing the purpose from one issue with the return from another, and no ID tie-break masquerading as evidence of actual issue order. A tied/invalid/missing current identity yields current-issue unknown; history still shows its real individual records. No guessed purpose/response date or manufactured event is saved. Future issues have the explicit identity regardless of clock precision. Editing working revision fields does not select a different history issue.

The register's Purpose/Return/Response date and their filters come from **that one actual latest issue**. Returning an older revision must not make its code appear on a later current issue. Display the latest issue revision/identity in the metadata context so a currently edited working revision cannot be confused with its last issue. History badges identify the actual latest issue independently of the existing documentary-date sort.

## Record a recipient response; preserve issuance history

Proposed new guarded route: `PATCH /deliverables/{id}/issues/{issueId}/response`. Both IDs must join to the same authorised project/deliverable; a missing/deleted/foreign/cross-project target is404 without target metadata. The authenticated project user records the recipient's response; no external account or inferred client authentication is introduced.

Proposed body/result:

```csharp
IssueResponseBody(Guid? ReturnCodeId, DateOnly? ResponseDate,
    int? RowVersion, int? IssueRowVersion, string? Reason);
IssueResponseSaved(Guid Id, Guid IssueId, int RowVersion, int IssueRowVersion);
```

`RowVersion` is the parent's displayed baseline; `IssueRowVersion` is the specific history record's baseline. Acquire the existing project lock, reload scope/permissions and check both versions in the transaction. The UI keeps both dirty-form baselines through refresh/conflict; never silently replaces them or retries a stale response with fresh versions. Root may supply an equivalent narrow version helper, but must retain this concurrency guarantee.

Permission recommendation is the existing **issuance authority**: normal Writable rules plus PM or the active lead of the deliverable's actual project discipline (including existing central Admin rules). Owner/reviewer alone does not grant authority to record client responses. Use a small root permission alias if needed; do not grant new rights through the reference picker. Existing project ReadOnly/state/correction-reason rules apply. It may target a real older issue after later revisions, without changing which issue is current.

Code and date are a pair. A recorded response requires a valid active return-code ID and an explicitly entered date between that issue's IssuedDate and the current organisation day. **Never default an unknown response date to today.** Both-null means no response; first-entry partial pairs refuse400. Correction/clearing of an existing response requires a nonempty correction reason (existing reason policy/minimum), with both-null clearing the pair together. Keeping the same previously recorded inactive code while correcting its date is allowed; choosing a new inactive/unknown ID is refused. Preserve its recorded code/name snapshot when that same ID is retained.

Only response metadata may change on this route. IssuedDate, Revision, IssuedTo, transmittal/note, purpose/label, IssuedBy/CreatedAt and all other history entries remain immutable. No generic PATCH or delete for issue history. The response never automatically marks the deliverable Accepted/Revision Required, changes tasks/reviews or changes a submission package. One successful mutation produces **one ordinary project-scoped deliverable activity entry** identifying the exact issue, old/new code/date, actor, time and reason, and returns refreshed parent/issue versions. Root's audit/version bridge should use existing mechanisms with an explicit parent touch; avoid a duplicate generic Changed event. Clearing/correction leaves the prior audit evidence, not a rewritten issuance event. Refusals/no-op saves produce no extra issuance or activity.

## Read envelopes and filters

Append optional/null-omitted `RegisterInfo` to DeliverableRow and DeliverableIssueRead, absent entirely off. With the flag on, the envelope exists even when values are null, preserving unknown versus disabled. Suggested shapes:

```csharp
DeliverableRegisterInfo(string? ClientDrawingNumber, string? NumberPattern,
    bool? NumberMatchesPattern, DeliverableCurrentIssue? CurrentIssue);
DeliverableCurrentIssue(Guid Id, string? Revision, DateOnly IssuedDate,
    Guid? PurposeOfIssueId, string? Purpose, Guid? ReturnCodeId,
    string? ReturnCode, string? ReturnLabel, DateOnly? ResponseDate);
DeliverableIssueRegisterInfo(int RowVersion, Guid? PurposeOfIssueId,
    string? Purpose, Guid? ReturnCodeId, string? ReturnCode,
    string? ReturnLabel, DateOnly? ResponseDate);
```

An absent current issue is not “not returned”; show Unknown/no recorded issue metadata as appropriate. Add a flag-gated `RecordIssueResponse` permission to the detail, computed server-side. Obtain purpose/response data for all selected deliverables with one project-bound issue/reference adapter, not one query per table row. Register/history use the same current-selector function.

New canonical data keys: `purposeOfIssueId`, `returnCodeId` (stable reference IDs, not mutable names/codes) and `noDueDate=true`. Validate malformed/unknown new choices rather than silently dropping them; known inactive IDs are valid filters. Purpose/code predicates inspect the actual latest issue only. `noDueDate=true` means **DueDate null AND canonical open**: exclude Issued, Accepted and Cancelled; include On Hold/Revision Required. Existing discipline/status/milestone/owner/type/review/search/indicator/date filters remain ANDed. No override that discards an incompatible status or due range.

Root owns saved-view whitelist, column metadata, cache/ref invalidation and the existing standard register-export filter bridge. Default visible additions are Client drawing number, latest-issue purpose, return code and response date; columns can use the current table controls. No S34 client export or new export columns/format in this slice. Preserve all old issue/read properties and flags.

## Client drawing number and project pattern

Append optional ClientDrawingNumber to CreateBody and support it in the existing deliverable PATCH using the same canonical Create/Edit permission, version and reason rules; existing edit authority is not broadened. Trim blank to null, reject control/newline/over120 input, and apply no uniqueness or status rule not required by the spec. A mismatch must still save.

**Supported pattern is deliberately small:** `#` matches exactly one ASCII digit0–9; every other character is literal, case-sensitive, over the entire trimmed number. No regex, glob, repetition, ranges, escapes or regex editor. `C-###` matches C-123; C12 is saved with **Does not match C-###**. Literal regex metacharacters stay literal. A blank pattern is disabled. Missing number or pattern yields NumberMatchesPattern=null/Not provided, not false. Implement a bounded character comparison, not executable user regex.

Root model field is Project.ClientDrawingNumberPattern. Recommended project-settings seam is a small guarded `GET/PUT /projects/{id}/deliverable-numbering` (like the existing RevisionCheck settings boundary): read returns pattern/project rowVersion/canEdit; PUT receives pattern/project rowVersion/reason, uses canonical PM-only Writable plus project lock/version/correction rules, and emits one NumberingPatternChanged project activity. Root can instead bridge the normal project PATCH with identical safeguards. Root owns the project-settings registration; C can provide the isolated control/helper if explicitly loaned. Pattern changes recompute warnings on reads/new saves; they do not rewrite client numbers or issue records, change revision-check marks, block issue/status commands, or require a new background job. Server warnings/read envelope are authoritative after a stale browser preview.

## No due date tray and date view

The existing register has grouped table/cards and due filters, but **no general date-view component**. Root approved a small register-local `layout=dates` mode: dated open deliverables grouped by DueDate, beside a No due date tray with count and exact record links. Use existing HTML/register primitives, not a new Calendar/Gantt, date-move workflow or the On Hold DateReview. The default table/cards stay available; this UI state is excluded from API predicates/export parameters and can be included in the deliverables saved-view presentation whitelist.

Use the same authorised, fully filtered open list response for both dated buckets and undated tray. Canonical open semantics are the existing `indicator=open` rule above. Derive the count and listed IDs from the very same response/visible cohort, including current-purpose/return filters; no separate broad query, task rows, deleted items, or terminal undated records. The tray's “Show no due date” sets noDueDate=true and returns to the normal register view, preserving every other filter. Its resulting IDs must equal the tray's IDs.

Due-range predicates stay ordinary AND predicates: with an active range no undated row can match, so the tray/filter returns0 and explains that the date range excludes undated records, offering explicit removal of that range. Never clear a user's scope silently or count undated items outside the displayed filter context. No date is invented or inherited merely to fill the view. Tray record opens preserve exact filters/view, panel return, scroll and keyboard focus using existing origin helpers. Phone uses dated list then tray with all values/links accessible, not a clipped calendar.

## Dark/refusal behavior and verification gates

Off: all new UI/columns/envelopes/reference lists/settings routes/response route are absent; new routes404; normal old commands/results/guards remain. Existing commands with omitted/null optional new values retain old behavior. A nonnull new purpose/client-number value while dark refuses explicitly rather than pretending it was stored. Frontend ignores/removes new filter/view keys from requests while off; direct meaningful new filter parameters refuse rather than silently widen scope. Internal current-issue identity maintenance is the explicitly requested exception above; no metadata value, response or fictional event is backfilled. Independent register/visual/revision/delivery flag pairs must be exercised.

Focused tests after approval: normal issue For review → record B with explicit date → register/history/filter same actual issue; normal reissue via Issued→Revision Required→In Progress→Ready/Issue, including backdated dates/fixed-clock ties and response on the older issue; no code-driven status mutation; immutable snapshots under rename/deactivate; unknown/inactive/empty references and legacy null metadata; dual-version conflict/dirty-baseline retention; PM/own-DL versus owner/reviewer/foreign-DL/ReadOnly/foreign-project refusal, ordinary correction reasons, one audit/no extra issue; accepted/issued/cancelled undated excluded and four open undated included; exact filter/tray ID parity with composed scopes/date conflicts; warning-only C-###/C12, literal metacharacters, missing/disabled pattern and stale pattern; saved filters/columns/date view, exact drawer return, phone/keyboard/off. Pure selector/pattern tests supplement **normal-command** issuance/reference/project-setting fixtures. Root executes, reviews and joins real disposable API/UI data; mock UI alone is not acceptance.

**Ready for root's approval and exact shared seam choices.** Specifically approve/provide reference model/registration + Admin list UI, nullable migration and internal current-issue tracking, permission/audit/version bridge, project-pattern settings registration, and register view/export metadata. Then open the bounded Deliverables.cs/project Deliverables UI/new helper/test loan. No application, test, schema, shared file or runtime changes have been made in this phase.
