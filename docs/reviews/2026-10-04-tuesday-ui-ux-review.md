# Tuesday UI/UX review — 4 October 2026

Local, uncommitted work after baseline `9d3e9b91`. Jay requested overlap, button placement, usability and key workflow review, then explicitly requested wrapping menu buttons into extra rows rather than horizontal scrolling. That instruction supersedes the earlier scrolling-tab recommendation. No homedev connection or authentication action was performed.

## Subsequent release-loop checkpoint

The later independent review found additional planner recovery defects and a shared-build labelling risk; all were repaired and verified. The final candidate passes exact type-check/lint/build, all five browser suites, six new planner recovery regressions and local Release publish. Ninety-six affected routes were cold-rechecked across six roles/four widths; combined full evidence has 1,956 zero-finding cases. Earlier UX results below are retained historical evidence. See [the release-loop report](2026-10-04-tuesday-release-review-loop.md). Deployment remains unperformed because a reviewed commit and interactive homedev sudo are required; the earlier no-authentication restriction remains in force. T023 remains open.

## Findings and repairs

| Finding | Resolution | Evidence |
|---|---|---|
| P2: planner panel had overlapping Close buttons | Removed the default duplicate, retained the labelled header Close; long title wraps with a separate non-shrinking button. | Actual IAB before: 40 × 40 and 32 × 32 buttons overlapped by 28 × 32 px. After: one Close. `EntryPanel.tsx`; planner regression asserts exactly one. |
| P2: different stale filters had identical labels | Person/week indicator remains “Stale plan”; entry restriction reads “Stale entries only”. Existing parameters unchanged. | `Planner.tsx`, `en.ts`; observed live desktop labels. Confidence, visibility and allocation approval remain separate. |
| P2: My Week displayed a false zero and duplicate heading | Removed the inapplicable badge and inner heading; retained the explanatory text, actual week values and permissions. | `MyWork.tsx`, `MyWeekStrip.tsx`; exact one-heading regression. Zero and negative capacity remain actual values. |
| P2: denied/failed Planner reads also looked like an editable empty scope | A 403 shows the actual error and My Work link; other failed reads suppress successful-empty guidance and pagination. | Mocked 500/403 assertions; no unsupported filter controls on denied screen. Backend refusal unchanged. |
| P2: phone My Work buried all work beneath seven filters | Added labelled Filters disclosure and active count; active tokens and Remove/Clear stay visible when collapsed. | Phone IAB shows Needs my attention count and section links in first viewport. Regression opens filters, closes them and removes a filter; URL behavior preserved. |
| P2: selected project sections and search categories were off-screen | Project, Search, Admin and shared panel tabs wrap into rows. Phone project tabs remain in ordinary vertical flow so a tall menu cannot cover the form. | Actual 375 px project menu: eight rows, clientWidth = scrollWidth = 375. Sweep now measures every visible `nav`/`tablist` for horizontal overflow. |
| P2: long phone dialogs lost their title and Close when scrolled | Header and Close stay outside the independently scrollable form body. Existing footer actions remain reachable in the form; Escape/focus return are retained. | 108 cases: all six users × 1440/375 × nine create flows; 40 applicable, 68 explicit permission/device NAs. Every applicable case retains title/Close after scrolling and has no sideways dialog overflow. |
| P2: Time conflicts requested Reload without a recovery action | Explicit Reload fetches the latest visible entry; replaces date/hours/note with stated guidance while retaining correction reason. Conflict blocks Save/keyboard submit until successful reload. Failed reload retains the draft and Reload action; no longer-editable latest records remove Save. | Parent mocked 1440/375 check: failed GET retention, explicit latest-version GET, reason retention, subsequent PATCH uses If-Match 9, and no Save after permission loss. No live writes. |

