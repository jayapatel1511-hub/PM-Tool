# S03 normal-command evidence and proposed read integration

Read-only source trace confirms the materialized Task blocker list and canonical readiness evidence are separate contracts. No change to Waiting/Blocked/materialization is made by this proposal. A focused normal-API evidence test is being authored in this lane.

## Valid normal command chain

1. Create Task A, Task B and a source Deliverable using existing project APIs.
2. Create a Handoff through `POST projects/{p}/handoffs`, targeting A; transition it to Submitted through the normal command. A Handoff has exactly one target.
3. Create/read a Readiness assessment for A; `ReadinessEndpoints.EvaluateCurrent` returns the exact typed Handoff source as unsatisfied while Submitted. This does not mutate TaskState.
4. Create an Open WorkConstraint through `POST projects/{p}/readiness/Task/{B}/constraints`, linked to that exact Handoff (`LinkedType=Handoff`, `LinkedId=...`). B now has explicit constraint evidence referring to the same Handoff; B has not become a second direct Handoff target.
5. Register/publish a source and adopt it independently for A and B through normal input-use commands. Both Task SourceUses reads already support this shared registered source with exact used/current revisions.
6. Assert all source links/ownership/dates through Readiness Detail/constraint reads, and assert no Handoff/constraint operation fabricated a Task Waiting/Blocked row. Legacy insertion into `TaskState.BlockedBy` is not used in this evidence test.

## Smallest proposed integration, for root review

Add a separately named read-only `ReadinessInputs` projection on enabled Task detail, sourced from the existing `EvaluateCurrent`/Readiness detail contract. It must label `Needs Assessment`/`Not Ready` evidence independently of the materialized task Waiting/Blocked state. Reuse core RecordContextReader for each exact typed source actor/date/action, with current authorization and unavailable recovery. Show the actual WorkConstraint and its explicitly linked Handoff as distinct relationship roles; do not substitute the Handoff for the constraint.

Project/Weekly grouping may deduplicate the same Handoff source over those explicit read relationships while preserving provenance (A direct target; B via the exact open linked constraint). A counter describing this cohort must name readiness-affected work, not assert that the materialized Task IsBlocked flag changed. Accepted/removed sources stop holding readiness by the existing EvaluateCurrent rules, never by an independent presentation rule.

Resolved clarification (Jay, 2026-10-31): keep one direct Handoff target and separately labelled readiness links. Root updated FR/AC-SCR-02 and DECISIONS. This lane now implements that read separation in WorkReadinessInputs.cs, TaskPanel and Weekly Coordination, with exact relationship roles and independent constraint resolution. The materialized predecessor/decision blocker path remains separately required and is exercised through normal commands by WorkReadinessSourceEvidenceTests. No Task evaluator or Handoff target change is proposed or implemented.

Read trace evidence: Handoffs.cs Create/Move; Readiness.cs EvaluateCurrent source loops and RaiseConstraint; Coordination.cs Adopt; Changes.cs input-use command; DisciplineCoordination.cs downstream Handoff blocker projection (a separate graph projection, not a second target).
