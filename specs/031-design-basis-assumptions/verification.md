# Verification: Shared Design Basis and Assumptions

**Date**: 2026-09-26
**State**: Domain and additive schema implemented. API, UI and product acceptance remain open.

Documentation validation is recorded in `docs/coordination-spec-validation.md`. Product acceptance scenarios AC-BAS-01, AC-BAS-02, AC-BAS-03, AC-BAS-04, AC-BAS-05 are **not run**. Implementation tasks remain unchecked until their full acceptance scope is met.

## First implementation increment — data and domain rules

The isolated review worktree now has an additive `DesignBasisSchema` migration for entries, immutable versions, exact-version consumer links, explicit conflicts and time-limited assumption dispositions. The model stores numeric values and units separately, source system/ID/link/declared revision, scope, owners and approval attribution. A database trigger rejects edits to confirmed version content and deletes of version history; the only later status transitions it permits are Confirmed to Superseded or Withdrawn. `dotnet ef migrations has-pending-model-changes --no-build` reported no model drift. Focused domain/schema tests passed 4/4 against an ephemeral PostgreSQL database, including a proposed numeric value without units, refusal to confirm it, successful confirmation once units and evidence were supplied, refusal to edit confirmed content, and retention after supersession.

This increment has no API or UI and does not prove cross-project relationship checks, owner/approver permissions, concurrent confirmation, automatic impact assessment, conflict display, assumption dispositions, source decisions, export, accessibility or any AC-BAS end-to-end scenario. Packet 031 remains **UNPROVEN** for product acceptance.

## Project API increment

A second additive migration adds the per-project B key counter and linked basis impact assessments. Project-scoped API commands create entries, propose replacements, confirm with source evidence/rationale, link exact versions to task/deliverable consumers, and authorise a time-limited Proceed under Assumption disposition. Confirmation of B retains consumers on A and creates a pending assessment for each use. A second confirmed conflicting value creates an explicit unresolved conflict; duplicate creation requires the caller to acknowledge the existing entry ID. The detail query exposes source-missing flags, version history, uses, impacts and conflicts. API write commands use project transactions, request IDs and expected row versions; owner and Discipline Lead actions use the existing permission matrix.

The focused `DesignBasisTests|DesignBasisSchemaTests|DesignBasisApiTests` set passed 6/6 against ephemeral PostgreSQL, including AC-BAS-01's exact-version/assessment invariant, AC-BAS-02 numeric units, AC-BAS-03 provisional disposition, and AC-BAS-04 conflict persistence. It also checked restricted-project 404, a refused PM consumer signature, a repeated confirmation request, and stale confirmation conflict. `dotnet ef migrations has-pending-model-changes --no-build` reported no model drift. These are API and schema slices of the acceptance criteria. Assessment disposition/adoption, withdrawal and source-decision reopen, UI/register filtering/export, template copying, cross-project negative references, simultaneous confirmation, browser and accessibility still need work; AC-BAS-01 through 05 remain unaccepted as full product scenarios.

## Local review register increment

The project now has a Design basis tab with scoped kind/status/discipline filters, current versus proposed status, version/source history, explicit conflict values, consuming work links, pending assessments and forms for proposals, confirmation, Proceed under Assumption and consumer links. A duplicate response identifies the existing entry and requires an explicit inspection acknowledgement before a separate entry can be saved. In the local synthetic browser, Taylor created `DEMO-101-B001` as a clearly labelled utility-depth assumption; the detail showed Proposed and missing source metadata, and the entry survived reload in the persistent `hub_review_local` database. The status filter selected Proposed, and an absent confirmed version now says Not confirmed. The local database was privately dumped before its two additive migrations were applied; no restore drill was done. Focused PostgreSQL API tests passed 2/2 after a status-filter fix that excludes historical Superseded versions. Frontend production build and lint passed, with pre-existing warnings. This does not prove a confirmed browser workflow, reviewer approval, multiple browser sizes, pointer operation or accessibility.

## Consumer assessment increment

The assessment command now requires expected row versions for the assessment, recorded use, new basis version and consuming work. A named consumer may adopt the confirmed replacement; an authorised coordinator independent of the consumer may record Unaffected with a rationale and evidence link. Adoption adds a new exact-version use while retaining the old use. The detail response marks current and historical uses, and the register highlights current work still using a superseded version. A second proposal is held while assessments for the current version remain pending. Confirmation of a later version assesses every current consumer use in the entry, including a consumer that was explicitly marked Unaffected by the prior change.

