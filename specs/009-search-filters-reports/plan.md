# Implementation Plan: Search, Filters, and Reports

**Branch**: `009-search-filters-reports` | **Date**: 2026-09-25 | **Spec**: [spec.md](spec.md)

## Summary

One search box with key lookup, a results page with type tabs, exports of every list with the list's own filters,
the first-release reports of §19 with "Open as filtered list", and the readable Activity History with an actor
filter. Reports and exports reuse the list queries (`TaskQueries.Apply`, `DeliverableEndpoints.Filter`, the decision
register, the milestone and project lists), so a report's rows are the rows its list shows (SC-004).

## Technical Context

As packet 001. No new tables and no search service (§18.2): search is `ILIKE` over keys, numbers, names and
subjects with a rank expression in SQL; trigram indexes are left to packet 011 if latency needs them. Exports use
the existing CSV/XLSX writer (`Export.cs`). Endpoints: `GET /search`, `GET /reports`, `GET /reports/{code}`,
`GET /reports/{code}/export`, and `GET …/export?format=csv|xlsx` beside the task, deliverable, decision, milestone,
project and activity lists.

## Constitution Check

| Principle | Check | Result |
|---|---|---|
| I | Fixed reports only: no report builder, pivots, scheduled emails or charts (§19 "Not built") | Pass |
| II | Report rows come from the same queries as the lists they open; tested by comparing a report total with its list | Pass |
| III | Reports name owners, assignees and blockers | Pass |
| IV | Canonical statuses and keys; key lookup follows the §9.5 key format | Pass |
| V | Every export is logged (who, which report or list, parameters) without its contents (§19, §20.1); the log stays read-only | Pass |
| VI | Search and reports cover only projects the caller may view; Restricted items never appear; Staff Assignments needs a Supervisor (direct reports) or an Executive/Admin (all staff) | Pass |
| VII | No new infrastructure | Pass |

## Design notes

- Ranking: exact key or number, then key/number prefix, then name prefix, then the rest; Active projects first;
  most recent activity last. Archived and Cancelled projects only with "include archived".
- Enter in the search box opens a highlighted result, else an exact key or project number, else the results page.
- People results open that person's My Work only where the caller may see it (self, direct reports, Executive/Admin,
  members of projects the caller manages); others are listed without a link.
- A report scoped to exactly one project offers "Open as filtered list" when its filters map to the list.
- Exports refuse more than 50,000 rows with "export one project at a time"; times are in the organisation time zone;
  file names carry the date.
- Register tables (deliverables, decisions) gain header sorting (URL `sort=`) with due date and key as tie-breaks,
  and a per-viewer column chooser; the task list already had both.

## Project Structure

```text
src/Hub.Api/Features/Search.cs, Reports.cs (+ ExportFile), ListExports.cs
src/Hub.Api/Features/Projects.cs, Deliverables.cs, Milestones.cs, Decisions.cs  (list parameters as records for reuse)
tests/Hub.Tests/Api/SearchReportsTests.cs
web/src/components/hub/search.tsx, export.tsx, table.tsx
web/src/pages/Search.tsx, Reports.tsx (+ export menus on each list, actor filter on Activity)
```

## Checks

AC-SRCH-01..03, FR-007, FR-010, FR-RPT-01, FR-ASG-08, SC-004, AC-AUD-01/02 as API tests; search box, results page,
reports, sorting and column choice in the browser.

## Complexity Tracking

None.
