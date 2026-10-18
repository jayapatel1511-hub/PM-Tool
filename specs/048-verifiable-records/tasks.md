# Tasks: Verifiable Records

Each slice is one PR. It merges dark behind its §52.7 flag and adds that flag's pair test (off: identical to before; on: present), which covers AC-VER-12 for its area. Every slice needs packet 039's flags slice (S09) first. Sizes are effort only.

## Part A: verifiable activity log

- [ ] S01 Chain columns and write (M, Tier B: migration and `HubDb.cs`).
  - Pure `AuditChain.Hash` (RFC 8785 canonical JSON, SHA-256) in `Hub.Domain`.
  - `chain_id`, `chain_seq`, `prev_hash` and `row_hash` on `ActivityLog`, the `ActivityChainHead` table, and a unique `(chain_id, chain_seq)` index.
  - `HubDb.SaveChangesAsync` takes the per-chain advisory lock, assigns sequences and hashes before the base save, and fails the save if any step fails.
  - Flag: `flag.verifiable_log`.
  - Covers FR-VER-01, FR-VER-02, FR-VER-03, FR-VER-18; AC-VER-01, AC-VER-03, AC-VER-12.
  - Tests:
    - `tests/Hub.Tests/Domain/AuditChainTests.cs`: canonical bytes are stable across field order; every stored field changes the hash; display names are excluded; sequence 1 uses zero bytes.
    - `tests/Hub.Tests/Api/AuditChainWriteTests.cs`:
      - 50 parallel edits in one project give gap-free sequences;
      - two projects don't block each other;
      - an org-level row goes to the org chain;
      - a forced log-write failure rolls back the change;
      - no client header alters actor or time;
      - flag pair.
- [ ] S02 Verify and nightly job (S).
  - `GET /admin/audit-chains`, `GET /admin/audit-chains/{id}/verify` and `GET /projects/{id}/activity/integrity`.
  - Nightly checkpoint job and weekly full job.
  - On a break: one Admin attention item and one email.
  - Covers FR-VER-04; AC-VER-02, AC-VER-04.
  - Tests in `tests/Hub.Tests/Api/AuditChainVerifyTests.cs`:
    - a tampered `changes`, reason or time each report the right `first_broken_seq`;
    - a deleted row reports a break;
    - no repair happens;
    - attention and email fire once per break, not once per night;
    - a record's head hash detects later tampering;
    - non-Admins get 403 on Admin routes.
- [ ] S03 Backfill (S). After S01.
  - Idempotent job that chains existing rows per chain in `occurred_at, id` order, then writes "Chain started" with the count.
  - New writes wait on the chain lock while it runs.
  - Covers FR-VER-06; AC-VER-05.
  - Tests in `AuditChainVerifyTests.cs`:
    - 10,000 seeded rows chain and verify;
    - re-running changes nothing;
    - writes during the backfill keep order;
    - the "Chain started" entry carries the count.
- [ ] S04 Chain UI (S). After S02.
  - Status line in Activity History and the project Activity Log report.
  - Admin chain page with verify and the verification-record download (JSON and CSV), each logged.
  - New mocked browser suite, wired into `web/package.json` and `tools/ci_steps.txt` (§43.5).
  - Covers FR-VER-05; AC-VER-02, AC-VER-04.
  - Tests in `web/tests/verifiable-records.cjs`, scenario "chain":
    - verified and failed lines;
    - the line is hidden for people who can't read the log;
    - download contents;
    - axe clean.

## Part B: commitment reliability

