# Verification: Discipline Coordination View

**Date**: 2026-09-26
**State**: Partial project Coordination view implemented in the isolated review worktree; full packet acceptance remains unproven.

Documentation validation is recorded in `docs/coordination-spec-validation.md`. The project Coordination tab now has the five fixed question headings, links to existing handoff/change/review registers, a client refresh label and explicit disclosure when a 100-row preview is partial. Handoff and open-review context rows follow the selected discipline; revision use and change cards are project-wide. Startability is explicitly unassessed rather than inferred from an open review. Frontend `npm run build` and `npm run lint` passed after self-review corrections. In the local synthetic browser, signed-in Taylor opened DEMO-101 Weekly Coordination and the five sections, scope note, register links, source-backed empty states and existing weekly sections rendered from the API. No UI mutation was exercised. The deployed browser flow, source count/export reconciliation, cross-project/default discipline scope, saved-view return, meeting actions, and AC-DCV-01 through 05 are **UNPROVEN**.

This is a staged UI increment and does not close packet 030.

## Meeting action capture and reuse increment (2026-09-28)

Project meeting mode can capture an action against a blocked handoff or an open change. The action records an explicit same-project source link and the currently available affected task or deliverable links. Active actions appear beside that source in project and My Work coordination views, with a route to the action; its detail panel links back to the source and affected work. Sharing a task alone does not associate an unrelated action with a handoff or change. Deleted change assessment targets are omitted from new action links and counted as unavailable in the projection. The My Work CSV includes the same linked action and unavailable-target rows shown by its evaluated browser snapshot.

The combined PostgreSQL suite passed 440/440, a focused handoff rerun with action-detail link assertions passed 25/25, and the frontend production build and lint passed with existing warnings. Source-link permissions, project boundaries and exclusion of completed handoff blockers were exercised in focused tests. Populated deployed browser capture/reuse, retry behavior, saved-view return, print and export inspection, and full AC-DCV-01 through 05 remain **UNPROVEN**. This increment does not close packet 030.

## Scope and drill-down increment

The project Coordination view now exposes an owner and handoff date scope in the URL. The owner filter uses the existing project team and source register filters; handoff incoming/outgoing rows are classified by the selected discipline side or selected/current owner rather than by the API's signed-in-only direction filter. Rows open their exact source panel. Counts from truncated handoff/change previews display a `+` marker, and the view discloses the 100-row limit and that dates do not filter changes, reviews or basis uses. The source registers can have broader scope than the preview, especially for handoffs, so count/export reconciliation and saved-view return are still **UNPROVEN**. Local browser showed the owner selection and mapped change/review register URLs; no populated handoff row was available for a browser drill-down. Frontend build and lint passed with pre-existing warnings. This increment still does not satisfy AC-DCV-01 through 05.

## Reconciled project and workspace projection increment

The project and My Work coordination views now read a server projection instead of five separately capped register pages. It applies project visibility, owner and discipline scope and handoff dates within one PostgreSQL repeatable-read snapshot, returning complete source rows, exact counts and an evaluation timestamp. The My Work tab resolves the existing selected workspace/project scope and offers project, global discipline, owner and date filters. Its CSV button exports the same in-browser projection that supplies the displayed counts and rows, with that evaluation timestamp; it does not issue a fresh query. Handoffs are classified by the selected sending or receiving side, cross-discipline review assignments use the current-round register predicate, and one unsatisfied source handoff groups its active target and downstream tasks with direct task links. Accepted handoffs and complete predecessor tasks do not appear as blockers.

Focused PostgreSQL checks passed for a three-task handoff group and its Accepted transition, receiving versus sending discipline, restricted-project omission, selected and owner-verified named workspace scope, cross-discipline review and change assessment, and source-backed startability. The startability projection now re-evaluates each due assessment's canonical checks in the same database snapshot, lists Ready versus blocked/unknown work in both project and My Work views, excludes unassessed work from Ready, and includes the exact assessed rows in My Work CSV. A past `to` date without `from` remains valid, and invalid date controls stay editable. The final combined PostgreSQL suite passed 440/440; frontend production build and lint passed with pre-existing warnings. Live browser export/download, keyboard/print workflow, meeting-action reuse, saved-view round trip and populated deployment remain **UNPROVEN**. Packet 030 is therefore **not fully accepted**.

