# Feature packets

The product specification has 34 Spec Kit packets: the original 18 first-release and 6 Phase 2 packets, plus 9 approved coordination additions (025–033) and 1 approved planning addition (034). Packets 025–027 have foundation implementations with passing automated checks; full acceptance and implementation of 028–034 remain pending. The user's confirmation of the six pictured views moved the Portfolio, Workload, improved Timeline,
saved views, and three new workspace/calendar/task-hour packets into the first release (§36). Each folder holds a `spec.md` in Spec Kit's format and a quality checklist;
`plan.md`, `tasks.md`, and `verification.md` are added as each packet is planned and built.

- Product specification: [`Engineering-Project-Coordination-Hub-Specification.md`](../Engineering-Project-Coordination-Hub-Specification.md).
  It remains the source of truth (constitution, principle IV); every packet requirement cites the
  specification ID it comes from, written like *(FR-TSK-03, R-01)*.
- Coverage: [TRACEABILITY.md](TRACEABILITY.md) lists every ID in the specification
  with the packets that cite it. `python3 tools/trace_spec.py --check` fails if any ID or section
  is left without a packet.
- Visual context: use only [Coordination Hub V2](../docs/reference/coordination-hub-v2/README.md) as a downloaded UI visual clue. The [six-panel image](../docs/reference/six-view-workspace.png) records user-confirmed feature scope; the older [wireframes](../Engineering-Project-Coordination-Hub-UI-Mockups.md) provide historical detail. The product specification and packets govern behavior and acceptance, not mockup layout.
- Rules for all work: [constitution](../.specify/memory/constitution.md) and
  [workflow](../docs/SPEC-KIT-WORKFLOW.md)

## Cross-packet rule

Packets are built in order, and some behaviour spans packets. A scenario that mentions a
notification, a computed indicator, an attention item, or health is fully verifiable once packets
005 (rules engine) and 006 (notifications) are implemented; until then, the packet verifies the
recorded change and its activity-log entry. Each packet's Assumptions name the packets it relies
on.

## First-release packets

Order follows the revised build sequence in §35.2. Start a packet only when the packets it
depends on are implemented and verified.

| Packet | Delivers | Depends on | Status |
|---|---|---|---|
| [001-platform-foundations](001-platform-foundations/spec.md) | Corporate sign-in, users and system roles, reference data and settings, the application frame, the activity log | Decisions Q1–Q3, Q6, Q7, Q15 | Built and verified |
| [002-projects-and-teams](002-projects-and-teams/spec.md) | Projects and their lifecycle, project roles and permissions, teams and discipline leads, item keys, project list | 001 | Built and verified |
| [003-milestones-and-deliverables](003-milestones-and-deliverables/spec.md) | Milestones, date changes and completion; the deliverables register with review and issue | 002 | Built and verified |
| [004-tasks-and-review](004-tasks-and-review/spec.md) | Tasks, the review workflow, collaborators, manual blocks, bulk actions, task list, task panel, board | 003 | Built and verified |
| [005-dependencies-and-rules-engine](005-dependencies-and-rules-engine/spec.md) | Dependencies; every indicator, milestone status, deliverable progress, health with override, attention items | 004 | Built and verified |
| [006-collaboration-and-notifications](006-collaboration-and-notifications/spec.md) | Comments, document links, notification centre, emails, daily digest, project following | 005 | Built and verified |
| [007-coordination-surfaces](007-coordination-surfaces/spec.md) | Project Dashboard, Weekly Coordination with meeting mode, My Work, My Staff with staffing | 005, 006 | Built and verified |
| [008-decision-register](008-decision-register/spec.md) | Decisions with external owners, decision blocks, record, defer, cancel, reopen | 005 | Built and verified |
| [009-search-filters-reports](009-search-filters-reports/spec.md) | Global search and key lookup, the shared filter bar, reports and exports, Activity History | 004 | Built and verified |
| [010-timeline-and-extras](010-timeline-and-extras/spec.md) | Baseline timeline, milestone cascade, copy project structure, reassign work for leavers | 005 | Built and verified |
| [016-portfolio-dashboard](016-portfolio-dashboard/spec.md) | Portfolio of projects needing help, both health values, trends, portfolio reports | 005, 007 | Built and verified |
| [017-resource-workload-view](017-resource-workload-view/spec.md) | 8-week workload grid, overload and cluster flags, rebalancing, workload reports | 004, 005 | Built and verified |
| [018-timeline-scheduling](018-timeline-scheduling/spec.md) | Tasks and arrows on the timeline, drag with confirmation, baselines, cascade to tasks | 005, 010 | Built and verified |
| [019-saved-views-and-board-ordering](019-saved-views-and-board-ordering/spec.md) | Personal and project views, manual card order | 004, 009 | Built and verified |
| [023-team-calendar](023-team-calendar/spec.md) | Week/Month/Agenda, deadlines, event creation, visibility | 001–004, 006 | Built and verified |
| [024-task-time-entries](024-task-time-entries/spec.md) | Time route, task-hour entry, edits and corrections, permission-filtered totals and export | 001, 002, 004, 009 | Built and verified |
| [022-six-view-workspace](022-six-view-workspace/spec.md) | Grouped project board, cross-project board and Gantt, overview dashboard, My Work views, shared navigation and scope | 002, 004–007, 009–010, 016–019, 023–024 | Built and verified |
| [011-hardening-and-pilot](011-hardening-and-pilot/spec.md) | Accessibility, scale, reliability, security readiness, support material, the eight-week pilot | 001–010, 016–019, 022–024 | Built and verified |

