# Generation17 S32 exact own-Time seam

Implementing only assigned Time.cs/new TimeWeekRead.cs, Time.tsx/new TimeWeekGrid and dedicated tests/scripts. Previous source held. No schema/permission/canonical capacity changes.

New capacity_screens-only pure GET `/time/week` with existing TimeQuery (From,To,ProjectId,TaskId,UserId,Scope). Reject scope=all/other UserId and non-Monday/exact-seven-day ranges; UI retains original list/correction path for those URLs and never requests this endpoint. No supplied person ID selects capacity. Authenticated me only. RepeatableRead snapshot; reuse Time.Visible for current/prior actuals and PersonCapacity.Load for the authenticated own week.

DTO names in partial TimeEndpoints:
- `TimeWeekDay(Date,NormalHours?,AvailableHours?,TimeAwayHours?,HolidayHours?,AdditionalHours?,Provisional,Unavailable)`; provisional daily numbers are explicitly labelled provisional; weekly CapacityHours null if any missing/provisional day. No schedule/HR/reference/reason fields.
- `TimeWeekTask(TaskId,TaskKey,TaskName,ProjectId,ProjectNumber,ProjectName,CanCreate,Deleted,Assigned,PreviousWeek,Entries:TimeEndpoints.Row[])`; Entries are current-week actual own entries only, preserve distinct IDs/notes/versions/EditNeedsReason. Assigned/PreviousWeek identify actual source membership only. Historical deleted actuals retain canonical reads and edit rights, never create.
- `TimeWeekView(From,To,Today,PersonId,CapacityHours?,Days,Tasks,AsOf)`; complete authorized distinct union, no first-page truncation. Project/task filters apply before projection. Counts/exports remain only real canonical entries.

Root seams: generate API/types and route inventory; exact locale/error JSON follows. The frontend uses root's requested `['time','week',filters,'capacity_screens']`, so existing TimeEntry invalidation `['time']` refreshes both reads. Root will add `['time']` to Task/canonical own-availability/schedule/holiday/User/Member changes and refresh the old `['time-tasks',term]` picker for actual Task changes. No lane edit to shared cache or metadata.
