# Plan: Delivery Process

Implement §43 (FR-DLV-01 to FR-DLV-31) with defaults D1 to D16 under Jay's 2026-10-14 instruction. Start after PR #37 is merged to main. Each slice in [tasks.md](tasks.md) is one pull request from `origin/main`. Nothing here changes product behaviour.

## Approach

Reuse before adding. Flags reuse the settings registry, seeding, audit and Admin page. CI keeps every current step and only splits them. The image job builds the Dockerfile that exists. The deploy timer is the activation script's sequence (dump, `up -d --no-build`, probes) without the build, installed root-owned like the backup timer. The rollback job and the nightly job reuse the review scripts and the preview seeders and guard as they are. `trace_spec.py` gains modes; `--check` is untouched. New Python checks share one file, `tools/pr_checks.py`, and CI test helpers share one file, `tools/ci_tests.py`, each with one stdlib unittest file, in the style of `tools/test_pilot_operations.py`.

## File boundaries

| Area | Files (new files marked +) |
|---|---|
| Size, tier and migration checks | + `.gitattributes`; + `.github/pull_request_template.md`; + `tools/pr_checks.py`; + `tools/test_pr_checks.py`; `.github/workflows/ci.yml` |
| CI split and counts | `.github/workflows/ci.yml`; + `tools/ci_steps.txt`; `tools/coverage_gate.py`; + `tools/ci_tests.py`; + `tools/test_ci_tests.py`; + `tools/test_coverage_gate.py` |
| Release image and rollback job | `hosting/Dockerfile.review` (build argument only, if packet 042 S01 has not added it, D15); `.github/workflows/ci.yml` |
| Flags | + `src/Hub.Domain/Flags.cs`; `src/Hub.Domain/Settings.cs` (one generated range, a `Flags` map in `OrgSettings.From`); `src/Hub.Api/Features/Me.cs` (one field); `src/Hub.Api/Features/Admin.cs` (packet on flag rows); `src/Hub.Api/Infrastructure/Http.cs` (one guard); `web/src/lib/auth.tsx` (`Me.flags`, one hook); `web/src/pages/admin/Admin.tsx` (flag label); `web/src/i18n/en.ts`; + `tests/Hub.Tests/Domain/FlagsTests.cs`; + `tests/Hub.Tests/Api/FlagsApiTests.cs` |
| Records | `tools/trace_spec.py`; + `tools/test_trace_report.py`; + `specs/_templates/` (`spec.md`, `plan.md`, `tasks.md`, `acceptance.md`); + `specs/human-only.txt`; `specs/DECISIONS.md` (extended; the spec pull requests start it); + `tools/test_decisions.py`; `specs/README.md` (one pointer line) |
| Agent rules | `CLAUDE.md`; + `AGENTS.md` (new tracked file carrying over the existing hostname rule); `docs/SPEC-KIT-WORKFLOW.md`; + `docs/runbooks/delivery.md` |
| Deployment | + `hosting/pm-tool-review-deploy-root.sh`; + `hosting/pm-tool-review-deploy.service`; + `hosting/pm-tool-review-deploy.timer`; `scripts/activate-homedev-review.sh` (new optional install flag only); `scripts/activate-homedev-pilot.sh` (new optional `--image-digest` only); `docs/runbooks/homedev-review.md`; + `tools/test_review_deploy.py`; `tools/test_pilot_operations.py` |
| Nightly and dependencies | + `.github/workflows/nightly.yml`; `.github/dependabot.yml`; + `.github/workflows/dependency-merge.yml` |

Not touched: `tools/preview/preview_target.py`, `hosting/homedev.compose.yml` and `hosting/homedev-pilot.compose.yml` (packet 042 S01 wires them), `scripts/verify-homedev-review.py`, `scripts/init-homedev-review.py`, `.specify/templates/`, every migration, every existing test and browser suite.

## Constitution Check

| Principle | Result |
|---|---|
| I. Coordination First | PASS. No product feature changes; delivery serves the existing scope. |
| II. Deterministic and Explainable | PASS. No AI. Flags are plain organisation settings; no computed status changes. |
| III. One Accountable Owner | PASS. Unchanged. |
| IV. Specification Is the Source of Truth | PASS. §43 is the contract; IDs, `build_spec.py` and `trace_spec.py --check` stay. Two lines of §23.4 and §23.7 are amended by request. |
| V. Traceable by Construction | PASS. Flag changes use the existing setting audit (`src/Hub.Api/Infrastructure/Audit.cs:48`); every deploy dumps first; no volume is deleted; no down migration runs on review or pilot. |
| VI. Secure by Default | PASS. Flags are enforced on the server. Image provenance is verified before any pull. The package token is root-owned and never logged. Tier B covers `Permissions.cs`, `Auth.cs`, `SocialAuth.cs`, `hosting/` and `.github/`. Authentication is unchanged. |
| VII. Simple Enough for a Small Team | PASS with the entry below. No service or library is added; flags reuse settings (`src/Hub.Domain/Settings.cs:88`). |

Jay directed the Development Workflow amendment, adopted as constitution 1.1.0 (D11); it takes effect when he merges this packet's spec pull request, and S13 and S14 follow that merge. D16 records that `plan.md` stays in every packet, as the amendment says.

## Complexity Tracking

| Addition | Why needed | Simpler alternative rejected |
|---|---|---|
| A hosted container registry (GHCR) and a root-owned deploy timer on homedev | The deployed image must be the one CI tested, and review deploys must not wait for Jay's sudo. | Building on homedev at each activation, as today: the tested image is never the deployed one, and every deploy needs sudo. A root-run deployment controller: a public-repository merge would drive a root process with broad Docker control. |

Jay approves this entry with the spec pull request.

## Checks to run

- Each slice: the tests it names in [tasks.md](tasks.md), then the full CI run on GitHub for that pull request.
- Flags slice: full backend suite, every browser suite, coverage gates; `GET /me` and Admin settings responses compared before and after.
- CI split: three consecutive green runs with the same test totals as the last serial run.
- Timer: `tools/test_review_deploy.py` with fake `docker` and `sudo`, then on homedev one real merge and one deliberately failing probe on a throwaway tag.
- Integrator, once: `python3 tools/build_spec.py --check` and `python3 tools/trace_spec.py --check`.
