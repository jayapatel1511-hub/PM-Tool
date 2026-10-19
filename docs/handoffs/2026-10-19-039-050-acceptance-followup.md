# Packets 039–050: local acceptance follow-up

Local implementation and this verification follow-up are complete within the recorded scope. **This is not a full-specification or packet Accepted verdict.** The task-list performance target and the human/hosted/pilot gates below remain open.

- Worktree: `/Users/jaypatel/.codex/worktrees/integrate-039-050/PM-Tool`; branch `codex/integrate-039-050`.
- Runtime source: `566fe5187d72be56a14fd16d0428e8b8e6e34d72`. Later handoff edits contain test diagnostics, browser harnesses, response-shape metadata and evidence only.
- Prior integration scope and all twelve packet boundaries: [18 October handoff](2026-10-18-039-050-integration.md). Packet043 remains separate and unadopted.
- Exact outcomes, invocations, route records and 42 artifact hashes: [evidence receipt](2026-10-19-039-050-acceptance-followup-evidence.json). Raw logs stay in ignored `ci-artifacts/integration/`; the compact receipt is committed.

## What changed

Automatic Task Pickup closure used to add notifications directly, bypassing recipient preferences, active-user checks and current access. Both lifecycle closure paths now use the shared Notifier. Eight newly tested refusal/filter cases failed before this fix; all 28 TaskPickup tests now pass, including exact recipient sets, overlapping roles, mute/direct-assignment behavior and duplicate-save protection. This verifies persisted notices, not actual email delivery.

The design-basis `assignBasisIn` flag could allow a discipline lead on a Complete project while its write endpoint refused the operation. It now derives from the same `Permissions.CreateBasis` gate as naming another owner. Six-role/six-status endpoint matrices cover all 13 permission decisions. The diagnostic receipt preserves36 old client-allow/server-deny differences. Existing Project matrix probes establish authorization via validation responses; new matrices use valid requests and require success or403.

Browser checks now exercise rejected inline-name drafts, optimistic status rollback, Block reasons, date/drag warnings, baseline ghosts, latest-planned notes, grouped activity, Task-to-Deliverable links, exact chain highlighting, handoff filters, Gantt URL state, shared-view copying, Staff defaults, project picker context and numeric allocation warnings. Seven panel cold boots and a full route-pattern sweep add missing coverage. No dependency or product feature was added for these checks.

## Verification

| Check | Result and boundary |
|---|---|
| Release backend | **PASS 1,514/1,514**, zero failed/skipped, 9m15s. Six diagnostic matrix tests rerun successfully after output-only edits. |
| Coverage | **PASS** rules 3,032/3,155 branches (96.1%); services 13,084/13,665 lines (95.7%). Two identical attachments are one report. |
| Response shapes | **PASS 799** from one 106-file run; 381/429 mapped endpoints (88.8%), all typed JSON responses observed. One new pickup 404 and 11 observed type expansions; no operation/field/kind removals. |
| Web | **PASS** production build stamped 566fe51, fixture typecheck, seven key/page-mode tests, served-asset allowlist, lint (warnings remain). |
| Interaction browser | **PASS** both scripts on ports 19507/19508; zero page errors/unknown GETs; checked Timeline/Gantt/allocation axe scopes have zero violations. Allocation axe waits for finite entrance animations. The earlier `interaction-workflows-566fe51.log` failed during the entrance animation and is retained; the final combined log passes both scripts including axe. |
| Panels/chunks/live mock | **PASS individual checks**: seven panels; three chunk routes, 122 asset responses, zero failed chunks; A→B→A notices, queryless authenticated stream and project pulse. Aggregate command's live step initially failed due to omitted build env; corrected standalone live invocation passed. |
| Real API/browser | **PASS 59 representative routes**, all 24 project tabs, five legacy URL/mode checks; no unexpected API/chunk failures, error banners or serious/critical axe. Two browser contexts receive Risk and Task changes within 2 seconds; zero browser errors. |
| Current local image | **PASS** `pm-tool-integration:566fe518`, image digest `sha256:2a50d680a672358ec1b905b90441f0ca3bbce8448bb3fb0dccfe2e98768730dd`; OCI revision/HUB_BUILD match runtime source. |
| Image permissions | **PASS** immutable image-ID invocation recorded; 41 migrations, 8 forbidden DB operations refused, least-privilege app session, admin secret absent and sign-in cookie survives restart; disposable PG17/Staging LocalPassword only. |
| Ratchets/spec | **PASS** enforced ratchets and five fault-injection self-tests; load literals 0/0, page permission derivations 2/2. HTML tables 59/51 and client permission derivations 65/62 remain report-only. Generated specification current; 1,299 IDs/405 sections, zero trace gaps. |
| Compatibility | Prior source 1b649f5 **29/29 old/new/old checks PASS** retained at its original identity. No schema changed afterward; this was not rerun against 566fe51. |

Independent review cleared the runtime fixes, permission matrices, shape changes and final harness corrections. The route sweep covers representative parameter values, not every role, filter or dataset. Milestone has the project-picker provider but no PeoplePicker consumer; only source wiring and its panel cold boot are claimed. Task/Deliverable project picker requests and Portfolio's unscoped request are browser-proven.

## Remaining limits

- **Task-list latency: LIMIT.** Warmed local TestServer/PostgreSQL, 500 tasks, 100 concurrent users, page 100, pool 20: uninstrumented p95 **912.2 ms flag off** and **392.2 ms flag on**, versus 500 ms target. Picker p95 is 68.6/108.8 ms. Fixed off-then-on order prevents causal speedup/regression claims. Coverage-run task-list timings also miss 500 ms. Functional TRX success does not certify performance. One coverage-disabled **OFF→ON→ON→OFF** control then used the same load, two 100-request warmup batches per phase/path and three measured batches. Task phase p95: **340.7, 366.5, 350.4, 315.9 ms**; aggregate OFF **338.4 ms** / ON **362.4 ms** from 600 samples each. Picker aggregate OFF **57.2 ms** / ON **103.7 ms**. This warmed local control passes 500 ms. All measured samples and the original failure are retained; a single control does not establish statistical significance or representative corporate-network/pilot performance. See [raw measurement summary](../../specs/046-task-and-timeline-interaction/evidence/scale-measurement.json).
- **040 S16:** qualifying cohort measurement and Jay's ruling remain open. Passing existing ratchets is not permission for speculative long-tail conversions or flag removal.
- **043:** isolated spike's strict parity result remains **KILL, 2/10 pairs pass**; eight disabled Risk permission titles fail. Approval of the detail-read replacement did not approve adoption or the spike verdict.
- **048:** the two preview indicators Jay explicitly deferred stay deferred.
- Hosted identity, tunnel/event behavior, release/protection journey, backups/scheduler operation, actual email delivery, native/phone and complete keyboard/screen-reader review, broader hierarchy UI scenarios and Jay's acceptance are not proven here.

No push, PR, deployment, operational-data migration or pilot activation occurred. The original checkout was preserved after the already-completed, narrowly authorized 78-file cleanup; that cleanup was not repeated. Primary tracked patch verification remains 215,154 bytes, SHA256 `481681a6acde7b6d227a6a33ffa383cbf593ff906195315227495f531479a4ec`, at HEAD `cf96389def081c99e243a03a88cd258302aa6673`.
