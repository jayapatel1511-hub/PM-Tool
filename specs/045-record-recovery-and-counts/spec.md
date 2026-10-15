# Feature Specification: Record Recovery and Counts

**Packet**: 045 | **Feature directory**: `specs/045-record-recovery-and-counts` | **Date**: 2026-10-14
**Status**: Specified, not started.
**Input**: roadmap items 12, 13, 14 and 31 of the 14 October build-approach report (kept outside the repository), which Jay approved on 2026-10-14 with the direction to build all of it, additive only.
**Source**: product specification §49 (§49.1 to §49.8) in `spec-parts/24-record-recovery-and-counts.md`. Builds on G-06, G-07, E-04, FR-TSK-11, AC-TSK-09 (soft delete and restore); §40.7 FR-CNT-01 to FR-CNT-07, AC-DASH-01, AC-DASH-02, FR-VIS-10 (counts); §12.14, FR-TPL-01, FR-TPL-02 and §28 item 11 (templates and working-day calendars); §13.1, §13.12, FR-DASH-01 and FR-PORT-01 (dashboards). Code paths and lines are at `48586029`.

## User outcome

A PM who deleted a task, deliverable or milestone by mistake can find it and put it back after the undo toast has gone, as the delete dialog already promises. Every number on the dashboards, Portfolio, My Staff and Time either opens the exact rows it counts or says what it adds up. A PM can make a template count working days, so its due dates skip weekends and office holidays, while every existing template keeps giving today's dates. A PM sees each discipline's deliverable status at a glance, and an executive can group Portfolio by client, office or project type and see overdue deliverables per project.

## Scope

1. **Show deleted and Restore** (§49.2: FR-RCV-01, FR-RCV-02, FR-RCV-03, FR-RCV-04, FR-RCV-05, FR-RCV-06). A PM-only "Show deleted" toggle in the Filters of Tasks, Deliverables and Milestones lists that project's deleted items with Restore, search and export. New restore actions for deliverables and milestones work like the existing task restore (`src/Hub.Api/Features/Tasks.cs:49`, `583-598`). A child whose parent is deleted is refused with the parent's key. `If-Match` is checked when sent. The 10-second undo (`web/src/pages/projects/TaskPanel.tsx:453-454`) and the dialog text (`web/src/i18n/en.ts:1460`, `task.deleteBody`) stay as they are.
2. **Every count opens its list** (§49.3: FR-RCV-07, FR-RCV-08, FR-RCV-09). Server-counted Issues and Risks tiles with links (`src/Hub.Api/Features/Dashboard.cs:96-97`); a link on the discipline "Due ≤ 14 d" count; links on the Milestones view counts; links on every Portfolio row count; an action for the Overdue decisions tile (`web/src/pages/Portfolio.tsx:94`); definitions on the My Staff and Time tiles (`web/src/pages/Staff.tsx:56-58`, `web/src/pages/Time.tsx:156`); a stalled-review filter so that tile opens what it counts (`src/Hub.Api/Features/MyWork.cs:194`, `204`); links on Time's project and task totals. No existing count changes value.
3. **Working-day template offsets** (§49.4: FR-RCV-10, FR-RCV-11, FR-RCV-12, FR-RCV-13). A per-template "Offsets count" setting, Calendar days by default. Working days use the project office's calendar through `WorkCalendar` (`src/Hub.Domain/Calendar.cs:5`) in `TemplatePlanner` (`src/Hub.Domain/Templates.cs:40,49,54-55`). Preview, creation and "Add from template" agree (`src/Hub.Api/Features/Templates.cs:72`, `437`, `563`).
4. **Discipline deliverable status bar** (§49.5: FR-RCV-14). Four fixed segments in each discipline row of the Project Dashboard, each opening its filtered register, with "n of m issued" text. Takes up the status-mix bar §40.10 deferred.
5. **Portfolio grouping and overdue deliverables** (§49.6: FR-RCV-15, FR-RCV-16). Group by Client, Office or Project type with sum-only subtotals, and an "Overdue deliverables" column counted by the deliverable list query (`src/Hub.Api/Features/Deliverables.cs:68`), not the At Risk count. Both columns are added to the export.

## Non-goals (§49.1)

- Permanent erase, purge, an archive state or a recycle bin across registers (G-06, constitution V).
- Show deleted for decisions, risks, issues, meetings, actions, comments, links, time entries or planning entries.
- A Delete action for deliverables or milestones in the interface. Today only the API deletes them; this packet does not add one.
- Changing any count's value or definition, Home's fixed tiles (FR-VIS-07), or the hard-coded 14-day window of the existing "Due ≤ 14 d" count.
- Working days as the template default, changes to existing projects' dates, `working_days_enabled`, dependency lag or thresholds.
- A widget or chart builder, other charts, cross-office comparisons, rates, ranks or trends (§29 item 7).
- Any AI, ML or LLM feature (constitution II).

