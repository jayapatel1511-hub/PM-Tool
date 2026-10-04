# Verification: Hardening and Pilot

Date: 2026-09-25. Engineering scope only: the pilot itself, the threat-model walkthrough, restore drills in Azure and
any penetration test are for the team and the organisation, and are prepared (see "Not done here").

## Automated checks

| Check | Result |
|---|---|
| `dotnet build Hub.slnx -c Release` with all .NET security analysers as errors | Pass, no findings |
| `dotnet test` | Pass (284/284) |
| `python3 tools/coverage_gate.py` | Pass: rules engine 97.1 % branches, services 90.2 % lines |
| `npm run build` and `npm run lint` (now including jsx-a11y rules, added to CI) | Pass |
| `npm audit --audit-level=critical --omit=dev`; `dotnet list package --vulnerable` | No critical findings |
| `python3 tools/trace_spec.py --check` | Pass |
| `az bicep build --file infra/main.bicep` (local compile only, nothing deployed) | Pass |

New tests: `OperationsTests` — each alert condition only past its threshold (evaluation lag, nightly grace, job
failures, digest deadline, weekends), the admin Operations endpoint (Admin only) and a watchdog run; the pilot measures
from snapshots, coordination reviews and status changes (and refused without portfolio access); `ExportSafetyTests` —
formula-like CSV text neutralised, numbers kept. `EvaluationTests` now checks the nightly snapshot keeps the day's counts.

## Performance at full scale (FR-013, US5)

Synthetic data (`tools/scale/seed.sql`): 600 projects (500 Active), 98,832 open of 123,540 tasks (one 5,000-task and one
2,000-task project), 6,000 deliverables, 3,000 milestones, 17,135 dependencies, 2,000,129 activity entries, 490 people.
API: Release build on the development Mac; PostgreSQL 17 in Docker limited to **2 CPUs and 4 GB** (production plan: 2
vCores, 8 GB). Client: `tools/scale/measure.py`, 100 concurrent users, 3 rounds each.

| Target (§22) | Result |
|---|---|
| Nightly re-evaluation and snapshots, 500 projects / 100,000 tasks, ≤ 30 min | **74 s** |
| Derived state after a change on a 2,000-task project ≤ 2 s | **0.6–0.9 s** |
| List endpoints and item reads ≤ 500 ms p95 at 100 concurrent users | projects page p95 **60 ms**, tasks page 25 ms, task item 38 ms |
| My Work, Project Dashboard, 500-task list ≤ 2 s p95 (page) | API p95: My Work **221 ms**, dashboard 100 ms (5,000 tasks: 256 ms), 500-task list 178 ms |

Realistic profile (each person pauses 1.5–4.5 s between screens): all of the above; Home 326 ms and search 219 ms p95.
Stress profile (no pauses, ~80 requests per second sustained): the 2-CPU database saturates — lists p95 0.4–0.8 s, pages
1.7–4 s — which bounds the throughput of this size of server; scaling the database tier is the §22 remedy.

Found and fixed on the way (each re-measured):

| Finding | Effect | Fix |
|---|---|---|
| Nightly run shared one database context across projects | quadratic: 0.5 projects/s and falling; projected 15+ min | one unit of work per project: 500 projects in 74 s |
| Job host ran jobs one after another | email, digests and the watchdog waited behind the nightly run | jobs run independently (advisory lock still one run per job) |
| Task rows used multi-column `First()` in the projection | EF ranked every task state, project and person per list: My Work ~950 ms | related rows loaded by id: 140 ms |
| "Assignee or collaborator" as one OR | scanned every task in the visible projects (123,543 rows) for 202 | two index-friendly halves: My Work 58 ms |
| My Work returned every routed attention item | 3,713 items, 4 MB for a PM of the two large projects | the 100 most urgent plus the total; the project dashboards list the rest |
| Contains-search without trigram indexes on tasks, deliverables, milestones | 124 ms alone, seconds under load | GIN trigram indexes: 43 ms |

Environment findings (not product defects): Docker's default 64 MB `/dev/shm` breaks parallel queries under load (compose
file now asks for 256 MB); the development database allows 100 connections in total.

## Accessibility (FR-002, FR-003, US2)

axe-core WCAG 2.0/2.1 A and AA rules on every screen, as Alex (team member), Priya (PM) and Jordan (administrator),
including item panels, the New Item, New Project, New Event, New Task and workspace dialogs, and Edit Dashboard.

| Finding (rule) | Where | Fix |
|---|---|---|
| nested-interactive | board cards (workspace and project boards) | pointer drag on the card, keyboard drag and ARIA on a named handle; Enter on the card's buttons now opens them |
| nested-interactive | milestone strip (`role="img"` over buttons) | a labelled group; each diamond named with key, date and status |
| aria-prohibited-attr | calendar all-day cells | labelled groups |
| aria-allowed-attr | comment box (`aria-expanded` on a text box) | removed; `aria-controls` only while suggestions show |
| scrollable-region-focusable | activity and report tables (text only) | focusable labelled regions |
| color-contrast | workload capacity figures at 70 % opacity | full-strength text |
| lint: empty header cells, combobox without controls | tables, people picker | screen-reader labels; the picker is a button with its popup |

