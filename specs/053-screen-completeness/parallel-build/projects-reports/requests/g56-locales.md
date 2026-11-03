# G56 C exact locale proposal

Server Text.cs: `issueRootCause.required` = "Choose a root-cause category."; `issueRootCause.activeRequired` = "Choose an active root-cause category."; `issueRootCause.onlyResolution` = "A root cause can be recorded only when resolving an issue."; `issueRootCause.integrity` = "Root-cause records are inconsistent. Correct them before reading the complete summary."

SPA en.ts keys below are new. Existing issue/common keys remain reused.

| Key | English |
|---|---|
| issueRootCause.title | Resolved issues by root cause |
| issueRootCause.scope | All resolved issues in this project. Opening a count clears other filters. |
| issueRootCause.notRecorded | Not recorded |
| issueRootCause.retired | Retired |
| issueRootCause.empty | No resolved issues in this project. |
| issueRootCause.label | Root-cause category |
| issueRootCause.last | Last resolution cause |
| issueRootCause.choose | Choose a root cause |
| issueRootCause.noCategories | No active root-cause categories. Ask an administrator to configure the list. |
| issueRootCause.reason | Reason |
| issueRootCause.reasonHint | A correction reason is required for completed projects. |
| issueRootCause.uncertain | The result is uncertain. Retry the same resolution; its inputs and version are preserved. |
| issueRootCause.retry | Retry same resolution |

Exact public integration: `IssueRootCauses.Flag`, `Map(RouteGroupBuilder)`, `Filter(IQueryable<Issue>, string?, bool)`, `Task<IssueRootCauseValue> LockSelection(HubDb, Guid?)`; `IssueRootCauseReplay.Guard` and `Task Authorise(HttpContext, Access, HubDb, SettingsStore)`. DTOs are top-level in Hub.Api.Features: IssueRootCauseChoice(Id,Name,IsActive,SortOrder), IssueRootCauseValue(CategoryId,RecordedName), IssueRootCauseCount(CategoryId,Name,IsActive,Count), IssueRootCauseSummary(ProjectId,ResolvedTotal,NotRecordedCount,Causes).

Registers.Map owns the summary route mount and transition marker. Root middleware invokes Authorise before generic receipt claim/replay. Root Me.ReferenceView appends null-omitted IssueRootCauses choices on register_screens. Root saved views/ListExports accept lowercase `rootCause`; parser accepts one GUID or `not-recorded` only on. IssueList applies the predicate for list/export; Risk behavior and export columns unchanged.

Query keys: summary `['issue-root-causes', projectId, viewerId, true]` (aligned with root's shared invalidation prefix); choices `['reference', 'issue-root-causes', viewerId, enabled]`; main issue list now adds viewerId/register flag, and issue detail appends register flag to its existing viewer/work/people key. Root invalidates summary on issue transitions and current-label/choices reads on catalogue changes. All enhanced components are absent off; current-key errors hide cached enhanced data.