Focused PostgreSQL tests passed 6/6 for the design-basis classes after recompilation. The API test covers adoption, an Unaffected decision, forbidden actors, stale target version conflict, idempotent retry, retained history, and the A-to-C assessment for a consumer still using A. Frontend production build and lint passed; lint reported existing warnings outside the changed screen. Browser assessment operation and full packet acceptance remain **UNPROVEN**. In particular, source-decision reopening, withdrawal, template copies and shared notification/readiness obligations remain open.

## Filtered register and export increment

The register now filters by scope, overdue proposed confirmation and linked task/deliverable as well as the earlier kind, status and discipline controls. A project-scoped CSV/XLSX export uses the same entry filter query and emits version rows with units, source system, stable source ID, source URL, declared revision and due date. The export retains historical versions for each matched entry. Focused PostgreSQL tests passed 6/6 after recompilation; the API test exercised matching and nonmatching filters, overdue detection, CSV provenance and a restricted-project 404. Frontend production build and lint passed, with existing warnings. In the local synthetic browser, the scope filter showed the synthetic DEMO-101 assumption for “site servicing” and an empty register for “bridge”; the export menu exposed CSV and XLSX. A browser download was invoked but its saved file was not inspected, so the browser export is **UNPROVEN**. Complete product acceptance, including multiple browser sizes and export parity for every filter, remains open.

## Withdrawal, decision, conflict and template increment

Two further additive migrations retain withdrawal assessment provenance and template basis suggestions. Withdrawing a confirmed current version retains historical uses and creates pending consumer assessments. Reopening a linked source decision also creates an assessment; neither action silently changes the consumer's recorded version. Conflict resolution now requires a confirmed replacement that explicitly supersedes one competing version, preserves both histories, and checks the conflict's actual row version. Template suggestions copy into new projects only as Proposed, without inherited confirmation. The register exposes withdrawal and impact decisions. The combined focused PostgreSQL set for design basis, templates, readiness and handoffs passed 36/36; EF reported no pending model changes; the frontend production build passed with warnings. The changed UI flows and migrations have not yet been exercised in a deployed browser or persistent review database. Full permission, concurrency, accessibility and packet acceptance remain **UNPROVEN**.

## Member proposal and owner assignment increment

An active Team Member may create a Proposed basis only in their primary project discipline, with themselves as initial owner and no self-appointed independent approver. A PM or the responsible Discipline Lead may assign the owner and independent approver of that unconfirmed proposal with a reason and expected entry version. The register exposes the member proposal and manager assignment forms. Confirmation remains limited to the responsible lead or appointed independent approver and retains the self-review check.

The focused PostgreSQL API test passed 1/1 on `29a47dc`, covering permitted Civil member creation, refusal for another discipline, another owner or an appointed approver, refusal of member and unrelated-lead reassignment, responsible-lead reassignment, and stale-version refusal. Frontend production build and lint passed; lint retained existing warnings. CI passed on exact commit `29a47dc` in run `36382618962`. This is one permission slice, not the full permission matrix or deployed browser acceptance.

## Proposed-version correction increment — 2026-10-01

The local browser exposed a correction gap: a numeric Proposed criterion without units was rightly refused at confirmation, but its creator had no supported way to add units. An owner or authorised PM/discipline lead can now edit an unused Proposed version with entry and version row checks, a reason, an idempotent request ID and audit history. Edits are refused after confirmation or once the proposal has a consumer or Proceed under Assumption disposition, so approved uses cannot silently change. A changed proposal invalidates affected unissued submission checks.

The focused PostgreSQL DesignBasisApiTests passed 6/6. They cover owner edit, restricted-project and unrelated-actor refusal, idempotent retry, stale edit refusal, confirmed immutability and refusal to alter a provisionally approved and consumed assumption. The frontend production build and lint passed. In local Chrome against persistent synthetic `hub_review_local`, Jay corrected `DEMO-101-B002` from a unitless numeric value to `1.5 m`, confirmed version 1, then reloaded: the value and Confirmed state persisted and Edit proposal was absent. The browser reported no JavaScript errors. This is local Development-auth evidence; deployed password sign-in, wider permission/concurrency cases, accessibility and full AC-BAS acceptance remain open.

