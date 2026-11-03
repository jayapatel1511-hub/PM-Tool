# S03 shared presentation request

The work lane is implementing Task-detail source-use comparison, exact other-open-task effects for existing materialized blockers, and enabled Weekly cause dedup using the canonical `RecordContextView.Inputs` already returned. No pure action rule, core reader, registry or schema is changed here.

Please expose two existing shared presentation functions from `web/src/components/hub/record-context.tsx` (aliases are sufficient), so the owned waiting-input components do not duplicate actor-state or action-label mapping:

```
export const recordContextActionLabel = actionLabel
export const RecordContextActors = ActorList
```

The existing private functions can retain their names and internal callers. The work lane will import these aliases. No behavior changes inside the shared component are requested.

Locale additions requested under `workInputs.*` (ordinary `t` calls):

```
'workInputs.title': 'Waiting on',
'workInputs.resolver': 'Who must resolve it',
'workInputs.alsoOne': 'Also holds up 1 task',
'workInputs.alsoMany': 'Also holds up {n} tasks',
'workInputs.otherTasks': 'Other open tasks',
'workInputs.sourceUses': 'Source uses',
'workInputs.used': 'Revision used',
'workInputs.current': 'Current registered revision',
'workInputs.assessment': 'Assessment status',
'workInputs.noAssessment': 'No assessment recorded for the current registered revision',
'workInputs.assessmentUnavailable': 'Assessment status unavailable',
'workInputs.registered': 'Manually registered',
'workInputs.checked': 'Source checked',
'workInputs.unavailable': 'Source facts unavailable',
```

Root retains API/OpenAPI generation and TypeScript checks. New endpoint DTOs are owned additions in Tasks.cs/new work-only read helper; no shared model migration.

Scope caveat from current source inspection: `EvaluationService` materializes task blockers from predecessor, decision and manual sources; it does not independently add handoff/readiness-constraint blockers. The S03 presentation must consume existing actual blocker evidence, not invent a second readiness/blocking rule. Handoff/constraint kind support applies when those actual references exist in the materialized blocker list (including legacy data). This distinction must remain visible in acceptance; a normal-write handoff→task readiness relationship is not automatically proof of task IsBlocked.
