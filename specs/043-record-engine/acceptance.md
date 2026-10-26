# Record-engine spike acceptance

> **Verdict confirmed 2026-10-25: KILL.** Jay confirmed the KILL verdict below (043-D3). This file and `evidence/` are the spike's record, ported unchanged from `codex/platform-039-050` (runtime `87e72b71`, evidence `878c6415`); none of the spike's engine code is on this branch, and `verification.md` below means the spike branch's copy. The fallback path follows: S27 (revert, keeping the flagged additions on the restored screens), then S28 to S40 (the shared `DataTable`), each gated by the S01 parity run (`npm run test:record-parity`). Adoption (S08 to S25) does not follow.

**Local measured result: KILL under the current strict FR-REC-11(a). Not human-confirmed.** Jay's approved Risk detail-read replacement is implemented; the independent disabled-field tooltip decision is still pending. No 043-D3 status, adoption, fallback, merge or Accepted status was selected by an agent.

| Rule | Measured value | Result |
|---|---|---|
| (a) Parity for three conversions | 10 persona/width comparisons: 2 PM pairs pass, 8 pairs fail only Risk-panel disabled permission titles in the exact-commit runner. Lists, Active/Complete/empty-role states, phone guidance, keyboard, requests and accessibility have no difference. | FAIL |
| (b) Existing tests unchanged | 1,260 API tests and all 8 required mocked browser suites pass. Existing assertions unchanged; only `/meta` fixtures added to mocked suites. Remote CI and optional nightly calm/count/clutter gates not run on this commit. | UNPROVEN in full |
| (c) Risk lines shorter | 96 nonblank TSX lines deleted; 56 new definition/slot/glue lines. Shared engine excluded as the rule specifies. | PASS |
| (d) Slots and CSS | 3 Risk panel slots: Realise, Close, Links. No Risk-specific stylesheet. | PASS |
| (e) Permission functions | All 5 personas on Active/Complete; exact old Risk data/edit reasons/ordered transitions, through existing permission/workflow functions. No new permission function. | PASS |
| (f) Open-role size | 10 source files, 273 nonblank touched lines for S05/S06, tests/generated output excluded. Entire shared list/endpoint files conservatively charged; S02 setup and S04 Risk panel engine excluded. | PASS |
| (g) Definition-driven additions | Saved views, role sorting, read-only panel/History, export entries driven by definitions and generic components; explicit panel registration only. Existing row-action/create/fill/cancel slots retained. | PASS |

Reproduce counts with `python3 specs/043-record-engine/evidence/measure-spike.py`; exact paths, boundaries and line indices are in `evidence/spike-measurement.json`. These are local slice attributions, not three opened or merged PRs.

Runtime source: `87e72b718f195903d94893da78b11f8292b62998`, compared with `e80fbe1e0358444e77d186ad1aba5f85045571ff`; matrix in `evidence/parity-summary.json`. Runner cleanup left all three owned ports free.

The owned browser workflow passes If-Match save/list refresh, stale 409, all tabs, Close reason in History, Reopen, Realise, My Work link, flag-off/on behavior, saved filters/sort/columns, role sort/History/live refresh and CSV/Excel data. The compiler fails when an actual renderer is removed; a missing declared slot fails closed. Full API coverage passes: rules 96.8%, services 95.7%. Detailed results, commands and limits: `verification.md`.

The tooltip conflict is concrete: FR-REC-06 requires a permission reason on every disabled field, while strict FR-REC-09 preserves legacy titles. The candidate follows FR-REC-06; old probability/impact/discipline fields used label titles and several other disabled fields had none. No normalization hides this. Any approved contract adjustment requires a fresh strict parity run. `specs/DECISIONS.md` points here with 043-D3 status unchanged.

Human session / visual review / sweep defects / final sign-off: **not performed**. Integration, remote CI, hosted runtime and human acceptance remain separate gates. The additions flag remains off outside owned synthetic test databases.
