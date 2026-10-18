# Feature Specification: Company Structure

**Packet**: 050 | **Feature directory**: `specs/050-company-structure` | **Date**: 2026-10-18
**Status**: Specified, not started. Scope approved by Jay on 2026-10-18 with the listed defaults (050-D1).
**Input**: Jay's 2026-10-18 request: "we will have to make company structure for managers", based on the organisation model chosen for the separate Friday capacity app (unit tree, named levels, unit leaders, one direct manager).
**Source**: product specification §54 (§54.1 to §54.9) in `spec-parts/29-company-structure.md`. Related contracts: §8.2, §8.7, §8.8 (roles, visibility, Q19 supervised staff), §13.11 (Workload and Planner), §13.12 (Portfolio), §13.15 (Administration), §13.19 (My Staff), §19 and §51.3 (reports), §20.1 (log), §42 (supervisor capacity), FR-PLN-19 (partial view), §43 (flags, packet 039).

## User outcome

- **Unit leader:** sees and rolls up every person in their unit and the units below it, on My Staff, Workload, Planner and reports, without being everyone's direct manager.
- **Direct manager:** keeps today's rights; can optionally see their whole reporting line.
- **Administrator:** builds the structure on screen or by CSV, with every change logged and past reports kept stable.

## Scope

1. **Levels and units** (§54.2; FR-UNT-01, FR-UNT-02, FR-UNT-03, FR-UNT-04).
2. **People in the structure** (§54.3; FR-UNT-05, FR-UNT-06, FR-UNT-07): one home unit, `supervisor_id` stays the one direct manager, cycles refused.
3. **What a manager sees** (§54.4; FR-UNT-08, FR-UNT-09, FR-UNT-10, FR-UNT-11): people scope for reading only; Restricted projects stay excluded.
4. **Screens and reports** (§54.5; FR-UNT-12, FR-UNT-13, FR-UNT-14, FR-UNT-15, FR-UNT-16): My Staff scope switcher, group by unit, optional project lead unit, Organisation page, report columns.
5. **Administration, import and history** (§54.6; FR-UNT-17, FR-UNT-18, FR-UNT-19).
6. **Shipping dark** (§54.7; FR-UNT-20): `flag.org_structure`.

## Non-goals

- Several companies or tenants.
- Edit rights granted by unit, dotted-line managers, approval chains, cost centres or headcount budgets.
- Synchronising units from Entra ID or HR (D5 keeps the `manager` sync TBD).
- Any per-person score or ranking.

## Acceptance

AC-UNT-01, AC-UNT-02, AC-UNT-03, AC-UNT-04, AC-UNT-05, AC-UNT-06, AC-UNT-07, AC-UNT-08 and AC-UNT-09 in §54.8 are this packet's acceptance criteria. [verification.md](verification.md) maps each one to its slice.

## Decisions

050-D1 to 050-D8 in [DECISIONS.md](../DECISIONS.md), mirrored in §54.9. D1 was approved on 2026-10-18; D2 to D8 use their defaults. A pending row blocks only the flag flip.

## Assumptions

- **Packet 039, S09:** flags exist before any slice merges.
- **Existing people model:** `AppUser.SupervisorId` and `AppUser.OfficeId` (`src/Hub.Api/Data/Entities.cs`) stay as they are; the packet adds `UnitId` beside them.
- **Packet 047, §51.3:** the Team Capacity report exists before S05 adds unit columns to it; until then S05 covers Staff Assignments only.
