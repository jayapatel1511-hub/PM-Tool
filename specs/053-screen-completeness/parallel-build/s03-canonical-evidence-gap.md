# S03 canonical evidence gap — still required by the full goal

FR/AC-SCR-02 is not proved by inserting Handoff identities into TaskState.BlockedBy.
The presentation batch correctly preserves legacy/materialized references, but root must
not call the normal-write handoff/shared-input scenario accepted from that fixture.

Observed source owners:

- EvaluationService builds TaskSnap/dependency/decision data; the task evaluator's stored
  Waiting/Blocked list currently comes from predecessor, decision and manual inputs.
- ReadinessEndpoints.EvaluateCurrent recomputes linked Handoff source checks from the
  actual canonical target relationship (Readiness.cs around119), and active constraints
  from their canonical target/state (around237). Source-backed checks are not satisfied
  from old manual rows. The read can collect typed CheckSource records.
- Readiness Detail reads an existing assessment/checks AsNoTracking, calls EvaluateCurrent,
  and returns typed source evidence plus blocked/unknown codes. It also applies separate
  allocation visibility; any reuse must preserve current access, current source identity,
  nullable/missing facts and exact-record links.
- A Handoff has one explicit TargetTaskId or TargetDeliverableId. Do not fabricate a
  second target, inherited relationship or Task IsBlocked change to fit an example.

Required follow-up after verifying the bounded presentation: exercise a normal command
path for actual handoff/constraint readiness evidence and define the presentation from
those canonical read contracts. Distinguish readiness failures from materialized Task
Waiting/Blocked state; no new mutation, threshold or permission may be invented by the
presentation helper. If an existing valid shared-source path supplies multiple tasks,
prove it through normal API commands and derive other-task effects from those exact
stored references. Any missing canonical capability stays a visible build dependency;
it is not removed from the objective or accepted from synthetic legacy JSON.

S03 stays unchecked until the full acceptance scope has current evidence. Source review
of the batch found and required fixes for Weekly Waiting-only input loss/dedup; those
UI repairs are distinct from this normal-source evidence gap.


Jay resolved the semantic conflict on2026-10-31: KEEP ONE TARGET; SHOW LABELLED READINESS
LINKS. The source FR/AC-SCR-02 now requires truthful direct-target versus via-constraint
provenance, independent resolution behavior and a separate normal materialized shared-
blocker example. Work owns the new readiness projection and normal-command tests. Full
S03 remains unchecked until those requirements are implemented and verified.
