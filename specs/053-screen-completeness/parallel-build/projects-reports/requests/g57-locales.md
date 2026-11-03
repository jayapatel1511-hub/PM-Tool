# G57 C exact shared seams and English copy

New public static mounts: `ReviewDisciplineSummaryEndpoints.Map(RouteGroupBuilder api)`, `ReviewDisciplineDefaults.MapAdmin(RouteGroupBuilder admin)` and `ReviewDisciplineDefaults.MapProject(RouteGroupBuilder api)`. Root mounts only; no C edits to shared files. Every new route is register_screens-owned. Summary rejects all query parameters; suggestions accept sourceRevisionIds only by documented source parsing. Current Approved package with an invalid/missing current-round relationship is graph-integrity409; an existing current round with missing StartedAt is an unavailable eligible sample.

Top-level Hub.Api.Features generated DTOs: ReviewDisciplineSummary, ReviewDisciplineSummaryRow (revised eligible/nonapplicable and assignment-round denominators); ReviewDefaultSettings, ReviewDefaultSettingsWrite (nullable DisciplineIds/RowVersion for canonical Admin sweep and manual validation), ReviewDefaultSuggestions, ReviewDefaultSourceType, ReviewDefaultSuggestion. Their source definitions are in the two new feature files. Exact API paths match g57-reviews-freeze.md, using `{projectId}` on both project routes and `{id}` on Admin child routes.

Root cache prefixes: `review-discipline-summary`, `review-default-suggestions`, and Admin `['admin','deliverable-review-defaults']`. Suggestions include project/viewer/register flag/sorted selected source IDs; summary project/viewer/register flag. Parent type/default/ref changes invalidate admin defaults and suggestions; all review-approval-affecting changes invalidate summary. Existing global source/discipline/user/member/setting invalidation must cover these, per plan.

Server Text keys:

| Key | English |
|---|---|
| reviewDiscipline.integrity | Review discipline history is inconsistent. Correct it before reading this summary. |
| reviewDefaults.required | Supply up to 50 distinct review disciplines; an empty list clears the defaults. |
| reviewDefaults.inactive | New defaults require an active type and active disciplines. Existing retired defaults may be retained or removed. |
| reviewDefaults.integrity | Review defaults contain inconsistent references. Correct them before applying suggestions. |

SPA en.ts keys:

| Key | English |
|---|---|
| reviewDiscipline.title | Review rounds by discipline |
| reviewDiscipline.scope | Currently approved packages in this project; independent of list filters. |
| reviewDiscipline.timing | Median days to discipline decision |
| reviewDiscipline.timingHint | Elapsed days from round start to the current stored latest terminal discipline decision. This does not measure whole-round completion or the first decision. |
| reviewDiscipline.roundMedian | Median rounds to approval |
| reviewDiscipline.roundMax | Maximum rounds to approval |
| reviewDiscipline.packages | Recorded / eligible packages |
| reviewDiscipline.nonApplicable | Not applicable |
| reviewDiscipline.timed | Timed / started assignment-rounds |
| reviewDiscipline.partial | Some eligible samples have unavailable timestamps. |
| reviewDiscipline.empty | No project disciplines to summarize. |
| reviewDiscipline.noSamples | No recorded eligible samples |
| reviewDiscipline.retired | Retired |
| reviewDefaults.title | Default review disciplines |
| reviewDefaults.edit | Review defaults |
| reviewDefaults.hint | Suggestions for new review packages. Coordinators may change them; reviewers are never chosen automatically. |
| reviewDefaults.chooseSources | Select deliverable revisions to see suggested disciplines. |
| reviewDefaults.loading | Loading review defaults… |
| reviewDefaults.empty | No default review disciplines are configured for these types. |
| reviewDefaults.add | Add suggested disciplines |
| reviewDefaults.manual | Your assignment choices are preserved. Suggestions will be added only when you choose. |
| reviewDefaults.tooMany | More than 50 disciplines are suggested. Choose a valid assignment set manually. |
| reviewDefaults.unavailable | Defaults could not be loaded. Retry or explicitly choose the required disciplines manually. |
| reviewDefaults.NotInProject | Not configured in this project |
| reviewDefaults.InactiveDiscipline | Organisation discipline is retired |
| reviewDefaults.InactiveProjectDiscipline | Project discipline is inactive |
| reviewDefaults.InactiveType | Deliverable type is retired |
| reviewDefaults.retired | Retired |
| reviewDefaults.uncertain | The save result is uncertain. Your original selection and version are preserved. Check the current saved defaults before starting another edit. |
| reviewDefaults.check | Check saved defaults |
| reviewDefaults.notMatched | The saved defaults do not match this selection. Your original intent is retained. |
| reviewDefaults.reconciled | Saved defaults match your selection. |

Existing discipline/common/settings labels are reused. No locale additions are written by C. UI/fixtures reference only actual generated schemas, pending root generation after the source freeze.
