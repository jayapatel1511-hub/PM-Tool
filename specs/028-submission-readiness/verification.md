# Verification: Submission Readiness and Issue Manifest

**Date**: 2026-09-27
**State**: API, domain, and initial project screen implemented. Product acceptance is not complete.

## Evidence run in the isolated Mac worktree

- The additive `SubmissionReadinessSchema` migration applied to the dedicated local `hub_review_local` database. PostgreSQL metadata showed the waiver constraints, source-less check uniqueness index and append-only issue triggers. `dotnet-ef migrations has-pending-model-changes` reported no model drift.
- `dotnet test tests/Hub.Tests/Hub.Tests.csproj --no-restore --filter 'FullyQualifiedName~SubmissionApiTests|FullyQualifiedName~ReviewChangeTests|FullyQualifiedName~SubmissionTests' -v:q -p:WarningLevel=0` passed 17/17 against ephemeral PostgreSQL test databases.
- AC-SUB-01: an Electrical blocking finding appeared as an individual source blocker; Issue returned 422 without an issue row. PM attempt to mark the derived blocking-finding check Not Applicable returned 400.
- AC-SUB-02: publishing revision B moved an unissued Ready package that referenced A to Checking, cleared optional evidence and preserved the previous manifest version. A PM reassigned its optional check owner and coordinator, then the new coordinator created manifest version 2.
- AC-SUB-03: a superseding package retained the first issue's A manifest and original transmittal. A database update to the first issue failed under the immutability trigger.
- AC-SUB-04: changing a checklist check after the PM's read made the old readiness fingerprint return 409. Retrying the same successful Issue command did not create a duplicate issue row.
- AC-SUB-05: the mandatory blocker waiver attempt returned 400. A non-PM optional Not Applicable attempt returned 403; the PM recorded a reason and evidence, after which the named owner could record Pass.
- The focused 17-test set passed again after adding the export and source paths. The API export test checked manifest and issue data and returned 404 for a caller outside a restricted project.
- `npm run build` passed with Vite CSS and chunk warnings. The local browser signed in as synthetic Taylor and loaded DEMO-101's Submissions page, its empty state, and New submission form. No deliverable revision existed in that local project's register, so an end-to-end browser issue was not exercised.

## Remaining acceptance

Complete notification coverage and a deployed browser workflow remain. Metadata editing exists but has not had a dedicated scenario run. Source-change invalidation is covered for revision publication, required review changes and the handoff/basis hooks described below. Read Only, inactive-owner, soft-deletion and simultaneous finalisation cases are not yet fully run. Accessibility needs a keyboard walkthrough. No Azure deployment, Entra sign-in, company pilot or production result is implied by these local tests.

Submission create, manifest replacement, metadata edit, coordinator assignment, start, optional-check action and reassignment, issue and cancel now emit a `SubmissionChanged` event within the command transaction. The event links to the package panel and uses project-access checks at creation and queued email delivery. A focused PostgreSQL test passed 3/3: an Issue notified the coordinator, a retry did not add a notice, and an unrelated restricted-project user had no notice. The test did not exercise preferences, mail delivery, source-triggered invalidation notices or every recipient transition. Those and the complete deployed browser workflow remain UNPROVEN.

## Populated local browser workflow

Against the persistent synthetic `hub_review_local` database and current Development-auth API, Jay created DEMO-101-SUB000 with milestone M01 and current D001 revision P02, then started Checking. The browser showed two source-linked blockers for the unapproved current review round, grouped under Civil and Yagmur, and withheld the Issue action. Its JSON export contained one manifest item and the unresolved source checks. Jay started the P02 review round; Yagmur approved it from a separate browser identity. A fresh submission read then showed zero blockers and Ready. Jay, the coordinator, still had no Issue action; Taylor, the project PM, did. Taylor recorded a **synthetic rehearsal** issue with an `example.test` transmittal URL. The browser and export showed Issued, one issue-history entry, P02 in the manifest snapshot, authorisation metadata, and stored Pass statuses for derived checks. Browser JavaScript errors: zero. No external files were transmitted.

Self-review found that a Ready package still rendered the pre-issue stored `Pending` status for all seven passing derived checks. The screen now labels those rows `Pass (live source check)` while they remain unissued; the API still re-evaluates all checks and the fingerprint atomically when the PM records Issue. The revised browser showed seven live-pass labels, no Pending labels, and zero violations or incomplete results in a focused settled-dialog axe WCAG 2.1 A/AA scan. Frontend build and lint passed with existing warnings. This is local synthetic acceptance of the exercised path only; deployed password sign-in, simultaneous live issue, accessibility with assistive technology and the full packet edge-case matrix remain **UNPROVEN**.

## Handoff and basis invalidation increment

Handoff creation, draft edits (including both old and new targets), transitions and reassignment now invalidate related unissued submission checks and clear their current evidence in the same project transaction. Confirming or withdrawing a design-basis version does the same for packages containing linked work. Issued snapshots remain unchanged. The combined `SubmissionApiTests|ReadinessApiTests|LocationIssueTests|DesignBasisApiTests|HandoffsTests` PostgreSQL set passed 38/38, including a real Handoff create and transition and a basis replacement confirmation. This does not yet prove every retargeting race, an end-to-end browser issue, notifications for source-triggered invalidations or full packet acceptance.

## Keyboard walkthrough — 2026-10-01

This used the same isolated build and `hub_agent_verify` database as the packet 031 rehearsal, in real Chrome, using only the keyboard: Tab, Shift+Tab, Enter, Space, Escape and type-ahead. Setup used the API and is not under test: a current D001 P01 revision and an approved review by Yagmur that is required for issue.

- **Create (PASS):** Jay (coordinator and Civil lead) reached New submission after 50 Tab stops; a skip link is present. Enter opened the form with focus in Title, and 40 more Tabs stayed inside the dialog. Jay filled every field from the keyboard, choosing the milestone by type-ahead and the manifest item with Space, then saved with Enter. DEMO-101-SUB001 opened with focus inside the detail.
- **Checking (PASS):** starting checking from a nested dialog left focus in the detail, which then showed Ready with live-pass checks. Escape from Edit submission returned focus to the detail. Jay had no Record issue action.
- **Issue (PASS):** Taylor (PM) opened the package from its row and recorded Issue with an `example.test` transmittal. The Issued record appeared, and focus stayed in the detail.
- **Focus, naming and axe (PASS):** all 177 focus stops had a visible ring and an accessible name. axe WCAG 2.1 A/AA found 0 violations across seven settled states (incomplete: colour contrast only). There were no browser errors.

**FAIL (focus return):** Escape on the New submission form and on the package detail left focus on `<body>`, so a keyboard user has to start again from the top of the page (WCAG 2.4.3). New basis entry, the basis detail and Propose allocation behave the same way. The Radix Export menu, which has a trigger, does return focus. The likely cause is that these dialogs are mounted from state without a `DialogTrigger`, so Radix's close auto-focus targets a trigger that does not exist (`web/src/components/ui/dialog.tsx` and callers such as `Submissions.tsx:42-43`).

No keyboard trap or unlabeled control was found. Not run: a screen reader, other viewport sizes and deployed sign-in.


## 2026-10-01 Codex recovery checkpoint

Constraint/promise keys, generic links, export and search commits were integrated. The combined PostgreSQL suite passed 563/563 before the later readiness applicability/export privacy commits. Populated local review-copy migration rehearsal applied 22 to 27 migrations without changing project/task/constraint/promise counts. This is local synthetic evidence, not company submission acceptance.
