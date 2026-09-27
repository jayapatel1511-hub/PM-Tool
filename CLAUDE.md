# Engineering Project Coordination Hub

This repository holds the product specification, UI mockups, and Spec Kit setup for the Hub, an
internal web app for coordinating multidisciplinary engineering projects. Application code is present; packet 025 now has a handoff foundation implementation; its final acceptance and packets 026–033 remain pending. See the packet verification record.

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
- `specs/`: the whole specification as Spec Kit packets 001–033, indexed in `specs/README.md`.
  `specs/TRACEABILITY.md` maps every specification ID to its packets; regenerate it with
  `python3 tools/trace_spec.py` (`--check` fails on any gap).

## How work happens

Spec Kit packets, as described in `docs/SPEC-KIT-WORKFLOW.md`: specify, plan, tasks, analyze,
implement, converge. Always name the packet directory explicitly.

## Rules

- No AI, machine-learning, or LLM features in the product, in any phase.
- Every item has exactly one accountable owner; use the canonical names from section 10 verbatim.
- A planning request is not permission to write application code. Implement only the tasks
  named in the request, and record checks in the packet's `verification.md`.
- Do not commit, push, open issues, deploy, or start sub-agents unless Jay asks.
