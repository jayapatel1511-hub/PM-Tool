# Tasks: Dated Capacity and Project Allocations

All tasks are pending. This list is the proposed build scope; this commit contains specifications only.

- [ ] T001 Add canonical records, constraints and an additive migration in `src/Hub.Api/Data/` (FR-CAP-01, FR-CAP-02, FR-CAP-03, FR-CAP-04, FR-CAP-05, FR-CAP-06, FR-CAP-07).
- [ ] T002 Implement pure transitions/derived rules in `src/Hub.Domain/Allocations.cs` and permission cases in `Permissions.cs` (FR-MDC-01, FR-MDC-02, FR-MDC-03, FR-MDC-04, FR-MDC-05, FR-MDC-06, FR-MDC-07, FR-MDC-08).
- [ ] T003 Add transactional, version-checked project commands in `src/Hub.Api/Features/Allocations.cs` and register routes; include audit and retry-safe outbox events (FR-CAP-01, FR-CAP-02, FR-CAP-03, FR-CAP-04, FR-CAP-05, FR-CAP-06, FR-CAP-07).
- [ ] T004 Build the scoped UI, source links and accessible forms at the planned paths; externalise labels (FR-CAP-01, FR-CAP-02, FR-CAP-03, FR-CAP-04, FR-CAP-05, FR-CAP-06, FR-CAP-07).
- [ ] T005 Connect permitted search, filters, reports/exports and deduplicated notifications (FR-MDC-01, FR-MDC-02, FR-MDC-03, FR-MDC-04, FR-MDC-05, FR-MDC-06, FR-MDC-07, FR-MDC-08).
- [ ] T006 Implement and run AC-CAP-01, AC-CAP-02, AC-CAP-03, AC-CAP-04, AC-CAP-05, concurrency/access cases and complete UI scenarios; record actual evidence in `verification.md`.
