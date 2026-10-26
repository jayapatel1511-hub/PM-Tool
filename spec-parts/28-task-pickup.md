## 53. Task Pickup Amendment

Approved scope: drafted on 2026-10-18 from Jay's request for a way for overloaded people to put a task up so someone else can pick it up, and approved by Jay the same day with the listed defaults (049-D1). The amendment is additive only: nothing in this section removes, hides, narrows or freezes a current feature, screen, route, report, setting or rule. Where an addition touches existing behaviour, today's behaviour stays the default. Packet 049 delivers this section.

A pickup offer is not a new item type. It is a short-lived state on an existing task: the task keeps its one accountable assignee until someone in the same team picks it up or a manager assigns it, and the handover then runs through the existing reassignment path (§8.5, Assign / reassign task), with its notification, log entry and version check. Nothing here assigns work automatically, ranks volunteers or scores people (§30, constitution II).

Where this section adds to §8.5 and §8.6 (permissions), §13.10 (My Work), §13.11 (Workload), §17 (notifications) and §20.1 (what is logged), this section governs.

### 53.1 Scope and boundary

- Offering a task for pickup and withdrawing the offer: §53.2.
- The Open for pickup list: §53.3.
- Who may pick up, and manager assignment: §53.4.
- Notifications and log: §53.5.

§53.6 sets how it ships. Out of scope: splitting a task's hours between people, offering deliverables, reviews, decisions or actions, cross-organisation pickup, a points or reward scheme for picking up work, and any automatic matching of offers to people.

### 53.2 Offering a task

- **FR-PKP-01.** The assignee of a task that is not Complete or Cancelled MUST be able to offer it for pickup with a required reason (free text) and optional fields: hours remaining, needed-by date and a note on skills or context. The project's PM, the Discipline Lead of the task's discipline and the assignee's Supervisor MAY offer it on the assignee's behalf; the offer then names both the offerer and the assignee. A task has at most one open offer.

- **FR-PKP-02.** While an offer is open the task MUST be unchanged in every other respect: same assignee, status, dates, dependencies, health and attention. The task shows an "Open for pickup" tag on its row, card and detail panel, with the reason, offerer and offer age.

- **FR-PKP-03.** The offerer, the assignee, the PM or the Discipline Lead MAY withdraw an open offer at any time with no reason required. An open offer MUST close on its own, with the closing cause recorded, when the task is completed, cancelled, deleted or reassigned by any other path, or when its project is completed or archived.

### 53.3 Open for pickup list

- **FR-PKP-04.** A new **Open for pickup** list MUST show the open offers the viewer may pick up (FR-PKP-06) or assign (FR-PKP-07), on tasks the viewer can already see (§8.7). It is reachable from My Work and from each project's Work tab group as a filter, and lists task key and name, project, discipline, offerer, reason, hours remaining, needed-by date, due date and offer age. It filters by project, discipline and needed-by date and sorts by needed-by date, then due date. A viewer in the assignee's team (FR-PKP-06) sees the offer with **Pick up**; the assignee's direct manager, the project's PM and the Discipline Lead of the task's discipline see it with **Assign**, which opens the task with its assignment choice, even when they are outside the team. Each row says which actions the viewer has. Anyone else does not see the offer. No new visibility is granted: a Restricted project's offers appear only to people who can view that project.

- **FR-PKP-05.** The viewer's own load band for the needed-by week (§45.4) MUST be shown beside the **Pick up** action as information only. It never hides, blocks or orders offers.

### 53.4 Who may pick up, and manager assignment

- **FR-PKP-06.** Only a person in the **same team** as the task's assignee MAY **Pick up** an open offer:
  - when the company structure is on (§54, `flag.org_structure`) and the assignee has a home unit, the same team is every active person whose home unit is that unit;
  - otherwise, it is every active person who shares the assignee's direct manager (`supervisor_id`, §8.8).

  The picker must also be able to view the project. A person cannot pick up their own task. The server checks the team at the moment of the pick and returns 403 for anyone outside it.

- **FR-PKP-07.** Picking up MUST reassign the task to the picker at once, with no approval step, through the existing reassignment command in one transaction, and close the offer. A picker who is not on the project team is added as a Team Member in the same transaction. When two people pick up the same offer, the first save wins; the second is refused with the existing conflict message because the task's rowVersion changed. The assignee's direct manager, the project's PM and the Discipline Lead of the task's discipline MAY instead **Assign** an offered task to any person in the same team, which closes the offer in the same way. The server enforces both rules.

- **FR-PKP-08.** No one outside the same team MAY pick up an offer, and no setting widens the team. A PM or Discipline Lead who wants someone outside the team keeps today's reassignment path (§8.5), which closes the offer as "reassigned elsewhere" (FR-PKP-03).

