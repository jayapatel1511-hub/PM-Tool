# Scalar Home preset integration / exact card destinations

Root's HomePresets shared resolver supersedes request005's tentative ID CSVs. Home will use scalar `homePreset=ProjectManager|DisciplineLead|Supervisor`, resolve afresh inside current access/project filters, and show the logical preset plus exact project/discipline/people counts. Missing/unheld preset never widens silently.

Root already assigned Work the scalar task API/URL bridge. The same bounded property is needed by two other **existing Home card destinations**, to avoid sending unbounded derived project IDs:

1. Projects/On Track cards: root/Projects owner add `homePreset` to ProjectQuery/OpenAPI + current URL/read allowlist and apply `HomePresets.Resolve(...).ApplyProjects` after ordinary visible-project filters under home_overview. Current Home status filters that exclude Active will send existing `ids=none` for these Active-only metrics; otherwise ordinary status/computedHealth filters suffice. Keep saved-view/area-tab allowlists if the property is preserved there. No canonical project visibility change.
2. Team Members card: this lane owns the existing Workspace.cs `/team` reader and can add the same project-cohort predicate. Capacity owner must preserve/pass `homePreset` from WorkspaceTeam.tsx. Team members remain distinct active memberships of those projects; the DL/Supervisor task-only filter does not redefine that card.

Home deadline-only drill-down stays on `/home` with the scalar preset. Chart/task count drill-down uses `/tasks` plus the same preset and ordinary status/open/date filters. No derived ID list is truncated. Copied scalar role-preset links re-resolve the recipient's held role and permitted cohort; UI must name that current-viewer role filter. They do not grant the sender's role or show hidden project IDs.

Home role presets are explicit Home filters, unlike the saved global workspace selection. The base workspace selector/normal view tabs retain their existing base project selection; new role-filter chips state the additional Home narrowing. Please review this boundary against FR14/FR-VSN17 during integration rather than silently pretending unrelated calendar/file views already implement owner/discipline presets.

Home preference metadata is planned inside flagged `HomeRead.preset` (available codes, filtered saved code, applied code, effective project/discipline/people counts and exclusion disclosure). This avoids a new GET prerequisite breaking old Home consumers. An owned PUT `/me/home-preset` stores only an explicit permitted code/null. GET stays read-only. On next visit without explicit URL scope/filter state, UI restores a saved permitted choice into the URL; explicit URL scope wins. No preset is applied on server GET by default.
