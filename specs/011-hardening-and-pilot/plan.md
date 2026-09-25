# Implementation Plan: Hardening and Pilot

**Branch**: `011-hardening-and-pilot` | **Date**: 2026-09-25 | **Spec**: [spec.md](spec.md)

## Summary

Make the first-release build ready for the pilot and for organisation-wide use without adding product behaviour:
operator alerting and an Operations screen, telemetry, performance at full synthetic scale (and the fixes that took),
security findings resolved, accessibility audited and enforced in every change, and the written material — admin and
user guides, interface reference, runbooks, threat model and pilot plan — plus a pilot-measures report. Running the
pilot, restore drills against Azure, the threat-model walkthrough and any penetration test are organisational steps this
packet prepares but cannot perform.

## Technical Context

As packet 001. New: `OpsChecks`/`OpsWatchdogJob` (every 5 minutes; `OpsAlert` errors and an `OpsHeartbeat`), `GET
/admin/operations`, the `pilot-measures` report, `project_health_snapshot.counts` (migration `SnapshotCounts`), trigram
indexes on task, deliverable and milestone names and keys (migration `SearchTrigramIndexes`), Azure Monitor OpenTelemetry
export when `APPLICATIONINSIGHTS_CONNECTION_STRING` is set, alert rules in `infra/main.bicep` behind `operatorEmail`,
`azure.extensions` allow-list for `citext` and `pg_trgm`, .NET security analysers as build errors
(`Directory.Build.props`), oxlint `jsx-a11y` rules in CI, `tools/scale/seed.sql` and `tools/scale/measure.py`,
`tools/a11y/audit.js`. Dependencies added: `Azure.Monitor.OpenTelemetry.AspNetCore` (telemetry), `axe-core` (dev only).

## Constitution Check

| Principle | Check | Result |
|---|---|---|
| I | No AI; the pilot measures are counts from snapshots and the log | Pass |
| II | Performance fixes keep every list's rows and totals identical (full suite unchanged) | Pass |
| III | Reassigning a leaver's work keeps one accountable owner per item (packet 010, documented) | Pass |
| IV | No new statuses or product behaviour | Pass |
| V | Settings changes are logged; the log stays append-only (triggers from packet 001); job records are operational and pruned after 90 days | Pass |
| VI | Operations endpoint is Admin only; pilot report requires portfolio access and visible projects; CSV formula injection closed | Pass |
| VII | Alert rules and the action group deploy only when `operatorEmail` is set (Jay's decision); the extensions setting fixes an existing resource; no new services | Pass, pending approval to deploy alerts |

## Design notes

- The watchdog's conditions are one pure function with worked examples; the job host that runs it now runs jobs
  independently, so a long nightly run can't delay email, digests or the heartbeat.
- Long jobs evaluate each project in its own unit of work (the shared context made the nightly run quadratic).
- Task rows load related data by id instead of per-row `First()` projections (EF ranked whole tables for each list).
- "Mine" filters split into index-friendly halves; My Work's attention list returns the 100 most urgent with the total.
- Accessibility: board cards keep pointer dragging but move keyboard dragging to a named handle (no nested controls, and
  Enter opens the task); unnamed header cells, a role-less labelled cell, an image role over buttons, a text box with
  `aria-expanded`, text-only scroll regions and one low-contrast text were fixed; forced-colours mode keeps a focus outline.

## Complexity Tracking

| Deviation | Why | Simpler alternative rejected because |
|---|---|---|
| Two new dependencies | Telemetry export needs the Azure Monitor distro; a WCAG audit needs an engine | Hand-written exporters or checks would be larger and less trustworthy |
