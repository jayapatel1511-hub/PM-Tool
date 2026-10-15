# Feature Gap: What We Have vs. What We Need to Adopt

2026-10-09 / v1.2 update: classifications below compare specification scope, not verified application functionality. HR-approved leave CSV, Vacation & Coverage, weekly copy/publish and connected staffing are now MVP requirements; personal ICS is MVP-Recommended. Live HR/two-way calendar connectors remain Phase 3.

Date: 2026-10-01
Inputs: Hub Specification v1.0 (`spec-parts/`, 2026-09-15) and the Tuesday Consolidated Product Scope (`docs/Tuesday-Consolidated-Product-Scope.md`, brainstorm of 2026-10-01).

Legend for the **Status** column:

| Status | Meaning |
|---|---|
| **Have** | Already specified in the Hub spec at the stated phase. No change. |
| **Have, adjust** | Exists in the Hub spec but Tuesday changes its behaviour, phase or ownership. |
| **Adopt** | Not in the Hub spec. New requirement from Tuesday. |
| **Drop / defer** | Hub item Tuesday makes less important or explicitly keeps out. |

Hub section references are to `spec-parts/`. Tuesday section references are to the scope document.

---

## 1. Headline shift

The Hub spec is a **project coordination engine** with resource planning as a derived Phase 2 view. Tuesday makes resource planning a **second, co-equal engine** that managers own directly and that works without tasks, estimates or even a project record.

| Dimension | Hub v1.0 | Tuesday |
|---|---|---|
| Resource planning phase | Phase 2 (§12.15), Phase 3 for leave/capacity | MVP core |
| Source of hours | Derived from task `estimated_hours` × remaining progress | Manual person × week allocations; task estimates become optional context |
| Who edits load | Nobody directly; it is computed | Managers (planning layer) and employees (own entries) |
| Work outside the system | Not represented | First-class: proposals, admin, small projects, training, leave |
| Uncertainty | Single `load_pct`, plus "unestimated" bucket | Confirmed / Expected / Possible, plus Draft / Published / Confirmed visibility |
| Private data | None; all project data visible per §8.7 | Manager private drafts invisible to the employee |
| Capacity default | 40 h (Q18) | 37.5 h |
| Product name | Engineering Project Coordination Hub | Tuesday |

Everything below follows from this shift.

---

## 2. Project engine (what we have, mostly keep)

| # | Feature | Hub ref | Status | Note |
|---|---|---|---|---|
| 2.1 | Projects: CRUD, statuses, phase, links, archive | §12.1, §27.2-3 | Have | Tuesday §7 matches. |
| 2.2 | Team and disciplines, discipline leads, project roles | §8, §12.2 | Have | Tuesday §5.3, §6 restate "status rolls up, control stays local". Already the Hub model. |
| 2.3 | Milestones, deliverables, lifecycle, At Risk | §12.3, §12.4 | Have | |
| 2.4 | Tasks with review workflow, single assignee | §12.5 | Have, adjust | Tuesday never mentions tasks. Keep them, but they are no longer the source of load. `estimated_hours` becomes optional. |
| 2.5 | Finish-to-Start dependencies, waiting/blocked | §12.6 | Have | Tuesday §8 lists "unresolved dependency" as an exception. Covered. |
| 2.6 | PM Attention Required engine, configurable thresholds | §12.12, §15.11 | Have, adjust | Add new rule inputs: stale discipline update, resource concern raised by a discipline lead (Tuesday §8). |
| 2.7 | Project and discipline health, override, snapshots | §16 | Have | Tuesday §8 "is each discipline moving" = Hub discipline status §16.6. |
| 2.8 | Weekly Coordination meeting view | §12.13, §13.9 | Have | Not in Tuesday. Keep; it is the PM's "chase only where stale" tool. |
| 2.9 | Thin Decision Register | §12.9 | Have | Tuesday lists decisions throughout. Promote from Recommended to Required. |
| 2.10 | Risks and Issues registers | §12.10 (P2) | Have, adjust | Tuesday lists risks/issues in the PM workspace (§5.2, §7). Consider pulling a thin version into MVP, same pattern as Decisions. |
| 2.11 | Comments, mentions, document links | §12.7, §12.8 | Have | Tuesday §10 "creator-owned comments" is already Hub §8.6 ownership. |
| 2.12 | Activity log, notifications, search, reports | §17-20 | Have | Extend notification catalogue and audit log to allocations (see §4). |
| 2.13 | Project Templates, Meeting Actions, improved Gantt, saved views | §28 | Have (P2) | Unchanged. Lower priority than the resource engine now. |