## Decisions (defaults apply until Jay decides)

| # | Decision | Default |
|---|---|---|
| D1 | Who restores | The existing `RestoreItem` rule: project PM or Admin. |
| D2 | Which records | Tasks, deliverables, milestones. |
| D3 | Toggle in saved views | No, URL only. |
| D4 | Child with a deleted parent | Refused, naming the parent. |
| D5 | Restore reason and version | No reason; `If-Match` checked when sent. |
| D6 | Counts that are sums | Definition plus link to the rows summed. |
| D7 | My Staff Reviews stalled tile | New `reviewsStalled` filter; old links work. |
| D8 | Existing count values | Unchanged; links reproduce them. |
| D9 | Template working-day calendar | Weekdays without org and office holidays, independent of `working_days_enabled`. |
| D10 | Status bar groups | Not started; In progress; Issued; On hold. Cancelled left out. |
| D11 | Portfolio group keys | Client, Office, Project type; none by default; sums only. |
| D12 | Portfolio deliverable figure | Overdue, not At Risk. |
| D13 | Flags | See below. |

Full wording is in §49.8.

## Acceptance

Acceptance criteria are defined in §49.7:

- Show deleted and Restore: AC-RCV-01, AC-RCV-02, AC-RCV-03, AC-RCV-04, AC-RCV-05, AC-RCV-06, AC-RCV-07.
- Counts: AC-RCV-08, AC-RCV-09, AC-RCV-10, AC-RCV-11, AC-RCV-12, AC-RCV-13.
- Templates: AC-RCV-14, AC-RCV-15, AC-RCV-16, AC-RCV-17.
- Status bar: AC-RCV-18.
- Portfolio and visibility: AC-RCV-19, AC-RCV-20, AC-RCV-21.

Existing AC-TSK-09, AC-DASH-01, AC-DASH-02, AC-CNT-01, AC-CNT-02, AC-CNT-03, AC-CNT-04, AC-CNT-05, AC-CNT-06 and AC-CNT-07 must keep passing. Each AC is cited by a named test in the slice that delivers it (see tasks.md).

## Depends on

- Packet 036 (§40.7 counts and `web/tests/counts.cjs`) and packet 012 (templates), both built.
- Packet 039, flags slice (FR-DLV-07, FR-DLV-08), before every slice of Parts A, C and D except S06. Part B (S03, S04, S05) and S06 do not wait.
- Packet 040: none required. If its registry slice has landed, the new saved-view keys and label keys go through the registry; its superset snapshot tests accept additions.
- Packet 041: none required. The project `restore` flag follows 041's server can-flag shape if that slice has landed.
- No order needed with 042, 043, 044, 046 or 047, but some files are shared. If 043 converts the Deliverables or Milestones list (§47), whichever lands second carries Show deleted across, and the parity run includes it. 047 also edits templates (FR-PRO-12) and adds the last Portfolio export column (FR-PRO-17); this packet's two export columns go before it. 046 adds a Portfolio link (FR-INT-24) and My Staff saved views (AC-INT-19), which then keep `reviewsStalled`.

## Flags

- `flag.show_deleted`: the deleted-list and new restore endpoints and the `restore` permission field (S01), and the Show deleted toggle and list (S02).
- `flag.template_working_days`: the `offsetDayKind` field (S07), and the template editor's "Offsets count" control and the wizard note (S08).
- `flag.status_rollups`: the segment and overdue-deliverable fields (S09), the discipline status bar (S10), and Portfolio grouping, the Overdue deliverables column and the two export columns (S11).

Each flag is enforced on the server as FR-DLV-08 (§43) requires: while it is off, the part's new endpoints (S01) answer 404 and its new response fields (S07, S09) are absent. Part B ships without a flag, because it corrects existing counts and existing features are never flagged (FR-DLV-09); its slices (S03, S04, S05) change what users see, so they are Tier B (§43.11). With `flag.show_deleted` off, the existing task restore behaves exactly as today: the parent-first refusal and the `If-Match` check apply to it only while the flag is on. Slice S12 removes the three flags once Jay has turned them on in review and pilot. The working-day rule in `Hub.Domain` (S06) needs no flag, because nothing calls it with a calendar until S07.

## Wording rule

Code, interface strings, specification text, commit messages and this packet's records name no third-party product.
