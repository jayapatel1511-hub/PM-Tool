# Verification: Delivery Process

Slice S01 built and merged into the integration branch; other slices not started.

Per FR-DLV-24, this file records checks until `trace_spec.py --report` (slice S11) is published on main; after that, this packet's verification is the report of the CI run for the deployed commit, and human checks go in `acceptance.md`.

| AC | Slice | Status |
|---|---|---|
| AC-DLV-01 | S05 | UNRUN |
| AC-DLV-02 | S01 | PARTIAL: passes locally against a temporary git repo (350 hand-written lines plus an 11,000-line Designer file pass with no warning, 450 warns and passes, 1,050 fails, 1,050 with label `large` passes); not yet run in GitHub CI on a real pull request |
| AC-DLV-03 | S01 | PASS (local): `git check-attr` reports `linguist-generated` set and `diff` unset for a Designer file, the snapshot, both combined spec files, `TRACEABILITY.md` and `package-lock.json`; nothing else in `git ls-files` is marked |
| AC-DLV-04 | S09 | UNRUN |
| AC-DLV-05 | S09 | UNRUN |
| AC-DLV-06 | S09 | UNRUN |
| AC-DLV-07 | S09 | UNRUN |
| AC-DLV-08 | S09 | UNRUN |
| AC-DLV-09 | S09 | UNRUN |
| AC-DLV-10 | S02 | UNRUN |
| AC-DLV-11 | S02 | UNRUN |
| AC-DLV-12 | S03 | UNRUN |
| AC-DLV-13 | S07 | UNRUN |
| AC-DLV-14 | S04 | UNRUN |
| AC-DLV-15 | S08 | UNRUN |
| AC-DLV-16 | S14 | UNRUN |
| AC-DLV-17 | S14 | UNRUN |
| AC-DLV-18 | S15 | UNRUN |
| AC-DLV-19 | S11 | UNRUN |
| AC-DLV-20 | S12 | UNRUN |
| AC-DLV-21 | S10 | PASS (local) |
| AC-DLV-22 | S13 | UNRUN |
| AC-DLV-23 | S16 | UNRUN |
| AC-DLV-24 | S17 | UNRUN |
| AC-DLV-25 | S06 | UNRUN |
| AC-DLV-26 | S18 | UNRUN |
| AC-DLV-27 | S13 | UNRUN |

## Build 039-S01

Checks on the merged tree: `python3 tools/test_pr_checks.py` 8/8 pass; `dotnet build Hub.slnx` 0 errors; `python3 tools/trace_spec.py --check` clean; `python3 tools/build_spec.py --check` up to date; `pr_checks.py size --base HEAD^1` on the slice commit: 218 hand-written lines, pass. No flag: CI tooling only, no product behaviour change. Web not changed, so web build, lint and browser suites were not run.

Assumptions:
- 039-D4 default: warn above 400 and fail above 1,000 hand-written lines, label `large` overrides; exactly 400 and 1,000 do not trigger.
- CI base is `HEAD^1` of the pull request merge commit, with checkout `fetch-depth: 2`.
- The `pull_request` trigger gains `labeled` and `unlabeled` so the `large` label takes effect; this re-runs the whole serial job on any label change until S02 splits CI into jobs.
- Tests use `contextlib.chdir` (Python 3.11 or later; the Mac and `ubuntu-latest` meet this).

Left for Jay:
- Confirm the size step and the `large` label on the first real GitHub CI pull request run.
- Make the size check a required status check on protected main (S05).
- Create the `large` label in the repository.

## Build 039-S10

Checks on the merged tree: `python3 tools/test_decisions.py` 8/8 pass (the validator also caught three deliberate log edits in the builder run: a dropped source row, a renamed decision ID, a status left as "Open"); `dotnet build Hub.slnx` 0 errors; `python3 tools/trace_spec.py --check` clean; `python3 tools/build_spec.py --check` up to date. No flag: decision log and tooling only, no product behaviour change. Web not changed, so web build, lint and browser suites were not run.

AC-DLV-21: PASS (local). `tools/test_decisions.py` checks unique IDs in the three allowed forms, the four statuses, filled Last change, Flag and Source, each source row (036 verification section 14, 037 verification section 8, the release-candidate handoff behaviour list) present exactly once, and a row for every decision in packet 039+ `spec.md` files.

Assumptions:
- Status mapping per FR-DLV-26: Open became default-active, Decided and Done became confirmed, Deferred stayed deferred. No open row was changed; all keep their defaults.
- Packet 036 already uses 036-D1 to 036-D18 for defects, so new decision rows are 036-D20 to 036-D28; packet 037 rows are 037-D1 and 037-D2.
- The 59 rows identical in 036 section 14 and 037 section 8 are listed once ("036 §14 and 037 §8"). Non-behaviour rows are grouped into "Accepted as built" rows; the 5 handoff behaviour rows (A5, A6, F1, E3, the reading behind 036-D2) and the visual changes B3, F14 and M8 each have their own row.
- The handoff's partial-view follow-up (036-D25) and proposed S3 defect severities (036-D26) became default-active rows.
- "Release-candidate handoff behaviour rows" means the list of earlier rows that change behaviour in `docs/handoffs/2026-10-12-packets-036-037-release-candidate.md`.
- Flag column: the flag a row names; else the packet's single flag; else "the packet's flags (NNN spec.md)"; "none" for RPT rows, deferred rows, and packets 036 to 040 and 044.
- Last change: the date in the decision text, else git blame; new rows dated 2026-10-18.
- "Written under FR-DLV-21" means packets 039 onwards; decisions read from each spec.md Decisions section (table rows, bullets, or ranges).
- A third-party product name was removed from the 042-DEF1 question.

Left for Jay:
- Confirm or overrule new rows 036-D20 to 036-D28 and 037-D1 to 037-D2 (all default-active).
- Add `python3 tools/test_decisions.py` to `.github/workflows/ci.yml` (with S02 or by Jay; Tier B file).
