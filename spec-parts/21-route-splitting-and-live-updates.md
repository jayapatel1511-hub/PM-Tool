## 46. Route Splitting and Live Updates Amendment

Approved scope: on 2026-10-14 Jay approved the build-approach report of that date (kept outside the repository) and directed building all of it. Features are fixed and additive only: nothing existing is removed, hidden, narrowed or put behind a flag. This section records the product contract delivered by packet 042: the report's sections 4.4 and 4.5 and its roadmap items 10 (build stamp), 16 (route splitting) and 33 (open screens refresh on others' changes). It adds a build identifier the server and the browser can compare, splits the single-page application by area without losing any registration, caches hashed assets safely, and lets an open screen refresh when someone else changes its project. Where an addition touches today's refresh, today's behaviour stays the default: both live-refresh additions ship dark behind flags, and with them off the application makes exactly today's requests.

Where this section adds to §23.4 (route-based code splitting per area), §22 and §23.8 (the page-load target and the `/health` probe), §13.0 (deep-linkable panels), §17.6 (in-app delivery) and §28 item 9 (server-sent events), this section governs. §17.6 and §28 item 9 are amended to record that change events now exist behind a flag and that polling stays. Flags follow §43 (packet 039). Events are routed through the query-key map of §44 (packet 040). The root error page and the outbox dead-letter are §48 (packet 044).

### 46.1 Scope and boundary

Part A (§46.2 to §46.4) is the build stamp, route splitting and asset caching. Part B (§46.5 and §46.6) is live refresh: a per-project stamp in the existing pulse first, then a stream of change events. Line citations are to the repository as of 2026-10-14. `web/src/app/registrations.tsx`, `web/src/lib/keys.ts` (from §44) and `scripts/check-live-stream.py` are new files.

Outside this amendment: removing or slowing any existing refresh (the 1.5-second follow-up, the 4-second pulse, the 15-second stale time, refetch on focus); events for screens without a project (Weekly Planner, My Week, availability, leave, Time); a client data store, offline mode or service worker; a WebSocket, SignalR hub, message broker or backplane; streaming item content of any kind; a pushed "new build" event; any change to routes, tabs, menus, permissions, data or the API beyond the additions named here.

### 46.2 Build stamp and update notice

- **FR-LIVE-01.** Every image MUST carry one build identifier: the full 40-character commit SHA passed as the `RELEASE_SHA` build argument of `hosting/Dockerfile.review`. The web stage compiles it into the SPA as `VITE_HUB_BUILD`; the runtime stage sets it as `HUB_BUILD`. The review and pilot compose files (`hosting/homedev.compose.yml:27`, `hosting/homedev-pilot.compose.yml:58,106`) pass the `RELEASE_SHA` they already use for the image tag, and the CI image of §43 passes the commit it builds. This packet owns the argument and the compose wiring (§43.14 D15). A build without the argument, such as local development and tests, uses `dev`. The identifier holds no branch, host, path or time. The SHA arrives as an argument because `.dockerignore` excludes `.git`.

- **FR-LIVE-02.** The server MUST send the identifier in an `X-Hub-Build` header on every response, and `/health` MUST add a `build` field beside its `status` and send `Cache-Control: no-store`. This section changes nothing else in `/health` (`src/Hub.Api/Program.cs:128-138`).

- **FR-LIVE-03.** When an API response carries a build identifier that differs from the SPA's own, and neither is `dev`, the SPA MUST show one notice: "Tuesday was updated. Reload to get the latest version." with a Reload action. It shows at most once per tab for each new server build, stays until it is dismissed or used, and never reloads the page by itself, so unsaved input is never lost. The check lives in the shared request helper (`web/src/lib/api.ts:33-55`), so every request path uses it. The notice uses the existing toaster (`web/src/main.tsx:31`) and is reachable by keyboard.

- **FR-LIVE-04.** The review and pilot activation probes (`scripts/verify-homedev-review.py`, `scripts/verify-homedev-pilot.py`) MUST accept the expected SHA and fail when `/health` reports a different `build`. The activation scripts (`scripts/activate-homedev-review.sh`, `scripts/activate-homedev-pilot.sh`) MUST pass the SHA they activate. The hosted build is then read from the server, not from records.

### 46.3 Route splitting with boot-time registrations

