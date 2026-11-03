## 56. Visual System and Navigation Amendment

Proposed scope: drafted on 2026-10-31 from the visual direction discussed and recorded in the research session while reviewing a 33-screen static prototype of the whole application. This section states that direction as requirements a developer can build without the prototype open. Packet 052 delivers it. No approval is claimed here; the scope awaits Jay's approval (052-D1).

The prototype is the visual reference: `.review/2026-10-31-unslop-home/proto/` (untracked). Its `kit/kit.css` holds every token, `home.html` is the reference screen and `index.html` lists every screen. It is not a design specification, a source of scope or application code. Where it and this section differ, this section governs; where its wording and the application's labels (`web/src/i18n/en.ts`) differ, the labels govern (052-D8). Until 052-D7 is decided, `docs/reference/coordination-hub-v2/` stays the repository's only visual-cue prototype for other work.

The amendment is additive. It changes how screens look and where navigation items sit, not what any screen contains: every route, tab, count, column, filter, action, permission, status name, role name and label stays. Data, rules, permissions and the API are unchanged, except a stored project identity colour (FR-VSN-07) and an `asOf` time on responses that lack one (FR-VSN-14), both released only with the flag. No interface library is added, and the icon family stays the application's own.

Where this section adds to §9.4 (navigation), §13.0 (layout, density, typography, status colour language, responsive and accessibility conventions) and §36.1 (FR-VIS-01, the shell), this section governs. It would also change two §41 rules: R8 (colour budget) and R4 with decision E2 (sidebar with a More group). Those two changes are proposed through decisions 052-D2 and 052-D3, stated precisely in FR-VSN-09 and FR-VSN-16, and take effect only when Jay decides them. Until then §41 stands as written, and the flag of §56.11 is not turned on in pilot.

### 56.1 Scope and boundary

- Tokens, type and spacing: §56.2.
- Colour with one meaning: §56.3.
- Shell, top bar and band: §56.4.
- The title block: §56.5.
- Count cards: §56.6.
- The one-list sidebar: §56.7.
- Workspace and project tab rows: §56.8.
- Phone behaviour: §56.9.
- Accessibility: §56.10.

§56.11 sets how it ships. Out of scope: the "calm" variant and the grid texture, both rejected by Jay; a dark theme; per-user or per-project themes; gradients between hues, glows, coloured shadows, emoji, uppercase letter-spaced labels, italic text and looping animation; any change to a label, status name or role name; and the screen content of §57.

The following conflicts and approval gates are explicit; none is silently resolved by this amendment:

| ID | Existing contract or uncertainty | Proposed treatment | Gate |
|---|---|---|---|
| 052-D1 | §56 is a new amendment, not an approved implementation scope | Approve §56 with its listed defaults | Open; no approval claimed |
| 052-D2 | §41 R8 and AC-CALM-05 cap chromatic fills at six, while the fixed-meaning palette measures about fourteen on the reference Home | Replace only the colour portions as FR-VSN-09 states; keep the chrome-word measure | Open; §41 remains authoritative until decided |
| 052-D3 | §41 R4/E2 uses a More group, while the agreed direction uses one eight-item list and a foot group | Replace only the sidebar wording as FR-VSN-16 states | Open; E2 and §41 remain authoritative until decided |
| 052-D4 | §41 E1 puts Activity and Settings under More | Use two right-aligned links, or retain More if Jay declines | Open; no route is removed either way |
| 052-D5 | Current project accents are derived; the proposed identity colour is stored and editable | Store one of five identity colours, distinct from every status and metric hue | Open; no migration or API field is implemented by this spec |
| 052-D6 | The current build may not bundle the proposed font family | Bundle IBM Plex Sans and Mono from the application origin, subject to the served-file/font licence check | Open |
| 052-D7 | `CLAUDE.md` names `docs/reference/coordination-hub-v2/` as the visual-cue prototype | Keep the 33-screen prototype outside `docs/reference/`; this amendment remains buildable without it | Open |
| 052-D8 | Prototype wording can differ from application labels | Canonical labels in `web/src/i18n/en.ts` win; no label or status rename is implied | Open |
| 052-D9 | One flag versus several flags affects release control | Use one `flag.visual_system` for this amendment | Open |
| 052-D10 | §13.0 already defines a phone bottom bar | Keep it and add the drawer/menu behavior | Open |

