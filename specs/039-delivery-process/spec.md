# Feature Specification: Delivery Process

**Packet**: 039 | **Feature directory**: `specs/039-delivery-process` | **Date**: 2026-10-14
**Status**: Specified, not started.
**Input**: Jay's 2026-10-14 instruction to build everything in the build-approach report (kept outside the repository): "We are building everything you suggested." Current features are fixed and additive only. This packet is the report's delivery-process section in full.
**Source**: product specification §43 (§43.1 to §43.14) in `spec-parts/18-delivery-process.md`. It adds to §21 (dependency management), §22 (quality gates), §23.4 (feature flags), §23.7 (CI/CD and configuration) and §24.6 (migrations), and records Q15 (GitHub Actions) as decided. On Jay's direction the constitution's Development Workflow section is amended to version 1.1.0 (D11).

## User outcome

Jay touches each packet three times: he approves the spec, answers its decisions and turns its flag on after looking at it on review. Routine pull requests merge themselves when CI is green, and every green main image reaches the review stack without his sudo. Users see no change: no feature, screen, route, report, setting or rule is removed, hidden, narrowed, frozen or put behind a flag.

## Scope

1. **Trunk and branches** (§43.2): protected main, squash merges, one slice per branch, red main stops the line, and a one-time inventory of branches and worktrees with archive refs before any deletion. FR-DLV-01, FR-DLV-02, FR-DLV-03, FR-DLV-04.
2. **Pull request size** (§43.3): `.gitattributes` for generated files, a size check and a pull request template. FR-DLV-05, FR-DLV-06.
3. **Feature flags** (§43.4): `Flags.cs` feeding `flag.*` settings on the `rule_enabled.*` pattern, flags on `/me`, a server guard, retirement and pair tests. Existing features are never flagged; an unflagged change users can see is listed with a decision row and merged as Tier B. FR-DLV-07, FR-DLV-08, FR-DLV-09, FR-DLV-10.
4. **CI** (§43.5): parallel jobs, three API shards, one step list outside `.github/`, coverage merged by file and line, and a test-count ratchet. FR-DLV-11, FR-DLV-12, FR-DLV-13.
5. **Release image** (§43.6): built once by CI and pushed to GHCR with the build stamp and a provenance attestation. FR-DLV-14.
6. **Migrations** (§43.7): a migration lint, expand and contract, and a rollback-compatibility job that runs the previous image on the new schema. FR-DLV-15, FR-DLV-16, FR-DLV-17.
7. **Deployment** (§43.8): a root-owned review-only deploy timer, manual pilot promotion by digest, manual production, and the rollback order. FR-DLV-18, FR-DLV-19, FR-DLV-20.
8. **Specification records** (§43.9): packet shape v2, `trace_spec.py --report`, the AC-to-test ratchet with a HUMAN-ONLY list, a one-page `acceptance.md` with a transition clause, the records budget and derived status. FR-DLV-21, FR-DLV-22, FR-DLV-23, FR-DLV-24, FR-DLV-25.
9. **Decision log** (§43.10): `specs/DECISIONS.md`, including the deferred and parked items with their triggers. FR-DLV-26.
10. **Agents and merge tiers** (§43.11): roles, Tier A automatic merge, Tier B owner merges and the boy-scout rule. FR-DLV-27, FR-DLV-28, FR-DLV-29.
11. **Regression net and dependencies** (§43.12): the nightly real-API run of the local-only browser suites and grouped dependency updates. FR-DLV-30, FR-DLV-31.

Section boundaries are in §43.1.

## Non-goals

- Any change to product behaviour. The only visible additions are a Flags group on Admin settings, shown once a flag is declared, and a `flags` field on `/me`.
- Automatic deployment to pilot or production; release branches; a merge queue; per-PR preview stacks; a deployment controller run as root; any new service.
- Per-user or per-role flag targeting, exposure logging, experiments, or flags inside entity payloads.
- Rewriting earlier packets' records; lowering a coverage threshold; dropping a test, suite or check.
- The build shown in `/health`, the `X-Hub-Build` header and the reload message (§46, packet 042); the typed contract (§44, packet 040); off-host backup and outbox recovery (§48, packet 044).
- The read-sync sidecar and the TypeScript port, deferred behind the triggers in §43.14 and `specs/DECISIONS.md`. Parked items that need a §30 or constitution change are recorded there, not built. AI features stay excluded (constitution II).

