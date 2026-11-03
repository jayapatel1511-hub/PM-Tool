# G56 — audit field ownership inventory

2026-11-02. **READY for root's audit repair; source inventory only.** Base `ac2fa894`, current checkout `/Users/jaypatel/.codex/worktrees/ef94/PM-Tool/.build-052-053`, branch `codex/build-052-053`. Explicit root loan permits only this document. No application, registry, test, locale, schema, handoff or browser file was edited. Root's optional browser screenshot hooks and fixture remain untouched.

## Scope and evidence

Compared `git diff ac2fa894 -- src/Hub.Api/Records` and enumerated all untracked files in that directory. There are **22 added field names in 11 existing registry entries**, plus **six new registry entries**. The twelfth modified file, `ScreenLists.cs`, contains URL/list-state definitions rather than audit fields and contributes no entry to this mapping.

`Records/Registry.cs:9-25` defines/reflection-loads these exact entries; `Infrastructure/Audit.cs:34-35` derives the logged-field allowlist from them. `Features/Activity.cs:98-125` currently renders raw change fields, deletion snapshots, labels, summaries and the stored ItemName. A flag check only in a feature's ordinary read is therefore insufficient for this shared history response.

Authority: `spec-parts/18-delivery-process.md:33` (FR-DLV-08: fields absent while their flag is off); `spec-parts/32-screen-completeness.md:47-55` (part-to-flag table) and `:400` (FR-SCR-84: every response/export returns to prior behavior, and existing features are not newly flagged); `spec-parts/31-visual-system-and-navigation.md:179,205` (FR-VSN-23/AC-VSN-12: visual_system owns the new identity-colour response).

## Exact new-field map

Keys below are the **stored audit ItemType plus PascalCase registry field**, not HTTP JSON member names. Apply the same ownership to changes and deletion snapshots, including derived labels/summary content. Preserve other fields and the preexisting event itself.

| Registry / stored ItemType | New audited fields versus base | Owning flag | Feature and specification evidence |
| --- | --- | --- | --- |
| `AppUser.cs` / `User` | `PersonType`, `PersonStartDate`, `PersonEndDate` | `capacity_screens` | `Features/PersonFacts.cs:42-43` shared Enabled guard; `:61-74` flag-limited reads; `:172-184` guarded facts write. FR-SCR-50 at `32-screen-completeness.md:278`, Part E. |
| `Decision.cs` / `Decision` | `CategoryId`, `OptionsConsidered`, `Rationale` | `register_screens` | `Features/DecisionMetadata.cs:16` exact Flag; Part F. Category is new; legacy DecisionText/Description/Status remain existing fields. |
| `Deliverable.cs` / `Deliverable` | `ClientDrawingNumber` | `register_screens` | `Features/DeliverableRegisterReads.cs:13,52-58,67`; `Deliverables.cs:322-335`; FR-SCR-58 at `32-screen-completeness.md:306`. |
| `DocumentLink.cs` / `DocumentLink` | `SourceSystem`, `StableSourceId`, `DeclaredRevision`, `ReferenceRegisteredBy`, `ReferenceRegisteredAt`, `ReferenceDeliverableId` | `people_screens` | `Features/DocumentLinkReferences.cs:26` exact Flag; `:60-67` stores/clears all six together; G55 manual source-reference addition, Part G Files. Existing Title/Url/LinkType/DeletedAt remain. |
| `FindingEvent.cs` / `FindingEvent` | `ResponseOutcome` | `register_screens` | `Features/Reviews.cs:66-69` conditional event read; `:345-378` guarded response write. FR-SCR-61 at `32-screen-completeness.md:318-320`, Part F. |
| `Issue.cs` / `Issue` | `RootCauseCategoryId`, `RootCauseLabelSnapshot` | `register_screens` | `Features/IssueRootCauses.cs:17-18,41` exact Flag/guard. Both are the new G56 root-cause pair; older IssueType/Resolution/OriginRiskId remain. |
| `Project.cs` / `Project` | `ClientDrawingNumberPattern` | `register_screens` | `Features/DeliverableNumbering.cs:38-42` guard uses `DeliverableEndpoints.RegisterFlag`, defined as register_screens at `DeliverableRegisterReads.cs:13`; FR-SCR-58. |
| `Project.cs` / `Project` | `IdentityColour` | `visual_system` | `Features/Projects.cs:208,412` conditional reads; `:484-489` guarded write; `Admin.cs:169-170` activation backfill. FR-VSN-23/AC-VSN-12. |
| `ProjectMember.cs` / `Member` | `ResponsibilityNote` | `people_screens` | `Features/MemberResponsibility.cs:26,32` exact guard; `Team.cs:229` conditional response. FR-SCR-76 at `32-screen-completeness.md:367`, Part G. The internal MemberRead carrier at Team.cs:191,207 is not the public response. |
| `ProjectTemplate.cs` / `Template` | `CopiedFromTemplateId` | `people_screens` | `Features/TemplateVersionProvenance.cs:18` exact Flag; `TemplateDraftDiff.cs:107-108` persists source pair. FR-SCR-81 at `32-screen-completeness.md:392`, Part G. |
| `ReviewFinding.cs` / `ReviewFinding` | `ResponseOutcome` | `register_screens` | `Features/Reviews.cs:57-61,345-378,480,514`; same response-outcome feature as FindingEvent, Part F. |
| `Risk.cs` / `Risk` | `TemplateRiskId` | `register_screens` | `Features/TemplateRiskSeeds.cs:18,60` exact Flag and created-risk origin assignment. FR-SCR-70 at `32-screen-completeness.md:346`, Part F. |