Until a gate is decided, the older contract wins for that conflict and the proposed behavior remains dark behind the flag. The proposed defaults are design inputs, not approvals.

### 56.2 Tokens, type and spacing

- **FR-VSN-01.** Colours, type and spacing MUST be defined once, as named tokens in one stylesheet, and every screen MUST use the tokens instead of literal values. Names and values are those of the prototype's `kit/kit.css` and the tables in §56.2 and §56.3, with one correction: muted text (`--ink-3`) is `#646d81`, because the prototype's `#687185` falls below 4.5:1 on the page, sidebar and raised surfaces.

  | Token | Value | Use |
  |---|---|---|
  | `--page` | `#f4f5f8` | Page background below the band |
  | `--side` | `#f1f2f5` | Sidebar |
  | `--card` | `#ffffff` | Cards, tables, panels, count cards |
  | `--raised` | `#f3f4f7` | Group rows, tracks, quiet fills |
  | `--ink` | `#161a23` | Text and figures |
  | `--ink-2` | `#465063` | Secondary text |
  | `--ink-3` | `#646d81` | Muted text, column headers, item keys |
  | `--line`, `--line-2`, `--line-3` | `--ink` at 9 %, 6 % and 16 % | Decorative hairlines, card outlines, dividers |
  | `--input` | `#7c8491` | The boundary of an input or control (3:1), kept from today's application |
  | `--navy` | `#1b2a4e` | Top bar, band, primary buttons |
  | `--brand` | `#385cac` | Links, focus ring, selection, today |
  | `--brand-tint` | `#eaf0fd` | Selected sidebar item, today's column |

- **FR-VSN-02.** The interface MUST use IBM Plex Sans, and IBM Plex Mono for item keys, dates, hours and figures in tables. Both are bundled with the build under their SIL Open Font License and served from the application's own origin, with today's system stacks as fallbacks; no font is requested from another host (052-D6). Weights are 400 and 500, with 600 only for small emphasis such as avatar initials. Figures use tabular numerals.

  | Text | Size / line height |
  |---|---|
  | Page title (one `h1`, in the band) | 28/36 px, weight 500; 24/32 px below 768 px |
  | Card heading | 15/24 px, weight 500 |
  | Body and controls | 14/20 px |
  | Table text | 13 px; row height follows §13.0's density setting, unchanged |
  | Labels, legends, meta lines | 12/16 px |
  | Count-card figure | 32/40 px, weight 500; 28/36 px below 768 px |
  | Smallest text anywhere | 11 px |

  Dates in data are ISO (`2026-11-02`) in Mono, with a relative helper beside them where it helps ("In 4 days", "Mon 2 Nov"), as §13.0's time display already allows. Widget and column names stay in the Title Case the application uses; labels and buttons carry no trailing period and no exclamation mark; titles carry no greeting.

- **FR-VSN-03.** Spacing MUST follow a 4 px grid: 20 to 24 px inside cards (16 px below 768 px), 24 px between cards, 16 px between count cards. Cards are white with a 14 px radius, a 1 px outline and a 1 to 2 px shadow; count cards use a deeper shadow; controls use an 8 px radius. Equal things get equal sizes. Motion is limited to hover and press transitions of 120 to 150 ms on named properties (never `all`) and one entrance on page load, in which content rises 8 px over 400 ms, lightly staggered. Nothing loops. `prefers-reduced-motion` removes both.

### 56.3 Colour with one meaning