## Phase 2 packets

Planned for after the MVP pilot (§28), in roughly this order; all six are built and verified. Implemented versus
unverified for every packet: [docs/IMPLEMENTATION-STATUS.md](../docs/IMPLEMENTATION-STATUS.md).

| Packet | Delivers | Depends on | Status |
|---|---|---|---|
| [012-project-templates](012-project-templates/spec.md) | Templates, instantiation with a date wizard, add-from-template, snapshot semantics | 002–006 | Built and verified |
| [013-register-enhancements](013-register-enhancements/spec.md) | Client export of open decisions, bulk linking, decision log, deliverable issue history | 003, 008 | Built and verified; defaults to confirm |
| [014-risk-and-issue-registers](014-risk-and-issue-registers/spec.md) | Risk and issue registers, A-07, the issue input to health | 005–007 | Built and verified |
| [015-meeting-actions](015-meeting-actions/spec.md) | Meetings and actions, capture in meeting mode, routing, convert to task | 007, 008 | Built and verified |
| [020-notification-and-search-enhancements](020-notification-and-search-enhancements/spec.md) | Weekly PM summary, digest sections, live unread counts, search in descriptions and comments | 006, 009 | Built and verified; defaults to confirm |
| [021-dependency-lag-and-working-days](021-dependency-lag-and-working-days/spec.md) | Deliverable dependencies, dependency lag, working-day calendars | 005 | Built and verified; defaults to confirm |

"Confirm details" marks packets whose source in the specification is a single line; their
specs fill the gaps with defaults listed under Assumptions. Run `/speckit-clarify` on them before
planning.

## Approved coordination additions — staged implementation

Jay accepted the full scope on 2026-09-26. Foundation tasks T001–T005 are implemented for packets 025–027; their full-acceptance tasks and the other six packets remain open. Each folder includes a specification, implementation plan, tasks, quality checklist and honest verification status. The common permissions/model contract is in §8.10, §10.8 and §37.1; feature behaviour is in §37–§38. [DESIGN.md](../DESIGN.md) defines the intended experience.