The next diff review found that the browser draft omitted `decisionId` and sent null for every edit or replacement. The draft now retains that source relationship. After frontend build/lint passed, a local Chrome/API regression used synthetic `DEMO-101-B003`: correcting its units preserved the source decision, confirmation retained the link, and proposing version 2 preserved the same decision while version 1 stayed Confirmed. Persistent API reads verified both links, with no browser JavaScript errors. This proves the exercised local relationship-preservation paths only.

Each version with a source decision now exposes a Source decision link. After frontend build/lint passed, local Chrome followed B003's link to the exact synthetic decision panel and displayed its expected heading, with no JavaScript errors. Full decision/basis acceptance and deployed navigation remain unproven.

## Duplicate scope correction increment

Creation, Proposed-version edits and replacement proposals now share a deterministic, project-scoped duplicate check. An edit or replacement that matches another entry's kind, title, discipline and non-withdrawn scope requires explicit inspection of that existing entry. The browser shows its link and inspection acknowledgement for all three forms. Commands retain project locking, version checks and idempotent request receipts; refusal saves no partial edit or extra version. No migration is required.

Focused PostgreSQL DesignBasisApiTests passed 7/7, including refused edit without mutation, acknowledged edit, refused replacement without a new version, and acknowledged replacement. Frontend build/lint and diff checks passed. In local Chrome/API, synthetic B004's colliding scope edit showed B003's link, kept the original persisted scope on refusal, disabled Save until acknowledgement and then persisted the acknowledged scope. No JavaScript errors or axe violations were found; one colour-contrast check was inconclusive. These are local synthetic checks; deployed acceptance and the remaining full packet scenarios are open.

## Source-decision selection increment

Create, edit and replacement forms now offer an optional Source decision selector populated from the existing permission-scoped project decision register. Existing links remain in the draft while options load or fail. Selecting a decision records a relationship without transferring approval. Frontend build/lint passed. Local Chrome selected the synthetic source decision for B001, saved and reloaded; the persistent API retained the decision ID and the assumption remained Proposed. No JavaScript errors occurred. The first axe scan had an inconclusive contrast check; a repeated settled-form scan had zero violations and zero incomplete checks.

Focused PostgreSQL DesignBasisApiTests passed 7/7 with added negative source-reference coverage: creation and proposal edits refused another project's decision, creation left no entry and the refused edit retained its original source decision. This is local synthetic evidence; deployed sign-in, complete permission/lifecycle/concurrency coverage and full packet acceptance remain open.

## Design basis notifications — 2026-10-01

Two catalogue events, App on and Email off by default; Admin defaults and per-user preferences still apply. `BasisImpactPending` is a direct event. It goes to consumer owners whose impact assessment is created when a replacement is confirmed (FR-BAS-03, AC-BAS-01), when the current version is withdrawn (FR-BAS-04), or when a linked source decision is reopened from Decided to Pending (FR-BAS-05). `BasisConflictRaised` goes to the owners of both entries when confirmation records a new unresolved conflict (FR-BAS-06, AC-BAS-04). Each command sends one notice per recipient and item, however many assessments it creates for that person (FR-MDC-06, §17.5). Basis commands queue the notice inside the `Coordination.Run` transaction. The decision reopen queues it in the same `SaveChanges` as the assessment. Refused, stale or replayed commands therefore add none, and the actor never receives one. Both events join the project-scoped set, so recipient access is rechecked when the notice is composed and again when queued email is delivered (FR-MDC-02, FR-MDC-03). §17.2 has no design-basis rows, so these recipients and defaults are authored choices.

**PASS (local):** `dotnet build Hub.slnx` succeeded. `dotnet test --filter "DesignBasis|Readiness|Decision"` passed 48/48, including the new `DesignBasisNotificationTests` at 4/4. The new tests show:
- A consumer with two affected uses receives one notice. The confirming lead receives none.
- A 409 stale confirm, a 403 withdrawal and a replayed confirm add no notice. A rejected repeat of the decision reopen also adds none.
- A consumer removed from a restricted project still gets an assessment but no notice.
- A conflict notifies both entry owners and not the actor.

`tools/trace_spec.py --check`: 0 not cited. `npm --prefix web run build` and `lint` passed.

**UNPROVEN:** email delivery through the worker, digest inclusion and how the notices look in the browser. Entry creation, owner/approver assignment, proposals, edits, Proceed under Assumption, impact decisions and conflict resolution send no notice.

## Browser acceptance rehearsal — 2026-10-01

