# Proposed next capacity batches: S31, then S32

READ-ONLY proposal from maintained sources on2026-11-01. Backend generation12 and all source remain held under current generation13. Only own notes change. No application/test implementation or code/test/build/browser/generator execution, helpers or goals. Root dispatches after checkpoint16 verification. S29 optional person facts stays deferred.

Both slices belong to **capacity_screens** (PartE FR53–55), independent of visual_system/people_screens. Prefer sequential bounded batches: S31 allocation shifting, then S32 own Time week. No schema, role, workflow status, approval or capacity-rule change is presently required.

## S31: Shift allocation dates (FR/AC53)

### Existing authority to reuse

`Features/Allocations.cs` **Edit** is the canonical material mutation, not a date-only update:

- Coordination.Run: project access/lock, Tx.Run, payload-hashed RequestId receipt, Complete-project correction reason. Coordination.Version checks the allocation version. ProposeAllocation plus PM-or-creator is the existing edit gate; only Proposed/Confirmed may change.
- Validate: current eligible person, purpose, inclusive duration≤366days, positive hours, unique in-range day overrides/work links, current task/review eligibility and canonical person-calendar AllocationRules.Spread.
- Confirmed edits lock the person. Old links are released and day overrides replaced inside the transaction; AfterMaterialEdit makes Confirmed→Proposed, and TouchDates invalidates old confirmed dates. Confirmation provenance remains, as existing `Date_only_edit_withdraws_confirmation_and_requires_reconfirmation` proves.
- The parent version is touched for child-only changes. Check.Reason, db.Audit.Note and NotifyChanged preserve reason/activity/outbox semantics. Subsequent confirmation still recomputes current date versions and daily overload/reason rules.

### Proposed bounded implementation

New capacity_screens-guarded preview/apply routes under the project allocations boundary. Preview is a pure read; apply is one atomic transaction through a narrowly extracted existing material-edit helper. Do not call N HTTP edits with separate commits or duplicate mutation/permission rules.

Inputs: nonzero integer Weeks; Selected/WholeProject mode; distinct IDs and expected allocation RowVersions; required reason for apply; RequestId. Server supplies existing person/hours/purpose/status. WholeProject preview enumerates every eligible row, independent of list paging/search/filter; apply rechecks that full ID/version set, rejecting newly added/removed/changed rows rather than moving unpreviewed records.

Translate exactly7×Weeks DateOnly days with checked overflow: **FromDate, ThroughDate, each DayInput.WorkDate and each active LinkInput.WorkDate** together. Retain every hour/task/review/person/purpose value. Do not shift task/review due dates, packages, milestones, schedules, leave or roles; do not round/clamp dates or impose a planner horizon absent from allocation rules.

Preview row: allocation ID/version, old/new inclusive dates, existing permitted person ID/name/state, status and will-require-confirmation marker. It must identify whose load changes but need not expose cross-project load totals. Reuse existing allocation identity scope; do not force this date preview through a capacity read that leaks hidden work or rejects authorized date changes because unrelated work is hidden. Re-run canonical target reference/calendar/day validation; invalid rows block Apply. Preview writes no receipt, audit, notification or record.

Minimal receipt reuse: Coordination.Run can mutate all selected records and return the stable first ID-sorted allocation as its standard Coordination.Result; UI refreshes the list instead of treating that one version as every row's version. Root should confirm this normal response shape before dispatch. It avoids a batch receipt schema. Payload hash includes IDs/versions/weeks/reason/mode; same-key retry does not move twice and changed payload is rejected.

Lock project then affected confirmed people in stable ID order. Release all selected old links before inserting replacements within the transaction, preserving the active person/work/day unique index. Any unselected collision, stale/denied row or now-invalid reference/calendar rolls back every parent, child, date version, audit and outbox record. Preserve the existing old confirmed-date invalidation; shifted Proposed dates do not acquire confirmation.

### Root defaults to settle