## Saved coordination view increment

The project and My Work coordination screens now use the existing personal/project saved-view API. A definition stores only scope and filter parameters; the projection is requested again when opened. The My Work tab marker survives applying, clearing and defaulting a coordination view. The existing project access checks govern shared project views, and the coordination API still rechecks current project access for every row. A focused PostgreSQL saved-view test passed 5/5 for filter restoration, project isolation and personal workspace view isolation.

A real local Chromium session against the current API and persistent `hub_review_local` database signed in as synthetic Taylor through Development auth. Taylor selected DEMO-101, Civil, self owner and a date range, saved `Civil review scope`, reloaded the page, opened project meeting mode and returned to the same saved scope. The server returned one permitted project and zero browser JavaScript errors. The saved definition remained after the API process restarted. During that check, the filter selects' accessible names included their option text; explicit labels now give each input its intended name. The final frontend build and lint passed. Focused axe WCAG 2.1 A/AA scans of the coordination main region and Save View dialog found zero violations or incomplete checks. This proves the exercised local path only. A source-record edit followed by refresh, deployed browser workflow, other browsers and assistive-technology review remain **UNPROVEN**; full AC-DCV-03 is not claimed.

In a separate local Chrome session against the same API and persistent synthetic database, Taylor's My Work coordination view downloaded `discipline-coordination.csv` with the expected evaluated-record header, three data rows and a DEMO-101 record. The Print control invoked `window.print()`, and its action container had `display: none` under print media. The browser reported zero JavaScript errors. This checks the exercised local export and print controls, not the rendered paper/PDF output or deployed browser-to-API behavior; those remain **UNPROVEN**.


## 2026-10-01 Codex recovery checkpoint

Meeting action reuse is a versioned idempotent command adding only missing source/affected-work links, with no repeated action notification. Recovered focused MeetingActions tests passed 7/7; handoff/grouping tests passed 14/14. A live Chrome-to-API rehearsal on the isolated persistent local review copy reused an action for DEMO-101-CH001, retained the link and default selection after reload, and created zero new actions. Full populated print/export/saved-view and deployed acceptance remain UNPROVEN.

### Final local projection rehearsal

The live Chrome CSV contained exactly the 4 records of the tested server projection and its evaluation timestamp.
Print media hid export/print controls and generated a PDF. This closes those bounded local checks; full visual print,
restricted/large populated browser reconciliation, saved-view and current hosted acceptance remain UNPROVEN.

## 2026-10-02 local browser-to-API acceptance rehearsal at b155601

Independent rehearsal on worktree head `b155601` against the real API and a fresh local database (`hub_agent_verify2`) populated through the 025–027 rehearsals (VER-201 open, VER-202 restricted). Each person used a separate Development-auth Chrome 154 session (Playwright 1.61.1); API calls were used for setup, readback and direct-permission probes. axe-core 4.13 WCAG 2.1 A/AA found zero violations in the project view, Save View, capture/reuse dialogs, My Work coordination and search; the only "needs review" result is `aria-prohibited-attr` on the scope-filter `div` carrying `aria-label` without a role. No page errors.