---

## 3. Resource engine (what we need to adopt)

| # | Feature | Tuesday ref | Hub today | Status |
|---|---|---|---|---|
| 3.1 | Per-person weekly capacity (default 37.5 h, per-user override, working schedule) | §9, §12 | `default_weekly_capacity_hours` 40 h, override mentioned in §12.15 | Have, adjust |
| 3.2 | Planned leave reducing capacity | §9, §12, §30 | Phase 3 ("import from HR") | MVP: HR-approved leave CSV with read-only provenance, explicit updates/cancellations and an HR-referenced Supervisor/Admin manual fallback; office holidays reduce capacity once. No live HR API is assumed. |
| 3.3 | Person × week allocation grid, 6 to 12 week rolling horizon | §13 | §13.11 grid exists but is read-only and task-derived | Have, adjust: make it editable and allocation-based |
| 3.4 | Allocation entity: person, week, hours, label, optional project link | §13, §27 | None | Adopt. Core new table. |
| 3.5 | Source category on every allocation (major project, other project, proposal, BD, training, admin, internal initiative, leave, other) | §11, §14 | None | Adopt |
| 3.6 | Employee-added entries, employee-owned | §15 | None | Adopt. Reuses Hub §8.6 ownership semantics. |
| 3.7 | Manager planning entries, manager-owned | §16 | None | Adopt |
| 3.8 | Visibility state: Private Draft / Published / Confirmed | §17 | None; Hub has no role-private data | Adopt. First privacy boundary in the data model. Needs a server-side filter, not just UI. |
| 3.9 | Confidence state: Confirmed / Expected / Possible | §22 | None | Adopt. Totals shown per confidence band, not as one percentage. |
| 3.10 | Quick add: click week, type "8 h — proposal support", Enter | §18 | None | Adopt. Parse `<number> h — <label>` client-side; everything else defaults. |
| 3.11 | Optional controls: start/end week, repeat, notes, category, move to person, link to project, convert to formal assignment | §19 | None | Adopt as a secondary panel. Not required for quick add. |
| 3.12 | Repeat across weeks, drag to week or person, copy, extend, shorten | §20 | None | Adopt. Model as one allocation with `start_week`, `end_week`; drag edits those. |
| 3.13 | Rough allocations with no project, no exact dates ("~15 h upcoming work") | §21, §27 | Everything is project-rooted | Adopt. `project_id` nullable, `label` required. |
| 3.14 | Remaining capacity = capacity − all commitments, per week | §12 | `load_pct` only | Have, adjust |
| 3.15 | Explainable totals: drill from a weekly number to its sources | §14 | Expandable Person → Project → Tasks | Have, adjust: expand to all source categories |
| 3.16 | Project-derived demand shown as context next to allocations (task estimates, upcoming deliverables) | §4, §30 | This *is* the Hub §12.15 calculation | Have, adjust: demote from "the number" to "context column" |
| 3.17 | Longer-term role-level demand ("need one civil technologist in January") | §28 | None | Adopt later. Defer to Phase 2; MVP horizon is 12 weeks of person-level hours. |

---

## 4. Conflicts, staleness and dashboards

