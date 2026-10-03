# Verification: Location-Linked Coordination Issues

**Date**: 2026-09-26
**State**: Backend and scoped UI slices implemented in the isolated review worktree. Deployed acceptance and pilot execution remain unproven.

Documentation validation is recorded in `docs/coordination-spec-validation.md`. AC-LOC-01 through AC-LOC-04 have focused API evidence; AC-LOC-05 has pure-rule coverage. Deployed UI, export reconciliation, notifications and browser flows remain unproven.

## Executed evidence

- PASS — `dotnet build src/Hub.Api/Hub.Api.csproj --no-restore` on the isolated review worktree.
- PASS — `dotnet ef migrations has-pending-model-changes --project src/Hub.Api/Hub.Api.csproj --startup-project src/Hub.Api/Hub.Api.csproj`: no pending model changes after `20260928010417_IssueMetadataIssueVersion`.
- PASS — `dotnet test tests/Hub.Tests/Hub.Tests.csproj --no-restore --filter FullyQualifiedName~LocationIssue`: 4 tests passed after integration review, including unversioned/stale issue writes, non-self or unappointed verification, a non-owner appointed verifier, latest rejection after verification, stale verification after a new reference, equal-clock ordering, and unavailable evidence.
- PASS — domain station/coordinate validation tests are included in `LocationIssueRulesTests`.
- PASS — additive migration creates `issue_location`, `issue_document_reference` and `issue_verification` with project/issue/user foreign keys, kind/status checks, station-order check and document identity uniqueness.
- PASS — additive issue-version migration persists the parent issue version on each metadata record; review DB migration applied successfully.

## Remaining evidence

- PASS — scoped issue location, document reference and appointed-verifier forms built with the frontend production build. API guards require a site area or building for a location.
- PASS — local synthetic browser on port 5084: Taylor raised DEMO-101-I01, saved a site area and drawing revision, appointed Yagmur, and all records survived reload. Yagmur signed in separately through Development auth, saw the issue without owner actions, and recorded Verified with a synthetic evidence link. An initial attempt to appoint Marc failed because he was outside DEMO-101; the picker was then changed to use the same eligible project people as the API. Frontend build and lint passed after that correction. This is local synthetic evidence, not the deployed individual-password flow.
- UNPROVEN — deployed browser operation and keyboard/accessibility. The register/export increment below has local evidence, with deployed reconciliation still open.
- PASS — local PostgreSQL AC-LOC-03: a published revision created a pending check for the resolved issue while retaining revision A; an unrelated source with the same identifier and revision was excluded by its different source URL. Owner and verifier each recorded a disposition; same-request retry was idempotent and an unrelated member was forbidden. The decision command uses the project lock, row version and atomic command log. `dotnet test --filter 'FullyQualifiedName~ReviewChangeTests|FullyQualifiedName~LocationIssueTests'` passed 15/15. EF reported no pending model changes after rebuilding. The issue detail UI has a two-person decision form; TypeScript and lint passed with existing warnings.
- UNPROVEN — deployed AC-LOC-03 browser flow, notification delivery and concurrent finalisation under separate live requests.
- PASS — local PostgreSQL AC-LOC-04: a review finding links an existing issue ID; the current-round discipline view returns that same ID and owner, with one issue row. Editing its owner produced exactly one Issue activity entry, and both the review link and discipline projection kept the same ID. Cross-project linkage and Read Only creation were refused, a stale owner edit conflicted, and a published source replacement carried the issue link into the new review round. The additive `ReviewFindingIssueLink` migration has no pending model changes. The review finding form and discipline view now link to the existing issue panel; frontend production build passed. Deployed browser behavior is UNPROVEN.
- UNPROVEN — deployed review environment, homedev browser-to-API flow, backup/restore, real tenant sign-in, pilot and production acceptance.

## Register increment

The project issue list API and CSV/XLSX export now include structured location, document/revision/availability and latest verification summaries. The register displays location and verification status by default and offers document references as a selectable column. A focused PostgreSQL test checked the CSV headers, location, revision and Verified status; the combined set passed 38/38. Location/document/revision/verification filtering and grouping, deployed browser/export reconciliation and full packet acceptance remain UNPROVEN.

## Source filtering increment

