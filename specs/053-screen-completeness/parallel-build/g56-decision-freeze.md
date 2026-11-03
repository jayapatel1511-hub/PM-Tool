# G56 S39 Decision contract freeze

Root freeze2026-11-02. A's reviewed g56-decision-metadata-plan.md governs except these explicit refinements. Independent review found organisation-only grouping compliant, and identified rationale authority needing the guard below. S39 is not implemented yet;35/49 slices remain locally checked.

## Data and meaning

- Owner flag is `register_screens`. No permission/§10 change. No starter category seeds or backfill;053-D15 stays Open. Root supplies `DecisionCategory : RefData`, `HubDb.DecisionCategories`, and nullable `Decision.CategoryId`, `OptionsConsidered` (string[]), `Rationale`.
- Category uses an actual active organisation-list ID for a new assignment. An unchanged stored inactive ID can be retained/read; no name lookup or invented Other. Canonical Admin vocabulary path will be `/api/v1/admin/decisionCategories`, same RefData120-name/active/sort/version/usage and register pre-replay guard. Root owns that shared registration/UI.
- Ordered options: null=not recorded, []=explicitly empty;0..5 nonblank trimmed strings, max500 each. Do not truncate, coerce or silently deduplicate. Rationale is optional trimmed text, max2000; this is newly chosen metadata, not a cap on the existing required DecisionText8000. Ordinary outcome text/date requirements stay unchanged.
- Rationale is outcome metadata. Any supplied rationale PATCH, including null/clear, requires currently Decided plus BOTH existing edit authority and existing `Permissions.DecideOrDefer` authority. This prevents a requester who cannot decide from rewriting the outcome's why. Validate the entire mixed PATCH before assignment/side effects. Category/options use ordinary edit authority. Non-null action rationale is accepted only for Decided.
- Reopen clears rationale together with existing outcome fields even off; a new old-style decision never revives stale rationale. Category/options survive. Canonical activity records history; no fabricated ownership/date snapshot or extra business event.

## Organisation summary

- Resolve the contact-model conflict by grouping **owner kind plus actual trimmed ExternalParty.Organisation**, ordinal exact string equality within the current readable project. Internal owners share one Internal group. External organisation text named Internal remains a separate external group. No contact IDs/names, user/recorder/requester names, client-name/category/email fallback, per-person metric or drilldown. No new organisation identity or case/spelling merge is claimed.
- This is a live calculation over current registered owner/organisation/date values, explicitly labelled in UI; corrections may change it. No immutable historical attribution claim or backfill.
- Cohort is all live currently Decided decisions in that project, independent of individual register filters. `/projects/{id:guid}/decisions/screen` takes no row/person filters; explicitly reject supplied query keys so no caller mistakes it for per-owner analysis. Current read access/flag gates apply before any data. Do not call a write/EnsureFresh path for this read.
- Calendar-day lag is DecisionDate−DateRequested. Same-day0 is valid; negative, missing or future decision dates are excluded. Capture server asOf/organisation today once. Odd/even medians exact (4/10/12→10;4/11→7.5). No sample is unknown/absent, never invented0.
- Exclusions are disjoint: invalid/missing/future dates first; among remaining dated rows, unknown/missing/foreign owner or blank organisation is unattributed. Sum(valid attributed samples)+invalidDateCount+unattributedCount equals full decided cohort. Expose denominator/sample/exclusion truth; no hidden filtered/capped cohort. Batch/query actual owners, no per-row DB lookup or writes.

## Protocol and UI

- Preserve all old arrays, members/property order and command replies. Append only ignored-null optional request/read metadata as proposed; existing callers/exports/context remain intact. Strict new fields/clear semantics, stored-on/off omission, unknowns and version/refusal atomicity follow the reviewed plan.
- A owns its direct Decisions.cs record/handler/route seams and existing Decisions.tsx integration, including shared RecordDecisionDialog used by Coordination. Root does not edit those files while A authors. No changes to Coordination's authority or workflow.
- Add marked narrow pre-replay helpers for feature-bearing create/transition bodies. Root mounts them in global IdempotencyMiddleware after handoff. Charset-aware buffered JSON and original stream restoration; current flag/access/authority before claim/replay. Omitted/null legacy bodies retain old receipt behavior. No generic body-hash guarantees.
- Successful receipt replay must not re-run stale-version or legal-transition-from-current-state checks: a first successful Decided command changed that state/version. Recheck canonical current authorization separately from feasibility, with unchanged authority predicates. Fresh commands still enforce all old state/version rules.
- Flag-on create/record UI uses immutable route/body/key/original observed version after uncertainty or idempotency-in-progress. Validate the actual response shape before clearing. No automatic fresh version/new key after uncertainty; Cancel/leave guards and natural focus remain. Off legacy form/body/behavior stays exact. Person navigation is no exception to dirty-form safety.
- Generated unique Decision-prefixed types only; no shadow DTO/any. Viewer/project/flag query partition; error-state cached metrics/actions withheld. Root owns locale/cache/generated files. Supply exact proposed locale keys AND text promptly so source/browser expectations agree.

## Exact loans and evidence

A may edit ONLY existing `src/Hub.Api/Features/Decisions.cs`, `web/src/pages/projects/Decisions.tsx`; NEW `Features/DecisionMetadata.cs`, `DecisionScreenReads.cs`, `DecisionMetadataReplay.cs`; NEW `tests/Hub.Tests/Api/DecisionMetadataTests.cs`, `DecisionLagTests.cs`, `DecisionMetadataReplayTests.cs`; NEW `web/src/pages/projects/decision-metadata.tsx`, `web/tests/decision-metadata.cjs`, `decision-metadata.fixture.cjs`; and its own g56 handoff/locale proposal docs. Prefix the three Features paths with `src/Hub.Api/`.

Root exclusively owns all schema/migration/registry, Admin vocabulary, middleware call, locale/cache/generated/inventory/build/runtime/Chrome integration. All other application/test files HOLD. No execution, helpers, dependencies, flags, services, commit/push/deploy/Accepted. All four chats/reviewers combined max4 helpers; root coordinates.

Prove real AC69 organisation-only medians/denominators/unknowns; requester rationale refusal (including mixed body) versus PM/owner success; metadata active/inactive/version/readonly/archived rules; receipt UTF8/UTF16/off/revoked replay; no read-side business writes; exact off wire, existing Decision tests and synthetic actual-SPA desktop/phone fields/metrics/refusals/retry/natural navigation. All checks authored by A remain UNRUN until root executes after source freezes. Return READY/HOLD hashes and exact unrun checks.
