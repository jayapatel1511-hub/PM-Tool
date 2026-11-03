# S39 exact locale proposal

Root owns en.ts. A consumes these exact keys/copy; no approved category values are seeded.

| Key | English text |
|---|---|
| decisionMetadata.title | Decision context |
| decisionMetadata.category | Category |
| decisionMetadata.notRecorded | Not recorded |
| decisionMetadata.options | Options considered |
| decisionMetadata.optionsHint | Optional: record up to five options, in the order considered. |
| decisionMetadata.option | Option {n} |
| decisionMetadata.addOption | Add an option |
| decisionMetadata.removeOption | Remove option {n} |
| decisionMetadata.none | None recorded |
| decisionMetadata.rationale | Rationale |
| decisionMetadata.rationaleHint | Optional: explain why this outcome was chosen. This is separate from the decision text. |
| decisionMetadata.edit | Edit decision context |
| decisionMetadata.inactive | {name} (Inactive) |
| decisionMetadata.unknownCategory | Selected category ID: {id} |
| decisionMetadata.unavailable | Decision context is unavailable. |
| decisionMetadata.lagTitle | Decision lag by owner organisation |
| decisionMetadata.lagBasis | All currently Decided decisions in this project. Uses current registered owner organisations and dates; register filters do not change these figures. |
| decisionMetadata.internal | Internal |
| decisionMetadata.external | External organisation |
| decisionMetadata.median | Median: {days} days |
| decisionMetadata.samples | {n} decided |
| decisionMetadata.cohort | {total} decided; {attributed} attributed. Excluded for dates: {invalid}; for organisation: {unattributed}. |
| decisionMetadata.noSamples | No attributable decided decisions with valid dates. |
| decisionMetadata.uncertain | This save may have completed. Retry checks the same request; your entries and original version stay locked. |
| decisionMetadata.retry | Check the same save |
| decisionMetadata.stale | This decision changed. Close this form and review it before starting another save. |
| decisionMetadata.emptyOptions | No options were considered. |

Backend validation reuses existing error.required/error.invalid/error.too_long and canonical permission/refusal text. No new server text keys are required by A.
