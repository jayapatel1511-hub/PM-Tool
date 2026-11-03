# G56 S40/S48 exact locale proposal

Root owns Text/i18n. B will consume these keys; no locale source edits by B.

| Key | English text |
| --- | --- |
| tpl.risks | Template risks |
| tpl.riskAdd | Add risk |
| tpl.riskRemove | Remove risk |
| tpl.riskTitle | Risk title |
| tpl.riskProbability | Probability |
| tpl.riskImpact | Impact |
| tpl.riskMitigation | Mitigation |
| tpl.riskTrigger | Trigger indicator |
| tpl.riskHint | Each risk becomes an Open risk owned by the new project's PM. Later template edits do not change project risks. |
| tpl.riskPreview | {count} risks will be created as Open, owned by the project's PM. |
| tpl.riskNone | No template risks recorded. |
| tpl.riskChoose | Choose a score |
| tpl.dataUnavailable | Template information is unavailable. Reload to try again. |
| tpl.versionProjects | Projects created from this version |
| tpl.versionProjectsNone | No projects you can view were created from this version. |
| tpl.versionProjectsCount | {count} projects you can view |
| tpl.draftChanges | Changes from copied version |
| tpl.diffSource | Compared with {name} v{version}. |
| tpl.diffNotRecorded | The copied-from version was not recorded. No comparison is available. |
| tpl.diffUnavailable | The recorded comparison baseline is unavailable. |
| tpl.diffSaved | This comparison uses the saved draft. Save your edits to refresh it. |
| tpl.diffNone | No changes in the displayed sections. |
| tpl.diffIncomplete | Some template sections are hidden by current feature settings. These counts cover only the displayed sections. |
| tpl.diffCounts | {added} added · {removed} removed · {changed} changed |
| tpl.diffAdded | Added |
| tpl.diffRemoved | Removed |
| tpl.diffChanged | Changed |
| tpl.diffBefore | Before |
| tpl.diffAfter | After |
| tpl.provenanceSource | Created from {name} v{version}. |
| tpl.provenanceNewer | v{version} exists ({status}). |
| tpl.provenancePublished | Current published version: v{version}. |
| tpl.provenanceNoNewer | No newer numbered version is recorded. |
| tpl.provenanceUnknown | Source template information is incomplete or unavailable. |
| tpl.provenanceInfo | Information only. This project and its items are unchanged. |
| tpl.kind.Header | Header |
| tpl.kind.Discipline | Discipline |
| tpl.kind.Milestone | Milestone |
| tpl.kind.Deliverable | Deliverable |
| tpl.kind.Task | Task |
| tpl.kind.Dependency | Dependency |
| tpl.kind.Role | Role |
| tpl.kind.Basis | Design basis suggestion |
| tpl.kind.Risk | Risk |

Existing common labels cover name, status, field, Previous/Next, loading/errors and Save/Cancel. Validation reuses error.invalid/error.required/error.too_long/risk.level; no new server text is proposed.

Exact diff field labels (prefix each key with `tpl.field.`):

| Suffix | Text |
| --- | --- |
| name | Name |
| description | Description |
| projectTypeId | Project type |
| offsetDayKind | Offsets count |
| defaultIncluded | Included by default |
| order | Order |
| milestoneType | Milestone type |
| anchor | Date anchor |
| offset | Day offset |
| completesPhaseId | Completes phase |
| clientFacing | Client facing |
| disciplineId | Discipline |
| deliverableTypeId | Deliverable type |
| milestoneRef | Milestone |
| requiresReview | Requires review |
| deliverableRef | Deliverable |
| priority | Priority |
| hours | Planned hours |
| assignTo | Assign to |
| startMilestoneRef | Start milestone |
| startOffset | Start offset |
| endMilestoneRef | End milestone |
| endOffset | End offset |
| note | Note |
| basisKind | Basis kind |
| scope | Scope |
| statement | Statement |
| numericValue | Value |
| units | Units |
| sourceSystem | Source system |
| stableSourceId | Source ID |
| sourceUrl | Source link |
| declaredRevision | Declared revision |
| probability | Probability |
| impact | Impact |
| mitigation | Mitigation |
| triggerIndicator | Trigger indicator |
| predecessor | Predecessor |
| successor | Successor |