- **FR-PKP-09.** After a pickup, the previous assignee MUST become a watcher of the task (§8.6, participants) unless they opt out when offering, so context is not lost. Hours already logged against the task (§36.8) stay with the person who logged them.

### 53.5 Notifications and log

- **FR-PKP-10.** Notifications (§17) MUST be sent as follows, never to the person who acted:

| Event | Recipients | In-app | Email digest |
|---|---|---|---|
| Task offered for pickup | Everyone in the same team (FR-PKP-06), assignee's direct manager, PM, Discipline Lead of the discipline, assignee (when offered on their behalf) | ● | ● |
| Task picked up | Previous assignee, offerer, assignee's direct manager, PM, Discipline Lead of the discipline | ● | ● |
| Task assigned from an offer | New assignee, previous assignee, offerer | ● | ● |
| Offer closed on its own (FR-PKP-03) | Offerer | ● | |

- **FR-PKP-11.** Offering, withdrawing, closing, picking up and assigning from an offer MUST each write one activity entry on the task (§20.1), with actor, reason or note, and, for a pick-up or assignment, the old and new assignee.

- **FR-PKP-12.** A **Pickup offers** catalogue report MUST list offers in a date range with project, discipline, offered, closed, closing cause (picked up, assigned by manager, withdrawn, task closed, reassigned elsewhere) and days open, with CSV and XLSX export, under the list's visibility rules. Counts are by project and discipline, never per person.

### 53.6 Shipping dark

- **FR-PKP-13.** The whole amendment MUST merge dark behind `flag.task_pickup` under §43's flag rules and is turned on by Jay. With the flag off, every screen, response, report, notification and export is as it was before this amendment.

### 53.7 Acceptance criteria

- **AC-PKP-01.** Given Alex is Over-allocated and assigned to task 1234-T0042, when Alex offers it with reason "Two deliverables due W42", then the task shows "Open for pickup", its assignee, status, dates and health are unchanged, and Alex's teammates, Alex's manager, the PM and the Structural Discipline Lead are notified.

- **AC-PKP-02.** Given Alex's open offer and Jill in Alex's team, when Jill picks it up, then the task is assigned to Jill at once with no approval, Alex becomes a watcher, the offer closes as picked up, and the activity log holds the offer and the pick-up with old and new assignee.

- **AC-PKP-03.** Given Diane is neither in Alex's team nor someone who may assign Alex's task, then the offer is not in Diane's Open for pickup list, and calling the pick-up endpoint returns 403 and changes nothing.

- **AC-PKP-04.** Given Jill and Kim, both in Alex's team, pick up the same offer at the same moment, then exactly one of them gets the task and the other sees the existing conflict message.

- **AC-PKP-05.** Given an open offer, when the task is completed, or reassigned by the PM directly to someone outside the team, then the offer closes with that cause and the offerer is notified once.

- **AC-PKP-06.** Given an offer on a Restricted project, then it appears in the Open for pickup list only for people who can view that project, and its hours are absent from everyone else's list and report.

- **AC-PKP-07.** Given Alex's open offer, when Alex's direct manager assigns it to Kim in Alex's team, then the task is assigned to Kim and the offer closes as assigned by manager; when the manager tries to assign it to someone outside the team from the offer, then the server refuses.

- **AC-PKP-09.** Given Alex's open offer and Sam, Alex's direct manager, who is outside Alex's team, then the offer is in Sam's Open for pickup list with Assign and without Pick up, and Sam's call to the pick-up endpoint returns 403.

- **AC-PKP-08.** Given `flag.task_pickup` is off, then existing API tests and browser suites pass unchanged, and responses, reports, notifications and exports carry no new fields.

### 53.8 Decisions

Each open decision below uses its default until Jay decides otherwise. The rows are also in `specs/DECISIONS.md` as 049-D1 to 049-D7.

| # | Decision | Default used |
|---|---|---|
| D1 | Approve this amendment's scope | Approved by Jay 2026-10-18 with the listed defaults |
| D2 | Who may pick up? | Decided by Jay 2026-10-18: only the same team as the assignee (same home unit under §54, otherwise the same direct manager); a picker not on the project team is added as Team Member |
| D3 | Approval, and who may assign? | Decided by Jay 2026-10-18: no approval for a same-team pick-up; the assignee's direct manager, PM or Discipline Lead may assign an offered task to someone in the same team |
| D4 | Whole task or share of it? | Whole task only; adding a collaborator stays the existing way to share work |
| D5 | Previous assignee after pickup | Becomes a watcher unless they opt out when offering |
| D6 | Does an offer expire? | No automatic expiry; the offer closes only by FR-PKP-03 |
| D7 | Who sees an offer in the Open for pickup list? | Decided by Jay 2026-10-25: the assignee's team with Pick up, and the direct manager, PM and Discipline Lead with Assign (FR-PKP-04) |
