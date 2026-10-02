# Verification: Ready-to-Start Planning and Weekly Commitments

**Date**: 2026-09-26
**State**: Domain, additive schema and partial API increments. No packet UI, deployed browser or pilot execution performed.

Documentation validation is recorded in `docs/coordination-spec-validation.md`. Product acceptance scenarios AC-RDY-01, AC-RDY-02, AC-RDY-03, AC-RDY-04, AC-RDY-05 are **not run end to end**. Implementation tasks remain unchecked until their full scope is complete.

## Domain rules increment

`src/Hub.Domain/Readiness.cs` now defines canonical readiness checks and derived states, including explicit unknown applicability, a narrowly scoped live assumption permission, and the rule that review/submission gates cannot be overridden by that permission. It also defines performer-only commitment and immutable-snapshot denominator rules. The focused `ReadinessTests` domain suite passed 3/3 locally after recompilation, covering a submitted handoff, unknown applicability, expiry and changed-basis reassessment, nonwaivable review gate, performer signature and a withdrawn promise retained in the original denominator. The rules alone do not prove API, browser or acceptance behavior. Packet 032 remains **UNPROVEN** for product acceptance.

## Additive schema increment

The `ReadinessSchema` migration adds project-scoped assessments/checks, work constraints, assumption exceptions, weekly snapshots, commitments and commitment events. It constrains canonical states and target types, unique assessment checks and one snapshot per project/week. Database triggers reject changes to captured snapshots, edits to a signed promise's output/criteria/date/owner, removal of commitment history, and edits to commitment events. The migration was applied only to ephemeral PostgreSQL by the test factory; the persistent `hub_review_local` database has **not** received it. `dotnet ef migrations has-pending-model-changes` reported no drift after a fresh build. Focused readiness tests passed 5/5, including database uniqueness, immutable promise and snapshot behavior, and a permitted Met transition. This is schema evidence, not an API or product acceptance result.

## Scoped assessment API increment

The project API now lets the named task or deliverable owner create an intended output and completion criteria with an explicit unknown value for each of the nine canonical checks. A PM or responsible discipline lead can record a check's applicability, reason and optional source URL. Both commands use project-scoped permissions, row versions and idempotent request IDs; neither command accepts a caller-supplied Ready or pass/fail result. The focused `ReadinessApiTests` passed 1/1 against ephemeral PostgreSQL after recompilation, covering restricted-project denial, wrong-role denial, stale versions, retry, duplicate creation and the unknown state after one applicability decision. Actual source-derived outcomes, exceptions, constraints, weekly commitments, browser workflow and the acceptance scenarios remain **UNPROVEN**.

## Constraint and weekly promise API increments

The weekly promise API now records a chair or performer proposal for the current task/deliverable owner, requires that performer to sign it, and captures project-wide weekly snapshots under PM authority. An attributed event records Met, Not Met or Withdrawn; the snapshot count remains the denominator after withdrawal. A PM can record Not Met or Withdrawn with a reason when reassignment prevents the performer from closing the promise, but cannot attest Met for someone else. The focused `Chair_proposal_requires_performer_confirmation` PostgreSQL API test passed 1/1, covering those permissions and the retained withdrawal count.

The test sets readiness checks directly in its isolated fixture. A later increment derives several checks from live sources, described below; capacity and review source evaluation remains open. Proceed under Assumption is refused until its exact-version, expiry, verifier and approval provenance is validated. Meeting-week boundary automation, a three-week UI, matching completion evidence to criteria, export and full AC-RDY-01 through 05 remain **UNPROVEN**. This increment is not packet acceptance.

The same API now creates a dated, sourced constraint for affected task/deliverable work, with a distinct removal owner. That owner may propose evidence of resolution, but only the unchanged affected work owner can verify removal; a PM can cancel an open or proposed constraint. Versioned, project-locked and retry-safe commands preserve each transition in audit. A focused PostgreSQL `ReadinessApiTests` run passed 1/1 after recompilation, including proposal refusal for the affected owner, verification refusal for the removal owner, stale-version refusal, and successful affected-owner verification. Mandatory production-owner applicability cannot be marked false. These checks do not establish source-derived readiness, the exception workflow, weekly snapshots, UI or AC-RDY-01 through 05; those remain **UNPROVEN**.

## Source-derived check increment

The readiness detail and weekly signature command now reevaluate linked handoffs, task/deliverable predecessors, blocking decisions, the latest basis use for each entry, unresolved basis conflicts, pending basis impacts, assigned production owner and open or resolution-proposed constraints. A stale stored Ready state cannot by itself authorise a weekly signature. A submitted handoff remains a blocker until acceptance or incorporation, and soft-deleted dependencies or links are excluded. The combined focused PostgreSQL set for design basis, templates, readiness and handoffs passed 36/36 after recompilation. Availability, review and submission checks still rely on recorded applicability rather than full source evaluation; the exception workflow, three-week UI, browser flow and AC-RDY-01 through 05 remain **UNPROVEN**.

Active linked constraints in Handoff, Decision, Basis, Capacity and Review categories now force their corresponding check to applicable and unsatisfied during each source evaluation. A focused PostgreSQL `ReadinessApiTests|HandoffsTests` run passed 26/26 after that change, including a proposed Handoff removal remaining blocked and a verified removal clearing the check. Other constraint categories still affect the aggregate Not Ready state without mapping to a named check. Full source-derived capacity/review/submission checks remain open.

## Scoped assumption exception increment

