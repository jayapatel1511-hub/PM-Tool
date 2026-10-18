# Feature Specification: Task Pickup

**Packet**: 049 | **Feature directory**: `specs/049-task-pickup` | **Date**: 2026-10-18
**Status**: Specified, not started. Scope approved by Jay on 2026-10-18 with the listed defaults (049-D1).
**Input**: Jay's 2026-10-18 request: "a ticketing system for overloaded personnel; they can have that task open for others to pick".
**Source**: product specification §53 (§53.1 to §53.8) in `spec-parts/28-task-pickup.md`. Related contracts: §8.5 to §8.7 (permissions, ownership, visibility), §12.2 (team), §13.10 (My Work), §17 (notifications), §20.1 (log), §36.8 (time entries), §43 (flags, packet 039), §45.4 (load bands, packet 041).

## User outcome

- **Overloaded person:** puts a task up for pickup with a reason, without losing ownership until someone takes it.
- **Teammate with room:** sees open offers from their own team and picks one up; it is theirs at once, reassigned through the normal path, logged and notified.
- **Manager, PM or Discipline Lead:** assigns an offered task to someone in the same team.

## Scope

1. **Offer and withdraw** (§53.2; FR-PKP-01, FR-PKP-02, FR-PKP-03): an offer is state on the task, not a new item; one open offer per task; closes on its own when the task closes or is reassigned elsewhere.
2. **Open for pickup list** (§53.3; FR-PKP-04, FR-PKP-05): one list under existing visibility, reachable from My Work and the project Work group, with the viewer's own load band shown as information.
3. **Pick up and assign** (§53.4; FR-PKP-06, FR-PKP-07, FR-PKP-08, FR-PKP-09): only the assignee's team (same home unit under §54, otherwise same direct manager) may pick up, with no approval; the direct manager, PM or Discipline Lead may assign within the team; both call the existing reassignment command.
4. **Notifications, log and report** (§53.5; FR-PKP-10, FR-PKP-11, FR-PKP-12).
5. **Shipping dark** (§53.6; FR-PKP-13): `flag.task_pickup`.

## Non-goals

- A separate ticket entity, queue or help-desk workflow.
- Splitting a task's hours, or offering deliverables, reviews, decisions or actions.
- Picking up work from outside one's own team.
- Automatic matching, ranking of volunteers, points or rewards.
- Any per-person pickup statistics.

## Acceptance

AC-PKP-01, AC-PKP-02, AC-PKP-03, AC-PKP-04, AC-PKP-05, AC-PKP-06, AC-PKP-07 and AC-PKP-08 in §53.7 are this packet's acceptance criteria. [verification.md](verification.md) maps each one to its slice.

## Decisions

049-D1 to 049-D6 in [DECISIONS.md](../DECISIONS.md), mirrored in §53.8. D1 was approved on 2026-10-18; Jay decided D2 and D3 the same day; D4 to D6 use their defaults. A pending row blocks only the flag flip.

## Assumptions

- **Packet 039, S09:** flags exist before any slice merges.
- **Packet 050 (optional):** with `flag.org_structure` on, the team is the home unit; without it, the team is the people sharing a direct manager.
- **Packet 041:** the server load band exists before S03 shows it; until then the band is omitted.
- **Reassignment:** pick-up and assignment reuse the task reassignment command in `src/Hub.Api/Features/Tasks.cs`, so notification, log and rowVersion rules come with it.