- **FR-VSN-04.** Each semantic colour token below MUST keep its documented meaning within its visual domain and MUST NOT be repurposed within that domain. The metric/project domain and the task-lane/status domain are separate, so a hue may be reused only where the tables explicitly assign it (for example, Blue for project metrics and Done lanes). A figure, chart segment, icon chip or bar takes the colour of what it counts. Project identity colours are a separate domain and never encode status, health or a metric. Each hue has a strong value for marks, icons and large figures and a tint for chip and card backgrounds.

  | Colour | Strong | Tint | Means | Examples |
  |---|---|---|---|---|
  | Blue | `#2f6fe0` | `#e8f0fe` | Projects (links use the brand blue `#385cac`) | Active Projects card, project counts |
  | Violet | `#6a52d1` | `#efebfd` | Open work, In Progress | Open Tasks card |
  | Red | `#d6334a` | `#fde9ec` | Overdue, blocked, critical | Overdue Tasks card, the runway's "Before today" cell, hours beyond 100 % |
  | Amber | `#c77d0a` | `#fdf1dc` | Due soon, review, at risk | Due Within n Days card |
  | Green | `#18915f` | `#e3f5ec` | On track, done well | On Track card, a good trend |
  | Teal | `#0f8a96` | `#def3f4` | People, capacity, hours | Team Members card, capacity bars, free hours |

- **FR-VSN-05.** Wherever task status is drawn as a bar, donut, column header or card edge, the four §36.3 buckets and On Hold MUST use the lane colours at full strength: To Do `#94a0b4`, In Progress `#6a52d1`, Review and revision `#f0a42b`, Done `#2f6fe0`, On Hold the hatch (FR-VSN-06). Status pills keep §13.0's colour language, symbol and text, with these values, and never take a lane or metric colour:

  | §13.0 meaning | Symbol | Text on background |
  |---|---|---|
  | On Track / Normal | ● | `#386953` on `#eaf5ef` |
  | Attention / Warning | ▲ | `#7a5817` on `#fff4db` |
  | Critical / Blocked / Overdue | ■ | `#b42332` on `#fff0f1` |
  | Not Started / Inactive / Not Evaluated | ○ | `#59606c` on `#f1f2f4` |
  | Complete / Issued | ✓ | `#37589a` on `#edf2fd` |
  | In Progress / In Review | ◐ | `#574e87` on `#f0effa` |

- **FR-VSN-06.** The hatch (135° stripes, 3 px `#94a0b4` and 3 px `#d5dae3`) MUST mean time away and on-hold work and nothing else: time off in capacity bars and planner cells, On Hold in status charts, days away on the Time grid. A faint variant (stripes of `--ink` at 3.5 %) marks non-working days, such as weekend columns, and nothing else. Every use of either hatch is named in its legend or label.

- **FR-VSN-07.** Each project MUST have one identity colour from five, kept clear of the lane hues: coral `#ec6a3e`, mint `#20a36d`, rose `#d9467b`, cyan `#1597cc` and lime `#84a32a`. It appears as a 10 px swatch, or as the fill of the project's item marks on timelines, calendars and the deadline runway, always beside the project number, so it is never read as a status. The colour is stored on the project only when `flag.visual_system` is on. While the flag is off, create and update paths do not set or change the field; stored colours survive flag rollback, and existing derived accents remain the displayed fallback. When the flag first turns on, existing non-deleted projects of every lifecycle are backfilled in project-number order using the least-used colour among Setup and Active projects, ties broken in the listed order; a new project then gets the least-used colour by the same rule. The Project Manager may change it only where existing project edit permissions allow, with optimistic concurrency, and the change writes one activity entry. Colours are not unique and never encode health, lifecycle status or any metric (052-D5).

- **FR-VSN-08.** Item types MUST keep one 10 px shape wherever they are drawn: a task is a circle, a deliverable a square with 2 px corners and a milestone a diamond. The shape is filled with the project colour on timelines, calendars and the runway, otherwise with `--ink-2`. Every legend that shows the shapes names them.

- **FR-VSN-09.** Chromatic fills, borders and text colours MUST come only from the §56.2 and §56.3 tokens. When Jay confirms 052-D2, this replaces the colour budget of §41 R8 and the colour half of AC-CALM-05, as follows:
  - R8 would read: "**Colour with fixed meaning**: every chromatic fill is a §56 token used only for its meaning (§56.3); brand navy carries the band and primary buttons; colour is never the only signal (§13.0)." The limit of six distinct fill colours above the fold is removed. The research measured about fourteen on the reference Home screen.
  - The colour half of AC-CALM-05 would count chromatic fills above the fold that are not §56 tokens (target 0) and record, with no limit, the number of distinct token fills on each of the 17 screens of §41.1, so a later decision can set a ceiling from measurements. The chrome-word half of AC-CALM-05 is unchanged.

  Until 052-D2 is decided, R8 and AC-CALM-05 stand as written.