Project issue list and export now share server filters for structured location, document ID, revision and effective verification status, including no verification. A verification becomes Needs reverification when a later location or document reference supersedes its recorded issue version; a stale Verified record is excluded from the Verified filter. The browser register exposes the same filters in its URL and forwards them to export. The focused `LocationIssueTests` passed 2/2 and the issue register/report consumer set passed 11/11 against PostgreSQL; frontend build and lint passed with existing warnings. The test checked the same issue ID under each filter, retained earlier drawing references in a revision-filtered export, and matched list/export results for stale versus current verification. In a temporary local Development-auth browser on port 5085, synthetic Taylor saw DEMO-101-I01 with its site area and Verified status; a matching location URL filter retained it, a nonmatch showed the empty state, and Verified versus None selected the correct rows. The CSV menu was invoked, but the browser download could not be inspected; deployed browser/export reconciliation remains UNPROVEN. At that stage grouping and station-number queries had not been implemented; the next increment records their local evidence.

## Station and grouping increment

The issue list and export now share alignment, station-from, station-to and station-units filters. Station intervals use inclusive overlap without converting coordinates or units; a reversed query range is rejected. A focused PostgreSQL test matched an overlapping range, excluded a nonoverlap, rejected reversal and found the same issue key in CSV. The browser register has these filters and URL-persisted grouping by location summary, discipline, owner, verification status, document identifier and revision. An issue with multiple document references appears under each applicable group with the same issue ID. Each group has its own table body and rowgroup heading; the active table sort is preserved within groups. The affected readiness, handoff and location PostgreSQL set passed 33/33 and the full local suite passed 432/432; a focused assertion for the document/revision group data passed 1/1 afterward. The frontend production build/lint and `ff86cc5` CI passed. Deployed browser grouping, multiple-location grouping semantics, keyboard review and full packet acceptance remain UNPROVEN.

In a later local Chrome session against the persistent synthetic API, Taylor grouped DEMO-101-I01 by drawing (`SYN-DWG-01`) and revision (`A`), reloaded with the revision group preserved in the URL, and downloaded a real CSV containing the same issue ID. There were zero browser JavaScript errors. The populated grouped register had ten column headers, one rowgroup header and ten cells in its one data row. A focused axe WCAG 2.1 A/AA scan reported zero violations but two **incomplete** rules: contrast on decorative `aria-hidden` symbols and table-header association for the rowgroup layout. Manual assistive-technology and keyboard acceptance remain **UNPROVEN**. Headless Chrome did not change even a plain native test select with ArrowDown/Enter, so that automation result is not evidence of an app keyboard defect.

## Independent verifier appointment guard

A later permission audit found that a non-creator issue owner or discipline lead could appoint the independent verifier through the general register edit permission. A Proposed appointment or replacement now requires the issue creator or a project PM, with a reason; the appointed independent verifier remains the only person who can submit Verified or Rejected evidence. A focused PostgreSQL test passed for denied owner/lead attempts and allowed creator/PM appointments. An additional focused run passed for denied owner replacement and allowed PM replacement. The integrated local suite passed 434/434 before that test extension. CI for this increment, deployed browser behavior and full packet acceptance remain UNPROVEN.

## Verifier notification increment

Appointing an independent verifier now creates an actionable in-app assignment notice for that verifier. Recording Verified or Rejected creates an outcome notice for the resolution owner, creator and PM, excluding the actor and duplicate recipients. These events use the existing per-user preferences, project access recheck and delivery access scope; assignment bypasses a muted follow while an outcome respects it. Verification record, issue version bump, audit event and notices commit in one database transaction. Concurrent submissions using the same issue version yielded one Created response, one Conflict and exactly one outcome notice; a rejected repeat also created no second notice. With a restricted project, a removed owner received no outcome notice, and queued verifier email was suppressed after the verifier lost access. The focused `LocationIssueTests` PostgreSQL set passed 4/4, the combined suite passed 440/440 before the final concurrency/privacy test additions, and the frontend build/lint passed with existing warnings. Complete deployed delivery, digest behavior and accessibility remain **UNPROVEN**.

## Affected disciplines and multi-location grouping (2026-10-01)

FR-LOC-03 affected disciplines are now an additive `issue_affected_discipline` relation (migration `20261001205406_IssueAffectedDiscipline`; unique per issue and project discipline, restrict foreign keys to project, issue and project discipline; no existing rows changed). Issues accept `affectedDisciplineIds` on create and through the existing issue PATCH, so the existing issue edit permission, If-Match row version and audit apply; a relation-only edit bumps the issue version, and each added or removed discipline writes its own activity entry. New affected disciplines must be active disciplines of the same project; a linked discipline that is later deactivated stays, and removing a project discipline with such links deactivates it instead. The issue list, detail and CSV/XLSX export carry the affected disciplines, and the register discipline filter, discipline coordination view and review linked-issue view match the primary or any affected discipline. FR-LOC-05: the register now groups by each structured location, and by primary plus affected discipline, so a multi-location issue appears once under each group with the same issue ID, matching document and revision grouping.