Isolated worktree `codex/pm-verify-031` at `5325e6c`. The frontend production build was served by Hub.Api with Development auth on 127.0.0.1:5095, against a fresh `hub_agent_verify` database seeded with `Seed__ReviewDemo=true` and dropped afterwards. Tests ran in real Chrome through Playwright, with no API mocks. Each synthetic identity had its own browser context: Yagmur (Civil Team Member), Jay (Civil Discipline Lead), Taylor (PM), Alex (non-member with no role) and Rita (Read Only). Setup used the API and is not under test: two Civil tasks for Yagmur (T0003, T0004) and a decided decision, DEC01.

- **PASS:** Yagmur proposed B001, a numeric 1.5 m criterion with source metadata, and Jay confirmed it. Taylor (T0002) and Yagmur (T0003) then linked exact-version uses. Each person could choose only their own work as a target.
- **PASS AC-BAS-01:** Taylor proposed v2 (1.8 m, rev B) and Jay confirmed it. Both uses stayed on version 1 and showed "Uses superseded version". Two 1 → 2 assessments were pending.
- **PASS notifications:** Taylor and Yagmur each received exactly one BasisImpactPending notice, and the confirming lead received none. The bell read "Notifications, 1 unread". The notification centre showed "Design basis DEMO-101-B001 was replaced; assess the impact on your work". Opening the notice lands on the register, not on the entry.
- **FAIL consumer decision in the browser:** no "Decide impact" control renders for any assessment. `web/src/pages/projects/DesignBasis.tsx:230` compares `i.status === 'Pending'`, but the API returns `Pending Assessment` (`AssessmentStatus.Pending`). When each consumer adopted through the API instead (200), the browser showed Resolved, with version 2 as the current use and version 1 kept as a historical use.
- **PASS withdrawal:** withdrawing the current v2 created two pending "2 → Withdrawn" assessments and kept the exact v2 uses. The register showed "Not confirmed". Taylor received a new notice. Yagmur's notice was merged into her unread one ("…withdrawn… (2 changes)"), which is the designed per-item collapse. The API refused the consumer's own Unaffected decision and an Adopt with no replacement (400). Jay recorded Unaffected through the API. **FAIL FR-BAS-04:** the current use of a withdrawn version is not flagged; only Superseded is (`DesignBasis.tsx:225-226`).
- **PASS FR-BAS-05:** Yagmur created B002, a narrative criterion with source decision DEC01. It was confirmed without units, which covers the narrative case of AC-BAS-02. Taylor reopened DEC01 from the Decisions panel. Only Yagmur received "Decision DEMO-101-DEC01 was reopened…", and she saw "Pending Assessment · Yagmur · Version 1 → Source decision reopened" while B002 stayed Confirmed.
- **PASS FR-BAS-06 and AC-BAS-04:** a second entry with the same title, scope and discipline enabled Save only after the user inspected the linked existing entry. After confirmation, 10 years and 5 years both stayed Confirmed. Both values were shown, both rows had a conflict count of 1, and both owners were notified but the actor was not. An acknowledged 5-year replacement raised no new conflict. The authorised resolution cleared both counts and kept the superseded 10-year version.
- **Export parity:** the CSV downloaded in the browser matched the keys in the filtered register, with one row per version and the units and provenance columns. This held for seven filter sets: no filter, Confirmed, Withdrawn, Assumption, "storm" scope with Criterion and Civil, overdue only, and an empty "bridge" scope. **FAIL affected work:** the select sends `Task:<id>` (built by `workChoices` in `DesignBasis.tsx:81-82`) to `Guid? AffectedWorkId` (`src/Hub.Api/Features/DesignBasis.cs:35`). The list and the export both return 400 and display the raw binding error. With a bare GUID, the API filter works.
- **Permissions:** Alex and Rita saw no New basis entry button and no action controls. All six of Rita's direct commands returned 403 Read Only. Alex received 403 for create, propose, withdraw, proceed and linking another person's work. **FAIL:** an unauthorised confirm returned 400 validation (`approverId`) instead of the 403 that §25 requires (`DesignBasis.cs:217-218`). Yagmur's confirm also returned 400. Refused commands changed nothing. The text of Alex's create refusal is missing the discipline name ("the  lead").
- **PASS stale edits:** two contexts edited B005. The second save was refused with "This record changed. Refresh and review it before trying again." The first edit persisted and the second user's draft stayed in the form. A Confirm form kept open across another person's edit was refused with the refresh guidance, and nothing was confirmed. Separately, linking work to an unapproved Proposed assumption was refused with "An unconfirmed assumption needs an approved, unexpired disposition before use." This is partial evidence for AC-BAS-03.
- **axe and browser errors:** axe WCAG 2.1 A/AA found 0 violations across 22 settled states (register, forms and detail). The incomplete results were all colour contrast (overlapping dialog layers and icon glyphs), plus one aria-hidden-focus in the decision reopen dialog. In the browser, the decision panel deep link requests `/api/v1/projects//external-parties` (404) before the decision loads. Closing a dialog opened from state (New basis entry, basis detail) with Escape leaves focus on `<body>`. The other errors were the expected 409 and 400 responses above.