1. WholeProject means all currently editable Proposed/Confirmed rows, explicitly labelled; PM/Admin manager scope. Terminal records remain unchanged/visible through existing lists. Selected sets containing a terminal/foreign/missing/denied row fail rather than skip. A lead/creator selection gets only existing Edit rights, never rights over another creator's records.
2. Existing Edit does not move/revalidate a filled role's range (Create alone uses LockOpenRole). Recommended bounded default preserves that rule and discloses the unchanged associated range where relevant. No automatic role extension/reopen/move; a different rule needs an explicit root choice before implementation.
3. AC53's three activities means three **ResourceAllocation** move events with actor/reason. Preserve ordinary child-link/day audit evidence rather than suppress it to force total activity-table count=3.

### Ownership and checks

Capacity: Allocations.cs narrow helper/shift routes; optional new AllocationShift helper; projects/Allocations.tsx + small shift dialog; new `CapacityAllocationShiftTests.cs` and `capacity-allocation-shift.cjs`. Previous S30 contracts/adapter/preview remain held except exact approved integration. AllocationPreview.cs does not need changing simply to show old/new dates.

Root: any shared Coordination/Permissions/receipt/metadata extension actually needed, locales/error text, cache keys, API generation/contracts. No entity/migration/settings request presently. Lane does not edit shared transaction/permission/registry files.

Meaningful prepared fixtures after authorization:

- Three allocations2026-11-09–13, one Confirmed: pure preview+2weeks→2026-11-23–27; apply all3, translate day/work dates, preserve hours/person/purpose/provenance, make Confirmed Proposed, three allocation activities, canonical reconfirmation still required.
- Negative weeks, explicit fractional day hours, changed holiday/calendar, inactive/missing person, superseded review, terminal/duplicate/foreign IDs, zero weeks and date overflow: no clipping/skipping/extra work-date mutation.
- Real unselected work-slice collision, stale one-row version, new WholeProject row after preview, mixed denied selection and concurrent confirmation/calendar change: all-or-none durable state and canonical confirmation guards.
- Same RequestId retry creates no extra move/audit; changed payload rejected. WholeProject beyond one page still complete. Creator-only selection leaks no hidden allocation/person/load metadata.
- Keyboard selection/preview/±weeks/explicit apply/retry; old rows first and supporting preview accessible; flag off preserves original actions/response/layout/export.

## S32: Own Time week grid (FR/AC54–55)

### Existing reads and exact own calendar inputs

Current source is **Features/Time.cs** and **pages/Time.tsx**. TimeView actual entries/byDay/byTask/byProject, filters, review mode, exports and EntryDialog stay. TaskTimeEntry has **nonunique** task/date and person/date indexes: multiple legitimate entries/notes may occupy one cell.

Add a narrow capacity_screens own-week read in Time.cs or a new helper. Person is authenticated **me**, not a supplied userId. Scope=all/another-person review retains the current list/correction path and must not become another person's editable grid. Keep visible project/task filters. Seven exact Monday–Sunday dates, org-time-zone Today and one AsOf. Existing Time Range accepts arbitrary starts/up to370days; custom links retain list data unless the user explicitly navigates to a week. Do not silently normalize/drop old range results.

Reuse **PersonCapacity.Load(db,[me.Id],from,through,settings)**, the same PLN06 input Planning Days projects. It handles effective schedules, flag-controlled weekday hours, explicit availability, office/global holidays, reconciled HR leave, legacy/unverified overrides, extra availability and Provisional. PlanningEndpoints.TimeAway intentionally refuses writes with hr_reference_required; Time must not revive self-entered leave or read private planning drafts as availability.

Suggested daily contract: Date; nullable NormalHours/AvailableHours/TimeAwayHours/HolidayHours/AdditionalHours; explicit Provisional/unavailable state. Weekly CapacityHours nullable. Known0 stays0. Missing/provisional input never becomes zero away or verified available capacity. Unset personal weekly override inherits the existing known org default; OfficeId=null uses existing global-holiday rules. Derive away from Day.Leave and capacity from Available—do not subtract leave twice or subtract actual hours. For provisional input, show Capacity unavailable or a clearly labelled provisional figure if root chooses it. No leave reasons, medical details, balances, HR references/sources or other person's availability.

Hatching plus accessible generic date/hours/state is information only. Fully away/holiday/zero-capacity days remain enterable whenever existing EnterTime allows.

