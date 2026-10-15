# Feature Specification: Task and Timeline Interaction

**Packet**: 046 | **Feature directory**: `specs/046-task-and-timeline-interaction` | **Date**: 2026-10-14
**Status**: Specified, not started.
**Input**: Jay approved the 14 October build-approach report and directed building all of it ("We are building everything you suggested"). Current features are fixed: additions only, and today's behaviour stays the default wherever an addition touches it.
**Source**: product specification §50 (§50.1 to §50.9) in `spec-parts/25-task-and-timeline-interaction.md`; roadmap items 18, 20, 22, 26, 30 and 32 of the report. Builds on §13.3, §13.3.1, §13.6.1, §13.7, §40.6, §12.3, §12.4, §12.6, §12.16, §13.5, §36.4, §37.2, §37.6, §18.4, §13.19, §13.14, §13.12 and §13.21. Amends §8.5.2, §10.4, §12.3, §12.16, §13.7 and §30 (amendment requests returned with this packet).

## User outcome

A PM working the task list starts, blocks or renames a task from its row. Choosing an assignee inside a project puts the project's own people first. Moving a date shows the earliest date that keeps the plan consistent and which successors a move would break, without ever changing anything on its own. A long project can be re-baselined by its PM with a reason, small slips can stop drawing noise, and a project that will finish after its target says so. The Timeline shows handoffs as dated rows and can highlight the chain of work feeding one milestone.

## Scope

1. **Shipping rule** (FR-INT-01, §50.1): six flags, one per part; flag off means today's behaviour everywhere.
2. **Row quick actions and inline name edit** (FR-INT-02, FR-INT-03, FR-INT-04, §50.2): Open, Start and Block on hover or focus, and a pencil for the name, on the project Task List. Builds the "Row hover reveals quick actions" line of §13.3, reusing the status menu path (its dialogs and FR-RDY-02), the manual-block dialog (§13.3.1) and the inline text control (G-07).
3. **Person picker ranked by project** (FR-INT-05, FR-INT-06, FR-INT-07, §50.3): the viewer, then the project team (current or removed within `picker_recent_days`), then everyone; search also by job title and home discipline; G-11 and FR-PICK-02 unchanged.
4. **Date context and successor warnings** (FR-INT-08, FR-INT-09, FR-INT-10, FR-INT-11, §50.4): one server read of earliest consistent start and due, latest consistent due, own inconsistencies and successor warnings, built from the evaluator's D-12, T-04 and DL-03 functions (FR-DEPT-05), with availability by the FR-PLN-08 day rule for viewers inside §40.10 D8. A muted hint in the panels; a successor list in the drag confirmation (§12.16, FR-VIS-05).
5. **Schedule honesty** (FR-INT-12, FR-INT-13, FR-INT-14, FR-INT-15, §50.5): `slip_display_days` (default 0) for ghosts and "Moved n d"; PM re-baseline with reason stored beside `original_date`, which never changes (AC-MS-04 still holds); slip still measured from the original date, with a "since re-baseline" figure added beside it; the latest planned date against the target as an information note. Activity per FR-AUD-02; M-02 unchanged.
6. **Small additions** (FR-INT-16 to FR-INT-24, cited individually: FR-INT-16, FR-INT-17, FR-INT-18, FR-INT-19, FR-INT-20, FR-INT-21, FR-INT-22, FR-INT-23, FR-INT-24; §50.6): copy as personal view and My Staff saved views (FR-VIEW-04); grouped activity, off by default; who acted in Last activity; create a deliverable from a task with a Related link; an optimistic status pill; "2 of 5 dates" in the FR-CAP-07 message; Gantt collapse state in the URL; a Portfolio link to the Planner by home discipline.
7. **Handoffs and the milestone chain on the Timeline** (FR-INT-25, FR-INT-26, §50.7): handoff rows with needed-by and promised marks (FR-HND-01, FR-HND-06), no drag; a "Chain to milestone" filter that highlights the stored D-13 result (`chain_depth_limit`).

## Non-goals

