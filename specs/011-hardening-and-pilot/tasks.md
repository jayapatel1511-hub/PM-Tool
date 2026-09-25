# Tasks: Hardening and Pilot

- [x] T001 Operator alerts: pure checks (evaluation delayed, nightly missed, job failed, digest late) with worked examples, five-minute watchdog with heartbeat, Admin → Operations (FR-006)
- [x] T002 Telemetry to Application Insights when configured; alert rules and action group in `infra/main.bicep` behind `operatorEmail`; extensions allow-list for Azure (FR-006, FR-007)
- [x] T003 Job host runs jobs independently; long jobs evaluate one project per unit of work; job records pruned after 90 days (FR-005, FR-013)
- [x] T004 Full-scale synthetic data and load measurement scripts; nightly, evaluation, list, item and page targets measured; fixes for task rows, "mine" filters, My Work attention and search indexes (FR-013)
- [x] T005 Security: CSV formula injection fixed with tests; .NET security analysers fail the build; key-reservation SQL made fixed-text; threat model prepared (FR-008..FR-012)
- [x] T006 Accessibility: jsx-a11y lint rules in CI; axe WCAG 2.1 AA audit of every screen as three roles; violations fixed; forced-colours focus; audit snippet saved (FR-002, FR-003)
- [x] T007 Pilot: snapshot counts with open tasks without a due date; `pilot-measures` report (G1–G4) with a test; pilot plan with weekly review and go/no-go record (FR-001)
- [x] T008 Admin guide, one-page user guide, interface reference, runbooks for jobs, restores (with drill record) and environments (FR-014, FR-005, FR-007)
- [x] T009 Verification against AC-VIS-01..08 (packets 022–024) and all gates; verification.md
- [x] T010 Final audit (2026-09-25): §25.10 `Retry-After` on 429 and `Idempotency-Key` replay for creates; four-role accessibility sweep of every screen including Phase 2
