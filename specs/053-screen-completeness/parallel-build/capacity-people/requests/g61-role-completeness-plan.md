# G61 — complete the role explainer without inventing a second permission engine

2026-11-02. **READY proposed plan / HOLD. Documentation only.** One authored file under Root's direct G61 documentation loan. Checkout: `/Users/jaypatel/.codex/worktrees/ef94/PM-Tool/.build-052-053`, branch `codex/build-052-053`. This document authorizes no source, test, manifest, generated, specification-status, flag or acceptance changes. No compiler, coverage tool, provider, browser, job or application code was executed for this plan. Proposed checks below are **UNRUN**.

## Outcome and current evidence

FR/AC-SCR-83 requires a plain-language explanation of every system/project role that agrees with the actual §8 rules. The smallest honest completion is a row/operation adapter over canonical decisions, truthful conditional wording, and a complete evidence ledger for the 83 matrix rows. Registering every `Permissions` method is necessary but does not prove the API's source, scope, feature, lifecycle or transaction gates.

Read for this plan: `work-delivery/requests/g59-permission-equivalence-audit.md`; current `src/Hub.Domain/PermissionExplanationCatalog.cs`; `src/Hub.Api/Features/AdminRoleExplanations.cs`; `web/src/pages/admin/admin-role-explanations.tsx`; §8 and FR/AC-SCR-83; `tools/check_permission_explainer_coverage.py`, its tests, and `tests/Hub.Tests/Snapshots/permission-explainer-bindings.json`; the actual Access/PeopleScope/Coordination, task-facts, Availability, Calendar and report seams; existing explanation tests; C's G61 external-review contract. Paths in this document are relative to the checkout unless stated otherwise.

Observed now, without executing the checker:

- The four manifest sections contain 22 system rows, 33 project rows, 18 coordination rows and 10 planning rows: **83**. Every row binding and each of the **20** registered guard files is still `Pending`. Most system rows bind only Auth/Admin; the coordination rows use broad repeated file lists. Neither identifies the relevant command branch by itself.
- The manifest names four remaining gaps: compound/transport, G57 new features, G58 task contribution, and G59 coordination/notices. The resolved template-policy gap has already been removed there. The catalog currently exposes only the coarser compound/transport gap; the API hard-codes `CoverageComplete=false`. Keep those incompleteness boundaries until the reviewed ledger closes. An older checkpoint's “108 pending” is historical, not a checker result from this pass.
- The checker validates section fingerprints, method signatures, registration names, row/guard presence and guard-file hashes. It accepts the literal `Evaluated` status without requiring behavioral evidence. It does not validate catalog arguments, same-signature permission-body changes, per-operation guard semantics or actual HTTP outcomes. Its five existing defect-seeded tests do not establish those properties.
- The UI still renders `Allowed / Not allowed` from one predicate's boolean, although source/context requirements can reverse that result. Scenario facts are currently pooled above the table rather than attached to the affected row.
- G59's Availability divergence is already repaired: current `Availability.Manage` calls `Permissions.ActOnStaff`, retaining the effective Read Only veto and the existing unrelated-person reason. Do not repeat that implementation or present its old source prediction as a current bypass.
- B has reported its four-file §8 template lane READY/HOLD. That handoff and Root's catalog cleanup are source authoring, not verified template or full FR83 evidence. C's G61 document records the settled **BLOCK unknown historical issue authors** choice; do not resurrect PM attestation from stale decision text. Its external-review implementation remains prospective.

## One adapter, with explicit limits on what was evaluated

Keep the explainer Admin-only, `people_screens`-gated, read-only and bounded to closed role/scenario/operation choices. It must not impersonate a user, look up a caller-supplied real person/project, mutate synthetic records, or expose synthetic GUIDs/private facts. System role, actual project membership, lead discipline, ownership and named-signature responsibility must remain separate facts; “Standard” must not imply Team Member without saying so.