- **FR-LIVE-05.** Every module-level registration MUST run from one eager module, `web/src/app/registrations.tsx`, imported before the router is created (`web/src/main.tsx:20`). It covers all 22 that exist today: the seven `PANELS` entries (Task, Milestone, Deliverable, Decision, Action, Risk, Issue); `ItemSlots` Comments, Links and Raise; `ShellSlots` Search, Workspace, QuickCreate and Bell; `ProjectSlots` Health, Override and Follow; the two `ADMIN_EXTRA` entries; the two `CREATE_SOURCES` entries; and the `SETTINGS_SECTIONS` template entry. Today each runs as a side effect of importing its page (for example `web/src/pages/projects/TaskPanel.tsx:473`, `web/src/pages/projects/Registers.tsx:1044-1046`, `web/src/pages/admin/Holidays.tsx:93`, `web/src/components/hub/comments.tsx:162`), and a missing one fails silently (`web/src/components/hub/panel-host.tsx:17`, `web/src/pages/projects/slots-items.tsx:9-22`, `web/src/app/slots.tsx:18-25`). A registration whose component lives in a lazily loaded area registers a lazy wrapper with its own loading placeholder, so the key exists at boot and the code loads on first use. Each entry keeps today's label, order and behaviour: the Admin tabs end with Operations then Holidays, and the new-project sources list the template source before copy. No other module writes to these registries.

- **FR-LIVE-06.** The SPA MUST load the shell and My Work eagerly and load every other page when it is first visited, using the router's route `lazy` (react-router 8.4.0, installed). Pages are grouped into chunks by area: Project (`projects/:number/*` and `tasks/:id`), Admin (`admin/*`), Planning (`planner`, `workload`, `timeline`), and one chunk for each remaining top-level page. The signed-out landing and sign-in pages (`web/src/pages/Entrance.tsx`, imported at `web/src/lib/auth.tsx:9` and mounted by the sign-in gate at `:109-111` and `:149`) load as their own chunk, so a signed-in user never downloads them. This meets §23.4's code splitting per area. Every route path, redirect, deep link, query parameter and `?panel=` link in `web/src/app/routes.tsx` keeps working unchanged, and no route, tab or menu entry is added, removed or renamed.

- **FR-LIVE-07.** While a page or panel loads for the first time, the shell, sidebar and top bar MUST stay in place and the content area MUST show the existing loading placeholder (`Loading`, `web/src/components/hub/common.tsx:67`).

- **FR-LIVE-08.** CI MUST run a boot suite against the built bundle. It proves that every registration of FR-LIVE-05 is present on a fresh load of `/my-work` before any other page is visited, and it opens `/my-work?panel=Task:<id>` in a new tab as a deep link. The same suite MUST fail when any module other than `registrations.tsx` mutates `PANELS`, `ItemSlots`, `ShellSlots`, `ProjectSlots`, `ADMIN_EXTRA`, `CREATE_SOURCES`, `SETTINGS_SECTIONS` or `PROJECT_ROUTES` in any way: assignment, index assignment, `push`, `unshift` or `splice`. Today the template source joins `CREATE_SOURCES` by `unshift` (`web/src/pages/projects/FromTemplate.tsx:119`) and copy by `push` (`web/src/pages/projects/CopyStructure.tsx:24`); `registrations.tsx` registers the template source before copy, keeping that order.

- **FR-LIVE-09.** Each CI build MUST record, as a build artifact, the size of every JavaScript chunk and the first-load JavaScript for `/my-work`: its entry chunk plus that chunk's static imports, read from the Vite manifest. The manifest is written to `src/Hub.Api/wwwroot/.vite/manifest.json`; the server never serves a dot-prefixed path, and the §48 served-asset check skips that folder (FR-OPS-02). Today there is one chunk of 2,229,116 bytes (local build of 2026-10-14; the report measured 2,219,073 bytes) and `web/src` has no `lazy(` call.

### 46.4 Asset caching and version skew

- **FR-LIVE-10.** Hashed files under `/assets/` MUST be served with `Cache-Control: public, max-age=31536000, immutable`. `index.html`, whether requested by name or served as the SPA fallback (`src/Hub.Api/Program.cs:109-110,175`), MUST be served with `Cache-Control: no-cache`. Other static files keep today's headers, and API responses keep `no-store` (`src/Hub.Api/Infrastructure/Http.cs:57`).

- **FR-LIVE-11.** A deploy replaces every hashed chunk (`web/vite.config.ts` empties the output folder), so a tab left open can ask for a chunk that no longer exists. When loading a lazy page, panel or slot fails, the SPA MUST reload the page once, remembering in session storage the build it reloaded for, so one build never loops. If loading fails again after that reload, the root error page of §48 shows with its Reload action and no further automatic reload happens. The reload keeps the URL, so the user returns to the page they asked for.

### 46.5 Project refresh stamp

