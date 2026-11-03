# B generation51: BACKEND READY / ALL SOURCE READY — HOLD

2026-11-01. Six new helper sources and five new focused API test files are authored. All eleven are now held for root's independent source review and single execution lane. No build, test, API, browser, database command, migration, service, generator or helper ran in this lane. Reading source and authoring test code are not runtime evidence. No shared app/model/DTO/MyWork/metadata/module/cache/locale/generated source was edited by B. No goal/status/acceptance/commit/push/deployment change.

## Owned files

- src/Hub.Api/Infrastructure/WorkAssignmentPublication.cs
- src/Hub.Api/Features/WorkSeenEligibility.cs
- src/Hub.Api/Features/WorkSeenReads.cs
- src/Hub.Api/Features/MyWorkSeen.cs
- src/Hub.Api/Features/WorkActivityFacts.cs
- src/Hub.Api/Features/WorkSeenContracts.cs
- tests/Hub.Tests/Api/WorkAssignmentPublicationTests.cs
- tests/Hub.Tests/Api/MyWorkSeenTests.cs
- tests/Hub.Tests/Api/MyWorkSeenRaceTests.cs
- tests/Hub.Tests/Api/WorkSeenEligibilityTests.cs
- tests/Hub.Tests/Api/WorkActivityCoverageTests.cs

The preceding documentation contract is work-delivery/requests/g51-seen-state-build-plan.md; it was amended with the two required architecture corrections before this source loan. Root's later explicit source loan, not that plan alone, authorized these files.

## Behavior authored

Publication covers the adopted twelve roles across nine canonical types. It compares real current/original role recipients including Decision/Action discriminators, binds the exact canonical activity/version before base save and accepts this repository's legitimate create-version0. Same-value/comment/stage-only edits and affiliations emit nothing. SourceVersion ordering is per canonical role, never wall-clock/UUID/personal revision. HasChanges/Publish do not touch private preference locks. An additional Bound(db, canonical) convenience returns only the exact object/version/roles actually read.

Eligibility uses one closed SQL builder for both bounded token reads and a single-statement INSERT/CTE MarkAll cut. It preserves actual own-assignee/collaborator membership, arbitrary-age Completed tasks, direct mapped roles, review/status/recent-requested rules, independently gated follow-up and concrete coordination targets. An unrelated readable nonTask owner is not blanket eligible. Source read bounds use <= returned version plus returned roles. Missing projection produces Unavailable; MarkAll's missing guard inserts no acknowledgements before raising an integrity conflict.

Own Visit/Opened/MarkAll commands use strict bodies and one user_setting row lock. Occurrence producers never acquire that lock. First enrollment inserts a token-only historical baseline cut, later visits leave pending markers untouched, and MarkAll selects no UI page/filter cut. Only exact observed opened tokens are inserted. Request/action/normalized-body SHA256 and immutable response receipt commit in the same transaction as ack/preferences. Replays recheck active actor/flag and opened item authority/current recipient. Existing generic idempotency is bypassed by the root-wired AtomicCommand marker. Active ReadOnly users may acknowledge private own facts.

An opened requestId is immutable for its normalized item+token payload: retry reuses it; replacement tokens in a still-mounted panel require a new ID. Backend rejects changed-body reuse. This is a root/frontend lifecycle obligation as well as an API test. No optimistic clearing is authored here.

FR15 facts bind event ID, exact event time, actual ActorId/ActorType/name from the same immutable typed item event, plus live comments. System requires actual System; unknown kinds/users remain unknown. Comments/related history are not relabelled as parent Task events. The public read keeps current canonical-key/project authority. Internal ReadAuthorisedAsync(db, exactSelectedKeys, ct) shares the same core for root's already-authorized Task/page adapter; it is not an arbitrary untrusted-ID bypass.

## Root integration seams

Root's Data/WorkSeenEntities, EF/FKs/indexes/append-only guards, UserSetting fields and publisher/middleware/module integrations are separate root work. Their names and private literals are consumed exactly as provided. B does not generate/apply a migration, perform backfill or edit canonical §10.

Public signatures match the accepted plan: WorkAssignmentPublication.HasChanges/Publish; WorkSeenEligibility.ReadCurrentAsync/AcknowledgeAllAsync; WorkSeenReads.DecorateAsync; MyWorkSeenEndpoints.Map plus AtomicCommand; WorkActivityFacts.ReadAsync plus INTERNAL ReadAuthorisedAsync; helper contracts in WorkSeenContracts.

Remaining shared integration/verification obligations:

1. Root completes and reviews migration/baseline/cutover. Legacy seeds retain unknown source activity/time, not invented assignments. Private revision stays non-concurrency/non-IVersioned, without generic expiry.
2. Root attaches nullable own seen envelopes to actual canonical versions/returned roles in all relevant rows/details, including full nonTask census. Other-person/off shapes omit them. Root retains MyWork.cs and existing DTO/detail ownership.
3. Full /me/work aggregate should suppress separate per-stream latest-event enrichment, union/dedupe its authorized keys, call FR15 core once and reuse identical facts across repeated rows. Standalone Task pages keep their one accepted core call. Cached LastActivityAt/business sort stays unchanged.
4. Root/D integrate visible-visit and successful-visible-detail hooks, exact normalized opened request-ID lifetime, MarkAll UI, New text, no prefetch/failed/background acknowledgement, typed nonTask Comments section and exact source/focus return.
5. Root/D supply the explicit polymorphic owner-kind bridge and only person-link true users. FR15 actor type is independent of owner type. Root owns flat Task activity adapters, other DTOs, cache/account invalidation, own visible15s/focus refresh, labels/transient metadata/generated contracts.
6. Root checks SQL/provider translation, schema parity, PostgreSQL cuts/races/constraints, focused API cases, cache and real browser joins only after independent source review/freeze. Nothing in this handoff claims those checks passed.

## Authored verification

Five files define35 expanded Fact/Theory cases (source count only), including:

- all nine type/twelve role binding probes plus normal-command creation matrix, create-version0, exact source event, no-op/comment/review-stage/stale-refusal behavior;
- first-use/no-GET-write, hidden/closed truthful BaselineSeed, duplicate visit/open payload, off-period reassignment, revoked recipient replay, ReadOnly/other-person/off, two observed roles, >200 task MarkAll, immutable normalized request payload and replay beyond a day without generic receipts;
- arbitrary-age Completed assignee and collaborator+reviewer cases, creator/collaborator-only exclusion, unwatched/readable Risk exclusion and independent watch flag, Action discriminator reactivation, stale-version future-token exclusion, nonTask closed-requested2/25-day cases;
- explicit barriers around uncommitted assignment versus enrollment/MarkAll, outer canonical rollback, reverse multi-project producers, stale unrelated preference writer versus visit, missing-projection all-or-nothing failure;
- paired actor/time versus comment-updated cache, actual typed collision, actual System/unknown actors, live deleted comment counts, unsupported capability and authority/off filtering.

Fault/temporal fixtures are labelled where direct audited saves or raw deletion deliberately represent legacy/corrupt/race state. Normal role creation/reassignment/workflow paths are tested through canonical commands. Tests remain uncompiled/unrun. Root's independent review may require exact repairs before execution.

## Evidence boundary

Authored: READY/HOLD. Source review by B: read-through only. Compile/migration/tests/browser: UNPROVEN in this lane. Human acceptance, D20 closure, deployment and all wider remaining052/053 scope remain separate. Preserve confirmed navigation choices and existing evidence.
