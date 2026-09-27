# Implementation Plan: Risk and Issue Registers

**Branch**: `014-risk-and-issue-registers` | **Date**: 2026-09-25 | **Spec**: [spec.md](spec.md)

## Summary

A risk register scored on a 3 × 3 grid with plain-language anchors, an issue register with severity and target dates, a
realised risk that becomes a linked issue, "raise from here" on task and deliverable panels, the dashboard counts, Weekly
Coordination section 10, and the Open Issues / High Risks report.

## Technical Context

As packet 001. The initial schema already holds `risk` and `issue` (with `realised_issue_id`, `origin_risk_id`), the
`R`/`I` key sequences, their status vocabularies and workflow steps, and the rules engine already reads them: A-07
(Critical, to owner and PMs), the High-issue input to Red health, and the `issuesOpen`/`issuesHigh`/`risksHigh`
snapshot counts behind the dashboard; Weekly Coordination already lists open issues and High risks. No migration.

New: `Hub.Domain/Registers.cs` (score, band, review-overdue and issue-overdue days, worked examples);
`Features/Registers.cs` (lists with filters and default sorts, raise, detail, edit, transitions, links, exports);
the `open-issues-high-risks` report; decision link helpers generalised (`NewLink`, `LinkRows`, item-link removal for
decisions, risks and issues); activity names for risks and issues. Web: `pages/projects/Registers.tsx` (Risks and
Issues tabs, panels, raise, realise and resolve dialogs, the "Raise" menu), a `Raise` item slot rendered on the task and
deliverable panels, dashboard counts that link to the registers, clickable section 10 rows.

Endpoints: `GET/POST /projects/{id}/risks`, `GET /risks/{id-or-key}`, `PATCH /risks/{id}`, `POST /risks/{id}/transition`,
`POST /risks/{id}/links`, `GET /projects/{id}/risks/export`, and the same six for issues; `GET /reports/open-issues-high-risks`.

## Constitution Check

| Principle | Check | Result |
|---|---|---|
| I | Arithmetic and date comparison only; no scoring beyond P × I (§12.10: no monetary value, Monte Carlo or appetite matrices) | Pass |
| II | Bands, overdue flags and the report come from the same `Registers` functions as the rules engine's counts | Pass |
| III | Every risk and issue has exactly one owner, defaulting to whoever raises it; owners join the team | Pass |
| IV | Statuses follow the existing `RiskStep` and `IssueStep`; Realised is final and needs its issue (RSK-03); Resolved needs its resolution (ISS-02) | Pass |
| V | Creation, edits, transitions ("Realised", "Resolved", "Reopened"), links and exports are logged in the same save | Pass |
| VI | Raising needs `RaiseRegisterItem` (PM, leads, team members); editing and moving need `EditRegisterItem` (PM, owner, raiser, the discipline's lead) | Pass |
| VII | No new infrastructure | Pass |

## Design notes

- Severity is stored as probability and impact and derived on read: Low 1–2, Medium 3–4, High 6–9. The band filter
  turns bands into the grid scores they contain, so filtering stays in the database.
- Realising either creates the issue from the dialog (title, severity from the band and owner carried over) or links an
  existing issue that has no other origin risk; the risk records the issue and the issue its origin risk.
- Cancelling an issue and reopening a resolved one need a reason; reopening clears the resolved date and keeps the
  resolution in the history.
- The report lists open issues (all severities, or the chosen one) and open risks (High, or the chosen band), most
  severe first, with the target or review date and days overdue; each row opens its own panel.
- "Raise from here" pre-links the new item as related and pre-selects the item's discipline.

## Complexity Tracking

None.