The project API now records a time-limited readiness exception for an exact Proposed assumption version linked to the affected work. A PM or responsible lead must approve it, with a distinct named verifier, bounded work and risk; an existing basis disposition must cover the same version, scope, owner and expiry. Evaluation requires that sole Proposed assumption to be the only blocker, no active constraint, and all other canonical checks assessed. Expiry or supersession returns Needs Assessment while approval history remains. The focused combined PostgreSQL set passed 38/38 with permission, stale-version, retry, expiry and supersession cases. The three-week UI, fully derived capacity/review/submission checks and complete AC-RDY-01 through 05 remain UNPROVEN.

## Dated production capacity and weekly window increment

The readiness evaluator now derives the named production owner's dated task demand, daily working capacity, availability overrides and confirmed allocation reservations. Target-linked reservation hours are counted once; an unlinked competing reservation still reduces daily capacity. Missing target estimates or dates, another scheduled active task outside the target calculation, and allocations from another project remain unknown rather than producing a false Ready result. A deliverable without open owner-assigned production tasks is marked not applicable. Focused PostgreSQL `ReadinessApiTests|ReadinessSchemaTests` passed 7/7, including available, unavailable, unknown, a zero-capacity day, linked reservation, competing reservation, and existing readiness lifecycle cases. This does not yet derive review capacity or submission gates, nor does it prove capacity from every concurrent project workload is fully assessed.

The project navigation now has a read-only weekly readiness window. It requests each week separately, displays commitments and immutable snapshot denominator with Met and later Withdrawn counts, links work items and Weekly Coordination, validates date ranges, and warns when the API's 500-row weekly cap truncates results. The production frontend build passed; lint passed with existing warnings in other screens. The default reads the new organisation lookahead setting (3 weeks, configurable from 1 to 12). Focused domain and API tests passed 18/18 on the final setting change, including invalid bounds and the value returned to a project user through `/me`. Deployed browser operation, keyboard review and complete FR-RDY-07 remain UNPROVEN. No pilot or production acceptance is claimed.

The window now also requests project-scoped constraints and ready outputs from a new API that reevaluates current readiness rather than displaying cached Ready states. A focused test created a Ready output, then an open constraint: the output disappeared from the Ready list while the constraint remained visible; a restricted-project outsider received 404. Integration review found false Ready risks from other-project work and undated deliverable work; both now return unknown, with a focused regression test. A later check confirmed undated competing work also remains unknown. The affected readiness, handoff and location tests passed 33/33 and the full local PostgreSQL suite passed 432/432 after that safeguard. The frontend production build and lint passed with existing warnings elsewhere. Each aggregate reports its count and a 500-row truncation flag. Complete browser and accessibility behavior remains UNPROVEN; CI and review/submission capacity gates remain open.

## Required review gate increment

The current readiness evaluation now checks a deliverable's required review package and its current round in the same project and package. A deliverable marked as requiring review without a linked package is Not Ready; a linked package remains blocked until both package and current round are Approved and the target deliverable remains in the round manifest. When no canonical package requirement exists, a PM/lead's manually assessed Review Gate is retained rather than silently cleared. A focused PostgreSQL API test passed for missing package, Changes Required, Approved and retained manual review. CI on the earlier gate increment passed; deployed browser behavior remains UNPROVEN.

## Review capacity increment

For a deliverable with a required current review round, readiness now evaluates each active discipline review assignment against its current reviewer, due date and confirmed in-scope Review allocation links. The calculation uses explicit `ReviewHours`, dated allocation reservations, working calendar, personal availability overrides and current reviewer capacity. Missing or stale links, invalid reviewer/date relationships, competing production workload, out-of-scope reservations and excessive date windows return unknown; overload returns unsatisfied. Manual Review Capacity applicability is retained when no canonical review requirement exists. A focused PostgreSQL test passed for confirmed fit, an out-of-scope reservation returning unknown, another same-project allocation without double-counting linked review demand, overload and missing-link unknown. The full integrated local suite passed 435/435 before those test extensions; the focused extended test passed afterward. CI on this newest increment, deployed browser behavior and submission-gate source evaluation remain UNPROVEN. Packet 032 product acceptance remains UNPROVEN.

## Weekly promise browser commands — 2026-10-01

The readiness window now offers versioned proposals, performer signatures, PM snapshot capture and permitted Met/Not Met/Withdrawn outcomes through the existing retry-safe command APIs. A project-scoped detail endpoint supplies lifecycle-aware action permissions and attributed outcome history; command handlers still reevaluate permissions, readiness, target ownership, constraints and versions. Met requires completion evidence. A proposed or withdrawn promise cannot silently alter the captured denominator. No schema migration was needed.

**PASS (local slice):** the affected PostgreSQL readiness API class passed 10/10, including chair/performer/read-only detail permissions, restricted-project 404, performer-only signing, fixed snapshot denominator and retained withdrawal history. Frontend production build, lint and `git diff --check` passed. Chrome against persistent `hub_review_local` created a Taylor-chaired proposal for Jay's synthetic work, withheld Taylor's Sign control, offered Jay's signature, and displayed the API's readiness refusal. The promise remained Proposed with no outcome event. The settled refusal dialog had zero WCAG 2.1 A/AA axe violations and zero incomplete checks; no JavaScript errors were observed.

**UNPROVEN:** successful browser signing, snapshot capture and evidence-backed outcome recording; manual keyboard/assistive-technology review; deployed password-authenticated workflows; complete packet acceptance. The readiness assessment/constraint/exception command UI, source-derived Submission Gate and FR-RDY-02 direct/bulk task-start authorization remain open. This synthetic local slice does not establish real pilot acceptance.

## Readiness assessment browser slice — 2026-10-01

