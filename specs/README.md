# Feature packets

The product specification has 47 Spec Kit packets: the original 18 first-release and 6 Phase 2 packets, 9 approved coordination additions (025–033), planning (034), availability/calendar (035), staffing/trust (036), calm workspace (037), supervisor capacity (038) and the 9 build-approach packets (039 to 047), which are specified and not started. Open decisions for packets 038 to 047 and the build-approach report are in [DECISIONS.md](DECISIONS.md). The iPhone companion draft in [`docs/ios`](../docs/ios/spec.md) is a separate, unnumbered track; it is not a packet here and these packets do not change it. Runtime implementation is present for packets 025–037 in integrated baseline `68c56f28`; packet 038 and the final review fixes are reconciled into PR #37 (`claude/tuesday-visual-batch1`), the line of record, which becomes main when Jay merges it (RPT-02). Full packet, hosted and human acceptance remain distinct from the passing local release checks. See the [current deployment handoff](../docs/handoffs/2026-10-12-deployment-ready.md). Packet 034's earlier implementation and synthetic-review deployment remain historical evidence; T023's manual screen-reader validation and human acceptance remain pending. The user's confirmation of the six pictured views moved the Portfolio, Workload, improved Timeline,
saved views, and three new workspace/calendar/task-hour packets into the first release (§36). Each folder holds a `spec.md` in Spec Kit's format and a quality checklist;
`plan.md`, `tasks.md`, and `verification.md` are added as each packet is planned and built. Packets 039 to 047 follow the shorter shape of packet 039 (§43.9): `spec.md`, `plan.md`, `tasks.md` as a list of one-pull-request slices, and `verification.md`, with their decisions in [DECISIONS.md](DECISIONS.md).

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

## Approved coordination additions: staged implementation

Jay accepted the full scope on 2026-09-26. Runtime implementation for all nine packets is present in the integrated baseline. Historical task checklists and verification records retain incomplete full-acceptance scenarios; they are not evidence that the runtime is absent. Each folder includes a specification, implementation plan, tasks, quality checklist and verification status. The common permissions/model contract is in §8.10, §10.8 and §37.1; feature behaviour is in §37–§38. [DESIGN.md](../DESIGN.md) defines the intended experience.

| Packet | Delivers | Depends on | Status |
|---|---|---|---|
| [025-discipline-handoffs](025-discipline-handoffs/spec.md) | Discipline Handoffs and Acceptance | 002–006, 009 | Handoff foundation and revision-impact integration built; automated checks passed; full acceptance pending |
| [026-multidisciplinary-reviews](026-multidisciplinary-reviews/spec.md) | Multidisciplinary Reviews and Comment Closure | 003, 004, 006, 009 | Review foundation built; automated checks passed; full acceptance pending |
| [027-revision-change-impact](027-revision-change-impact/spec.md) | Revision Awareness and Change Impact | 003–006, 009, 025 | Revision/change foundation built; automated checks passed; packet 031 adapters and full acceptance pending |
| [028-submission-readiness](028-submission-readiness/spec.md) | Submission Readiness and Issue Manifest | 003, 006, 009, 025–027 | Runtime implemented; full acceptance pending |
| [029-dated-capacity-allocations](029-dated-capacity-allocations/spec.md) | Dated Capacity and Project Allocations | 002, 004, 006, 009, 017, 021 | Runtime implemented; full acceptance pending |
| [030-discipline-coordination-view](030-discipline-coordination-view/spec.md) | Discipline Coordination View | 007, 009, 019, 022, 025–029, 031–033 | Runtime implemented; full acceptance pending |
| [031-design-basis-assumptions](031-design-basis-assumptions/spec.md) | Shared Design Basis and Assumptions | 002–004, 006, 008, 009, 027 | Runtime implemented; full acceptance pending |
| [032-readiness-weekly-commitments](032-readiness-weekly-commitments/spec.md) | Ready-to-Start Planning and Weekly Commitments | 004–009, 021, 025, 027, 029, 031 | Runtime implemented; full acceptance pending |
| [033-location-linked-issues](033-location-linked-issues/spec.md) | Location-Linked Coordination Issues | 003, 006, 009, 014, 026, 027 | Runtime implemented; full acceptance pending |