### 56.4 Shell, top bar and band

- **FR-VSN-10.** The desktop shell MUST be a light sidebar (`--side`, 240 px, collapsible to today's 76 px icon rail) beside a navy top bar and a navy band that read as one block. The 56 px top bar stays in place while the page scrolls and holds, in this order: breadcrumbs, search with its `/` hint, the workspace scope selector (FR-VIS-02), New (white on navy; FR-VIS-01's permission-filtered quick-create), the notification bell with its unread count, and the user menu. Each control keeps today's behaviour.

- **FR-VSN-11.** Every screen MUST open with the navy band: one `h1` page title, at most one plain subtitle line (§41 R5), actions on the right as quiet buttons, the title block where §56.5 applies, and then the screen's tab row (§56.8). Lists and registers use the plain band and start their content on the light page below it. On Home, the project Dashboard, Portfolio and My Staff, whose first row is count cards, the cards overlap the band's bottom edge by 72 px (64 px below 768 px). The band carries no texture or image.

- **FR-VSN-12.** The project header in the band MUST follow §41 R6: the project number in Mono, the name on one line, truncated with the full name on hover and focus, the status pill and the health pill; then one meta line with Project Manager, Client and Target date. It is at most 72 px high. The prototype's two-line name is not adopted.

- **FR-VSN-13.** Primary actions on light surfaces MUST be navy-filled buttons with white text, replacing today's black; on navy, New is white-filled and other actions are quiet buttons. Secondary buttons are white with a `--input` outline. Every input and control whose boundary identifies it keeps a 3:1 boundary (`--input`), never a decorative hairline. A segmented control (view switch, role preset) shows the selected option as a white segment marked with `aria-pressed` or `aria-current`. The focus ring is 2 px `--brand`, or `#9bb8ff` on navy, offset 2 px.

### 56.5 Title block

- **FR-VSN-14.** A screen that shows computed counts or totals MUST show a **title block** in its band: a small two-column box whose first row is "Current values" with the date and time the server computed the figures, followed by one row for each other reference time the screen uses (for example "Trends compare with" and the snapshot time, §57 FR-SCR-10). This applies to Home, the project Dashboard, Portfolio, Workload, the Weekly Planner, My Staff, an open report and Time's totals. Each time is ISO date and 24-hour time in `org_time_zone`, in Mono, taken from the response's `asOf`, never from the browser clock; a response that lacks `asOf` today adds it behind the flag. Screens without computed figures (registers, panels, forms) show no title block. The block is how a screen states that snapshot totals are current values (FR-VIS-07).

### 56.6 Count cards

- **FR-VSN-15.** A count card MUST show, top to bottom:
  1. an icon chip in its metric colour (§56.3) and, where the screen has a comparison, a trend chip (§57 FR-SCR-10);
  2. its label, underlined with dots, whose definition appears on hover and on focus;
  3. the figure (32/40 px, tabular numerals);
  4. a "where" line naming where the count sits (§57 FR-SCR-08) or its numerator and denominator;
  5. an optional meta line.

  Except for the On Track ratio, the whole card is one link to the filtered list that reproduces the count (§49.3, FR-RCV-07). On Track keeps separate links to Green projects and all evaluated projects, preserving its numerator/denominator drilldowns (FR-SCR-08; Jay confirmed 052-D11 on 2026-11-01). It is a labelled ratio card, not one list count, and MUST NOT nest links. Its accessible name reads label, figure and where line. The figure of a red metric is red only when it is above zero, and then the meta line carries "Needs attention" with a square symbol, so the alert is never colour alone. Cards in a row are equal in size: up to six per row on desktop, three below 1180 px and two below 768 px.

