# G56 — S40 template risks + S48 version provenance

2026-11-02. READY for root review/freezing; documentation only, then HOLD.
Checkout: `.build-052-053`, branch `codex/build-052-053`.
CONTROL: generation56; initial planning-only mode, rechecked during `generation56-A-S39-authoring-other-lanes-planning`. B's documentation-only loan remains unchanged.
Only this plan is B's write loan. No app/test/schema/config/generated/locale edits or execution.

## Requirements and current truth

S40 is FR/AC-SCR-70 under **register_screens**; S48 is FR/AC-SCR-81 under **people_screens**. These remain independent existing flags, including when only one is enabled. No new flag.

| Current evidence (checkout-relative path:line) | Consequence |
| --- | --- |
| `spec-parts/32-screen-completeness.md:346,392,493,507`; flags table at `:46` | Two template risks become two Open project risks owned by the PM; later edits stay isolated. Every version shows its actual created projects. Project provenance states source v3/newer v5 without modifying the project. Draft diff uses the version actually copied, with added/removed/changed items. |
| `spec-parts/05-modules-b.md:175`; `specs/012-project-templates/spec.md:103–110` | Existing snapshot/authorisation/date/discipline rules and project/item origin references remain. Initial template instantiation, adding a discipline pack and copying a project are distinct commands. No project-to-latest-template comparison or reapply workflow. |
| `src/Hub.Api/Features/Templates.cs:25,46,62,185,207` | Wire is typed JSON; persisted structure is relational Parts, not a saved template JSON document. Read/List have no risks, created-project rows or draft baseline. List.Projects already counts exact CreatedFromTemplateId, but currently does so without project-visibility filtering. |
| `Templates.cs:279–411` | Whole-structure save requires Draft/editor/current RowVersion; ref GUIDs survive for milestones/deliverables/tasks/roles. Discipline rows are recreated. Basis IDs survive ordinary structure save; roles have existing omitted/off preservation logic. ClearChildren/reinsert runs in Tx.Run. |
| `Templates.cs:431–471` | NewDraft creates fresh child IDs, preserving graph references through a temporary map. It records only a DraftStarted reason such as v3; no source ID/content/map is stored. An already-existing family Draft is returned, including when requested from another version. Never reinterpret that Draft's origin as the newest request/source. |
| `Templates.cs:473–494`; `Data/Entities.cs:1119`; `Data/HubDb.cs:601` | Publish numbers versions and retires the previous Published row. Published/Retired contents are immutable through normal routes. A version's row ID differs from FamilyId. There is no copied-from column or baseline JSON; no historical baseline can be derived reliably from latest version or row titles. |
| `Templates.cs:562,640–697`; `Features/Projects.cs:272–315` | Generic Instantiate is also used by AddPack. Initial TemplateHook runs inside project-create Tx.Run, copies source parts, then stores exact Project.CreatedFromTemplateId and TemplateVersion. The creating actor is currently made the PM; new risk ownership must use actual p.ProjectManagerId. |
| `Data/Entities.cs:681`; `Features/Registers.cs:160,223–245`; `Data/HubDb.cs:461` | Risk already has Title, Probability/Impact 1..3, Mitigation, TriggerIndicator, Open default, OwnerId, nullable review/discipline/realised-issue fields. Command validation requires the scores; entity defaults of 1 do not justify silently accepting missing scores. No template risk storage exists. |
| `src/Hub.Api/Features/Projects.cs:365`; `web/src/pages/projects/FromTemplate.tsx:206`; `web/src/app/extension-slots.tsx:30–39`; `web/src/app/registrations.tsx:34` | Existing Project source ID/version/name are real persisted provenance. TemplateSettings is already mounted through the settings slot and shows source version; it lacks newer-version information. No new project provenance table is needed. |
| `web/src/pages/Templates.tsx:37,65,140,171–197` | Current editor uses generated records, whole-structure save/current version, guarded local drafts, existing save/publish actions and fresh refs for new items. Templates remain DesktopOnly with PhoneNotice. The family list currently sums counts, not per-version project lists. |
| `Infrastructure/Keys.cs:43`; `Infrastructure/Idempotency.cs:38–65`; `Domain/Permissions.cs:155,161` | Existing Tx.Run joins an outer transaction. Project creation and template editing have different canonical permissions. Ordinary POST receipts require an explicit Idempotency-Key; they are not a new atomic-template RequestId protocol. |

