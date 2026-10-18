## 54. Company Structure Amendment

Approved scope: drafted on 2026-10-18 from Jay's request: "we will have to make company structure for managers". It follows the organisation model Jay chose for the separate Friday capacity app: a free tree of units, levels the company names itself, one leader per unit and one direct manager per person. Approved by Jay the same day with the listed defaults (050-D1). The amendment is additive only: nothing in this section removes, hides, narrows or freezes a current feature, screen, route, report, setting or rule. Where an addition touches existing behaviour, today's behaviour stays the default.

The Hub serves one organisation, so this section has no companies or tenants. It keeps `User.supervisor_id`, maintained under FR-ORG-07, as the one direct manager (§8.8, Q19) and adds a unit tree beside it. Units change who a manager can **see and roll up**; they never widen who may **edit** work. Project visibility (§8.7), Restricted projects and every write permission in §8.5, §8.6 and §8.10 are unchanged. Nothing here ranks or scores people (§30, constitution II).

Where this section adds to §8.2 and §8.8 (roles and how they are assigned), §13.11 (Workload and Planner), §13.12 (Portfolio), §13.15 (Administration), §13.19 (My Staff), §19 (reports), §20.1 (what is logged) and §42 (supervisor capacity), this section governs.

### 54.1 Scope and boundary

- Levels and units: §54.2.
- People in the structure: §54.3.
- What a manager sees: §54.4.
- Structure on existing screens and reports: §54.5.
- Administration, import and history: §54.6.

§54.7 sets how it ships. Out of scope: several companies, editing rights granted by unit, matrix (dotted-line) managers, cost centres or finance hierarchies, approval chains, headcount budgets, and synchronising units from another system.

### 54.2 Levels and units

- **FR-UNT-01.** A System Administrator MUST be able to define an ordered list of **levels** and name each one, for example Region, Business unit, Operating centre, Team. The default list is Region, Business unit, Team. A level in use can be renamed and reordered but not deleted.

- **FR-UNT-02.** A **unit** MUST have a name, a level, an optional parent unit, an optional leader (one active user), an optional discipline (§12.2 reference list), an optional office (§8.2 reference list) and an optional closed date. Units form one tree with any number of roots. A unit's level MUST sit below its parent's level in the level order; skipping levels is allowed. The server refuses a parent change that would create a cycle.

- **FR-UNT-03.** A unit with active people or open child units cannot be closed. A closed unit keeps its history, is hidden from pickers and appears in reports only for dates before it closed. Units are never deleted once anyone has been placed in them.

- **FR-UNT-04.** The same discipline MAY appear on several units, and one unit MAY span offices. The discipline and office on a unit are labels used for filters and defaults; they do not change any person's home discipline (FR-GRP-01) or office.

### 54.3 People in the structure

- **FR-UNT-05.** Each active user MUST have at most one **home unit** (`User.unit_id`). A person's direct manager stays `User.supervisor_id`; it MAY sit in another unit. A unit leader need not be placed in the unit they lead.

- **FR-UNT-06.** The server MUST refuse a manager change that makes a person their own direct or indirect manager, and MUST list existing cycles on the Admin structure page. A cycle grants no indirect scope (FR-UNT-08) from any person on it.

- **FR-UNT-07.** Users without a home unit MUST appear under "No unit" wherever the structure is shown, and in an Admin to-do list with users who have no manager.

### 54.4 What a manager sees