## Decisions (defaults apply until Jay decides)

| # | Decision | Default |
|---|---|---|
| D1 | Registry package visibility | Private; read-only token root-owned on homedev |
| D2 | Machine account for agents | None; Tier B is a label control |
| D3 | Branch drift limit | 50 commits on main since the branch started |
| D4 | Size limits | Warn above 400, fail above 1,000 hand-written lines |
| D5 | Review timer interval | 5 minutes |
| D6 | Review-proven for pilot | Probes passed on review, the soak period (default 2 days) on review, and one green nightly on the commit; Jay promotes when he chooses |
| D7 | Flag retirement | After pilot sign-off in `acceptance.md`; no date |
| D8 | Who sees flag states | Every signed-in user via `/me`; packet on Admin only |
| D9 | Branch coverage across shards | Highest per-line count |
| D10 | Nightly comparing checks | Compare with the last green nightly commit |
| D11 | Constitution amendment | Decided: adopted as constitution 1.1.0; in effect when Jay merges this spec pull request |
| D12 | Timer installation | Jay installs once with sudo when he chooses; manual activation until then |
| D13 | Major dependency updates | Tier B |
| D14 | Deployed-commit record | Container revision label and the timer state file; `current` means the last manual activation |
| D15 | Dockerfile argument shared with §46 | `RELEASE_SHA` to `HUB_BUILD`; packet 042 S01 adds it; S07 adds it only if absent |
| D16 | `plan.md` in every packet | Kept, short (constitution 1.1.0); the report proposed it only under VII |

## Acceptance

- Trunk and size: AC-DLV-01 (protection readback), AC-DLV-02 (size limits), AC-DLV-03 (generated attributes), AC-DLV-25 (inventory without loss).
- Flags: AC-DLV-04 (definitions), AC-DLV-05 (`/me` unchanged with no flags), AC-DLV-06 (server guard and client hook), AC-DLV-07 (retirement), AC-DLV-08 (pair check), AC-DLV-09 (nothing existing flagged).
- CI: AC-DLV-10 (shards), AC-DLV-11 (coverage merge), AC-DLV-12 (test counts).
- Image and migrations: AC-DLV-13 (image and attestation), AC-DLV-14 (lint), AC-DLV-15 (rollback compatibility).
- Deployment: AC-DLV-16 (timer deploy), AC-DLV-17 (timer refusals and rollback), AC-DLV-18 (pilot by digest, build path unchanged).
- Records: AC-DLV-19 (report), AC-DLV-20 (AC ratchet), AC-DLV-21 (decision log).
- Agents and regression net: AC-DLV-22 (tiers), AC-DLV-27 (rule files), AC-DLV-23 (nightly), AC-DLV-24 (grouped updates).
- End to end: AC-DLV-26 (the first packet after this one travels the whole path).

Human-only candidates for `specs/human-only.txt`: AC-DLV-01, AC-DLV-16 on homedev, AC-DLV-25, AC-DLV-26 and AC-DLV-27.

## Constitution Check (summary)

PASS. The standing requests for Tier A automatic merge and review-stack deployment, the generated verification with `acceptance.md`, the decision log, the boy-scout rule and the agent rules are in the Development Workflow amendment Jay directed (constitution 1.1.0, D11), which takes effect when he merges this packet's spec pull request. Principles I to VII are unchanged. Details in [plan.md](plan.md).

## Depends on

- PR #37 merged to main as the one line of record, with one last manual activation of merged main by Jay.
- Jay's merge of this packet's spec pull request, which brings the constitution 1.1.0 amendment into effect (D11), before slices S13 and S14 ([tasks.md](tasks.md)). Jay's sudo once for the timer (D12) before S14's homedev check.
- Packet 042 slice S01 owns the `RELEASE_SHA` argument and the compose wiring (D15).
- Slice order inside this packet is in [tasks.md](tasks.md). Packets 040 to 047 use the flags of S09 and the process of S11 to S13 as they land; none waits for the review timer.

## Flag name

None. This packet changes how work is delivered and ships no user-facing addition, so nothing is dark. `Flags.cs` starts empty.

## Wording rule

Code, interface strings, specification text, commit messages and this packet's records name no third-party product.