1. Keep the existing typed `Permissions` bindings as **factors**, including the genuine `Allow` reason and correction-reason tuple. Attach a matrix row key and concrete operation key to the row examples. A broad matrix row may need several operations; do not pretend one method covers create/edit/delete/export or every transition target.
2. Add an explicit result classification to the existing explanation DTO/catalog seam, for example `EligibleForExample`, `NotEligibleForExample`, `Conditional`, `NotApplicable`, plus row-specific conditions with `Met / NotMet / NotEvaluated`. These are bounded CLR/display results, not a public policy expression language. Retain the old boolean as a factor if compatibility requires it; the UI must not promote it to effective permission.
3. Compose alternatives through the **same actual canonical helper** used by the operation. For example, `Permissions.ViewWorkload=false` is not final denial when the current `PeopleScope` unit-leader branch can allow a scoped read. Do not create a generic “any false denies” aggregator that changes that OR into an AND. Use `Access.Project`/`VisibleProjects`, `PeopleScope` and the operation's existing query/flag/state helper when they can actually evaluate the supplied facts.
4. When a source lookup, real current relationship, feature, required reason, version, provenance or workflow gate cannot be evaluated in a synthetic example, show that exact requirement on that row and return **Conditional**. Do not invent `true`, infer a real target from a label, or run a copied SQL/boolean rule over fake records. Plain-language role eligibility can be complete while a live command is still conditional; `CoverageComplete` describes reviewed explanation coverage, never permission to execute a particular command.
5. If a necessary decision is private or inline, Root may loan a **small extraction in that canonical owner**, used by enforcement and explanation. Extract the current predicate/fact calculation without changing grants, order or side effects; do not copy it into the catalog. No global authorization refactor, new role engine, schema or policy interpreter. If no safe shared seam is approved, retain `NotEvaluated` and explicit conditional language; behavioral witnesses still exercise the real command.
6. Labels name stable actions/facts. Grants, denials and reasons come from canonical results, not a second prose role table. A canonical rule change must change the corresponding result/reason automatically. A §8-only edit must fail the row/fingerprint/equivalence gate until enforcement and explanation are reconciled; parsing Markdown into new runtime authority is not an acceptable interpretation of “no separate edit.”

### First misleading examples to repair

| Case | Minimum truthful behavior and witness |
|---|---|
| Legacy `RecordTimeAway` | Separate role eligibility from the actual `/planning/time-away` command's unconditional `422 hr_reference_required`. Explain HR-referenced manual/import/reconciliation operations separately with their real source, scope and version requirements. Do not re-enable arbitrary overrides to make an Allowed label true. |
| Creator window / assignment at creation | Replace the impossible current “In Progress + creator window open” example. Derive the condition from the canonical task fact calculation: Not Started and no other person's comment, plus the operation's creation-only arguments. A valid creator witness must lose eligibility after the actual status/comment change. Reuse `TaskEndpoints.Facts`/`PathAllowed`; any fact extraction belongs to `Tasks.cs` and its canonical domain seam, after an exact Root loan. |
| Change status / archive / unarchive | Name source status, requested target and required reason/version. Archived→On Hold is not the Admin unarchive question; test Archived→Complete separately and retain `Workflow.ProjectAllowed`. Rebaseline/return-to-baseline and cancel/reopen likewise need their real operations. |
| Planning correction / unit reader | Normal vs data-correction mode, required reason and access log are separate. Reuse current Planning visibility and management predicates. Unit/indirect people scope is read-only; it must not produce `ActOnStaff` or planner-write authority. Show the current `org_structure`/scope facts. |
| Private calendar / task-hour entries | PM/Admin role eligibility cannot expose another owner's private event: the actual Calendar source reader returns404. Time ownership, current source project, correction reason, nested/source versions and day-cap remain separate conditions. |
| Coordination signatures / readiness | Manager/coordinator authority is not a named reviewer, receiving owner, verifier or performer signature. Current round, independence, source provenance, expiry, manifest/readiness and replay authority remain explicit. A role factor must not advertise “ready to issue.” |
| Reports / saved views / own preferences | Use the selected report's own definition, actual visible project/person/unit scope and flags. A saved/shared container grants no data access. Own preferences are an explicit personal-state exception for active Read Only users; do not apply a blanket business-write veto to this §8 row. Include G60 own notice preferences and grouped-source privacy where relevant. |

## Bounded author lanes and ordering

These are proposed future loans, **not current write authority**. One writer per file. Freeze shared DTO/operation keys before parallel test/UI authorship; keep source/test execution with Root after all writers HOLD.

