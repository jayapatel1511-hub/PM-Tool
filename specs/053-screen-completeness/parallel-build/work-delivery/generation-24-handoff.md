# Generation24 S11-B handoff

Status: ALL SOURCE READY / HELD. C# was separately returned BACKEND READY and remains held. No lane edit or process is in flight. Root alone owns integration, review, execution and acceptance ledgers. No checks, types, lint, build, API/SQL/browser execution, generation, migration, helpers, goal, commit, push, deployment or activation were performed here.

## Changed owned files

- `web/src/pages/WorkspaceTasks.tsx`: flag-gated workspace column chooser; URL-authoritative normalized selection and per-viewer browser defaults; effective saved-view columns merged with scope.params; mandatory Key/Task/Project; dynamic table/group colspans and labelled phone details; existing actor/comments/action/return helpers retained. All9 legacy columns remain when off;11 existing enabled default columns retained. No new task filters or export workflow.
- `web/src/pages/workspace-task-columns.ts` (new):15 canonical layout IDs; mandatory3; legacy9/default11; unknown/duplicate normalization; URL-empty semantics; separate `hub.workspaceTaskColumns.<viewerId>` browser convenience. Project `hub.taskColumns` untouched. Structural optional Submission fact input permits absent/malformed fields so missing facts stay unavailable before/after generated contract refresh.
- `tests/Hub.Tests/Api/WorkTaskColumnsTests.cs` (new):7 Fact cases plus4 flag-combination Theory cases, all authored/unrun. Real commands create normal tasks, deliverables, source registrations, packages, target edits, current-manifest replacements, cancellation, issue and supersession. Damaged-reference and optional Required=false fixtures are explicitly labelled recovery cases; the existing manifest command creates only required entries. Manifest items are not soft-deletable, so missing-membership recovery deletes that exact fixture row instead of inventing DeletedAt.
- `web/tests/work-task-columns.cjs` (new): dedicated actual-app/mock-API acceptance, authored/unrun. Does not start a runtime. Takes existing TUESDAY_UI_BASE_URL and uses the existing fixtures/Playwright factory. Only SSE transport is mocked for the live case: actual useLiveEvents and keysForChange must refetch current workspace rows on SubmissionPackage event.

No Project Tasks, MyWork, task grouping helper, business command, canonical Handoff, source/readiness/review rule, shared registry/cache/locale/generated file or task checkbox was edited by B.

## Stable UI/wire contract

Canonical column order:

`key,name,project,deliverable,status,assignee,reviewer,dueDate,priority,estimate,progress,indicators,lastActivity,submissionDate,comments`.

Mandatory: `key,name,project`. Enabled defaults:

`key,name,project,status,assignee,dueDate,priority,progress,indicators,lastActivity,comments`.

The five required optional facts are `deliverable,reviewer,estimate,lastActivity,submissionDate`. Selecting only those plus mandatory identity yields8 columns; each group header derives colspan8 from the same definitions. Export IDs are a different dialect and remain unchanged.

Column toggles update explicit normalized URL cols, keep the page/scope/filter/group/sort/panel/hash and router state, and preserve scroll. Separate browser defaults are updated for the current user. URL wins even with empty/unknown IDs, retaining mandatory identity. Saved views capture effective default/selected IDs even if URL has no cols and retain scope.params. Existing saved-view semantics omit page; page preservation applies to column selection and exact item return, not a newly invented saved-page definition.

Root-authored optional task row fact: `submissionDate: { state: 'Known' | 'Unavailable', targetDate: string | null }`. Known+date formats the calendar date; Known+null reads No open submission; unavailable, omitted or incomplete fact reads Unavailable. Estimated0 shows0h and null shows existing Not estimated. No row fetches package/person/history detail merely to render these columns.

Root accepted locale keys: `ws.submissionDate` = Submission date; `ws.noOpenSubmission` = No open submission. Existing `common.unavailable`, `task.columns`, `task.estimate`, `task.unestimated` and field/column labels reused. Root owns all locales and generated types.

Root owns TaskQueries/Tasks/TaskSubmissionDates snapshot and date projection, ScreenLists/View metadata/sanitizer and keys invalidation. Exact optional date projection is list-only, after the authorised page, at most100/200 current rows, same snapshot as list membership and existing group/Home reads. All non-list Rows callers, disabled bytes and group metrics remain root contracts.

## Authored numeric acceptance

API:

1.3 projects ×4 tasks =12, identical deliverable labels with distinct actual IDs/3 project parents/3 leaf keys; reviewer, activity, estimated0 and date facts.
2. Earliest02-11 versus02-13; normal target edit of02-11→02-15 yields02-13; replacing current02-13 manifest with another deliverable leaves old membership stored but yields02-15; cancellation yields known-none.
3. Normal issue/supersession/cancellation exclude earlier02-07/08/09 packages while eligible02-13 remains.
4. Current optional Required=false02-10 participates; foreign-project imported membership02-06 does not; deleted package falls back02-11; missing current manifest membership produces known-none.
5. Valid no-package and null reference are Known+null; recorded deleted reference is Unavailable; GET changes0 task/package/state/activity facts. Hidden project remains inaccessible.
6.201 command-created tasks paginate100/100/1 with identical known date, exact membership and full201 group metrics.
7. Eight-column personal saved view keeps scope/filter/group/sort/cols, drops transient panel and remains private.
8–11. work_screens ×small_ux four combinations: date only on the2 enabled list GETs, not MyWork Rows; work off restores exact prior project list JSON.

Browser:

- Default11 columns;3 projects/12rows/6distinct parent+leaf keys; all5 required optional chooser entries.
- Five required optional columns +identity =8headers and every grouped colspan8;0/null estimates and actual reviewer/activity shown; unknown/duplicate IDs normalize.
- Two ordinary Views menu saves: untouched effective defaults and exact8-column choice. Scope retained, explicit saved URL overrides other browser defaults. Existing project column preference sentinel unchanged.
- SubmissionPackage SSE event refetches the current wtasks page and changes02-11→02-13 without navigation. This fails unless root's cache seam is integrated.
- Known-none, unavailable and omitted date facts at375/768/1280; phone shows selected optional labels/details and no page overflow.
-201rows; keyboard Reviewer toggle keeps page2/filter/group/sort/scope, persists separate browser default; page3contains1row; Comments exact open/section/render and close returns to exact page2 URL/cols.
- Unknown-only URL retains3mandatory columns. Off ignores stale cols, shows9 columns, offers no chooser/date and sends no grouping parameter. Only2 saved-view POSTs are expected; no business writes.

Suggested root execution after independent source review/freeze: focused WorkTaskColumnsTests plus existing relevant TaskGrouping/WorkActivity/Views checks, required type/fixture/lint/contracts/build, then `web/tests/work-task-columns.cjs` against the coordinator's existing built preview. Root may choose a smaller adjacent set where prior unchanged evidence remains valid. No authored case is a PASS claim.

## Outstanding boundaries

Root still needs to integrate/verify shared locales, View sanitizer/metadata and Submission event→wtasks invalidation, regenerate optional API contract, independently review all changed sources, then run actual focused API/browser/layout checks. A passing source authoring handoff does not check S11 or prove real runtime acceptance.

S10 seen state and generic follow-up census remain outside this loan.053-D20 remains Open. Earlier S11-A grouping/runtime evidence remains coordinator-owned; this batch does not revisit canonical single-target Handoff or broaden project/task authority.