## S40 — small risk seed and initial-project snapshot

Root schema proposal: a `TemplateRisk` child with Id, TemplateId, SortOrder, Title, Probability, Impact, Mitigation?, TriggerIndicator?. Add nullable `Risk.TemplateRiskId` as the actual origin reference, following §12.14's existing template-item references. Old templates/risks remain absent/null; no inferred seed rows, origin backfill or change to ordinary risk defaults. Root owns relationships, delete behavior, constraints, audit registry and migration. Retain published source seeds; draft deletion must not cascade into project risks.

Use the existing Risk limits: trimmed title1..300, probability/impact each explicitly1..3, mitigation<=4000 and trigger indicator<=2000; optional blank text becomes null. Do not add seed owner/status/review date/discipline/realised issue, prices or integrations. Template seed score fields are validated values, not an implied risk assessment supplied by the system.

Proposed generated wire addition, reusing the existing whole-structure endpoint:

```text
TemplateRiskIn(ref, title, probability, impact, mitigation?, triggerIndicator?)
StructureBody.Risks?: TemplateRiskIn[]       # omitted/null preserves; [] explicitly clears while on
TemplateRead.Risks?: TemplateRiskIn[]        # null-omitted off; stable actual refs on
```

Reject invalid/missing score, duplicate/bad refs and new nested forged actor/status/target fields before changing storage. New risk-bearing input while register_screens is off refuses through the established dark-feature convention; off clients omit it. Existing header/structure/create/publish response fields and property order remain. Only authoritative GETs and the current PUT structure-save projection gain these optional read fields; all existing POST response contracts stay unchanged for S40/S48. Use existing generated DTOs and validation patterns, not client-only shadow types.

Risk preview needs no extra POST field or route: the wizard already GETs the selected template in `web/src/pages/projects/FromTemplate.tsx:38–44`. For a published immutable version, use that current flag-authorized GET's Risks array and its length to show the real seeds/count and the PM/Open explanation. Seeds have no date/discipline-dependent calculation. Keep POST preview's existing milestone/date/role calculation and exact reply. Withhold enhanced preview on a missing/current-key-refused GET or when register_screens is off; a cached POST preview is not risk authority. Initial project creation still rechecks that the actual source is Published.

Extend the internal Parts loader and Draft cloning to retain real risk rows. Structure saves preserve stored risks when omitted, including old clients/off mode; explicit on-mode replacement/clear uses the same template version and transaction as the other structure. Preserve seed IDs on normal retained refs so the diff can identify edits. NewDraft gives each seed a new ID and records its clone mapping when S48 capture is enabled. Publishing preserves that version's seed values; later drafts do not mutate them.

**Instantiate only in the initial TemplateHook**, under register_screens, inside the already-open project-create transaction. Do not put unconditional risk creation in shared Instantiate: that would also seed risks when appending a discipline pack. Copy every template risk independently of the discipline checklist because the required seed has no discipline field. Reserve ordinary risk keys in one batch; create normal Risk rows with fresh IDs, exact seed values, TemplateRiskId, Status=Open, OwnerId=p.ProjectManagerId, one server instant for StatusChangedAt/LastActivityAt and normal creation/audit facts. Description/ReviewDate/ProjectDisciplineId/RealisedIssueId stay null. Validate the actual PM identity through existing person/creation rules; do not accept owner overrides or use a later template editor as owner.

Reuse the outer CreateProject permission and transaction, automatic per-item Created activity and existing one CreatedFromTemplate project entry. No nested HTTP calls, duplicate activity, new permission or independent commit. A seed/refusal/failure rolls back project, keys, structure, risks and activity together. Source template rows never become live links controlling risk status/value. Subsequent risk changes use the normal risk commands and versions. CopyStructureHook and AddPack retain their existing scope; neither gains risk seeding or new source-project lineage.

