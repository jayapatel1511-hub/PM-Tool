# Preserve Home's project-status predicate without derived cohort URLs

Scalar presets/base homeProjects solve role and base-project scope transport. A selected Home `projectStatus` is still an independent basis filter. Sending only base projects+homePreset to Tasks/Team would include records in other lifecycle states; sending the derived matching project IDs would reintroduce the unbounded URL problem.

Please coordinate a small flagged read bridge:

- Tasks API/TaskFilter description/WorkspaceTasks URL filters: `homeProjectStatus` (comma-separated canonical project statuses), honored only under home_overview, applied to the already-visible project query before the shared preset and task filters; invalid values refuse, never widen. Add a named/removable URL token so this basis is visible.
- Team UI WorkspaceTeam passes/preserves the same key. The Team server reader is owned here and will implement it with the same validation and predicate.
- Home task/chart/team links will carry `homeProjectStatus=<existing selected projectStatus>` plus ordinary base projects and scalar homePreset. Existing Home deadline-only links retain native projectStatus too. Projects Active/OnTrack cards need no extra predicate when selected status includes Active; if it excludes Active they will pass existing ids=none, reproducing the zero Active-only count. All other preset project-card links use root's homeProjects base-scope bridge and mine=false.

This is a read-only transport of an existing Home filter, not a new metric or workflow rule. Flag off ignores it. Please include this in shared metadata and exact-link tests; no census/permission workaround.

Home backend is now ready: Team reader implements this property with canonical validation and flag guard. HomeVisualPresetTests includes exact Task/Team status parity and the Supervisor `projects=mine` versus visible nonmember counterexample. Task metadata/API/URL and Team UI patch remain root/Work prerequisites before those tests and generated contracts can pass. Home builds scalar links only, never derived cohort ID CSVs.
