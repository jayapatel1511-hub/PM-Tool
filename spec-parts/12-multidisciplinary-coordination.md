## 37. Multidisciplinary Coordination Amendment

Approved scope: Jay requested all six coordination additions and their researched refinements on 2026-09-26. This section records the product contract; implementation and acceptance remain pending. The product name is Tuesday; existing Hub code namespaces and filenames remain technical identifiers. The earlier 24 packets remain the implementation baseline, not evidence that packets 025–033 are built.

### 37.1 Shared rules and release boundary

All nine additions are approved product scope. The initial 50-person pilot may stage capabilities only after their own acceptance checks pass; a disabled packet remains explicitly unbuilt. This is not a production-readiness or ISO/BCF-compliance claim. Existing security, performance, independent-review and operational gates remain applicable. Section 10.8 supplies canonical entities and states; §8.10 supplies permissions. No new health rule is implied by a feature label.

- **FR-MDC-01.** Every new record MUST belong to one project (except a person availability override), have one accountable owner, a stable ID, a row version and an immutable activity history. Child assignments each have their own single owner; multiple reviewers do not create multiple task assignees. Existing task, deliverable and project status names remain unchanged.

- **FR-MDC-02.** Every command, query, aggregate, export and notification MUST enforce current server-side permissions and project lifecycle restrictions. Read Only is a veto on mutations; Archived and Cancelled projects are read-only; Complete follows the existing PM-only edit rule. Ownership never bypasses these restrictions. Recheck recipient access before composing confidential content and again before queued delivery; external parties have no account or outgoing notification.

- **FR-MDC-03.** All mutable writes, including bulk operations, MUST compare the versions the caller read. Conflicts return the affected record without overwriting newer work. Cross-record approval, publication and issue operations MUST be transactional and retry-safe. Assignment cannot create membership through a refused or empty operation, and reassignment must recheck independent-review rules.

- **FR-MDC-04.** All relationships in this amendment MUST remain within a project. A workspace may aggregate permitted projects but must not create cross-project dependencies. Deleted and superseded records remain available in authorised history; broken, deleted or inaccessible required references produce an explicit unresolved condition rather than an accepted state.

- **FR-MDC-05.** The product MUST retain document links and revision metadata only. An external reference records source system, stable source ID where available, URL/path, declared revision, registered-by and registered-at. Label the revision as manually registered unless verified through an authorised connector. A mutable URL is not proof of exact file contents or of the latest revision. No CAD/BIM viewer, file hosting, engineering calculation, AI or model parsing is added.

- **FR-MDC-06.** Lists MUST support scoped filtering, pagination, saved views and export; exports reconcile with the same permitted records. Controls are keyboard accessible, use text alongside colour, and use externalised strings. Notifications reuse existing preferences and digests; transitions create one deduplicated event per recipient and actionable item, not one event per dashboard.

- **FR-MDC-07.** All derived readiness and change flags MUST show their contributing records and rule. These additions do not silently change task progress, project health, due dates or logged actual hours. New attention items reuse the existing routing/digest mechanism and remain separate from health until an explicit health rule is specified.

- **FR-MDC-08.** Required owners/reviewers/receivers MUST be active and permitted to access the project. Removal or deactivation retains historical attribution and creates an actionable unassigned/inactive warning; replacement requires an authorised, audited command. No approval is inferred from silence. Project hold suspends overdue escalation under existing rules while retaining the pending workflow.

### 37.2 Discipline Handoffs and Acceptance

A receiving discipline can identify, accept and incorporate the exact input it needs for a defined purpose.

- **FR-HND-01.** A handoff MUST record the sending and receiving disciplines, one sending owner, one receiving owner, source deliverable/revision, at least one receiving task or deliverable, intended use, acceptance criteria, needed-by date and promised date. Drafts may omit a promised date; submission requires it. A sender promising after the needed-by date generates a visible mismatch without silently changing either date.

- **FR-HND-02.** The sender submits a fixed revision. The receiver may accept it for the stated use, request clarification or return it with a reason. Acceptance records the receiver, time, purpose and criteria outcome. Incorporation is a separate receiver action recording the target work and revision actually used; acceptance is not technical approval or a transfer of professional responsibility.

