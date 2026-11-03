# Generation17 S32 — ALL SOURCE READY, C# held, runtime UNRUN

FR/AC-SCR54–55 source is fully authored and held. No owned edit or process in flight. Earlier capacity sources remain held; S29 deferred. Root owns shared integration, independent review, compilation/generation/runtime and acceptance ledgers.

## Owned files

- Time.cs (only partial declaration/new own-week route), new TimeWeekRead.cs and new CapacityTimeWeekTests.cs. Backend contract/eight dedicated normal-command Facts: `s32-contract.md` and `s32-backend-handoff.md`. All C# remained held throughout frontend authoring; no further backend change was needed for the UI review repair.
- Time.tsx: capacity_screens own Monday–Sunday query at exact root-requested `['time','week',filters,'capacity_screens']`; original review/another-person/custom-range list and dialogs retained. No new own-week read off or in legacy scopes. Own grid, sidebar sums and AsOf use the same week snapshot's real entries; blank suggestions never enter totals. Existing TimeEntry invalidation returns its promise so cells await refresh settlement; other existing callers may ignore it.
- New components/planner/TimeWeekGrid.tsx and scoped time-week.css: seven exact days; positive canonical own TimeAwayHours hatch with text, never infer absence/nonworking from0/weekend. Capacity32/known0/null and explicit provisional daily context. Fully-away days remain enterable. Complete authorized task union, actual totals, named keyboard scroll region,375px containment. Blank cells POST only dirty positive≤24/two-decimal hours. One-entry cells PATCH exact ID/version, body hours only so notes remain. Multi-entry cells show sum+selector and open existing correction/delete dialogs on one entry, never overwrite/merge aggregates.
- New capacity-time-week.cjs: synthetic rendered workflow cases described below. API Facts own real calculation/privacy/command evidence.

## Root review create/refetch race repair

Addressed the coordinator's P2 before source READY. `inFlight` blocks Enter+blur synchronously. After a known successful POST/PATCH, a separate acknowledged/saved state protects the cell until an authoritative entry/version-keyed component replaces it. The cell awaits the full invalidateChange promise and remains disabled even if refresh quietly resolves with stale cached data or rejects. It offers Reload and never re-enters the blank POST path; Reload only invalidates/refetches. Unknown create results remain separate: preserve exact body+Idempotency-Key, lock those inputs and retry that logical request. No backend/canonical command modification.

The browser script authors a deliberately held own-week response, attempts fast second keyboard entry while protected, then releases the authoritative read. It also returns503 for all own-week refetch attempts after a known successful create: saved cell remains protected, Reload eventually reveals the existing entry, no second POST. These are authored cases, not executed proof.

## Root seams

- `s32-server-text.json` has two errors; root reports already merged.
- `s32-ui-locales.json` now has **29** TimeWeek strings including `timeWeek.savedAwaiting`; merge its latest version. Existing app.retry/error.reload/time.editTitle/time.edit strings are reused verbatim.
- Root cache seam agreed exactly in `s32-contract.md`; root reports Time prefix invalidation authored for actual/candidate/capacity dependencies. No lane keys.ts edit.
- Root API/types/metadata/route-inventory generation required for GET /time/week and TimeWeek DTOs. No local shadow API contract/schema/permission/PersonCapacity/Availability/Planning change.

## Authored synthetic browser coverage

1. Off original daily list/no week request; enabled four deduplicated suggested tasks/28 blanks, Capacity32, Wednesday8h hatch and entry allowed; focusing blank writes nothing.
2. Tuesday2 Enter+blur makes one POST with stable key and actual2h; deliberately delayed authoritative read protects against fast second edit before remount. Single exact-ID/version PATCH2.25 sends no note field.
3.0/clear neither delete nor write;409 blocks until reload/versioned actual3 appears; unknown Wednesday create with lost response retries exact key/body and creates once.
4. Two distinct entries/notes on a cell show sum+chooser, correction targets only selected entry's ID/version/note and never sends aggregate hours.
5. Known successful create + failed503 refetch keeps disabled saved state; reload reveals the entry with no duplicate POST.
6. Provisional daily4h context gives unavailable week; real known0 does not hatch or infer absence/nonworking; ReadOnly grid has no inputs/Add time.
7. Exact task filters, Dec28→Jan4 year navigation, team/custom/other-person URL fallback with zero new week calls; named region focus and375px contained table; off restores original list and no new reads.

## Static evidence actually executed

- `node --check web/tests/capacity-time-week.cjs` — PASS (final held-read synchronization amendment checked again).
- Scoped `git diff --check` — PASS for tracked diff; new-file text separately inspected.
- TypeScript createSourceFile.parseDiagnostics on Time.tsx/TimeWeekGrid.tsx — PASS syntax only. This is not typechecking, compilation or runtime proof.

No lane tests, builds, browser, typecheck, generator, migration, helper, goal, commit, push, deploy or flag activation ran. Root should freeze/integrate then compile/run eight CapacityTimeWeekTests plus canonical TimeTests/availability guards proportionately, generate/typecheck/build, execute `TUESDAY_UI_BASE_URL=<root loopback preview> CHROME_EXECUTABLE_PATH=<root Chrome> node web/tests/capacity-time-week.cjs`, review no duplicate writes under delayed/failed refresh and measure final real geometry. Universal first-data geometry is not claimed here. Only root records functional PASS/FAIL and full slice/packet progress; no Accepted status changed.
