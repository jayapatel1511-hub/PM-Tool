## 53. Task Pickup Amendment

Approved scope: drafted on 2026-10-18 from Jay's request for a way for overloaded people to put a task up so someone else can pick it up, and approved by Jay the same day with the listed defaults (049-D1). The amendment is additive only: nothing in this section removes, hides, narrows or freezes a current feature, screen, route, report, setting or rule. Where an addition touches existing behaviour, today's behaviour stays the default. Packet 049 delivers this section.

A pickup offer is not a new item type. It is a short-lived state on an existing task: the task keeps its one accountable assignee until a pickup is approved, and the handover then runs through the existing reassignment path (§8.5, Assign / reassign task), with its notification, log entry and version check. Nothing here assigns work automatically, ranks volunteers or scores people (§30, constitution II).

Where this section adds to §8.5 and §8.6 (permissions), §13.10 (My Work), §13.11 (Workload), §17 (notifications) and §20.1 (what is logged), this section governs.

### 53.1 Scope and boundary

- Offering a task for pickup and withdrawing the offer: §53.2.
- The Open for pickup list: §53.3.
- Requesting, approving and declining a pickup: §53.4.
- Notifications and log: §53.5.

§53.6 sets how it ships. Out of scope: splitting a task's hours between people, offering deliverables, reviews, decisions or actions, cross-organisation pickup, a points or reward scheme for picking up work, and any automatic matching of offers to people.

### 53.2 Offering a task

- **FR-PKP-01.** The assignee of a task that is not Complete or Cancelled MUST be able to offer it for pickup with a required reason (free text) and optional fields: hours remaining, needed-by date and a note on skills or context. The project's PM, the Discipline Lead of the task's discipline and the assignee's Supervisor MAY offer it on the assignee's behalf; the offer then names both the offerer and the assignee. A task has at most one open offer.

- **FR-PKP-02.** While an offer is open the task MUST be unchanged in every other respect: same assignee, status, dates, dependencies, health and attention. The task shows an "Open for pickup" tag on its row, card and detail panel, with the reason, offerer and offer age.

- **FR-PKP-03.** The offerer, the assignee, the PM or the Discipline Lead MAY withdraw an open offer at any time with no reason required. An open offer MUST close on its own, with the closing cause recorded, when the task is completed, cancelled, deleted or reassigned by any other path, or when its project is completed or archived.

### 53.3 Open for pickup list

- **FR-PKP-04.** A new **Open for pickup** list MUST show every open offer on tasks the viewer can already see (§8.7). It is reachable from My Work and from each project's Work tab group as a filter, and lists task key and name, project, discipline, offerer, reason, hours remaining, needed-by date, due date and offer age. It filters by project, discipline and needed-by date and sorts by needed-by date, then due date. No new visibility is granted: a Restricted project's offers appear only to people who can view that project.

- **FR-PKP-05.** The viewer's own load band for the needed-by week (§45.4) MUST be shown beside the **Request pickup** action as information only. It never hides, blocks or orders offers.

### 53.4 Requesting and approving

- **FR-PKP-06.** A member of the project team (§12.2, TM rules) MAY **Request pickup** on an open offer, with an optional note. Several people may request the same offer; each request is listed on the offer in the order received. A person cannot request their own task.

- **FR-PKP-07.** The project's PM or the Discipline Lead of the task's discipline MUST approve one request or decline any request with a reason. The server enforces who may approve. Approving reassigns the task to the requester through the existing reassignment command in the same transaction, closes the offer and declines the other open requests with the reason "Picked up by [name]". The approval is refused, and nothing changes, if the task's rowVersion changed since the approver loaded it.

- **FR-PKP-08.** An organisation setting `task_pickup_self_approve` (default off) MUST, when on, let the requester take the task on request without approval when they are a member of the task's discipline on that project. The server checks discipline membership at the moment of the request.