## S48 — record a copy once; compare against that record

Minimal root schema proposal on ProjectTemplate: nullable `CopiedFromTemplateId` plus nullable jsonb `CopiedFromSnapshot`. Both describe one actual server copy operation; they are not editable input. No Project schema change is needed for existing exact source ID/version. Add registry treatment deliberately; do not serialize raw internal snapshots into public reads.

The snapshot is a small feature-specific, typed, versioned document, not a generic history framework:

```text
TemplateCopySnapshotV1
  schemaVersion: 1
  source: { templateId, familyId, version?, name, rowVersion }
  capturedAt: server instant
  structure:
    header(name, description?, projectTypeId?, offsetDayKind)
    disciplines(global disciplineId, default inclusion, order)
    milestones / deliverables / tasks / roles / basisSuggestions / risks:
      exact source refs and existing content fields, with relationships expressed as source refs
    dependencies: source task-ref pairs
  clones: [{ kind, sourceRef, draftRef }]    # actual server clone map, not title matching
```

Capture source content, source identity/version and mapping from the actual selected source at NewDraft, atomically with the clone. The source is not necessarily the latest: a draft copied from retired v3 while v5 exists still compares to v3. Returning the existing family Draft never rewrites its baseline. Later structure/header/basis saves, publish and retire do not recapture or rebase it. A request body cannot supply or replace lineage. Snapshot JSON null/future schema/corrupt content is not “zero changes”.

For legacy drafts, missing copy facts remain **NotRecorded**; do not infer from family max/version-1/UpdatedAt/DraftStarted reason/title similarity or currently equal content. New standalone families have no copied version and say so without calling their empty baseline a historical version. Nullable numbers remain null, never v0. A valid captured snapshot remains the actual historical comparison source; an unavailable current source link must not make the diff fall back to a different version.

Diff algorithm is one backend pure comparison over recorded baseline plus the current persisted draft, read consistently. Match disciplines by global ID; match cloned milestone/deliverable/task/role/basis/risk rows by the stored kind/source-ref/draft-ref map. New draft rows have no source counterpart and are Added; disappeared counterparts are Removed; changed content/relationships/order are Changed. Normalize relationship refs through the map. Compare dependency endpoint pairs, not their regenerated IDs. Preserve duplicate titles as distinct identities and report a remove+add as such when there is no recorded identity continuity. Ignore generated row IDs/TemplateId/row versions/audit timestamps as content changes. Header and all existing content kinds participate; do not silently limit diff to tasks or risks.

Proposed named wire contracts, root to freeze:

- `TemplateDraftDiffRead(state, asOf, draftRowVersion, source?, counts?, changes, incompleteSections)`. State includes Available, NotRecorded, Unavailable; counts are null when unavailable. A finite `TemplateDraftChange` has kind, Added/Removed/Changed, sourceRef?/draftRef?, beforeName?/afterName?, and changed fields with before/after display text. No raw internal JSON or guessed baseline is exposed.
- Add nullable `TemplateRead.Screen: TemplateVersionScreenRead` under people_screens. Include this exact version's visible project count and its draft diff with all changes; no arbitrary change cap or silent truncation. The existing whole-template GET already loads its full structure. Existing Family version rows can gain null-omitted visible-project counts. The separate paginated project list below keeps potentially large usage history out of the template envelope; a separate diff route is not needed for this proposed shape.
- `Page<TemplateVersionProject>` for `/templates/{id}/projects`: projectId, projectNumber, name, status, createdAt. Count and paginate the **exact CreatedFromTemplateId=id** cohort after current Access visibility, stable createdAt/ID order. Include visible historical archived/cancelled projects; do not use workspace Scope, family membership, matching version number alone, copied-project ancestry or later AddPack use as evidence. Batch per-version counts for list/family reads, not a query per row.
- Add null-omitted `ProjectDetail.TemplateProvenance` under people_screens: state, actual source templateId/name/version, familyId when coherent, newer numbered version/status?, current Published version? and asOf. Use existing persisted Project.CreatedFromTemplateId/TemplateVersion, with coherent same-family version reads. Keep the existing source fields. An inconsistent/missing source is labelled unknown/unavailable; never silently replace the project's recorded version with a family max.