Implementation order: 025/026 → 027 → 029/031 → 032/028 → 033 → integrated 030. Slashes indicate independent prerequisites, not authorisation to start sub-agents. The discipline view can expose accepted sections incrementally; its final acceptance requires the whole integrated flow.

## Approved planning addition

On 2026-10-03 Jay chose to add the weekly planning entries of the unmerged Specification v1.1 draft as a separate record beside packet 029's allocations (option (b)). The contract is §39 with §8.11, §10.9 and §13.21. Packet 029, the Workload grid and its calculations, and packet 032's readiness capacity checks stay unchanged; Manager Home, People and role-level demand are deferred proposals (§39.12).

| Packet | Delivers | Depends on | Status |
|---|---|---|---|
| [034-weekly-planning-layer](034-weekly-planning-layer/spec.md) | Weekly Planning Layer: person × week planning entries with confidence and private drafts, the Weekly Planner and My Week | 001, 002, 006, 009, 017, 019, 021, 029 | Implemented in c829f0e6, deployed to synthetic review in d81c13f9; software/browser and deployed safety checks PASS; T023 manual screen-reader BLOCKED; human acceptance pending |

Implementation order: after packet 029's built allocation model, which 034 reads but does not change. 034 is independent of 030–033.

## Phase 3: not yet packets

The specification lists these as designed for, not built (§29), with too little detail to specify
without inventing requirements. Each becomes a packet after discovery: Microsoft Teams
notifications (FR-NOT-04), SharePoint document picking, ERP project sync,
read-only project financials, HR-sourced utilisation and resource planning (manual dated allocations are now packet 029 and manual weekly planning entries packet 034), client or external
access, advanced portfolio reporting, per-project threshold overrides, and a second interface language if not done earlier. Cross-project dependencies (FR-DEP-10) are
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

Review the current local candidate and its [deployment handoff](../docs/handoffs/2026-10-12-deployment-ready.md), then authorize the exact release activation separately. Complete the hosted scenarios and human sessions retained in each packet's verification record. Do not reuse the original 24-packet completion claim for expanded-product acceptance.

## Availability and calendar extension

[035: Availability, weekly allocation and personal calendar](035-availability-weekly-calendar/spec.md) extends the approved v1.2 requirements and records local verification separately from deployed/client acceptance.

## Staffing signals and calm workspace

On 2026-10-11 Jay approved both packets and accepted their default decisions ("lets start build"). The contracts are §40 and §41. Packet 037's shared layout (Phase 1) lands before packet 036's screens, which follow its rules R1–R10.

| Packet | Delivers | Depends on | Status |
|---|---|---|---|
| [036-staffing-and-signal-trust](036-staffing-and-signal-trust/spec.md) | Open roles, Start-to-Start and Finish-to-Finish links, the Status not moving rule (A-21), capacity grouping, capacity in the person picker, counts that match their lists, timelines that name their dates | 005, 007, 016–018, 021, 022, 029, 034 | Locally implemented and checked; hosted/human acceptance pending |
| [037-calm-workspace](037-calm-workspace/spec.md) | One-row toolbars, grouped project tabs, a shorter sidebar, one-line headers, one signal per row, My Work opening on Today | 019, 022, 034 | Locally implemented and checked; hosted/human acceptance pending |

## Supervisor capacity signals

On 2026-10-12 a gap review of the October 2026 product research against §39–§41 left two recommendations open. Packet 038 records them as §42 and rewrites the stale v1.1 rows 16 and 17 of §27.2. Both are implemented locally with defaults D1–D7 under Jay's build request, plus his explicit confirmation of shared Partial view for hidden Confirmed allocations. See [verification](038-supervisor-capacity-signals/verification.md).

| Packet | Delivers | Depends on | Status |
|---|---|---|---|
| [038-supervisor-capacity-signals](038-supervisor-capacity-signals/spec.md) | A "This week" capacity column on My Staff from the planner's batch read, and a daily-digest hold on working days a person has no availability | 006, 007, 034, 035, 036 | Locally implemented and verified; hosted/human acceptance pending |

## Build-approach packets

