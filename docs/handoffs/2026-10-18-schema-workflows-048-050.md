# Schema/workflows local handoff: packets 048–050

Baseline: a6e0104b0b00ea58b9098e41b2f155305063aeed. Branch: codex/schema-workflows. Worktree: /Users/jaypatel/.codex/worktrees/schema-workflows/PM-Tool.

Core source/model/migration/evidence commit: 4f92c6ea6376b777d51ce7218bd3739f9132bff6. This companion completes pickup's own load hint, offer context/form inputs, and the Activity Log report integrity line. Companion commit: 58352bcb1a35cbf58ed0cdb29cb54292a7e9ee06. Exact core/companion/follow-up revisions are recorded after commit in the durable artifact /Users/jaypatel/.codex/visualizations/2026/10/18/01a1513f-a233-7c62-ba81-d29d7217bec8/schema-workflows-final.json. The temporary coordination file is only a convenience copy.

## Integration order and shared files

Adopt the reviewed recovery model ea202b192c93c49516f6f56300eb9bc8a42426db and capacity model as reconciled by root 8c1ae565c0acc63e8c45833f3b2f2facd671db38. This lane already contains those canonical model inputs. Preserve the original schedule CHECK, one template OffsetDayKind, and no unsupported per-role daykind fields. Reconcile shared files additively; do not replace complete peer files with this lane's copies.

The sole new combined migration is 20261018232730_DeliveryWorkflowRecords + Designer + snapshot. Original recovered 20261018222421_TemplateOffsetDayKind files are byte-identical to the handoff. The commitment-state CHECK and history guard support AwaitingConfirmation; content and terminal outcomes remain immutable. Migration/Tier B release gates are separate from source readiness.

Shared reconciliation paths: HubDb, Entities, AvailabilityEntities, PlanningReportingEntities, ReadinessEntities, Audit rules, Domain Settings/Flags/Vocabulary/Readiness, API Me/Tasks/TaskQueries/Deliverables/Reports/Planning/WeeklyPlanning/Workload/Availability/Projects/Search/Allocations/WeeklyCommitments/Modules/Program/Text; web navigation/routes/types/i18n and affected workspace/project pages; package scripts and ci_steps. Keep parent 044 module split and 047 weekday-vector/runtime changes when merging.

## Required owning-lane seams

046 reviewed command 7553f954e82c98a36d998acbfde1c4edd7f88557 already owns Tx.Run/project lock/version/reason checks. Inject TimeProvider and add after baseline date assignments, before its final SaveChangesAsync:

`await BaselineHistory.Capture(db, d, body.Clear == true, Check.Reason(body.Reason), access.Me.Id, clock.GetUtcNow());`

Do not create another route or transaction. Contract tests exercise B1/B2, clear, SQL/application immutability and delta export, but only combined integration proves this API caller.

Team Capacity must use OrgStructureReports.At(db, ids, exclusiveWeekEnd) and UnitPath, as Staff Assignments already does. Root owns that report integration/regression. Historical unit identity comes from old/new activity, never current placement for a past week.

Audit switch-on is the explicit migration-only privileged initializer (Db:MigrateOnly=true, Db:InitializeAuditChains=true), described in the packet guide. It runs before HTTP/jobs, validates the migration identity and records/seals inside a transaction. Do not grant runtime UPDATE/DELETE/TRUNCATE/function/ownership rights. The ordinary writer refuses uninitialized legacy evidence.

Jay resolved the 048 preview scope: both global indicators remain deferred; use existing capacity warnings. Root owns narrow source/packet amendment 1867801. No unresolved preview scope decision remains.

## Evidence and boundaries

Core: 57/57 focused domain/API/flag-metadata tests. Companion: 21/21 focused API checks, including one added own-load case. Actual mocked Chromium: 8 bounded workflows, 104 API calls, four My Work view/flag pairs, zero axe/page errors/unknown GETs; needed-by context and refresh asserted in the final companion. App/node TypeScript no-incremental checks and Vite build pass. EF model drift: none. Fresh test databases migrate successfully; combined-spec freshness/trace and diff checks pass. Independent source review cleared the final changes. Durable test records are under specs/048-verifiable-records/evidence; per-AC boundaries are in each packet verification.md.

Not claimed here: combined full Release/regression/coverage, populated upgrade/Down/previous-image safety, p95 latency, live authenticated browser/native/iOS/hosted acceptance, comprehensive phone/keyboard/screen-reader review, Tier B approval or Jay Accepted. Maintained flags remain false. No operational DB migration, push, PR, deployment or pilot activation occurred. Original checkouts were not changed by this lane.

Recovery-time boundary rerun: two strengthened assertions compile but are BLOCKED before assertions because Docker/PostgreSQL localhost:55432 are unavailable. Exact runner outcomes/reason are durable in specs/048-verifiable-records/evidence/boundary-rerun.json. Earlier 57 core and 21 companion checks remain separate PASS evidence; the previous companion Chromium run included 72 total API calls, needed-by context and load refresh. Restore only the disposable test service and run the two named boundary cases as part of combined integration. Do not treat environment refusal as passing assertions.

## Additive My Work regression follow-up

Apply the follow-up after core and companion. Overview and Upcoming Tasks now guard their pickup page action with task_pickup. The original me/work browser fixture was invalid and could pass its missing-link assertion on an error fallback; the replacement test proves both rendered views with the flag off/on, scopes the action to main rather than global navigation, and keeps the view switcher, saved Views and New task controls visible in all four pairs. CHROME_EXECUTABLE_PATH is supported. No backend or schema change is included.

The final mocked Chromium evidence is copied to specs/048-verifiable-records/evidence/mocked-chromium.json. TypeScript app/node no-incremental checks, Vite build, actual Chromium and independent source/test review pass. Exact follow-up revision is recorded after commit in the durable final manifest. Integration already has an equivalent source guard; reconcile this small source diff additively and retain the corrected fixtures/assertions. Database boundary and combined release/human gates above remain separate.