| Scenario | Result | Evidence |
|---|---|---|
| AC-DCV-01 three tasks wait on one handoff | **FAIL** | One group "VER-201-H001 · 3 linked tasks (T0001, T0002, T0003)" matches the projection, but the count is plain text; only each task key links |
| FR-DCV-04 counts vs source registers, before and after a source edit | PASS | Civil scope owe/waiting/changed/using 0/3/3/1 equalled the handoff, change and input-use registers; after Alex accepted VER-201-H005 elsewhere and Taylor reloaded, counts reconciled and its blocker group was gone |
| AC-DCV-03 saved view, drill-down and return | **FAIL** | Saved view stored only discipline/owner/from/to; re-opening restored them and Back kept them. After Alex accepted the handoff opened from the view, the returned view still showed it Submitted (and as a blocker) with an unchanged "Client refreshed" time at 4 s and 17 s; only reload showed Accepted |
| FR-DCV-03 change rows | **FAIL** | Rows show only "N assessment(s)"; acknowledgement is not shown, and the number is not labelled Pending Assessment |
| "project-wide" labels under a discipline scope | **FAIL** | Civil scope "Which revision are we using? · project-wide" shows 1 of 4 project uses; "What changed? · project-wide" shows CH002 "0 assessment(s)" while it has 2 pending (register: 3 incomplete) |
| AC-DCV-05 capture, reuse, second source | PASS | First capture on CH005 created VER-201-A01 linked to the notice and its targets; capturing again defaulted to reusing A01 and created nothing; reusing A01 for CH004 added only that link; actions 1→1 |
| FR-DCV-06 reuse replay and stale version (API) | PASS | Same request ID returned the first result; a new request with the old row version → 409 |
| AC-DCV-04 mark reviewed | PASS | Handoffs, review, notices, assessments (including acknowledgements) and actions unchanged; only the reviewed time and name changed |
| FR-DCV-07 keyboard | PASS | ArrowRight in meeting mode moved focus from section 1 to section 2 |
| FR-DCV-07 print | **FAIL** | My Work Print called `window.print()` once and print media hid `.no-print` controls, but Chrome's print PDF was one A4 page cut off inside VER-201 (VER-202 and the rest of VER-201 missing). The project Weekly Coordination print was also one page although `main` held 5,756 px of content |
| FR-DCV-04 My Work CSV vs screen | PASS | Jay's CSV had one evaluation timestamp and the same per-project owe/waiting/changed/using counts as the screen |
| AC-DCV-02 one permitted project in a workspace | PASS | Alex's workspace held VER-201 and VER-202; after Jay removed Alex from VER-202, rows, counts ("1 permitted projects"), CSV, page text under print media, search UI/API and project choices contained only VER-201 |

**Defects.** (Medium) Handoff commands do not invalidate the coordination projection: `refresh()` in `web/src/pages/projects/Handoffs.tsx` invalidates handoff, option and search queries but not `['p', projectId, 'discipline-coordination', …]`, so within the 15 s `staleTime` (`web/src/main.tsx`) a returning user sees the pre-edit snapshot; review and change commands use `useCoordRefresh`, which does invalidate it. (Medium) The server filters input uses and change assessments by the selected discipline (`Build` in `src/Hub.Api/Features/DisciplineCoordination.cs`) while card titles and the scope note in `DisciplineCoordinationView.tsx` call them project-wide, so a partial count is described as project-wide. (Medium) Printing truncates to one viewport: in print media the shell `div` in `web/src/app/Shell.tsx` keeps `h-dvh overflow-hidden` and `main#content` keeps `overflow-auto` (computed: shell height 1000px, overflow hidden; main clientHeight 1000 of scrollHeight 5756), and `@media print` in `web/src/index.css` only hides `.no-print`. (Low) The blocker-group count is not a link to those tasks, although the task list accepts an `ids` filter. (Low) My Work counts every notice, including Closed and Draft, under "What changed?" (VER-201: 5), while the project card counts only open ones (3) for the same scope; My Work CSV InputUse rows carry only an ID. (Low, accessibility) `aria-label` on the scope-filter `div` without a role.

NOT RUN: staffing-conflict filters, weekly-commitment snapshots in meeting mode, screen-reader and other-browser checks, large populated data, hosted/homedev acceptance. Packet 030 is not accepted.

## 2026-10-02 fix verification: scope labels, change counts, print and CSV

Fixes for the coordination-view findings of the 2026-10-02 rehearsal above (the handoff-refresh defect behind the AC-DCV-03 failure belongs to the handoff writer and is not addressed here).

- **Scope labels.** Every section of the project view follows the selected discipline and owner, as the server already counted. "project-wide" now appears only when neither is selected, the scope note says which cards fall back to the signed-in user's handoffs, and the blocker list is headed "Linked task blockers" instead of repeating "What are we waiting for?".
- **FR-DCV-03 change rows.** Each row shows Pending Assessment apart from acknowledgement, for example "2 Pending Assessment · 1 of 2 acknowledged". A scoped row says "in this scope" and adds the notice's whole-project pending count when that differs, so a partial number is never read as the total.
- **One "What changed?" definition.** The server returns only notices that are Open or still hold Pending Assessment work in scope, so My Work, the project card and the CSV count the same rows. Draft and closed notices are no longer counted in My Work.
- **CSV.** Input-use rows carry the consuming work's key and name and the source key and revision. Change rows carry the pending, acknowledged and project-pending numbers. Finding L7: client CSV cells use the server `Export.Csv` formula guard (leading `= + - @`, tab or CR gets an apostrophe; numbers stay numbers) and are also quoted when they contain `;` or a tab. A formula after leading spaces is guarded as well, so the client rule is never weaker than the server's; the server does not quote `;` or a tab.
- **AC-DCV-01.** "N linked tasks" links to the task list filtered to exactly those tasks (`tasks?ids=…`), in both the project view and My Work.
- **Print.** In print media the app shell flows (`print:block print:h-auto print:overflow-visible`, rail hidden, `main` not scrolled), so the whole page paginates.
- **Accessibility.** The scope filter is a labelled `role="group"`.