- **FR-LIVE-12.** With `flag.live_project_stamp` on, while a project page is open the existing notification pulse (`GET /api/v1/me/notifications/pulse`, `src/Hub.Api/Features/Notifications.cs:44-55`) MUST carry the open project's id, and its response MUST add a `projectStamp` built from two values: the time of the latest activity row in that project that the viewer's History filter returns (`ActivityEndpoints.Visible`, `src/Hub.Api/Features/Activity.cs:53-69`), and the project's last evaluation time (`project_state.evaluated_at`). The existing `stamp` stays byte-identical. For a project the viewer cannot see, or one that does not exist, `projectStamp` is absent and the call does not fail. With the flag off, the request and the response are exactly today's.

- **FR-LIVE-13.** When `projectStamp` changes after its first reading in a tab, the SPA MUST invalidate the queries that `useProjectRefresh` invalidates for that project (`web/src/hooks/data.ts:15-28`), without adding a timer. The pulse keeps its 4-second interval and its pause in hidden tabs (`web/src/components/hub/notifications.tsx:39-51`).

### 46.6 Change events

- **FR-LIVE-14.** With `flag.live_events` on, `GET /api/v1/events` MUST return a `text/event-stream` for the signed-in viewer, under the same authentication and authorisation as every other API call: the review session cookie, an Entra bearer token (`src/Hub.Api/Infrastructure/Auth.cs:124`) or the development scheme. The SPA MUST read it with `fetch` and a stream reader, sending the same headers as `api()`, because `EventSource` cannot send an `Authorization` header. No token, user id or project id goes in the URL. With the flag off, the endpoint answers 404 and the SPA opens no stream.

- **FR-LIVE-15.** The server MUST build events in-process from committed rows only, with no new service, broker or datastore (constitution VII). One feed per API instance runs while at least one stream is open. It reads `activity_log` rows by primary-key range (uuid v7, `src/Hub.Api/Data/Entities.cs:734-753`) with a 3-second overlap and de-duplication by id, so a row that commits late is still sent once, and it reads `project_state.evaluated_at`. It never writes, and it never reads the outbox, which the evaluation worker consumes (`src/Hub.Api/Infrastructure/EvaluationService.cs:254`). An event is `{topic: "project", projectId, itemType}` or `{topic: "evaluated", projectId}` and nothing else: no item id, key, name, value, actor or reason.

- **FR-LIVE-16.** An event MUST reach a viewer only when the row has a project; the viewer can view that project by the rule every list uses (§8.7; `Access.VisibleProjects`, `src/Hub.Api/Infrastructure/Access.cs:46-56`); and, for `TimeEntry` and `CalendarEvent` rows, the viewer is the row's actor. This covers restricted projects and open roles (FR-ROLE-05). Rows without a project are never streamed, which covers planning entries and their private drafts (FR-PLN-18), availability, leave, users and settings. The set of projects a stream may receive is computed when the stream opens, and again before any batch that contains a Project, Member or User row. Each stream ends after at most 10 minutes, or when its bearer token expires if that is sooner, and the client reconnects under fresh authentication, so a revoked session or a changed system role takes effect within that bound. What a viewer receives is never wider than what the History filter (`ActivityEndpoints.Visible`) returns for them, except that an actor's own calendar-event changes reach their other tabs.

- **FR-LIVE-17.** The server MUST send a heartbeat comment every 25 seconds (configurable, always under 60 seconds) and send each event as soon as it is read, with buffering off: `Cache-Control: no-store` as for every `/api` response, `X-Accel-Buffering: no`, and no compression. The client MUST treat 60 seconds without data as a dead stream; reconnect with a backoff of 1, 2, 4, 8, 16 and then 30 seconds; close its stream while the tab is hidden and reopen it when the tab is visible; and after every reopen, invalidate the open project's queries once to cover anything missed. A user may hold at most 5 open streams; a further request gets 429, and that tab keeps today's refresh. Streams end cleanly when the server stops, so deploys and restarts do not wait on them.

- **FR-LIVE-18.** The SPA MUST route each event through the query-key map of §44 (`web/src/lib/keys.ts`). A project event invalidates the keys mapped to its item type for that project. An evaluated event invalidates the project-derived keys that `useProjectRefresh` refreshes in its 1.5-second follow-up. Every event also refetches the notification pulse, so unread and Following counts update on the event. Events that arrive within 300 milliseconds of each other are handled in one pass. Events only add refreshes; nothing refreshed today is skipped.