The window now opens a work-scoped inspector with current evaluated state, unknown/blocking checks, source reasons and evidence. Only the named performer can define an output. PM or responsible discipline lead applicability forms use both assessment and check versions, require a rationale and retain optional evidence; Production Owner cannot be marked not applicable. Applicability does not record satisfaction, and current linked source evaluation remains authoritative. No API semantics or schema changed.

**PASS (local slice):** frontend production build/lint and diff checks passed. Real Chrome withheld output creation from Taylor for Jay-owned work, let Jay create its synthetic assessment, withheld the Production Owner not-applicable choice, and saved a lead's reason/evidence for a non-applicable synthetic Submission Gate. The record survived reload while seven other unknown checks kept the output Needs Assessment. The settled inspector axe scan had zero WCAG 2.1 A/AA violations and zero incomplete checks; no JavaScript errors were observed. The earlier affected readiness API class passed 10/10, including owner creation, PM/lead applicability, mandatory owner, stale versions, retry and restricted access.

**UNPROVEN:** constraint/exception command browser flows, successful promise lifecycle browser rehearsal, deployed authentication, manual assistive-technology review and complete packet acceptance. Submission Gate source relationships and task-start policy still require their pending product decisions.

## Constraint removal browser slice — 2026-10-01

The inspector now records constraints with category, evidence/source URL, a distinct removal owner and needed-by date. Only that removal owner sees Propose resolution; only the unchanged affected work owner sees Verify removal. PM/lead cancellation remains reasoned and version checked. Current constraints are loaded independently of assessment creation, so work without an assessment can still record a blocker. Each command uses existing retry-safe APIs; no schema or server semantics changed.

**PASS (local slice):** final frontend build/lint and diff checks passed. Chrome created a synthetic Jay-work/Taylor-removal-owner Handoff constraint, withheld Jay's proposal action, and showed Not Ready. Taylor's evidence proposal retained Not Ready and withheld Taylor's verification action. Jay verified the evidence; the record retained Verified Removed, Jay's verifier ID and the resolution URL. The source evaluator then returned Needs Assessment for the other unknown inputs. No JavaScript errors occurred. The settled inspector had zero WCAG 2.1 A/AA axe violations; one incomplete contrast item was inspected and identified as an aria-hidden status glyph containing only non-text characters. Manual assistive-technology/contrast review remains UNPROVEN.

The earlier affected API class passed 10/10, including distinct-owner proposal/verification, stale-version refusal, retry and read-access checks. Deployed password-authenticated behavior, cancellation browser rehearsal, linked canonical constraint records, notification delivery and full packet acceptance remain UNPROVEN.

## Constraint and weekly promise notifications — 2026-10-01

Four catalogue events (App on, Email off by default; Admin defaults and per-user preferences apply): `ConstraintAction` (direct: removal owner on creation; affected work owner when removal is proposed and needs verification), `ConstraintOutcome` (removal and affected owners on Verified Removed or Cancelled), `CommitmentProposed` (direct: performer when someone else proposes their promise, AC-RDY-05) and `CommitmentChanged` (performer and proposer on Committed, Met, Not Met or Withdrawn). Notices are queued by the existing `Notifier` inside the `Coordination.Run` transaction, so refused, stale or replayed commands add none. The actor never receives one. All four join the project-scoped set that rechecks recipient access when the notice is composed and again when queued email is delivered (FR-MDC-02, FR-MDC-03, FR-MDC-06). Basis: §17.1 principles and FR-MDC-06. §17.2 has no readiness rows, so the recipients and defaults are authored choices.

**PASS (local):** `dotnet build Hub.slnx` succeeded. `dotnet test --filter "ReadinessNotificationTests|ReadinessApiTests|LocationIssueTests|SubmissionApiTests"` passed 20/20, including the new class at 2/2. The new tests prove: recipients receive the notice and the actor does not; a 403 proposal, a 409 stale verification, a 400 signature of an unassessed promise and a 403 chair Met attestation add no notice; retries add no duplicate; and a removal owner removed from a restricted project gets neither an in-app notice nor an email. `DomainHelpersTests|AllocationApiTests` passed 24/24. `tools/trace_spec.py --check`: 0 not cited. `npm --prefix web run build` and `lint` passed with only existing warnings.

**UNPROVEN:** email delivery through the worker for these events, digest inclusion, and browser display of the new notices. No notices are sent for the weekly snapshot, assessment creation, applicability changes or readiness exceptions.

## Scoped assumption exception browser slice — 2026-10-01

The readiness inspector now lists every recorded assumption exception for the work (newest first) with its state (In effect, Expired or Not in effect), linked Proposed assumption key/version/title and scope, limited work, risk, approving PM/lead with date, responsible verifier and expiry. Basis keys and scope are resolved from the existing design-basis register (`kind=Assumption&affectedWorkId=…` plus entry detail). Only users who may manage coordination for the work's discipline (the same `canAssess` gate as applicability, mirroring `ManageCoordination`) see Approve assumption exception, and only when the work's current use is a Proposed assumption with an unexpired proceed disposition for the performer. The form uses the shared retry-safe `CommandForm`: assessment and basis version row versions, required limited work and risk, a verifier list excluding the performer and the approver, and an expiry bounded by today and the disposition expiry; server refusals are shown. Frontend only (`ReadinessForms.tsx`, `en.ts`); no API semantics or schema changed.

