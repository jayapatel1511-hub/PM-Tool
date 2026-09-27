# Tuesday — multidisciplinary coordination design

**Date:** 2026-09-26
**Status:** Approved feature scope and implementation design. Foundations for packets 025–027 are implemented; full acceptance and packets 028–033 remain pending. See packet verification records.

This document translates the approved coordination requirements into a consistent experience. The product specification remains authoritative, with §10 defining canonical vocabulary and §8 defining permissions. Source amendments are `spec-parts/12-multidisciplinary-coordination.md` and `spec-parts/13-design-inputs-readiness-location.md`. The [packet index](specs/README.md) supplies implementation order and [research notes](docs/research/multidisciplinary-coordination.md) explain the recommendations.

The product name is **Tuesday**. The existing application still renders Coordination Hub; this design change does not implement a rename, create a logo or certify trademark availability. Existing technical Hub namespaces remain valid.

## Product intent

[Inference] The proposed experience is intended to make five coordination questions answerable from each discipline's work queue:

1. What do we owe?
2. What are we waiting for?
3. Which revision are we using?
4. What changed?
5. What can we confidently start?

## Navigation and information architecture

Retain the existing workspace/project scope, My Work, Projects, Tasks, Calendar, Files, Time, Reports, Team and Workload. Add the new capabilities inside their owning project context. Only implemented and permitted routes become interactive. Reuse existing record IDs and deep links when an item appears in multiple views.

| Destination | Primary content | Main actions |
|---|---|---|
| Coordination | Five fixed question sections; pending reviews and submissions | Open source, assign action, set needed date, record weekly outcome |
| Handoffs | Incoming/outgoing receipts; needed/promised dates; revision used | Submit, accept for purpose, request clarification, return, record incorporation |
| Reviews | Fixed revision manifest, discipline reviews, findings | Start round, assign reviewer, respond, verify, approve own discipline |
| Changes | Current registered revision versus revision used; assessment queue | Register revision, publish notice, assess, link correction, verify resolution |
| Submissions | Required deliverables, exact revisions, checks and issue history | Assemble, check, open blocker, issue after final recheck |
| Design Basis | Criteria and assumptions, sources, units, scope and consumers | Propose, confirm, replace version, assess affected work |
| Ready to Start | Upcoming outputs, prerequisites, constraints and weekly promises | Remove constraint, verify removal, propose/confirm commitment |
| Issues (existing) | Existing issue plus location/drawing context and verification | Link source, propose resolution, independently verify |
| Workload (existing) | Capacity, reservations, estimated demand and proposed requests | Request allocation, confirm with conflict warning, change dated availability |

Submissions is a coordination record; Files remains an external-link library. Location-linked issues remain the same Issue record. A design basis may link to a Decision while preserving their different lifecycles. The discipline overview is a projection, not another place to maintain duplicate status.

## Shared screen structure

- Header: project/workspace scope, discipline, owner, date range, saved view, search and export.
- Lists: readable key, title, accountable owner, discipline, due/needed date, explicit state, revision/context and next action.
- Detail panel: purpose and applicable scope, source references, state history, related work, evidence and permitted actions.
- Refusal: show the exact blocker, required actor or stale version, with a link to the record that must be addressed.
- Unknown: show Needs Assessment, missing source, partial visibility or unknown latest revision explicitly. A blank value must not look like a pass.
- Loading and empty states: distinguish no results in a filter, no access and unavailable capability without revealing restricted project existence.
- Keyboard: preserve focus on return from source detail, use accessible tables/dialogs and never make colour the only status indication.

The existing navy frame and blue accent may remain as visual cues. Engineering workflow clarity governs the design; no monday.com assets, logo or screen reproduction is introduced.

## Handoff experience

A handoff begins from existing work so project, source owner, discipline and target are prefilled. The creator adds the receiving owner, exact input/revision, intended use, needed date, promised date and acceptance criteria. Separate receipts serve different receiving disciplines or intended uses.