| # | Feature | Tuesday ref | Hub today | Status |
|---|---|---|---|---|
| 4.1 | Over-allocation warning per person per week | §24 | §12.15 "Over-assigned" > 110 % | Have, adjust: compute on allocations, keep threshold configurable |
| 4.2 | Under-allocation warning | §24 | §12.15 "Under-assigned" < 40 % with caveat | Have, adjust |
| 4.3 | Overlapping manager plans on one person (cross-team conflict) | §23, §24 | None | Adopt. Rule: two or more managers' allocations on the same person-week whose sum plus confirmed work exceeds capacity. Flag both managers. Nobody's plan wins. |
| 4.4 | Leave conflict (allocation overlaps leave) | §24 | None | Adopt |
| 4.5 | Deliverable pressure (major deliverable due, assigned people over capacity) | §24, §30 | Deadline cluster rule in §12.15 | Have, adjust |
| 4.6 | Warnings are visible, never blocking | §24, §38 | Same principle in §8.1 "prefer a rule with a warning over a permission" | Have |
| 4.7 | Stale allocation / stale manager plan ("needs confirmation") | §29 | Stale update rules exist for tasks and disciplines only | Have, adjust: add `last_confirmed_at` to allocations and a staleness threshold |
| 4.8 | Manager exception dashboard: people needing attention, projects needing attention, upcoming pressure, planning conflicts, decisions | §25, §26 | My Work (§13.10) is personal; Portfolio Dashboard is P2 | Adopt. New landing page for the Supervisor / Manager role. |
| 4.9 | PM health-check dashboard per discipline: owner, status, next deliverable, due, last update, risk, stale flag | §8 | Project Dashboard §13.1 plus discipline summaries | Have. Verify §13.1 shows "last update" and "next deliverable" per discipline; add if missing. |
| 4.10 | Employee view: my projects, priorities, assigned work, weekly load with own entries | §5.5 | My Work §13.10 | Have, adjust: add weekly hours strip and quick add for own entries |
| 4.11 | Leadership view: capacity trends, staffing pressure, utilisation | §5.6 | Portfolio Dashboard P2 | Have (P2). Extend with capacity data once the resource engine has history. |
| 4.12 | Decision-oriented wording ("two competing deadlines next week", "Bennett has 18 h available") | §25 | Attention items have reason text | Have, adjust: write rule reason templates for the new rules |

---

## 5. Roles and permissions

| # | Change | Tuesday ref | Hub today | Status |
|---|---|---|---|---|
| 5.1 | Resource / Team Manager role owns a planning layer for supervised staff | §5.1, §16 | Supervisor system role: read Resource View, reassign tasks | Have, adjust: Supervisor gains create/edit/publish on allocations for supervised people |
| 5.2 | Employee creates and owns personal entries | §15 | Ownership rules §8.6 | Have, adjust: extend to Allocation |
| 5.3 | Private drafts hidden from the employee and from other managers | §17 | No private data in Hub | Adopt. Permission matrix row: read Draft allocations = creator only (plus Sys Admin for data correction). |
| 5.4 | PM sees staffing but cannot edit allocations outside their discipline | §5.2, §6 | PM has no Resource View write today | Have |
| 5.5 | Discipline Lead allocates within own discipline | §5.3 | DL assigns tasks within discipline | Have, adjust: DL may create allocations linked to their discipline's workstream |
| 5.6 | Cross-team visibility of published allocations for anyone with the Resource area | §23 | §8.7 visibility model | Have, adjust |

---

## 6. Navigation and screens

| Area | Hub §9.4 | Tuesday §32 | Status |
|---|---|---|---|
| Landing | My Work | Home (role-based: manager exception dashboard, PM health, employee view) | Have, adjust |
| Projects | Projects | Projects | Have |
| People | Admin → Users | People (profiles with capacity, skills, leave, manager) | Adopt as a top-level area |
| Resources | Resources (P2) | Resources (MVP weekly planner) | Have, adjust: move to MVP |
| Portfolio / Reports | Portfolio (P2), Reports | Insights | Have, adjust: rename, merge |
| Notifications, Admin, Search | Present | Not mentioned | Have, keep |

New screens to specify: **Weekly Resource Planner** (editable grid, quick add, drag), **Manager Home**, **Person Profile**, **Allocation side panel**. Screens to amend: Project Dashboard (per-discipline last update and next deliverable), My Work (published My Week). Add Vacation & Coverage year/list view and People/Projects modes on Team Planner; reuse allocations rather than maintaining another plan.

---

## 7. Data model additions

New entities for Hub §10.1:

| Entity | Key fields |
|---|---|
| `Allocation` | id, person_id, created_by_id, owner_role (employee / manager / system), project_id nullable, discipline_id nullable, label, source_category, hours_per_week, start_week, end_week, confidence (confirmed / expected / possible), visibility (draft / published / confirmed), notes, last_confirmed_at, created_at, updated_at |
| `Leave` | id, person_id, start/end, hours-per-day/full-day, source kind/state, stable HR source key, source/import timestamps, row version; no private HR reason/balance |
| `OfficeHoliday` | office_id, date, name, row version; Admin-maintained availability calendar |
| `PersonCapacity` | person_id, weekly_capacity_hours (default 37.5), working_days, effective_from |
| `ResourceConflict` (materialised, like attention items) | person_id, week, type (over / under / overlap / leave / deliverable pressure / stale), reason, involved_allocation_ids, involved_manager_ids |