**PASS (local slice):** `npm --prefix web run build` passed; `npm --prefix web run lint` exited 0 with 0 errors and 88 warnings, none in the changed files; `git diff --check` passed. A fresh `hub_agent_exception` database with `Seed__ReviewDemo=true` ran on `http://127.0.0.1:5091`. Setup used the existing APIs: Taylor created a synthetic Assumption with Jay as owner, recorded a proceed disposition, Jay linked it to DEMO-101-T0001 and defined the output, and Taylor marked the other non-mandatory checks not applicable. This left the work Not Ready, blocked only by Basis. In Playwright Chrome, Yagmur (team member/reviewer) saw the exception section but no approve control; a direct POST by Yagmur returned 403. Taylor opened the form with focus inside the dialog and the single eligible assumption preselected; the verifier list offered only Yagmur. Native validation blocked an empty submit. An expiry beyond the disposition, with the `max` attribute removed, displayed the server's field refusal. A valid two-day expiry saved, and the inspector showed Proceed under Assumption with the In effect exception. Repeat approvals kept the older exceptions as Not in effect. Expiry was simulated by setting `expires_on` to yesterday in this disposable database. The inspector then showed Needs Assessment with every approval retained as Expired (AC-RDY-03). Settled axe scans of the exception form and the inspector found zero violations; an earlier scan made before the dialog animation finished had transient contrast hits. The only console error was the deliberate 400 refusal. The API was stopped and `hub_agent_exception` was dropped.

**UNPROVEN:** expiry through the real clock rather than a database edit; changed-assumption (superseded version) display in a browser; manual keyboard/assistive-technology review; deployed authentication; notification of the verifier; complete packet acceptance.

## Promise weeks on the coordination day — 2026-10-01

This implements Jay's FR-RDY-05 decision: a promise week starts on the project's `CoordinationDay`, or Monday when it is unset. Propose accepts only that day. Snapshot and the single-week list also accept a week start that already has a recorded promise or snapshot, so a week recorded before the day changed can still be listed and closed. No rows are re-dated. The list also accepts a `from`..`to` range of recorded week starts, capped at 90 days. The readiness page now loads its window in one request. It shows sections for current coordination weeks and for any other recorded week start in the window, and it computes defaults from the project day. The week-start field is limited to coordination days through native `min`/`step` validation and shows the day as a hint. `ProposeBody` takes an optional `reason`, so a PM can record a correction on a Complete project; the form asks for it only when the project is Complete. The lifecycle sweep now expects success with a reason in the "Complete, PM" case.

**PASS (local):** `dotnet build Hub.slnx` succeeded. `dotnet test --filter "WeeklyCommitment|CoordinationLifecycleSweep|Readiness"` passed 84/84: the new `WeeklyCommitmentWeekTests` 3/3 and `CoordinationLifecycleSweepTests` 63/63. The new tests cover:
- a Wednesday project refusing Monday for propose, snapshot and list;
- a Monday row surviving a change to Wednesday, listed and snapshotted under its recorded start;
- a new Monday proposal refused after that change;
- a 90-day range limit;
- a Complete-project proposal refused without a reason and accepted with one.

`tools/trace_spec.py --check`: 0 not cited. `npm --prefix web run build` and `npm --prefix web run lint` passed, with lint at exit 0.

**UNPROVEN:** browser rehearsal of the changed week picker and the mixed-week sections.

## Submission Gate prerequisite packages — 2026-10-01

This implements Jay's Submission Gate decision (`docs/decisions.md`). A PM or the responsible Discipline Lead links a task or deliverable to a prerequisite submission package in the same project and gives a reason, through `Coordination.Run`. That makes each link retry-safe, version-checked against the target, audited and lifecycle/Read Only guarded. Removing a link needs the link's row version and a reason. The row stays in history with who removed it, when and why. New table `readiness_submission_prerequisite` (migration `ReadinessSubmissionPrerequisite`, additive) has a unique index on active links only. A package is refused when its current manifest lists the target deliverable or the task's deliverable, including through the successor the gate would follow. A package from another project is refused.

Evaluation is live on every readiness read and command, so a package status change takes effect on the next read. The gate passes only when every active link's package is Issued. A Superseded package is followed to the successor whose issue superseded it. Cancelled, Draft, Checking or Ready packages leave the gate Not Ready. With no link, a reasoned Not Applicable stands; otherwise the gate stays unknown (Needs Assessment), and a stored satisfied value is ignored. A link whose current package later lists the output becomes unknown, not Ready. Proceed under Assumption cannot override the gate. The readiness inspector lists links with the linked and current package status, any self-listing warning and removal history. PM/lead users can add or remove links with a reason.

**PASS (local):** `dotnet build Hub.slnx` succeeded. `dotnet test --filter "Readiness|Submission|CoordinationLifecycleSweep|WeeklyCommitment"` passed 96/96. `HandoffsTests` passed 25/25, and the full suite passed 520/520.

The new `ReadinessSubmissionGateTests` (3/3) cover:
- an unlinked gate: unknown at first, Ready with a reasoned Not Applicable, and unknown again after Applies, even with a stored satisfied value;
- a Draft link leaving the work Not Ready;
- a retried link returning the same link, and a duplicate refused with 409;
- API issue making the work Ready; an unissued successor not changing that; issuing the successor marking the original Superseded with the gate following it;
- a cancelled linked package leaving the work Not Ready;
- removal: stale 409, performer 403, then success; a repeat removal refused with 400, history retained;
- performer, other-discipline lead, Read Only and Archived links refused with 403; a stale target refused with 409; a foreign package refused with 400;
- self-listing refused with 400 for a deliverable and for its task;
- a lead allowed to link;
- a later self-listing turning the gate unknown.

A domain test confirms an assumption permission cannot override an unissued or unknown gate. Mutating the Issued rule or the supersession following failed the new tests.

Three existing fixtures that stored Applies/Satisfied = true for an unlinked gate now mark it Not Applicable: `ReadinessApiTests` (two) and `HandoffsTests` (one). The API cannot produce that stored state. `tools/trace_spec.py --check`: 0 not cited. `npm --prefix web run build` and `npm --prefix web run lint` passed.

