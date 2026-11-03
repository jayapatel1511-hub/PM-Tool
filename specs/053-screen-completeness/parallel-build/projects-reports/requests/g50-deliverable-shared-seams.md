# C50 exact shared integration seams — before source implementation

Root's explicit S33 source-authoring approval supersedes the initial contract-only text in CONTROL until its next update. C owns Deliverables.cs/new DeliverableRegisterReads.cs/DeliverableNumbering.cs and isolated UI/tests; all shared models/metadata/refs/settings registration/cache/locales/generated files remain root-owned. No execution.

Root supplied CheckBodyVersion and numbering module registration; both are acknowledged and consumed. Root correctly reserves **view** for a saved-view UUID: the approved date/register presentation uses **layout=dates|register**, with no generic ViewMenu change. Every implementation/metadata reference below uses layout; the older proposal wording is superseded.

## Confirmed model/version/audit boundary

Use exactly root's approved nullable names in g50-deliverable-issue-contract.md. Root clarified that existing DeliverableIssue inherits **Entity only**, so root supplies **IVersioned + JsonIgnored int RowVersion + normal EF concurrency token**, using the existing SaveChanges increment path. No xmin or fabricated legacy version. No metadata backfill. All new model fields are JsonIgnored and exposed only in explicit register_screens envelopes. Internal CurrentIssueId is maintained on every real canonical issue, even dark, and is not a registered audit field. ClientDrawingNumber is a normal deliverable audit field; no issue metadata entity is separately auditable, to avoid duplicate response events.

Response will use existing DeliverableTransition(actor,context,facts,ReadyToIssue,Issued) for PM/actual-DL issue authority, without changing status/workflow. It explicitly touches parent's LastActivityAt (force modified even with a fixed clock) and uses **one db.LogEvent Deliverable/IssueResponseRecorded** with exact issue identity and old/new code/date. The ordinary automatic audit ignores LastActivityAt, so it creates no second Changed event. One parent and one real issue version advance on a mutation; a no-op changes neither version and writes no event.

**Required narrow Http seam:** normal Http.CheckVersion reads the request If-Match header before its body argument. Calling it twice would incorrectly use the parent's header for the child's version. Please expose:

```csharp
public static Task Http.CheckBodyVersion(HubDb db, IVersioned entity, int? bodyVersion);
```

It should reuse the existing428/409/who-changed logic; existing CheckVersion can delegate with IfMatch(ctx,bodyVersion), leaving all current callers unchanged. C then calls normal CheckVersion for the parent and CheckBodyVersion for the issue. No request-header mutation or fake HttpContext. A parent header must never substitute for omitted/stale IssueRowVersion. This is the only newly identified shared helper dependency.

## Exact owned API/read/filter signatures

DeliverableEndpoints becomes partial so DeliverableRegisterReads.cs can own its supplemental records and endpoint helpers. Append to existing records with optional defaults/null omission, preserving old constructor callers/wire:

```csharp
// append CreateBody: string? ClientDrawingNumber = null
// append IssueBody: Guid? PurposeOfIssueId = null
// append DeliverableQuery:
Guid? PurposeOfIssueId = null, Guid? ReturnCodeId = null, bool? NoDueDate = null
// append DeliverableRow:
[property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
DeliverableRegisterInfo? RegisterInfo = null
// append DeliverableIssueRead:
[property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
DeliverableIssueRegisterInfo? RegisterInfo = null
// append DeliverablePermissions:
[property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
bool? RecordIssueResponse = null

public sealed record DeliverableRegisterInfo(string? ClientDrawingNumber,
    string? NumberPattern, bool? NumberMatchesPattern, DeliverableCurrentIssue? CurrentIssue);
public sealed record DeliverableCurrentIssue(Guid Id, string? Revision, DateOnly IssuedDate,
    Guid? PurposeOfIssueId, string? Purpose, Guid? ReturnCodeId,
    string? ReturnCode, string? ReturnLabel, DateOnly? ResponseDate);
public sealed record DeliverableIssueRegisterInfo(int RowVersion, Guid? PurposeOfIssueId,
    string? Purpose, Guid? ReturnCodeId, string? ReturnCode,
    string? ReturnLabel, DateOnly? ResponseDate);
public sealed record IssueResponseBody(Guid? ReturnCodeId, DateOnly? ResponseDate,
    int? RowVersion, int? IssueRowVersion, string? Reason);
public sealed record IssueResponseSaved(Guid Id, Guid IssueId, int RowVersion, int IssueRowVersion);
```

New response route is PATCH /deliverables/{id}/issues/{issueId}/response with register_screens404 guard, strict allowed body members and required explicit code/date properties (nullable together for correction/clear). Every issue/current lookup is scoped to both project and deliverable. No purpose/history edit on this route.

Data filters/URL keys are **purposeOfIssueId, returnCodeId, noDueDate**. IDs are singular GUID reference identities, with known inactive references valid as filters. Query malformed IDs are normal400; missing reference IDs also400, never ignored. noDueDate=true adds DueDate=null AND existing open statuses; it does not override any other filter. Direct meaningful new query values while dark refuse; normal legacy queries remain unchanged. Deliverables List owns the new filter adapter, and existing ListExports already calls List, so export cohort parity needs no duplicate arithmetic/filter implementation.

Root deliverables view whitelist should allow those three data filters, new visible column IDs **clientDrawingNumber, purposeOfIssue, returnCode, responseDate**, and presentation **layout=register|dates** under register_screens. Preserve current group/cols/filter/panel behavior and off cleaning. Presentation layout is never sent to List/export; dates mode appends canonical open to the data indicator without replacing existing AND indicators. No client export columns/S34 format is added. Root export parameter labels should resolve the two reference IDs and label noDueDate; existing export rows/columns otherwise stay unchanged.

Root /reference additions **issuePurposes** (Id,Name,IsActive,SortOrder) and **issueReturnCodes** (same plus Code), absent off, are used by normal frontend useReference. Root Admin kinds are issuePurposes/issueReturnCodes; existing Ref conventions/usage/deactivation apply. Seed D12 defaults exactly, no UI auto-selection. All submit reference checks use db.Set<IssuePurpose>() / db.Set<IssueReturnCode>() so no new DbSet property is assumed.

## Numbering settings registration

Owned backend class **DeliverableNumberingEndpoints**, `public static void Map(RouteGroupBuilder api)`, root adds module registration. GET/PUT `/projects/{id}/deliverable-numbering`, guarded register_screens. Read response `{ pattern, rowVersion, canEdit }`; write `{ pattern, rowVersion, reason }` with explicit nullable pattern property; saved `{ id, rowVersion, pattern }`. Normal project lock/PM Writable/version/correction rules and one project audit. Root registers ClientDrawingNumberPattern only as appropriate project audit metadata, not a general raw field/wire or regex setting.

Owned isolated frontend export will be:

```tsx
export function DeliverableNumberingSettings({ projectId }: { projectId: string })
```

from `web/src/pages/projects/deliverable-numbering.tsx`. It owns its guarded GET/version-baseline form and renders nothing off; root may insert exactly `<DeliverableNumberingSettings projectId={p.id} />` in ProjectSettings (use that file's actual project variable). No duplicate project-scope/permission invention. UI uses #=one ASCII digit and every other character literal, full ordinal match; blank disables. No regex editor.

C will send exact server/UI labels in a separate JSON request as implementation settles. No direct shared edits by this lane.
