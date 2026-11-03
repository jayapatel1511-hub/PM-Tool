# Generation 8 Work and delivery handoff

Status: READY. All owned source/test mutations held for root integration. No lane build, test execution, browser run, generation or migration. No commit, push, deploy or flag activation.

Generation 10 browser-only exception closed: corrected the inline reviewer locator to the actual popover trigger (excluding Clear), scoped Board to the existing View-switch group (retaining the full filter URL), and set live_events=false consistently with events:false in all three new scripts. No acceptance/API-error assertion was removed or broadened. Node syntax checks PASS for all three updated scripts. No browser/build/test execution or application/API source change; all scripts held and generation 10 freeze re-acknowledged. Runtime outcomes belong to root's subsequent rerun.

## Narrow review repair and Home lifecycle bridge

- Root's S04 P2 corrected in projects/Tasks.tsx TransitionDialog. When screen_patterns is enabled and a move needs a reviewer, it reads fresh canonical task detail for the performer and permissions.allowSelfReview, excludes the performer only when that setting forbids self-review, and explains it. The picker/confirm wait for valid guidance, query errors use existing recovery, and explicitly allowed self-review is labelled. No caller-prop guessing, shared-component edit or canonical command change. Disabled flags retain the prior unrestricted picker and add no guidance read.
- work-delivery-today-reviewers.cjs now follows the real list StatusMenu → Ready for Review → TransitionDialog path, checks exclusion/explanation even with a contradictory cached /me setting, chooses an independent person, submits the actual transition and checks its reviewer/toStatus payload. It also exercises allowed-self and flag-off picker behavior. Browser execution remains UNRUN here.
- Root's S08 request009 implemented on both task routes: canonical homeProjectStatus CSV is parsed/validated only under home_overview, intersects the already-visible project scope before preset/task filters, and uses the enabled scope snapshot. TaskFilter.Describe declares the query. WorkspaceTasks/Board preserves, names and removes it using root's homeScope.projectStatus label. WorkHomePresetBridgeTests adds one lifecycle/filter/preset intersection fact and extends disabled exact-shape comparisons to invalid ignored lifecycle values. Root owns the corresponding savedview metadata/generation.
- Post-repair checks: updated browser script node --check PASS; scoped tracked git diff --check PASS; updated new-file whitespace scan PASS. All other completed sources stayed held. No helper was spawned and no build/test/browser/generation ran during this repair window. Return READY/held for root re-review.

## S04 — FR-SCR-03/04, AC-SCR-03/04

- MyWork.tsx renders the existing Today focus membership before My Week/attention only under screen_patterns. All data stays available. Disabled ordering remains legacy.
- MyWeekStrip.tsx (exclusive loan) has one-line Confirmed+Expected planned/canonical remaining free, explicit partial upper bound and provisional unavailable, person/view device expansion, expanded Overview and explicit planning links. Possible hours are not counted as planned in the enabled summary; disabled summary stays unchanged.
- projects/Tasks.tsx, TaskPanel.tsx and Board.tsx show labelled performer/reviewer facts and the missing-reviewer signal, retaining OneSignal severity ordering. Disabled self-review excludes the performer from the existing autosave picker and explains it; allowed self-review is explicitly labelled.
- Reviews.cs/Reviews.tsx (exclusive loan): enabled source-guidance endpoint and omitted-null per-round presentation use the canonical author union (registered/current/captured), current task performers labelled separately, active reviewer availability, and existing ValidateRound with lockRows:false for read guidance. No mutation guard or command permission changed. Current required missing/inactive reviewer or unavailable source disables Start with its reason; form submission uses root's submitDisabled guard, including Enter/submit events. Off shapes/actions remain legacy.

## S03 — FR-SCR-02, AC-SCR-02

- Jay's clarified one-direct-target/readiness-link semantics now implemented in new Features/WorkReadinessInputs.cs. It calls existing EvaluateCurrent on detached records, reuses core context reader for state-based actors/date/action, and preserves unassessed/malformed/unavailable distinctions. GET does not create an assessment. WorkConstraint remains the exact source and retains its own removal owner; its linked Handoff is a separately labelled source. No invented Task Waiting/Blocked/materialization or second handoff target.
- TaskDetailResponse adds enabled omitted-null ReadinessInputs inside its existing snapshot. TaskPanel/WorkInputs show current readiness separately from materialized Waiting inputs and source-use revision comparison.
- New work-readiness-presentation.ts groups only exact Handoff direct/via-constraint relationships across the actually shown Weekly tasks, preserves constraint provenance, deduplicates repeated task cards/relationships, and labels the count Readiness-linked work. Accepting the Handoff leaves the Open constraint intact; removal is independently verified. Copy Summary uses the displayed memberships/roles. Existing materialized blocker grouping/counts stay unchanged.
- Root integrated Dashboard.WeeklyCoordination.ReadinessInputs for the existing selected tasks in its enabled snapshot, supplied 11 locales, and updated the old Weekly parity comparison. Root-owned generated contracts still need refresh.

## S08 task destination bridge

- Tasks.cs uses root HomePresets.Resolve/ApplyTasks on both task routes before normal filters, with RepeatableRead only for explicit enabled presets. Presets intersect existing project and explicit filters; role cohorts stay server-side. No-query/off path has no new snapshot or role queries.
- TaskQueries.Describe declares homePreset. WorkspaceTasks names/sends/preserves/clears scalar homePreset, project-discipline CSV and complete multi-assignee CSV under home_overview. All new token behavior is gated. Root supplied home.rolePreset and savedview metadata; no lane locale/generated edits.

## Checks

PASS: node --check for work-delivery-today-reviewers.cjs, work-delivery-readiness-links.cjs, work-delivery-home-preset.cjs.

PASS: scoped git diff --check for tracked owned changes; separate whitespace scan of all eight new source/test files.

Independent static review: gen8_work_review reported no remaining actionable must-fix. A response-wait race in the preset script was fixed and rechecked on disk. Review did not execute builds/tests/browser checks or measure performance.

UNRUN, root exclusive verification required:

- Backend focused filter: `FullyQualifiedName~WorkReviewerPresentationTests|FullyQualifiedName~WorkReadinessSourceEvidenceTests|FullyQualifiedName~WorkHomePresetBridgeTests|FullyQualifiedName~WorkInputPatternsTests|FullyQualifiedName~WeeklyAgendaContextTests` plus relevant existing task/review/readiness regressions. New files contain 11 focused facts across the three new classes after the lifecycle bridge repair. The normal-command tests prove shared predecessor/decision cohorts, direct Handoff + explicit constraint, independent source adoptions, acceptance/removal boundaries, current readonly permissions and no GET writes or TaskState fabrication. Existing fixture-based grouping is separately retained; API evidence alone is not browser dedup proof.
- API/registry generation; forced frontend/app and fixture type checks; lint/build.
- Actual Chrome, local base 8853: `node tests/work-delivery-today-reviewers.cjs`, `node tests/work-delivery-readiness-links.cjs`, `node tests/work-delivery-home-preset.cjs` with the root CHROME_EXECUTABLE_PATH/TUESDAY_UI_BASE_URL environment. Also retain work-delivery-input-patterns.cjs and work-delivery-related-return.cjs regressions.
- Real API-backed My Work census at 1600x900: first useful row must satisfy AC-CALM-01 <=405px. The mocked script asserts this threshold but no runtime result is claimed.

No hosted, real-recipient, native, pilot, human or Accepted evidence is claimed. Full packet slices stay unchecked until root records their full AC evidence. Existing S05+ assigned slices remain full-goal work after this bounded batch.
