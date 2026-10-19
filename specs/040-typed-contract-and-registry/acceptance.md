# S16: five-feature measurement

**Status: NOT ACCEPTED.** The current integration line contains source for five later packet lanes, but the evidence does not establish five completed and verified features built on the platform. No median, ratchet waiver, or stop-rule conclusion is valid.

## Evidence boundary

The review target is the current integration checkout:

```text
Path: /Users/jaypatel/.codex/worktrees/integrate-039-050/PM-Tool
HEAD: 803116b30cdc29f3090f2da1c610308b7e445c8e
```

The checkout was already dirty when reviewed (`tests/Hub.Tests/Api/*` modifications, `web/src/gen/openapi.json`, and untracked `tools/schema-compat.py`). Counts below are therefore commit-diff evidence, not a claim that the dirty working tree is a releasable tree.

At the inspected integration commit, packet 040’s copied verification remains `Not started`, with AC-PLAT-01 through AC-PLAT-21 `UNRUN`. The later c84ba94f handoff reconciles that stale opening and provides local checks; this review does not treat stale prose as proof that implementation is absent. The inspected commit contains the typed-registry source commit `46dc7685`; stale verification prose does not negate that source evidence.

## Reproducible source measurement

The source counts use the actual integration commit diff and exclude test paths and generated client/shape artifacts:

The exact included paths and counts are saved in `evidence/fifth-feature-source-counts.json`. The count uses each named commit against its first parent, includes product source under src/, web/src/, scripts/, tools/ and hosting/, and excludes generated client files, migrations, build outputs, snapshots and test files (including tools/test_* and scripts/test_*). These lane-level unions cannot substitute for per-feature attribution.


Observed product-source counts:

| Integration commit | Packet lane | Product source files changed | Boundary |
|---|---|---:|---|
| `46dc7685` | 040 typed registry/contract foundations | 211 | Excludes tests and `web/src/gen`; the copied verification opening is stale at this commit; c84ba94f supplies the corrected local record. |
| `83c308b3` | 044 operations/security | 25 | Commit includes delivery/039 overlap; 044 verification has both local PASS rows and many UNRUN rows. |
| `40c97456` | 045 recovery/counts + 046 task/timeline | 45 | One combined commit; cannot honestly split the source files between 045 and 046 without inventing attribution. Both packet verifications retain partial/schema-blocked gates. |
| `0aff927b` | 041 capacity/server values + 047 planning/reporting | 72 | One combined commit; cannot honestly split the source files between 041 and 047. 047 explicitly says API execution is blocked and “This is not acceptance.” |

These are handwritten product-source diff counts, not feature-completion counts. Tests, generated files, snapshots, and verification prose are deliberately excluded. The counts also overlap packet seams and do not prove that a packet is complete.

## Packet evidence and qualification

| Packet | Actual source evidence | Verification boundary | S16 qualification |
|---|---|---|---|
| 041 | `0aff927b` cherry-pick contains capacity and permission source; 041 verification records local PASS for selected AC-FIG rows. | AC-FIG-08, AC-FIG-12, AC-FIG-13 are UNRUN; S08 generated-contract/ratchet work is blocked on 040; flag removal remains pending. | **UNPROVEN** |
| 044 | `83c308b3` contains operations/security source; verification records local PASS for selected idempotency, headers, environment, admin-sweep, and wording checks. | AC-OPS-01–05, 08–11, 18, 19, 21 remain UNRUN; other rows are partial or local-only. | **UNPROVEN** |
| 045 | `40c97456` contains recovery/counts source and mocked evidence. | Verification retains partial API/schema gates and AC-RCV-13 is UNPROVEN because the packet 036 live count suite was not run. | **UNPROVEN** |
| 046 | `40c97456` contains task/timeline source and interaction browser evidence. | Verification says schema integration and acceptance remain pending; API cases are blocked by EF `PendingModelChangesWarning`, and every AC-INT row is PARTIAL. | **UNPROVEN** |
| 047 | `0aff927b` contains capacity/reporting/onboarding source shared with 041. | Verification explicitly says local implementation only, API execution blocked by the combined migration, runtime tests authored/compiled but not executed, and “This is not acceptance.” | **UNPROVEN** |

The five packet lanes are concrete candidates for the required measurement, but **zero of these five packet-level candidates is established here as a completed measured platform feature** from the available evidence. This is not a claim that the source is absent; it is a claim that completion proof is absent.

## Criteria required before FR-PLAT-21 can be accepted

Each of the five packet lanes needs, on the final integrated commit:

1. A complete local Release build and the packet’s required API/domain/browser checks, with counts and exit status recorded.
2. Runtime API execution on the combined schema, replacing `PendingModelChangesWarning`/migration-blocked results.
3. Packet verification rows changed from `UNRUN`, `PARTIAL`, `BLOCKED`, or `UNPROVEN` to evidence-backed PASS where required.
4. The 040 typed registry/contract and generated outputs refreshed and checked on the same tree; 041 and 047 currently identify this as an integration dependency.
5. A reproducible final source identity for each measured feature, excluding tests/generated files from the handwritten-file count; unrelated dirty work need not be discarded.

Until those checks exist, the five-feature median is **invalid** and the ratchet stop rule remains **UNPROVEN**. No Jay ruling or waiver is inferred.

## Defect-sweep comparison

Packet 036 baseline: about **35 handwritten files**, **18 sweep defects**, including **7 seam defects** (verification.md:379–396). None of the candidate lane diffs above carries an attributable feature-by-feature defect sweep comparable with that baseline. The sweep count for each of 041, 044, 045, 046 and 047 is **UNPROVEN**, not zero. Their median is not calculated. Review findings and passing automated cases are not substituted for a complete sweep.

This record supplies concrete source unions and the exact completion/attribution gap for Jay’s eventual ruling. It does not waive any of the six separately failing combined ratchet counters, authorize more long-tail conversion, or set decision040-D8/AC-PLAT-21 Accepted.