### 56.7 Sidebar: one list

- **FR-VSN-16.** The sidebar MUST be one list of eight items in this order: My Work, Home, Projects, Tasks, Resources, Time, Reports, Team. After a gap, a foot group holds the rare destinations: Files, My Staff, Templates, Admin. There is no More group (052-D3).
  - Every item follows role permissions as today (FR-VIS-01): Resources needs Workload access, My Staff the Supervisor capability, Templates and Admin their roles. A hidden item leaves no gap.
  - Notifications stays only in the top bar's bell (E2).
  - Views of another page are tabs on their parent and highlight its item. Board, Timeline and Calendar are workspace tabs that highlight Tasks; the workspace Dashboard tab is Home. Portfolio is a Projects tab. Workload and Vacation & Coverage are Resources tabs. For a permitted user without Workload access, Resources stays hidden and Vacation & Coverage highlights Time (Jay’s 2026-10-31 clarification).
  - Every content route still loads for a permitted user and highlights exactly one sidebar item. The utility pages `/search`, `/notifications` and `/preferences` intentionally select no main or foot sidebar item (Jay confirmed 052-D12 on 2026-11-01); their existing top-bar/account/phone destinations remain. AC-CALM-06 carries this explicit exception. The decided Vacation & Coverage fallback above is unchanged.

  When Jay confirms 052-D3, decision E2, §41 R4, the sidebar half of AC-CALM-04 and FR-VIS-01 would change as follows. E2 is replaced by this list. R4 and AC-CALM-04 would read "at most 8 items in the main list and at most 4 in the foot group, for any role, with no More group". In FR-VIS-01, "More holds permitted lower-frequency routes" would read "the foot group holds permitted lower-frequency routes". A System Administrator who is also a Supervisor then sees 12 sidebar rows, where E2 shows six primary items and a collapsed More. Until 052-D3 is decided, E2, R4, AC-CALM-04 and FR-VIS-01 stand as written.

### 56.8 Tab rows

- **FR-VSN-17.** Workspace views MUST share one tab row in the band, in this order: Dashboard, Board, List, Timeline, Calendar, Files, Team, Workload, then the plus control that opens the view switcher (FR-VIS-01), and Copy link at the right end. Each tab keeps the selected scope (FR-VIS-02). The current tab carries `aria-current="page"` and a lighter fill; icons are 16 px, in the text's colour. Tabs a role cannot open are hidden. Projects (list, board, Portfolio) and Resources (Weekly Planner, Workload, Vacation & Coverage) show their own tab rows in the same style.

- **FR-VSN-18.** Project pages MUST show the six §41 E1 groups as their tab row: Overview, Work, Deliverables, Coordination, People and Files, each with its pages unchanged (People keeps Team & Disciplines, Allocations and Workload). The active group, when it has more than one page, shows a second-level switch below the row (§41 R3). Activity and Settings sit at the right end of the row as two quiet links instead of under a More menu (052-D4). If 052-D4 keeps E1's More, they stay under More. Every existing project route and deep link keeps working and highlights its group.

### 56.9 Phone

- **FR-VSN-19.** Below 768 px:
  - the sidebar is hidden, and a menu button at the left of the top bar opens the same list, foot group included, as a drawer that keeps focus inside until it closes and returns focus to the button;
  - the §13.0 bottom bar (My Work, Search, Notifications) stays (052-D10);
  - the top bar keeps the menu button, search as an icon, the scope selector, New, the bell and the user menu, and drops the breadcrumbs;
  - the band keeps its order with 16 px side gutters, the title block takes the full width and tab rows scroll sideways inside the band;
  - count cards sit two per row, grids collapse to one column, tables either scroll inside their card or drop the columns marked secondary, and side panels take the full width;
  - no screen scrolls sideways at 375 px wide.

  §13.0's phone action scope is unchanged: screens that are desktop and tablet only keep their notice.

### 56.10 Accessibility

- **FR-VSN-20.** Status MUST never be shown by colour alone. Pills carry text and symbol (§13.0). Count cards name their metric. Chart segments, runway marks and capacity bars are links or images whose accessible names state the label and value ("250101, In Progress: 3"), and every chart has its values as visible text beside it, as legend totals or a list. Both hatches are named in their legend. Health pills carry their word.