| Lane | Exact owned files / sequence |
|---|---|
| **B current template lane** | `src/Hub.Domain/Permissions.cs`; `tests/Hub.Tests/Domain/PermissionMatrixTests.cs`; `tests/Hub.Tests/Domain/PermissionExplanationTests.cs`; `tests/Hub.Tests/Api/TemplatesTests.cs`. Only the settled ManageTemplates policy/fixtures/explanation alignment. No other lane edits these until B returns HOLD; existing Admin + effective ReadOnly precedence stays canonical. |
| **D explanation adapter, after B HOLD** | `src/Hub.Domain/PermissionExplanationCatalog.cs`; `src/Hub.Api/Features/AdminRoleExplanations.cs`; `tests/Hub.Tests/Api/AdminRoleExplanationTests.cs`; then a Root-approved reopening of `tests/Hub.Tests/Domain/PermissionExplanationTests.cs`. Own attainable scenarios, row/operation bindings, result classification, row-specific requirements and bounded/read-only contract tests. No authorization changes. |
| **A presentation, after DTO freeze** | `web/src/pages/admin/admin-role-explanations.tsx`; `web/src/i18n/en.ts`; `web/tests/admin-screen.cjs`; `web/tests/admin-screen.fixture.cjs`. Show per-row facts/requirements and conditional states, retain relation Yes/No, remove unqualified factor-based Allowed/Not allowed. No permission logic in React. Test loading/error/stale-response/selection/accessibility at1440/375 with the actual adapter response shape. |
| **B system/project witnesses, after current lane HOLD** | New `tests/Hub.Tests/Api/PermissionExplainerSystemRowsTests.cs` and `PermissionExplainerWorkRowsTests.cs`. Own the S/W ledger rows below. Reuse `HubFactory`, `TestData` and existing matrix/family suites; add only missing real-command witnesses. No shared fixture framework or production-source edits. |
| **C coordination witnesses, after external contract freeze** | New `tests/Hub.Tests/Api/PermissionExplainerCoordinationRowsTests.cs`. Own C01–C18 and reuse current Handoff/ReviewChange/Submission/DesignBasis/Readiness/WeeklyCommitment/LocationIssue cases. If C is building external review, its changing Review/issuance guards remain pending until that lane is held and integrated; do not bless a source that is still being authored. |
| **D people/planning witnesses, after adapter HOLD** | New `tests/Hub.Tests/Api/PermissionExplainerPeopleRowsTests.cs`. Own P01–P10, with B's system tests referencing the same canonical people/HR behavior for S05–S15. Do not duplicate an existing passing command case merely to change its class name; ledger references to current-source evidence are allowed. |
| **Root integration and coverage** | `tools/check_permission_explainer_coverage.py`; `tools/test_permission_explainer_coverage.py`; `tests/Hub.Tests/Snapshots/permission-explainer-bindings.json`; `src/Hub.Api/Text.cs`; generated API/registry files under established tools; packet verification/evidence and any approved §8 wording clarification. Root alone updates coverage/gaps after review/evidence, runs generation/checks and decides whether S49 can advance. Root owns all canonical-owner extractions as separately named exact loans, not speculative edits by a witness lane. |

Candidate extraction owners, only if genuinely required: `src/Hub.Api/Infrastructure/Access.cs`, `PeopleScope.cs`, `Coordination.cs`; `src/Hub.Api/Features/Tasks.cs`, `Planning.cs`, `Calendar.cs`, `Reports.cs`, `Views.cs`, `Availability.cs`. `src/Hub.Domain/Permissions.cs` must remain B's until its handoff. Other lifecycle owners in the ledger are read/reused by default; Root names a new bounded loan if a specific inline gate needs extraction. No explanation work changes their policy to fit the examples.

## All 83 rows: operation and evidence ledger

S/W/C/P numbers below are local aliases in the existing manifest section order. The authoritative key remains the **exact existing `section:Action` string**; do not rename the matrix or replace its keys with these aliases. Each row remains Pending until every listed compound suboperation is mapped, its applicable role/ownership/scope alternatives are covered, and its material refusal conditions have current-source witnesses. This table is a work inventory, not a copied grant matrix or a completed evidence record.

Boundary abbreviations:

- **AUTH:** `Infrastructure/Auth.cs`, `Access.cs`, `Http.cs`, canonical `Permissions.cs`; actual identity/active state, scoped404, flag and version behavior. Apply to every applicable row; preserve Admin's effective ReadOnly exception and personal-state exceptions.
- **SYS:** `Features/Admin.cs`, `Projects.cs`, `Portfolio.cs`, `Activity.cs`, `Templates.cs`, `Me.cs` and the actual per-row registry/read boundary.
- **PEOPLE:** `Infrastructure/PeopleScope.cs`; `Features/Workload.cs`, `MyWork.cs` (staff routes), `Availability.cs`, `PersonCapacity.cs`, `Planning.cs`, `Allocations.cs`, `WeeklyPlanning.cs`, `Extras.cs`, `Team.cs` as applicable. Read widening is never a staff-write grant.
- **WORK:** `Features/Projects.cs`, `Team.cs`, `Milestones.cs`, `Deliverables.cs`, `Tasks.cs`, `Dependencies.cs`, `Comments.cs`, `DocumentLinks.cs`, `Decisions.cs`, `Registers.cs`, `Meetings.cs`, `Dashboard.cs`; canonical `Workflow.cs` and task facts/path. Include actual source ownership and current eligible people.
- **COORD:** `Infrastructure/Coordination.cs` plus the named feature and its domain rules, `SubmissionReadiness.cs`, readiness/context/provenance dependencies and actual export readers. No manager-as-signature shortcut.
- **READ:** `Features/Calendar.cs`, `CalendarScreen.cs`, `Time.cs`, `Reports.cs`, `Views.cs`, `ListExports.cs`, `Workspace.cs` (`Scope`), `Notifications.cs`, `NoticePreferences.cs`; `Infrastructure/UserPreferences.cs`, `NotificationDelivery.cs` and relevant scoped readers. Do not treat data projection, saved-view ownership, recipient identity or feature gating as a generic `CanView` grant.

### §8.5.1 — 22 system rows (B witnesses; D supplies people scope cross-references)

| Row | Matrix action | Concrete boundary / minimum discriminating witnesses |
|---|---|---|
| S01 | Sign in and use the application | AUTH: genuine active sign-in vs absent/inactive identity; effective role resolution, not Admin endpoint access. |
| S02 | View project list and any project | AUTH/SYS: `Access.Project` plus list query; open nonmember, restricted member/outsider and removed membership; same restricted404 population. |
| S03 | Create a project | SYS `Projects`: real create, effective ReadOnly refusal and resulting creator/PM membership; no partial sequence/team/audit on denial. |
| S04 | View Portfolio Dashboard | SYS `Portfolio`: current role, own/default vs expanded filter, permitted-project counts; no hidden detail/count leakage. |
| S05 | View Weekly Resource Planner | PEOPLE `Workload/Planning/Allocations`: published/confirmed sources, actual direct/self/managed-project people; unit flag/read expansion separate from writes. |
| S06 | View Private Draft allocations | PEOPLE: creator vs other manager; Admin correction mode with reason/log; exclude others' drafts from totals/search/export. |
| S07 | Create/edit/delete manager allocations | PEOPLE: each operation and current owner/direct-report authority, not just `CreatePlanningEntry`; preserve person/project/versions. |
| S08 | Publish/confirm/unpublish manager allocation | PEOPLE: each legal visibility/confidence transition; actual creator/manager or reasoned Admin correction; no former-supervisor grant. |
| S09 | Create/edit/delete own employee allocations | PEOPLE: actual self source and ownership; stale/different target, effective ReadOnly; do not confuse an own manager entry with self-entered work. |
| S10 | Record/correct manual HR-approved leave | PEOPLE `Availability.Manage/ActOnStaff`: real HR reference/manual provenance, direct/unrelated target, effective ReadOnly and imported read-only source; reconcile correction separately. |
| S11 | Preview/apply HR leave CSV; office holidays | PEOPLE/SYS: preview, apply and holiday writer separately; Admin scope, validated source/version and atomic late-row refusal; preview is not apply authority. |
| S12 | View Vacation & Coverage | PEOPLE `Availability`: its actual all/team/project-member availability scope, masked HR/source details and independently protected project coverage; avoid reusing Workload's narrower people list. |
| S13 | Set weekly capacity and working days | PEOPLE: current capacity writer plus dated schedule writer, actual `ActOnStaff` and versions; Admin+stored ReadOnly vs effective ReadOnly Supervisor. |
| S14 | Reassign tasks across projects for staff | PEOPLE/WORK `Extras`: current direct reports, each current source/target/project, actual reassignment right, atomic refusal; no new restricted-project access. |
| S15 | Add/remove supervised staff as Team Member | PEOPLE/WORK `Team`: direct-report relation, target access/active state and TeamMember-only limitation; removal as well as add; no supervisor role escalation. |
| S16 | Manage users/roles/offices/disciplines/clients/types/phases | SYS `Admin`: bind each named child handler, including role mutation and reference lifecycles; valid non-Admin refusal must not hide behind invalid input. |
| S17 | Manage templates | B's current `ManageTemplates`/real template commands and `/me`: SystemPM without editor, editor non-PM, project-PM-only, effective ReadOnly combinations; status remains pending until executed/reviewed. |
| S18 | Manage thresholds/notification defaults | SYS `Admin`: actual setting writes and own-vs-organisation distinction; validate Admin transport and atomic invalid input. |
| S19 | Unarchive project | WORK `Projects`: actual Archived→Complete Admin branch, required reason/version; do not reuse fixed On Hold target. |
| S20 | View all activity history | SYS/READ `Activity` and source visibility: permitted project history vs global Admin audit, Time/private Calendar projections, list/export consistency; `Administer` alone is not this row. |
| S21 | Export reports | READ `Reports.Prepare/DemandReport/Ctx`: each relevant report definition, source/role/unit/flag filters and actual export; unresolved “own projects” population below stays explicit. |
| S22 | Own notification preferences/follows/saved views | READ: actual caller ownership; active ReadOnly personal settings remain permitted; foreign owner refusal, sharing rules and stored-on/flag-off G57–G60 behavior. |