- **FR-HND-03.** Canonical transitions MUST be Draft → Submitted; Submitted → Accepted, Clarification Requested or Returned; Clarification Requested/Returned → Submitted with a response and revision; Accepted → Incorporated. A sender cannot accept their own handoff unless the existing self-review setting explicitly permits that same-person case. PM/lead cancellation requires a reason; prior transitions remain in history.

- **FR-HND-04.** A newer source revision MUST NOT overwrite acceptance of the earlier revision. Register it as a new handoff revision linked to the old one and initiate change assessment under packet 027. The receiver explicitly adopts or retains the prior revision with a documented disposition. Partial input is accepted only for a named limited purpose and scope; another purpose requires another handoff.

- **FR-HND-05.** The sender owns delivery and response; the receiver owns acceptance and incorporation. The PM or relevant Discipline Lead may assign these roles but cannot silently sign on their behalf. Multiple receiving disciplines get separate handoff records or child receipts with separate owners and states.

- **FR-HND-06.** Incoming and outgoing lists MUST distinguish promised, submitted, accepted and incorporated information, with overdue-by-needed-date and awaiting-response reasons. Existing task dependencies remain effective; accepting a handoff alone does not complete a task or unblock a separate unresolved dependency.

- **FR-HND-07.** Handoff creation from a deliverable/task MUST prefill permitted project, discipline, source and target references. The form asks only for missing fields. Repeating a submitted command returns the existing result and does not create duplicate receipts or notifications.

**Acceptance criteria**

- **AC-HND-01.** Given a Survey handoff promised after Civil needs it, when it is submitted, then both dates and the mismatch are visible to both owners without changing the task dates.

- **AC-HND-02.** Given revision A is submitted, when the named Civil receiver accepts it, then the handoff is Accepted but not Incorporated and the downstream task is not marked Complete.

- **AC-HND-03.** Given revision A has been incorporated, when revision B is registered, then A remains the recorded input and the receiver sees a pending assessment for B.

- **AC-HND-04.** Given a receiver returns a handoff without a reason or a sender tries to accept it, then the operation is refused unless the same-person setting explicitly allows the latter; authorised acceptance is attributed to its actual actor.

- **AC-HND-05.** Given two disciplines receive one source package, when only one accepts, then the other receipt remains pending; a restricted-project viewer cannot see either receipt in search, aggregate or export.

### 37.3 Multidisciplinary Reviews and Comment Closure

Each required discipline reviews a fixed package revision and unresolved comments remain visible through correction and verification.

- **FR-MRV-01.** A review package MUST identify one coordinator, a fixed manifest of deliverable revisions, the required disciplines, one reviewer per discipline, review due dates and the review purpose. The coordinator starts a round only when required reviewers and referenced revisions exist and are accessible.

- **FR-MRV-02.** A discipline review assignment MUST use Pending, In Review, Changes Required or Approved. The reviewer records their result for that round and revision set. The package uses Draft, In Review, Changes Required, Approved, Superseded or Cancelled; only all required current-round assignments Approved and all blocking comments Verified Closed allow Approved.

- **FR-MRV-03.** Each review comment MUST have an originator, one resolution owner, affected discipline/deliverable/revision, severity (Blocking or Advisory), response and evidence link. Its states are Open, Responded, Verified Closed and Withdrawn. The resolver records Responded; the originator verifies closure or returns it to Open with a reason. PM reassignment of verification requires a reason and an independent permitted reviewer.

- **FR-MRV-04.** Independent-review checks MUST apply to the work author/assignee and assigned reviewers on creation, replacement, bulk reassignment and final approval. The existing allow_self_review setting governs explicit exceptions. Package coordination rights alone do not grant technical approval rights.

- **FR-MRV-05.** Changing a manifest revision, required discipline or blocking criterion after review starts MUST create a new round. Preserve earlier comments and approvals; mark affected assignments Pending and carry unresolved comments forward with provenance. A recorded mapping may carry unaffected approvals forward, with reviewer acknowledgement of the new scope; never silently reuse approval of different content.

- **FR-MRV-06.** The package list MUST show outstanding disciplines, review workload, unresolved blocking comments and elapsed waiting time. Review completion remains separate from the deliverable Issued and Accepted lifecycle. Where a deliverable requires this package, its Ready to Issue command rechecks current review approval.

