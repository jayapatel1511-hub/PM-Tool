# Plan: Visual System and Navigation

Implements §56 (FR-VSN-01 to FR-VSN-23) with the proposed defaults 052-D2 to 052-D10, subject to their Open decisions. Each slice in [tasks.md](tasks.md) is one PR that merges dark behind `flag.visual_system`; no slice may claim approval or change an older contract before its decision is recorded.

## Approach

Reuse first. The look changes through tokens. No new library, and one nullable column.

- **One switch.** `Shell.tsx` sets `data-visual="v2"` on the document root when the flag is on. `web/src/index.css` keeps today's tokens and adds the §56 set under `:root[data-visual="v2"]`, so screens that already use tokens change with no code edit. Literal colours found by the token census are moved to tokens as each slice touches their files (boy-scout rule).
- **Fonts.** IBM Plex Sans and Mono `woff2` files (400 and 500 weights, Latin subset) are bundled under `web/src/assets/fonts/` with their licence file and declared with `@font-face` in `index.css`. No CDN.
- **Band and title block.** The existing `Page` component (`web/src/components/hub/common.tsx`) gains a band mode (title, subtitle, actions, `stamp`, tabs). The title block is one small component that reads `asOf` from the page's query. `SummaryTile` becomes the count card of FR-VSN-15 in v2 mode, keeping its props.
- **Sidebar.** `web/src/app/nav.ts` gains a `foot` group and an order list for v2. `NAV` items, `show` rules, `also` highlights and `BUILT` stay, and `vacation` moves into Resources' `also` list. The More disclosure renders only in v1.
- **Tab rows.** `web/src/components/hub/workspace.tsx` renders the workspace row. The project row keeps E1's groups from `ProjectLayout.tsx`; Activity and Settings render as links in v2 (052-D4).
- **Project identity colour.** `Project.IdentityColour` (nullable text, one of five) in `src/Hub.Api/Data/Entities.cs`, set on create by a pure function `IdentityColour.Pick(counts)` in `src/Hub.Domain/`, backfilled by a one-time job when the flag first turns on, and returned only while the flag is on. `AccentDot` reads it and falls back to today's hash.
- **`asOf`.** Home, Dashboard, Portfolio, Workload, Planner, My Staff, Reports and Time reads add `asOf` (server time, `org_time_zone`) where they lack it, behind the flag.
- **Phone.** `Shell.tsx` adds the menu button and a drawer reusing the sidebar list; the bottom bar is untouched.

## File boundaries

| Slice | Files |
|---|---|
| S01 tokens, fonts, switch | `web/src/index.css`, `web/src/assets/fonts/` (new), `web/src/app/Shell.tsx`, `src/Hub.Domain/Flags.cs`, `web/tests/visual-system.cjs` (new) |
| S02 band, buttons, project header | `web/src/components/hub/common.tsx`, `web/src/components/ui/button.tsx`, `web/src/pages/projects/ProjectLayout.tsx` |
| S03 colour meanings, lanes, pills, hatch, shapes | `web/src/components/hub/pills.tsx`, `indicators.tsx`, `web/src/pages/Home.tsx`, `Gantt.tsx`, `Calendar.tsx`, `Planner.tsx`, `Workload.tsx` (colour classes only) |
| S04 project identity colour (Tier B: migration) | `src/Hub.Domain/IdentityColour.cs` (new), `src/Hub.Api/Data/Entities.cs`, `HubDb.cs`, one additive migration, `src/Hub.Api/Features/Projects.cs`, `web/src/components/hub/common.tsx` (`AccentDot`), `tests/Hub.Tests/Domain/IdentityColourTests.cs` (new) |
| S05 title block and `asOf` | `common.tsx`, `src/Hub.Api/Features/Workspace.cs` (`/home`), `Dashboard.cs`, `Portfolio.cs`, `Workload.cs`, `Planning.cs`, `MyWork.cs` (My Staff read), `Reports.cs`, `Time.cs` |
| S06 count cards | `common.tsx` (`SummaryTile`), `Home.tsx`, `web/src/pages/projects/Dashboard.tsx`, `Portfolio.tsx`, `Staff.tsx` |
| S07 sidebar and tab rows | `web/src/app/nav.ts`, `Shell.tsx`, `web/src/components/hub/workspace.tsx`, `ProjectLayout.tsx`, `web/src/i18n/en.ts` |
| S08 phone drawer | `Shell.tsx`, `web/tests/visual-system.cjs` |
| S09 accessibility and census | `web/tests/clutter.cjs` (token census beside today's colour count), `web/tests/ui-review.cjs` (contrast census), `web/tests/visual-system.cjs` |
| S10 flag cleanup | removes `flag.visual_system`, the v1 token set and the More disclosure once Jay has turned the flag on in pilot |

Server files are named by feature; a slice that finds a read under another file name records it in its PR.

## Risks

- **Literal colours.** Components that bypass tokens will not change with the switch. S09's token census lists them; each is fixed in the slice that owns the file.
- **The §41 census.** With the flag on and 052-D2 pending, AC-CALM-05's six-fill limit fails by design. The census runs with the flag off for §41 and with it on for §56 until D2 is decided.
- **Font weight on slow links.** Two families at two weights are about 160 KB as `woff2`. They load with `font-display: swap`, and the asset caching of §46 covers them.
