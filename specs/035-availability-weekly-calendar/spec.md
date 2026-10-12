# 035 — Availability, weekly allocation and personal calendar

Approved 2026-10-09. Tuesday plans leave approved by HR. This packet extends the existing packet 029 staffing reservations and packet 034 weekly planning layer. It does not introduce approvals, leave balances, payroll, inbound meetings or two-way calendar writes.

## Contract

Product specification §8.5, §9.4, §10.2, §10.9, §12.15, §12.18, §12.19, §13.11, §13.21, §14.10, §15.12, §21, §26.1, §31.11 and §31.12. FR-RES-01..24, RES-01–20, AC-RES-01, AC-RES-02, AC-RES-03, AC-RES-04, AC-RES-05, AC-RES-06, AC-RES-07, AC-RES-08, AC-RES-09, AC-RES-10, AC-RES-11, AC-RES-12, AC-RES-13, AC-RES-14, AC-RES-15, AC-RES-16, AC-RES-17, AC-RES-18, AC-RES-19, AC-RES-20, AC-CAL-01, AC-CAL-02. G-06, G-07, FR-PLN-01, FR-PLN-02, FR-PLN-03, FR-PLN-04, FR-PLN-05, FR-PLN-06, FR-PLN-07, FR-PLN-08, FR-PLN-09, FR-PLN-10, FR-PLN-11, FR-PLN-12, FR-PLN-13, FR-PLN-14.

- Admin maps stable source/person identities and previews a normalized CSV. Only a wholly valid preview gets an authenticated, expiring hash receipt. Apply revalidates every field, identity and row version under locks and a transaction. It stores neither the CSV nor its normalized preview contents. Missing rows never cancel leave; explicit newer cancellations do.
- Supervisors may record a manual copy of HR approval for direct reports, with a stable HR reference. Imported records remain read only. Legacy availability remains preserved and provisional until explicitly reconciled with a referenced HR record.
- Effective schedules divide weekly contracted hours equally over selected working days. Holidays and overlapping absence reduce daily availability once. The same calculation drives weekly planning, workload and reservation confirmation.
- Vacation & Coverage gives year, day and list views. Ordinary employees see own-team availability without HR identifiers; work details remain project scoped. Coverage concerns flag dated owner, assignee and reviewer conflicts separately from workload. Undated work remains unknown.
- People and Projects views use existing weekly PlanningEntry records. Copy operates on the creator's manager rows, previews continuation/duplicates and creates target-week Drafts. Publish preserves confidence and sends one summary per affected employee. Whole selections reject stale snapshots and replay atomically.
- Personal ICS links are revocable capabilities, stored only as hashes and shown once. Feeds recheck active accounts and project access, use stable UIDs and sequences, and emit sanitized cancellation tombstones. Dated personal work and approved leave are all-day events; weekly hours have no invented appointment time. Client refresh and Outlook compatibility require real-client acceptance.

## Acceptance

Exercise whole-batch rejection, unmapped and stale source data, repeat apply, explicit cancellations, overlap/holiday arithmetic, effective schedules, legacy reconciliation, private project boundaries, selected copy/publish and notification batching, token rotation/revocation, stable event identity, UTF-8 folding and privacy-safe cancellation. Verify web build, lint, API/domain checks, browser workflows, accessibility, migration and spec traceability.

Planning decisions Q22, Q23, Q24, Q25 and Q26 govern visibility, horizon, HR export mapping, working schedules and calendar-client acceptance.