- **FR-MRV-07.** A required discipline cannot be removed to bypass open findings: removal requires PM reason, impact review and a new round. Withdrawing a comment requires its originator or a PM-designated independent verifier, a reason and retained history; a blocking finding requires the coordinator to acknowledge the withdrawal.

**Acceptance criteria**

- **AC-MRV-01.** Given Civil and Structural have approved but Electrical is Pending, then the package cannot be Approved or satisfy a required submission review gate.

- **AC-MRV-02.** Given the resolver responds to a blocking comment, then it remains open for verification until the originator or authorised independent replacement verifies closure.

- **AC-MRV-03.** Given an approved review references revision A, when revision B replaces an affected item, then affected approvals become pending in a new round and A remains in history.

- **AC-MRV-04.** Given a reassignment would make the author their own reviewer while self-review is disabled, then the entire reassignment/approval operation is refused without partial changes.

- **AC-MRV-05.** Given a required discipline is removed, then a reason and new round are required, and its earlier comments/approvals remain inspectable by authorised users.

### 37.4 Revision Awareness and Change Impact

People can identify the revision they used and make an explicit disposition when linked information changes.

- **FR-CHG-01.** A source revision MUST have a stable internal ID, declared external identifier/revision, title, issuer, registration time, link and scope. Published registrations are immutable snapshots; corrections create a replacement with a reason. Do not lexically sort revision labels to infer recency: an authorised explicit supersedes relationship defines the registered sequence, without cycles.

- **FR-CHG-02.** An InputUse link MUST record a receiving task/deliverable, its owner, source revision, intended use and adoption time. Multiple source documents are allowed. Registering a new revision never overwrites an InputUse link or claims that the source system has no newer information.

- **FR-CHG-03.** Publishing a change notice MUST require one owner, old/new revision or changed design-basis entry, a human-authored description, effective date, affected scope and assessment due date. Identify recipients by explicit InputUse/handoff/requirement relationships; allow an authorised owner to add known affected work. Display the detection boundary: unlinked work is not assessed.

- **FR-CHG-04.** Each affected owner MUST disposition the notice as Pending Assessment, Unaffected, Update Required or Clarification Needed, with rationale and evidence. Update Required creates or links a single-owner follow-up task and estimated effort/date impact; the reviewer verifies completion before Resolved. Retaining an older revision requires a recorded reason and approval from the PM or responsible Discipline Lead.

- **FR-CHG-05.** The notice is closed only after every required assessment is Unaffected with evidence (and approval where retaining an older revision) or Resolved with verified correction. Cancelling a correction task does not resolve an Update Required assessment; it needs a new evidenced, authorised disposition. No response remains pending. Acknowledgement means the notice was seen, not that an engineering impact assessment was completed.

- **FR-CHG-06.** Change propagation MUST be deterministic, bounded to explicit relationships and deduplicated per change/target. Show downstream linked items as potentially affected until assessed; do not automatically revise designs, dates, task completion or technical conclusions. Concurrent publication and adoption must recheck referenced versions.

- **FR-CHG-07.** The interface MUST show Current registered revision, Revision used and Assessment status together, with manual-registration/source-check timestamps. Closed projects retain history; later changes cannot mutate their issued records. Reopening or a permitted new change process is required.

**Acceptance criteria**

- **AC-CHG-01.** Given three items record use of revision A, when B explicitly supersedes A, then exactly those three receive pending assessments and unrelated/unlinked work is not claimed as checked.

- **AC-CHG-02.** Given an owner acknowledges a notice but has not assessed it, then Pending Assessment remains and closure is refused.

- **AC-CHG-03.** Given a receiver retains A with authorised rationale, then InputUse remains A, the assessment records that disposition and B remains the current registered source revision.

- **AC-CHG-04.** Given two simultaneous adoption/publication commands based on stale versions, then a conflict is returned and no mixed revision/approval snapshot is committed.

- **AC-CHG-05.** Given an Update Required assessment links a correction task, then the notice stays open until correction and verification are recorded; no due date changes automatically.

### 37.5 Submission Readiness and Issue Manifest

A submission is assembled from explicit revisions and accountable checks, with evidence for every readiness gate.

