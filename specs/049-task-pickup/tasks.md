# Tasks: Task Pickup

Each slice is one PR, merges dark behind `flag.task_pickup` and adds the flag pair test (off: identical to before; on: present), covering AC-PKP-08 for its area. Every slice needs packet 039's flags slice (S09) first; Jay approved 049-D1 on 2026-10-18.

- [ ] S01 Offer, withdraw, auto-close (S, Tier B: migration).
  - `TaskPickupOffer` table; `POST /tasks/{id}/pickup-offer`, `DELETE /tasks/{id}/pickup-offer`; auto-close in the task save path.
  - Covers FR-PKP-01, FR-PKP-02, FR-PKP-03, FR-PKP-11, FR-PKP-13; AC-PKP-01, AC-PKP-05, AC-PKP-08.
  - Tests in `tests/Hub.Tests/Api/TaskPickupTests.cs`: assignee and on-behalf offers; second offer refused; task fields unchanged; complete, cancel, delete and direct reassign each close the offer with the right cause; non-permitted offerer gets 403.
- [ ] S02 Request, approve, decline (M). After S01.
  - `TaskPickupRequest` table; request, approve and decline endpoints; approval through the existing reassignment command; `task_pickup_self_approve` setting; watcher on pickup; notifications of FR-PKP-10.
  - Covers FR-PKP-06, FR-PKP-07, FR-PKP-08, FR-PKP-09, FR-PKP-10; AC-PKP-02, AC-PKP-03, AC-PKP-04, AC-PKP-07.
  - Tests in `TaskPickupTests.cs`: two requests, one approved, other declined; 403 for non-approvers; rowVersion conflict refuses; self-approve inside and outside the discipline; notification recipients exclude the actor.
- [ ] S03 Open for pickup list and task UI (M). After S02.
  - Tag and actions in the task panel and rows; Open for pickup list from My Work and the project Work group; load band beside Request pickup.
  - Covers FR-PKP-04, FR-PKP-05; AC-PKP-06.
  - Tests: `web/tests/task-pickup.cjs` (offer, request, approve end to end; Restricted project hidden); `tests/Hub.Tests/Api/TaskPickupTests.cs` visibility case.
- [ ] S04 Pickup offers report (S). After S01.
  - Covers FR-PKP-12.
  - Tests: report rows and causes; CSV and XLSX; no per-person column.