For the required example, the actual version-row v3 yields project250101 in v3's list; its settings show “Created from <template> v3; v5 exists”. The line is information only: no apply/update button, project patch, item/Seen mutation, new snapshot or row-version bump. A source version summary does not grant access to retired template structure or to other projects. All created-project rows/counts use current project authority, including for a Template Editor who cannot view a restricted project. Existing unscoped List.Projects count must not be reused as the enhanced visible count; keep exact old behavior off and use current scoped counts in the on-mode presentation/contract.

Use one repeatable read for each metadata response's count/rows/diff facts. Project/template creation copies are consistent command snapshots inside their existing transaction, not GET-side repair. Root should freeze the narrow same-family serialization strategy for concurrent NewDraft/Publish: current checks/loads partly precede Tx.Run and the inspected model shows only a nonunique FamilyId index. Preserve the existing 200-existing-draft/201-new-draft and current RowVersion refusals; do not create two competing baselines, a new generic receipt engine or a new template permission.

### Exact response projections and cached POST receipts

`Infrastructure/Idempotency.cs` only handles keyed POSTs and can return stored successful response bytes before the handler runs. A handler-side current flag check cannot scrub an earlier successful reply. The inspected Templates POSTs currently do **not** return TemplateRead; the shared TemplateRead return is GET detail and PUT structure-save. Keep that distinction explicit rather than expanding commands to return their enriched read.

| Existing route | S40/S48 response rule |
| --- | --- |
| POST `/templates` | Keep TemplateCreated(id,rowVersion), status/Location unchanged. |
| POST `/templates/{id}/draft` | Keep TemplateDraft(id,rowVersion), both existing200 and created201/Location unchanged. No Risks, Screen, copied-from snapshot or nested enriched TemplateRead. Navigate/invalidate, then GET the draft for current flag-authorized metadata. |
| POST `/templates/{id}/publish` | Keep TemplatePublished(id,version,status,rowVersion). Fetch the authoritative version GET after success; no embedded enriched template. |
| POST `/templates/{id}/preview` | Keep PreviewResult/PreviewCounts exactly as today for these additions, including existing independent template_roles behavior. Add neither Risks nor Screen at any depth. Source risk information comes from GET, not replayed preview bytes. |
| POST design-basis, retire, project creation and template-packs | Keep existing BasisSuggestionCreated,204,ProjectCreated and PackAdded replies respectively. No enriched template/project object is introduced indirectly; ordinary risk command replies also remain unchanged. |
| PUT `/templates/{id}/structure` | It is not generic-POST receipt-cached. May return TemplateRead with Risks/Screen projected from current flags after the normal versioned save, retaining omission/guard/preservation behavior. Current-key GET refresh remains authoritative after uncertain/error/refusal states. |
| GET template list/detail/project detail | Apply current feature flags/visibility to optional additions on every read; partition/invalidate caches. Retained ids/version-only POST replies do not stand in for these reads. |

**Extra path/marker decision:** only the proposed flag-gated GET `/templates/{id}/projects` is needed for complete paginated version usage. Risk preview and complete draft diff reuse existing authoritative GET `/templates/{id}`; project information reuses GET project detail. No new readonly POST, replay marker, global middleware change, receipt migration or permission/replay-semantics change is necessary. This deliberately leaves unrelated existing POST feature/replay behavior unchanged; it does not claim S40/S48 fixes older template_roles receipt behavior.

## Independent flags, old clients and UI