- Quick actions on the board, the cross-project task list or phone cards; any status or label editor (§30 custom statuses).
- Enforcing a date, rewriting a lag or link, undo, or shifting successors (Should-NOT row on auto-shift; constitution I warnings over validation).
- Named or multiple baselines, task re-baselines, critical path, float (§30 CPM; parked roadmap item 37).
- The latest-date note as a health input, attention item, notification or Portfolio column.
- Handoffs or the chain on the cross-project Gantt; changes to Workload, the Weekly Planner's figures, exports' columns or reports beyond the added "since re-baseline" figure.
- Anything AI (constitution II). Deferred-with-trigger items of the report (ElectricSQL, the TypeScript port) are not touched.

## Decisions (defaults apply while dark)

| # | Decision | Default |
|---|---|---|
| D1 | Where row quick actions appear | Project Task List desktop table only |
| D2 | Which actions | Open, Start, Block |
| D3 | "On this project" | Current team plus people removed within 90 days |
| D4 | Picker order without a project | Unchanged |
| D5 | Where the date hint shows | Task and deliverable panels, drag confirmation |
| D6 | Availability in the hint | Only inside §40.10 D8 |
| D7 | `slip_display_days` | 0 |
| D8 | What a re-baseline stores | Current planned dates; one baseline; clearable |
| D9 | Slip measures from | Original date always; since-re-baseline figure added when a baseline is set |
| D10 | Who re-baselines | Project PM; milestones and deliverables |
| D11 | Latest-date note scope | Active projects with a target; open milestones and deliverables |
| D12 | Activity grouping | Off by default; 10 minutes |
| D13 | Deliverable from a task | Prefill plus Related link; task not moved |
| D14 | Flags | One per part; §50.6 shares one |
| D15 | Handoff rows | Project Timeline; Cancelled not drawn |
| D16 | Chain source | Stored D-13 result |

Each row is added to `specs/DECISIONS.md` (§43) by the integrator. A pending row blocks only its flag being turned on.

## Acceptance

Acceptance criteria are AC-INT-01, AC-INT-02, AC-INT-03, AC-INT-04, AC-INT-05, AC-INT-06, AC-INT-07, AC-INT-08, AC-INT-09, AC-INT-10, AC-INT-11, AC-INT-12, AC-INT-13, AC-INT-14, AC-INT-15, AC-INT-16, AC-INT-17, AC-INT-18, AC-INT-19, AC-INT-20, AC-INT-21, AC-INT-22, AC-INT-23, AC-INT-24, AC-INT-25, AC-INT-26, AC-INT-27, AC-INT-28 and AC-INT-29, as written in §50.8. Decisions and deferred proposals are in §50.9.

Every AC is cited by an automated test in the slice that adds it (see [tasks.md](tasks.md)). AC-INT-29's accessibility part is automated with axe; keyboard and screen-reader spot checks and Jay's visual review go in the packet's one-page acceptance record.

## Depends on

- §43 (packet 039): the flag slice (`Flags.cs` feeding `flag.*` settings, returned on `/me`, flag pair tests, retirement test). Every slice here needs it.
- Packet 040 is not required. Where its registry has already replaced a hand list this packet touches (`Views.Lists`, the audited field list, label families), the slice adds its entry to the registry instead, and 040's superset snapshot tests cover it. Where its golden `openapi.json` exists, slices that add response fields regenerate it.
- Packet 041 is not required. The quick actions follow whatever permission source the row's inline edits use, so 041's server can-flags take over without a change here.
- Packet 042 is not required. The optimistic pill keeps local pending state only.
- Packet 045 is not required. Quick actions never appear on its deleted-items list (§49).
- Packet 043: if it converts a screen this packet adds to (the Portfolio link of FR-INT-24, the Milestone and Deliverable panels), the conversion carries this packet's flagged additions as slots and runs its parity run with each flag off and on (§47.6).
- Inside the packet: S02 after S01; S05 after S04; S07 after S06; S08 after S07. All other slices are independent of each other. S16 removes the six flags after Jay turns them on.

## Flags

`flag.row_quick_actions` (§50.2), `flag.picker_project_rank` (§50.3), `flag.date_context` (§50.4), `flag.schedule_baseline` (§50.5), `flag.small_ux` (§50.6), `flag.timeline_handoffs_chain` (§50.7). All ship dark. No existing feature is put behind a flag.

## Wording rule

Code, interface strings, specification text, commit messages and this packet's records name no third-party product.
