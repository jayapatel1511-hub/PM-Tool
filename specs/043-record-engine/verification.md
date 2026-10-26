# Verification: Record Engine

Status (2026-10-26, branch `codex/integrate-039-050`): KILL confirmed by Jay on 2026-10-25 (043-D3, see `acceptance.md`). S01, the parity run, is merged here from `build2/043-S01p` (see Build 043-S01p) with passing local checks (see Build 043-S01). None of the spike's engine code is on this branch, so no screen is converted and the conversion ACs stay UNRUN. Remote CI not run. Not Accepted.

The spike was measured separately in the platform worktree: runtime `87e72b718f195903d94893da78b11f8292b62998`, evidence `878c64150551ba07b35dcb990090b5811362b368`. Strict parity measured KILL (2/10 persona/width pairs pass; eight differ in disabled Risk permission titles). Jay confirmed the verdict on 2026-10-25. No 043 runtime code is integrated here; the table below records integration-local checks only. See [the integration handoff](../../docs/handoffs/2026-10-18-039-050-integration.md) for the separate-source boundary.

| AC ID | Status |
|---|---|
| AC-REC-01 | UNRUN (no 043 code on this branch) |
| AC-REC-02 | UNRUN (no 043 code on this branch) |
| AC-REC-03 | UNRUN (no 043 code on this branch) |
| AC-REC-04 | UNRUN (no conversion on this branch; the S01 parity run checks it, see Build 043-S01) |
| AC-REC-05 | UNRUN (no conversion on this branch; the S01 parity run checks it, see Build 043-S01) |
| AC-REC-06 | UNRUN (no conversion on this branch; the S01 parity run checks it, see Build 043-S01) |
| AC-REC-07 | UNRUN (no 043 code on this branch) |
| AC-REC-08 | UNRUN (no 043 code on this branch) |
| AC-REC-09 | UNRUN (no 043 code on this branch) |
| AC-REC-10 | HUMAN (human-only; nothing to check here until the spike is integrated) |
| AC-REC-11 | UNRUN (no conversion on this branch; the S01 parity run checks it, see Build 043-S01) |
| AC-REC-12 | UNRUN (no 043 code on this branch) |
| AC-REC-13 | UNRUN (no 043 code on this branch) |
| AC-REC-14 | UNRUN (no 043 code on this branch) |
| AC-REC-15 | UNRUN (no 043 code on this branch) |
| AC-REC-16 | HUMAN (human-only; nothing to check here until the spike is integrated) |

## Build 043-S01

- Built: `web/tests/record-parity.cjs` (ported from the spike's harness and generalised to any route), `web/scripts/run-record-parity.mjs` (builds a base commit and HEAD, serves both against one owned synthetic Development API and database), the `test:record-parity` script, and the pull-request line in `tools/ci_steps.txt` and `.github/workflows/ci.yml`. FR-REC-09. Test and CI code only: no runtime change and no flag.
- Default screens: the Risks register, the Risk panel (`?panel=Risk:<id>`), and the open-roles tab of an Active project, a Complete project and an Active project with no open roles. Each runs for five personas (Project Manager, Discipline Lead, Team Member, Read Only, an outsider of a Restricted project) at 1600 and 375 px. It compares the aria tree, headings, column headers, links, buttons, tabs and every titled element (a disabled inline field is a titled div that the aria tree leaves out), fields (value, disabled, required, minimum length, title), API reads and serious or critical axe findings. On a list it also compares each filter, column sort, the column chooser, saved views, CSV and Excel export rows, the create form (the Complete project's required correction reason) and the keyboard path. On a panel it compares each tab and Esc. It also checks that the outsider cannot see the Restricted project.
- Review fixes (2026-10-26): titled elements are compared, so the disabled-field title change that measured the spike's KILL is caught. The Risk panel and the Complete and empty-role states are in the default screens, which the CI line uses. Each list's create form is opened. Screenshot names keep the placeholder name, so the three open-roles screens no longer overwrite one another.
- Checks (2026-10-26, local; base `3e18792f`, candidate this commit's `web/` tree (the run used it before the spec text was added); `web/src` and `src` identical, so every pair must pass):
  - `RECORD_PARITY_SELFCHECK=1 node web/tests/record-parity.cjs`: PASS.
  - `RECORD_PARITY_BROWSER_SELFCHECK=1 TUESDAY_RECORD_BASE_REF=3e18792f npm run test:record-parity`: PASS on the five default screens. Identical builds pass at 1600 and 375 px. A removed column header fails, and a Columns button taken out of the Tab order fails. The Discipline Lead's Risk panel with the Probability title replaced by the permission reason also fails, which is the spike's difference.
  - `TUESDAY_RECORD_BASE_REF=3e18792f npm run test:record-parity`: PASS, 10 of 10 persona and width pairs, in 15 minutes including both builds. The report shows the Risk panel's tabs at both widths, the disabled Probability, Impact and Discipline titles for non-managers, the Reason field (required, minimum 5) on the Complete project and "No open roles" on the empty project.
  - Before these fixes (commit `19940d93`, two screens): the self-check, the browser self-check and the full run passed 10 of 10. The reviewer's reruns gave the same result.
  - `tools/test_ci_tests.py`, `tools/trace_spec.py --check`, `tools/test_trace_report.py`, `tools/test_decisions.py`: OK.
- Not run: remote CI; a candidate that differs from its base (no conversion exists, so the browser negative controls stand in). The run does not perform the mutating steps of AC-REC-05 (inline edit with If-Match, stale conflict, Realise, Close, Reopen) and it does not open the open-role row menus (Edit, Cancel) of AC-REC-06. The spike checked those in its separate owned workflow. Browsers ran on headless Chromium build 1248 instead of the 1228 build that Playwright expects.
- Assumptions: 043-D2 default (the spike screens are the parity run's default screens). 043-D8 default (the fallback starts with open roles, so the open-roles states are in the default run).

## Build 043-S01p

Merge of `build2/043-S01p` (S01, the parity run, plus the spike's verdict records and evidence; no engine code) into `codex/integrate-039-050`. The AC rows above are from the builder's report: AC-REC-04, 05, 06 and 11 stay UNRUN because no screen is converted; the harness that checks them is in place.

- Checks on the merged tree (clean merge, no conflicts; `web/package.json` merged with the existing scripts): `dotnet build Hub.slnx` 0 errors; `npm run build`, `npm run lint` exit 0; `RECORD_PARITY_SELFCHECK=1 node web/tests/record-parity.cjs` PASS; `TUESDAY_RECORD_BASE_REF=HEAD^1 npm run test:record-parity` on the merge commit (base `f64e2cee`, the pre-merge integration head, so `web/` and `src/` are identical) PASS, 10 of 10 persona and width pairs on the five default screens; `tools/test_ci_tests.py` 34 OK; `tools/test_trace_report.py`, `tools/test_decisions.py` OK; `tools/ratchet.py` exit 0; `tools/trace_spec.py --check` 0 not cited; `tools/build_spec.py --check` up to date. The browser self-check (`RECORD_PARITY_BROWSER_SELFCHECK=1`) was not rerun here; the builder ran it on the slice branch.
- S01 in `tasks.md` stays unticked: this port supplies the harness only, and no converted screen has been checked against it.
- Assumptions: 043-D2 default (the spike screens are the parity run's default screens) and 043-D8 default (the fallback starts with open roles, so the open-roles states are in the default run). Test and CI code only, so no flag applies and runtime behaviour is unchanged.
- Left for Jay: nothing is pushed and remote CI has not run. The pull-request CI parity step takes about 15 minutes locally.
