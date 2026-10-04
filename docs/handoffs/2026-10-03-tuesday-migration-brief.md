# Tuesday visual overhaul — migration brief for screen owners

You are one of several engineers migrating PM-Tool's React screens to the Tuesday visual standard **in parallel, in the same
working tree**. Batch 1 (tokens, shared components, app shell, Resources workflow) is done. Your job: migrate the screens
you own so they look and behave like batch 1, without changing what they do.

- Worktree: `/Users/jaypatel/PM-Tool/.claude/worktrees/tuesday-visual-batch1` (web app in `web/`, React 19 + TS + Tailwind v4 + shadcn/Radix).
- Visual contract: `/Users/jaypatel/.codex/worktrees/29d5/PM-Tool/docs/design/README.md` (read §2–§8 and the §10 row for your area) and `tuesday-tokens.json` beside it.
  `reference/tuesday-full-mockup.html` is a synthetic visual reference only (its 10–12 px text is too small; use the scale below).
- Behaviour authority: `spec-parts/06-screens.md` (§13.0 global conventions + your screens' §13.x). Do not change behaviour.
- Reference implementations (read them first): `web/src/pages/Workload.tsx`, `web/src/pages/projects/Allocations.tsx`,
  `web/src/pages/Staff.tsx`, `web/src/pages/ReassignWork.tsx`, `web/src/pages/projects/ProjectLayout.tsx`, `web/src/components/hub/common.tsx`.

## Hard rules (parallel work)
1. **Edit only the files you own** (listed in your task). Never edit any other file — not shared components, not `index.css`,
   not `en.ts`, not tests, not configs. If a shared component needs a change, describe it in your final report instead.
2. **No git commands that change state** (no checkout/stash/reset/commit/add). Read-only `git diff -- <your files>` is fine.
3. **No npm install, no `npm run build`, no test runs, no servers, no browser automation.** Other agents and the dev server
   share this tree. A Vite dev server is already running and hot-reloads your edits.
4. Type-check with exactly: `cd web && npx tsc -p tsconfig.app.json --noEmit --incremental false` — then **only errors in your own
   files are yours** (others are mid-edit). Lint with `cd web && npx oxlint <your files>`; add no new warnings.
5. **Strings:** all UI text goes through `t()`. Reuse existing keys in `web/src/i18n/en.ts` wherever possible. New keys go **only**
   in your area file `web/src/i18n/en-<area>.ts` (already created, merged at runtime). Use your screens' existing namespace
   (e.g. `home.*`, `mywork.*`) and check with grep that a new key does not already exist in `en.ts` or another `en-*.ts` file.
6. **Freeze behaviour and contracts:** same queries, mutations, URL parameters, permission checks, conditions, validation,
   exports and props. Do not rename or change the props of anything exported (other files import them). Keep canonical
   status values verbatim (display them with `tv()`/`StatusPill`). Do not remove features or controls. No new dependencies.
7. **Keep accessible names stable:** `web/tests/*.cjs` drive screens by role, name and label (`getByRole('button', { name: 'Next' })`,
   `getByLabel('Search')`, headings, `navigation` names, dialog names…). Before finishing, grep the tests for your screens and
   keep every targeted role/name/label exactly. Exactly one element per name where tests expect uniqueness inside `<main>`.

## Visual rules (match batch 1)
- **Page frame:** wrap screens in `Page` (title, optional `eyebrow`, `subtitle`, `actions`). Inside a project, `Page` renders an h2
  automatically. One **black primary** action per page or dialog (`<Button>` default variant); secondaries `variant="outline"`;
  tertiary `ghost`; text links `variant="link"` or `text-primary underline underline-offset-4`; destructive actions
  `variant="destructive"` with explicit wording. Standalone page/toolbar buttons use the default size (40 px); `size="sm"`
  only inside table rows, cards and compact lists.
- **Type scale:** body/descriptions `text-base/6` (16 px) where it is prose; tables, controls and labels `text-sm` (14 px);
  captions/metadata `text-xs/[18px]` (12 px). Never below 12 px — replace `text-[10px]`, `text-[11px]`, `text-[13px]`.
  In-page section headings `text-lg/[26px] font-semibold tracking-[-0.2px]` or use `Section` (16 px card title). Use
  `tabular-nums` for hours, %, counts and dates; right-align numeric table columns.
- **Surfaces:** white cards `rounded-lg border bg-card`, no shadows on cards; subtle grouping `bg-muted`; panels/popovers are
  already styled. Card padding 20 px (`p-5`), section gaps 24 px (`gap-6`). Radius: controls/chips `rounded-md`, cards
  `rounded-lg`, panels `rounded-xl`.
- **Colour:** status = `Pill`/`StatusPill`/`Chip` (tinted bg + text + symbol, never colour alone). Identity/grouping =
  pastel accents only: `data-accent={accentOf(id)}` with `bg-(--acc-bg) text-(--acc-fg)` / `border-t-4 border-t-(color:--acc-stripe)`,
  or `<AccentDot id={projectId} />`. A project or person keeps one accent everywhere (derive from its id, never the row index).
  Accents never signal health, urgency or permission. People: `<Avatar id={personId} name={name} />`.
- **Controls:** native selects use `selectCls`; `Input`, `Textarea`, `Checkbox` from `@/components/ui`; every field has a
  persistent visible label — use `Field` (`label`, `htmlFor`, `hint`, `error`, `optional`). Required/optional cues via `optional`.
- **Filters (§13.0):** wrap in `FilterBar`; labelled fields in a `flex flex-wrap items-end gap-3` row; quick chips with
  `ChipToggle` (aria-pressed); active filter tokens with `ActiveFilters` + Clear when the screen already has URL filters.
  Keep the existing URL parameter names and behaviour.
- **Tables:** `TableRegion` > `<table className="w-full text-sm">`, `<thead className="bg-muted">`, `<th scope="col" className={thCls}>`,
  `<td className={tdCls}>` (density-aware padding and 36/48 px rows), `hover:bg-muted` rows, selected row
  `bg-accent shadow-[inset_3px_0_0_var(--primary)]`. Screens using `useTable` already get density; just align headers/cells.
- **Tiles:** `SummaryTile` (label, value, accent, hint, `to` or `onClick`+`selected`). Home order: blue, mint, lavender, peach.
- **States:** `Loading` (skeleton keeps shape), `Empty` (title + what would appear + useful next action), `ErrorBanner`
  (with `retry`), `Notice` (read-only / permission / scope explanations with the next step), `Missing` for unknown values
  (distinct from 0 and from empty). Saving shows "Saving…", failures keep the user's input, conflicts name who/when (existing
  `ErrorBanner`/`errorText` already do this — keep using them).
- **Responsive (§13.0):** ≥1280 full; 768–1279 tables may hide low-priority columns and scroll inside `TableRegion`;
  <768 lists become cards where the spec says so; no horizontal page overflow. Screens the spec marks desktop/tablet-only
  on phones (portfolio, templates, admin, project creation, milestone editing, resource views) use `DesktopOnly` with a
  `Notice` (see `Workload.tsx`).
- **Accessibility:** visible focus is global; keep labels; icons `aria-hidden` inside labelled buttons; selected states use
  `aria-pressed` / `aria-current` / `aria-selected` plus a visible cue beyond tint; interactive targets ≥ 24×24 px.

## Your final report (keep it factual)
Files changed; per screen what changed; new string keys (file + keys); type-check/lint result for your files; any
behaviour you intentionally left untouched; shared-component changes you need from the main agent; known gaps or risks.
Do not claim browser verification (you are not allowed to run one).
