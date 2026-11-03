# S08 task drilldown bridge

Owned WorkspaceTasks now preserves and names `disciplineId` under `home_overview`, for both `/tasks` and `/boards`. The existing `assigneeId` URL remains supported; comma-separated selections retain their entire wire value and show a selected count instead of pretending a CSV is one person. Scalar names use returned rows when available, otherwise existing Missing. No cohort IDs are sliced or silently dropped, and no permission/scope rule is duplicated.

Canonical TaskFilter already accepts comma-separated Guid arrays for both keys (`DisciplineIds`, `AssigneeIds`), not merely scalar values. This bridge forwards the complete string. Root owns saved-view metadata/API generation; `disciplineId` is an explicit URL-only literal in the typed key assertion pending that shared metadata integration.

For unbounded role presets, prefer a scalar server-resolved preset selector scoped to the current actor over emitting a very long URL list. Work has not invented that selector independently; please supply Home/root's exact scalar contract before adding it here. Until that is applied, Home must not claim a preset count-to-list match that requires an incomplete/truncated cohort.