Rows: permission-scoped union of own **assignee** open tasks (Complete/Cancelled excluded; On Hold remains open), own nondeleted entries in the exact preceding7days, plus tasks already present in own current-week entries so actuals cannot disappear. Deduplicate by TaskId, apply access/project/task filters before metadata/counts, and do not stop at page1. Prior-week tasks can be complete/no-longer-assigned; current canonical permission determines write affordance. Deleted-task historical actuals preserve the existing read behaviour but cannot create new entries. No other-person reminders or prefill writes.

### Cell writes reuse actual-entry commands

- Blank suggestion cell is not a persisted0. Only a dirty typed value explicitly committed by Enter/blur calls existing POST `/time`. Create resolves owner=me, checks task/project and EnterTime, validates positive≤24 and at most2decimals, advisory-locks person/date and enforces total≤24 in Tx.Run. Loading/focus/prefill/navigation/export creates nothing.
- Exactly one existing own entry: PATCH **its ID** with If-Match/current version, preserve fixed task identity/note unless intentionally changed. Keep owner/PM-reason and stale reload rules. Clearing/0 is not implicit deletion; retain explicit existing delete confirmation or canonical positive-hours refusal.
- Multiple existing entries: show actual sum + entry selector opening existing edit dialogs. Never replace the first entry with the sum, merge notes, distribute a typed aggregate, or add a unique person/task/day constraint. This is the recommended bounded default root should settle.
- Prevent Enter+blur double-submit and keep a stable `typedRequest` idempotencyKey per logical create retry. Existing generic IdempotencyMiddleware handles time POST/concurrent key claims; Time EntryBody has no RequestId. Distinct legitimate intents can still create multiple canonical entries and must render honestly.
- DailyCap remains24 across all own nondeleted entries under concurrency. Away/capacity does not create a lower limit, alter estimates/progress or turn Time into expected-vs-actual scoring. Exports/reports sum only real visible entries; never candidate rows. No timer, submit, lock or approval.

### Ownership and checks

Capacity: Time.cs; optional narrow TimeWeekRead helper; Time.tsx; own grid component/local hatch styling; new `CapacityTimeWeekTests.cs` and `capacity-time-week.cjs`. Read PersonCapacity only; no Availability/Planning mutations. Keep old list/review/correction dialogs. No S29 facts/person panel.

Root: locales/errors, API contracts/generation, precise TimeEntry/own-availability cache invalidation and module wiring if needed. No schema/permission/Idempotency-model change presently. Prefer separate optional read or null-omitted DTO additions that retain old TimeTests off keys/order/nulls/export.

Meaningful prepared fixtures after authorization:

- **AC54:** Alex normal40; verified HR-referenced away8 Wednesday2026-11-11: hatched day and Capacity32 thisweek. A permitted Wednesday entry still saves; availability/approval/estimate/progress unchanged.
- Partial/full/overlapping leave; holiday+leave; reconciled override counted once; effective schedule change; extra availability; inherited default; known-zero/provisional/unavailable: no false0/32 or private HR/person leak.
- **AC55:** three assigned + two prior-week tasks including overlap, exact deduplication, all blanks persist nothing. Type2 Tuesday→one2h entry owned by Alex and no other entry; real totals/export agree. Hidden/removed task/project access does not leak via history.
- One-entry versioned edit; stale/no-longer-authorized row; multiple-cell entries with distinct notes; explicit soft delete; positive/precision/24h validation. Never overwrite an aggregate or unrelated note.
- Stable-key retry/Enter+blur creates once; concurrent daily total≤24. Fully away day allowed. ReadOnly, removed membership, archived/cancelled project, deleted task, PM correction and review scope keep existing gates.
- Month/year week navigation, previous7day window, project/task filters, custom-range URL and scope=all retain intent/data. Keyboard names/named scroll region,375px hatch/text access, primary rows early and flag-off exact legacy behaviour. Root reports synthetic UI/real API/Chrome separately.

## Dispatch

Root chooses the bounded defaults, opens one named window, retains shared ownership and runs the single frozen verification lane. Lane authors meaningful API cases, reports BACKEND READY, then finishes only assigned UI/scripts. This proposal changes no requirement checkbox, decision, source, deployment or acceptance status.
