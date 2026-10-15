# Tuesday iOS simulator verification — 2026-10-14

## Scope and identity

Jay authorized the native iOS app, a tracked goal, and simulator testing, then explicitly chose **simulator fixtures for now** because an Entra native registration is unavailable. The deliverable is a local native synthetic workspace. It contains no live API connection or production authentication.

Repository base: `16399682` (detached HEAD, specification-only). Application changes are local and uncommitted in this worktree. Existing implementation worktrees and operational data were not edited. No commit, push, deployment, distribution or company-data pilot occurred. The original production proposal remains in [spec.md](spec.md); its full [acceptance gate](acceptance-gate.md) is not passed by fixture checks.

Build: shared `Tuesday` scheme, `com.engcalchub.tuesday`, version `0.1.0 (1)`, minimum iOS 17, Xcode 27.0 (`27A266a`), iOS Simulator 26.5 (`23F77`). No iOS 17 runtime is installed, so the minimum OS is a compilation target, not a runtime acceptance claim. No external packages are required. The [source manifest](evidence/source-manifest.json) identifies the final uncommitted application, tests and project configuration by SHA-256.

## Implemented native fixture workflow

- Synthetic assignee, independent reviewer and restricted read-only accounts; protected workspace requires fixture identity revalidation.
- My Work and task/review context, project hierarchy, blockers, guarded edits and status transitions; create one task in a selected permitted project/discipline.
- Comments with identity-selected mentions; own actual time and versioned corrections, localized decimals, organisation-local dates and combined daily cap.
- Project/task search, incremental task pages, explicit Inbox acknowledgment and read-only My Week with unknown capacity/freshness explanations.
- Protected account-scoped drafts, immutable unknown-outcome requests, durable fixture replay, explicit conflict review and retention of other people's changes to untouched fields.
- Background editor dismissal, foreground lock/revalidation and a scene privacy shield covering presented sheets. Strict approved-host project/task-panel link parsing; installed universal links are not configured.

Fixture lists filter/page local synthetic records. The fixture service supplies its own demonstration permissions and rules; it is not a client for, or a replacement implementation of, the existing ASP.NET API. Unsupported web handoffs carry synthetic IDs with an explicit warning that no matching hosted record exists.

## Executed results

| Check | Result | Evidence |
|---|---|---|
| iPhone 17e, Debug, light/default text; full native suite | **PASS: 28 passed, 0 failed, 1 skipped** (29 tests; 397 s) | [Raw Xcode summary](evidence/main-suite-summary.json); `/tmp/Tuesday-final-verified-20261014.xcresult` |
| Final durability/state regressions, iPhone SE | **PASS: 19 passed, 0 failed, 1 skipped** (20 unit tests) | [Extracted unit results](evidence/unit-checks.json); original mixed batch retained in `evidence/history/` |
| iPhone SE, dark, maximum accessibility text; comment and actual-time workflow + lost-response resolution | **PASS: both native UI cases** | [Passed case records](evidence/small-workflows-checks.json), [comment/time screenshot](evidence/small-comment-time.png) |
| iPhone SE, dark, maximum accessibility text; conflict comparison and explicit retry | **PASS: native UI case** | [Xcode summary](evidence/small-conflict-summary.json), [screenshot](evidence/small-conflict-recovery.png) |
| Final generated-task draft recovery after termination/relaunch | **PASS: native UI case**, with original task context and unsent body retained | [Xcode summary](evidence/final-draft-summary.json), [screenshot](evidence/small-draft-recovery.png) |
| Release build/run on iPhone 17 Pro Max | **PASS: configuration gate only; no fixture sign-in or workspace controls** | [Release gate screenshot](evidence/release-configuration-gate.jpg); release build log `build_run_sim_2026-10-15T00-49-09-855Z_pid66512_e2c9c46b.log` |
| Final Debug build/install/launch and manual native navigation | **PASS: fresh Alex workspace, open task context, return to My Work** | [Final My Work screenshot](evidence/my-work-final.jpg); build log `build_run_sim_2026-10-15T01-03-47-103Z_pid66512_221f5ba0.log` |

