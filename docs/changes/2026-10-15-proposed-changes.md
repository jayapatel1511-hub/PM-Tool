# Tuesday · Proposed Changes & Build Progress

**15 October 2026** · PM-Tool · Specification packets **039–047**

> [!IMPORTANT]
> **This is a planning and progress snapshot, not a release announcement.**
> The specifications below were drafted on 14 October and consolidated here on 15 October. Several areas are implemented and verified locally; the complete programme is still in progress. Publishing this document does **not** publish the implementation or deploy Tuesday.

## At a glance

| Direction | What it means for Tuesday |
| :--- | :--- |
| 🧭 Clearer planning | Explain capacity figures, keep edited rows visible, and show the context behind dates and counts. |
| ⚡ Faster everyday work | Load less code on first visit and refresh visible projects when colleagues make changes. |
| 🛠️ Safer delivery | Catch contract drift, missing registrations, migration hazards, and regressions before release. |
| 🔐 Reliable operations | Tighten served assets, retries, error reporting, recovery, and backup evidence. |
| 🌱 Additive product growth | Add recovery, timeline tools, reports, onboarding, and imports while retaining existing workflows. |

**Delivery principle:** existing features remain available. New behaviour uses the controls specified by its packet, including default-off flags where required. Human review and activation stay explicit.

## The proposed change programme

| Packet | Proposed change | Practical outcome | Position at this snapshot |
| :--- | :--- | :--- | :--- |
| [039 · Delivery process](../../specs/039-delivery-process/spec.md) | Parallel CI, coverage and test-count gates, migration checks, merge tiers, release images, rollback probes, review deployment, and promotion by digest. | Routine delivery becomes repeatable and reviewable, with evidence attached to each change. | **Partial local implementation.** Repository settings, remote CI, release and deployment workflow proof remain open. |
| [040 · Typed contract & entity registry](../../specs/040-typed-contract-and-registry/spec.md) | One entity registry, generated API types, response-shape checks, typed labels, saved-view fixes, compatible page modes, and a shared invalidation map. | Missing fields and registrations fail checks; Activity History and saved views become more consistent. | **Core foundation locally verified.** Further feature conversions and the later adoption decision remain open. |
| [041 · Capacity figures & server values](../../specs/041-capacity-figures-and-server-values/spec.md) | Honest partial capacity, distinct free-hours labels, server-owned load bands and permissions, shared rules, and temporary retention of edited Planner rows. | Supervisors can understand a figure and act without entries disappearing mid-edit. | **Implementation slices locally verified.** Human review and pilot activation remain open. |
| [042 · Route splitting & live updates](../../specs/042-route-splitting-and-live-updates/spec.md) | Build identity, update notices, lazy routes and panels, project refresh stamps, and an authenticated event stream. | Faster first load, recovery across updates, and permission-aware refresh without navigation. | **Routes, stamps, feed, and server stream locally verified.** Stream client and end-to-end streaming acceptance remain open. |
| [043 · Record engine](../../specs/043-record-engine/spec.md) | Measure a declarative list/panel engine against existing Risks and open-role screens before deciding whether to adopt it. | Reduce duplicated screen code only if the measured result is simpler and preserves behaviour. | **Spike authorised; not implemented.** Full engine adoption is not approved by the spike alone. |
| [044 · Operations & security hardening](../../specs/044-operations-and-security-hardening/spec.md) | Served-file allowlist, dead-letter retries, traceable errors, idempotency, security guards, account density, and off-host recovery. | Safer retries and clearer failures, backed by recovery evidence. | **Application slices locally verified.** Off-host backup diagnosis and a measured restore drill remain open. |
| [045 · Record recovery & counts](../../specs/045-record-recovery-and-counts/spec.md) | Show deleted/Restore, exact count links, working-day template offsets, discipline status bars, and Portfolio grouping. | Recover accidental deletions and move from a number to the records behind it. | **Specified; implementation pending.** |
| [046 · Task & timeline interaction](../../specs/046-task-and-timeline-interaction/spec.md) | Row actions, inline names, project-ranked people, date context, re-baselining, activity improvements, Timeline handoffs, and milestone chains. | Complete common actions in place and see schedule consequences before committing a change. | **Specified; implementation pending.** |
| [047 · Planning, reporting & onboarding](../../specs/047-planning-reporting-and-onboarding/spec.md) | Role-based getting started, capacity reports, aggregate route use, template roles, unfilled work, status notes, follow-up views, project import, and capacity polish. | Help the pilot get started and connect staffing demand, capacity, and follow-up work. | **Specified; implementation pending.** |

## What the product additions include

### Capacity people can interpret

- Label partial capacity on the person's row and present remaining hours as an upper bound where visibility is incomplete.
- Distinguish Planner **remaining capacity** from Workload **uncommitted hours**, with a short basis for each figure.
- Compute load bands and action permissions on the server using the rules the API actually enforces.
- Keep recently edited Planner entries visible until filters change or the page reloads.
- Add Team Capacity and Allocations by Source reports, unfilled roles, unassigned work, estimate-versus-planned context, optional weekday schedules, and Team time-away chips.

### Faster screens that stay current

- Load routes, panels, and larger areas when needed; retain first-screen menus, panel registration, keyboard access, and deep links.
- Identify the running build, notify an open tab of an update, and recover from a stale lazy-loaded chunk.
- Refresh an open project after another authorised person changes it, without a page reload.
- Keep the existing polling and delayed refresh behaviour as backstops while the event-stream client is added.
- Limit live notifications to small change hints; apply project visibility and private-record rules before sending them.

