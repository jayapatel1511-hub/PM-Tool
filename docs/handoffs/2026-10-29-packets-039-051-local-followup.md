# Packets 039–051: local wording and review follow-up

Date: 2026-10-29. Jay selected **“Finish local wording and review items.”** This follow-up changes specification wording, generated specification documents and review records only. Application code, packages, migrations, settings and flags are unchanged. Nothing is pushed, deployed or marked Accepted.

**Review result:** both requested wording corrections are resolved. One **P2 pickup defect** remains in the existing application source; fix and verify it before enabling `flag.task_pickup`. The visual review is complete as an agent review, with styling differences and human acceptance still open. The original release checks remain evidence for their recorded scenarios; they do not cover the newly identified defect.

Runtime/rehearsed release: `2dcdaec424eba81998e0c125f2e29621c8df39b0`. Records before this follow-up: `86c00a8c0ea42bb98bfe785aa8ee1a7ed8422b19`, branch `codex/integrate-039-050`.

## Disposition of the seven For Jay items

| Item | Local disposition | Remaining boundary |
|---|---|---|
| 1. FR-BKL-08 report wording | **RESOLVED.** Removed “and the Tasks report”; the existing Task List export retains the `backlog` Yes/No column. No report added. | Application/export behavior unchanged. |
| 2. AC-VER-09 label | **RESOLVED.** Changed “Over” to the existing “over capacity” label. The 32 → 46 of 40 h example and write-free preview requirements remain. | No capacity logic or label change. |
| 3. Packet 043 styling | **REVIEW COMPLETE; visual acceptance OPEN.** Normal-state retained screenshots are readable. Styling changes are real and are not covered by the parity PASS. See visual review below. | No CSS changed; no human approval or spec exception granted. |
| 4. Flag-off behavior | **REVIEWED; KEEP current behavior.** Evaluator-relevant setting changes and `flag.backlog` queue reevaluation of Active projects; Manual-order export follows board order; failed-field text survives blur/retry but may be lost on panel exit/unmount while `data_entry_safety` is off; stale retries conflict even after the same actor's intervening edit. | Corrected the overbroad “any flag / at once” and “leaving a field” descriptions. Safety prompt remains flag-gated by FR-SAV-08. No flag or policy decision changed. |
| 5. Internal notes in activity | **REVIEWED; KEEP display redaction for all readers.** Activity retains the change event, with old/new values “Not shown”; deleted-record snapshots omit the field. | Stored audit data is not erased. Direct project-note access keeps its existing permissions. |
| 6. Export/pickup/audit assumptions | **REVIEWED.** Admin review exports reconstruct rule text from current settings; the page exports the latest review, while the API accepts a recorded review ID. Historical pickup report rows remain with the original assignee's team. The audit alert marker is intentionally retained after a cleared break. | Current-setting export text is an audit-fidelity limitation. Historical pickup retention does not establish live eligibility; see P2 below. Same-entry audit alerts remain deduplicated. |
| 7. Activation and verifiable log | **OUTSIDE THIS FOLLOW-UP.** The existing operator runbook and irreversible-log warning remain. | No sudo, host access, activation or deployment. Hosted, Tier B, pilot and human gates stay open. |

## P2: assignee deactivation blocks open-offer pickup and manager assignment

**Source-confirmed defect; runtime unchanged in this review.** An offer made while its assignee is active stays open after deactivation. FR-PKP-03 and decision D6 do not close it for this event. FR-PKP-06 defines the eligible picker as an active person in the assignee's unit, or sharing the assignee's direct manager; it does not require the assignee to remain active ([policy](../../spec-parts/28-task-pickup.md), lines 24, 34–40 and 96).

`TaskPickupRules.SameTeam` requires both `assigneeActive` and `pickerActive` ([source](../../src/Hub.Api/Features/TaskPickup.cs), lines 235–239). Its live list/detail and command callers pass the assignee's actual activity status (lines 112, 141, 185, 203 and 206). After deactivation, an otherwise eligible active peer loses the list/action and receives 403 from pickup. Managers can still see Assign but get no eligible candidates; the pickup Assign command rejects the same active targets. Ordinary task reassignment is a separate workaround. The domain test currently asserts the restrictive implementation rather than the written rule.

**Recommended correction:** retain the active picker/assignment-target requirement and project/team permissions, but do not require the referenced assignee to be active. Apply the same policy to list, detail, pickup and pickup assignment; preserve historical-report visibility.

**Required verification for a future fix:** with organisation structure both off and on, create an offer, deactivate its assignee while retaining unit/manager, and prove that it stays open, appears for an eligible active peer, and can be picked up. In a separate case prove the manager sees active candidates and can assign one. Keep negative controls for inactive targets, outsiders and Restricted-project nonmembers; preserve original-team report history. These scenarios were not executed in this wording/review pass. Existing release-suite PASS results do not prove them. **Disposition: request changes before feature activation.**

## Visual review of packet 043

Inspected the retained rendered Vacation, Holidays and Clients screenshots from the completed parity run:

- `record-parity-verified/candidate-priya-1600-vacation.png`
- `record-parity-verified/candidate-jordan-1600-admin-holidays.png`
- `record-parity-verified/candidate-jordan-1600-admin-reference-clients.png`

