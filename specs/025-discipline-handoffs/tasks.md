# Tasks: Discipline Handoffs and Acceptance

T001–T005 are implemented for the initial handoff scope, with passing automated API and mocked-API browser checks. T006 remains open for full packet acceptance, including packet 027 integration and end-to-end/manual UI checks; see verification.md for actual evidence.

- [x] T001 Add canonical records, constraints and an additive migration in `src/Hub.Api/Data/` (FR-HND-01, FR-HND-02, FR-HND-03, FR-HND-04, FR-HND-05, FR-HND-06, FR-HND-07).
- [x] T002 Implement pure transitions/derived rules in `src/Hub.Domain/Handoffs.cs` and permission cases in `Permissions.cs` (FR-MDC-01, FR-MDC-02, FR-MDC-03, FR-MDC-04, FR-MDC-05, FR-MDC-06, FR-MDC-07, FR-MDC-08).
- [x] T003 Add transactional, version-checked project commands in `src/Hub.Api/Features/Handoffs.cs` and register routes; include audit and retry-safe outbox events (FR-HND-01, FR-HND-02, FR-HND-03, FR-HND-04, FR-HND-05, FR-HND-06, FR-HND-07).
- [x] T004 Build the scoped UI, source links and accessible forms at the planned paths; externalise labels (FR-HND-01, FR-HND-02, FR-HND-03, FR-HND-04, FR-HND-05, FR-HND-06, FR-HND-07).
- [x] T005 Connect permitted search, filters, reports/exports and deduplicated notifications (FR-MDC-01, FR-MDC-02, FR-MDC-03, FR-MDC-04, FR-MDC-05, FR-MDC-06, FR-MDC-07, FR-MDC-08).
- [ ] T006 Implement and run AC-HND-01, AC-HND-02, AC-HND-03, AC-HND-04, AC-HND-05, concurrency/access cases and complete UI scenarios; record actual evidence in `verification.md`.