### Recovery and useful navigation

- Add a PM-only deleted-record view with Restore for tasks, deliverables, and milestones, including parent and concurrency checks.
- Make dashboard and Portfolio counts open the exact matching records, or explain what a total adds up.
- Offer working-day template offsets using the project's office calendar; existing calendar-day templates retain their default.
- Add discipline deliverable status segments and Portfolio grouping by client, office, or project type.

### Tasks and schedules with more context

- Add task-row Open, Start, Block, and inline rename through the existing permission and reason flows.
- Rank people by the viewer and project team, with broader search by job title and discipline.
- Show earliest consistent dates and successor warnings before a move; never silently reschedule work.
- Allow a reasoned PM re-baseline while retaining original dates and slip history.
- Add Timeline handoffs and a chain-to-milestone view, plus the specified saved-view, activity, related-record, and Gantt-state improvements.

### A more useful pilot

- Offer a small, hideable, role-specific getting-started card on My Work.
- Create open roles from template roles and add Watching / Requested by me views.
- Provide an optional project status note and an Admin project CSV import with preview before commit.
- Record aggregate route counts and load-time bands under the organisation control, without user identifiers, item identifiers, or entered text.

## Local verification already recorded

These results describe the local implementation checkout. They do not establish remote CI, hosted behaviour, or human acceptance. Larger browser sweeps below belong to the frozen route-splitting build; the later server-stream slice did not change those frontend assets.

| Check | Observed local result | Boundary |
| :--- | :--- | :--- |
| Backend discovery and four test partitions | **PASS · 1,296 / 1,296**, zero failures or skips. Partitions: 638 / 249 / 199 / 210. | Backend test aggregate; not the complete remote CI manifest. |
| Coverage gates | **PASS · 96.5%** rules-engine branch coverage; **95.8%** service line coverage. | Local coverage artifacts. |
| Recorded API response shapes | **PASS · 765 cases across 85 recorder files.** Reviewed additive changes; prior recorded shapes retained. | 365 / 382 mapped operations called; **17 remain uncalled**. |
| Initial JavaScript | **1,266,914 raw bytes · 43.2% reduction** against the recorded 2,229,116-byte baseline. | Initial graph measurement; not a user-perceived speed or compressed-transfer claim. |
| Route and panel checks | **PASS · 76 checks across 57 route patterns**, including failure recovery; eight axe audits reported zero violations. | Local built browser checks. |
| Before/after route sweeps | **PASS · 1,952 cases before and 1,952 after**, with matching case identities and page titles and no recorded failures. | Frozen builds, covered roles, widths, and routes. |
| Existing browser workflows | All nine existing mocked workflows passed during route verification. | Mocks verify UI behaviour, not hosted integration. |
| Latest real-API browser smoke | **PASS · 15 audited surfaces**, zero reported axe violations, browser errors, or bad responses. A different actor's project change refreshed in **3,816 ms** with zero navigation. | Built local API and PostgreSQL; stream client still pending. Disposable database removal independently confirmed. |
| Server event-stream tests | **PASS · 21 cases** included in the backend aggregate, covering authentication, visibility, private rows, revocation, limits, heartbeat, expiry, and shutdown. | Real PostgreSQL with the test host; not a tunnel or two-browser stream demonstration. |
| Build and repository checks | Release asset build, served-asset checks, contract/type checks, EF model drift, and whitespace checks passed. | Local build; no release image or deployment proof. |

### Self-review correction worth recording

The server stream originally subscribed after reading project visibility. A controlled slow-query test reproduced a permission-revocation race in that order. Subscription now starts before the visibility read, and the regression test passes. This is a locally verified fix; hosted streaming review remains required.

The feed also uses the specified timestamp/index fallback after a PostgreSQL probe disproved the assumed UUID ordering. It does not rely on UUID creation order for change delivery.

## Remaining work before a release claim

- [ ] Finish the event-stream client: authenticated reader, bounded reconnect/backoff, hidden-tab handling, coalesced invalidations, and reconnect refresh.
- [ ] Prove streaming with two browser sessions and the review transport/tunnel; complete its security and activation gates.
- [ ] Complete remaining delivery-process work, remote CI evidence, release image/provenance, rollback compatibility, and promotion/deployment preparation.
- [ ] Finish paced typed-contract conversions and the authorised record-engine spike; record the adoption decision before any wider migration.
- [ ] Diagnose scheduled off-host backup and complete a measured restore drill using the existing recovery arrangement.
- [ ] Implement packets 045–047 and run their relevant acceptance checks.
- [ ] Reconcile new specification additions and merged work as the other agent continues writing.
- [ ] Perform final integration review and collect applicable hosted, native, and human acceptance evidence.

## Publication and source boundary

**This publication contains this Markdown document only.** Application code, tests, configuration changes, generated contracts, specification edits, and local verification artifacts are excluded from its commit and push.

The implementation checkout is `codex/spec-completion`, based on `48586029`. The merged `main` baseline checked for this document is `e4ca1661`. Concurrent and untracked implementation work remains preserved separately. No operational feature flag is activated by this document, and no deployment is performed.

The linked packet specifications and maintained `spec-parts/` sources define behaviour. This brief is a readable snapshot, not a replacement for their requirements or acceptance criteria.