### §8.5.2 — 33 project rows (B witnesses)

| Row | Matrix action | Concrete boundary / minimum discriminating witnesses |
|---|---|---|
| W01 | View project/dashboard/registers | AUTH/WORK: actual source/project reader and list/count/export parity; role membership alone never supplies a hidden source. |
| W02 | Edit project information | WORK `Projects`: valid PM edit, non-PM/effective ReadOnly, lifecycle/reason/version; no write on denial. |
| W03 | Change project status | WORK `Projects/Workflow.ProjectAllowed`: separate source/target paths, cancellation/Complete reason and version; no generic On Hold-only answer. |
| W04 | Archive project | WORK: Complete→Archived only under current gate; Active→Archived and nonmanager controls. |
| W05 | Manage team/roles | WORK `Team`: managerial role edits vs narrow Supervisor staffing, current project/person and immutable old signatures; no proxy permission. |
| W06 | Disciplines/Discipline Lead | WORK `Team/Projects`: add/remove/lead assignment separately, active same-project references and manager authority. |
| W07 | Health override | WORK `Projects`: set and clear, actual PM gate, reason/expiry/version; no generic task-edit grant. |
| W08 | Milestone CRUD/date | WORK `Milestones`: create/edit/delete/date/cascade operations, current children and versions; not a generic DL milestone grant. |
| W09 | Rebaseline/return to original dates | WORK `Milestones/Deliverables`: both source kinds and directions, actual rebaseline reason/authority and retained baseline/version. |
| W10 | Mark milestone Complete | WORK `Milestones`: real completion action and its source/readiness conditions; management factor is not completion readiness. |
| W11 | Create deliverable | WORK `Deliverables`: same vs other discipline, valid owner/reviewer/source and current project state. |
| W12 | Edit deliverable | WORK: actual owner vs unrelated member, PM/DL own scope, dates/owner changes and current references/version. |
| W13 | Deliverable status | WORK/Workflow: owner production paths vs named reviewer and PM/DL issue/accept paths; separate actual source/target and readiness gates. |
| W14 | Delete/cancel deliverable | WORK: both commands, completed-task/source links and actual scope/version restrictions; do not omit the downstream delete guard. |
| W15 | Create task | WORK `Tasks`: own discipline vs other, current project/team/source and effective ReadOnly; actual create request. |
| W16 | Calendar create/edit/cancel | READ `Calendar.Readable/Visible/CanEdit`: each operation, private-other404, own/project event, cancelled source and current version; Admin is not impersonation. |
| W17 | View task-hour entries | READ `Time`: own/PM/DL/supervised scope, actual source visibility and aggregate/export projection; no private note leak. |
| W18 | Add/edit/delete task-hour entries | READ `Time`: create/own edit/delete and PM correction reason separately, source/nested versions and day-cap transaction; no task-edit right inferred. |
| W19 | Edit task fields | WORK `Tasks.Facts`: actual assignee/collaborator/valid creator window; lose creator permission after another person's comment or status change. |
| W20 | Assign/reassign task | WORK `Tasks`: creation-only creator argument vs later reassignment, actual current eligible target and dependency/project locks. |
| W21 | Task status | WORK `Tasks.PathAllowed/Workflow`: every step of actual checkbox/path and named reviewer transitions; include current contribution/seen-related guards only where reachable. |
| W22 | Set task reviewer | WORK: creator-at-creation vs later manager/DL, current eligible reviewer and self-review setting; no synthetic “at creation” on In Progress task. |
| W23 | Add/remove task dependency | WORK `Dependencies`: both endpoints visible/same project, either-end DL vs own successor, removal and cyclic/invalid path; include deliverable dependencies only through their separate binding. |
| W24 | Set/clear manual block | WORK `Tasks`: both actions, actual owner/management and source/version/reason; refusal before mutation. |
| W25 | Change due date | WORK: assignee vs collaborator, creator/manager cases, actual `DueChangeNeedsReason`, notification/audit/version; no generic edit answer. |
| W26 | Delete task | WORK: current Not Started/creator/dependencies and parent/link constraints; scope/right is not deletability. |
| W27 | Cancel task | WORK/Workflow: actual cancellation command/path, PM/DL and required reason/current source; no owner cancellation inferred. |
| W28 | Reopen completed task | WORK/Workflow: actual completed source and named reviewer/manager branch, versions and legal target. |
| W29 | Comment/@mention/document link | WORK `Comments/DocumentLinks`: each operation, Viewer-comments setting and actual target visibility; user mention/link picker eligibility is separate. |
| W30 | Edit/delete own comment | WORK `Comments`: each action's own author/time-window/moderation/project rules; preserve real boundary-time witness. |
| W31 | Decision/risk/issue/meeting/action CRUD | WORK `Decisions/Registers/Meetings`: split raise/edit/cancel and every named record family; owner scope vs manager, current source and field/state guards. |
| W32 | Record decision outcome | WORK `Decisions`: actual pending/allowed outcome, owner or manager and reason/version; mere ability to raise is insufficient. |
| W33 | Weekly Coordination reviewed timestamp | WORK `Dashboard`: actual `RunCoordination` gate, project state and timestamp/audit; comment/chair status alone does not mark reviewed. This handler currently has no caller version input: do not invent an If-Match prerequisite in its explanation. |

