# Feature Specification: Weekly Planning Layer

**Packet**: 034 | **Feature directory**: `specs/034-weekly-planning-layer` | **Date**: 2026-10-03
**Status**: Approved scope; specified and planned; application implementation not started.
**Input**: Jay, 2026-10-03: option (b) of the three-way review `T-tuesday-v1.1-vs-code.md` — add the weekly planning entries of the unmerged Specification v1.1 (local commit 16399682) as a separate record in a new packet beside packet 029's confirmed allocations, leaving packet 029, the Workload grid and its calculations, and packet 032's readiness capacity checks unchanged.
**Source**: Product specification §39 and §39.1–§39.12; §8.11 (permissions); §10.9 (vocabulary and settings); §13.21 (Weekly Planner screen); §13.10 (My Week row); §9.4 (Resources area); §27, §29 and §30 (scope notes). Related, unchanged contracts: §37.6 (FR-CAP-02, FR-CAP-03, FR-CAP-04, FR-CAP-06, AC-CAP-04), §37.1 (FR-MDC-02, FR-MDC-03, FR-MDC-06), §10.8, §8.10, §12.15, §13.11, §13.0, §38.2, FR-AUD-01; decisions Q4, Q18, Q19.

## User outcome

A Supervisor sees, for each direct report and each of the next weeks, the person's capacity and everything already planned — project and non-project work, with its confidence — and plans or adjusts in one or two interactions, without counting any approved project allocation twice and without exposing private planning to anyone else. Each person sees and maintains their own weekly picture in My Work.

## Scope

