# Capacity, people and utilities

The user asked to split the remaining PM-Tool build into different chats to fast-track it. This lane is part of the existing full packets 052/053 goal; the coordinator retains the whole objective.

## Authoritative checkout

Use only /Users/jaypatel/.codex/worktrees/ef94/PM-Tool/.build-052-053 for implementation. The app may start this chat in the outer directory: explicitly set this exact workdir on every command. Verify branch codex/build-052-053 and inspect status/diff before editing. HEAD is ac2fa89429653144b3258e80f102057ed8c918f9 plus extensive intentional uncommitted work. Preserve it. Do not edit /Users/jaypatel/PM-Tool or the outer checkout. No reset, stash, clean, branch switch, commit, push, deployment or pilot activation.

Read the completed spec-parts/31-visual-system-and-navigation.md, spec-parts/32-screen-completeness.md, the owning packet's spec/plan/tasks/verification, and specs/052-visual-system-and-navigation/build-progress.md. Twelve verified checkpoints already exist. Inspect actual implementation; do not replace it with an older prototype or redo finished work. The preferred prototype is read-only at /Users/jaypatel/PM-Tool/.review/2026-10-31-unslop-home/proto. The numbered 8851 copy is preserved.

## Coordination protocol

Read CONTROL.json and README.md in this directory before every source mutation. Each lane has exclusive maintained-file ownership. Do not edit another lane's files or shared integration files. Write concrete, reviewable patch requests in your own requests/ directory for shared files. Root applies them.

Do not run dotnet build/test, production Vite builds, API/registry generation, migrations, or shared browser tests. The coordinator runs one build lane: no C# mutation during .NET compilation, and no frontend mutation during production frontend builds. Static inspection, git diff --check and node --check for your own scripts are fine. Add meaningful tests and describe exact commands; do not claim them executed.

CONTROL.json mode editing permits owned-file changes. Mode freeze-csharp means acknowledge in your status.md and stop all C# mutations. Mode freeze-all means acknowledge and stop all source mutations. Write status FROZEN only after active edits finish; continue read-only analysis if useful. Root will resume integration after acknowledgements. Root alone changes CONTROL.json.

Work in small reviewable batches of 1–3 slices. When ready, write status READY in your own status.md with changed files, requirement/AC IDs, concrete acceptance checks, shared requests and known limits, then stop mutations until root resumes your lane. Do not spawn additional writers or create another goal; this chat owns its assigned lane, while the parent owns the persistent full goal.

Flags remain disabled by default; enabled behavior and unchanged-off behavior are required. Unknown/partial data must stay distinct from zero, false, available or complete. Preserve existing permission checks, stored provenance, concurrency and canonical workflow rules. Never invent rates, budgets or client facts. Open decisions remain open; the user authorized local dark implementation using recommended defaults. Vacation users without Workload keep Resources hidden and highlight Time.

## Root-owned integration files

Do not directly edit Data/Entities.cs, Data/HubDb.cs, migrations/model snapshots, Hub.Domain/Flags.cs, Settings.cs, Permissions/Workflow, RecordContextRules/Reader and shared adapters, Records metadata, Views.cs, Activity.cs, generated files, package manifests, shared auth/routes/registrations, i18n, keys.ts, common.tsx, panel-host.tsx, record-context.tsx or record-context-navigation.ts. Request exact changes with rationale and acceptance checks. No silent alternate implementation or duplicate canonical rule.

The coordinator owns spec/task/decision/combined/traceability updates. Put your evidence in your lane status/handoff, not those shared ledgers. Never mark a requirement or packet done from a narrow test or source inspection alone.

## Your lane

Assigned slices: 053 S26/S27/S28/S29/S30/S31/S32/S42/S43/S44/S45/S46/S47/S49.

Owned maintained files: Features Planning.cs, Allocations.cs, Workload.cs, Time.cs, Team.cs, Admin.cs, DocumentLinks.cs, Notifications.cs, Search.cs and Preferences-specific feature files. UI Planner.tsx, Workload.tsx, Time.tsx, Staff.tsx, Vacation.tsx, WorkspaceTeam.tsx, Files.tsx, Notifications.tsx, Search.tsx, Preferences.tsx; projects Allocations.tsx and Team.tsx; admin pages except Templates. components/hub/people.tsx and new narrow person-panel components are yours. New capacity/people/utility helpers and uniquely prefixed tests are yours. Root owns shared routes/registration, vocabulary/settings/entity metadata and API generation.

Next useful work: Start with S26 verified Find room and S28 Workload discipline context as a bounded batch, then planner/allocation/time and narrow person facts/panel, followed by utilities. Availability and permission scope must be proven; unknown or partial capacity is never free capacity. The spec allows a narrow read-only person panel, not a full profile or individual performance comparison. Keep leave reasons private, use real optional facts only, and preserve exact hours/FTE/unit math. Schema and registry additions need root's shared patch request. Notification tests must not send real email or contact providers.

Write status/evidence to /Users/jaypatel/.codex/worktrees/ef94/PM-Tool/.build-052-053/specs/053-screen-completeness/parallel-build/capacity-people/status.md and shared requests to /Users/jaypatel/.codex/worktrees/ef94/PM-Tool/.build-052-053/specs/053-screen-completeness/parallel-build/capacity-people/requests/. Start with current-source inspection and a concrete batch; do the implementation, not merely a plan.

