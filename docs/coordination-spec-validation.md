# Coordination specification validation

**Validation date:** 2026-09-27 (UTC)
**Scope:** Documentation and design for packets 025–033.

The documentation checks below passed against the change based on `build-coordination-hub` at `c8737ec53e798510b2e52c9f24a22bece9a7e65f`. They validate the written contract, not runtime behaviour.

| Check | Executed result |
|---|---|
| `python3 tools/build_spec.py` | Rebuilt the combined Markdown and HTML from `spec-parts/`. |
| `python3 tools/build_spec.py --check` | Up to date. |
| `python3 tools/trace_spec.py --check` | 612 IDs and 236 sections; zero uncited IDs, zero uncited sections, zero unknown references. |
| `git diff --check` | Passed without whitespace errors. |
| Packet/manifest consistency inspection | All nine packets have a spec, plan, tasks, verification record and requirements checklist. All 63 feature requirements and 45 acceptance scenarios match their canonical source definitions exactly; eight shared requirements apply throughout. |
| Implementation-state inspection | All 54 new implementation tasks remain unchecked. Each verification record marks runtime acceptance as not run. No application, frontend, test or infrastructure files changed. |
| Local document links and generated section anchors | 72 relative Markdown file links resolve. Generated HTML contains the new sections 37 and 38. |

The one-off consistency inspection used Python to compare the packet manifest with canonical source definitions, resolve relative Markdown links, check new HTML section IDs and inspect the Git change list. It did not contact external source links or exercise application routes.

The existing specification builder used a highlighting option unavailable in installed pandoc 3.1.3. It now uses the equivalent `--no-highlight` option and normalises older pandoc table-row classes to retain the existing HTML style across versions. Rebuilding twice and checking the result confirmed local reproducibility.

Cross-feature review clarified staged integration: 025/026 establish shared revision references, 027 adds impact commands, and 031 adds design-basis sources. The 025 impact scenario remains unaccepted until 027 is verified. Cancelling a correction does not silently resolve its required impact assessment. Capacity reservations and linked estimates are not added twice; submitted, accepted, incorporated, reviewed and issued states remain distinct.

No application build/test suite, database migration, runtime acceptance scenario, accessibility audit, deployment or pilot was executed for this documentation change. The original 24-packet verification history is preserved; it does not verify the enlarged scope. The nine added packets are specified and planned, with implementation and runtime acceptance pending.
