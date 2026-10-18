# Engineering Project Coordination Hub

This repository holds the product specification, UI mockups, and Spec Kit setup for the Hub, an
internal web app for coordinating multidisciplinary engineering projects. Application code is present; packets 025–027 have foundation implementations with passing automated checks; full acceptance and packets 028–034 remain pending. See each packet's verification record.

## Sources of truth

- `Engineering-Project-Coordination-Hub-Specification.md`: product behaviour. Section 10 wins
  over later sections. It is generated: edit `spec-parts/`, then run
  `python3 tools/build_spec.py` (needs pandoc) to rebuild the combined Markdown and HTML;
  `--check` reports stale files.
- `docs/reference/coordination-hub-v2/`: the only downloaded UI prototype to use for visual
  cues. It is not a design specification, source of release scope, or application code. The
  six-panel image records confirmed feature scope; older wireframes are historical detail.
  The product specification and packets govern behavior and acceptance.
- `.specify/memory/constitution.md`: the rules every change follows.
- `specs/`: the whole specification as Spec Kit packets 001 to 048, indexed in `specs/README.md`.
  Open product and process decisions, each with a default, are in `specs/DECISIONS.md`.
  `specs/TRACEABILITY.md` maps every specification ID to its packets; regenerate it with
  `python3 tools/trace_spec.py` (`--check` fails on any gap).

## How work happens

Spec Kit packets, as described in `docs/SPEC-KIT-WORKFLOW.md`: specify, plan, tasks, analyze,
implement, converge. Always name the packet directory explicitly. Packet 039
(`specs/039-delivery-process`, §43) defines the delivery process: spec pull request then slice
pull requests, flags, merge tiers and verification.

## Rules

- No AI, machine-learning, or LLM features in the product, in any phase.
- Every item has exactly one accountable owner; use the canonical names from section 10 verbatim.
- A planning request is not permission to write application code. Implement only the tasks
  named in the request, and record checks in the packet's `verification.md`.
- Do not commit, push, open issues, deploy, or start sub-agents unless Jay asks.

## Delivery process rules (packet 039)

- Work from `origin/main` on one `a/<packet>-<slice>` branch with disjoint file ownership. At most five agents write
  concurrently; one schema-lane writer and one separate reviewer agent serve each pull request.
- Tier A changes are dark additions, guards, tests, documentation, unchanged-test refactors, added lines in
  `tools/ci_steps.txt`, and patch or minor dependency updates. Tier B includes specification and decision files,
  `.github/`, hosting, homedev scripts, auth and permissions, migrations, approved behaviour and `large` or
  `replace-test` changes. Jay applies `tier-b-ok` and merges Tier B.
- Agents never apply `tier-b-ok`, `contract`, `large` or `replace-test`, change decision status, turn on flags, deploy,
  or delete branches/worktrees. The boy-scout rule stays inside files already changed by the slice.
- Hostnames and origins are environment-specific; verify the current target before treating one as hosted evidence.