Packet 031 remains **UNPROVEN** for product acceptance. The impact-decision control, the affected-work filter and the withdrawn-use highlight need fixes and a rerun. Not run: email and digest delivery, restricted-project and lifecycle-status repeats, Proceed under Assumption in the browser, template copy, assistive technology and other viewport sizes.

## Template basis suggestions UI

**Date**: 2026-10-01 · branch `codex/pm-tmpl`, parent `70e47bd`

The template page now lists a version's design basis suggestions: kind, discipline, scope, value and units, and source. Admins and Template Editors can add one to a Draft through a form. Add is disabled while structure edits are unsaved, because the refresh after adding would drop them. Published and retired versions, and non-editors, see the list read-only. The template read returns `basisSuggestions` and each discipline's `templateDisciplineId`, which the add endpoint takes; Save ignores the extra field. The create-from-template summary counts the suggestions for ticked disciplines. It states that they are copied as Proposed, not confirmed, not approved and not linked to work, and how many are left out with unticked disciplines, as the server does (FR-BAS-07, AC-BAS-05). The API has no edit or remove for a suggestion, so the UI offers neither.

**PASS (local):** `dotnet build Hub.slnx` had 0 errors. `dotnet test --filter FullyQualifiedName~TemplatesTests` passed 6/6. The new `Editors_add_basis_suggestions_to_drafts_and_the_template_lists_them` shows:
- A PM gets 403. An Admin gets 201 on a Draft.
- The read lists the suggestion with its discipline and units.
- Publishing makes the version refuse further suggestions with 400.
- The wizard's PM can read the suggestions, and the next Draft keeps them.

`npm --prefix web run build` passed. `lint` had 0 errors and 88 warnings, the same count as before the change.

The browser check used headless Chrome with Playwright, the API on port 5098 and database `hub_agent_tmpl`, which was dropped afterwards. As Admin, jordan started a Draft of Municipal Infrastructure Design v1:
- The form refused a numeric value without units before sending any request.
- Two suggestions were added: a Civil criterion of 5 years with source metadata, and a Transportation assumption.
- The Draft was published as v2 without a structure save. Add was absent on v2.

As PM, priya saw the list read-only with no Add, and her direct POST got 403. Her wizard summary showed 1 suggestion copied and 1 not copied; ticking Transportation changed it to 2 copied. The project was then created with the default disciplines. It had exactly one entry, B001: Proposed, with no current version, confirmation, decision, use or disposition. The value of 5 years and the source were kept. axe (wcag2a/2aa/21aa) found 0 violations on the add dialog, the editor page and wizard step 2. The project register also had 0 once the creation toast had faded; the only hit was colour contrast on that toast. There were no JavaScript errors. At 390 px the suggestion list wraps without overflow; the page's existing milestone, deliverable and task tables still overflow.

**FAIL (backend, not changed here):**
- Once a Draft has a suggestion, `PUT /templates/{id}/structure` and `DELETE /templates/{id}` return 500. The PostgreSQL error is 23503: `ClearChildren` deletes `template_discipline` rows that the RESTRICT foreign key from `template_design_basis` still references. `NewDraft` copies suggestions, so every later Draft of the family has the same fault. This was seen in the browser and through the API.
- `POST /templates/{id}/design-basis` with a numeric value and no units returns 500 from `ck_template_basis_numeric_units`; only the form prevents this.

**UNPROVEN:** deployed sign-in, concurrent edits, keyboard-only operation and full packet acceptance.


## 2026-10-01 Codex recovery checkpoint

Recovered template basis suggestions, draft preservation/remapping and validation, Pending Assessment actions, GUID filters, withdrawal visibility and focus restoration were integrated. The mocked design-basis browser regression passed with five list queries, one impact decision and zero unmocked GETs. A removed appointed approver no longer has confirmation authority on an open project (domain sweep 18/18). Deployed and company engineering acceptance remain UNPROVEN.


