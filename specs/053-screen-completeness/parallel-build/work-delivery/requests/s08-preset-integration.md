# S08 task preset bridge

Generation 8 implements the scalar `homePreset` contract on both task list routes, before normal TaskQueries filters, using root's HomePresets.Resolve/ApplyTasks. An explicit enabled preset uses RepeatableRead; the off/no-query path has no added transaction or role query. Invalid or no-longer-held enabled choices retain the helper's explicit refusal. Existing project/scope/other filters intersect the preset; the caller's current cohort stays server-side without a URL ID limit.

Workspace tasks/board preserve, send, name and clear homePreset under home_overview. Discipline IDs and multi-assignee IDs remain supported without truncation. All new token naming is flag-gated. TaskFilter.Describe declares homePreset for both implemented routes. Root owns generated API/registry integration.

Locale request for root/Home owner: `home.rolePreset`: `Home preset`. The token value reuses existing `role.ProjectManager`, `role.DisciplineLead`, `role.Supervisor`. If Home adopts an equivalent key, coordinate it and replace this one reference. No shared locale file was edited.

Verification is authored separately in this lane; no lane execution/build/generation.
