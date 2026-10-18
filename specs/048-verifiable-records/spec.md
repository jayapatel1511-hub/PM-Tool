# Feature Specification: Verifiable Records

**Packet**: 048 | **Feature directory**: `specs/048-verifiable-records` | **Date**: 2026-10-18
**Status**: Specified, not started. Scope approved by Jay on 2026-10-18 with the listed defaults (048-D1).
**Input**: Jay's 2026-10-18 request to add the mechanisms confirmed by the October 2026 product research. That research lives outside this repository; no third-party product is named here.
**Source**: product specification §52 (§52.1 to §52.9) in `spec-parts/27-verifiable-records.md`. Related contracts: §12.4 (deliverables, DeliverableIssue), §13.14 (Activity History), §20.1 to §20.4 (audit), §37.4 (change records), §37.6 (allocations, FR-CAP-07), §38.2 (weekly commitments, FR-RDY-05, FR-RDY-06, FR-RDY-07, AC-RDY-04), §43 (flags, packet 039), §45.4 (load bands, packet 041), §48 (off-host backup, packet 044), §50.5 (re-baseline, FR-INT-13, FR-INT-14, packet 046).

## User outcome

- **Audit:** an Admin, a PM or an auditor can see that the activity log has not been altered since each entry was written, and can download a record that proves it later.
- **Weekly commitments:** a project's commitment reliability reads as one honest figure with its numerator and denominator, and misses are grouped by why they happened. The organisation can require a second person to confirm Met.
- **Allocations:** a Supervisor sees what an allocation change does to a person's week before saving it.
- **Re-baselines:** a PM's re-baseline keeps the estimate and owner as well as the dates, and earlier baselines are never lost.
- **Revision codes:** a project with a revision convention sees non-standard or out-of-sequence revisions marked without anyone being blocked.

## Scope

1. **Verifiable activity log** (§52.2; FR-VER-01, FR-VER-02, FR-VER-03, FR-VER-04, FR-VER-05, FR-VER-06):
   - a per-project and organisation hash chain written in the change's own transaction, with the actor and time set on the server;
   - nightly and weekly verification;
   - a status line in Activity History;
   - an Admin chain page with a downloadable verification record;
   - retroactive chaining of existing rows, marked as such.
2. **Commitment reliability** (§52.3; FR-VER-07, FR-VER-08, FR-VER-09, FR-VER-10, FR-VER-11):
   - "Met M of C (P%)" per snapshot at project and discipline level, never per person;
   - bands set in organisation settings;
   - a required miss category on Not Met;
   - an optional Met confirmation step;
   - a six-week trend and a "Why commitments were missed" table and report.
3. **Allocation preview** (§52.4; FR-VER-12, FR-VER-13): `preview=true` on allocation create, edit and confirm, through the save path in a rolled-back transaction, and "After this change" figures in the form.
4. **Baseline effort and owner** (§52.5; FR-VER-14, FR-VER-15): numbered, never-edited deliverable baselines B1 to Bn holding dates, owner, estimate and remaining hours, with the change since the current baseline.
5. **Revision-code check** (§52.6; FR-VER-16, FR-VER-17): an optional project convention, with Non-standard and Out-of-sequence marks that never block, and a re-check job.
6. **Shipping dark** (§52.7; FR-VER-18): one flag per part; flag off is today's behaviour.

## Non-goals

- External anchoring of the log (a public ledger or a third-party timestamp service), digital seals on deliverables, or any change to who can read the log.
- Any per-person reliability figure, ranking or productivity score (FR-RDY-07, constitution II).
- Automatic levelling, rescheduling or assignment from previews.
- Refusing an issue because of its revision code.
- Repairing or re-chaining log rows after a break.

## Acceptance

AC-VER-01, AC-VER-02, AC-VER-03, AC-VER-04, AC-VER-05, AC-VER-06, AC-VER-07, AC-VER-08, AC-VER-09, AC-VER-10, AC-VER-11 and AC-VER-12 in §52.8 are this packet's acceptance criteria. [verification.md](verification.md) maps each one to its slice.

## Decisions

048-D1 to 048-D10 in [DECISIONS.md](../DECISIONS.md), mirrored in §52.9. Defaults apply while work ships dark. A pending row blocks only its flag flip.

## Assumptions

- **Packet 039, S09:** the flags exist before any slice merges.
- **Packet 041, S01 and S02:** the server load band exists before the preview slice shows a band.
- **Packet 044, S14:** the off-host backup exists before the head hash is copied off the database (D3). Until then the verification record download is the only off-database copy.
- **Packet 046, S07 and S08:** re-baseline exists before baseline history.
- **All writes:** every log row is written by `HubDb.SaveChangesAsync` inside one transaction (`src/Hub.Api/Data/HubDb.cs:981-1043`), so the chain is assigned in one place.