**UNPROVEN:** browser rehearsal of the prerequisite inspector, and a notice to the work owner when a linked package is issued (not sent; §17 has no readiness entry).
## FR-RDY-02 task start authorisation — 2026-10-01

Jay's 032–033 decision is now enforced in the shared `TaskEndpoints.ApplyTransition`, so it applies to direct `/tasks/{id}/transition` and bulk `transition`. A start is Not Started onto a path through In Progress, including a one-step Complete. If readiness is Not Ready or Needs Assessment, the start needs the starter's `acknowledgeReadiness`, a reason of at least 5 characters and a PM/Discipline Lead authorisation. Work never assessed counts as Needs Assessment, and an evaluator refusal (such as an invalid owner) is treated the same way with its message. The guard runs after the existing review, access, lifecycle and reason guards and reuses `ReadinessEndpoints.EvaluateCurrent` read-only; `Readiness.cs` and `WeeklyCommitments.cs` are unchanged.
- **Refusal:** a 422 `start_authorisation_required` problem names the readiness state and reasons. It lists `needs` (`acknowledgement`, `reason`, `authorisation`) and says whether the caller may authorise.
- **Inline:** a PM or lead starter authorises inline.
- **Recorded:** a PM or lead can record an authorisation in advance (`POST /tasks/{id}/start-authorisations`, retry-safe). `GET /tasks/{id}/start-readiness` shows the current state and the usable authorisation.
- **Binding:** an authorisation is bound to the readiness state and reason codes it acknowledged and is used by exactly one start.

Ready and Proceed under Assumption start normally. Bulk start reports each refusal and starts none of the refused tasks. A refused bulk row also drops any authorisation it would have used or recorded. The `TaskStartAuthorisation` entity and its additive migration `20261001211215_TaskStartAuthorisation` record the authoriser, reason, readiness state, unknown and blocked codes, creation time, starter and start time. A database trigger rejects edits to that content, deletion, and changes after use. `dotnet ef migrations has-pending-model-changes` reported no drift.

The frontend handles every start through `useTaskActions`: the task panel/list status control, board drop including keyboard drag, My Work and the progress offer. On this refusal it opens a readiness dialog showing:
- the readiness state;
- the blocking and unknown checks;
- any usable authorisation;
- an acknowledgement checkbox (focused once loaded);
- a reason field.

A PM/lead starts inline. A performer starts only with a recorded authorisation, and otherwise sees who must authorise. A PM or lead has an Authorise start control on a Not Started task's panel. The bulk UI offers no status transition, so bulk start is API-only. Labels are in the `tasks.startAuth*` block.

**PASS (local slice):**
- **API tests:** the new `TaskStartTests` passed 7/7. They cover the following:
  - never-assessed performer refusal and needs;
  - PM/lead-only authorisation needing acknowledgement and a reason;
  - starter acknowledgement still required;
  - single use after cancel/restore;
  - lead inline authorisation;
  - access, Read Only and review guards answering first;
  - a one-step Complete counting as a start;
  - an other-discipline lead refused;
  - stale-readiness authorisation refused;
  - Ready and Proceed under Assumption starting without authorisation;
  - bulk per-task refusal and lead bulk start;
  - trigger immutability.
- **Full suite:** `dotnet test tests/Hub.Tests` passed 520/520 on the final code. Existing tests were not weakened. `TestData.Move` now retries a `start_authorisation_required` refusal by recording the project PM's authorisation and acknowledging with a reason, as a user would; passing `acknowledgeReadiness` opts out.
- **Frontend:** `npm --prefix web run build` passed; `npm --prefix web run lint` exited 0 with 0 errors and 88 warnings, none in the new file. `git diff --check` passed.
- **`test:coordination`:** passed with installed Chrome, 0 errors and zero violations in 5 axe scans.
- **Fresh database:** `hub_agent_exception` with `Seed__ReviewDemo=true` migrated through the new migration and seeded DEMO-101–103.
- **Browser (Playwright Chrome, port 5091):**
  - Yagmur, a team member, starting an unassessed synthetic Civil task saw Needs Assessment and that no authorisation existed, with no start button; the task stayed Not Started.
  - Jay, the Civil lead, recorded an authorisation from the task panel; the record button stayed disabled until acknowledgement and reason were given.
  - Yagmur then saw Jay's authorisation, acknowledged, gave a reason and started; the task was In Progress with the authorisation used by Yagmur and her reason in the activity log.
  - Taylor, the PM, moved a PM-discipline task to In Progress by keyboard on the board and authorised inline.
  - When readiness became Ready while Jay's dialog was open, the dialog showed the server's 422 refusal and the refreshed Ready state.
  - Ready work then started with no dialog and no authorisation.
  - A keyboard-only authorisation (Tab, Space, typing, Enter) saved.
  - Settled axe WCAG 2.1 A/AA scans of the five dialog states above had zero violations, with one incomplete contrast item each. A separate keyboard-run inspection identified these items as the aria-hidden status glyph and a dialog description whose background overlaps the overlay.
  - The only console errors were the expected 422 refusals.
- **Cleanup:** the API was stopped and `hub_agent_exception` was dropped.

**NOT PASSING (pre-existing):** `test:handoffs` hard-codes the bundled Playwright browser, so it was run with a preload that substitutes installed Chrome. It fails a colour-contrast assertion in the handoff draft dialog. The identical failure occurs on base `00c7f9a` without these changes. That test does not exercise task starts.

**UNPROVEN:**
- A PM/lead authoriser who later loses that role still leaves a usable unused authorisation.
- A bulk start UI does not exist.
- Manual assistive-technology review.
- Deployed authentication.
- Notifications for recorded authorisations (none are sent).
- Complete packet acceptance.

