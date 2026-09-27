# Implementation Plan: Dependencies and Rules Engine

**Branch**: `005-dependencies-and-rules-engine` | **Date**: 2026-09-25 | **Spec**: [spec.md](spec.md)

## Summary

Finish-to-Start task dependencies with loop refusal and a chain view, and one deterministic rules engine that
derives every indicator, milestone status, deliverable progress and risk, project and discipline health, and
attention item from a project's data, today's date and the organisation settings. Results are stored in state
tables, recomputed after every change (outbox plus worker), on read when a day has passed, and nightly; changes of
state are signalled once to notifications. The PM may override reported health; attention items may be snoozed.

## Technical Context

As packet 001. Tables `task_dependency`, `deliverable_dependency`, `task_state`, `deliverable_state`,
`milestone_state`, `decision_state`, `project_state`, `attention_item`, `attention_snooze`,
`project_health_snapshot` and `outbox_event` exist in the initial schema.

## Constitution Check

| Principle | Check | Result |
|---|---|---|
| I | Dependencies are Finish-to-Start in one project; no scheduling engine, no critical path (§30) | Pass |
| II | `Evaluator.Evaluate` is one pure function (no I/O, clock or randomness); every result carries the rule, threshold and values (`Reason`); nothing computed is stored as a status | Pass |
| III | Attention routes to named people from current roles (ATT-05); nothing is dismissible, only snoozed with a note | Pass |
| IV | Canonical statuses and rule IDs verbatim (A-01..A-20, D-01..D-18, DL-07/08, §16.2/§16.3) | Pass |
| V | Dependency add/remove, snooze, override set/clear/expiry all logged in the same transaction; expiry logged with actor System | Pass |
| VI | `Permissions.ManageDependency` (D-10), `Snooze` (PM, DL), `HealthOverride` (PM) checked server-side | Pass |
| VII | No new infrastructure: in-process worker and advisory-lock jobs in the monolith; thresholds only from `OrgSettings` | Pass |

## Design notes

- **Evaluation pipeline** (§23.5): every audited save adds one `ProjectChanged` outbox row per touched project and
  pokes an in-process signal; `EvaluationWorker` debounces (400 ms), takes an advisory lock, re-evaluates each
  project whole and marks the rows processed. Failures are retried by `EvaluationRetryJob` (15 min), which also
  re-evaluates projects whose state predates today. Reads call `EnsureFresh`, so a missed night never shows stale
  indicators. `NightlyJob` (00:15 org time) expires overrides and snoozes, evaluates Active projects and writes one
  `project_health_snapshot` per project per date (upsert).
- **Transitions only**: the service snapshots the previous blocked/waiting flags before upserting state rows and
  notifies on changes only (became blocked, unblocked, A-03 first detection, new Critical item, milestone At Risk or
  Overdue, decision overdue). A self-set manual block does not notify its setter.
- **Assumptions recorded while building** (no spec text decides them):
  - Null deliverable progress counts as 0 for "progress < 50 %" (DL-08) and "< 100" (§16.2 c).
  - An undated milestone has no status (shown "Undated"); it never goes Overdue.
  - A non-Active project gets no indicators and Grey health with the reason "Project on hold" / "Project in setup".
  - A-15 fires while the override is cleared and its expiry is within the last `health_override_expiry_days`.
  - "Next due" for a discipline excludes overdue items (they are already counted as overdue).
  - The overdue-task contribution to health needs at least `health_overdue_task_min_yellow` overdue tasks; below
    that the reason says so and names the minimum.
  - Working days (packet 021) change due-soon and stale counting only; Overdue stays "due date before today".
- **Health override** can be Green, Yellow or Red (never Grey), needs a note, expires after
  `health_override_expiry_days`; wherever both values differ the header shows Reported and Computed.

## Project Structure

```text
src/Hub.Domain/Evaluation.cs, Calendar.cs         pure rules engine, graph helpers, working-day calendar
src/Hub.Api/Features/Dependencies.cs              add/remove/list, chain, candidates (loop-creating ones disabled)
src/Hub.Api/Features/Evaluation.cs                attention list and snooze, health override, project state
src/Hub.Api/Infrastructure/EvaluationService.cs   loader, state upsert, transition notices, worker, retry and nightly jobs
tests/Hub.Tests/Domain/EvaluatorTests.cs, EvaluatorEdgeTests.cs, WorkflowTests.cs, PermissionSweepTests.cs, DomainHelpersTests.cs
tests/Hub.Tests/Api/EvaluationTests.cs
web/src/pages/projects/Health.tsx                 header health with "Why?" and override
web/src/components/hub/attention.tsx              attention list with "Why?" and snooze (mounted by packet 007)
web/src/pages/projects/TaskPanel.tsx              blockers box, dependencies, chain view (with packet 004)
```

## Checks

Every §15.12 worked example and acceptance scenario as automated tests (SC-001); the 2,000-task re-evaluation
within 2 seconds (SC-002); nightly snapshot per Active project (AC-HLT-05); Hub.Domain branch coverage ≥ 95 %.

## Complexity Tracking

None.
