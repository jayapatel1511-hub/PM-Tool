## 52. Verifiable Records Amendment

Approved scope: drafted on 2026-10-18 from Jay's request to add the mechanisms confirmed by the October 2026 product research, and approved by Jay the same day with the listed defaults (048-D1). The amendment is additive only: nothing in this section removes, hides, narrows or freezes a current feature, screen, route, report, setting or rule. Where an addition touches existing behaviour, today's behaviour stays the default. Packet 048 delivers this section.

It adds five things. The activity log becomes verifiable. Weekly commitments gain a defined reliability figure, miss categories and an optional confirmation step. Allocation edits can be previewed before they are saved. A PM re-baseline also keeps effort and owner, and keeps every earlier baseline. A project can declare a revision-code convention that is checked without blocking anyone. Each addition reuses records, rules and reads that already exist.

Where this section adds to §12.4 (deliverables), §20.2 to §20.4 (audit structure, properties and implementation), §37.6 (FR-CAP-07, allocation confirmation), §38.2 (FR-RDY-05 to FR-RDY-07, weekly commitments) and §50.5 (FR-INT-13 and FR-INT-14, re-baseline), this section governs.

Every addition is deterministic and explainable. Nothing here scores or ranks people, forecasts, levels resources or uses AI (§30, constitution II).

### 52.1 Scope and boundary

- Part A, the verifiable activity log: §52.2.
- Part B, commitment reliability: §52.3.
- Part C, allocation preview: §52.4.
- Part D, baseline effort and owner: §52.5.
- Part E, the revision-code check: §52.6.

§52.7 sets how each addition ships. Out of scope: external anchoring of the log on a public ledger or a third-party timestamp service, digital signatures or seals on deliverables, and any change to who may read the activity log (§20.3 Access).

### 52.2 Verifiable activity log

- **FR-VER-01.** Every `ActivityLog` row MUST belong to exactly one chain: the project's chain when `project_id` is set, otherwise the organisation chain. Each row MUST add `chain_seq` (gap-free integer from 1 within its chain), `prev_hash` (the previous row's `row_hash`, or 32 zero bytes for sequence 1) and `row_hash`.

- **FR-VER-02.** `row_hash` MUST be SHA-256 over a canonical serialisation (RFC 8785 JSON canonicalisation). The input holds the chain identifier, `chain_seq`, `prev_hash` and every stored field of the row: `id`, `occurred_at` (UTC, microseconds), actor user and type, project, discipline, item type, ID, key and name, action, categories, `changes` with every old and new value, reason, correlation ID, source and snapshot. Display names resolved at render time are not hashed (§20.3).

- **FR-VER-03.** The sequence and both hashes MUST be assigned on the server inside the same database transaction that writes the change and its log row (§20.3, Same transaction). Rows on one chain MUST be serialised with a transaction-scoped lock on that chain, so concurrent writes never share or skip a sequence. No client value contributes to the actor, time or hash input. If the log row or its hash cannot be written, the change MUST fail. There is no path that saves the change and drops its log row.

- **FR-VER-04.** A verification read MUST recompute a chain from a given sequence and return `valid` (boolean), `total`, `first_broken_seq` (null when valid), `head_seq`, `head_hash` and `verified_at`. A nightly job MUST verify every chain from its last good checkpoint, and a weekly job MUST verify every chain in full. A break MUST raise an Admin attention item and one email to Admins naming the chain and first broken sequence. The system never repairs, rewrites or re-chains rows after a break.

- **FR-VER-05.** Activity History (§13.14) and the project Activity Log report MUST show one line to the people who can already read that log:
  - "Log verified · N entries · checked [time]", or
  - "Log check failed at entry N".

  An Admin page MUST list every chain with its last check. It MUST offer **Download verification record**, a JSON and CSV file with the chain, checked range, entry count, head sequence and head hash, verification time and verifier. Anyone holding an earlier record can later show that the entries it covers are unchanged. Verifying and downloading write one activity entry each in the organisation chain.

- **FR-VER-06.** On switch-on, a one-time job MUST chain the existing rows of each chain in `occurred_at`, then `id` order. It then writes one "Chain started" entry recording how many earlier rows were chained retroactively, so the record never implies those rows were protected when they were written. The application database role keeps INSERT and SELECT only on `activity_log` (§20.3), and the hash columns cannot be updated through any application path.

### 52.3 Commitment reliability