After the fixes every audited screen reports no violations. Reduced motion and increased contrast follow the operating
system (existing CSS); forced-colours mode now keeps a visible focus outline. Keyboard: board card picked up with Space,
announced, and cancelled with Escape. Not done: the manual audit with screen readers, and Edge, Firefox and iPad Safari
runs (FR-003) — they need the people and devices of the pre-GA audit.

## Security readiness (US4)

See `docs/security/threat-model.md`: 18 threats with mitigations. Fixed in this packet: CSV formula injection (T-05) and
the analyser finding (T-14). Open for decision: migrations as the admin group so the app role cannot drop the log
triggers (T-09), mail-secret rotation schedule (T-12), Graph mailbox policy (T-16), penetration test (T-18).

## Operations (FR-004..FR-007)

Admin → Operations shows all clear and each job's last run on the development instance (checked in the browser).
Runbooks: `docs/runbooks/jobs.md` (alerts and first responses), `restore.md` (point-in-time restore, verification, drill
procedure and record), `environments.md` (rebuild from definitions).

## Final audit (2026-09-25)

| Check | Result |
|---|---|
| §25.10: a 429 carries `Retry-After` (temporary instance at 3 requests a minute: the fourth and fifth answer 429 with `Retry-After: 60`) | Pass |
| §25.10: `IdempotencyTests` — the same key replays the first 201 with `Idempotent-Replayed`, one task is made, the key on another path is refused (422), keys are per person; replays are pruned after two days | Pass |
| axe (WCAG 2.1 AA) on every screen: 46 routes as Admin (29 workspace and admin, 17 project tabs) including Phase 2 registers, meetings, templates and holidays, and the main routes again as PM, team member and read-only | Pass after two fixes: the empty week grid is now keyboard-scrollable; read-only people no longer see "New task" on My Work or "Add time" |

## Not done here (organisational)

| Item | Prepared |
|---|---|
| Run the 8-week pilot and record go/no-go (FR-001, US1) | `docs/pilot/pilot-plan.md`, Reports → Pilot measures |
| Threat-model walkthrough before the pilot (FR-011) | `docs/security/threat-model.md` with a walkthrough record |
| Restore drill in Azure before go-live and quarterly (FR-005) | `docs/runbooks/restore.md` with a drill record |
| Availability 99.5 % (FR-004) | measured after go-live from `/health` and request telemetry |
| Deploy alert rules (needs an operator address and approval) | `operatorEmail` in `infra/env/<env>.bicepparam` |
| Penetration test if required | T-18 |

## Decisions

- My Work's attention section shows the 100 most urgent items and says how many there are; the complete lists stay on
  each project's dashboard.
- Job records are kept 90 days; the activity log is the permanent audit trail.


## Homedev shared field correction — 2026-10-03 UTC

The actual `02ca7cd` review at `pm.engcalchub.com` exposed clipped people Clear controls and a clipped native Start date editor at narrow widths. Shared picker sizing and responsive FieldRow layout now pass the existing Chromium TaskSheet regression at 1440, 390 and 320px. Native Start/Due date editors are associated with the existing visible field labels; the name assertion failed before the fix and passes after it. All six date/viewport combinations preserve exact opener focus and cancel without task writes. Frontend build, focused lint and bounded independent review passed. These are local mocked-API checks; hosted correction and manual screen-reader/device acceptance remain unproven. Current deployment and recovery evidence is in the [combined checkpoint](../../docs/reviews/2026-10-02-combined-candidate.md).


## Approved Paper landing hero — 2026-10-03

Jay approved the Paper hero before requesting integration into main. The public landing route now uses the approved Terrace mark, centered serif heading, original engineering margin sketches and straight fictional Task Board preview. Design exploration controls stay in the separate prototype. Both public sign-in links enter the existing `/login` flow; the `LoginPage` implementation and authentication/security modules are unchanged.

The decorative app preview is a script-free SVG DOM snapshot with a fixed 1728 × 873 viewport, unchanged captured app CSS and a JPEG image-error fallback. No live API, active links or embedded frames are included. Font files and their SIL Open Font License notices are served locally. Provenance, hashes and fictional-data boundaries are recorded in `web/public/landing/`.

| Check | Result |
|---|---|
| Frontend TypeScript and production build | PASS; existing Vite/config, generated-selector and bundle-size warnings remain |
| Frontend lint | PASS with existing repository warnings; no findings in the new hero component |
| Spec traceability | PASS: 612 IDs, 236 sections, no missing or unknown references |
| `git diff --check` | PASS |
| Static SVG XML, no script/frame/external DOM destinations, asset and font hashes | PASS |
| Existing `LoginPage` implementation compared with base | PASS; byte-for-byte unchanged |
| Built page in native Chrome under the existing API CSP and framing headers | PASS; sharp static preview rendered, no console warning/error |
| Public Sign in and unauthenticated project deep link | PASS; both show the existing development login in the isolated fixture |
| Preview dialog keyboard behavior | PASS; named dialog, close focus, Escape dismissal and opener focus return |
| Weekly planning tabs | PASS; ArrowRight and Home change the selected tab, associated panel and fictional capacity total |
| Responsive geometry at 320, 375, 768 and 1440 pixels | PASS; no document horizontal overflow, heading and primary action within viewport, preview image loaded |

Browser checks used a local production frontend build and an isolated fictional GET API, with the existing security headers applied. They do not establish production Entra sign-in, hosting, manual screen-reader acceptance, Safari/Firefox behavior or deployment. Automated backend and browser regression coverage remains the PR CI gate.
