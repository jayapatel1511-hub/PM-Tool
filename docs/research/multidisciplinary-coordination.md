# Research rationale for Tuesday's coordination expansion

**Research date:** 2026-09-26 (America/Halifax)
**Status:** [Inference] Product recommendations based on the primary sources below, the inspected repository and Jay's accepted scope. Source guidance is not proof that a proposed feature improves Tuesday, and this document is not a standards-compliance or professional-practice certification.

## Evidence and application

| Primary source | Relevant documented practice | Proposed Tuesday application |
|---|---|---|
| [NASA Systems Engineering Handbook: Interface Management](https://www.nasa.gov/reference/6-3-interface-management/) | Define interfaces and responsibilities, retain interface documentation and assess changes across affected parties. | Named sending/receiving owners, purpose-specific handoffs, explicit criteria and change-impact assignments. Adapted from systems engineering; not a claimed civil-engineering requirement. |
| [NASA: Requirements Management](https://www.nasa.gov/reference/6-2-requirements-management/) | Maintain sources, ownership, traceability and baselines; evaluate and communicate approved requirement changes. | Versioned design basis/assumptions linked to consuming work, with evidence and human approval. |
| [Lean Construction Institute: Last Planner System for Design](https://leanconstruction.org/lean-topics/last-planner-system-for-design/) | Plan information handoffs and short design cycles, establish performer commitments, identify constraints and learn from reasons for variance. | Ready-to-start checks, removal owners, fixed weekly output promises and retained reasons for missed commitments. |
| [Autodesk Design Collaboration: Packages](https://help.autodesk.com/cloudhelp/ENG/Collab-Timeline/files/Design_Collab_Packages.html) | Teams share versioned packages and receiving teams choose when to consume them; previously vetted and updated versions can be distinguished. | Separate submitted, accepted-for-purpose and incorporated states and preserve the consumer's actual revision. This is a workflow precedent, not a claim that Tuesday supports Autodesk integration. |
| [buildingSMART: BIM Collaboration Format](https://technical.buildingsmart.org/standards/bcf/) | Exchange model-based topics with references to viewpoints, coordinates and model elements through file/API workflows. | Location and source-revision metadata on existing issues; later validated BCF exchange if needed. No BCF conformance is claimed. |

## Repository baseline

Inspected implementation: `build-coordination-hub` at `c8737ec53e798510b2e52c9f24a22bece9a7e65f`. It contains the original 24 feature packets and application source. Relevant existing capabilities include deliverables/issue history, a single-reviewer task workflow, dependencies, decisions, risks/issues, meeting actions, weekly coordination, time capture, workload and saved views.

The existing `Deliverable` and `DeliverableIssue` records already store revision and transmittal metadata. This amendment adds exact revision-use and assessment relationships rather than presenting basic revision fields as a new feature. The existing weekly coordination view already displays blocked/upcoming work; ready-to-start checks and performer commitments extend it. Location context extends the existing Issue ID.

Earlier discussion accepted six additions, which were not present in this GitHub commit. This change records all six and the three further accepted capabilities as packets 025–033. It does not assert that earlier unpushed files were recovered byte-for-byte.

## Product decisions

- Keep the six additions: handoffs, multidisciplinary reviews, revision/change impact, submission readiness, dated capacity allocations and a discipline coordination view.
- Add shared design basis/assumptions, ready-to-start planning/weekly commitments and location-linked issues.
- Make exact source versions, named owners and explicit receiving/verification actions part of the written contract.
- Reuse existing task, issue, decision and document-link records; represent new obligations as linked coordination records.
- Keep proposals, acknowledgement, acceptance, incorporation, verification and issue distinct.
- Preserve original weekly promises, earlier approvals and issued manifests after later changes.
- Use manual source registration with honest freshness labels before adding connectors.

The detailed states, fields and refusal rules in the specification are authored design decisions, not quotations or mandatory implementations prescribed by the sources. Validate them with the pilot teams. These sources do not establish a quantified reduction in delay, rework or coordination effort for Tuesday.

## Deferred work

Embedded BIM/CAD viewers, automatic clash detection, file hosting, document transmission, BCF exchange, external login, deeper Teams/SharePoint integration and AI are not included in the initial nine-packet build contract. The existing no-AI constraint remains unchanged. The user accepted the feature scope described in the research recommendation; future interoperability needs their own concrete contracts and acceptance cases.