- **FR-PKP-09.** After a pickup, the previous assignee MUST become a watcher of the task (§8.6, participants) unless they opt out at approval, so context is not lost. Hours already logged against the task (§36.8) stay with the person who logged them.

### 53.5 Notifications and log

- **FR-PKP-10.** Notifications (§17) MUST be sent as follows, never to the person who acted:

| Event | Recipients | In-app | Email digest |
|---|---|---|---|
| Task offered for pickup | PM, Discipline Lead of the discipline, assignee (when offered on their behalf), assignee's Supervisor | ● | ● |
| Pickup requested | PM, Discipline Lead of the discipline, offerer | ● | |
| Pickup approved | Requester, previous assignee, offerer | ● | ● |
| Pickup declined | Requester | ● | |
| Offer closed on its own (FR-PKP-03) | Offerer and open requesters | ● | |

- **FR-PKP-11.** Offering, withdrawing, closing, requesting, approving and declining MUST each write one activity entry on the task (§20.1), with actor, reason or note, and, for approval, the old and new assignee.

- **FR-PKP-12.** A **Pickup offers** catalogue report MUST list offers in a date range with project, discipline, offered, closed, closing cause (picked up, withdrawn, task closed, reassigned elsewhere) and days open, with CSV and XLSX export, under the list's visibility rules. Counts are by project and discipline, never per person.

### 53.6 Shipping dark

- **FR-PKP-13.** The whole amendment MUST merge dark behind `flag.task_pickup` under §43's flag rules and is turned on by Jay. With the flag off, every screen, response, report, notification and export is as it was before this amendment.

### 53.7 Acceptance criteria

- **AC-PKP-01.** Given Alex is Over-allocated and assigned to task 1234-T0042, when Alex offers it with reason "Two deliverables due W42", then the task shows "Open for pickup", its assignee, status, dates and health are unchanged, and the PM, the Structural Discipline Lead and Alex's Supervisor are notified.

- **AC-PKP-02.** Given an open offer, when Jill and Sam both request pickup and the Discipline Lead approves Jill, then the task is assigned to Jill, Alex becomes a watcher, Sam's request is declined with "Picked up by Jill", and the activity log holds the offer, both requests, the approval with old and new assignee, and the decline.

- **AC-PKP-03.** Given a Team Member who is not the PM or the Discipline Lead, when they call the approve endpoint, then the server returns 403 and nothing changes.

- **AC-PKP-04.** Given the task was edited after the approver loaded it, when they approve, then the approval is refused with the existing conflict message and no reassignment happens.

- **AC-PKP-05.** Given an open offer, when the task is completed, or reassigned by the PM directly, then the offer closes with that cause and the offerer and requesters are notified once.

- **AC-PKP-06.** Given an offer on a Restricted project, then it appears in the Open for pickup list only for people who can view that project, and its hours are absent from everyone else's list and report.

- **AC-PKP-07.** Given `task_pickup_self_approve` is on, when a member of the task's discipline requests pickup, then the task is reassigned immediately; when a member of another discipline requests, then approval is still required.

- **AC-PKP-08.** Given `flag.task_pickup` is off, then existing API tests and browser suites pass unchanged, and responses, reports, notifications and exports carry no new fields.

### 53.8 Decisions

Each decision below uses its default until Jay decides otherwise. The rows are also in `specs/DECISIONS.md` as 049-D1 to 049-D6.

| # | Decision | Default used |
|---|---|---|
| D1 | Approve this amendment's scope | Approved by Jay 2026-10-18 with the listed defaults |
| D2 | Who may request pickup? | Project team members only; anyone else is added to the team first through the existing team path |
| D3 | Who approves? | PM or Discipline Lead of the task's discipline; self-approval off (`task_pickup_self_approve`) |
| D4 | Whole task or share of it? | Whole task only; adding a collaborator stays the existing way to share work |
| D5 | Previous assignee after pickup | Becomes a watcher unless they opt out at approval |
| D6 | Does an offer expire? | No automatic expiry; the offer closes only by FR-PKP-03 |