They are under `/private/tmp/pm-tool-release-20261029/`. Capture source `e4463524c8d0c190fee093593e1a05a05cbc5cfb` has the same application sources as the rehearsed release/current tree. These are retained normal-state captures, not a fresh browser run or before/after pixel comparison.

**Observed:** neutral headers, readable column separation and clear row actions. Vacation uses contained horizontal scrolling; the coverage section follows the grid in normal page flow. No normal-state clipping was observed in these three captures. Their mostly single-line Holidays/Clients rows do not establish wrapped-cell alignment. Hover and loading behavior were reviewed in source, not demonstrated by the static captures.

**Styling differences remain:** Vacation coverage uses a grey header, muted medium-weight heading text, row hover and shared padding; converted headers use `py-2.5` rather than `py-3` (about 4 px shorter overall at a 16 px root); Holidays/Reference cells no longer explicitly top-align. The shared loading component uses six rows normally and four for `flush` tables, including Holidays, rather than six universally ([table source](../../web/src/components/hub/registers.tsx), lines 58, 69 and 76; [header source](../../web/src/components/hub/table.tsx), line 34). Loading counts differ from some former per-screen lists.

**Recommendation:** the neutral header and density are reasonable within the existing business UI; no usability blocker was observed in the inspected normal states. That design opinion does not meet FR-REC-14's output-preservation requirement or grant an exception. Automated parity compares accessible structure, controls, requests and permissions, not CSS or loading states. Strict visual preservation remains **UNPROVEN**; restoring prior per-screen appearance/alignment/loading counts is the conservative implementation path if that requirement is retained. No styling change or acceptance decision is made here.

## Behavior review evidence and limits

- **Settings reevaluation:** [Admin.cs](../../src/Hub.Api/Features/Admin.cs), lines 140–150, emits `SettingsChanged` only for changed settings in thresholds/health/rules/calendar or `flag.backlog`. [EvaluationService.cs](../../src/Hub.Api/Infrastructure/EvaluationService.cs), lines 265–280, expands the event to Active projects and evaluates them asynchronously. “At once” must not imply synchronous completion. Prior evaluation regressions cover Backlog toggles and retry of a failed global batch.
- **Manual export:** [ListExports.cs](../../src/Hub.Api/Features/ListExports.cs), line 55, uses the shared board sort. The recorded list/export regression passed. No new export test was run here.
- **Failed text and conflicts:** [fields.tsx](../../web/src/components/hub/fields.tsx), lines 127, 176–188 and 197, pins retries to their edit version and cancels timed retries on unmount. The existing [data-entry browser test](../../web/tests/data-entry.cjs), lines 140–165 and 230–280, covers retry after Tab and panel exit with the flag off/on. The exact same-person/other-field sequence is a source inference; the browser conflict case uses another actor. Actor-independent row versions explain the same-person conflict.
- **Internal-note display:** [Activity.cs](../../src/Hub.Api/Features/Activity.cs), lines 115 and 139–145, redacts returned values/snapshots. Prior activity/history/export regressions passed. No stored-history rewrite is claimed.
- **Admin review export limitation:** [AdminReview.cs](../../src/Hub.Api/Features/AdminReview.cs), lines 145–153 and 165–173, stores counts/notes but reconstructs title/rule text using current settings. An older review can therefore export changed policy text beside its original counts. FR-ADR-06 does not explicitly require historical threshold snapshots; this is an audit-fidelity recommendation, not a newly asserted specification breach. Future work should snapshot the rule/settings at review time or label the export's current-rule context; legacy unknown values must remain unknown. The page offers “Export last review”; the endpoint loads the supplied recorded ID, not only the latest record.
- **Pickup report:** [TaskPickup.cs](../../src/Hub.Api/Features/TaskPickup.cs), lines 285–299, uses the offer's original assignee and explicitly retains the team relationship after deactivation for historical reporting. This is distinct from the live defect above.
- **Audit marker:** [AuditChain.cs](../../src/Hub.Api/Features/AuditChain.cs), lines 87–91, clears the current break after a full successful verify but keeps the already-alerted sequence. The recorded 048 decision intentionally deduplicates the same chain/entry. It does not mean every future break is suppressed.

## Checks on this follow-up

- `tools/build_spec.py` regenerated the combined Markdown and HTML from maintained `spec-parts/`.
- `tools/build_spec.py --check`: **PASS**, generated documents up to date.
- `tools/trace_spec.py --check`: **PASS**, 1,343 IDs and 412 sections, zero uncited or unknown.
- `tools/test_decisions.py`: **PASS**, 8 tests; existing Python deprecation warning only.
- `git diff --check`: **PASS**. Runtime sources/manifests/migrations/hosting/scripts/tools unchanged from `2dcdaec4`; generated-spec changes contain only the two wording substitutions.
- Independent source/specification review completed. Full backend, browser, coverage, audit and image rehearsal checks were not repeated: this follow-up changes no runtime code. Their prior evidence remains in [the release handoff](2026-10-29-packets-039-051-release-candidate.md) and [evidence record](2026-10-29-packets-039-051-evidence.json), with the new pickup gap explicitly excluded from any blanket readiness claim.

Local wording/review work is complete. Feature activation, the pickup correction, visual acceptance, repository controls/Tier B, 039 S18, 040 S16, hosted/operator/human/pilot checks and later flag retirement remain separate work. No product decision status or acceptance label changed.