- **FR-SUB-01.** A submission package MUST have one accountable coordinator, one existing milestone, purpose, recipient reference, target date and a manifest of required deliverable revisions. A template may supply the checklist, but each project takes its own versioned snapshot.

- **FR-SUB-02.** Each checklist item MUST identify one checking owner, evidence or a derived source rule, and whether it is required. Required checks include required deliverables present, current required multidisciplinary reviews approved, blocking findings verified closed, required handoffs accepted for the purpose, and outstanding change assessments resolved. Show individual blockers; a percentage must not imply Ready.

- **FR-SUB-03.** States MUST be Draft, Checking, Ready, Issued, Superseded and Cancelled. Ready is derived from all required current checks Pass or an allowed approved Not Applicable. Draft → Checking is coordinator-controlled; Checking/Ready → Issued requires an authorised PM command. Issued is an immutable manifest snapshot linked to the actual external transmittal reference.

- **FR-SUB-04.** Only the PM may approve Not Applicable with reason and evidence on an optional applicability check. Required independent review, current revision identity, access checks and unresolved blocking findings cannot be waived through this checklist. A conditional submission requires a separate purpose and explicit applicable criteria; never relabel failed mandatory criteria as passed.

- **FR-SUB-05.** Changes to the manifest, linked review round, accepted input or applicable design basis MUST invalidate affected readiness evidence and return an unissued package to Checking. Issued manifests remain unchanged; publish a superseding package for corrections and retain both histories.

- **FR-SUB-06.** Issue MUST re-evaluate the manifest and all required checks in one transaction using expected versions. A change during checking returns a conflict or explicit blocker; no issue is recorded against stale sign-offs. Record who authorised issue, when, declared destination and external transmittal link; this does not send files or create a legal signature.

- **FR-SUB-07.** The readiness screen MUST group blockers by discipline and owner and allow opening the source item. A per-submission export includes revision manifest, evidence, unresolved items, exceptions and issue history within the caller access scope.

**Acceptance criteria**

- **AC-SUB-01.** Given every checklist row except an Electrical blocking review finding passes, then the package is Checking and Issue is refused with that source finding.

- **AC-SUB-02.** Given a Ready package references revision A, when an affected deliverable changes to B, then the unissued package returns to Checking and affected approvals are invalidated.

- **AC-SUB-03.** Given revision A was issued, when B is issued later, then the first manifest still shows A, its original authorisation and its original transmittal reference.

- **AC-SUB-04.** Given an owner changes a required review while the PM issues from a stale screen, then the issue transaction is refused without recording a partial issued manifest.

- **AC-SUB-05.** Given a PM tries to waive an unresolved blocking finding using Not Applicable, then the command is refused; a genuinely inapplicable optional check requires recorded justification.

### 37.6 Dated Capacity and Project Allocations

Supervisors can confirm production and review commitments for defined dates without double-counting the existing task forecast.

- **FR-CAP-01.** A PM or Discipline Lead may propose an allocation with one person, project, production/review purpose, inclusive date range, positive planned hours and linked tasks or review assignments. The person supervisor or Admin confirms it. States are Proposed, Confirmed, Declined, Cancelled and Completed; changing person, dates or hours after confirmation returns it to Proposed.

- **FR-CAP-02.** A person availability override MUST record date, available hours and a non-sensitive category; the supervisor for direct reports or Admin edits it. Store no leave reason, diagnosis or HR document. The override replaces that day capacity, not subtracts twice. Otherwise distribute the existing weekly capacity over that calendar working days; holidays contribute zero unless an explicit override supplies hours.

- **FR-CAP-03.** Spread allocation hours over eligible days within its inclusive range using the person calendar, with explicit per-day overrides available. Store decimal hours; reject negative values, inverted ranges and a positive allocation with no eligible days. Preserve exact totals and apply display rounding only after aggregation.

- **FR-CAP-04.** The grid MUST show available capacity, confirmed reservations, proposed requests and the existing remaining-work forecast separately. To calculate committed load, use max(reservation hours, linked remaining-work hours) for each confirmed allocation plus unlinked remaining-work hours, per person/week. A task/review estimate may belong to at most one allocation for the same person/date slice. Proposed requests are separate scenario demand, never confirmed load.