The first expanded menu audit also found 36 long-breadcrumb overflow observations at 1024 px. That run was stopped after 450 rows and is retained in `/private/tmp/tuesday-sweep-ux-interrupted/`; it is not a passing full sweep. Breadcrumbs now wrap complete chevron/label groups; actual tablet geometry fits a 50 px two-row breadcrumb within the 64 px header. The repaired-source fresh full sweep completed all 1,956 combinations with zero findings; no web source changed during the sweep. The earlier Deliverable detail screenshot already showed its sticky item-type header/Close, so that panel was not incorrectly changed on the basis of the initial screenshot recommendation.

## Verification of this additional pass

- Type-check, lint and build: PASS. Lint has 87 baseline/current warnings and zero new file/rule/message diagnostics.
- Planner: PASS, 14 flows, 150 API calls, three created synthetic entries, two notices, five axe scans; zero page errors, violations or unknown requests. Keyboard check waits for the closing sheet to detach before asserting natural exact-opener focus; it does not refocus in the test.
- Focused dialogs: PASS, 108 final records, 40 applicable and 68 NAs; 40/40 retained drafts, visible failures, conflict retention and visible title/Close after body scroll. The interrupted run is retained separately and is not counted as passing evidence.
- Time recovery: parent-reproduced PASS using fully mocked responses at both widths. The API, calculation and ownership contracts were unchanged.
- English integration: 3,561 keys, all unique; no area translation files, runtime glob or unused frame tokens.
- Required Handoffs, Coordination, Design Basis (PORT=5186), Resources and Planner browser suites: PASS on the final build with the required Chromium executable.
- Live project menu checks and parent screenshot review: PASS, all six users at 1440/375 (12 cases); wrapping rows, selected Settings visible, zero page/main/menu overflow or page errors.
- Independent source review: no actionable defect in the additional UI changes; later breadcrumb geometry was verified by the parent.
- Fresh full route sweep: PASS, 1,956 unique route/role/width combinations at 1440/1024/768/375. Zero page errors, axe violations, page/main/menu sideways overflow, sub-12 px text, raw keys, obsolete palette colours or evaluation failures. Source hashes remained unchanged during the sweep.

Artifacts: `/private/tmp/tuesday-planner-ux-final3.log`, `/private/tmp/tuesday-time-ux-parent.log`, `/private/tmp/tuesday-editors-review-ux/results.json`, `/private/tmp/tuesday-lint-ux-final3.json`, `/private/tmp/tuesday-sweep-ux-final2/`. Spec build and trace checks were rerun after this evidence update: 660 IDs, 252 sections, zero uncited/unknown; whitespace check passes. Logs: `/private/tmp/tuesday-{spec,trace}-ux-close.log`. Baseline evidence remains in the original handoff and packet verification.

## Review limits and remaining recommendations

Saved screenshots and source review covered action hierarchy, spacing, long labels, error recovery and permission-specific states. New targeted interaction checks exercised the repaired controls. This is not a claim to test every possible record or every conditional workflow.

The proposed Workload legend clipping issue was not reproduced: actual 1440 px item bounds and scroll/client widths fit the container. The full label ends “red above” in the maintained copy; no threshold calculation was changed.

Phone Time primary-action ordering and collapsing repeat-use report parameters remain optional refinements for Jay's final review. No overlap or inaccessible action was observed in those controls, so the existing behavior is retained. Search's horizontal-menu recommendation is resolved by the explicit wrapping change.

Dense tables, timelines and planning/board grids retain contained horizontal data scrolling where the canonical design requires it. Page and menu overflow are checked separately; data columns are not hidden to manufacture a pass.

T023 manual screen-reader validation remains BLOCKED/UNRUN: native VoiceOver control timed out before usable accessibility state. Keyboard/axe checks cannot replace it. Jay's visual acceptance and actual-supervisor spreadsheet pilot remain pending human evidence. No homedev authentication will be attempted while Jay is away.

Open decisions remain: brand mark; re-capturing the hero board image after approval; confirmations for deleting holidays/removing manual roles; per-notification Mark read. No commit, push or deployment was performed.