- **FR-VSN-21.** Text under 24 px regular or 18.66 px bold MUST meet 4.5:1 against its actual background, text on navy included. Large text and non-text marks that carry meaning without a text equivalent MUST meet 3:1. The WCAG 2.1 relative-luminance calculation MUST use the rendered foreground and background, not a token's contrast against white by assumption. For the strong metric hues on white, the measured ratios are Blue 4.70:1, Violet 5.61:1, Red 4.74:1, Amber 3.30:1, Green 4.00:1 and Teal 4.12:1. Therefore Blue, Violet and Red MAY be normal-size metric text on white; Amber, Green and Teal are restricted to figures at least 24 px, icons and non-text marks unless the actual pairing passes 4.5:1. Smaller coloured text uses the status-pill tones only, each measured between 5.66:1 and 6.48:1 on its stated tint.
  - Muted text uses `#646d81`.
  - The To Do and Review lane colours (below 3:1 on white) appear only where the value is also given as text (FR-VSN-20).
  - Focus is always visible: `#9bb8ff` on navy is 7.2:1.

- **FR-VSN-22.** No text MAY be smaller than 11 px. At 200 % browser zoom and with WCAG 2.1 text-spacing overrides, no content or control is hidden. Controls are real `button` and `a` elements; icon-only controls have accessible names; decoration is hidden from assistive technology; keyboard order follows visual order. An automated accessibility scan reports no serious or critical issue on any screen (as AC-CALM-08).

### 56.11 Shipping dark

- **FR-VSN-23.** The amendment MUST merge dark behind `flag.visual_system` under §43's flag rules (FR-DLV-07 to FR-DLV-10). The flag sets one attribute on the document root that selects the §56 token set and the §56.7 sidebar; both token sets live in the one stylesheet until the flag's cleanup removes the old one. With the flag off, every screen looks and navigates as before this amendment, responses carry neither the identity colour nor any new `asOf` field, and existing browser suites pass unchanged. The flag is turned on in pilot only after 052-D2 and 052-D3 are decided.

### 56.12 Acceptance criteria

- **AC-VSN-01.** Given the flag is on, when Home, project Tasks, Workload and the Weekly Planner render at 1440×900, then a token census reports no chromatic fill, border or text colour outside the §56 tokens, body text is IBM Plex Sans, item keys and table dates are IBM Plex Mono, and no font request leaves the application's origin.

- **AC-VSN-02.** Given Home with one overdue task, then the Overdue Tasks card's chip and figure are red and its meta line reads "Needs attention" with a square symbol; with no overdue task, the figure is ink and the flag is absent. The other five cards use the colours of FR-VSN-04: Active Projects blue, Open Tasks violet, Due Within n Days amber, On Track green, Team Members teal.

- **AC-VSN-03.** Given a project with tasks in every bucket and one On Hold, then Tasks by Status draws To Do grey, In Progress violet, Review and revision amber, Done blue and On Hold hatched, each named in the legend with its count; and a planner cell with time away shows the hatch labelled "Time off".

- **AC-VSN-04.** Given five Active projects coloured coral, mint, rose, cyan and lime, when a sixth project is created, then it is coral (the first of the least used); when its PM changes it to cyan, one activity entry records the change, and the swatch beside its number on Home, the Timeline and the Calendar is cyan.

- **AC-VSN-05.** Given project 250101 whose name is 120 characters long, then the band header shows the number, the name on one truncated line with the full name on focus, the status and health pills and the meta line, within 72 px.

- **AC-VSN-06.** Given Home whose response says `asOf` 2026-10-31T14:24:05 in `org_time_zone`, then the title block reads "Current values 2026-10-31 14:24" even when the browser clock differs; the Risks register shows no title block.

- **AC-VSN-07.** Given the Overdue Tasks card has keyboard focus, then its definition is shown, its accessible name reads its label, figure and where line, and Enter opens the task list whose row count equals the figure.

