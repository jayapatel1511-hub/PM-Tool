# S04 shared requests / ownership extension

Generation 8 Work lane owns Today/reviewer presentation and the temporary exclusive Reviews.cs/Reviews.tsx loan. No edits to shared files will be made without this extension.

1. Please loan `web/src/components/planner/MyWeekStrip.tsx` exclusively to this lane for S04 (its sole caller is MyWork.tsx). Proposed optional `view: 'today' | 'overview'` prop, only acted on under `screen_patterns`: Today collapsed by default, Overview expanded by default, per-person/device/view expansion stored locally, explicit `myWeek`/`planningEntry` links open it. Enabled summary uses the existing grid's Confirmed+Expected and Remaining, never Possible as planned or a second time-away subtraction. Partial/provisional availability must label free unavailable or at most, not claim spare capacity. Disabled behavior remains untouched. My Work's enabled Today focus list will render first; the compact My Week line and attention follow it. Overview keeps My Week above all sections, expanded.

2. Please add optional `exclude?: string[]` to shared `InlinePerson` in `web/src/components/hub/fields.tsx` and forward it to its existing PeoplePicker. Default empty/undefined preserves every current caller and its autosave/refusal/retry UI. This lets TaskPanel exclude the performer while self-review is disabled without duplicating autosave or person-picker internals.

3. Locale additions requested:

```
'workReview.performer': 'Performer',
'workReview.reviewer': 'Independent reviewer',
'workReview.missing': 'Reviewer missing',
'workReview.independentHint': 'The performer is excluded while self-review is disabled',
'workReview.packageIndependentHint': 'Source authors are excluded while self-review is disabled',
'workReview.selfAllowed': 'Self-review (allowed by setting)',
'workReview.sourcePerformers': 'Current source performers',
'workReview.performersUnknown': 'Performer not recorded',
'workReview.startUnavailable': 'Start checks unavailable',
'workReview.assignmentIncomplete': 'Assign an active, independent reviewer to every required discipline',
'mywork.weekCompact': 'My Week: {planned} planned, {free} free',
'mywork.weekFreeUnavailable': 'Free unavailable',
```

`planned`/`free` receive the established `hoursLabel` values with `h`, avoiding doubled units. Existing `mywork.weekShow/weekHide`, planner partial/unavailable and other labels are reused.

Review-package caveat being handled in the owned read projection: required reviewer IDs are nonnullable by canonical schema, so missing/ineligible reviewers are represented as read-time availability failures, not a nullable-schema change. Start readiness will call the existing ValidateRound with `lockRows:false`, retaining every server refusal and permission. Package source performer facts must be actual current source-task assignees/source owners, labelled as current and separate from the complete canonical author set used for independence; no guessed single performer or creator-as-performer mapping.