## Proposed assumption disposition and consuming-work forms — 2026-10-02

PASS locally on application code `7c412b0` / follow-up base `4950665`: a fresh synthetic database and real browser/API with Development authentication. A Proposed Site grading assumption was supplied through the API. Taylor then saved Proceed under Assumption through the form, naming Jay, Site grading, an explicit synthetic rationale and expiry 2026-10-09. The native date picker committed the expiry. Jay used the consuming-work form to link his own task to exact version 1. Reload and API assertions retained one current exact-version use and one scoped disposition attributed to Taylor; version 1 stayed Proposed, without a current confirmed head or confirmation attribution. Browser console errors: zero. Credential-free evidence: `/private/tmp/pm-basis-proposed-use-evidence.json`; screenshot: `/private/tmp/pm-basis-proposed-use.jpg`.

This completes the successful form path missing from the earlier AC-BAS-03 browser record. Existing API tests cover refusal without a disposition and invalid scope/expiry. The template-copy browser/API proof above covers AC-BAS-05; recovered template fixes are included in the merged review source. These are local product slices, not hosted password sign-in, readiness-exception approval, engineering confirmation, assistive-technology or company pilot acceptance.


## FR-MDC-06 register completion — 2026-10-03

Corrected the pager helper that deleted its own page parameter, retaining page changes and resetting on filter changes. Added existing saved-view controls and exact B-key/scoped title search. The extended Chromium harness reaches page two, resets on Kind change and retains prior impact decisions, withdrawn-version warning, focus return and TaskSheet date/Clear controls at 1440/390/320px.

The combined increment passed 27 focused PostgreSQL cases across the final runs, frontend build/lint and the extended existing Chromium harness. This is local synthetic implementation evidence; the new exact-head CI, merge, image, homedev activation and hosted checks are separate gates in [the combined checkpoint](../../docs/reviews/2026-10-02-combined-candidate.md). Packet T006 remains open for full acceptance, and no company pilot or production acceptance is inferred.


## Completion correction checkpoint — 2026-10-03

See [the completion audit](../../docs/reviews/2026-10-03-completion-audit.md) for exact implementation commits, focused regression evidence and remaining combined/hosted gates. The earlier `26c0ae1` candidate is held. This checkpoint does not close T006 or claim final hosted/company acceptance; live review remains `02ca7cd` pending activation of the corrected candidate.

## Hosted unresolved-conflict/current-use slice — 2026-10-03

On executable `ac494a8ba70d50517f540ad7e0acced85d84897e`, two new independently confirmed fictional narrative assumptions in `SYNTH-GATES-1003` retained their own current confirmed versions and one shared unresolved conflict. The existing exact A use stayed unchanged when B was confirmed, and a second consumer explicitly linked B. Both consumers' derived Basis checks were applicable and unsatisfied; their whole assessment states were Needs Assessment while other inputs remained unknown. Task dates and completion stayed unchanged. The signed-in public register and inspector displayed both conflicting values, the original current use and one conflict on each entry after full reload.

This is PASS for a bounded AC-BAS-04 slice, not replacement/adoption, template copy, complete readiness, assistive technology or company engineering acceptance. Credential-free evidence is `pm-tool/data/hosted-basis-conflict-ac494a8b.json`. See the [hosted acceptance checkpoint](../../docs/reviews/2026-10-03-hosted-acceptance-ac494a8b.md); T006 remains open.

A separate hosted keyboard slice passed Enter activation, Tab wrapping inside the inspector, and Escape close with focus restored to its original entry button. No record was changed. Full form and assistive-technology acceptance remain UNPROVEN.


## Template provisional confirmation boundary — 2026-10-03

PASS locally: the existing template-copy test now proves that an independently appointed
approver receives 400 on confirmation while the copied Proposed version's project
confirmation date is unknown. Persisted version, confirmation attribution and current
head remain unchanged. The owner then supplies an explicit project-specific date;
independent confirmation succeeds and retains that date and attribution. This extends
regression coverage of existing guards; no API/UI behaviour changed and no date was
invented for template-derived operational data.

`TemplatesTests` passed 7/7 with zero failures/skips against isolated branch base
`4b853602`; independent changed-test review found no actionable findings. Final commit
CI/merge and hosted redesigned acceptance are separate gates. Jay requested committing
completed work and recording the remainder before the incoming redesign; see the
[checkpoint](../../docs/handoffs/2026-10-03-redesign-checkpoint.md). T006 remains open.