- **FR-VER-07.** For each captured weekly plan snapshot (FR-RDY-05), the readiness screen MUST show the weekly commitment reliability as "Met M of C (P%)":
  - C is the snapshot's committed count.
  - M is the commitments from that snapshot recorded Met (confirmed Met when FR-VER-10 is on).
  - P is M ÷ C as a whole percent, rounded half up, or "—" when C is 0.

  Withdrawn-after-capture commitments stay in C and are listed separately (AC-RDY-04). Commitments not yet recorded after week close are shown as "N not yet recorded" and are not counted as Met. The figure covers a project, or a discipline within a project, and never a person (FR-RDY-07).

- **FR-VER-08.** Two organisation settings, `commitment_reliability_good_pct` (default 80) and `commitment_reliability_watch_pct` (default 60), MUST set the bands: Good at or above the first, Watch at or above the second, otherwise Low. Bands are shown as text with colour, never colour alone, and never change health, attention or notifications.

- **FR-VER-09.** Recording Not Met (FR-RDY-06) MUST add a required miss category from an organisation-managed list. The default list is Missing input, Decision delay, Changed scope, Unavailable capacity, Technical problem and Other. The existing free-text reason stays required. Categories are copied by ID, never by text, so renaming a category relabels history without changing it. A category in use can be retired but not deleted.

