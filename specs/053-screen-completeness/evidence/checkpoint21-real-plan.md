# Checkpoint 21 real QA — execution-ready plan

Status: READY FOR EXECUTION, preparation only. Authored 2026-11-01. The paired harness is runnable only under the explicit `CHECKPOINT21_REAL_EXECUTE=1` loan; no API, DB, browser, service, flag, build, test, migration, or helper execution was performed in this preparation pass.

## Runtime loan required from root

Use only the fresh `hub_qa_checkpoint21` database on API `5088` and final build preview `8853`, with the same-origin Development `X-Dev-User` accounts. Root must explicitly loan execution after the final build is frozen. Keep all mutations to this disposable fixture and close every browser context.

## Fixture, in normal-command order

1. Enable only disposable `visual_system`, `small_ux`, `work_screens`, `project_screens`, `people_screens`, `org_structure`, `capacity_screens`, and `capacity_reports` through Admin settings if their current values are off. Record prior/current values.
2. Read `/config`, `/me`, `/reference`, `/organisation`, `/admin/users`, and the relevant project/team/report options as the actual persona before creating data.
3. Create one active project with Civil discipline, PM Priya, lead Marc, reviewer-capable members Alex/Jill, and a Halifax office/home-discipline cohort. Use ordinary project/member/team commands only.
4. Board fixture: create five ordinary tasks through the task command with these distinct conditions: 12h total estimates, one genuinely overdue unfinished task, one current deliverable, one PM-owned task, one reviewer-0-day task, and one `In Review` task with a preserved historical `lastActivity`/review timestamp. Read the board/dashboard/task endpoints and reconcile cards to source rows; do not manufacture timestamps in SQL.
5. Team fixture: use ordinary membership, organisation/person, open-role, and availability/time-away commands to produce home grouping, office plus null-office, two separate open roles, separately filled people, and a scoped current-week cohort with free/away/outside-week states. Preserve actual IDs and dates.
6. Capacity fixture: normal commands only for Civil 40h standard capacity and 50h Confirmed+Expected demand in the displayed week(s), with no Possible demand counted. Read Team Capacity in Hours and FTE/month modes and retain the source response for chart/table reconciliation.

## Acceptance joins

### Board

At 1440×1000 and 375px, compare actual API cards and rendered cards: five cards, 12h total, exactly one overdue, current deliverable, PM attribution, reviewer with 0-day signal, and `In Review` timestamp retained. Verify card links preserve IDs, no hidden synthetic card is counted, and phone document containment holds.

### Team / home / roles / current week

Browse as the authorised Team persona. Verify home-discipline and office grouping, a distinct null/Not set group, same-name IDs not merged, exactly two separate open roles, and Fill controls only for authorised/scoped roles. Verify filled people are separate from roles. On the current-week scope, reconcile actual free/away/outside-week values and source dates; unknown is not zero. Check project Team and workspace Team URL filters/group/columns and saved-view return if the final build exposes them.

### Team Capacity / C46 comparison

Run `team-capacity` with the actual supported parameters from the report form, first `unit=hours`, then `unit=fte&period=months`, retaining the exact `from`, `weeks`, `includeMyDrafts`, unit and period. Assert Civil capacity 40h, planning demand 50h, and positive weekly shortfall 10h in the API comparison rows and rendered chart/table. Confirm the same numeric row drives chart and table; FTE denominator uses standard 40; Possible is separately labelled and excluded. Check desktop first-row geometry, phone containment, period/page URL preservation, source `asOf`/coverage, export parameters, and S23 cohort/task return unchanged. Record null/provisional/partial states if the actual fixture produces them; do not force them.

## Time travel boundary

The six-day time-travel cases are already API-test scope. Real browser evidence must not claim time-travel or raw clock manipulation. The runtime check only verifies current-week behaviour from actual `/me.today` and ordinary dated commands.

The runnable harness creates the QA21 project, five tasks, two review transitions, two open roles, current-week planning entries, exact API joins, and settled Board/Team screenshots. Mutations fail immediately on any non-2xx response; missing joins are recorded as failures and the process exits nonzero.

## Evidence files to produce during execution

- `checkpoint21-real-api.json` — exact requests/statuses/response excerpts and fixture IDs.
- `checkpoint21-real-browser.json` — settled desktop/phone URL, viewport, source-response IDs, rendered counts/text, geometry and containment.
- `checkpoint21-real-board-*.png`, `checkpoint21-real-team-*.png`, `checkpoint21-real-capacity-*.png`.
- `checkpoint21-real-summary.json` — PASS/FAIL/BLOCKED/UNPROVEN per join, with untouched limits.

Existing checkpoint20 receipts/scripts are prior evidence only; do not copy their QA20 IDs into checkpoint21.