- **AC-VSN-08.** Given a System Administrator who is also a Supervisor, then the sidebar lists My Work, Home, Projects, Tasks, Resources, Time, Reports and Team, then Files, My Staff, Templates and Admin in the foot group, with no More group; a Read Only user without Workload access sees seven items and Files. Opening `/boards`, `/portfolio`, `/workload` and `/vacation` highlights Tasks, Projects, Resources and Resources. For a permitted viewer without Workload access, Resources stays hidden and `/vacation` highlights Time. Search, Notifications and Preferences select no sidebar item (052-D12).

- **AC-VSN-09.** Given Home, then the workspace tab row lists Dashboard, Board, List, Timeline, Calendar, Files, Team and Workload in order, the plus control, and Copy link at the right; on a project's Reviews page the Deliverables group is current with its second-level switch, Activity and Settings sit at the right end of the row, and every former project route loads and highlights its group.

- **AC-VSN-10.** Given a 375×812 viewport, then the menu button opens the drawer with focus inside and Escape closes it and returns focus to the button, the bottom bar still shows My Work, Search and Notifications, Home shows two count cards per row, and no screen scrolls sideways.

- **AC-VSN-11.** Given the flag is on, then the automated scan reports no serious or critical issue on the 17 screens of §41.1, a contrast census finds no text under 24 px below 4.5:1, and each Tasks by Project segment's accessible name states its project, bucket and count.

- **AC-VSN-12.** Given `flag.visual_system` is off, then existing browser suites and the §41 census pass unchanged, the sidebar follows E2, and no response carries the identity colour or a new `asOf` field.

### 56.13 Decisions

D1 to D10 below retain their proposed defaults and Open status; no approval of those rows is implied. D11 and D12 record Jay’s explicit 2026-11-01 choices. All rows are mirrored in `specs/DECISIONS.md` as 052-D1 to 052-D12.

| # | Decision | Default used |
|---|---|---|
| D1 | Approve this amendment's scope | Approve with the listed defaults |
| D2 | Colour budget: replace §41 R8's six-fill limit and the colour half of AC-CALM-05? | Yes, as FR-VSN-09 states: every chromatic fill a §56 token with one meaning; the census counts non-token fills (target 0) and records token fills with no limit |
| D3 | Sidebar: replace decision E2's More group, §41 R4 and the sidebar half of AC-CALM-04 with one list of eight and a foot group of up to four? | Yes, as FR-VSN-16 states; Vacation & Coverage becomes a Resources tab; FR-VIS-01's "More" becomes the foot group |
| D4 | Project tab row: Activity and Settings as two right-aligned links instead of E1's More menu? | Yes; six group tabs and two links; AC-CALM-04's project half reads "at most 7 group tabs plus at most 2 page links" |
| D5 | Project identity colours: stored per project, or derived from a hash of the project number as today? | Stored; least used of five among Setup and Active projects at creation; PM may change; never status |
| D6 | Font delivery | IBM Plex Sans and Mono bundled with the build and served from the application's origin; no third-party font host |
| D7 | Add the 2026-10-31 prototype (21 MB with screenshots) to `docs/reference/`? | No. §56 is written to be built without it; it stays outside `docs/reference/`, and `coordination-hub-v2` stays the only visual-cue prototype CLAUDE.md names |
| D8 | Prototype wording that differs from the application's labels (for example its spreadsheet export label) | The application's labels in `web/src/i18n/en.ts` win; prototype wording is not adopted |
| D9 | How many flags? | One, `flag.visual_system`, covering tokens, type, band, title block, count cards, sidebar and tab rows |
| D10 | Phone navigation | Keep today's bottom bar and add the menu drawer of FR-VSN-19 |

| D11 | Home On Track ratio versus the one-link count-card rule | Confirmed by Jay, 2026-11-01: keep separate Green and all-evaluated links; the ratio is the explicit FR-VSN-15 exception, with no nested links |
| D12 | Selected sidebar item on Search, Notifications and Preferences | Confirmed by Jay, 2026-11-01: no main or foot sidebar item selected on these utility pages; Vacation & Coverage still highlights Time when Resources is hidden |