The current sources contain **30 distinct tests: 29 have passed and one is deliberately device-only**. Counts are consolidated across the full suite and focused final regressions, not presented as a single 30-test execution. The full suite ran before the final generated-ID durability correction; that correction was verified with all 20 unit tests and a stronger native relaunch scenario. The final semantic text-color and planner `from` parameter edits were built and inspected; the hosted planner handoff was not exercised.

Device IDs: iPhone 17e `590155A0-44BC-499F-B3E9-BFB7D156AB00`; dedicated iPhone SE (3rd generation) `0466093A-EC79-4BBF-BF3F-FAF00E4F7138`; iPhone 17 Pro Max `01EAFB1D-642C-4279-B75F-D537CD97895F`. Small-phone settings were dark appearance and `accessibility-extra-extra-extra-large`; no Dynamic Type cap was applied.

The skipped test is `testDeviceFileProtection`: Simulator cannot prove hardware data protection. Backup exclusion, draft persistence/account isolation, corrupt-input preservation, row versions, independent review, receipt replay, decimal/date rules, shared actual-hours cap and explicit Inbox acknowledgment were exercised. Nine retained normal-size workflow screenshots and the small-screen evidence are in [evidence](evidence/). Ordinary web/backend test suites were not run because their sources were not changed.

The final iPhone 17e is left running as the synthetic **Alex Morgan** account on My Work in Xcode 27's Device Hub. Native AX navigation was used for the final manual open/return check; the MCP center-tap helper did not activate the displayed rows reliably in this frontend.

## Corrections found during verification

- SwiftUI's automatic List row button behavior invoked both Inbox open and Mark read. Independent button styles now preserve explicit acknowledgment; the same correction isolates the week arrows.
- Foreground locking now dismisses open editors and suppresses editor presentation until identity revalidation.
- Conflict retry merges current values for fields that the user did not edit, retaining the intended changed fields for explicit review/save.
- Save status stays beside the save control, including conflict and unknown outcomes; drafts remain immutable after an uncertain submission. The footer yields while entering text.
- Maximum text size on iPhone SE exposed a fixed banner consuming the usable viewport. Environment/freshness information now scrolls with the content. Task metadata uses semantic foreground colors instead of inheriting a faded button tint.
- Initial synthetic records are persisted before the first mutation, keeping generated task IDs stable for unsent drafts across relaunch. Unreadable retained input cannot be overwritten by starting another draft.
- Native test navigation now handles lazy List cells, elements above/below the viewport, separate task/home draft-resume identifiers and actual switch hit points.

One intermediate batch could not launch because the simulator's SpringBoard reported Busy/preflight failure. That run is infrastructure-blocked evidence, not a passing or failing app scenario. A fresh simulator boot restored execution. One long small-phone batch exceeded the MCP five-minute response limit; its actual Xcode result bundle completed and was read directly. The final full suite used `xcodebuild` directly to avoid that tool limit. Keyboard presentation has also emitted an `Invalid frame dimension (negative or non-finite)` runtime warning; it still reproduces during keyboard presentation in passing UI tests. No functional failure remained in the completed scenarios, but the warning is unresolved and real-device behavior remains unproven.

## Remaining evidence boundaries

**BLOCKED:** corporate Entra/MSAL sign-in, approved tenant/client/API settings, live endpoint DTO integration and server permission/filter/error handling, and the shared backend's atomic payload-bound authorization-safe receipt prerequisite. The inspected backend reference `b83e9bd3` still runs general POST handlers before recording receipts.

**UNPROVEN:** live API/database/web reconciliation, actual corporate authorization/revocation, real-device file protection, VoiceOver, universal-link association, hosted network/performance budgets, distribution/signing, IT data-handling approval and human/engineering acceptance. This simulator app cannot pass those gates. Release and physical-device builds must remain behind the corporate-configuration gate.

The privacy hook follows Apple's [scene lifecycle callbacks](https://developer.apple.com/documentation/uikit/uiscenedelegate/scenewillresignactive(_:)): scene notifications are used because app-delegate activation callbacks are not the scene lifecycle. The native checks exercise a simulator shield; actual app-switcher/device acceptance remains separate.

See [ios/README.md](../../ios/README.md) for running, synthetic accounts, reset behavior and the production integration handoff.