- **FR-LIVE-19.** Today's refresh MUST stay unchanged with either flag on or off: `useProjectRefresh` with its 1.5-second follow-up, the 4-second pulse, and the 15-second stale time with refetch on focus (`web/src/main.tsx:14-18`). With both flags off, the SPA makes exactly today's requests. Writes that bypass `SaveChanges` write no activity row and so send no event: 28 `ExecuteUpdate`, `ExecuteDelete` or `ExecuteSql` sites in 14 files at the time of writing (for example `src/Hub.Api/Features/Tasks.cs` and `src/Hub.Api/Features/Templates.cs`). The kept refresh is their backstop.

- **FR-LIVE-20.** `flag.live_events` MUST NOT be turned on in review or pilot until a tunnel check through the cloudflared tunnel has passed AC-LIVE-17 and the outbox dead-letter of §48 is in place, so a stranded outbox row cannot silence a project's evaluated events without showing on Admin > Operations. Both flags follow §43: declared in `src/Hub.Domain/Flags.cs` with their packet, stored as `flag.*` settings, returned on `/me`, enforced on the server through §43's shared guard, each with a tagged flag-off and flag-on test, and used only for these unreleased additions.

### 46.7 Acceptance criteria

- **AC-LIVE-01.** Given an image built with `RELEASE_SHA=<sha>`, then `/health` returns its `status` and `build: "<sha>"` with `Cache-Control: no-store`, and every response, including static files and error responses, carries `X-Hub-Build: <sha>`. Given no argument, both say `dev`.

- **AC-LIVE-02.** Given the SPA built as A and the server answering B, when any API response arrives, then one "Tuesday was updated" notice with Reload shows; further responses from B add no second notice; the page never reloads by itself; and the notice and its action pass axe and are reachable by keyboard. Given `dev` on either side, no notice shows.

- **AC-LIVE-03.** Given an activation of SHA X while `/health` reports Y, then the probe fails and names both values. Given X, it passes.

- **AC-LIVE-04.** Given a fresh load of `/my-work` with no other page visited, then each of the seven panel types opens from a `?panel=<Type>:<id>` link with its Comments tab; the task and deliverable panels show Links and the Raise menu; the top bar shows search, the workspace selector, quick create and the bell; the new-project dialog opened from quick create offers the template source and then copy; and the Admin tabs end with Operations then Holidays. After a project opens, its header shows health, the override action and follow, and its settings show the template section.

- **AC-LIVE-05.** Given `/my-work?panel=Task:<id>` opened in a new tab, then the task panel opens over My Work, and its "Open full page" control goes to `/tasks/<id>`.

- **AC-LIVE-06.** Given a seeded line `PANELS.Demo = ...` in any page module, then the boot suite fails and names the file.

- **AC-LIVE-07.** After the split, every route in `web/src/app/routes.tsx` renders its page at its path; every mocked browser suite in CI and every local-only suite passes unchanged; and the preview sweep (`tools/preview/sweep.cjs`) shows no new axe violation.

- **AC-LIVE-08.** Given the CI build artifact, then the first-load JavaScript for `/my-work` is at least 40 % smaller (decision D4) than the single chunk before the split, and every chunk's size is listed.

- **AC-LIVE-09.** Given a test web root holding `index.html` and `assets/app-<hash>.js`, then the asset is served `public, max-age=31536000, immutable`; `/`, `/index.html` and a deep path such as `/projects/P-1/tasks` are served `no-cache`; `/api/v1/me` keeps `no-store`; and other static files keep today's headers.

- **AC-LIVE-10.** Given a tab on build A after the server moved to build B with different chunks, when the user opens a page whose chunk is gone, then the page reloads once and opens on build B at the same URL. Given the chunk still fails after that reload, then the root error page shows with Reload and no second automatic reload happens.

- **AC-LIVE-11.** Given `flag.live_project_stamp` on and Alex on project P's Tasks tab, when Blair changes a task in P, then Alex's list shows the change within 5 seconds of the commit without a reload or a focus change, and after P is evaluated Alex's indicators refresh the same way. Given the flag off, the pulse request names no project and its response equals today's.

- **AC-LIVE-12.** Given a restricted project R that the viewer cannot see, then a pulse naming R returns no `projectStamp` and the same response as a pulse naming an unknown id, and the existing `stamp` is unchanged.

- **AC-LIVE-13.** Given `flag.live_events` on and two users in two browsers, one on project P's Tasks tab and one on Workload, when one changes a task in P, then the other's open view shows it within 2 seconds on the local stack without a reload or a focus change, and after evaluation the derived indicators follow on the evaluated event.