Changes to existing entities: `User` gains `manager_id` (Hub already has a supervisor field), `discipline_id`, `skills`; `Task.estimated_hours` becomes optional context; `OrgSetting` gains capacity default, over/under thresholds, allocation staleness days.

Audit: allocations and leave go into `ActivityLog`. Notifications: allocation published to me, allocation confirmed, conflict raised on my team, allocation stale.

---

## 8. Things Tuesday keeps out that the Hub already excluded

Timesheets, payroll, invoicing, CRM, HR, document storage, enterprise scheduling, Teams and Outlook replacement, AI assignment, individual performance tracking. Hub §30 and Tuesday §34 agree. The "no AI, every flag traceable to a rule" constraint from Hub §1 stays and is what makes Tuesday's "explain the number" principle (§38) possible.

---

## 9. Proposed MVP cut (combined)

**Required**
1. Hub §27.2 Required items 1 to 15, with Decision Register promoted to Required.
2. Allocation, Leave, PersonCapacity entities and server-side visibility filtering (3.4 to 3.9, 3.13).
3. Weekly Resource Planner with quick add, repeat, drag, remaining capacity, explainable totals (3.3, 3.10, 3.12, 3.14, 3.15).
4. Conflict rules: over, under, overlapping managers, leave, stale (4.1 to 4.4, 4.7).
5. Manager Home exception dashboard; My Work weekly load strip (4.8, 4.10).
6. People area with person profile (§6).
7. HR-approved leave CSV preview/apply, office holidays, Vacation & Coverage, dated coverage warnings, copy-last-week and batched publish (FR-RES-18…23).

**Recommended**
Personal read-only calendar subscription (FR-RES-24); project-derived demand as context column (3.16); deliverable pressure rule (4.5); thin Risk/Issue registers (2.10).

**Phase 2**
Role-level long-term demand (3.17); leadership capacity trends (4.11); planned vs actual (Tuesday §36); everything in Hub §28 not listed above.

---

## 10. Open decisions

| # | Question | Default if undecided |
|---|---|---|
| 1 | Capacity default: 37.5 h or 40 h? | 37.5 h, per Tuesday §12. Org setting either way. |
| 2 | Can a published allocation be edited by the employee, or only commented on? | Comment only; employee adds their own entry if they disagree. Preserves §10 ownership. |
| 3 | Does Confirmed visibility require the employee to accept, or is it a manager action? | Manager action. No approval workflow (Tuesday §17). |
| 4 | Do task estimates still drive anything? | Context column only. Not summed into load. |
| 5 | Who resolves a cross-manager overlap? | Nobody in-system. Both managers are flagged; resolution is a conversation. |
| 6 | Product name in UI: Hub or Tuesday? | Tuesday, pending confirmation. Spec text keeps "the Hub" until renamed. |


## 11. v1.2 Planning and Calendar Additions

| Addition | Existing foundation | Adopted scope |
|---|---|---|
| HR-approved availability | Leave/PersonCapacity | Validated CSV first; HR alone approves, explicit cancellations, exact identities, source freshness, no duplicated HR ledger |
| Vacation & Coverage | Leave conflicts, task/review due dates, existing reassignment | Daily year calendar and accessible list; dated concerns, partial-day prompts, permitted handover/reassignment |
| Weekly team routine | Allocation copy, visibility and My Week | Copy-last-week preview, creator-only Drafts, atomic publish, one employee summary |
| Connected staffing | Allocation project/discipline links | People/Projects views and reports share records and permission-filtered totals |
| Calendar subscription | Former P3 personal ICS | MVP-Recommended read-only dated events, revocable access, stable updates/cancellations; no fabricated appointment times |
| Live sync | Integration inventory | Phase 3: discover HR/calendar APIs and consent first; reconcile event identities and avoid double-counting meetings/leave |

Authoritative details: `spec-parts/05-modules-b.md` §§12.15.12, 12.18–12.19; `spec-parts/06-screens.md` §§13.10–13.11, 13.21; acceptance in §31.16. These additions do not authorise application implementation, company-data imports, connector consent or deployment.
