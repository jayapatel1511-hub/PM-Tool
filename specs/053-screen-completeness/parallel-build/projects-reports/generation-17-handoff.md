# Generation17/18 C lane: six task-report inputs and report controls

ALL SOURCE READY / HELD. No C#/API-test/application TS/CSS/script mutation is in flight. No lane build, compiler, browser, behavioral test, generator, migration, helper, goal, commit, push, deployment or activation was run. Parent owns integration and runtime verdict. No S23/FR39/full packet acceptance claim.

## Maintained source

- Reports.cs and new ReportTaskInputs.cs: the six named task-row report sources now share one full IQueryable between existing Run, uncapped metric counts and the report-owned task-input pager. Enabled Run uses one captured time/read snapshot. Later input pages reauthorize and state their own fresh-read time. Export result formatting/caps remain existing. New DTO fields are null-omitted off and absent from all other reports. Root applied its separately reviewed scalar-GUID and int-offset boundary repairs after this lane's C# freeze; C made no backend edit during parent execution.
- Reports.tsx: taskInput/inputPage/inputPageSize are excluded from data/export capture and cleared by parameter/history changes. Optional task summary/dialog render only for actual returned TaskInputs under capacity_screens. Actual nonempty result tables have data-report-table=code for precise geometry checks. The compact native parameters/Views disclosure requires BOTH capacity_screens and visual_system per root's review. With visual_system=false, checkpoint17 expanded S23-A controls remain. ViewMenu stays mounted for saved required-project default bootstrap/correction.
- New report-task-inputs.tsx: canonical server summary links retain catalogue/view container state; two overlapping metrics stay distinct. Exact report-owned query key is ['report',code,canonicalParameters,'task-inputs',metric,page,size]. Unknown metrics do not fall back. Page/error/loading hides stale rows/counts. Read-only Task rows open the normal panel independently of screen_patterns. Existing recordContextReturn chain restores exact report/input URLs and main/dialog-body scroll; a stable source button ID/ref restores the actual clicked non-first Task row with preventScroll. Input close focuses its original metric. Deep-link fallback removes only input UI keys. Scope/live/asOf/basis remain visible; null complete totals stay unavailable with known subtotals.
- reports-catalogue.css: compact report-local disclosure, count-row space and phone layout; no shared CSS/tokens changed.

## Prepared verification

- New ReportTaskInputsTests.cs initially contained nine focused Fact cases; root's boundary additions/compiled checks now belong to parent evidence. Lane prepared normal assignment/reviewer/transitions/readiness/reasons, dependency creation and versioned estimate PATCH. Direct state fixtures are explicitly labelled legacy stored-JSON recovery, timestamp aging or >MaxRows scale cloning. The cases cover all six predicates/overlap/zero/mixed review task branch/full cohorts/paging/export guard/off parity/malformed references and all scalar types.
- New report-task-inputs.cjs uses synthetic nonempty data and the real six report column sets. It asserts exact scalar scope for all six reports, overlapping null estimates/undated counts, known0, pages1/2 and size1/50/200, reload/deep links, refused page without stale rows/counts, non-first Task source button focus plus main/dialog-body scroll, exact report/metric focus return, no extra Run for the explicitly scoped origin, save/Copy/export canonical data, invalid metric/unsupported report/flag-off absence, required-project default bootstrap with closed disclosure, counts/dialog independent of visual_system, and phone overflow.
- Compact standard desktop1440x1000 geometry measures only `table[data-report-table="<code>"] tbody tr:first-child` with nonempty visible rows and actual report columns; top must remain <=405px for initial and all five complex scoped cases. No catalogue/sidebar/search proxy or threshold relaxation. Expanded visual-system-off geometry is recorded separately as the deliberately preserved checkpoint17 presentation and is not claimed to meet the compact layout bound. No geometry/runtime result is claimed yet.

## Executed static checks

PASS: node --check web/tests/report-task-inputs.cjs.

PASS: tracked Reports.tsx git diff --check, and whitespace/final-newline inspection of Reports.tsx, report-task-inputs.tsx, reports-catalogue.css and the new script. These are static evidence only; no application typecheck/lint/build or browser execution by C.

## Root integration notes

- Locale JSON17 keys was merged by parent; generated new DTO/route contracts were produced by parent. No shared keys/locales/metadata/generated output edited by C.
- `requests/g17-report-task-inputs-integration.md` records the old S23-A parity additive-field check and amended visual-only disclosure scope. Existing report-catalogue-views.cjs runs visual_system=false and should retain its expanded-controls assertions; it does not need a disclosure workaround.
- `requests/g18-task-input-pager-offset-review.md` was reported without editing held backend; root reports independent repair/re-review.
- `requests/g18-default-param-link-normalization.md` records one static performance edge for root review: materializing previously implicit server defaults in a canonical input href can change the Run key and cause one ordinary report reread. No shared cache TTL/raw scope fallback was introduced. Script zero-extra-Run assertion is explicitly for concrete scoped parameters.
- Root suggested execution: the new script with its already-built UI at localhost8853, followed by any relevant unchanged report catalogue/views regression. Root alone chooses/runs final API/browser/typecheck gates and records final verification. S23 remains incomplete for Workload/Capacity/Time and other forms outside the named six.
