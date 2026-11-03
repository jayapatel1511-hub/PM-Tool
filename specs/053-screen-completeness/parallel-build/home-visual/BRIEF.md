# Home and visual system

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

Assigned slices: 052 S02/S03/S06/S07/S08/S09 and 053 S07/S08/S09.

Owned maintained files: src/Hub.Api/Features/Workspace.cs; web/src/pages/Home.tsx; web/src/app/Shell.tsx, nav.ts, slots.tsx; web/src/pages/projects/ProjectLayout.tsx; web/src/index.css and visual-layout.css; web/src/lib/visual-system.ts; web/src/components/hub/workspace.tsx; count-card.*, title-block.*, semantic-colours.*, item-mark.tsx, identity-colour.tsx and visual-tabs.css. New home/visual-specific helpers and tests with a unique home-visual prefix are yours. Board/WorkspaceTasks changes required for lane colors belong to work-delivery: request a coordinated patch.

Next useful work: Start by auditing the remaining 052 visual slices against their actual existing implementation and census gaps; close the smallest incomplete acceptance batch, then implement 053 Home layout/charts, scope-correct trends, capacity/deadline notes. Keep existing exact counts and server asOf behavior. Snapshot/entity additions require a shared schema request. Do not perform 052 S10 cleanup: it is conditional on a later pilot activation, which is not authorized.

Write status/evidence to /Users/jaypatel/.codex/worktrees/ef94/PM-Tool/.build-052-053/specs/053-screen-completeness/parallel-build/home-visual/status.md and shared requests to /Users/jaypatel/.codex/worktrees/ef94/PM-Tool/.build-052-053/specs/053-screen-completeness/parallel-build/home-visual/requests/. Start with current-source inspection and a concrete batch; do the implementation, not merely a plan.