| Situation | Required behavior |
| --- | --- |
| Both off | Exact old public template/project/preview/command shapes, ordering and UI. No new risks instantiated, provenance/diff controls or feature-bearing outgoing bodies. Core Project source ID/version still persist as today. |
| register on, people off | Risk editor/preview and initial risk snapshot work. S48 reads are omitted. Existing captured lineage remains internally unchanged; it is not exposed through a risk response. |
| people on, register off | Version projects/project information/draft diff work; hidden stored risks survive source cloning and structure saves. Diff must not expose register-gated risk content or claim an unavailable section has no changes. Mark displayed diff coverage explicitly; re-enabling risks compares the preserved actual data. Apply the same discipline to template_roles and template_working_days visibility. |
| Stored-on risks then register off | Omitted structure risks preserve seeds; NewDraft copies hidden seeds; ordinary off project creation creates no risk seeds and is never retroactively backfilled when re-enabled. No AddPack or project-copy change. |
| Stored-on lineage then people off | Save/publish/retire preserve the draft's original baseline/map. A new clone must never copy its source row's **own ancestry** as though that were the new clone's origin. Recommended gate: an off-mode new clone has null/unrecorded S48 baseline while its content is preserved; a later enable honestly reports NotRecorded. Root must freeze whether future off-mode copy events instead capture private exact provenance; neither option permits historical inference or copying false ancestry. |
| Older structure client omits additions | Preserve stored risks and immutable copy metadata. Explicit on-mode empty risks clears; null/omitted does not. New read-only lineage/provenance is never accepted as a write override. |

Mount within the existing Templates editor/version navigation and existing TemplateSettings slot. Keep risk inputs ordinary generated draft fields and submit through the existing guarded whole-structure save; show PM/Open preview with actual seeds. Distinguish original copied version, newer version/status and draft unsaved edits. The server diff is tied to draftRowVersion; label it as saved changes and withhold a “current/no changes” claim while the editor is dirty until the normal explicit Save refreshes it. No autosave or publish to obtain a diff. Invalidate template/draft/version-project/project-provenance reads after their canonical changes, preserving viewer/flag/project/version/query cache partitioning and error withholding.

Keep Templates' existing phone notice and desktop/tablet editor; do not silently invent a phone editor. The project information line must wrap at375px, preserve Settings/Template navigation and normal keyboard focus. Existing refusal, safe-form drafts and Cancel handling remain. A current RowVersion conflict cannot silently promote/refetch and replay a save; retain user edits for an explicit retry. Same explicit Idempotency-Key is reused for retries of one POST where the existing transport/command supports it. PUT save's stale-version refusal is not converted to a new auto-retry; no new RequestId protocol is claimed.

## Proposed ownership, dependencies and focused proof

Root freezes exact DTO/schema/baseline/flag contracts before implementation and retains shared schema/registry/Text/i18n/generated/mount/cache/integration ownership. Proposed disjoint new files:

| Owner | Proposed files and boundary |
| --- | --- |
| B backend helpers | New `Features/TemplateRiskSeeds.cs`, `TemplateVersionProvenance.cs`, `TemplateDraftDiff.cs`; new `tests/Hub.Tests/Api/TemplateRiskSeedTests.cs`, `TemplateVersionProvenanceTests.cs`. Helpers reuse canonical permissions/transaction and produce typed facts, not parallel command engines. |
| One UI writer | New `web/src/pages/template-risk-seeds.tsx`, `template-version-provenance.tsx`; new `web/tests/template-risk-provenance.cjs` and `.fixture.cjs`, using generated contracts and existing form controls. |
| Root existing seams | `Data/Entities.cs`, `HubDb.cs`, one migration/snapshot, `Records/ProjectTemplate.cs` and `Risk.cs`; `Features/Templates.cs` and `Projects.cs` DTO/maps/save/NewDraft/Hook integration; `Templates.tsx` and `projects/FromTemplate.tsx` mounts, project/settings type propagation, Text/i18n/cache/generated/test inventory. Only one writer per existing file; root may issue a later exact whole-file loan instead of simultaneous seam edits. |

Focused acceptance/refusal/unknown tests to author and then execute only after root freeze:

1. **AC70:** normal commands create/publish a template with two distinct risks, its authoritative published GET supplies both seeds for wizard preview, create project under actual PM and assert two Open ordinary risks with exact scores/text/keys/source refs/owner, null non-seed facts and expected activity. Edit/re-publish template and separately edit a project risk; neither propagates into the other. Unticked disciplines do not drop global risks; AddPack/CopyStructure do not clone them.
2. Validate score0/4/null, blank/overlong fields, duplicate/foreign refs and forged ownership/status/lineage; editor/noneditor/ReadOnly, non-Draft, stale RowVersion and unavailable source refuse without data/key/activity/receipt changes. An invalid seed or injected failure during project hook rolls back **the whole project creation**. Retry one explicit project-create receipt produces one project and two risks, not four; preserve existing duplicate-project-number/refusal contracts.
3. **AC81:** create actual published versions through v5, with project250101 created from v3. Both version list and project settings prove exact v3 identity and v5 information. After all informational reads, project/risks/structure/versions/activity/Seen/receipts/outbox remain unchanged. Different families' v3s do not mix; project-copy/AddPack are not origin events.
4. Copy retired v3 with v5 present; unchanged clone has no content changes despite fresh IDs, then rename/change/reorder/delete/add rows of each supported kind, duplicate titles and relationship/dependency edits. Show exact added/removed/changed identity/content against v3 through repeated saves. Request Draft again from v5 while v3-based Draft exists; return it and preserve its v3 baseline. Verify captured source and cloned values are one snapshot, including concurrent source status/draft/publish cases.
5. Legacy/null, missing/incoherent source, unsupported/corrupt snapshot schema and no-copy new family are explicit unknown/not-recorded states, with null unknown counts; never compare to latest or invent v0/empty history. Valid newer Draft without a number is not a released version. Retired/newer/no-current-published cases use actual status and the root-frozen wording.
6. Current project visibility is applied **before** per-version counts/paging, including a Template Editor without restricted-project access. Include actual visible archived/cancelled projects; paginate beyond a page without dropping usage. Full retired/draft template structures retain canonical editor refusal. Project pages and the complete embedded diff are coherent with their asOf/version snapshot.
7. Exercise both independent flags and stored-on→off→on for cloning, whole-structure editing, preview and project creation, including omitted arrays and roles/working-day combinations. Stored data survives; legacy wire/order is exact off; no hidden-section values or fake zero-diff conclusion leak; no project backfill or ancestor substitution occurs.
8. Browser proof uses actual generated types and finite request guards: two risk fields/seeds, version-specific project rows, C3→current5 information without writes, actual changed/removed/added diff, dirty/saved distinction, stale/refused save retaining input, Cancel, cache-key refusal and flag-off behavior. Templates375px retains the established phone notice; project Settings375px wraps and returns focus naturally. Fixtures prove rendering, provider tests prove storage/authority. Reuse adjacent TemplatesTests shape/role/basis/snapshot checks and ordinary risk/create-project tests; do not rewrite expectations to hide drift.
9. Keyed POST replay matrix: issue each affected old POST successfully with both additions on, switch flags off, replay the exact same request/key and assert the original command/preview wire never contains Risks/Screen or another enriched object at any depth, with no duplicate mutation. Subsequent GETs omit current-off additions; re-enable and GET actual retained data. Specifically test preview, NewDraft and Publish and unchanged status/Location/legacy fields. Do not add a handler flag check and mistake it for replay protection. PUT save separately proves current flag projection/refusal; no generic receipt or permission semantics are rewritten.

Root freeze items (no decision-file changes here): off-mode future copy capture policy; exact latest-released/current-Published wording and summary visibility; partial diff presentation when another existing feature flag hides a section; whether risk-only templates remain subject to the existing milestone-or-deliverable publish guard (recommended unchanged). These are explicit, not silently narrowed requirements or user approvals requested by this plan.

READY/HOLD: source/spec inspection only. No app/test/schema/locale/generated/config edits, tests/builds/Node/browser/EF/services/flags/helpers/dependencies/commit/push/deploy/Accepted operation. Root review and exact future source loans are required before implementation.