- **FR-UNT-08.** A user's **people scope** MUST be the union of:
  - their direct reports (today's supervised staff, §8.8);
  - when the organisation setting `include_indirect_reports` is on (default off), every person below them in the reporting line;
  - for each unit they lead, every person whose home unit is that unit or one of its descendants.

  Executives and System Administrators keep their existing all-staff scope.

- **FR-UNT-09.** People scope MUST govern read access only: My Staff (§13.19), the person's My Work read-only view, the Workload and Planner rows, Vacation and Coverage, and §42's capacity column. The rights to staff people on projects, reassign their tasks and approve their records stay with the direct manager, PM and Discipline Lead exactly as today. A unit leader who is not someone's direct manager sees that person but gains no edit right over them.

- **FR-UNT-10.** Restricted projects stay restricted. A person's work on a project the viewer cannot see MUST be excluded from the viewer's lists, totals and counts in the same way as FR-PLN-19, with the same partial-view note.

- **FR-UNT-11.** A unit leader holds the Supervisor system role's read capabilities over their unit scope without being added to `HUB-Supervisors`. Holding the Supervisor role still requires the existing group or override for direct-report edit rights.

### 54.5 Structure on screens and reports

- **FR-UNT-12.** My Staff MUST add a scope switcher: "My direct reports", "All my reports" (only when `include_indirect_reports` is on) and each unit the user leads. The cards of §13.19 and §42 are computed for the chosen scope. With only direct reports in scope, the page is unchanged.

- **FR-UNT-13.** Workload, Weekly Planner and Vacation and Coverage MUST add "Group by unit" beside the existing groupings, and a unit filter that includes descendants. Group subtotals add the people in that unit subtree; a person appears once, under their home unit.

- **FR-UNT-14.** A project MAY name an optional **lead unit**, set by its PM or an Admin. Portfolio (§13.12) and the project list MUST add a lead-unit filter that includes descendants, and "No unit" for projects without one. A lead unit never changes who can view or edit the project.

- **FR-UNT-15.** An **Organisation** page MUST show the tree with each unit's level, leader, discipline, office, active head count and, for people the viewer has in scope, the person list. Every signed-in user can see unit names, levels and leaders; person lists follow people scope.

- **FR-UNT-16.** The Staff Assignments report and the Team Capacity report (§51.3) MUST add unit and unit-path columns and a unit filter. Counts are by unit, never a per-person score.

### 54.6 Administration, import and history

- **FR-UNT-17.** The Admin screens (§13.15) MUST let a System Administrator create, rename, move and close units; set levels; set leaders; and place people, singly or in bulk. Moving a unit moves its whole subtree. Every change MUST write one activity entry with old and new values (§20.1).

- **FR-UNT-18.** An Admin MAY import units and placements from a CSV file with columns `unit_path` (names joined by " / "), `level`, `leader_email`, `discipline`, `office` and, in a second file, `email`, `unit_path`, `manager_email`. The import MUST validate everything first, show a preview of creates, moves and unchanged rows, and apply all or nothing. It never deletes a unit or clears a field left blank.

- **FR-UNT-19.** Placement and leadership changes MUST be effective from the time they are saved. Reports for past weeks use each person's home unit at the end of that week, read from the activity log of FR-UNT-17, so earlier totals do not move when someone changes unit.

### 54.7 Shipping dark

- **FR-UNT-20.** The amendment MUST merge dark behind `flag.org_structure` under §43's flag rules and is turned on by Jay. With the flag off, every screen, response, report and export is as it was before this amendment, and `supervisor_id` keeps its current meaning.

### 54.8 Acceptance criteria

- **AC-UNT-01.** Given levels Region > Business unit > Team, when an Admin creates Atlantic > Civil > Civil Halifax Team and places Alex there, then the Organisation page shows the path, leader and a head count of 1 at every level.

- **AC-UNT-02.** Given a unit at level Team, when an Admin tries to make it the parent of a Region unit, or to move a unit under its own descendant, then the server refuses with a reason and nothing changes.

- **AC-UNT-03.** Given Pat leads Civil and is not Alex's direct manager, then Pat sees Alex on My Staff under the Civil scope, opens Alex's My Work read-only, and is refused when reassigning Alex's task or adding Alex to a project team.

- **AC-UNT-04.** Given Alex has work on a Restricted project Pat cannot see, then that work and its hours are absent from Pat's My Staff, Workload and reports for Civil, and the partial-view note is shown.

- **AC-UNT-05.** Given `include_indirect_reports` is off, then Sam sees only direct reports; when it is turned on, "All my reports" lists every person below Sam, and a reporting cycle grants no indirect scope and is listed for Admins.

- **AC-UNT-06.** Given Workload grouped by unit and filtered to Atlantic, then every person whose home unit is in Atlantic's subtree appears exactly once, and the subtotals add up to the Atlantic total.

- **AC-UNT-07.** Given a CSV import with one unknown manager email, then the preview lists the error, nothing is applied, and a corrected file applies every row in one transaction.

- **AC-UNT-08.** Given Alex moved from Civil to Structural in week 44, then the Team Capacity report for week 43 still counts Alex under Civil.

- **AC-UNT-09.** Given `flag.org_structure` is off, then existing API tests and browser suites pass unchanged, and responses, reports and exports carry no new fields.

### 54.9 Decisions

Each decision below uses its default until Jay decides otherwise. The rows are also in `specs/DECISIONS.md` as 050-D1 to 050-D8.

| # | Decision | Default used |
|---|---|---|
| D1 | Approve this amendment's scope | Approved by Jay 2026-10-18 with the listed defaults |
| D2 | Default level names | Region, Business unit, Team; Admin renames |
| D3 | Do unit leaders get edit rights over their unit's people? | No; read and roll-up only; edits stay with the direct manager, PM and Discipline Lead |
| D4 | Include indirect reports by default? | No; organisation setting `include_indirect_reports`, off |
| D5 | Source of manager and unit data | Hub Admin screens and CSV import; Entra ID `manager` sync stays TBD (§8.8) |
| D6 | Project lead unit | Optional, filter only; no access effect |
| D7 | History for past reports | Home unit at the end of each week, read from the activity log |
| D8 | Who may see the person list of a unit outside their scope? | Nobody but Executives and Admins; unit names, levels, leaders and head counts are visible to all |
