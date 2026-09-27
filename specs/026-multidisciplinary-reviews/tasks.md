# Tasks: Multidisciplinary Reviews and Comment Closure

The foundation implementation is written. Automated validation is in progress in draft PR #3. Full acceptance, including real browser-to-API and operational checks, remains pending; see `verification.md`.

- [x] T001 Add canonical records, constraints and an additive migration in `src/Hub.Api/Data/` (FR-MRV-01, FR-MRV-02, FR-MRV-03, FR-MRV-04, FR-MRV-05, FR-MRV-06, FR-MRV-07).
- [x] T002 Implement pure transitions/derived rules in `src/Hub.Domain/Reviews.cs` and permission cases in `Permissions.cs` (FR-MDC-01, FR-MDC-02, FR-MDC-03, FR-MDC-04, FR-MDC-05, FR-MDC-06, FR-MDC-07, FR-MDC-08).
- [x] T003 Add transactional, version-checked project commands in `src/Hub.Api/Features/Reviews.cs` and register routes; include audit and retry-safe outbox events (FR-MRV-01, FR-MRV-02, FR-MRV-03, FR-MRV-04, FR-MRV-05, FR-MRV-06, FR-MRV-07).
- [x] T004 Build the scoped UI, source links and accessible forms at the planned paths; externalise labels (FR-MRV-01, FR-MRV-02, FR-MRV-03, FR-MRV-04, FR-MRV-05, FR-MRV-06, FR-MRV-07).
- [x] T005 Connect permitted search, filters, reports/exports and deduplicated notifications (FR-MDC-01, FR-MDC-02, FR-MDC-03, FR-MDC-04, FR-MDC-05, FR-MDC-06, FR-MDC-07, FR-MDC-08).
- [ ] T006 Implement and run AC-MRV-01, AC-MRV-02, AC-MRV-03, AC-MRV-04, AC-MRV-05, concurrency/access cases and complete UI scenarios; record actual evidence in `verification.md`.