On 2026-10-14 Jay approved the build-approach report and directed building all of it: features are fixed and the work is additive only, with no deadlines. Packet 039 sets the delivery process every later packet follows, including the constitution's Development Workflow (version 1.1.0). Existing behaviour stays the default; new additions that change what pilot users see ship dark behind organisation-setting flags unless a packet lists them as unflagged corrections, each with a decision row and a Tier B merge, and no existing feature is put behind a flag. The packets are ordered by dependency only.

| Packet | Delivers | Depends on | Status |
|---|---|---|---|
| [039-delivery-process](039-delivery-process/spec.md) | Delivery process (§43): protected trunk, merge tiers, PR size check, flags, sharded CI with coverage and test-count ratchets, one CI-built image, migration lint and rollback check, review-only auto-deploy, generated verification, the decision log, nightly real-API run | PR #37 merged; Jay's sudo for the review timer (039-D12) | Specified, not started |
| [040-typed-contract-and-registry](040-typed-contract-and-registry/spec.md) | Typed contract and entity registry (§44): one registry per entity, typed results and generated client, invalidation map, saved-view and `view=` seam fixes, ratchet with a stop rule | PR #37 merged; joins 039's CI jobs where they exist | Specified, not started |
| [041-capacity-figures-and-server-values](041-capacity-figures-and-server-values/spec.md) | Capacity figures and server values (§45): honest free hours under partial view, distinct figure names, server load bands with thresholds in settings, server can-flags, one copy of each rule, edited planner rows kept | 038; 039 flags (S03, S04, S12); 040 ratchet (S08 only) | Specified, not started |
| [042-route-splitting-and-live-updates](042-route-splitting-and-live-updates/spec.md) | Route splitting and live updates (§46): build stamp and reload notice, eager registrations, asset caching, per-project stamp then change events behind flags, today's refresh kept | 039 flags (S08, S10, S11); 040 `keys.ts` (before S11); 044 error page (before S06) and dead-letter (before `flag.live_events` is on in review) | Specified, not started |
| [043-record-engine](043-record-engine/spec.md) | Record engine (§47): registry-driven list and panel, parity spike on Risks and open roles, adoption per screen, shared-table fallback | 039, 040, 042; Jay's constitution VII approval (043-D1) | Specified, not started |
| [044-operations-and-security-hardening](044-operations-and-security-hardening/spec.md) | Operations and security hardening (§48): served-folder allowlist, off-host backup and restore drill, outbox dead-letter and retry, error reference, idempotency claim, security tests, honest copy | PR #37 merged; Jay's approval before pilot data goes off-host | Specified, not started |
| [045-record-recovery-and-counts](045-record-recovery-and-counts/spec.md) | Record recovery and counts (§49): Show deleted with Restore, every count opens its list, optional working-day template offsets, discipline status bar, Portfolio grouping and overdue deliverables | 039 flags (all but the count-link slices and S06) | Specified, not started |
| [046-task-and-timeline-interaction](046-task-and-timeline-interaction/spec.md) | Task and timeline interaction (§50): row quick actions, project-ranked picker, earliest-date hints, slip threshold and PM re-baseline, small additions, handoffs and chain highlight on the Timeline | 039 flags (every slice) | Specified, not started |
| [047-planning-reporting-and-onboarding](047-planning-reporting-and-onboarding/spec.md) | Planning, reporting and onboarding (§51): getting-started card, Team Capacity and Allocations by Source reports, pilot route counter, template roles, unfilled roles beside capacity, status note, Watching and Requested by me, project import, capacity polish | 039 flags (every slice); 041 S04 (before S03, S04); 045 S07, the template offset schema and API (before S07) | Specified, not started |

Dependency order: 039 first, because its flags slice gates most later slices. 040 and 044 can start once PR #37 is merged. 041 follows 040's ratchet for one slice. 042 follows 039's flags, 040's `keys.ts` and 044's error page. 045 and 046 follow 039's flags. 047 follows 039's flags, 041's free-hours slice and 045's S07. 043 comes last, after 039, 040 and 042 and Jay's approval of its complexity entry. Each packet's `tasks.md` gives slice-level order.