- **FR-VER-10.** An organisation setting `commitment_met_confirmation` (default off, which keeps today's behaviour) MUST, when on, change how Met works:
  - Met recorded by the performer becomes "Met, awaiting confirmation".
  - The project's PM or the Discipline Lead of the work, never the performer, confirms it or returns it to Committed with a reason.
  - The server enforces who may confirm.
  - Reliability counts confirmed Met only, and shows the awaiting count beside it.

  Commitments recorded while the setting was off stay Met.

- **FR-VER-11.** The readiness screen MUST show, for the last `commitment_trend_weeks` captured weeks (organisation setting, default 6):
  - the weekly M of C series;
  - a "Why commitments were missed" table of Not Met counts by category, each count opening the list of those commitments.

  The same data MUST be available as a catalogue report with CSV and XLSX export, under the screen's visibility rules.

### 52.4 Allocation preview

- **FR-VER-12.** The allocation create, edit and confirm commands (§37.6) MUST accept `preview=true`. A preview runs the same validation and figure computation as the save inside a transaction that is always rolled back. It returns the affected person-weeks after the change: capacity, planned, free, the load band (packet 041) and the existing over-capacity/load warnings. Overlapping Plans and Deliverable Pressure remain deferred under §39.12; a preview MUST NOT return fabricated false or zero values for those unavailable indicators. It writes nothing, logs nothing and notifies no one.

- **FR-VER-13.** The allocation form and planner edit MUST show "After this change: [planned] of [capacity] h, [band]" for each affected week before save, refreshed after a short pause in typing. The save recomputes everything. If the saved figures differ from the last preview because of a concurrent change, the form shows the new figures. The over-capacity reason of FR-CAP-07 and the rowVersion rules are unchanged.

### 52.5 Baseline effort and owner

- **FR-VER-14.** Each PM re-baseline of a deliverable (FR-INT-13) MUST also create a numbered baseline record, B1, B2 and onward per deliverable. The record holds the baseline dates, owner, `estimated_hours_total`, `remaining_hours`, reason, actor and time. Records are never edited or deleted. "Use original dates" clears the current baseline pointer and keeps every record. The FR-INT-13 date columns keep holding the current baseline as specified. Milestones keep dates only.

- **FR-VER-15.** The deliverable detail panel, register export and reports MUST add, beside "Since re-baseline" (FR-INT-14), the change since the current baseline: estimate hours (+/−), remaining hours, due-date days and an owner change ("Owner: A → B"). A baselines list MUST show B1 to Bn with their values and reasons. A change record (§37.4, CH) MAY cite the baseline it is measured against.

### 52.6 Revision-code check

- **FR-VER-16.** A project setting MAY declare a revision convention. The options are a preset (A, B, C …; 0, 1, 2 …; P01, P02 … then C01, C02 …) or an ordered list of allowed codes, and the default is none, which keeps today's behaviour. When a convention is set, a deliverable revision or issue revision (§12.4, DeliverableIssue) that does not match is still saved. It is marked "Non-standard revision: expected [pattern]", and a revision that sorts before the deliverable's last issued revision is marked "Out of sequence". The marks appear on the register, the detail panel and a register filter, never block an issue, and never change status, health or attention.

- **FR-VER-17.** Saving or changing a convention MUST re-check the project's deliverables and issue history in one job and write one activity entry with the counts. Clearing the convention removes every mark.

### 52.7 Shipping dark

- **FR-VER-18.** Each part MUST merge dark behind its own flag under §43's flag rules, declared with its packet, and is turned on by Jay. With a flag off, every screen, response, report and export is as it was before that addition. No existing feature is put behind a flag.

| Flag | Covers |
|---|---|
| `flag.verifiable_log` | §52.2 (the chain is written from switch-on; once on, it cannot be switched off without a decision, 048-D4) |
| `flag.commitment_reliability` | §52.3 |
| `flag.allocation_preview` | §52.4 |
| `flag.baseline_history` | §52.5 |
| `flag.revision_check` | §52.6 |

### 52.8 Acceptance criteria

- **AC-VER-01.** Given two users edit tasks in the same project at the same moment, then their log rows get consecutive sequences with no gap or duplicate, and each `prev_hash` equals the previous row's `row_hash`.

- **AC-VER-02.** Given a database operator changes one `changes` value, reason or time in a project chain, then the next verification returns `valid=false` with that row's sequence as `first_broken_seq`; Activity History shows "Log check failed at entry N"; Admins get one attention item and one email; nothing is repaired.

- **AC-VER-03.** Given the log row for a task edit cannot be written, then the task edit is not saved and the user sees the existing error reference; no change exists without its log row.

- **AC-VER-04.** Given an Admin downloads a verification record and, later, a covered row is altered, then verifying again against the record's head hash identifies the break.

- **AC-VER-05.** Given switch-on with 10,000 existing rows, then each chain is built in order, one "Chain started" entry states the count chained retroactively, and full verification passes.

- **AC-VER-06.** Given a snapshot of 9 commitments with 7 Met, 1 Not Met and 1 withdrawn after capture, then reliability reads "Met 7 of 9 (78%)", band Watch with defaults, and "1 withdrawn after capture" is listed; no per-person figure appears anywhere.

- **AC-VER-07.** Given a performer records Not Met, then a miss category and a reason are both required; renaming the category later relabels the history and keeps the counts.

- **AC-VER-08.** Given `commitment_met_confirmation` is on, when the performer records Met, then it reads "Met, awaiting confirmation" and is not counted; the performer cannot confirm it; the Discipline Lead confirms it and it counts.

- **AC-VER-09.** Given an allocation edit that would raise a person-week from 32 to 46 of 40 h, when previewed, then the form shows "After this change: 46 of 40 h, over capacity" and nothing is written, logged or notified; the saved result equals the preview when nothing else changed.

- **AC-VER-10.** Given a PM re-baselines a deliverable twice, then B1 and B2 both remain with dates, owner, estimate, remaining hours and reasons. The panel shows the change since B2 in hours, days and owner. "Use original dates" keeps both records.

- **AC-VER-11.** Given a project convention of A, B, C …, when a deliverable is issued as "Rev 1", then the issue is saved and marked "Non-standard revision: expected A, B, C …". When it is later issued as B after C, it is marked "Out of sequence". Status, health and attention are unchanged.

- **AC-VER-12.** Given each flag of FR-VER-18 is off, then existing API tests and browser suites pass unchanged, and responses, reports and exports carry no new fields.

### 52.9 Decisions

Each decision below uses its default until Jay decides otherwise. The rows are also in `specs/DECISIONS.md` as 048-D1 to 048-D10.

| # | Decision | Default used |
|---|---|---|
| D1 | Approve this amendment's scope | Approved by Jay 2026-10-18 with the listed defaults; Jay also confirmed that allocation previews use existing capacity warnings and keep Overlapping Plans and Deliverable Pressure deferred |
| D2 | Chain granularity | One chain per project plus one organisation chain |
| D3 | Where the head hash is kept off the database | In the nightly off-host backup manifest of packet 044 |
| D4 | May `flag.verifiable_log` be switched off once on? | No; switching off needs a recorded decision, and the chain then records a "Chain paused" entry |
| D5 | Withdrawn-after-capture commitments in the denominator | Yes (AC-RDY-04) |
| D6 | Reliability bands | Good ≥ 80 %, Watch ≥ 60 %, Low below |
| D7 | Default miss categories | Missing input, Decision delay, Changed scope, Unavailable capacity, Technical problem, Other |
| D8 | Met confirmation | Off by default; PM or Discipline Lead of the work confirms |
| D9 | Revision presets | Letters, numbers, and P/C two-stage with two digits |
| D10 | How many flags? | One per part, as §52.7 lists |