- **AC-LIVE-14.** Given streams open for an Admin, an Executive, restricted project R's PM, a member of R, a non-member, a Read-only user, and a member removed from R while connected, when changes are written in turn to a task in R, an open role in R, a task in an open project, a private planning draft, a time entry and a private calendar event in an open project, then each stream receives exactly the events FR-LIVE-16 allows, the removed member receives nothing for R after the removal, and no payload has any field other than `topic`, `projectId` and `itemType`.

- **AC-LIVE-15.** Given `flag.live_events` on, then the stream authenticates with an `Authorization` header and no cookie in the header-based test scheme; no request URL carries a token, user id or project id; and in the browser the stream request carries the same authentication headers as an API call. Given the flag off, `/api/v1/events` returns 404 and the SPA makes no request to it.

- **AC-LIVE-16.** Given an open stream, then a heartbeat arrives every 25 seconds. Given 60 seconds of silence, the client reconnects with the stated backoff and invalidates the open project once. Given the tab hidden, its stream closes, and it reopens when the tab is visible. Given a sixth stream for one user, it gets 429. Given the server stopping, open streams end and the host does not wait on them.

- **AC-LIVE-17.** Given the review origin behind cloudflared, then `scripts/check-live-stream.py` holds a stream that is idle except for heartbeats for 10 minutes with no disconnect, and an event written during the run arrives within 2 seconds. A run with heartbeats off records the tunnel's idle cutoff. The result is recorded before `flag.live_events` is turned on in review.

- **AC-LIVE-18.** Given both flags off, then a scripted session (open My Work, open a project's Tasks tab, change a task) makes the same requests as before this packet, including the 1.5-second follow-up and the 4-second pulse, and none to `/api/v1/events`. Given both flags on, the 1.5-second follow-up and the 4-second pulse still run.

- **AC-LIVE-19.** Given ids created in order by `Guid.CreateVersion7()`, then PostgreSQL orders them the same way. Given a row whose transaction commits up to 3 seconds after a row with a later id, then the feed sends it exactly once. The feed writes no row.

### 46.8 Decisions and deferred proposals

Each decision below uses its default until Jay decides otherwise.

| # | Decision | Default used |
|---|---|---|
| D1 | Who may read the build identifier | Anyone, as `/health` is anonymous today. Only the commit SHA: no branch, host or time. |
| D2 | Version skew after a deploy | Reload once on a failed lazy load (FR-LIVE-11). The previous release's `/assets` is not carried into the new image. |
| D3 | Update notice | One notice per tab per new build, with Reload. Never an automatic reload. |
| D4 | First-load target for `/my-work` | At least 40 % less JavaScript than the single pre-split chunk. Where shared code that My Work, the shell or quick create imports from page modules blocks it (`web/src/pages/MyWork.tsx:23-28`, `web/src/app/Shell.tsx:18`, `web/src/components/hub/new-item.tsx:16-18`), that code moves into shared components with no behaviour change. |
| D5 | Chunk boundaries | Shell and My Work eager; Project, Admin and Planning areas; the signed-out entrance pages; then one chunk per remaining page. |
| D6 | Which additions ship dark | Both refresh additions: `flag.live_project_stamp` and `flag.live_events`, default off. The build stamp, notice, caching and route splitting are not flagged: each is complete in one slice and changes no feature. The update notice is an unflagged change users can see, so its slice (S02) is Tier B (§43.11). |
| D7 | Event transport | One `fetch`-based stream for every sign-in mode. No `EventSource`, WebSocket or SignalR. |
| D8 | Stream parameters | Heartbeat 25 s, dead-stream timeout 60 s, stream lifetime 10 min, overlap 3 s, feed read every 1 s, coalescing 300 ms, 5 streams per user. Held as `Live:*` configuration, not organisation settings, because they tune transport and are not rule thresholds (constitution II). |
| D9 | What is streamed | Project-scoped activity and evaluations only. Screens without a project keep today's refresh. `TimeEntry` and `CalendarEvent` rows reach only their actor. |
| D10 | A viewer's own change | Not suppressed: the acting tab may refetch once more. No per-tab id. |
| D11 | Feed order | uuid v7 primary-key range. If AC-LIVE-19's ordering test fails, the feed reads by `occurred_at` with an additive index, added in a schema-lane slice. |

Deferred follow-up proposals, outside packet 042:

- per-tab echo suppression;
- person-scoped events for the Weekly Planner, My Week, availability and Time;
- a pushed "new build" event;
- events for writes made outside `SaveChanges`;
- carrying the previous release's assets;
- a read-sync engine (ElectricSQL) for offline reads, deferred until offline reads become a requirement or the change feed measurably misses changes;
- service-worker caching or offline mode.

The 1.5-second follow-up and the 4-second pulse stay with live events on or off.
