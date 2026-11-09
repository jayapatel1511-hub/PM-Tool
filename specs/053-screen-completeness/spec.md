# Feature Specification: Screen Completeness

**Packet:** 053 · **Date:** 2026-10-31 · **Status:** specified, not started; scope and
defaults await Jay's decision. **Source amendment:** §57.1–§57.11 in
`spec-parts/32-screen-completeness.md`.

## Outcome and boundary

People can move from a count to the exact records behind it, see owner/next actor/needed
by/next action together, and see blockers, gaps, supply and demand without invented
precision. This is a screen-completeness amendment only: it adds read-side presentation,
small additive records/settings and commands named in §57; it does not remove or freeze
existing behaviour. Section 10 remains authoritative. No AI, money, timesheet approval,
per-person ranking, critical-path calculation, file storage, custom roles/fields/statuses,
or earned-value index is introduced.

All new concepts follow §57's data/permission/validation boundary: additive nullable or
project-scoped model, server authorization, optimistic concurrency, activity entry in the
same transaction, and permission/partial-view filtering before figures are calculated.
Derived values are read-only pure functions. Existing labels and canonical names win over
prototype wording. Every count, chart, chip and export links to or reconciles with its
source rows.

## Requirements

The source sections are §57.1, §57.2, §57.3, §57.4, §57.5, §57.6, §57.7,
§57.8, §57.9, §57.10 and §57.11. This packet implements FR-SCR-01..84, each
matched by number to the following acceptance IDs:

AC-SCR-01, AC-SCR-02, AC-SCR-03, AC-SCR-04, AC-SCR-05, AC-SCR-06, AC-SCR-07, AC-SCR-08, AC-SCR-09, AC-SCR-10, AC-SCR-11, AC-SCR-12, AC-SCR-13, AC-SCR-14, AC-SCR-15, AC-SCR-16, AC-SCR-17, AC-SCR-18, AC-SCR-19, AC-SCR-20, AC-SCR-21, AC-SCR-22, AC-SCR-23, AC-SCR-24, AC-SCR-25, AC-SCR-26, AC-SCR-27, AC-SCR-28, AC-SCR-29, AC-SCR-30, AC-SCR-31, AC-SCR-32, AC-SCR-33, AC-SCR-34, AC-SCR-35, AC-SCR-36, AC-SCR-37, AC-SCR-38, AC-SCR-39, AC-SCR-40, AC-SCR-41, AC-SCR-42, AC-SCR-43, AC-SCR-44, AC-SCR-45, AC-SCR-46, AC-SCR-47, AC-SCR-48, AC-SCR-49, AC-SCR-50, AC-SCR-51, AC-SCR-52, AC-SCR-53, AC-SCR-54, AC-SCR-55, AC-SCR-56, AC-SCR-57, AC-SCR-58, AC-SCR-59, AC-SCR-60, AC-SCR-61, AC-SCR-62, AC-SCR-63, AC-SCR-64, AC-SCR-65, AC-SCR-66, AC-SCR-67, AC-SCR-68, AC-SCR-69, AC-SCR-70, AC-SCR-71, AC-SCR-72, AC-SCR-73, AC-SCR-74, AC-SCR-75, AC-SCR-76, AC-SCR-77, AC-SCR-78, AC-SCR-79, AC-SCR-80, AC-SCR-81, AC-SCR-82, AC-SCR-83, AC-SCR-84.

[build-gaps.md](build-gaps.md) lists previously specified behaviour against existing IDs;
those are implementation/prototype gaps, not duplicate canonical requirements.

## Accepted patterns and provenance

The five accepted prototype patterns are: exact-record drawers with the four ownership
facts; blocking-input context plus revision-used/current-revision truth; queue-first Today
with My Week collapsed; explicit performer versus independent reviewer with missing-reviewer
and self-review-setting states; and blocker-first Submission/Readiness checks. Home's eight
accepted ideas and the navy/title-block/IBM Plex/hatch visual direction are governed by §56
and the research record, not by prototype markup. Real records, `asOf` times, source
revisions, partial-view labels and unknown/missing values must remain distinguishable from
sample data; no research proper names are product requirements.

## Open decisions and defaults

053-D1–D22 remain **OPEN**; defaults apply only while dark and never constitute approval:
D1 recommended default is approval of scope; D2 Home charts/report chart/planner summary only; D3 no
My Staff six-week strip; D4 no estimated-vs-logged/template-vs-actual additions; D5 no
board over-capacity chip; D6 separate open-role figures; D7 elapsed and progress are plain
facts; D8 snapshot comparison uses `trend_compare_days` (7); D9 undo is 10 seconds;
D10 hold, do not redirect, notices while away; D11 Employee/Co-op/Contractor; D12–D15
Admin-managed vocabularies as listed in §57.11; D16 external review references an
`ExternalParty` without an account; D17 drawing-number mismatch warns only; D18 defaults
are 30/60/4/7 for the named windows; D19 one flag per part; D20 “New” means assigned or made reviewer after the previous My Work visit; D21 proposes the narrow person panel/facts, keeping the full People screen deferred; D22 proposes standard-period FTE. A conflict or §10 mismatch discovered in
implementation stays OPEN and blocks activation, not specification merge. Dark
implementation may proceed once authorized; only activation waits on a decision.

## Acceptance and exclusions

AC-SCR-01–84 are the acceptance contract. Automated checks must cite each ID; visual or
human-only checks must be named. With each part flag off, existing behaviour and API
responses remain unchanged. This packet does not claim implementation, runtime readiness,
browser acceptance, deployment or pilot approval.
