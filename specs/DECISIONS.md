# Decision log

One table of the open product and process decisions for packets 038 to 047 and the build-approach report. Each row has a default.

- Defaults apply while work ships dark. A pending row never blocks a merge or a review deployment; it blocks only a flag flip, that is, turning its addition on for pilot users.
- Only Jay confirms or overrules a decision. Statuses: Open (the default applies), Decided, Done, Deferred (waits for its trigger).
- IDs: `RPT-NN` are the build-approach report's decisions (§7.1); `<packet>-Dn` are packet decisions as each packet's `spec.md` and spec part number them; `-DEF` rows are deferred or parked items with their triggers.
- Packet 039 slice S10 (FR-DLV-26) later adds the flag, last-change and source columns and the rows from packets 036 and 037 and the release-candidate handoff, and maps these statuses to its four: Open to default-active, Decided and Done to confirmed, Deferred to deferred. The source rows stay in their files.
- AI, machine-learning and LLM features are not decisions: constitution II excludes them in every phase.

| ID | Packet | Question | Default | Status |
|---|---|---|---|---|
| RPT-01 | 039 to 047 | Adopt the hybrid: delivery process v2, then evolve in place with one registry; the read-sync engine and the TypeScript port deferred behind named triggers | Adopt | Decided by Jay 2026-10-14: adopt everything, add only, no deadlines |
| RPT-02 | 039 | Line of record: reconcile `codex/deployment-ready` into PR #37 and merge it to main | Reconcile | Done: PR #37 is the line of record; Jay merges |
| RPT-03 | 039 | Amend the constitution's Development Workflow: review-stack auto-deploy, Tier A auto-merge, generated verification plus `acceptance.md`, and the boy-scout rule. Principles I to VII unchanged | No amendment until Jay directs it | Decided by Jay 2026-10-14: amend the Development Workflow (constitution 1.1.0) |
| RPT-04 | 039 | Install the review deploy timer with Jay's sudo, once (same as 039-D12) | Not installed | Open |
| RPT-05 | 039 | Container registry package public or private; is the public repository intended for an internal company tool? (same as 039-D1) | Private package | Open |
| RPT-06 | 039 | This log: defaults apply while dark; pending rows block only flag flips | Adopt | Decided by Jay 2026-10-14 with RPT-03: constitution 1.1.0 states this rule |
| RPT-07 | 039 | Three to five real company users on the pilot; Entra sign-in first (constitution VI) or local passwords? | The pilot keeps today's sign-in and users until Jay names them | Open |
| RPT-08 | 040 | Hold the platform long tail until pilot evidence names a pain, or run it on the ratchet now with the five-feature stop rule (same as 040-D8) | Ratchet with the stop rule | Open |
| RPT-09 | 038, 041, 044 | Roadmap rulings: 038 D1 to D7 (its code merges either way); 90 % as the default amber-band setting (same as 041-D4); 15-minute recovery point objective versus 24 h off-host (same as 044-D3) | Roadmap ranks 3 and 4 first; 038 merges with the reconcile on its current defaults | Open |
| RPT-10 | 039 | Optional machine account for agents so GitHub enforces Tier B review (same as 039-D2) | Label control only | Open |
| RPT-11 | 043 | Run the record-engine spike (same as 043-D1) | Run it | Decided by Jay 2026-10-14: build everything in the report, so the spike runs; adoption still needs a PASS verdict (FR-REC-13) |
| RPT-12 | 039 | Who activated `77b1b25c` on homedev? (split from RPT-02) | Recorded as unknown; Jay's next manual activation of merged main replaces it as the recorded hosted build | Open |
| 038-D1 | 038 | Which week My Staff shows | The current ISO week (Monday in `org_time_zone`), on every day of the week. No next-week switch or horizon. | Open |
| 038-D2 | 038 | Partial-view rule on My Staff | The person picker's (FR-PICK-02), including any later decision on it, so the two surfaces never disagree. | Open |
| 038-D3 | 038 | Admins and Executives on the all-staff scope | Figures for everyone, loaded one request per 50 rows (FR-SUP-02). | Open |
| 038-D4 | 038 | What holds the digest | A working day of the person's calendar with 0 h available, whatever the cause: a full-day Unavailable override, approved full-day leave or an office holiday. A Reduced day sends. | Open |
| 038-D5 | 038 | What happens to a held digest | Nothing is queued or merged. The next digest's "since the last digest" sections cover the time away under the existing caps. | Open |
| 038-D6 | 038 | Where the hold is controlled | A per-user preference, default on. No organisation setting. | Open |
| 038-D7 | 038 | Exports and reports | Unchanged. The column is screen-only. | Open |
| 039-D1 | 039 | Visibility of the container registry (GHCR) package | Private. Homedev keeps a read-only package token, root-owned, outside the release tree. | Open |
| 039-D2 | 039 | A machine account for agents, so GitHub enforces Tier B review | None. Tier B stays a label control. | Open |
| 039-D3 | 039 | Branch drift limit | 50 commits on origin/main since the branch started; a count, not a time. | Open |
| 039-D4 | 039 | Pull request size limits | Warn above 400 and fail above 1,000 hand-written lines (label `large` overrides). | Open |
| 039-D5 | 039 | Review deploy timer interval | 5 minutes. | Open |
| 039-D6 | 039 | When an image is review-proven for the pilot | Its probes passed on review, it has run on review for the soak period (a release parameter, default 2 days), and one nightly run on its commit is green. Jay promotes when he chooses. | Open |
| 039-D7 | 039 | When a flag is retired | After its pilot sign-off is recorded in the packet's acceptance.md, by the cleanup pull request. No date is set. | Open |
| 039-D8 | 039 | Who sees flag states | Every signed-in user receives names and on or off through /me. The packet shows only on Admin settings. | Open |
| 039-D9 | 039 | Branch coverage merge across shards | The highest per-line covered-branch count any shard reports (a lower bound). | Open |
| 039-D10 | 039 | Nightly checks that compare against a base UI (calm-acceptance quick, filters, deep links; calm-clicks) | Compare with the last green nightly commit. The first nightly runs the other checks only. | Open |
| 039-D11 | 039 | Constitution Development Workflow amendment | Adopted as constitution 1.1.0; it takes effect when Jay merges packet 039's spec pull request. Automatic Tier A merge (S13) then depends only on that merge, and the review timer (S14) also on 039-D12. | Decided by Jay 2026-10-14 with RPT-03: amendment adopted |
| 039-D12 | 039 | Review deploy timer installation | The code merges; Jay installs it once with sudo when he chooses. Until then activation stays manual as today. | Open |
| 039-D13 | 039 | Major dependency updates | Tier B; Jay merges. | Open |
| 039-D14 | 039 | Where the deployed commit is recorded | The running container's revision label and the timer's root-owned state file. The `current` link keeps meaning the last manual activation. | Open |
| 039-D15 | 039, 042 | Dockerfile build argument shared with §46 (packet 042) | RELEASE_SHA becomes HUB_BUILD. Packet 042 slice S01 adds it with the compose wiring; packet 039 slice S07 adds it only if it is still absent. | Open |
| 039-D16 | 039 | Does every packet keep a plan.md? | Yes, short, as Jay's constitution 1.1.0 amendment says (FR-DLV-21). The report proposed plan.md only where constitution VII complexity tracking applies. | Open |
| 039-DEF1 | 039 | Hours budget per discipline and read-only actuals import (parked) | Not built. Trigger: Jay changes §30 (budgets and timesheets) and §29 item 5. | Deferred |
| 039-DEF2 | 039 | Critical path, successor auto-shift and cross-project links (parked) | Not built. Trigger: Jay changes §30 (critical path), the "should not be built yet" auto-shift rule and constitution II as it applies. | Deferred |
| 039-DEF3 | 039 | Expiring read-only client link to one submission (parked) | Not built. Trigger: Jay changes constitution VI and §30 (client accounts). | Deferred |
| 039-DEF4 | 039 | Organisation-wide custom fields and statuses (parked) | Not built. Trigger: Jay changes §30 (custom fields); the record engine (§47) is the prerequisite. | Deferred |
| 039-DEF5 | 039 | Read-only machine access, a public API (parked) | Not built. Trigger: Jay changes §30 (public API); the typed contract (§44) is the prerequisite. | Deferred |
| 040-D1 | 040 | How does the registry find its entries? | Reflection in one place. Each entity file adds itself, so parallel changes never edit one shared list. | Open |
| 040-D2 | 040 | What are the new names for the page modes that use view= today? | Allocations tab=roles; Weekly Planner layout=list or layout=projects; Decisions tab=log. | Open |
| 040-D3 | 040 | What happens to legacy view= mode links? | They are accepted indefinitely and rewritten in place without a new history entry. They are never removed. | Open |
| 040-D4 | 040 | What happens to text that differs today between Text.cs and en.ts for the same key (23 of 79 shared keys, e.g. itemType.Action)? | Both sides keep their current text. Aligning a pair is a copy change Jay rules on key by key. | Open |
| 040-D5 | 040 | What text do the added itemType.* and field.* labels use? | Plain English from the names the specification uses for the entity and field. Jay may change any of them. | Open |
| 040-D6 | 040 | Are new response fields allowed during typed conversion? | Yes, with the shape snapshot regenerated in the same change so the addition shows in review. | Open |
| 040-D7 | 040 | What happens if the suite calls fewer than about 60 % of mapped endpoints? | Uncovered endpoints get tests before their file converts. Conversion continues file by file where endpoints are covered. | Open |
| 040-D8 | 040 | Does the platform long tail run now on the ratchet, or wait for pilot evidence (RPT-08)? | It runs on the ratchet now, with the five-feature stop rule. | Open |
| 040-D9 | 040 | How does the real-API browser suite sign in? | Through the development picker on loopback, against the CI database. | Open |
| 040-D10 | 040 | Which runner executes the keys.ts unit test? | Node's built-in test runner with type stripping (expected to work on Node 24, not yet tried). If it cannot load the file, the same assertions run in a mocked browser suite. | Open |
| 040-D11 | 040 | Are item-history lookups added for further entity types? | None are added here. Each one comes with the change that gives its type a history surface, under the FR-PLAT-05 privacy rule. | Open |
| 040-D12 | 040 | Does a feature change convert the files it touches (the boy-scout rule)? | Yes, within the files it already changes, once constitution 1.1.0 is in effect (039-D11, when Jay merges packet 039's spec pull request). Until then, conversions are separate changes. | Open |
| 040-D13 | 040 | What happens to page keys that saved views silently drop today (e.g. Portfolio computedHealth, Planner stale)? | Filter, sort, grouping, column and mode keys are added to the saved-view keys. Paging, panel and access-reason keys (e.g. Planner page, draftAccessReason) are declared as not kept, each with a reason. | Open |
| 040-D14 | 040 | Which visible changes ship unflagged? | The added Activity History labels, the saved-view key fixes and the page-mode names with old links kept ship unflagged as corrections of existing behaviour; their slices (S05, S06, S09) are Tier B. | Open |
| 040-DEF1 | 040 | TypeScript port of the server (deferred) | Not built. Trigger: a client-side what-if cannot be served by a server dry run in about 250 ms through the tunnel, local-first becomes a requirement, or measured agent friction with .NET remains after CI sharding. Needs a constitution VII entry Jay approves. | Deferred |
| 041-D1 | 041 | What is the Workload free-hours figure called, so it no longer shares "Remaining" with the Planner's remaining capacity? | "Uncommitted N h" (available minus committed load). The Planner keeps its canonical "Remaining capacity" (PLN-09, §10.9) and "N h free" in the picker and My Staff. Alternatives: "Spare"; or rename the Planner figure "Free to plan" and keep "Remaining" on Workload. | Open |
| 041-D2 | 041 | How should free hours show hidden work? | A Partial view label on the Planner row and an upper-bound figure ("at most N h"). No "Other project work N h" line, because it would reveal how much hidden work exists. | Open |
| 041-D3 | 041 | Should Workload adopt the Planner's per-person partial rule? | No. Keep today's rule: every row is partial for a viewer who cannot see every restricted project. A per-person rule is deferred. | Open |
| 041-D4 | 041 | Band names and default thresholds (confirm 90 % as the default amber setting, RPT-09) | Over above 110 %, Near above 90 %, Within from 40 %, Light below. These reproduce today's shading exactly. | Open |
| 041-D5 | 041 | What happens to a control whose server flag differs from today's client rule? | Offer it where the endpoint allows it; where today's client shows it and the endpoint refuses, keep it visible and disabled with the server's reason. Alternative: hide it. | Open |
| 041-D6 | 041 | How long does an edited Planner row stay, and does it count in totals? | Until the filters change or the page reloads. At most 50 kept ids. Kept rows count in totals and group subtotals (FR-GRP-02 unchanged). | Open |
| 041-D7 | 041 | Where does the guard against new client rule copies live? | Two counters in the §44.6 ratchet (packet 040), not a separate script. | Open |
| 041-D8 | 041 | Do exports change? | No. No band, uncommitted figure or kept row appears in any export. | Open |
| 041-D9 | 041 | Which changes ship behind flags, and which unflagged? | flag.figure_names covers the Workload name, the basis lines, the Partial view row label and the "at most" wording; planner_kept_rows covers kept rows. The three threshold settings and the Workload legend (S01, S02) and controls kept visible and disabled with the server's reason (S07, S09 to S11) ship unflagged as corrections; their slices are Tier B. | Open |
| 042-D1 | 042 | Who may read the build identifier in /health and the X-Hub-Build header? | Anyone. /health is anonymous today. Only the commit SHA is shown: no branch, host or time. | Open |
| 042-D2 | 042 | How are tabs left open across a deploy handled? | A failed lazy load reloads the page once per build. The previous release's /assets is not carried into the new image. | Open |
| 042-D3 | 042 | How does the update notice behave? | One notice per tab for each new build, with a Reload action. The page never reloads by itself. | Open |
| 042-D4 | 042 | What is the first-load target for /my-work? | At least 40 % less JavaScript than the single chunk before the split. Shared code that My Work, the shell or quick create imports from page modules moves into shared components where needed, with no behaviour change. | Open |
| 042-D5 | 042 | Where are the chunk boundaries? | The shell and My Work load eagerly. Then the Project, Admin and Planning areas, the signed-out entrance pages, and one chunk for each remaining page. | Open |
| 042-D6 | 042 | Which additions ship dark? | Both refresh additions: flag.live_project_stamp and flag.live_events, default off. The build stamp, update notice, caching and route splitting are not flagged; the update notice is an unflagged change users can see, so its slice (S02) is Tier B. | Open |
| 042-D7 | 042 | What transport carries events? | One fetch-based stream for every sign-in mode. No EventSource, WebSocket or SignalR. | Open |
| 042-D8 | 042 | What are the stream parameters, and where are they held? | Heartbeat 25 s, dead-stream timeout 60 s, stream lifetime 10 min, overlap 3 s, feed read every 1 s, coalescing 300 ms, 5 streams per user. They are held as Live:* configuration, not organisation settings. | Open |
| 042-D9 | 042 | What is streamed? | Project-scoped activity and evaluations only. Screens without a project keep today's refresh. TimeEntry and CalendarEvent rows go only to their actor. | Open |
| 042-D10 | 042 | Is the echo of a viewer's own change suppressed? | No. The acting tab may refetch once more, and there is no per-tab id. | Open |
| 042-D11 | 042 | In what order does the feed read activity? | By uuid v7 primary-key range. If the AC-LIVE-19 ordering test fails, an additive occurred_at index is added in a schema-lane slice. | Open |
| 042-DEF1 | 042 | Should a read-sync engine (ElectricSQL) be built for offline reads? | Deferred. Revisit only if offline reads become a requirement or the change feed measurably misses changes. It would also need a constitution VII entry that Jay approves. | Deferred |
| 042-DEF2 | 042 | The 1.5-second follow-up and the 4-second pulse once live events exist | Kept with live events on or off; no removal is proposed (add only). | Open |
| 043-D1 | 043 | Approve the constitution VII Complexity Tracking entry in specs/043-record-engine/plan.md for the spike and, on a confirmed PASS, adoption (RPT-11) | Approved for the spike; adoption beyond the spike screens follows a confirmed PASS verdict (043-D3) | Decided by Jay 2026-10-14: approved for the spike under his direction to build everything in the report; adoption beyond the spike screens still needs a PASS verdict (FR-REC-13, 043-D3) |
| 043-D2 | 043 | Which screens the spike converts | The Risks register list and Risk panel (Registers.tsx), and the open-roles list on the Allocations tab (Allocations.tsx). | Open |
| 043-D3 | 043 | How the spike verdict is reached | Measured against rules (a) to (g) of FR-REC-11, recorded in the packet's acceptance.md; Jay confirms by setting this row's status. PASS starts adoption; KILL starts the revert (S27), which keeps the flagged additions, and the fallback. | Open |
| 043-D4 | 043 | What the spike adds | Behind flag.record_engine_additions, off: Risks saved views; open-role column sorting, a read-only panel with History, and export entries in the Allocations Export menu. Adoption PRs add no surface. On a KILL verdict the additions are kept on the restored screens (FR-REC-14). If the spike never runs because 043-D1 is declined, the open-role export returns to packet 047's deferred list (§51.13). | Open |
| 043-D5 | 043 | Adoption order after a PASS | Milestones list; Milestone panel; Holidays; reference data; Users; project Team; WorkspaceTeam; Vacation; Allocations list; Reviews and Changes; Deliverables list; Deliverable panel; Decision panel; Meeting action panel; Project List; Portfolio list; project Activity; org Activity. A screen that misses a rule stays bespoke. | Open |
| 043-D6 | 043 | Hybrid and bespoke screens | Not converted (§47.1); each later conversion is a separate proposal with a parity checklist from its FR and AC IDs. | Open |
| 043-D7 | 043 | Generic create and update endpoints | Not built. Panels save through each entity's existing PATCH and transition endpoints. | Open |
| 043-D8 | 043 | Fallback table and order | RegisterTable (Registers.tsx:162-187) becomes DataTable in components/hub/registers.tsx. Order: open roles, Allocations list, project Team, WorkspaceTeam, Holidays, reference data, Users, Vacation, Milestones, Project List, Portfolio, Reviews and Changes. | Open |
| 043-D9 | 043 | How panels are registered | One explicit PANELS line per converted type in web/src/app/registrations.tsx (§46); no automatic fallback to RecordPanel for an unregistered type. | Open |
| 043-D10 | 043 | Merge tier of conversion PRs | Spike PRs (S03 to S06) are Tier B, Jay merges. Adoption and fallback PRs whose parity run passes with no test assertion changed are refactors with unchanged tests, so Tier A under §43, with parity screenshots attached. | Open |
| 044-D1 | 044 | Which served landing files move, and what does the CI allowlist check? | capture.json, landing README.md and fonts/sources.json move unchanged to docs/landing-assets/. Historical landing images stay served. Extension allowlist (.html .js .css .svg .jpg .jpeg .png .webp .ico .woff2 .txt) plus a sourceMappingURL check; no scan of file text for host or branch names. | Open |
| 044-D2 | 044 | Where does the off-host copy live, and when may pilot or company data join it? | The encrypted repository Jay already approved for review recovery data, through the existing export path; no new host, service or paid storage. Pilot or company dumps join only after Jay records approval. | Open |
| 044-D3 | 044 | Which recovery targets does the drill measure against, and what is the freshness window? (RPT-09) | §22's 15-minute RPO and 8-hour RTO stay the written targets; the drill records measured values against them and against the proposed 24-hour off-host RPO; §22 changes only by Jay's decision. Freshness window 26 hours. | Open |
| 044-D4 | 044 | Dead-letter limit, /health behaviour with dead letters, and how a retry is audited? | 10 attempts (the worker's existing limit) as a named constant, not a setting. /health stays Healthy when only dead-lettered events remain; they show on Operations and in watchdog alerts. A retry writes an application log line (like /admin/evaluate), not an activity-log row. | Open |
| 044-D5 | 044 | Error reference format and client-error report limits? | Route pattern, build when present, 6 random characters. Signed-in users only (errors before sign-in are not reported). Log only; 8 KB body; 10 a minute per person; 5 a page load. | Open |
| 044-D6 | 044 | How is copied-cookie replay after social sign-out checked on the hosted stack? | Server tests (SocialAuthTests) cover it; the hosted social replay check is a human check listed in specs/human-only.txt. | Open |
| 044-D7 | 044 | What does a request get when its idempotency key is claimed by a request still running? | 409 idempotency_in_progress with Retry-After: 1; a pending claim is abandoned after 2 minutes; the generic path keeps comparing the path only, not the body. | Open |
| 044-D8 | 044 | What copy is shown when the outcome of a failed request is unknown? | 502 and 503 say the change was not saved; 504 and no answer say the change may not have been saved. | Open |
| 044-D9 | 044 | Which server strings get the product name Tuesday? | Email and digest footers only; subjects (pinned by tests), 'Open in the Hub' and in-app server messages are unchanged in this packet. | Open |
| 044-D10 | 044 | Tab title format and how account density interacts with the browser's stored choice? | Notification count only, capped at 99+, no page name. The browser's stored density wins where one exists, as today; the account value applies in browsers with none; default stays compact. | Open |
| 044-D11 | 044 | Which slices change what users see without a flag? | The dead-letter list with Retry (S03), the error page and trace reference (S04), the updating messages (S10), the tab title count (S11), the email footers (S12) and the saved density (S13) ship unflagged as corrections and are Tier B. The other slices change nothing users see. | Open |
| 045-D1 | 045 | Who sees deleted items and restores them? | The existing RestoreItem rule: the project's PM or an Admin, on a project that is not Archived or Cancelled, with an account that is not Read Only. Discipline Leads do not. | Open |
| 045-D2 | 045 | Which records get Show deleted? | Tasks, deliverables and milestones only. Other registers deferred until the record engine (§47) is adopted for them or a PM asks. | Open |
| 045-D3 | 045 | Is the Show deleted toggle stored in saved views? | No. URL only (deleted=true), so no saved view ever opens on deleted items. | Open |
| 045-D4 | 045 | What happens when a child's parent (deliverable or milestone) is deleted? | Restore is refused, naming the parent's key. No cascade restore. | Open |
| 045-D5 | 045 | Does restore need a reason, and is the row version checked? | No reason on any project status, as today's task restore. If-Match is checked when sent; the undo toast keeps working without it. | Open |
| 045-D6 | 045 | How does a count that is a sum (hours, assignments, stalled reviews, overdue decisions across projects) open its list? | A one-line definition plus a link to the rows it sums. | Open |
| 045-D7 | 045 | What does the My Staff Reviews stalled tile open? | People with at least one stalled review, through a new reviewsStalled filter; old indicator=reviews links keep working. | Open |
| 045-D8 | 045 | May an existing count's value change to match its list? | No. Values and definitions stay; links carry the parameters that reproduce them. | Open |
| 045-D9 | 045 | Which calendar do working-day template offsets use? | Monday to Friday without organisation-wide and project-office holidays (the dependency-lag calendar), whatever working_days_enabled says. | Open |
| 045-D10 | 045 | Which groups make up the discipline deliverable status bar? | Not started; In progress (In Progress, In Review, Revision Required, Ready to Issue); Issued (Issued, Accepted); On hold. Cancelled left out. | Open |
| 045-D11 | 045 | Which Portfolio group keys, and what do subtotals show? | Client, Office and Project type; no grouping by default; counts and sums only, no rates, ranks or trends (§29 item 7 stays Phase 3). | Open |
| 045-D12 | 045 | Which deliverable figure does Portfolio show? | Overdue (G-01) from the deliverable list's indicator=overdue, not At Risk. | Open |
| 045-D13 | 045 | Which parts ship behind flags? | Part A behind flag.show_deleted, Part C behind flag.template_working_days, Part D behind flag.status_rollups, each server-enforced per FR-DLV-08. Part B (count links and definitions) ships unflagged as a correction of existing counts (FR-DLV-09); its slices (S03, S04, S05) are Tier B. With flag.show_deleted off, task restore behaves exactly as today. | Open |
| 046-D1 | 046 | Where do row quick actions appear? | The project Task List desktop table only. | Open |
| 046-D2 | 046 | Which quick actions? | Open, Start and Block. No status or label editor (§30 custom statuses). | Open |
| 046-D3 | 046 | Who counts as 'on this project' in the picker? | Current team members plus people removed within picker_recent_days = 90. | Open |
| 046-D4 | 046 | Picker order when no project is sent? | Unchanged: by display name; the viewer is not moved first. | Open |
| 046-D5 | 046 | Where does the earliest-date hint show? | Task and deliverable panels and the drag confirmation; list date cells unchanged. | Open |
| 046-D6 | 046 | Who sees the assignee's availability in the hint? | Only viewers who could already see that person in Workload or the Weekly Planner (§40.10 D8). | Open |
| 046-D7 | 046 | slip_display_days default? | 0, so today's display is unchanged until an Admin raises it. | Open |
| 046-D8 | 046 | What does a re-baseline store? | The item's planned dates at that moment; one baseline per item; 'Use original dates' clears it. | Open |
| 046-D9 | 046 | What does slip measure from? | The original date, everywhere slip appears today (indicator, Milestone View, exports, reports). A set PM baseline adds a separate "since re-baseline" figure beside it. | Open |
| 046-D10 | 046 | Who re-baselines, and what? | The project's PM; milestones and deliverables only, not tasks. This is the one §30 exception to "baselining beyond original dates". | Open |
| 046-D11 | 046 | Scope of the latest planned date vs target note? | Active projects with a target; open milestones and open deliverables; tasks excluded; information only, not a health input. | Open |
| 046-D12 | 046 | Activity grouping default and window? | Off by default; activity_burst_minutes = 10. | Open |
| 046-D13 | 046 | What does 'Create deliverable from this task' do to the task? | Prefill plus a Related link; the task is not moved under the new deliverable. | Open |
| 046-D14 | 046 | Flag granularity? | One flag per part; the §50.6 small additions share flag.small_ux. | Open |
| 046-D15 | 046 | Which handoffs show on the Timeline? | Project Timeline only; Cancelled not drawn; Incorporated hidden by Hide completed. | Open |
| 046-D16 | 046 | Source of the chain-to-milestone highlight? | The evaluator's stored D-13 affected milestones with chain_depth_limit; no new graph calculation. | Open |
| 047-D1 | 047 | Where does the getting-started card show? | My Work's Today view, collapsed to one line, on the person's own My Work only. | Open |
| 047-D2 | 047 | Which steps does each role get? | The §51.2 catalogue: three or four steps per role, each ticked by an existing record. | Open |
| 047-D3 | 047 | How does the card handle a person with several roles? | One group per held role, in the catalogue's order. | Open |
| 047-D4 | 047 | Team Capacity layout? | Measures as rows, weeks as columns, for the people in scope. | Open |
| 047-D5 | 047 | Do private drafts count in the capacity reports? | The caller's own drafts count by default, as on the planner, with a parameter to leave them out; other people's drafts never count. | Open |
| 047-D6 | 047 | What hours does Allocations by Source show? | Counted hours after coverage, all three confidence bands, with approved project allocations as their own row. | Open |
| 047-D7 | 047 | Where do route counts appear? | A separate Pilot route use report beside Pilot measures, which stays unchanged. | Open |
| 047-D8 | 047 | Is there a per-user opt-out from the route counter? | No. The organisation setting route_counter_enabled is the control, on for the pilot only, and users are told while it is on. flag.route_counter only releases the addition and is cleaned up as usual. | Open |
| 047-D9 | 047 | Do discipline packs added from a template create open roles? | No; roles come only with a whole new project. | Open |
| 047-D10 | 047 | What happens to a template role that cannot be dated? | Skipped with its reason shown in the wizard preview; it never blocks the project. | Open |
| 047-D11 | 047 | How many rows does Unassigned work show? | At most 50, with the count and Show all to the cross-project Unassigned list. | Open |
| 047-D12 | 047 | Who can read the project status note? | Everyone who can view the project; it is also the last Portfolio export column. | Open |
| 047-D13 | 047 | Which closed items does Requested by me list? | Those closed within the last 14 days, the window Recently completed uses. | Open |
| 047-D14 | 047 | Which watches does Watching list? | Every watch the person holds, manual or from a mention. | Open |
| 047-D15 | 047 | Which column names does the project import expect? | Tuesday's five headers (project number, name, client, office, Project Manager email) until Jay supplies a sample of the company project-list export; then the headers follow the sample as a data change. | Open |
| 047-D16 | 047 | What happens to an imported project number that already exists? | Skipped and never updated. | Open |
| 047-D17 | 047 | Step size for per-weekday hours? | 0.5 h, as planning entries use. | Open |
| 047-D18 | 047 | Does the Team tab away chip show for a person with a partial view? | Yes, because time away carries no project detail. | Open |
| 047-D19 | 047 | How many flags? | One per area: flag.onboarding_card, flag.capacity_reports, flag.route_counter (release only; route_counter_enabled is the lasting control), flag.template_roles, flag.unfilled_beside_capacity, flag.project_status_note, flag.my_work_follow_up, flag.project_import, flag.capacity_polish. | Open |