| Check | Result |
|---|---|
| New `DisciplineCoordinationTests` (Open + Draft notices, two assessments with one acknowledged, project/Civil/Electrical/owner scopes, My Work equality, readable uses) | 1/1 passed. It fails if the "What changed?" filter is removed |
| Focused suites (DisciplineCoordination, MeetingActions, Handoffs, ReviewChange, CoordinationLifecycleSweep, Views) | 120/120 passed |
| Full PostgreSQL suite | 570/570 passed |
| `python3 tools/trace_spec.py --check` | 612 IDs, 0 not cited, 0 unknown |
| `npm --prefix web run build`, then `npm --prefix web run lint` | Build passed. Lint showed 88 warnings and 0 errors, the same warnings as at `e28577c` |
| `test:coordination` with the installed Chrome (`CHROME_EXECUTABLE_PATH`) | Exit 0. The new CSV-cell assertions passed (`;`, tab, quote, newline, `= + - @`, leading tab or CR, numbers), as did the 5 axe-scanned flows with 0 violations. Run alone against a copy without `;` quoting, the same CSV assertions fail |

Real Chrome against the API on 127.0.0.1:5099 with the isolated database `hub_agent_dcv` (dropped afterwards), Development auth, and synthetic projects seeded through the API:

| Scenario | Result |
|---|---|
| Project view, no scope (Priya) | "Which revision are we using? · project-wide" counted 2. "What changed? · project-wide" counted 1 because the Draft notice was excluded. The row read "CH001 · Coordinated survey · Open · 2 Pending Assessment · 1 of 2 acknowledged" |
| Civil scope (`?discipline=`) | Titles had no "project-wide"; uses counted 1. The row read "1 Pending Assessment in this scope · 0 of 1 acknowledged · 2 pending across the project", which resolves the earlier "0 assessment(s)" reading |
| AC-DCV-01 count link | "3 linked tasks" opened `tasks?ids=` with exactly T0003–T0005. The two consumer tasks were not listed |
| My Work (Marc, default Civil scope) | "What changed? (1)" matched the project view for the same scope. The downloaded CSV InputUse row was `DCVF9336-T0002,"Grading tie-in; east · DCVF9336-D001 rev A · Coordinate the corridor"`, with the `;` field quoted. The Change row title carried `pending: 1 · acknowledged: 0 · project pending: 2` |
| axe WCAG 2.1 A/AA | Project view and My Work: 0 violations and 0 incomplete. The scope-filter `aria-prohibited-attr` result is gone |
| Print before and after, same data, A4 under print media | With the shell at `e28577c`, Weekly Coordination and My Work each printed 1 page, and the last section or project was missing. With the fix, each printed 3 pages with "12. Held items" and the last project present |
| Print final run | Weekly Coordination printed 3 pages with all 12 numbered sections and the coordination card titles in the PDF text. My Work printed 5 pages with every on-screen project heading. Export and print controls were absent. Shell and `main` overflow were `visible`, and `main` clientHeight equalled its scrollHeight |
| Errors | 0 page errors, 0 console errors, 0 failed API calls |

NOT RUN: Letter and other paper sizes, other browsers and assistive technology, hosted or homedev runs. Packet 030 is not accepted. AC-DCV-03 stays open until handoff commands refresh the projection.


## 2026-10-02 combined candidate

Integrated count scope, pending/acknowledgement, linked-task, print and CSV corrections (`36b6cd5`, `8b3bdd3`, `c70cedf`). Frontend build/lint and mocked Chromium regressions passed. Hosted scope/export/print acceptance remains pending activation. See [combined release evidence](../../docs/reviews/2026-10-02-combined-candidate.md).