## Task start authorisation follow-ups — 2026-10-01

**Use-time authority.** A recorded authorisation is now usable only while its authoriser is still an active user who can write in the project and is its PM or the task discipline's Lead. This is checked when the authorisation is used, with the shared `Permissions.ManageCoordination` rule applied to the authoriser's current roles and project standing. If the authority has lapsed, the record stays unused in history. The start is refused with a 422 that names the authoriser and why the record can no longer be used, and the problem includes `unusableAuthorisationId`. `GET /tasks/{id}/start-readiness` returns the same record as `unusableAuthorisation`, and the readiness dialog shows it as a warning above the "ask the PM or Discipline Lead" message. A current PM or lead can still start inline or record a new authorisation.

**Notice.** Recording an authorisation in advance (`POST /tasks/{id}/start-authorisations`) now queues `TaskStartAuthorised` for the task's assignee inside the command transaction.
- **Defaults:** in-app on, email off, direct assignment, and included in `ProjectScoped`. The defaults mirror the other packet 032 action notices.
- **Content:** the title names the authoriser, task and readiness state; the body is the reason; the link opens the task panel.
- **Recipients:** the actor is never notified, and a refused or replayed command adds nothing. A starting PM/lead's inline authorisation sends no notice.
- **Authoriser at start:** I did not add a notice telling the authoriser when the performer starts. §17.1 reserves immediate notices for events that need a response or unblock someone, and never notifies people about their own actions. §17.2 has no such event. Under FR-ASG-01/03, PMs and Discipline Leads follow their projects at All activity, so the start and its reason already appear in their Following tab and digest Project updates. FR-MDC-06 asks for one deduplicated event per recipient and actionable item. The start gives the authoriser nothing to act on.

**Review demo seed and documented synthetic flows (finding; seed unchanged).** The seed creates each DEMO project's T0001 directly as In Progress. Each T0002 is Not Started and never assessed, and is assigned to that project's PM: Taylor for DEMO-101, Jay for DEMO-102, Yagmur for DEMO-103. The PM starts it with an inline acknowledgement and reason. None of the documented flows starts a task:
- **Handoff H001 (draft → submit → accept → incorporate):** only needs a non-cancelled target task in the receiving discipline.
- **P01 review and independent approval:** acts on deliverable revisions.
- **P02 publication → change assessment acknowledge/close refusal:** neither step changes task status.
- **Submission readiness/issue:** gates on handoffs, assessments, inputs and reviews; issuing with open tasks only asks for confirmation.
- **Weekly signing:** needs Ready readiness, not a start.

A start becomes necessary only if a reviewer carries a change assessment through Update Required → Resolved → Close. That path needs the assessment owner's correction task to be Complete (`ChangeRules.CorrectionReady`). A new correction task is never assessed. Its PM or lead owner authorises inline; any other owner is told to ask a PM or lead, who has "Authorise start" on the task panel, and that owner is now notified when the authorisation is recorded. Instrumented diagnostic run (instrumentation removed before commit): of the coordination test classes, only `ReviewChangeTests.AC_CHG_05` used the authorised start path; the handoff, submission, readiness and review-seed tests never started a task.

**Test helper.** With the authorisation API now notifying the performer, `TestData.Move`'s authorised retry briefly broke the existing `CollaborationTests.Assignment_notifies_in_app_and_by_email_but_never_the_actor`. Recording the PM's authorisation through the API notified Alex, and that test expects only TaskAssigned. The retry now writes the project PM's authorisation for the exact reported readiness directly to the database. The start still goes through the server guard, including the use-time authority check. The API command, its checks and its notice are covered only in `TaskStartTests`. No existing test was edited.

**PASS (local slice):**
- **Focused:** `TaskStartTests` passed 9/9. The 2 new tests cover:
  - a lead role moved away or a deactivated authoriser making the record unusable, with the refusal message and the unusable view;
  - reactivation making the same record usable again;
  - the performer's single notice and project-scoped email when email is enabled;
  - no notice for the actor or for refused (403/400) and replayed commands;
  - no notice for an inline lead start;
  - nothing for a performer removed from a restricted project.
- **Mutation check:** dropping the event from `ProjectScoped` made the notice test fail.
- **Full suite:** `dotnet test tests/Hub.Tests` passed 534/534 on the final code.
- **Other checks:**
  - `python3 tools/trace_spec.py --check` reported 612 IDs and 236 sections, with 0 not cited.
  - `npm --prefix web run build` passed.
  - `npm --prefix web run lint` exited 0 with 0 errors and 88 warnings.
  - `git diff --check` passed.
- **Browser:** a fresh `hub_agent_exception` with `Seed__ReviewDemo=true` applied all 26 migrations. Playwright Chrome on port 5091 ran this flow on a synthetic Civil task for Yagmur:
  - Jay recorded an authorisation from the task panel.
  - Yagmur's notification centre showed "Jay authorised starting DEMO-101-T0003 … while it is Needs Assessment" with the reason. Opening it went to the task panel, where the readiness dialog showed Jay's authorisation.
  - After Taylor moved the Civil lead to herself, Yagmur's dialog showed the "can no longer be used" warning with no start control. The API reported `unusableAuthorisation` by Jay, and the task stayed Not Started.
  - With Jay restored as lead, Yagmur started under the same authorisation.
  - Yagmur, PM of DEMO-103, started the seeded never-assessed DEMO-103-T0002 inline.
  - The database showed one notice, for Yagmur only, and both authorisations used.
  - Settled axe WCAG 2.1 A/AA scans had zero violations (incomplete contrast items only).
  - The only console errors were the expected 422 refusals.
