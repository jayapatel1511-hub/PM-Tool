# G56 S40/S48 Template contract freeze

Root2026-11-02. B's reviewed g56-template-risk-provenance-plan.md governs with these refinements. Independent source review READY; implementation/tests UNRUN.35/49 checked. No decision/permission/§10 change or new flag.

## Schema, seeds and exact source

Root supplies `TemplateRisk : Entity` with TemplateId/SortOrder/Title/Probability/Impact/Mitigation?/TriggerIndicator?, `HubDb.TemplateRisks`; nullable `Risk.TemplateRiskId` origin; nullable `ProjectTemplate.CopiedFromTemplateId` and string `CopiedFromSnapshot` jsonb. No defaults/backfill/inferred ancestry. Root owns all Data/schema/migration/registry fields and FKs. Seed title max300, scores explicitly1..3, mitigation4000, trigger2000; no owner/status/review/discipline fields from client. Reuse existing canonical validation and reject forged extra fields in new seed input. Actual PM/Open/no invented default score on missing input.

`register_screens` independently owns S40 risk editing/read/initial-project instantiation. `people_screens` owns S48 provenance/diff. Existing template_roles/working-day gates keep their behavior. Global template risk seeds do not disappear with excluded disciplines. Seed risks ONLY in initial TemplateHook inside existing outer project transaction, with actual p.ProjectManagerId, normal key/activity and actual origin IDs. Do not seed via shared Instantiate/AddPack/project-copy, reapply a template, alter project provenance or propagate later template/project edits. Roll back whole create on seed failure; receipt retry one project/two risks once.

Published/Retired source seed content remains immutable through ordinary commands. Risk origin FK is nullable/Restrict; TemplateRisk parent cleanup follows existing child pattern without deleting project risks. Root will add checks/lengths/keys consistent with existing model; ordinary Risk defaults/CRUD and register workflow remain unchanged.

## Copy snapshot, concurrency and diff

Capture exact selected source identity/content plus actual clone mapping atomically in NewDraft; source may be retired v3 while v5 exists. Use same family transaction/serialization for NewDraft/Publish and required consistency seams, rechecking inside transaction. No new receipt engine. Existing-draft200 versus new201 behavior remains; returning an existing draft NEVER recaptures/rebases its source.

Root adopts the proposed typed versioned snapshotV1. Raw snapshot/clone-map fields are server-owned, never client input or public JSON reads. Header, disciplines, milestones, deliverables, tasks, roles, basis suggestions, risks and dependency pairs participate. Match through actual recorded refs/map (disciplines by global ID), normalize relationships; ignore generated IDs/versions/timestamps as content. No title/latest/version-minus-one/audit-reason matching. Preserve duplicates as distinct records. New/removed/reordered/content changes are truthful against actual copied source.

When people_screens is OFF, new clones leave lineage NULL; they never copy the source row's own ancestry. Preserve existing baselines and hidden seed content through unrelated saves/clones. No GET repair/backfill. Legacy/standalone/missing/corrupt/future-schema/incoherent baseline states remain explicit NotRecorded/Unavailable with null unknown counts. Do not invent empty history/v0.

Diff reads use current persisted draft, not unsaved form content. Each metadata response uses one consistent read snapshot and actual observed draft version. Page large change lists with exact totals/stable identity order. Hidden feature sections are omitted with explicit incompleteSections; counts are visible-section counts and must never claim global no changes when incomplete. Keep existing risk-only publish-content guard; no incidental workflow change.

## Provenance and privacy

Project's existing CreatedFromTemplateId/TemplateVersion are authoritative; no new project source table/field. Prefer NEW flag-gated GET `/projects/{id:guid}/template-provenance` mounted by B within TemplateEndpoints.Map and normal current project access (track:false). It returns minimal typed source/newer-version facts; do not change existing ProjectDetail schema/Projects.cs. Existing TemplateSettings in FromTemplate.tsx consumes the read and preserves old branch off. Error/current-key refusal hides cached enhanced facts; viewer/project/flag keys. No project/Seen/version/activity write.

Newer vN exists means actual highest numbered Published/Retired version in coherent source family; expose status/current Published separately. Never unnumbered Draft, another family's v3, or substitute latest for recorded project v3/name. No apply/update action. Minimal provenance does not grant retired/draft structure access.

Template version project list/count uses exact CreatedFromTemplateId after current Access visibility, including visible historical archived/cancelled but no deleted projects. Stable paging and exact visible counts; no workspace filter, family/version-number coincidence or AddPack/copy inference. Batch list/family counts. While people_screens ON, scope the EXISTING TemplateListRow.Projects wire field too; adding a safe companion while retaining an unsafe old total is insufficient. Preserve old count behavior OFF.

## Cached replies and old wire

Generic POST receipts can replay before handler flags. For these two additions keep ALL existing POST command responses legacy-shaped, including NewDraft/Publish/preview paths which may reuse TemplateRead. Omit new Screen/Risks/diff/project-list extensions from these POST reply projections even when enabled, then invalidate/navigate and obtain authoritative enhanced GET. Old POST body/member/order/status/receipt semantics remain. Do not change global receipt behavior or assume handler flags guard stored new response fields.

New template-risk information needed in creation preview may use actual immutable source GET, without adding a risk-only field to old cached POST preview replies. If source inspection shows a truly needed extra read route, propose its narrow GET contract before implementation; no global middleware rewrite. Existing PUT structure save is not generic POST cached; validate new properties/flag before mutation and project current enhanced data safely. Feature-bearing risk input off refuses404; omitted old risk fields preserve hidden stored data. Typed nullable appended fields preserve old JSON shape; no shadows/any. Unknown/future state is not a match or zero.

Keep current canonical edit/view/publish/retire/project-create rights, versions, reason/audit/notice boundaries. UI stale/uncertain saves keep actual original intent/version; no automatic fresh-version promotion. Preserve DesktopOnly template phone notice, settings375px reflow, dirty guards, natural navigation and source context. Supply exact proposed locale keys/text early for root; use generated types after root generation.

## Exact B ownership

Existing `src/Hub.Api/Features/Templates.cs`, `web/src/pages/Templates.tsx`, `web/src/pages/projects/FromTemplate.tsx` (TemplateSettings/source UI only; preserve other project creation logic).
NEW `src/Hub.Api/Features/TemplateRiskSeeds.cs`, `TemplateVersionProvenance.cs`, `TemplateDraftDiff.cs`.
NEW `tests/Hub.Tests/Api/TemplateRiskSeedTests.cs`, `TemplateVersionProvenanceTests.cs`, `TemplateDraftDiffTests.cs` if meaningful pure/real diff checks need separation.
NEW `web/src/pages/template-risk-seeds.tsx`, `template-version-provenance.tsx`, `web/tests/template-risk-provenance.cjs` and fixture; own handoff/locale proposal docs.

Root owns schema/registry/locale/cache/generated/inventory/build/runtime/Chrome. No Projects.cs, Registers.cs, Domain/Templates.cs, global middleware or other shared edits. Request exact seam if needed, do not edit another lane. No execution/helpers/services/flags/dependencies/commit/push/deploy/Accepted. Author the plan's real AC70/81/rollback/authority/flag/snapshot/paging/privacy/diff/refusal cases plus strict actual-SPA synthetic proofs. Return READY/HOLD hashes and exact unrun checks. Current A author/C planning/D inventory refinement may run in parallel; all compilers/generation/runtime wait for source freezes.