- One new person-scoped record, the **planning entry** (§10.9): person × ISO-week range × hours per week, with label, source category, optional project and project discipline, Confidence (Confirmed, Expected, Possible), Visibility (Draft, Published, Confirmed), notes and last-validated staleness (FR-PLN-02).
- Self entries (owned by the person) and manager entries (owned by the person's Supervisor or an Admin), creator-owned; Private drafts private to their owner everywhere (FR-PLN-03, FR-PLN-18).
- Quick add `<hours> h <label>`, `~` for Possible; side-panel editing, repeat, extend, move, copy, Still valid and visibility changes (FR-PLN-05, FR-PLN-06, FR-PLN-07).
- Capacity from the existing weekly capacity and §37.6 availability overrides; time away recorded only as availability overrides, including an all-or-nothing range command (FR-PLN-08, FR-PLN-09, FR-PLN-10).
- Confidence bands, remaining capacity, read-only projection of Confirmed resource allocations with exact coverage so nothing is counted twice, task estimates as context only, and an explainable contribution list (FR-PLN-11 to FR-PLN-15).
- Planner indicators Over-planned, Under-planned and Stale plan with Admin settings (FR-PLN-16).
- Permissions, privacy, restricted-project and scope rules (FR-PLN-17 to FR-PLN-20); lifecycle, audit and notifications (FR-PLN-21 to FR-PLN-23); lists, exports, search, saved views and concurrency (FR-PLN-24, FR-PLN-25).
- The Weekly Planner screen (§13.21), the My Week strip on My Work (§13.10), keyboard operation and the §13.0 responsive policy (FR-PLN-26, FR-PLN-27, FR-PLN-28).

## Non-goals

- Any change to packet 029: the resource allocation model, its PM/DL proposal and Supervisor confirmation, confirmation snapshot, per-date versions, availability override editors and statuses (§37.6) stay as built. The only new write to a §37.6 record is the time-away range command, which applies FR-CAP-02 unchanged.
- Any change to the Workload grid (§12.15, §13.11, packet 017), its Over-assigned/Under-assigned/Deadline cluster rules or its FR-CAP-04 committed load; any change to packet 032's readiness capacity checks (§38.2).
- Deferred to later proposals (§39.12): Manager Home; People and Person Profile; role-level long-term demand; planned versus actual effort; Overlapping Plans and cross-team planning; Deliverable Pressure and planned staffing on the Project Dashboard; self-recorded time away; pointer drag of planner blocks; conversion of an entry to a task assignment; comments on planning entries; Team Capacity and Allocations by Source reports; a decimal default capacity; moving Workload's fixed thresholds into settings.
- Leave management, HR integration, automatic assignment, resource levelling, forecasting, billing, payroll or productivity ranking (§30).
- v1.1's navigation restructure, section numbers and identifiers.

## User Scenarios & Testing

### User Story 1 — Plan a direct report's weeks quickly (Priority: P1)

Taylor, a Supervisor, opens the Weekly Planner, clicks Yagmur's week cell, types `8 h proposal support` and presses Enter. The entry is a Private draft until Taylor publishes it.

**Why this priority**: Fast manager planning is the reason for the layer; without it Supervisors keep their spreadsheets.

**Independent Test**: With one Supervisor and one direct report, create, publish and withdraw an entry and check the person's view after each step.

**Acceptance Scenarios**:

1. **AC-PLN-01.** Quick add creates one 8.0 h, one-week entry with source Other project, Confidence: Expected and Visibility: Private draft, asking for nothing else.
2. **AC-PLN-03.** Publishing notifies Yagmur once and shows the hours in her My Week as Confidence: Expected · Visibility: Proposed assignment; returning it to Private draft tells her only that it was withdrawn.
3. **AC-PLN-08.** Changing the end week (or Repeat for N weeks) keeps one entry spanning every week.

### User Story 2 — Explainable totals with nothing counted twice (Priority: P1)

Every person-week shows capacity, time away, hours by confidence band and remaining capacity; selecting a total lists every contributing row, including approved project allocations from packet 029.

**Why this priority**: A total that cannot be decomposed, or that double counts an approved allocation, will not be trusted.

**Independent Test**: Seed one Confirmed resource allocation, two availability overrides and three planning entries for one week, then reconcile the contribution list by hand.

**Acceptance Scenarios**:

1. **AC-PLN-04.** 30 h Confirmed + 13 h Expected against 40 h gives −3.0 h remaining and Over-planned at 105 % but not at 110 %; 8 h Possible is shown and not subtracted.
2. **AC-PLN-05.** A 12 h approved allocation and a 16 h Confidence Confirmed entry for the same project give 16 h, not 28 h; an 8 h Expected entry for that project is fully covered.
3. **AC-PLN-06.** Two Unavailable days reduce 40 h to 24 h once, matching Workload.
4. **AC-PLN-10.** Task estimates appear only as context.
5. **AC-PLN-20.** The person sees their own approved allocation hours read-only in My Week.

### User Story 3 — Private drafts stay private (Priority: P1)

A Supervisor's scenario planning is visible to nobody else, through any channel, unless an Admin deliberately opens it for data correction, which is logged.

**Why this priority**: Without server-side privacy, managers will not plan in the tool.

**Independent Test**: With one draft, query every read path as each other role.

**Acceptance Scenarios**:

1. **AC-PLN-02.** The draft and its hours are absent from other users' planner, My Week, search, exports, notifications, digests, activity and totals; the Admin sees it only in logged data-correction mode.
2. **AC-PLN-12.** Entries on Restricted projects are hidden from viewers who cannot view the project; totals are labelled partial; Under-planned is suppressed.

### User Story 4 — Record my own non-project work (Priority: P2)

Yagmur adds `6 h small project` to her own row in My Work › My Week so she does not look idle.

**Why this priority**: Work outside the Hub must appear, or capacity looks wrong.

**Independent Test**: As a Standard user, add, change and delete a self entry from My Week; as the Supervisor, try to change it.

**Acceptance Scenarios**:

1. **AC-PLN-07.** The self entry is Confidence: Confirmed and Visibility: Self-entered; the Supervisor can see but not change it.

### User Story 5 — Keep plans current (Priority: P2)

Entries not validated for `planning_stale_days` are flagged Stale plan until their owner marks them Still valid or changes them.

**Independent Test**: Advance the clock past the threshold and check the planner and digest.

**Acceptance Scenarios**:

1. **AC-PLN-09.** Stale plan appears in the planner and the owner's digest, still counts, and Still valid clears it.
2. **AC-PLN-19.** An Archived linked project leaves the entry unchanged and flagged "Project not active".

### User Story 6 — Record time away for a date range (Priority: P2)

Taylor records Yagmur's week off in one action; Workload, readiness and the planner all see the same reduced capacity.

**Independent Test**: Record and clear a five-day range, including a stale-date refusal.

**Acceptance Scenarios**:

1. **AC-PLN-16.** Five overrides are written in one transaction, versions advance, a stale date writes nothing, clearing restores capacity, and Yagmur cannot record her own.

### User Story 7 — Trustworthy access, labels, concurrency and operation (Priority: P3)

**Acceptance Scenarios**:

1. **AC-PLN-11.** Planning commands leave packet 029, Workload and readiness results byte-identical; existing 017, 029 and 032 tests pass unchanged.
2. **AC-PLN-13.** Stale saves return a conflict naming the earlier change; an idempotent retry creates one entry.
3. **AC-PLN-14.** "Confirmed" never appears without its qualifier.
4. **AC-PLN-15.** Every §8.11 row allows and refuses the stated roles.
5. **AC-PLN-17.** 12 people × 12 weeks with 500 entries returns within 1.5 s at p95; My Week within 500 ms.
6. **AC-PLN-18.** Keyboard operation at 1440 px; read-only at 1024 px; notice at 375 px; never colour alone.

### Edge Cases

- Capacity 0 in a week (holiday override, full time away) with any Confirmed or Expected hours is Over-planned (PLN-10); the cell shows remaining as a negative number.
- An entry whose hours exceed capacity is saved with a warning; hours above `planning_max_hours_per_week` or not in 0.5 steps are refused (PLN-02).
- Quick-add text such as `8 h`, `h proposal` or `8.25 h x` is refused with a field message; `8 hotels` parses as 8 h "hotels" (PLN-13).
- A Supervisor changes: the former Supervisor's published entries become read-only with "Owner no longer manages this person" and still count; their drafts remain private (FR-PLN-21).
- The person becomes inactive: their row disappears; entries stay in history (FR-PLN-21).
- A linked project becomes Restricted after linking: viewers who cannot view it lose the entry and see partial totals (FR-PLN-19).
- A Confirmed allocation is later edited (returns to Proposed under FR-CAP-01): it stops covering entries, which then count fully (FR-PLN-12, FR-PLN-13).
- An entry spans weeks outside the horizon: only weeks shown are summed; the entry list shows its full range.
- An Admin opens data-correction mode: every such request is logged with the reason; normal views never include others' drafts (FR-PLN-18).
- Two editors change the same entry: the second receives a conflict and nothing of it is stored (FR-PLN-25).

## Requirements

The product specification is the source of truth; these lines summarise each requirement.

- **FR-PLN-01** Separate person-scoped record; no writes to §37.6 records except FR-PLN-10; Workload, readiness, tasks and time untouched; FR-MDC-02, FR-MDC-03 and FR-MDC-06 apply (§39.1).
- **FR-PLN-02** Planning entry fields, ISO-week range, 0.5 h steps up to the setting, label, notes, Major project if and only if a project is linked (§39.2, PLN-01, PLN-02).
- **FR-PLN-03** Self and manager entries, owner = creator, defaults, no overwriting, owner-only changes (§39.2, PLN-03, PLN-17).
- **FR-PLN-04** Never a bare "Confirmed"; Confidence, Visibility and Approval status qualifiers; draft, possible and source presentation (§39.2, §10.9).
- **FR-PLN-05** Quick add grammar and defaults (§39.2, PLN-13).
- **FR-PLN-06** Side-panel editing, repeat, extend, move, copy, Still valid, soft delete (§39.2).
- **FR-PLN-07** Owner-only visibility changes without approval; not a §37.6 approval (§39.2, PLN-16).
- **FR-PLN-08** Capacity = Workload available hours via the FR-CAP-02 rule (§39.3, PLN-05).
- **FR-PLN-09** Time away only as availability overrides, explained, never subtracted twice; no leave category (§39.3, PLN-06).
- **FR-PLN-10** All-or-nothing time-away range command under FR-CAP-02 rules (§39.3, PLN-19).
- **FR-PLN-11** Bands and remaining capacity; Possible never subtracted; display rounding after aggregation (§39.4, PLN-09).
- **FR-PLN-12** Read-only projection of Confirmed resource allocations on visible live projects; no snapshot or reason exposed (§39.4, PLN-07).
- **FR-PLN-13** Coverage order so approved and planned hours for one project are never counted twice (§39.4, PLN-08).
- **FR-PLN-14** Task estimates shown as context only (§39.4).
- **FR-PLN-15** Contribution list accounts for every figure (§39.4).
- **FR-PLN-16** Over-planned, Under-planned, Stale plan from settings; distinct from Workload indicators; warnings only (§39.5, PLN-10, PLN-11, PLN-12).
- **FR-PLN-17** Server-side §8.11 matrix; Read Only veto; Admin data correction with reason (§39.6, §8.11).
- **FR-PLN-18** Private drafts excluded from every other read path; logged Admin data-correction mode (§39.6, PLN-04).
- **FR-PLN-19** Restricted-project exclusion and partial-view labelling (§39.6, PLN-15, PLN-18).
- **FR-PLN-20** People scope = Workload scope plus own row (§39.6, PLN-14).
- **FR-PLN-21** Project status never changes entries; owner authority; deactivation; soft delete only (§39.7, PLN-17, PLN-18).
- **FR-PLN-22** Same-transaction, person-scoped activity history (§39.7).
- **FR-PLN-23** Planning entry changed notifications to the person; Planning digest section for stale plans (§39.7, PLN-16).
- **FR-PLN-24** Filters, sorting, paged list, exports, saved views, global search (§39.8).
- **FR-PLN-25** Row versions, conflicts, idempotent creates (§39.8).
- **FR-PLN-26** Weekly Planner screen and responsive policy (§39.9, §13.21).
- **FR-PLN-27** My Week strip on the person's own My Work (§39.9, §13.10).
- **FR-PLN-28** Keyboard operation and accessibility (§39.9).

Acceptance criteria: AC-PLN-01, AC-PLN-02, AC-PLN-03, AC-PLN-04, AC-PLN-05, AC-PLN-06, AC-PLN-07, AC-PLN-08, AC-PLN-09, AC-PLN-10, AC-PLN-11, AC-PLN-12, AC-PLN-13, AC-PLN-14, AC-PLN-15, AC-PLN-16, AC-PLN-17, AC-PLN-18, AC-PLN-19, AC-PLN-20 (§39.11).

### Key Entities

- **Planning entry** (new, §10.9): person, owner (creator), hours per week, start and end week, label, source category, optional project and project discipline, confidence, visibility, notes, last validated, row version, soft deletion.
- **Availability override** and **person/date version** (existing, §10.8): the only representation of time away; written by the time-away command under FR-CAP-02.
- **Resource allocation** (existing, §10.8): read-only input; only status Confirmed contributes.
- **Weekly capacity** (existing `app_user.weekly_capacity_hours` and `default_weekly_capacity_hours`).
- Six planning settings (§10.9), one notification event, one digest section, one search group and one saved-view list type.

## Success Criteria

- **SC-001**: In the pilot, a Supervisor plans 12 direct reports for 12 weeks from their existing spreadsheet in under an hour, and AC-PLN-01 to AC-PLN-20 pass.
- **SC-002**: For every sampled person-week, the contribution list reconciles to the displayed bands and remaining capacity, and planner capacity equals Workload available hours.
- **SC-003**: No private draft, hidden-project entry or restricted hour is observable by an unauthorised viewer through any list, total, export, search result, notification, digest or history.

## Assumptions and dependencies

- Depends on packets 001, 002, 006, 009, 017, 019, 021 and 029 being built (029's allocations, overrides and per-date versions; 017's people scope and forecast; 021's working-day calendars; 019's saved views).
- Decisions: Q4 (restricted projects), Q18 (weekly capacity default, kept at 40 h), Q19 (direct reports only). The detailed defaults in §39.12 are authored design choices to confirm with Jay; they are not claims that the behaviour exists.
- Presentation follows the Tuesday visual standard's resource-planning language; it supplies no rule or threshold.
- No new infrastructure, background job, message broker or external credential is needed.
