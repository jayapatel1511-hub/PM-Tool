# Tasks: Task Pickup

Each slice is one PR, merges dark behind `flag.task_pickup` and adds the flag pair test (off: identical to before; on: present), covering AC-PKP-08 for its area. Every slice needs packet 039's flags slice (S09) first; Jay approved 049-D1 on 2026-10-18.

- [x] S01 Offer, withdraw, auto-close (S, Tier B: migration). (local part; flag stays dark until Jay turns it on)
  - `TaskPickupOffer` table; `POST /tasks/{id}/pickup-offer`, `DELETE /tasks/{id}/pickup-offer`; auto-close in the task save path.
  - Covers FR-PKP-01, FR-PKP-02, FR-PKP-03, FR-PKP-11, FR-PKP-13; AC-PKP-01, AC-PKP-05, AC-PKP-08.
  - Tests in `tests/Hub.Tests/Api/TaskPickupTests.cs`: assignee and on-behalf offers; second offer refused; task fields unchanged; complete, cancel, delete and direct reassign each close the offer with the right cause; non-permitted offerer gets 403.
- [x] S02 Pick up and assign (M). After S01. (local part; flag stays dark until Jay turns it on)
  - `Permissions.SameTeam`; `POST /tasks/{id}/pickup` and `POST /tasks/{id}/pickup-offer/assign`; both through the existing reassignment command, adding the picker as Team Member when needed; watcher on pickup; notifications of FR-PKP-10.
  - Covers FR-PKP-06, FR-PKP-07, FR-PKP-08, FR-PKP-09, FR-PKP-10; AC-PKP-02, AC-PKP-03, AC-PKP-04, AC-PKP-07.
  - Tests in `TaskPickupTests.cs`: same-team pick reassigns at once; outside-team pick 403; team by home unit with flag.org_structure on and by shared manager with it off; two simultaneous picks, one wins; manager, PM and DL assign within team, refused outside it; picker added to project team; notification recipients exclude the actor.
- [x] S03 Open for pickup list and task UI (M). After S02. (local part; `task-pickup.cjs` is a source check and the flows run in mocked Chromium; live and human acceptance remain)
  - Tag and actions in the task panel and rows; Open for pickup list from My Work and the project Work group; load band beside Request pickup.
  - Covers FR-PKP-04, FR-PKP-05; AC-PKP-06.
  - Tests: `web/tests/task-pickup.cjs` (offer and same-team pick-up end to end; outside-team offer hidden; Restricted project hidden); `tests/Hub.Tests/Api/TaskPickupTests.cs` visibility case.
- [x] S04 Pickup offers report (S). After S01. (local part; rows tested in `TaskPickupTests`; CSV and XLSX use the shared report export, no pickup-specific export test)
  - Covers FR-PKP-12.
  - Tests: report rows and causes; CSV and XLSX; no per-person column.