- **Cleanup:** the API was stopped and `hub_agent_exception` was dropped. `hub_review_local` was not used.

**UNPROVEN:**
- The persistent `hub_review_local` data itself was not inspected; the findings come from the seed code, a fresh seed and the flow code and tests.
- No UI for bulk start.
- Manual assistive-technology review.
- Deployed authentication.
- Email delivery of the new event when a user enables it (the queueing was tested; the delivery job was not run).
- Complete packet acceptance.


## Constraint links and readable keys — 2026-10-01

FR-RDY-03 asks for links to existing decisions, issues and handoffs, and §10.8 says an "existing issue/decision may be linked". A constraint can now record one optional typed link (`linkedType` plus `linkedId`, either Decision, Issue or Handoff) to a same-project, non-deleted record. The link is checked inside the same retry-safe command, and a database check enforces both-or-neither and the type list. The inspector form offers the project's records from a new read endpoint, `readiness/link-options` (project read access, up to 500 per type). The constraint list shows the linked record's key, title and current status, with navigation to it; a deleted or missing record shows as unavailable. The linked record's outcome is displayed only. The spec says the affected owner verifies removal (AC-RDY-02), so nothing proposes or verifies automatically. Source evidence stays required.

§10.8 also requires readable per-project keys: constraints now take `CT` keys and promises `WC` keys from project counters inside the command transaction. Migration `ReadinessConstraintLinksAndKeys` is additive. It numbers existing rows per project in creation order (padded to three digits, never truncated) and starts each counter after the highest number.

**PASS (local):** `dotnet build Hub.slnx` succeeded. `dotnet test --filter "Readiness|WeeklyCommitment|CoordinationLifecycleSweep"` passed 90/90, including the new `ReadinessConstraintLinkTests` 2/2. The new tests cover:
- refused links (foreign, missing id, unknown type, deleted issue);
- a retry neither duplicating nor taking a key, giving keys CT001/CT002;
- Read Only users seeing the linked decision;
- a decided decision showing as Decided while the constraint stays Open;
- link options limited to the project, with an unknown type refused (400) and a restricted project refused for Read Only (404);
- promise keys WC001/WC002 with a refused proposal taking no key.

**Backfill on my own database:** `hub_agent_week` (API on 127.0.0.1:5093) assigned DEMO-101-CT001/CT002 and WC001 at runtime. I then ran `dotnet ef database update` back to `20261001212053_ReadinessSubmissionPrerequisite`: the columns were dropped and the rows kept. Re-applying restored the same keys in creation order, set the counters to 3 and 2 (1 for projects without rows), and the commitment history guard allowed the key update on a Committed row (inside a transaction that was rolled back). `npm --prefix web run build` and `npm --prefix web run lint` passed.

## Readiness exports — 2026-10-01

FR-MDC-06 export for the readiness page. All three exports produce CSV or XLSX through the existing `ExportFile` helper, which also logs the export:
- `readiness/window/export?from&to&list=constraints|ready` uses the same window query as the page, now shared as `WindowData`. Constraints are listed with key, work, category, description, owners by name, needed-by, state, linked record (with its status) and source. Ready outputs are listed with key, work, type, owner, discipline, due date, output, criteria and state.
- `weekly-commitments/export?from&to` lists the promises due in the window that the page shows: key, work, performer, recorded week start, target date, output, criteria, state, readiness at commitment, snapshot time and evidence.

Each export is scoped to the project. A restricted project returns 404 to non-members. The window span stays within 84 days, and an unknown list returns 400. The page has an export menu on the constraint and ready-output sections and an "Export promises" menu in the header. `ExportMenu` gained an optional label. Ready outputs are now ordered by due date and key.

**PASS (local):** `dotnet build Hub.slnx` succeeded. `ReadinessExportTests` passed 1/1: the ready export matched the window's ready output, the constraint export matched the window keys (linked decision label, owner name, cancelled constraint excluded), the promise export kept the in-window promise and excluded the later one, XLSX returned 200, an over-long span and an unknown list returned 400, and a restricted project returned 404 to Read Only. `dotnet test --filter "Readiness|WeeklyCommitment|CoordinationLifecycleSweep|DisciplineCoordination"` passed 91/91. `npm --prefix web run build` and `npm --prefix web run lint` passed.

## Readiness search — 2026-10-01

Global search now has "Readiness constraints" and "Weekly promises" groups, matching by CT/WC key, constraint description or promise output. They use the same visible-project set and ranking as handoffs, reviews and changes, so a restricted project stays hidden from non-members. An exact CT or WC key opens the record: results link to `/projects/{number}/readiness?panel=WorkConstraint:{id}`, which opens the constraint's work in the inspector through a new project-scoped `readiness/constraints/{id}` lookup, or `?panel=OutputCommitment:{id}`, which opens the promise. The backend and SPA key patterns now accept CT and WC.

**PASS (local):** `dotnet build Hub.slnx` succeeded. `ReadinessSearchTests` passed 1/1, and with `SearchReportsTests|NotificationSearchTests` 12/12 passed. The test covers:
- text hits for one constraint and one promise, with key, project and status;
- exact lower-case CT and WC keys returning the record type and id;
- the lookup locating the constraint's work;
- a non-member of the restricted project getting zero counts, no exact match and 404, while a member still finds the record.

`npm --prefix web run build` and `npm --prefix web run lint` passed (88 warnings before and after; none new). `tools/trace_spec.py --check`: 0 not cited.

## Browser rehearsal: weeks, gate, links, export and search — 2026-10-01

