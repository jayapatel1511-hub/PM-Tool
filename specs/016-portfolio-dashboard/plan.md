# Implementation Plan: Portfolio Dashboard

**Branch**: `016-portfolio-dashboard` | **Date**: 2026-09-25 | **Spec**: [spec.md](spec.md)

## Summary

The Portfolio Dashboard: summary tiles, one row per project with computed and reported health side by side (with
the PM's note and its age), next milestone and submission, overdue, blocked, decision, issue and attention counts,
and an 8-week health trend from the nightly snapshots; plus the Projects At Risk and Health History reports.

## Technical Context

As packet 001. No new tables: rows are the project list's own query (`ProjectEndpoints.Sorted`, now also returning
who set an override) and trends read `project_health_snapshot`. Endpoints: `GET /portfolio`, `GET /portfolio/export`;
two reports join the packet 009 catalogue.

## Constitution Check

| Principle | Check | Result |
|---|---|---|
| I | Tables, tiles and an 8-square trend only; no cross-office comparisons or throughput metrics (Phase 3) | Pass |
| II | Every row and tile comes from the same query and derived state as the project list; the trend reads stored snapshots, not recomputed history | Pass |
| III | Each row names the PM; override notes name who set them | Pass |
| IV | Canonical health values; colour tiles count Active projects only (Setup and On Hold are Grey and listed with their status) | Pass |
| V | Exports are logged like every other export | Pass |
| VI | Portfolio needs Admin, Executive, Supervisor or PM (§8.5.1); PMs see their own projects unless they ask for all; Restricted projects only for those who may see them | Pass |
| VII | No new infrastructure | Pass |

## Design notes

- Tiles count computed health (the value that is never hidden); rows show reported health beside it when they
  differ, with the note, its author and its age (§16.4).
- Trend: eight weekly points ending today, each the last snapshot of that week; weeks without a snapshot show a
  dashed square.
- Default order: reported health severity, then next submission date (§13.12).
- Clicking a tile filters the table (Active, Red, Yellow, Green, submissions within 14 days).

## Project Structure

```text
src/Hub.Api/Features/Portfolio.cs (+ Reports.cs: projects-at-risk, health-history; Projects.cs: override author)
src/Hub.Api/Features/Team.cs (fix: members and follows added earlier in the same save are found)
tests/Hub.Tests/Api/PortfolioTests.cs
web/src/pages/Portfolio.tsx
```

## Checks

§13.12 tiles and sorting, §16.4 both values, FR-HLT-03 trend, FR-003 scope and §19 reports as API tests; the page in
the browser.

## Complexity Tracking

None.