| Packet | Delivers | Depends on | Status |
|---|---|---|---|
| [025-discipline-handoffs](025-discipline-handoffs/spec.md) | Discipline Handoffs and Acceptance | 002–006, 009 | Handoff foundation and revision-impact integration built; automated checks passed; full acceptance pending |
| [026-multidisciplinary-reviews](026-multidisciplinary-reviews/spec.md) | Multidisciplinary Reviews and Comment Closure | 003, 004, 006, 009 | Review foundation built; automated checks passed; full acceptance pending |
| [027-revision-change-impact](027-revision-change-impact/spec.md) | Revision Awareness and Change Impact | 003–006, 009, 025 | Revision/change foundation built; automated checks passed; packet 031 adapters and full acceptance pending |
| [028-submission-readiness](028-submission-readiness/spec.md) | Submission Readiness and Issue Manifest | 003, 006, 009, 025–027 | Specified; implementation pending |
| [029-dated-capacity-allocations](029-dated-capacity-allocations/spec.md) | Dated Capacity and Project Allocations | 002, 004, 006, 009, 017, 021 | Specified; implementation pending |
| [030-discipline-coordination-view](030-discipline-coordination-view/spec.md) | Discipline Coordination View | 007, 009, 019, 022, 025–029, 031–033 | Specified; implementation pending |
| [031-design-basis-assumptions](031-design-basis-assumptions/spec.md) | Shared Design Basis and Assumptions | 002–004, 006, 008, 009, 027 | Specified; implementation pending |
| [032-readiness-weekly-commitments](032-readiness-weekly-commitments/spec.md) | Ready-to-Start Planning and Weekly Commitments | 004–009, 021, 025, 027, 029, 031 | Specified; implementation pending |
| [033-location-linked-issues](033-location-linked-issues/spec.md) | Location-Linked Coordination Issues | 003, 006, 009, 014, 026, 027 | Specified; implementation pending |

Implementation order: 025/026 → 027 → 029/031 → 032/028 → 033 → integrated 030. Slashes indicate independent prerequisites, not authorisation to start sub-agents. The discipline view can expose accepted sections incrementally; its final acceptance requires the whole integrated flow.

## Approved planning addition

On 2026-10-03 Jay chose to add the weekly planning entries of the unmerged Specification v1.1 draft as a separate record beside packet 029's allocations (option (b)). The contract is §39 with §8.11, §10.9 and §13.21. Packet 029, the Workload grid and its calculations, and packet 032's readiness capacity checks stay unchanged; Manager Home, People and role-level demand are deferred proposals (§39.12).

| Packet | Delivers | Depends on | Status |
|---|---|---|---|
| [034-weekly-planning-layer](034-weekly-planning-layer/spec.md) | Weekly Planning Layer: person × week planning entries with confidence and private drafts, the Weekly Planner and My Week | 001, 002, 006, 009, 017, 019, 021, 029 | Specified and planned; open decisions to confirm (§39.12); implementation pending |

Implementation order: after packet 029's built allocation model, which 034 reads but does not change. 034 is independent of 030–033.

## Phase 3: not yet packets

The specification lists these as designed for, not built (§29), with too little detail to specify
without inventing requirements. Each becomes a packet after discovery: Microsoft Teams
notifications (FR-NOT-04), SharePoint document picking, ERP or Vantagepoint project sync,
read-only project financials, HR-sourced utilisation and resource planning (manual dated allocations are now packet 029 and manual weekly planning entries packet 034), client or external
access, advanced portfolio reporting, per-project threshold overrides, a personal calendar feed,
and a second interface language if not done earlier. Cross-project dependencies (FR-DEP-10) are
also Phase 3.

## Decisions to settle before planning 001

These come from §34 of the specification. Each has a default the plan can use if it is not
decided, but the plan records which one it assumed. Q19 (direct reports only) and Q21
(enter task hours under Time) are decided.

| Decision | Default if undecided |
|---|---|
| Q1 Backend: ASP.NET Core or Node.js with TypeScript | ASP.NET Core on the current LTS release. The specification says .NET 8, but its support ends in November 2026. |
| Q2 System roles: Entra app roles on security groups, in-app, or both | App roles on groups, plus in-app additions |
| Q3 Graph permissions for directory sync and the service mailbox | Request both; fall back to manual deactivation and another mail service |
| Q6 Organisation time zone and date format | One organisation time zone; ISO dates |
| Q7 Bilingual interface | Strings externalised; English only at launch |
| Q15 CI/CD platform | Whichever the organisation already uses |
| Q21 Time menu: task-hour entry or reserved route | Decided: enter task hours in the first release (§36.8) |

## Next step

The first two increments implement foundations for packets 025–027. Review their verification records, then continue with 029/031 before dependent 032/028, respecting the dependency order and existing hardening findings. Full application acceptance for packets 025–033 remains pending. Do not reuse the original 24-packet completion claim for the expanded product.