The receiver sees three distinct questions: Was it delivered? Is it usable for the stated purpose? Has it been incorporated? A returned or clarification-requested input displays the reason and the sender's response. An accepted input never silently becomes a technical sign-off. A source revision change appears alongside the incorporated revision, with an action to assess or adopt it.

## Review and submission experience

A review round locks its manifest and assigns one reviewer for each required discipline. Each finding has a resolution owner and a verifying actor. A response is not closure; the originator or independent authorised replacement verifies it. Changed content opens a new round for affected approvals while preserving historical evidence.

Submission readiness presents failed and unknown checks first, grouped by owner and discipline. A readiness percentage cannot replace the actual blocking list. The Issue action previews exact revisions, recipient reference and mandatory checks, then performs a server-side transactional recheck. A stale approval returns the user to the affected check. An issued manifest is immutable and points to the external transmittal; Tuesday does not send the files.

## Design changes and assumptions

Use one shared source-revision representation across handoffs, review manifests, design-basis links and submissions. Distinguish the source's current **registered** revision from the consumer's **used** revision. Manual registration is labelled with who and when; a URL does not prove that its content is unchanged.

A change notice uses explicit links to identify potentially affected work. Each owner records Unaffected, Update Required or Clarification Needed. Unknown/unlinked work is outside the assessed scope. Neither seeing a notice nor clicking acknowledgement completes the impact assessment.

A Proposed assumption may support only named limited work under an authorised, expiring exception. Confirmed design-basis values have units and source evidence where applicable. The product never supplies or infers engineering values. Replacements retain earlier consumer references and open impact checks.

## Planning and capacity

The Ready to Start view adds readiness to existing upcoming work. It displays missing inputs, decisions, basis conflicts, production ownership and review availability. A constraint has one removal owner; the affected work owner verifies removal. The performer confirms a weekly output commitment with criteria and a date. Weekly snapshots retain the original promise after a later withdrawal or date change.

Workload presents these separately: available hours, confirmed reserved hours, proposed requests and estimated remaining work. Use the specified overlap rule rather than adding linked reservations and estimates twice. Actual hours remain a separate historical record. Partial project visibility must not be labelled spare capacity. A supervisor can approve an overload with a recorded reason; the app never silently levels resources.

## Integration boundaries

- Keep the existing .NET/React/PostgreSQL modular monolith and single database.
- Add project-scoped entities, commands, pure state evaluators and accessible views; reuse audit, outbox, notifications, search and exports.
- Relationships identify exact source records and versions; no cross-project dependency graph is introduced.
- Keep DMS/SharePoint/CAD/model tools as the content systems of record; links and metadata work without new connectors.
- Retain optional external topic/model identifiers for future BCF interoperability. BCF exchange, embedded models, automatic change extraction and deeper Teams/SharePoint integrations remain separately scoped follow-ups.
- Preserve current tenant sign-in, project visibility and lifecycle rules on every entry point. No new external users, outgoing external notifications, AI, custom workflow builder or generic field builder.

## Delivery and acceptance

| Increment | Packets | Reviewable outcome |
|---|---|---|
| Handoffs and review foundations | 025, 026 | Revision-specific receipt and discipline review with verified comment closure |
| Change awareness | 027 | Consumer revision tracking and accountable impact assessments |
| Basis and staffing | 029, 031 | Dated confirmed allocations and versioned shared design inputs |
| Readiness and submissions | 032, 028 | Explicit ready work, original weekly promises and current issue gates |
| Location and integrated view | 033, 030 | One issue per physical problem and reconciled discipline coordination |

These increments cover all approved features. The order is dependency-driven, not a promise of duration or release date. Foundation tasks for 025–027 are implemented; remaining packet tasks and full acceptance are tracked in the packet index. The 50-person pilot can enable each increment after its acceptance and existing hardening gates pass; participant count is not evidence of concurrency capacity.

No prior implementation-completion percentage applies to the enlarged 33-packet scope. The [validation record](docs/coordination-spec-validation.md) distinguishes documentation checks from application verification.
