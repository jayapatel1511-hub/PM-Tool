# Tasks: Submission Readiness and Issue Manifest

All tasks are pending. This list is the proposed build scope; this commit contains specifications only.

- [ ] T001 Add canonical records, constraints and an additive migration in `src/Hub.Api/Data/` (FR-SUB-01, FR-SUB-02, FR-SUB-03, FR-SUB-04, FR-SUB-05, FR-SUB-06, FR-SUB-07).
- [ ] T002 Implement pure transitions/derived rules in `src/Hub.Domain/Submissions.cs` and permission cases in `Permissions.cs` (FR-MDC-01, FR-MDC-02, FR-MDC-03, FR-MDC-04, FR-MDC-05, FR-MDC-06, FR-MDC-07, FR-MDC-08).
- [ ] T003 Add transactional, version-checked project commands in `src/Hub.Api/Features/Submissions.cs` and register routes; include audit and retry-safe outbox events (FR-SUB-01, FR-SUB-02, FR-SUB-03, FR-SUB-04, FR-SUB-05, FR-SUB-06, FR-SUB-07).
- [ ] T004 Build the scoped UI, source links and accessible forms at the planned paths; externalise labels (FR-SUB-01, FR-SUB-02, FR-SUB-03, FR-SUB-04, FR-SUB-05, FR-SUB-06, FR-SUB-07).
- [ ] T005 Connect permitted search, filters, reports/exports and deduplicated notifications (FR-MDC-01, FR-MDC-02, FR-MDC-03, FR-MDC-04, FR-MDC-05, FR-MDC-06, FR-MDC-07, FR-MDC-08).
- [ ] T006 Implement and run AC-SUB-01, AC-SUB-02, AC-SUB-03, AC-SUB-04, AC-SUB-05, concurrency/access cases and complete UI scenarios; record actual evidence in `verification.md`.