- PASS — `dotnet build Hub.slnx`; `dotnet ef migrations has-pending-model-changes`: no changes after the new migration.
- PASS — PostgreSQL `LocationIssueTests` 5/5, including the new case: the Civil and Electrical leads both see the same issue key with both location labels; foreign-project discipline refused on create and edit; Read Only and affected-discipline lead edits refused (affected disciplines do not grant edit rights); stale version conflicts; removal audited; the restricted project hides the issue from a non-member. Focused `LocationIssueTests|RegistersApiTests|SchemaTests` 12/12; full local suite 445/445.
- PASS — `python3 tools/trace_spec.py --check`; frontend production build and lint (exit 0, existing warnings only).
- PASS — local synthetic Chrome (Development auth, throwaway DB, since dropped): DEMO-101-I01 with two site areas appeared under both location groups, and under Civil and Project Management when grouped by discipline; the `group` URL survived reload; the affected-discipline checkbox in the issue panel saved and reloaded with no JavaScript errors.
- UNPROVEN — notifications to affected disciplines, dashboard discipline counts (still primary discipline only), keyboard/assistive-technology review and deployed behaviour. The Coordination issue type and its required-reference rule await a product decision.

## Coordination issue type and reference audit (2026-10-01)

Per Jay's FR-LOC-01 decision, issues now carry an explicit type, General or Coordination, chosen at creation or when a risk is realised (default General). Migration `20261001211638_CoordinationIssueType` adds `issue.issue_type` (default General) with a check constraint, then marks every existing issue that already has a location or document reference as Coordination, so the existing verification gate is unchanged; all other issues are General. The type appears in the register (column and URL filter), the issue detail panel, list/export (`issueType` filter and "Issue type" column) and the create and realise forms.

- Reference rule: the spec text puts no time limit on "require at least one location or drawing/model reference for a Coordination issue", so it is applied in the strictest form. A Coordination issue cannot be created without one: the create and realise bodies accept its first locations and drawing/model references, validated in the same transaction. A General issue can become Coordination only once it has a reference, and resolution re-checks this. References cannot be removed, so the rule holds after creation. Any document reference counts: each one already records a drawing/model identifier, declared revision and source link, and this matches the backfill.
- Resolution gate: only Coordination issues need current independent verification and available references before Resolved. The old inference from "has a location or document reference" is gone, so a General issue with references resolves without verification.
- Type change: requires the existing register edit permission and If-Match, and only the PM or the issue owner may change the type (the raiser, discipline lead and Read Only users are refused). It is allowed only while the issue is Open or In Progress, so a closed issue keeps the type it was resolved under. Coordination→General is refused once any verification record exists, so an appointed or rejected verification cannot be bypassed. A concurrent appointment and type change conflict on the issue row version.
- Audit: `IssueLocation`, `IssueDocumentReference` and `IssueVerification` now have audit field lists (and `Issue` audits `IssueType`), so their writes produce activity rows. These rows carry the issue key, so they appear in the issue's History tab, and their item types and fields have interface labels.
- Seed: the review demo seed creates no issues. A manually raised DEMO-101-I01 with a site area and drawing reference becomes Coordination through the backfill, so no seed change was needed.
- UI fix: the resolve dialog now shows field-level gate messages (verification, reference, impact) that it previously hid.

Evidence:
- PASS — `dotnet build Hub.slnx`; `dotnet ef migrations has-pending-model-changes`: none after the new migration.
- PASS — PostgreSQL `LocationIssueTests` 9/9 with four new cases:
  - creation refused with no reference, an invalid reference or an unknown type, and nothing partly created;
  - list, filter, export and detail;
  - Coordination resolution blocked until Verified, while a General issue with a location resolves;
  - risk realisation follows the same rule;
  - type-change permissions, If-Match conflict, reference requirement, verification lock, closed-issue lock and audit row;
  - location, reference and verification activity rows reach the issue history;
  - the migration's own Down/Up operations, re-run in a rolled-back transaction, mark located and Markup-referenced issues Coordination and a plain one General.
- PASS — focused `LocationIssueTests|RegistersApiTests|SchemaTests` 16/16; full local suite 518/518.
- PASS — `python3 tools/trace_spec.py --check`; frontend production build and lint (exit 0, 88 existing warnings, none in `Registers.tsx`).
- PASS — local Chrome on a throwaway database (since dropped). The previous build (`bfd6a12`) raised DEMO-101-I01 with a site area and drawing, plus a plain DEMO-101-I02. Starting the new build on that populated database applied the backfill and re-ran the review demo seed without error. I01 then showed Coordination and I02 General, and the Coordination filter returned only I01. Raising a Coordination issue without its first location was refused with no issue created, and the API refused one without any reference (400 `reference`). With a site area it was raised as DEMO-101-I03 Coordination. I02 resolved without verification. Resolving the backfilled I01 was refused with the verification-required message, and it stayed Open. No JavaScript errors.
- UNPROVEN — deployed behaviour, keyboard and assistive-technology review of the new form controls, and notification changes (none were made).

