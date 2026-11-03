# G53 S46 Search API contract

Draft implementation contract, 2026-11-02. B owns only Search.cs, new SearchScreenReads.cs, new SearchScreenTests.cs and this file. No execution, generated/shared/schema changes or QA24 runtime operation. All additions are under `people_screens`; the prior Search/person-reference contract stays intact.

`GET /api/v1/search` retains `q,type,includeArchived,limit,page` and adds optional single-value strings `projectId,status,disciplineId`. IDs name canonical Project and global Discipline, not ProjectDiscipline. Parse/validate new inputs only while the flag is on; while off, even malformed new controls are ignored like previously unknown query parameters. Missing/blank controls mean no restriction. Active malformed/empty GUIDs, an unknown status or a filter inapplicable to an explicit tab return the normal 400 validation response. A syntactically valid but unavailable/unknown ID never proves existence: it simply yields no matching applicable records under existing visibility.

Status is **per explicit type tab**: it requires a known `type` with status support, and accepts that tab's exact case-sensitive `statusValues`. Do not carry a Task status into another tab. No type + status is a 400. In an all-types request, project/discipline constrain only types advertising those dimensions; inapplicable types (for example global directory People with projectId) keep their existing cohort. An explicit inapplicable tab/filter combination is a 400, preventing a misleading applied control. Frontend clears unsupported filters on tab change.

Every group count is its total after text and applicable filters, before existing paging. `type` still returns only that group's rows/count; all-types still returns the existing ordered groups/counts. New filtered searches suppress `exact` rather than jumping to a candidate outside the selected result semantics. With no new filters, legacy exact lookup, original ranking, paging and existing hit fields/targets/person metadata/CanViewWork remain unchanged. Filter-only empty `q` keeps legacy empty rows/counts, but returns screen metadata while on so controls can be rendered; no broad blank-query record discovery.

Root's frontend count convention: the separate all-types tab-count request omits per-tab `status`; the selected tab displays its own filtered response's `counts[type]`. Do not present the unfiltered all-types count as the selected status-filtered total. Clear incompatible controls on tab switch.

| type | project | status | discipline |
| --- | --- | --- | --- |
| projects | own ID | ProjectStatus | project's actual ProjectDiscipline membership |
| tasks | ProjectId | TaskStatuses | actual ProjectDisciplineId → DisciplineId |
| deliverables | ProjectId | DeliverableStatus | actual ProjectDisciplineId → DisciplineId |
| milestones | ProjectId | MilestoneStatus | optional actual ProjectDisciplineId |
| decisions | ProjectId | DecisionStatus | unsupported; no canonical discipline field |
| handoffs | ProjectId | HandoffStatus | actual sending OR receiving discipline |
| reviews | ProjectId | ReviewStatus | actual ProjectDisciplineId |
| changes | ProjectId | ChangeStatus | actual ProjectDisciplineId |
| submissions | ProjectId | SubmissionStatus | unsupported; no single canonical discipline field |
| allocations | ProjectId | AllocationStatus | unsupported; do not substitute person's home discipline |
| planning | actual optional ProjectId | existing display status: Self, Draft, Published, Confirmed | actual optional ProjectDisciplineId |
| design-basis | ProjectId | current DesignBasisVersion.Status / BasisStatus | actual ProjectDisciplineId |
| constraints | ProjectId | ConstraintState | exact typed live Task/Deliverable target's discipline |
| commitments | ProjectId | CommitmentState | exact typed live Task/Deliverable target's discipline |
| comments | ProjectId | unsupported (deleted already excluded) | unsupported; no canonical discipline field |
| people | unsupported; directory cohort unchanged | Active / Inactive from IsActive | actual optional AppUser.HomeDisciplineId |

Milestone Complete/Cancelled use its canonical completion flags (Complete takes precedence, including completed-then-cancelled milestones with both flags true); remaining legal statuses exclude both flags and match existing persisted MilestoneState only, without evaluation/writes or inventing missing state. Basis status requires its actual current-version reference; missing current version does not become Proposed. Planning Self means CreatedBy == PersonId (as the existing result status); other values match non-self entries' actual Visibility. Discipline never comes from owner/performer names, home discipline of another record's person, linked-item guesses or missing-value substitution.

While on, People text also matches actual nullable JobTitle and the name of its referenced HomeDiscipline. This uses ordinary AppUser directory fields already supported by `/users` matching, not PersonFacts notices/certifications/date facts or an expanded person-panel/Staff scope. Null/missing home discipline is no match, and no extra private person facts are returned. Existing name/email prefix ranking remains the same; new title/home-only matches occupy the old non-prefix tier.

Response adds an omitted-null optional **`screen`** after the old four top-level fields. Its uniquely prefixed DTOs are:

```text
SearchScreenMetadata {
  applied: SearchScreenAppliedFilters { projectId: Guid?, status: string?, disciplineId: Guid? },
  tabs: SearchScreenTab[] {
    type: string, project: bool, status: bool, discipline: bool, statusValues: string[]
  },
  projects: SearchScreenProjectChoice[] { id, projectNumber, name }, projectsTruncated: bool,
  disciplines: SearchScreenDisciplineChoice[] { id, name, isActive }, disciplinesTruncated: bool
}
```

`tabs` follows the authoritative sixteen names/order in `SearchEndpoints.Groups` / `ScreenLists.SearchGroups`, including Planning immediately after Allocations; the legacy `groups` JSON field order is unchanged. Project choices come only from the caller's already visible includeArchived-aware project query **before** selected project filtering, ordered by number/ID; discipline choices are existing reference-directory rows, ordered by name/ID. Each options list is capped at 200 with a 201st-row truncation probe, never an unbounded record load or a per-result query. Metadata choices do not add any record, person-panel or Staff authority. No recents endpoint or server recents persistence: A owns browser-only last-ten/Clear.

Self planning entries legally persist `Visibility = Confirmed` (`ck_planning_self_visible`) and still display/filter as Self. The Confirmed search filter applies only to non-self entries whose actual visibility is Confirmed; it does not relabel a self entry by its storage value.

Focused source tests: EIT by real JobTitle; home discipline by actual nullable ID/name with no fake match; all dimension predicates/metadata and representative status semantics; per-tab status refusal; project/discipline/hidden/unknown IDs; archived/optional/derived/current-version cases; counts before paging and exact suppression; type/count/ranking/off-payload/person-reference parity; bounded option truncation; readonly/no private-facts expansion. Tests are authored under the loan but run only by root after all writers HOLD.
