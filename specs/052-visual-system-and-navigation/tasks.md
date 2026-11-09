# Tasks: Visual System and Navigation

Each slice is one PR, merges dark behind `flag.visual_system` and adds the flag pair test (off: identical to before; on: present), covering AC-VSN-12 for its area. Every slice needs packet 039's flags slice (S09) first and Jay's approval of 052-D1. D2–D10 remain proposed until decided; the flag is not turned on in pilot until D1, D2 and D3 are decided.

- [ ] S01 Tokens, fonts and the switch (S).
  - §56 token set under `data-visual="v2"`; `--ink-3` `#646d81`; IBM Plex bundled and self-hosted; flag entry.
  - Covers FR-VSN-01, FR-VSN-02, FR-VSN-03, FR-VSN-23; AC-VSN-01 (fonts).
  - Tests: `web/tests/visual-system.cjs` (root attribute follows the flag; computed font families; no font request to another origin).
- [ ] S02 Band, buttons and project header (M). After S01.
  - Covers FR-VSN-10, FR-VSN-11, FR-VSN-12, FR-VSN-13; AC-VSN-05.
  - Tests: band order on every route; project header at most 72 px with a 120-character name; input boundary at 3:1.
- [ ] S03 Colour meanings, lanes, pills, hatch and shapes (M). After S01.
  - Covers FR-VSN-04, FR-VSN-05, FR-VSN-06, FR-VSN-08; AC-VSN-02, AC-VSN-03.
  - Tests: count-card colours by metric; lane colours and hatch in Tasks by Status; pill values.
- [ ] S04 Project identity colour (S, Tier B: migration). After S01.
  - Covers FR-VSN-07; AC-VSN-04.
  - Tests: `tests/Hub.Tests/Domain/IdentityColourTests.cs` (least used, tie order, backfill order); API test for the PM change and its activity entry; field absent with the flag off.
- [ ] S05 Title block and `asOf` (S). After S02.
  - Covers FR-VSN-14; AC-VSN-06.
  - Tests: title block reads the response time with a shifted browser clock; absent on registers; `asOf` absent with the flag off.
- [ ] S06 Count cards (S). After S03.
  - Covers FR-VSN-15; AC-VSN-07.
  - Tests: card is one link whose list count equals the figure; definition on focus; accessible name.
- [ ] S07 Sidebar and tab rows (M, Tier B: proposed navigation behaviour changes). After S02.
  - Covers FR-VSN-16, FR-VSN-17, FR-VSN-18; AC-VSN-08, AC-VSN-09.
  - Tests: sidebar per role (six roles of AC-CALM-04); every route highlights one item; workspace and project tab order; former project routes load.
- [ ] S08 Phone drawer (S). After S07.
  - Covers FR-VSN-19; AC-VSN-10.
  - Tests: drawer focus trap and return; bottom bar kept; no sideways scroll at 375 px.
- [ ] S09 Accessibility and censuses (S). After S03 to S08.
  - Token census and contrast census; §41 census run with the flag off and on.
  - Covers FR-VSN-09 (census), FR-VSN-20, FR-VSN-21, FR-VSN-22; AC-VSN-01 (colours), AC-VSN-11, AC-VSN-12.
  - Tests: `web/tests/clutter.cjs`, `web/tests/ui-review.cjs`, axe scan on the 17 screens of §41.1.
- [ ] S10 Flag cleanup (S). After Jay turns the flag on in pilot: remove the entry, the v1 token set and the More disclosure; keep the addition on (§43 FR-DLV-09).
