## 38. Design Inputs, Readiness and Location Context

Approved scope: Jay accepted all three researched additions on 2026-09-26. Detailed behaviour below is the proposed implementation contract; it requires acceptance testing, not further feature-selection approval. Apply all shared safeguards in §37.1 and canonical vocabulary in §10.8.

### 38.1 Shared Design Basis and Assumptions

Teams can see the current approved criterion or explicit assumption and identify the work that relies on it.

- **FR-BAS-01.** An entry MUST identify its kind (Criterion or Assumption), title, one accountable owner, responsible discipline, applicable location/system scope, value or statement, units when numeric, source link/revision, confirmation due date when provisional and linked consuming tasks/deliverables. Distinguish authority/client requirements from project assumptions; free-text content is not an engineering calculation.

- **FR-BAS-02.** Use Proposed, Confirmed, Superseded and Withdrawn. Only the responsible Discipline Lead or an explicitly assigned independent approver may confirm an entry, with source evidence and rationale. The existing self-review setting applies. An unconfirmed assumption can be used only through the explicit Proceed under Assumption readiness disposition with approval, owner and expiry; it must never display as confirmed.

- **FR-BAS-03.** Confirmed entries are immutable versions. Changing value, units, scope or source produces a proposed replacement with an explicit supersedes link. Confirmation of the replacement invokes change assessment for every linked consumer; the former version remains in historical InputUse records.

- **FR-BAS-04.** Each consumer MUST identify the exact basis version it uses. Highlight consumers using a superseded or withdrawn version and require assessment rather than silently updating it. Relationships must be acyclic and limited to the same project. A missing source is an explicit data-quality condition.

- **FR-BAS-05.** A source decision may confirm or change a basis entry, but the records retain distinct purposes and linked histories. Reopening a decision creates an assessment requirement; it does not silently unconfirm every downstream design.

- **FR-BAS-06.** Duplicate entries with the same title, scope and discipline MUST prompt the user to inspect existing entries before creating another. Conflicting confirmed values remain visible with an unresolved conflict; do not choose a value automatically. Conflict resolution creates an authorised replacement and retains the competing histories.

- **FR-BAS-07.** The register MUST filter by discipline, scope, kind, status, overdue confirmation and affected work; show source evidence, current registered version and version used. Export retains units and provenance. Templates may suggest entries as Proposed only; project confirmation never transfers from a template.

**Acceptance criteria**

- **AC-BAS-01.** Given Civil and Structural use confirmed basis version A, when B is confirmed, then both consumers require impact assessment and their recorded version remains A until explicitly adopted.

- **AC-BAS-02.** Given a numeric criterion has no units, then confirmation is refused; a narrative criterion does not require invented units.

- **AC-BAS-03.** Given an assumption is still Proposed, then ordinary Ready is unavailable for dependent work; authorised Proceed under Assumption records scope, approver and expiry.

- **AC-BAS-04.** Given two confirmed values conflict in the same scope, then the register displays the conflict and does not silently select the newer value.

- **AC-BAS-05.** Given a template is copied into a new project, then its basis entries are Proposed and no approval, source-use acknowledgement or technical sign-off is inherited.

### 38.2 Ready-to-Start Planning and Weekly Commitments

An owner commits to a defined output after checking inputs, decisions, assumptions and available production/review capacity.

- **FR-RDY-01.** For a task or deliverable, readiness MUST separately evaluate required handoffs, predecessor rules, required decisions, basis conflicts/assumptions, assigned production owner and confirmed production/review availability where those resources are required. The PM or responsible Discipline Lead records applicability with reason. Missing or unassessed inputs yield Needs Assessment, not Ready.

- **FR-RDY-02.** Use derived readiness states Needs Assessment, Not Ready, Ready and Proceed under Assumption. Preserve the existing task workflow separately. The owner records Intended Output and completion criteria. A permitted task start that is not Ready requires an explicit warning acknowledgement, reason and PM/lead authorisation; review, access and lifecycle guards cannot be bypassed.

- **FR-RDY-03.** A constraint MUST have a category, one removal owner, removal-needed-by date, affected work and source evidence. States are Open, Resolution Proposed, Verified Removed and Cancelled. The affected work owner verifies removal; the removal owner response alone is not verification. Use links to existing decisions/issues/handoffs rather than duplicate records when they already represent the constraint.

- **FR-RDY-04.** Proceed under Assumption MUST link a specific Proposed assumption version, state the limited work allowed, name the approving PM/lead, record risk and expiry and identify the responsible verifier. Expiry or a changed assumption returns readiness to assessment. Mandatory technical review and unresolved blocking review or submission gates cannot be overridden.

- **FR-RDY-05.** A weekly plan MUST snapshot owner-approved output commitments, target dates, linked work, criteria and readiness at the project coordination-week boundary. The responsible performer explicitly commits; a chair may propose but cannot silently commit another person. States are Proposed, Committed, Met, Not Met and Withdrawn. Later changes retain the original promise and add an attributed reason.