## Affected-discipline notices, discipline views and impact settlement (2026-10-01)

- Notices: new event `IssueAffectedDiscipline` ("Issues affecting your discipline"; default in app on, email off; not a direct assignment, so a muted follow suppresses it). It goes to the lead of each newly affected discipline when an issue is raised, realised from a risk or edited. It follows FR-LOC-03 (affected disciplines are identified so they can resolve the same problem), the §17.1 rule to notify people of changes others make to their work, and FR-MDC-06 (existing preferences, one deduplicated event per recipient). It is ProjectScoped: access is rechecked when the notice is composed and again before email delivery (FR-MDC-02). The notice is queued in the same transaction as the change, never goes to the actor, and is sent once per recipient per command even when one person leads several newly affected disciplines. A refused, stale or repeated command adds no notice. Removing a discipline sends nothing. Affected-discipline additions and removals now carry the issue key, so they appear in the issue history.
- Discipline views: Weekly Coordination section 10 (`/projects/{id}/coordination?disciplineId=`) now lists open issues for their primary discipline and each affected discipline, once per view, matching the register filter. Unchanged on purpose:
  - rules-engine discipline health and A-07 routing stay on the primary discipline, because FR-MDC-07 forbids silently changing health;
  - the project dashboard issue counts are project-wide, not discipline-scoped.
- Unavailable reference: the spec (FR-LOC-04, FR-LOC-06, AC-LOC-03) defines no way to restore or replace an unavailable reference on an open Coordination issue. Impact checks cover changed revisions on closed issues only. Nothing was implemented; this needs a product decision.
- Fixed a related dead end: FR-LOC-04 says "the issue owner/verifier decides". An impact check on an issue resolved without a verifier (now possible for General issues) waited forever for a verifier and blocked reopening. The owner's decision now settles it when there is no verifier.

Evidence:
- PASS — `dotnet build Hub.slnx`; `dotnet ef migrations has-pending-model-changes`: none (no migration in this increment).
- PASS — PostgreSQL `LocationIssueTests` 11/11, two new:
  - notice to the lead, none to the actor or an unaffected lead, link path, project-scoped email;
  - no self-notice when the owner adds his own discipline; stale (409), Read Only (403) and repeated commands add nothing;
  - one notice per recipient per command when one person leads two newly affected disciplines;
  - nothing for a lead who lost restricted-project access;
  - the coordination dashboard lists the shared issue once under each discipline.
- PASS — new `ReviewChangeTests` case: a General issue resolved without verification and then superseded becomes ReopenRequested on the owner's decision and can be reopened. It fails without the fix. The existing two-party AC-LOC-03 case still passes.
- PASS — full local suite 535/535; `python3 tools/trace_spec.py --check`; frontend production build and lint (exit 0, 88 existing warnings).
- PASS — local Chrome on a throwaway review-demo database (since dropped). Taylor raised a General issue under Project Management and marked Civil affected in the panel. Jay, the Civil lead, got one "Issue DEMO-101-I02 affects your discipline" notice, which opened the issue panel. His preferences listed the new event, and Weekly Coordination filtered to Civil showed the issue. Taylor, the actor, got no affected-discipline notice. No JavaScript errors.
- UNPROVEN — deployed delivery, digest presentation and keyboard/assistive-technology review.


## 2026-10-01 Codex recovery checkpoint

Recovered grouping places one handoff blocker above its affected tasks, with live source/action links. Focused handoff/grouping checks passed 14/14 and the combined PostgreSQL suite passed 563/563 before subsequent readiness/export additions. Full location workflow, source/register reconciliation, print/export and company engineering acceptance remain UNPROVEN.


## Declared-coordinate persistence check — 2026-10-02

PASS locally: `Valid_coordinate_preserves_declared_values_and_reference_metadata_on_readback` (1/1) creates a fresh General issue, stores one Coordinate location, and reads back the exact decimal X/Y/Z values (including negative Y), declared CRS `EPSG:26920` and units `m`, with no conversion or duplicate location. This is synthetic metadata preservation, not validation of an engineering survey or coordinate transformation. Application code is unchanged from `7c412b0` / staged release `7995e88`. Full hosted and assistive-technology acceptance remain unproven.
