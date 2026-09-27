# Verification: Portfolio Dashboard

Date: 2026-09-25.

## Automated checks

| Check | Result |
|---|---|
| `dotnet test tests/Hub.Tests` | Pass (241/241) |
| `npm run build` and `npx oxlint src` (web) | Pass, no lint errors |

`PortfolioTests` (3, all pass):

| Test | Covers |
|---|---|
| Tiles, rows, sorting and both health values | §13.12 tiles (3 active; Red, Yellow and Green 1 each; 1 submission in 14 days; 1 overdue decision; the On Hold project listed last with its status but not counted), worst first, reasons text, next submission; §16.4 computed Yellow beside reported Green with the PM's note, author and time; health and submission filters; Standard User refused; export |
| Trend shows each week's computed health in order | FR-HLT-03 / §16.5: eight weekly points from snapshots, the last snapshot of each week, ending today; Health History lists every snapshot in its window |
| Who sees which projects and the at-risk report | FR-003: a PM sees their own projects by default and others with "mine=false"; a PM who is also a Supervisor sees all by default; §19 Projects At Risk lists computed or reported Red/Yellow with reasons and omits Green; hidden from and refused for Standard Users |

`ExtrasTests.Several_items_to_a_newcomer_in_one_save_add_them_once` guards the defect below.

## Manual checks (browser pane)

| Check | Result |
|---|---|
| Portfolio as Lena (Executive): six tiles, filter bar, table with health and "why" | Pass |

## Defects found and fixed while verifying

- Creating a project where the creating PM is also a Discipline Lead failed with "That value already exists": the
  team service looked only in the database for members and follows, not at people added earlier in the same save,
  so the PM was added twice. The same flaw would have hit reassigning several items to a newcomer in one call.
  `TeamService.Active` and `Follow` now check the unit of work first.

## Decisions

- Colour tiles count computed health; reported health is shown per row beside it when different.

## Deferred (cross-packet rule)

- High issues read 0 until packet 014's issue register exists.