Ran in real Chrome (Playwright 1.61, `channel: 'chrome'`, Development sign-in) against my own database `hub_agent_week`, with the API at 127.0.0.1:5093 and synthetic review demo data. The database was dropped afterwards. DEMO-101's coordination day was set to Wednesday.

**PASS:**
- **Week picker:** the page showed Wednesday weeks (2026-09-30, 10-07, 10-14) with the earlier Monday promise in its own recorded "Week of 2026-10-05" section. The week-start field had `min` 2026-09-30, `step` 7 and the hint "Weeks start on Wednesday". A Thursday was blocked by native validation ("nearest valid values are 2026-10-07 and 2026-10-14"). A Wednesday proposal saved into "Week of 2026-10-07".
- **Submission Gate:** as the Civil lead, linking a package that lists the task's own deliverable was refused with the specific message. Linking the geotechnical package made the task Not Ready (Draft). After the package was started and issued through the API, reopening the inspector showed Ready. Removing the link with a reason gave Needs Assessment and kept the removal history (who, when, why).
- **Constraint link:** a constraint linked to DEMO-101-DEC01 showed CT003, the linked decision and its Pending status. The link opened the decision panel.
- **Export:** the promise, constraint and ready-output downloads matched the page: both recorded weeks, CT003 with "DEC01 … (Pending)", and no ready rows while CT003 is open.
- **Search:** "storm outfall" listed CT003 under Readiness constraints. The hit opened the readiness inspector on DEMO-101-T0001, and closing it cleared the link. Entering "demo-101-wc002" in the top search opened that promise.
- **Accessibility:** axe (WCAG 2.0/2.1 A/AA) on the settled propose, link-package, remove-link, record-constraint, inspector, promise and search views found 0 violations. Remaining "incomplete" items were the Radix focus guards and contrast on aria-hidden status glyphs. One contrast violation appeared only while the global "Saved" toast was on screen; it was not present once the toast had cleared.
- **Console:** no JavaScript errors from the readiness views, apart from the expected 400 for the refused link.

**Fixed during the rehearsal:**
- Timestamps in the inspector, constraint verification and promise history repeated the date (`fmtDate` followed by `fmtTime`, which already includes the date).
- Promise rows now show their WC key.

**Outside this packet:** the Decisions page requested `/api/v1/projects//external-parties` (404) when opened from a deep link.

**Final checks:**
- `dotnet build Hub.slnx` succeeded.
- The full suite passed 525/525.
- `tools/trace_spec.py --check`: 0 not cited.
- `npm --prefix web run build` and `npm --prefix web run lint` passed (exit 0, 88 warnings, none new), and `git diff --check` passed.

**UNPROVEN:** manual assistive-technology review, deployed authentication, and keyboard-only operation of the new forms (they use native controls and the existing dialog primitives).


## 2026-10-01 Codex recovery checkpoint

Constraint/promise keys, links, export/search and notification text were integrated. Live Chrome against the isolated populated local review copy showed the constraint key, retained Verified Removed/evidence, opened the prerequisite inspector, returned focus on Escape and withheld Sign promise from the chair. Migration rehearsal preserved 3 projects, 7 tasks, 1 constraint and 1 promise with 27 migrations. Applicability now recomputes live checks using the organisation date; combined checks are recorded separately. Hosted performer/supervisor acceptance remains UNPROVEN.

### Final local lifecycle rehearsal

The isolated live Chrome/API rehearsal completed performer signatures with Ready-at-signature, snapshot freeze and
Met/Not Met/Withdrawn closure. Reload retained history and Met evidence; withdrawal kept the original denominator at
3. The applicability setup was explicitly synthetic and carried reasons/evidence, so it proves workflow behavior only.
The complete integrated suite then passed 568/568. Real hosted password, populated company and engineering acceptance
remain UNPROVEN; see the current recovery checkpoint.

## Finding L6: invalid owner or discipline no longer breaks readiness reads — 2026-10-02

Reproduced first: an assessed item whose owner was removed from the project, whose owner was deactivated, or whose discipline was deactivated made the readiness detail return 400, for the PM and Admin alike (`ownerId` or `projectDisciplineId` validation from `Coordination.Target`). The same 400 hit every list that evaluates the item.

**Fix:** the shared evaluator `EvaluateCurrent` and the per-work constraint and prerequisite reads now load the work through a read-only `ReadTarget`. It keeps the cancelled and missing-work refusal but no longer validates the owner or discipline. The Production Owner check now reports the condition as a known unmet check (Not Ready, which Proceed under Assumption cannot override), with one of these reasons:
- "No production owner is assigned to this work."
- "The production owner is no longer an active project member."
- "The work's discipline is no longer active in this project."

Commands still refuse invalid work through `Coordination.Target`, which was not changed. Task start now receives an evaluated Not Ready, with "Production Owner" blocked, instead of an unevaluated note, and still requires authorisation.

**PASS (local):** `dotnet build Hub.slnx` succeeded. The new `ReadinessInvalidOwnerTests` (3/3) cover owner removed, owner deactivated (an isolated user) and discipline deactivated. For the PM and an Admin each test checks:
- the readiness detail returns 200 with Not Ready, Production Owner blocked and the exact reason;
- the window returns 200 and no longer lists the item as ready;
- the project discipline-coordination view returns 200 with a Not Ready startability row, and the all-projects view returns 200;
- the per-work constraint and prerequisite lists, both window exports, the weekly-commitment range list and its export all return 200.

Task start readiness returned Not Ready, needing authorisation, with no note. Creating a constraint on the work was still refused with 400. Against the unfixed evaluator the same three tests failed with the reproduced 400s. The full suite passed 572/572, and `tools/trace_spec.py --check` reported 0 not cited. No web files changed.
