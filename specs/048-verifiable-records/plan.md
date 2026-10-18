# Plan: Verifiable Records

Implements §52 (FR-VER-01 to FR-VER-18) with defaults D1 to D10. Each slice in [tasks.md](tasks.md) is one PR that merges dark behind its §52.7 flag. Paths are from the repository root.

## Approach

Reuse first. New storage is a few columns and three small tables.

- **Chain** (§52.2):
  - **Columns:** `chain_id`, `chain_seq`, `prev_hash` and `row_hash` on `ActivityLog` (`src/Hub.Api/Data/Entities.cs:734`). The chain ID is the project ID, or a fixed organisation ID.
  - **Writing:** `HubDb.SaveChangesAsync` already collects every log row before `base.SaveChangesAsync` in one transaction (`HubDb.cs:981-1043`). Just before the base save, for each chain touched, it:
    1. takes `pg_advisory_xact_lock(hash(chain_id))`;
    2. reads the chain head;
    3. assigns sequences in insertion order;
    4. computes hashes with a pure `Hub.Domain` function (RFC 8785 canonical JSON, then SHA-256).
  - **Head table:** a `ActivityChainHead(chain_id, head_seq, head_hash, last_verified_seq, last_verified_at)` table keeps head reads O(1) and is updated in the same transaction.
  - **Unique index:** `(chain_id, chain_seq)` turns a race into a failed write, never a silent fork.
- **Verify** (§52.2):
  - The same pure hash function recomputes rows in pages.
  - A job in `Infrastructure/Jobs.cs` runs nightly from the checkpoint and weekly in full.
  - A break raises the existing Admin attention path and email.
  - Activity History reads the head table; the Admin page is a new tab under `web/src/pages/admin`.
- **Backfill** (§52.2): a one-time idempotent job chains existing rows in `occurred_at, id` order per chain, then writes the "Chain started" entry. While it runs, new writes wait on the chain lock, so order is preserved.
- **Reliability** (§52.3):
  - The weekly commitment read already returns `CommittedCount`, `Met` and `Withdrawn` per snapshot (`src/Hub.Api/Features/WeeklyCommitments.cs:236-237`); the slice adds the percent, band, not-yet-recorded count and awaiting count.
  - Miss categories are one small reference table with the existing Admin reference-data pattern, plus a nullable `miss_category_id` on the commitment state change.
  - Confirmation is one state value plus a permission rule in `Permissions.cs`.
  - The report is a catalogue entry in `Reports.cs`.
- **Preview** (§52.4):
  - Allocation create, edit and confirm (`src/Hub.Api/Features/Allocations.cs:44-52`) take `preview`. The handler runs inside a transaction that is rolled back after computing the person-week figures with the planner's existing read.
  - Notifications and log rows are queued in the same transaction, so rollback discards them.
  - Return existing capacity/load warnings only. Overlapping Plans and Deliverable Pressure stay deferred (Jay, 2026-10-18); omit those unavailable indicators rather than returning false or zero.
- **Baselines** (§52.5):
  - One `DeliverableBaseline` table (number, dates, owner, estimate, remaining hours, reason, actor, time), written by packet 046's re-baseline command.
  - The panel and export add the delta and the list.
- **Revision check** (§52.6):
  - A project setting holds the convention.
  - A pure `Hub.Domain` matcher and order function marks rows on read; there are no stored marks, so clearing the convention needs no cleanup.
  - The re-check job only counts and logs.

## File boundaries

| Slice | Files |
|---|---|
| S01 chain columns and write | `src/Hub.Domain/AuditChain.cs` (new), `src/Hub.Api/Data/Entities.cs` (`ActivityLog`, `ActivityChainHead`), `src/Hub.Api/Data/HubDb.cs`, one additive migration, `src/Hub.Domain/Flags.cs` |
| S02 verify and job | `src/Hub.Api/Features/AuditChain.cs` (new), `src/Hub.Api/Infrastructure/Jobs.cs`, `src/Hub.Api/Text.cs` |
| S03 backfill | `src/Hub.Api/Features/AuditChain.cs`, `src/Hub.Api/Infrastructure/Jobs.cs` |
| S04 chain UI | `web/src/pages/projects/ProjectActivity.tsx`, `web/src/pages/admin/Admin.tsx` (new tab), `web/src/i18n/en.ts`, `web/tests/verifiable-records.cjs` (new), `web/package.json`, `tools/ci_steps.txt` (one line) |
| S05 reliability server | `src/Hub.Api/Features/WeeklyCommitments.cs`, `src/Hub.Domain/Settings.cs`, `src/Hub.Api/Data/Entities.cs`, `HubDb.cs`, one additive migration, `src/Hub.Api/Features/Admin.cs` (miss categories), `src/Hub.Domain/Permissions.cs` (confirm rule), `src/Hub.Api/Features/Reports.cs`, `Text.cs` |
| S06 reliability UI | `web/src/pages/projects/Readiness.tsx`, `web/src/pages/projects/ReadinessForms.tsx`, `en.ts` |
| S07 allocation preview | `src/Hub.Api/Features/Allocations.cs`, `src/Hub.Api/Features/Planning.cs` (read reuse), `web/src/pages/projects/Allocations.tsx`, `web/src/pages/Planner.tsx`, `en.ts` |
| S08 baseline history | `src/Hub.Api/Data/Entities.cs` (`DeliverableBaseline`), `HubDb.cs`, one additive migration, `src/Hub.Api/Features/Deliverables.cs`, `src/Hub.Api/Features/ListExports.cs`, `web/src/pages/projects/Deliverables.tsx`, `en.ts` |
| S09 revision check | `src/Hub.Domain/RevisionConvention.cs` (new), `src/Hub.Api/Features/Projects.cs` (setting), `src/Hub.Api/Features/Deliverables.cs`, `web/src/pages/projects/ProjectSettings.tsx`, `web/src/pages/projects/Deliverables.tsx`, `en.ts` |

Tests named per slice live in `tests/Hub.Tests/Domain/`, `tests/Hub.Tests/Api/` and `web/tests/`. Unrelated edits in shared files are kept.

## Risks

- **Write latency:** the per-chain lock serialises writes within one project only. If measured p95 write latency rises by more than 20 ms in the nightly real-API run, revisit D2 before flipping the flag.
- **Database role:** INSERT and SELECT only (§20.3) is still a Recommendation; S01 records whether the deployed role already meets it and, if not, adds the grant change as a separate Tier B step.