- **FR-RDY-06.** At week close, the owner records Met only with completion evidence matching the original criteria; otherwise record Not Met with a reason such as missing input, decision delay, changed scope or unavailable capacity. Withdrawal after commitment stays in the original snapshot. New scope is a separate commitment, not an edit that erases the earlier outcome.

- **FR-RDY-07.** The screen MUST show the coming three-week window by default (organisation setting), constraints to remove, ready outputs and weekly promises; allow other permitted dates. Display any completion ratio with numerator/denominator and the fixed committed snapshot; record later withdrawals separately and do not use the result as an individual productivity ranking. Link to the existing Weekly Coordination meeting.

**Acceptance criteria**

- **AC-RDY-01.** Given a task is due next week but its required handoff is Submitted and not Accepted, then readiness is Not Ready with the handoff as its reason.

- **AC-RDY-02.** Given the removal owner proposes that a constraint is resolved, then readiness remains blocked until the affected work owner verifies the evidence.

- **AC-RDY-03.** Given an authorised assumption expires, then Proceed under Assumption becomes Needs Assessment and the original approval remains in history.

- **AC-RDY-04.** Given five outputs were committed and one is withdrawn after the snapshot, then the original five remain inspectable and the withdrawal cannot erase the original promise or alter the denominator silently.

- **AC-RDY-05.** Given a meeting chair proposes an output for another person, then it remains Proposed until that performer confirms; task completion and technical review guards still apply.

### 38.3 Location-Linked Coordination Issues

A coordination issue identifies the affected physical area and exact drawing/model reference so disciplines can resolve and verify the same problem.

- **FR-LOC-01.** Extend the existing Issue record with optional structured location references: site area, building/level/room, asset/system, or alignment and start/end station with units. Drawing/model references MUST include identifier, declared revision and external source link. Allow multiple references when one issue spans boundaries; require at least one location or drawing/model reference for a Coordination issue.

- **FR-LOC-02.** Location fields MUST retain the project coordinate/station convention; validate end station at or after start within the same alignment and units. A coordinate requires a declared coordinate reference system and units. Do not infer datums, convert coordinates or interpret engineering geometry.

- **FR-LOC-03.** Keep one resolution owner and identify affected disciplines plus one independent verifying owner. Reuse the existing Issue status vocabulary; a new verification record captures Resolution Proposed, Verified or Rejected. Resolving a Coordination issue requires evidence and verification; creator/PM can appoint a replacement verifier with reason, subject to self-review rules.

- **FR-LOC-04.** Store links to external markups, screenshots and model viewpoints. Include source revision in the issue context and retain historical references after resolution. A changed referenced revision creates a pending impact check; the issue owner/verifier decides whether to reopen through the existing workflow.

- **FR-LOC-05.** Filter and group by location, discipline, drawing/model, revision, owner and verification status. The same issue ID appears in the project register, review package and coordination view; linking it to another view must not create a duplicate issue.

- **FR-LOC-06.** When location or document access is restricted, apply existing project and source permissions without fetching external file bytes automatically. Show unavailable evidence explicitly; a broken link cannot satisfy a required verification check. Exports include only permitted metadata.

- **FR-LOC-07.** BCF import/export is a deferred interoperability follow-up, not part of this initial implementation. Preserve optional external topic ID, model element GUID and viewpoint URL metadata now without claiming BCF conformance. A later packet requires validated file/API contracts, permission mapping and round-trip acceptance cases before enabling exchange.

**Acceptance criteria**

- **AC-LOC-01.** Given a Civil/Utilities issue concerns a station range and drawing revision A, then both disciplines see the same issue ID, exact range/units and source reference.

- **AC-LOC-02.** Given a resolver supplies a correction, then the Coordination issue cannot become Resolved until the independent verifier records verification evidence.

- **AC-LOC-03.** Given drawing B supersedes A, then the closed issue retains A and receives an impact check; it is not silently reopened or marked unaffected.

- **AC-LOC-04.** Given an issue is linked from a review package and the discipline view, then editing its owner updates the single existing record and creates one audit event.

- **AC-LOC-05.** Given station end precedes start or a coordinate omits its reference system, then validation refuses the location entry; no coordinate conversion is guessed.

### 38.4 Delivery sequence and evidence

Implement prerequisites before dependent commands: 025 and 026; 027; 029 and 031; 032 and 028; 033; then complete the integrated 030 view. Packet 030 may add sections incrementally but is not accepted until all nine workflows reconcile. This dependency order refines the user-facing priority order; it does not change which capabilities were approved. Carry the 50-person pilot scenario and the existing 011 hardening gates across every increment. Record failed, passed and unrun checks separately, retain the original 24-packet evidence as historical, and re-estimate delivery after the new scope rather than reuse earlier release percentages.

Packets 025/026 establish immutable revision references; 027 extends those same records with source registration, InputUse and impact commands. The 025 change-impact integration scenario AC-HND-03 is verified after 027, so foundation delivery is not full packet acceptance. Packet 027 supports deliverable/external-source changes first; 031 adds the design-basis source adapter to the same assessment mechanism. This staged integration must not create duplicate revision stores or circular implementation prerequisites.