### §8.10 — 18 coordination rows (C witnesses)

| Row | Matrix action | Concrete boundary / minimum discriminating witnesses |
|---|---|---|
| C01 | Create/assign handoff | COORD `Handoffs`: source/receiving discipline and actual work owner; assign/reassign distinct, reason/version/eligible people. |
| C02 | Submit/respond handoff | COORD: named sending owner vs manager-only reassignment; actual legal state and source version. |
| C03 | Accept/incorporate handoff | COORD: named receiver, actual state/source, self-review off/on and current membership; manager cannot sign by role. |
| C04 | Coordinate review package | COORD `Reviews`: PM/responsible DL/named coordinator, real round/source/assignment references and lifecycle. |
| C05 | Technical review approval | COORD `Reviews` and domain independence/context: named discipline reviewer, current round, actual author/provenance and settings; keep external unknown-author implementation pending. |
| C06 | Respond/verify review finding | COORD `Reviews/ReviewResponses`: resolver response vs originator/designated independent verifier, reason/evidence/state/ancestry; no resolution-owner self-verification. |
| C07 | Publish source revision/change | COORD `Changes`: current source owner/PM/DL, actual revision/author/source/project and transaction; publication is not a receiving signature. |
| C08 | Assess changed input | COORD `Changes`: named receiving-work owner assessment; separate PM/DL old-revision retention approval with reason/current impact. |
| C09 | Issue submission package | COORD `Submissions/SubmissionReadiness`: actual authorising PM, current manifest/source fingerprints and mandatory gate recheck under transaction; failure after earlier valid input writes nothing. |
| C10 | Propose allocation | PEOPLE/COORD `Allocations`: PM/DL project proposal, actual source/target/confidence; not confirmation or HR approval. |
| C11 | Confirm allocation/change availability | PEOPLE/COORD: real current direct-report/Admin authority plus each project's visibility; separate project allocation confirmation from HR/schedule writers. |
| C12 | Propose design basis/assumption | COORD `DesignBasis`: own discipline/member proposal vs managerial owner assignment, valid evidence/units/versions. |
| C13 | Confirm basis | COORD `DesignBasis`/domain: responsible lead or named independent approver, current version/evidence/author facts. |
| C14 | Approve Proceed under Assumption | COORD `Readiness`: actual PM/DL, scope, fixed expiry and current evidence/reason; generic basis edit is not exception approval. |
| C15 | Propose/verify constraint removal | COORD `Readiness`: removal owner vs affected-work verifier, actual independent roles/source/state/evidence. |
| C16 | Commit weekly output | COORD `WeeklyCommitments`: actual performer signs, chair only proposes; current readiness/week/source and version. |
| C17 | Resolve/verify location issue | COORD `Registers`/issue rules: owner proposes, designated independent verifier acts, own submission restriction and actual source/versions. |
| C18 | View coordination/sources/export | AUTH/COORD/READ: actual project/source readers and export scope, including private projection/flags; opening an external link never implies source-system permission. |

