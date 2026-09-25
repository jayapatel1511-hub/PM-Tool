# Tasks: Dependencies and Rules Engine

- [x] T001 Pure evaluator: G-01/G-02/G-05, D-04..D-18 with lag, manual and decision blocks, stale, unassigned, missing due, date inconsistency, inactive owner, held past due, review stalled, affected milestones, notes (FR-003..FR-013, FR-016..FR-018)
- [x] T002 Deliverable progress and At Risk, derived and explicit deliverable dependencies (FR-014, FR-026, FR-027)
- [x] T003 Milestone status §16.2, project and discipline health §16.3/§16.6 with reasons (FR-025, FR-028, FR-029)
- [x] T004 Attention rules A-01..A-06, A-08..A-20 (A-07 for packet 014) with routing, rank and snooze masking (FR-019..FR-024)
- [x] T005 Graph helpers: path and cycle-if-added; working-day calendar (FR-002, packet 021 hook)
- [x] T006 Dependency endpoints: add with project lock and cycle path, remove, list, chain, candidates (FR-001, FR-002, FR-008, FR-015)
- [x] T007 Evaluation service: load, evaluate, upsert state, keep first-detected time, transition notices (FR-006, FR-020, FR-032)
- [x] T008 Outbox worker, retry job, nightly job with override and snooze expiry and health snapshots; `EnsureFresh` on reads (FR-030, FR-031, FR-032)
- [x] T009 Attention list, snooze (1–30 days, note, logged), health override set/clear, project state endpoints (FR-022, FR-023, FR-030)
- [x] T010 UI: health "Why?" and override in the header; blockers box, dependency add/remove with disabled loop candidates, chain view; indicator chips; attention list component
- [x] T011 Domain tests: §15.12 worked examples, acceptance scenarios, edge cases, workflow tables, permission sweep; coverage gate ≥ 95 % branches
- [x] T012 API tests (dependencies, blocking through the API, snooze, override expiry, nightly snapshot, outbox worker, 2,000 tasks); verification.md