Counts: capacity3 + register10 + people8 + visual1 = **22**. These counts cover fields only, not the new whole-record entries below.

## New whole-record events

The common vocabulary fields Name/IsActive/SortOrder are old on other RefData types: **do not hide those field names globally**. Instead, suppress a new vocabulary's whole history row while its own flag is off, including stored ItemName, ItemKey, changes, summary, reason and any snapshot. The stored key is the discriminator; a label/name match is not trustworthy type evidence.

`Data/Entities.cs:79-87` defines inherited RefData audit metadata: ItemType=`ReferenceData`, AuditProjectId=null, AuditKey=`GetType().Name`, AuditName=`Name`. None of these five new RefData subclasses overrides AuditKey/AuditType. Their declarations are at `Entities.cs:96-99` and `PersonFactEntities.cs:4`. The registry declarations confirm the same ItemType.

| New registry entry | Exact stored ItemType | Trusted stored ItemKey | Whole-event owning flag | Evidence |
| --- | --- | --- | --- | --- |
| `DecisionCategory.cs` | `ReferenceData` | `DecisionCategory` | `register_screens` | `Features/Admin.cs:72,75-77`: register-only Ref routes. Registry fields Name/IsActive/SortOrder. |
| `IssueRootCauseCategory.cs` | `ReferenceData` | `IssueRootCauseCategory` | `register_screens` | `Features/Admin.cs:72,78-80`: register-only Ref routes. Registry fields Name/IsActive/SortOrder. |
| `IssuePurpose.cs` | `ReferenceData` | `IssuePurpose` | `register_screens` | `Features/Admin.cs:72-74`; this registry's ExportNames also explicitly checks register_screens. Registry fields Name/IsActive/SortOrder. |
| `IssueReturnCode.cs` | `ReferenceData` | `IssueReturnCode` | `register_screens` | `Features/Admin.cs:72,81`; this registry's ExportNames also explicitly checks register_screens. Registry fields Code/Name/IsActive/SortOrder. |
| `CertificationTag.cs` | `ReferenceData` | `CertificationTag` | `capacity_screens` | `Features/Admin.cs:71` guards all vocabulary routes; PersonFacts.cs:42-43 guards facts readers/commands. Registry fields Name/IsActive/SortOrder. |
| `UserCertification.cs` | `UserCertification` | Actual recorded `UserId.ToString()` (not a fixed type discriminator) | `capacity_screens` | `Data/PersonFactEntities.cs:7-15`: exact type/key, null project, ItemName is CertificationTagId string. `Features/PersonFacts.cs:42-43,130-131,155-169` guards creates/edits/deletes. Registry fields UserId/CertificationTagId/ExpiresOn. Match the exact ItemType to suppress the whole new event; do not require one particular user key. |

`Features/Admin.cs:407-435` generic Ref only offers GET list/usage, POST and PATCH with deactivation through IsActive. `MapCertificationTags` at `:366-396` similarly offers GET/POST/PATCH. There is **no RefData delete endpoint or audit-key override** to account for here. UserCertification separately has actual scoped DELETE commands, so its deletion snapshots must remain covered by the whole-event rule.

## Minimal mapping boundary / preserved prior behavior

1. Exact field-level tuples are only the 22 additions above. Match record type as well as field name: e.g. Template source ancestry is people_screens, whereas Risk.TemplateRiskId is register_screens. Do not infer ownership from a shared name, source record ID, broad category, or which screen happens to render a row.
2. Exact whole-event predicates are `ReferenceData` plus one of the five literal keys above, and `UserCertification` for all its stored keys. Keep legacy ReferenceData types such as Office, Discipline, Client, Phase, DeliverableType and ProjectType visible under their existing rules. Never classify a vocabulary by editable ItemName.
3. Filter before resolving hidden-field IDs or constructing snapshot labels/summary, so those projections cannot reintroduce a hidden value. Existing access checks and the InternalNotes rule remain in force. Root owns the Activity/Admin helper, query/count/paging policy and tests; this inventory does not broaden them or claim verification.
4. **Preexisting in ac2fa894:** Project `StatusNote`, `StatusNoteBy`, `StatusNoteAt` are already in both the base entity and base Project registry. Their existing owner remains `project_status_note` (`Projects.cs:351,440-442`); they are not new 052/053 fields and must not be put behind project_screens or another new flag by this repair. AppUser HR/source/UnitId/capacity/IsTemplateEditor fields, Template OffsetDayKind and all other unchanged registry fields are likewise outside this new-field map. Keep prior capability/feature semantics.
5. ProjectTemplate `CopiedFromSnapshot` is persisted but **not in its registry allowlist**. TemplateRisk has no new registry entry in this inventory. Derived projection fields, DeliverableIssue wire additions and ScreenLists URL keys are not new registry fields; do not invent audit field mappings for them.

## Unknowns and verification limits

No owning flag is unresolved for the inventoried 22 fields or six entries. A legacy/corrupt ReferenceData row with null/unrecognized ItemKey cannot be classified by this trusted-key map; do not guess from its display name or hide every ReferenceData event. Root must choose any further handling outside this exact source-backed map.

Only git/source/spec reads and this document write occurred. No helper, compiler, parser, build, generator, tests, browser, database/runtime, flag or service operation was started. The root-only audit fix and its acceptance checks remain unimplemented/unrun by B.