### §8.11 — 10 planning rows (D witnesses)

| Row | Matrix action | Concrete boundary / minimum discriminating witnesses |
|---|---|---|
| P01 | Open Weekly Planner | PEOPLE `DemandWorkload/Planning/Me`: own My Week vs planner capability, direct/managed/unit readers, current flags/active scope; same menu and reader facts. |
| P02 | See self/assignments/allocations/capacity/time away | PEOPLE: each source kind's actual privacy/scope query, approved vs draft, common aggregate/export consistency; no copy of Workload scope as all-source authorization. |
| P03 | See Private draft | PEOPLE `SeePlanningEntry`: actual creator, Admin normal vs reasoned/logged correction; hidden totals/search/notifications remain protected. |
| P04 | Create self entry | PEOPLE `CreatePlanningEntry`: actual own person/project and self kind, active/effective ReadOnly, forbidden foreign target. |
| P05 | Create manager entry | PEOPLE: actual current Supervisor/direct report or Admin, eligible project/source and manager kind; read-only unit leadership does not create. |
| P06 | Change/move/extend/Still valid/delete/copy | PEOPLE `ManagePlanningEntry`: each mutation, actual source owner/current management, Admin correction reason and versions; copy rechecks create right on its new person. |
| P07 | Change manager visibility | PEOPLE: actual manager-entry creator or reasoned Admin correction, current scope and legal state/version; not publication of another manager's private draft. |
| P08 | Record/clear time away | PEOPLE: identify disabled legacy override and real HR-referenced manual/import/reconcile/cancel correction operations, existing source authority and immutable imported state. |
| P09 | Planner export/entry list/personal views | PEOPLE/READ `Planning/Views/Scope`: same actual people/project/private-entry population, caller-owned views and current flags; sharing does not widen data. |
| P10 | Change planning settings | SYS/PEOPLE `Admin/Planning`: actual Admin setting transport and validation; personal notice/reminder preference is a distinct S22 operation. |

## Coverage evidence and guard inventory repair

Preserve all 20 current guard entries. Extend the inventory only from the row call paths above: at minimum review canonical Permissions/Workflow, Projects/Team/Tasks/Deliverables/Milestones/Dependencies/Comments/DocumentLinks/Decisions/Meetings/Dashboard, Infrastructure Coordination/Http, Workload/MyWork/Allocations/WeeklyPlanning/PersonCapacity, Activity/Portfolio/Templates, CalendarScreen, Workspace/Scope/ListExports/SubmissionReadiness, Notifications/NoticePreferences/UserPreferences/NotificationDelivery and the named domain coordination/context rules. Include `Settings`/flag declarations and route/filter registration where a row's reachability depends on them. A file not reachable by a row needs an explicit reviewed exclusion, not an invented blanket test requirement.

For **each row and child operation**, Root's maintained ledger must record: exact row key; operation/route and canonical method/fact/guard members; relevant settings/role/scope/state variants; attainable fixture facts; independent §8 expectation; actual result/reason or scoped404; applicable refusal/no-side-effect evidence; exact test FQN/case, result artifact and source revision/hash. An allowed role factor with an unevaluated alternative/context gate remains conditional. A hash or repeated filename is not a behavioral witness.

For **each guard**, name the relevant member/branch and the row/operation witnesses, not “this whole file was hashed.” Cover all relevant paths across lookup, list, count/aggregate, export and actual commands. Reuse current-source prior evidence where its predicates, facts and dependencies are demonstrably unchanged; otherwise rerun the smallest affected family. Do not add83 copies of the same assertion or rerun the entire repository by default.

Root should extend the existing checker, not turn it into an authorization evaluator:

- Validate exact83 row keys and child-operation registrations, real evidence/test identifiers and referenced existing result artifacts. Reject unsupported coverage labels, missing evidence, unmapped guards/operations and stale source dependencies. A hand-edited `Evaluated` string must not satisfy the strict gate by itself.
- Detect changed canonical predicate bodies as well as signatures/declared dependencies; a whole-source hash is a conservative invalidation signal, not semantic proof. A same-signature body change must demand renewed evidence/review. Retain section cell/condition fingerprint failures.
- Seed defects for impossible creator facts, omitted operation target/correction/privacy/alternative-unit branches, missing child operations, stale evidence, fake success records, new/changed guard dependencies and pending gaps. Structural validation of an evidence record still cannot prove its claimed HTTP result; Root independently reviews receipts and records that review.
- Keep G57/G58/G59 gaps open until their actual feature paths are mapped to the relevant rows and current evidence. In particular, contribution membership is not generic EditTask authority, record-context actions remain source/actor/version-scoped, and enhanced preferences/notification sources remain caller-owned and flag-safe. C's changing external review branch is not covered by a pre-implementation hash.
- Reconcile the public gap/coverage metadata with the reviewed compiled row adapter. Do not read workspace manifests at runtime or silently make `CoverageComplete=true` merely because the template gap disappeared. Clearing a generic family gap requires all its actual row/operation witnesses.

## Routine implementation versus genuine policy uncertainty

**Already settled; no repeated question:** Jay's SystemPM/template alignment; Admin's established effective ReadOnly precedence; direct reports under Q19 and approved flagged unit/indirect **read** expansion; HR as approval authority with HR-referenced fallback and immutable imported facts; named technical signatures; and C's reported Jay decision to block unknown historical issue authors, even with self-review enabled. Root may reconcile broad matrix wording with those detailed approved rules without inventing a new grant. The old legacy override label and “Supervisor all” cell are mapping/qualification work, not authority to reopen their writers or add all-staff permissions.

**One remaining scope ambiguity found here:** S21's Standard-user “own projects” can mean membership/ownership or every currently visible open project. Current `Reports.Ctx.Projects()` selects permitted projects and allows a visible-open nonmember project; the broad matrix does not disambiguate that phrase. Preserve enforcement and explain current source scope. Root should first reconcile it against the complete report requirements/decisions; only if those do not settle it, present that exact population choice to Jay with current visible-project scope as the continuity default. Do not silently narrow exports or label this row fully equivalent while the wording conflict remains. This pass did not establish another hidden-project disclosure.

An actual newly discovered enforcement/spec contradiction must be recorded with a valid command witness and escalated to Root; do not weaken enforcement, mask it with conditional text, or invent a new policy decision just because a helper has not yet been extracted. External implementation defaults are owned by C/Root's separate freeze; they are not new permission choices for this document.

## Root-owned acceptance sequence — all proposed, UNRUN here

1. Freeze B's four-source template handoff; independently review/run its focused role combinations, real template commands and own `/me` capability. Update only the resolved gap; no FR83 completion inference.
2. Freeze row/operation DTOs and the explanation adapter. Run focused `PermissionExplanationTests` and `AdminRoleExplanationTests`: attainable facts, row-specific unknown/alternative branches, current reasons, bounded choices, forged/duplicate queries, no IDs/private facts, no writes and flag-off404. Check the concrete creator/time-away/status/private-event counterexamples above.
3. Run the targeted full83-row/child-operation conformance ledger, reusing existing current-source matrix/family witnesses and adding only missing cases through the four proposed test files. Include effective mixed roles, own/other discipline/owner, removed/restricted membership, current feature flags, versions/reasons/lifecycle, named independence and refusal side effects. The expected matrix conditions must be independent of calling the same catalog/predicate as the test oracle.
4. Run `python3 tools/test_permission_explainer_coverage.py`, then the normal coverage checker. Run `python3 tools/check_permission_explainer_coverage.py --require-complete` only after all reviewed evidence entries/gaps are reconciled. Before that, strict refusal is the expected incompleteness boundary, not a completed gate. Root records exact current results rather than inheriting an earlier PASS.
5. After generation/types/scoped lint/build pass, run A's actual Admin explanation browser driver at1440/375 across every registered role and operation, sampling all distinct conditional/refusal/requirement states and the83-row inventory. Check row-specific facts, role vs project membership, correction/transition selection, stale selection responses, dark/error behavior and keyboard reading. A fixture proving the UI branches does not prove server authorization.
6. Independently review the integrated diff and ledger. Only Root may advance individual rows/guards to Evaluated after their evidence is complete, reconcile `CoverageComplete`, and assess FR/AC-SCR-83/S49 alongside the separate FR82/deactivation requirements. Local implementation/tests are not Jay's Accepted gate, a commit or deployment.

**HOLD.** Only this new plan was written. All application/UI/test/model/manifest/generated/flag/commit/deployment states are unchanged by this lane.