- [ ] S05 Reliability server (M, Tier B: migration and permissions).
  - Percent, band, not-yet-recorded and awaiting counts on the snapshot summaries.
  - The `commitment_reliability_good_pct`, `commitment_reliability_watch_pct`, `commitment_trend_weeks` and `commitment_met_confirmation` settings.
  - Miss-category reference table with retire, and a required category on Not Met while the flag is on.
  - Confirm and return actions, with the server rule that the confirmer is not the performer.
  - "Why commitments were missed" catalogue report (CSV and XLSX).
  - Flag: `flag.commitment_reliability`.
  - Covers FR-VER-07, FR-VER-08, FR-VER-09, FR-VER-10, FR-VER-11, FR-VER-18; AC-VER-06, AC-VER-07, AC-VER-08, AC-VER-12.
  - Tests:
    - `tests/Hub.Tests/Api/CommitmentReliabilityTests.cs`:
      - the AC-VER-06 figures;
      - C = 0 gives "—";
      - rounding half up;
      - withdrawn stays in the denominator;
      - the response holds no per-person figure;
      - category required and relabelled by ID;
      - a retired category can't be chosen but stays in history;
      - with confirmation on: the performer is refused, the Discipline Lead and PM are allowed, awaiting is not counted, return needs a reason;
      - with the setting off, behaviour is as today;
      - report export logged;
      - flag pair.
    - `tests/Hub.Tests/Api/WeeklyCommitmentWeekTests.cs`: unchanged.
- [ ] S06 Reliability UI (S). After S05.
  - Figure with band text on the readiness screen; withdrawn and not-yet-recorded lines.
  - Category picker on Not Met; confirm and return actions.
  - Trend and miss table, each count opening its list.
  - Covers FR-VER-07, FR-VER-09, FR-VER-10, FR-VER-11; AC-VER-06, AC-VER-07, AC-VER-08.
  - Tests in `web/tests/verifiable-records.cjs`, scenario "reliability":
    - figure text and band label, not colour alone;
    - picker required;
    - confirm hidden for the performer;
    - axe clean.

## Part C: allocation preview

- [ ] S07 Allocation preview (S). After packet 041 S01 and S02.
  - `preview=true` on allocation create, edit and confirm: same validation, rolled-back transaction, the planner read returns the affected person-weeks. Capacity, planned, free, load band and existing capacity warnings are included; Overlapping Plans and Deliverable Pressure stay deferred (Jay, 2026-10-18), without fabricated indicator values.
  - "After this change" line in the allocation form and planner edit, refreshed after a short pause in typing.
  - Flag: `flag.allocation_preview`.
  - Covers FR-VER-12, FR-VER-13, FR-VER-18; AC-VER-09, AC-VER-12.
  - Tests:
    - `tests/Hub.Tests/Api/AllocationApiTests.cs`:
      - the AC-VER-09 figures;
      - preview writes no allocation, log row or notification;
      - validation errors are the same as the save's;
      - save equals preview without concurrency;
      - a concurrent change yields different saved figures;
      - FR-CAP-07 reason still required;
      - flag pair.
    - `web/tests/verifiable-records.cjs`, scenario "preview": the line updates and the Over band is shown as text.

## Part D: baseline effort and owner

- [ ] S08 Baseline history (S, Tier B: migration). After packet 046 S07 and S08.
  - `DeliverableBaseline` table written by the re-baseline command.
  - "Use original dates" keeps records.
  - Change-since-baseline in the panel, register export and reports; baselines list.
  - Flag: `flag.baseline_history`.
  - Covers FR-VER-14, FR-VER-15, FR-VER-18; AC-VER-10, AC-VER-12.
  - Tests:
    - `tests/Hub.Tests/Api/ScheduleBaselineTests.cs`:
      - two re-baselines keep B1 and B2 with all fields;
      - records can't be edited or deleted;
      - clearing keeps records;
      - the delta in hours, days and owner;
      - milestones keep dates only;
      - export column added only with the flag on.
    - `web/tests/verifiable-records.cjs`, scenario "baselines".

## Part E: revision-code check

- [ ] S09 Revision check (S).
  - Pure `RevisionConvention` matcher and order for the three presets and custom lists.
  - Project setting; marks computed on read for deliverables and issue history; register filter.
  - Re-check job writes one log entry with counts.
  - Flag: `flag.revision_check`.
  - Covers FR-VER-16, FR-VER-17, FR-VER-18; AC-VER-11, AC-VER-12.
  - Tests:
    - `tests/Hub.Tests/Domain/RevisionConventionTests.cs`: each preset, a custom list, out-of-sequence ordering, case and whitespace handling.
    - `tests/Hub.Tests/Api/MilestonesDeliverablesTests.cs`:
      - a non-standard issue is saved and marked;
      - status, health and attention are unchanged;
      - clearing the convention removes marks;
      - a convention change logs one entry;
      - flag pair.