- **FR-CAP-05.** Allocation changes MUST not modify task estimates, progress, due dates, payroll or actual-hour entries. Use the existing workload thresholds and explain missing estimates. Review effort is explicit review-assignment demand, not duplicated production-task effort. Allocation is a staffing commitment, not evidence of work completion.

- **FR-CAP-06.** Supervisors see direct reports only within existing project permissions; PMs see their permitted project requests. If other assignments are outside the viewer scope, label visible totals as partial and do not claim complete spare capacity. Do not expose restricted project names, hours, counts or existence through aggregate differences.

- **FR-CAP-07.** Over-capacity confirmation MUST warn with the exact dates/hours and require a supervisor reason; it does not silently level or reschedule work. Conflicting simultaneous edits use expected versions, and confirmation records the current capacity/commitment snapshot for audit. A calendar/capacity change recomputes the warning without silently cancelling commitments.

**Acceptance criteria**

- **AC-CAP-01.** Given a confirmed 12-hour reservation and 8 linked estimated remaining hours in one week, then committed load is 12 hours, not 20; a separate unlinked 3-hour task makes it 15.

- **AC-CAP-02.** Given a day capacity override of 4 hours, then that day shows 4 hours regardless of the normal daily capacity and no leave reason is visible.

- **AC-CAP-03.** Given a confirmed request changes dates, then confirmation is withdrawn to Proposed and the supervisor must confirm the new range.

- **AC-CAP-04.** Given a PM can see only some of a person work, then the screen labels the visible workload partial and does not infer spare capacity from hidden work.

- **AC-CAP-05.** Given two stale competing confirmations, then the second must refresh the affected person/date version before confirming; a resulting overload requires an explicit reason.

### 37.7 Discipline Coordination View

A discipline lead can work through incoming inputs, outgoing promises, revisions in use, changes and ready work from one scoped view.

- **FR-DCV-01.** The view MUST answer five questions through fixed sections: What do we owe? What are we waiting for? Which revision are we using? What changed? What can we start? Include pending reviews, upcoming submissions and staffing conflicts as contextual filters and source links.

- **FR-DCV-02.** The default is the user discipline and permitted workspace projects; allow explicit project, discipline, owner and date filters. Every row identifies its project and underlying stable item. Scope persists across drill-down and return, and a saved view stores filters/columns rather than a copy of data.

- **FR-DCV-03.** Incoming handoffs distinguish submitted, accepted and incorporated; outgoing handoffs show promised versus needed dates. Change rows show Pending Assessment separately from acknowledgement. Readiness rows display remaining constraints, and submission rows show the exact failing checks.

- **FR-DCV-04.** Every count and export MUST reconcile with the same filtered source records at the same evaluation timestamp. Show evaluation freshness. Cross-project totals omit restricted work and never describe a partial view as an organisation-wide total.

- **FR-DCV-05.** Inline actions MUST invoke the same domain commands as the owning screen, with permission/refusal reasons, version checks and required evidence. Viewing, discussing or marking a meeting reviewed cannot close a review finding, accept a handoff or approve technical work.

- **FR-DCV-06.** Meeting mode MUST let the chair group related rows by blocker or source change, assign an action and set its date without duplicating existing tasks. Reuse meeting actions and existing notifications. Preserve prior weekly commitment snapshots while discussing a later week.

- **FR-DCV-07.** Place this capability under existing Coordination/Weekly Coordination and My Work. Render only implemented, authorised sections; show explicit unavailable capability messaging during staged rollout. Status text, keyboard navigation, a print view and direct source-item links are required.

**Acceptance criteria**

- **AC-DCV-01.** Given three tasks wait on one source handoff, then the view shows one blocker group with three linked tasks and clicking the count opens those same tasks.

- **AC-DCV-02.** Given only one project in a workspace is permitted, then rows, counts, search, print and export contain only that project.

- **AC-DCV-03.** Given an owner opens an item from a saved discipline view and returns, then project/discipline/date scope is preserved with refreshed current data.

- **AC-DCV-04.** Given the chair marks the coordination meeting reviewed, then no handoff, technical review, change assessment or commitment is automatically approved or closed.

- **AC-DCV-05.** Given one source change has several linked actions, then capture action reuses an existing linked action when selected and never silently creates duplicate work.
