# Feature Specification: Route Splitting and Live Updates

**Packet**: 042 | **Feature directory**: `specs/042-route-splitting-and-live-updates` | **Date**: 2026-10-14
**Status**: Specified, not started.
**Input**: On 2026-10-14 Jay approved the build-approach report of that date (kept outside the repository) and directed building all of it: "We are building everything you suggested." Current features are fixed; additions only. This packet carries the report's sections 4.4 and 4.5 and its roadmap items 10, 16 and 33.
**Source**: product specification §46 (§46.1 to §46.8) in `spec-parts/21-route-splitting-and-live-updates.md`. Builds on §23.4 (route-based code splitting per area), §22 (page-load target), §23.8 (`/health`), §13.0 (deep-linkable panels), §8.7 (visibility model), FR-ROLE-05 (open-role visibility) and FR-PLN-18 (private drafts). Amends §17.6 (in-app delivery) and §28 item 9 (server-sent events). Uses §43 flags (packet 039), the §44 query-key map (packet 040), and the §48 root error page and outbox dead-letter (packet 044).

## User outcome

Testers and Jay can see which build a server runs, and an open tab says once, politely, when Tuesday was updated. The first visit to My Work loads much less code, yet every panel, menu and slot works from the first screen, and a tab left open across a deploy recovers by itself. When the flags are on, a project screen someone has open refreshes when a colleague changes that project, without a reload, and nobody ever learns of changes in a project they cannot see.

## Scope

1. **Build stamp and update notice** (FR-LIVE-01, FR-LIVE-02, FR-LIVE-03, FR-LIVE-04; §46.2): the commit SHA as a build argument, compiled into the SPA and set in the container; an `X-Hub-Build` header and a `build` field on `/health`; one "Tuesday was updated" notice with Reload; activation probes that check the hosted build.
2. **Route splitting with boot-time registrations** (FR-LIVE-05, FR-LIVE-06, FR-LIVE-07, FR-LIVE-08, FR-LIVE-09; §46.3): all 22 module-level registrations (`PANELS`, `ItemSlots`, `ShellSlots`, `ProjectSlots`, `ADMIN_EXTRA`, `CREATE_SOURCES`, `SETTINGS_SECTIONS`) moved to one eager module behind a boot suite and a `?panel=Task:<id>` deep-link test from `/my-work`; then lazy Project, Admin and Planning areas, signed-out entrance pages and remaining pages; chunk sizes recorded per build.
3. **Asset caching and version skew** (FR-LIVE-10, FR-LIVE-11; §46.4): immutable `/assets`, `no-cache` `index.html`, and one reload per build when a lazy load fails.
4. **Project refresh stamp** (FR-LIVE-12, FR-LIVE-13; §46.5), behind `flag.live_project_stamp`: the existing pulse also reports a stamp for the open project, and a change refreshes that project's queries.
5. **Change events** (FR-LIVE-14, FR-LIVE-15, FR-LIVE-16, FR-LIVE-17, FR-LIVE-18, FR-LIVE-19, FR-LIVE-20; §46.6), behind `flag.live_events`: an in-process feed over committed activity-log rows and evaluations; a `fetch`-read event stream that works with Entra bearer tokens; the list visibility rule applied to every event; heartbeat, reconnect and hidden-tab rules; routing through the §44 key map; today's refresh kept in every flag state; a tunnel check and the §48 dead-letter before the flag goes on in review.

Where an addition touches existing behaviour, today's behaviour stays the default. With both flags off the SPA makes exactly today's requests. No route, tab, menu entry, permission or existing API response changes, apart from the added header, the added `/health` field and the added pulse field when its flag is on.

## Non-goals

- Removing or slowing any existing refresh: the 1.5-second follow-up (`web/src/hooks/data.ts:23-27`), the 4-second pulse, the 15-second stale time or refetch on focus.
- Events for screens without a project (Weekly Planner, My Week, availability, leave, Time); events for writes outside `SaveChanges`.
- A client data store, offline mode, service worker, WebSocket, SignalR hub, broker or backplane; item content in events.
- A read-sync engine (ElectricSQL), deferred until offline reads become a requirement or the change feed measurably misses changes.
- The root error page, client-error log and outbox dead-letter themselves (§48, packet 044); the flag mechanism and CI image (§43, packet 039); the key map (§44, packet 040).

