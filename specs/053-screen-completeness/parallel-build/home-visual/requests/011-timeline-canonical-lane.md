# Required root bridge: canonical task lane on TimelineTask

Generation11 supplemental Work visual loan is executing in Board.tsx, projects/Timeline.tsx and Gantt.tsx only. Board already has TaskQueries.Row.lane. Current `TimelineEndpoints.TimelineTask` has Status but no canonical lane, and both project Timeline and workspace Gantt consume it. Frontend must not invent a status→lane mapping.

Please root-own this narrow additive read bridge in `src/Hub.Api/Features/Timeline.cs`:

- Append nullable string `Lane = null` to TimelineTask with JsonIgnore WhenWritingNull, preserving all constructor/default and off shapes.
- In the existing shared Build projection, set it only when visual_system is enabled, using the existing **canonical** `Workflow.Lane(t.Status)` (the same function already used by TaskQueries). Do not introduce another mapping or change task/filter/status semantics. The shared Build already feeds both routes.
- No lane field on TimelineDeliverable: deliverable statuses are a separate domain. No schema/migration/permission/command changes.
- Refresh generated API/types only in the root integration lane. Suggested focused API evidence: off omits Lane; on returns exactly the same lane as the same permitted task's task-list row, across all four buckets and On Hold, for project `/projects/{id}/timeline` and cross-project `/timeline`; canonical flags/access/filter parity and unchanged other properties.

I will consume optional `lane` only under visual_system and use a neutral unknown role if the field is missing/invalid; no fallback derived from status. Existing shape and overdue-solid/legend work can proceed independently. Please confirm the property name before I bind the dependent branch. Root owns these C#/generated edits; this lane will not touch them.