## Decisions (defaults apply until Jay decides otherwise)

| # | Decision | Default |
|---|---|---|
| D1 | Who may read the build identifier | Anyone; commit SHA only. |
| D2 | Version skew | Reload once on a failed lazy load; old assets not kept. |
| D3 | Update notice | One per tab per new build; never an automatic reload. |
| D4 | First-load target for `/my-work` | At least 40 % smaller than the pre-split chunk; shared code moves out of page modules if needed. |
| D5 | Chunk boundaries | Shell and My Work eager; Project, Admin, Planning areas; signed-out entrance pages; one chunk per other page. |
| D6 | What ships dark | Both refresh additions; not the stamp, notice, caching or splitting; the notice's slice is Tier B. |
| D7 | Event transport | One `fetch`-based stream for every sign-in mode. |
| D8 | Stream parameters | Heartbeat 25 s, dead after 60 s, lifetime 10 min, overlap 3 s, read every 1 s, coalesce 300 ms, 5 streams per user; `Live:*` configuration. |
| D9 | What is streamed | Project-scoped activity and evaluations; `TimeEntry` and `CalendarEvent` rows to their actor only. |
| D10 | A viewer's own change | Not suppressed. |
| D11 | Feed order | uuid v7 key range; an additive `occurred_at` index only if the ordering test fails. |

Deferred proposals are listed in §46.8.

## Acceptance

Acceptance criteria are AC-LIVE-01, AC-LIVE-02, AC-LIVE-03, AC-LIVE-04, AC-LIVE-05, AC-LIVE-06, AC-LIVE-07, AC-LIVE-08, AC-LIVE-09, AC-LIVE-10, AC-LIVE-11, AC-LIVE-12, AC-LIVE-13, AC-LIVE-14, AC-LIVE-15, AC-LIVE-16, AC-LIVE-17, AC-LIVE-18 and AC-LIVE-19, as written in §46.7.

- Build stamp: AC-LIVE-01, AC-LIVE-02, AC-LIVE-03.
- Splitting and registrations: AC-LIVE-04, AC-LIVE-05, AC-LIVE-06, AC-LIVE-07, AC-LIVE-08.
- Caching and skew: AC-LIVE-09, AC-LIVE-10.
- Project stamp: AC-LIVE-11, AC-LIVE-12.
- Change events: AC-LIVE-13, AC-LIVE-14, AC-LIVE-15, AC-LIVE-16, AC-LIVE-17, AC-LIVE-18, AC-LIVE-19.

AC-LIVE-17 runs against the hosted review origin and needs an activation Jay authorises.

## Depends on

- The spec PR for this packet, with the §17.6 and §28 item 9 amendments, before any slice.
- Packet 039 (§43): the flag list and `flag.*` settings on `/me` before slices S08, S10 and S11. S01 owns the `RELEASE_SHA` argument and the compose wiring (039-D15), and 039's CI image passes it once S01 lands. Its nightly real-API job runs S12's browser check. After S01 and S03 merge, Jay refreshes the root-owned compose and probe copies before the review timer deploys again (FR-DLV-18).
- Packet 040 (§44): the `web/src/lib/keys.ts` slice before S11.
- Packet 044 (§48): the root error page slice before S06; the outbox dead-letter slice before `flag.live_events` is turned on in review.

Slice order and tests are in [tasks.md](tasks.md).

## Flags

- `flag.live_project_stamp`: the project stamp in the pulse (§46.5). Default off.
- `flag.live_events`: the change-event stream (§46.6). Default off; turned on in review only after AC-LIVE-17 passes and the §48 dead-letter is in place.

Both follow §43. Slice S13 removes them once Jay has turned them on in review and pilot, keeping the features on.

## Wording rule

Code, interface strings, specification text, commit messages and this packet's records name no competitor product. Libraries and tools are named only where the work uses or defers them.
