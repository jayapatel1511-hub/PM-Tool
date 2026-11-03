# G60 C — digest composition interface proposal

2026-11-02. **READY proposed interface / HOLD implementation.** Only this document is authorized. Application/test/schema/specification/decision/generated files and all existing source remain held. No build, check, compiler/parser, browser, scheduler, job, flag, mail, helper or commit operation was performed. Signatures below are a proposed C# contract, not compiled declarations or a source loan. Root freezes the final shared names, model changes and exact writing boundaries.

Authority: reviewed [G58 model](../../capacity-people/requests/g58-notification-delivery-model-contract.md), [G59 compound addendum](../../capacity-people/requests/g59-bulk-summary-delivery-addendum.md), root [technical plan](../../g58-notification-delivery-technical-plan.md), and the maintained sources below. The G59 coordination feature is not changed by this plan. All new delivery work belongs to people_screens; unlinked legacy mail, the legacy digest signature/policy, and WeeklySummary stay independently unchanged.

## Source facts and consequential gaps

All source paths are relative to `/Users/jaypatel/.codex/worktrees/ef94/PM-Tool/.build-052-053`.

| Maintained source | Observed behavior; consequence for the enhanced contract |
|---|---|
| `Infrastructure/Digest.cs:16–38` | Public Row/Section/Updates/Result and the existing Build signature have no rendered planning scope, held membership or policy witness. Result.RequiredProjectIds is set to **every** active visible project at line280. Enhanced code must return its own exact provenance; it cannot treat that broad array as rendered scope. |
| `Digest.cs:25–26`, `:58–64`, `:236–280` | There are20 configurable section codes, shared severity/dedup behavior and a10-row section cap. Make marks all fresh item IDs before taking10; its Total and “more” text still describe omitted rows. Preserve that policy, including off-section items being eligible in a later section. Source metadata must follow the final rows **and displayed aggregate/count contributors**, not every visible project and not only the10 printed Row objects. |
| `Digest.cs:107–148` | A rendered supervisor staff row aggregates work from several projects but Row.Project is empty. Stalled-review selection loses ProjectId. StaffAssignment notifications can require several projects. Project updates print project-level counts even with no top detail lines. Exact requirements must be retained at these producers, not recovered by parsing links, titles, names or a later full-roster lookup. |
| `Digest.cs:124`, `:139` | Staff notices use CreatedAt>since and updates use OccurredAt>since with **no upper cutoff**. The enhanced overload must add <=Window.Through to those timestamped histories. Keep the legacy off predicates unchanged. Dated/current-state sections are not historical reconstructions. |
| `Digest.cs:176–184` | Planning uses VisibleForUser, recipient-created entries and current stale/working-week rules, with Take(Cap+1). Preserve the actual bounded source behavior; do not claim its Total is an uncapped global count. Enhanced scope needs the actual planning-entry IDs behind its rows/count, which the current Result cannot express. |
| `Digest.cs:240–256`, `:335–336` | An overdue Admin review is an unswitched inline line, but DigestJob still skips when all20 configurable sections are off. Preserve that gate: an enabled admin_review flag does not authorize a held-only bypass when all ordinary sections are off. No new21st switch is proposed. |
| `Digest.cs:317–353` | The job returns early for weekends and skips by current time/LastDigestAt before Build; it directly stages legacy mail and advances LastDigestAt even for an empty build. Those creation/preparation shortcuts cannot gate continuation of an existing Monday slot. The watermark is not provider acceptance. |
| `Digest.cs:358–367`, `Operations.cs:42–77` | Known away is planner Normal>0 and Available==0. Operations duplicates daily due/time/watermark logic, assumes08:00 and counts same-day SentAt-null Digest mail. Enhanced weekly creation, retries, dormant/held/uncertain outcomes and reminders need the same future due readers as their jobs; no inference from these legacy counts is sufficient. |
| `Data/NoticeDeliveryEntities.cs`, `Data/Entities.cs:899–929`, `Data/HubDb.cs:661–677` | Existing foundation supplies immutable slot window/schedule, composition0..3, attempt0..8 and exact email linkage. ScheduledLocalTime is checked HH:mm text, not a TimeOnly property. There is currently **no ordinary-section policy witness** on EmailMessage; BulkRenderedMembershipJson is reserved for its closed bulk map. Root must settle the persistence seam described below. |
| `Infrastructure/NoticeRecipientLock.cs` | Acquire requires an originating ReadCommitted transaction and uses the existing recipient advisory-lock namespace. A repeatable-read composition transaction would violate the established lock contract. D's future context must bind the actual HubDb/transaction/user, without silently starting or committing a different transaction. |

## One writer per proposed source

| Owner | Exact proposed scope, still awaiting root's source freeze |
|---|---|
| C | NEW `src/Hub.Api/Infrastructure/NotificationDigestComposition.cs`: enhanced digest due/window/slot coordination, composition DTOs, orchestration over D's policy/reservation methods. NEW `tests/Hub.Tests/Api/NotificationDigestCompositionTests.cs`. |
| C, exact existing hunks | `src/Hub.Api/Infrastructure/Digest.cs`: one shared Build core plus an internal enhanced overload; transient output provenance at existing section producers; enhanced-only timestamp upper cutoffs; one DigestJob enhanced branch that consumes the same due reader. Preserve legacy public DTOs, existing Build signature and ordinary20-section ordering/selection/dedup/caps/copy. No WeeklySummary/WeeklySummaryJob edits. |
| D | NEW `Infrastructure/NotificationDelivery.cs`: the single shared clock resolver, recipient context/fresh policy, held eligibility and prepared candidates, guarded reservations/immutable email and rendered-map codec, claims/completion/uncertainty/attempt policy. Existing NoticeRecipientLock/NoticeDeliveryProof remain authoritative. No second resolver or general delivery framework. |
| B | NEW `Infrastructure/TimeEntryReminderJob.cs` and its focused tests: reminder-specific current-week eligibility and atomic own app notification/Emitted slot. Consumes D's resolver/context. C neither writes reminder scheduling nor makes email for it. |
| Root | Data/Email/slot/schema/guards/migrations, ordinary-section witness representation, shared registration/Operations/Notify/EmailJob integration, locales, generator/build/provider/browser/acceptance. Any additional shared source seam needs an exact loan first. |

Publicly messaged alignment with D/B: the low-level class is **NotificationDelivery**, not a new NoticeDeliverySchedule file/class. D owns ResolveLocal/WeekMonday; C owns digest eligibility/due/window policy and B owns reminder eligibility. All following names are one proposal for root to reconcile with the parallel core/reminder documents, not permission to declare duplicate types.

## Shared core boundary to consume

These declarations belong to D's proposed core, not C's source file. Namespace is Hub.Api.Infrastructure; Guid/DateOnly/TimeOnly/DateTimeOffset/CancellationToken use normal System types; immutable collections use System.Collections.Immutable. Shared CLR types/methods are **public**, consistently with B's due reader; opaque context/preparation constructors are **internal** to D and validate the actual live transaction. Public CLR accessibility alone does not bind an HTTP endpoint/schema. No new business endpoint accepts these records; no InternalsVisibleTo/reflection workaround is proposed.

```csharp
public sealed record NoticeLocalSchedule(string OrgTimeZoneId,
    DateOnly RequestedDate, TimeOnly RequestedTime,
    DateOnly EffectiveDate, TimeOnly EffectiveTime, DateTimeOffset ScheduledAtUtc);

// Proposed D methods on NotificationDelivery, no Begin/Save/Commit hidden in LockRecipientAsync:
// public static Task<NoticeRecipientContext> LockRecipientAsync(HubDb db, Guid userId, CancellationToken ct = default);
// public static Task<NoticeRecipientPolicy> ReadRecipientPolicyAsync(NoticeRecipientContext context, CancellationToken ct = default);
// public static NoticeLocalSchedule ResolveLocal(OrgSettings settings, DateOnly requestedDate, TimeOnly requestedTime);
// public static DateOnly WeekMonday(DateTimeOffset nowUtc, OrgSettings settings);
public sealed record NoticeDayAvailability(string State, bool? IsAway);
// Proposed lazy D seam, final name not yet frozen:
// public static Task<NoticeDayAvailability> ReadDayAvailabilityAsync(NoticeRecipientContext context,
//     DateOnly day, CancellationToken ct = default);
```

ResolveLocal calls Clock.Zone(settings), captures the actual resolved zone (including existing UTC fallback), advances an invalid wall time to the **first valid instant after the gap**, and chooses the **minimum UTC candidate** for an ambiguous local time. It must not add a fixed hour, pick the later UTC offset, or poll twice in the repeated hour. Persist the requested schedule basis in existing HH:mm fields plus the resolved ScheduledAtUtc; effective date/time can remain transient. Root freezes any different mapping; C does not add effective-time columns or a second DST algorithm.

D's context exposes the bound db/user/transaction only through its trusted API; its constructor and preparation constructor remain internal. **ReadRecipientPolicyAsync loads fresh base flags, active user/address and preferences/section switches only. It does not eagerly load capacity or Time hours.** ReadDayAvailabilityAsync is a separate lazy delivery-hold seam, using the existing canonical person-day reader only when a current immediate/digest hold needs it. Known-away means Normal>0 and Available==0; reduced positive, zero-normal and nonworking are not away. Unknown IsAway is null, never fabricated zero. B's reminder due reader does not call this seam, read capacity or inspect hours/quota. Read-only Operations uses these same lazy policy/due paths without inserting a slot, reserving work, advancing a watermark or consuming a policy/transport attempt. Exact lazy-method spelling is not yet frozen with D.

The exact C request/result definitions below reference these proposed D-owned records:

```csharp
public sealed record NoticeRequiredScope(ImmutableArray<Guid> ProjectIds,
    ImmutableArray<Guid> PlanningEntryIds);
public sealed record NoticeHeldCandidate(Guid IntentId, Guid UserId,
    DateTimeOffset CreatedAt, string Subject, string BodyText, string? LinkPath,
    NoticeRequiredScope Scope, NoticeBulkRenderedMembership? BulkMembership);
public sealed record NoticeRenderedMembership(ImmutableArray<Guid> IntentIds,
    ImmutableArray<NoticeBulkRenderedMembership> BulkMemberships);
```

Prepared candidates are current-policy-admitted, same-recipient, unreserved/unconsumed scalar or compound facts. A compound line's generic count/text and scope must be regenerated from its **exact currently eligible original email keys**; never reuse the parent/app's original title/count/full scope after a partial refusal. The original app row stays unchanged and keeps its original all-project visibility rule. D validates BulkMembership with NoticeDeliveryProof.Render/ValidateRendered and owns the closed persisted rendered-map codec. No current same-item/app/correlation lookup manufactures membership.

## C-owned exact proposed interfaces

All shared declarations/methods are public CLR in Hub.Api.Infrastructure, with matching public types throughout signatures; no new HTTP endpoint/schema binding or registration is implied. Opaque D context/preparation constructors remain internal. State strings below are not new persisted vocabularies. D-specific preparation/reservation/policy-witness details remain proposed, not agreed source authority.

```csharp
public sealed record NoticeDigestWindow(DateTimeOffset From, DateTimeOffset Through);
public sealed record NoticeDigestBuildRequest(Guid SlotId, Guid UserId,
    string FirstName, DateOnly Today, DateTimeOffset ReadAtUtc,
    NoticeDigestWindow Window, OrgSettings Settings, string BaseUrl,
    ImmutableHashSet<string> SectionsOff, ImmutableArray<NoticeHeldCandidate> Held);
public sealed record NoticeDigestActivityScope(Guid ProjectId, ImmutableArray<Guid> ActivityIds);
public sealed record NoticeDigestRenderPolicy(ImmutableArray<string> RenderedSectionCodes,
    ImmutableArray<Guid> UpdateProjectIds, ImmutableArray<Guid> StaffDirectReportIds,
    ImmutableArray<Guid> StaffAssignmentNotificationIds,
    ImmutableArray<NoticeDigestActivityScope> UpdateActivity);
public sealed record NoticeDigestRenderedPayload(Guid SlotId, NoticeDigestWindow Window,
    string Subject, string BodyText, NoticeRequiredScope Scope,
    NoticeRenderedMembership Membership, NoticeDigestRenderPolicy RenderedPolicy);

public enum NoticeDigestWorkKind
{ Compose, Recompose, DispatchExisting, InFlight, Uncertain, Terminal, Dormant }
public sealed record NoticeDigestSlotDue(Guid SlotId, NoticeDigestWorkKind Work,
    string State, DateOnly CreationDate, DateTimeOffset ScheduledAtUtc,
    NoticeDigestWindow Window, int CompositionVersion, int ExternalAttempts,
    DateTimeOffset? NextCheckAtUtc);
public sealed record NoticeDigestDue(Guid UserId, DateOnly Today, string Frequency,
    string CreationDecision, NoticeLocalSchedule? CreationSchedule,
    DateTimeOffset? CarryForwardFrom, ImmutableArray<NoticeDigestSlotDue> Existing,
    bool HasMoreExisting);
public enum NoticeDigestCompositionDecision
{ Queued, Empty, HeldNoDigest, Dormant, NotEligible, InFlight, Uncertain, Exhausted, AlreadyPrepared }
public sealed record NoticeDigestCompositionResult(NoticeDigestCompositionDecision Decision,
    Guid? SlotId, int CompositionVersion, Guid? EmailId, ImmutableArray<Guid> RenderedIntentIds);

// C-owned methods on NotificationDigestComposition:
// public static Task<NoticeDigestDue> ReadDueAsync(NoticeRecipientContext context,
//     DateTimeOffset nowUtc, CancellationToken ct = default);
// public static Task<NoticeDeliverySlot?> GetOrCreateDueSlotAsync(NoticeRecipientContext context,
//     DateTimeOffset nowUtc, CancellationToken ct = default);
// public static Task<NoticeDigestCompositionResult> ComposeAsync(NoticeRecipientContext context,
//     Guid slotId, DateTimeOffset nowUtc, string baseUrl, CancellationToken ct = default);

// The sole new Digest overload, on the existing Digest class:
// public static Task<NoticeDigestRenderedPayload?> Build(HubDb db,
//     NoticeDigestBuildRequest request, CancellationToken ct = default);
```

CreationDecision is a closed proposed result code (Due, BeforeTime, NotCreationDay, Disabled, AllSectionsOff, HeldAway, PendingAvailability, RecipientInactive, NoAddress, ExistingSlot, Dormant); D/root may align the spelling once. These are scheduling observations, not permission receipts or transport results. Exact core policy decides channel/follow/access eligibility per held obligation. Existing results distinguish compose/recompose from dispatch/retry, unresolved I/O and terminal history; there is no bool that conflates all of those as “due”.

Request.Window must come from the loaded recipient-owned Digest slot, not LastDigestAt or an HTTP caller. SlotId/user/window are bound to D's preparation ticket and rechecked by reservation. Request.ReadAtUtc is the server composition instant, with Today from Clock.Local(ReadAtUtc,current settings). Timestamped histories use `From < occurred/created <= Through`; dated/live sections use canonical current Build day/state rules and current authorization. Recomposition on Tuesday can therefore have current dated facts while retaining Monday's exact history cutoff. It does not reconstruct Monday's entity state or widen the captured window. A clock earlier than the captured Through is not admitted as a new composition.

## Shared Build core and exact rendered scope

Keep legacy `Digest.Build(HubDb,Guid,string,DateOnly,DateTimeOffset,DateTimeOffset,OrgSettings,string,IReadOnlySet<string>?)` and public Row/Section/Updates/Result unchanged. Both wrappers call **one** existing-policy core. Legacy mode supplies no enhanced context and retains its old timestamp predicates, subject/body/ordering, dedup, section caps, broad RequiredProjectIds and job behavior. Enhanced mode supplies the typed request and a private transient render ledger. No second copy of20 LINQ section policies or a parallel digest builder.

The private ledger records the trusted project/planning scope alongside each producer's output facts, before those facts lose their identity. Make registers provenance only after the off check and canonical fresh-item dedup; final assembly accounts for what it actually prints. Every printed Row requires its own source scope. **Printed Total/“more” counts also require every actual contributor to that count**, including omitted rows: an eleventh P2 row behind a visible total11 is P2 information even if only10 P1 rows print. Truly unused/off/filtered/deduplicated facts with no printed aggregate contribution add nothing. Preserve existing bounded-count truth, including planning's Cap+1 query and updates'2000-row source bound; this slice does not silently replace them with new global analytics.

Staff summary count scopes retain the projects of the overdue/blocked/stalled facts that actually contribute to each printed direct-report row, not every report task or all visible projects. StaffAssignment rows retain their actual trusted multi-project requirements. Updates require their printed project's identity even if only aggregate counts print. Planning requires each entry contributing to a printed row/count, including a counted Cap+1 overflow row; use PlanningEmailAllowed at current admission/claim. Global Admin review/greeting/footer with no scoped source facts adds no invented project scope. Never derive scope by parsing Row.Project, URLs, names, free text or subject/body. Never attach projects merely because the recipient can see them.

Union validated, nonzero, distinct, deterministically ordered project/planning IDs across that ordinary ledger and exact prepared held lines. Result.Scope is that union. The formatter must fail closed on missing required provenance rather than return a narrower unproven scope or fall back to legacy mail. Only the enhanced payload carries it; current legacy Result.RequiredProjectIds is not edited.

### Initial held rendering and section eligibility

At least one of the existing20 ordinary section switches must be enabled, and DigestEnabled/current policy must permit composition. If all are off, held work remains **HeldNoDigest**, with the existing explanatory policy state; no21st toggle, forced enabling, held-only bypass or immediate burst. The unswitched Admin review line does not defeat this gate. An enabled configuration with no ordinary rows may still render otherwise eligible held lines; that differs from all sections being disabled.

Select at most10 eligible **parent intents**, ordered CreatedAt then Id, after current per-obligation policy. Render one explicitly labelled line per parent; no grouping across parents. A scalar line reserves that exact intent. A compound line carries exactly its admitted remaining keys and their origin hash; its distinct changed-task count is derived from those keys by D's proof logic. A parent with no admissible keys does not occupy a held line. No ordinary same-item row/header/count reserves a held intent. The new held header counts rendered parent lines only; do not print an unscoped backlog total. Overflow and “more” text never reserve omitted membership; no such dynamic held-overflow count is needed for the initial contract.

Payload.Membership.IntentIds is precisely the distinct rendered parent set (<=10), with exactly one bulk membership entry for each rendered compound parent and none for scalar/unrendered work. Not every candidate is rendered by definition: validate actual output membership after formatting, not candidates before it. No accepted/simulated/unknown/uncertain/currently reserved keys are re-rendered as fresh held work. Internal scope/text data may exceed a source cap only by refusal, never truncating obligations or widening their meaning.

## Transaction, immutable window and budgets

Caller owns one ReadCommitted Tx.Run, acquires D's context, reloads current policy, and calls C. ReadDueAsync is side-effect-free and is shared by DigestJob/Operations. New weekly creation is **only actual organisation-local Monday**, at/after the resolved chosen time. A missed/away Monday creates no new Tuesday slot. Daily creation honors the existing weekend preference. Creation uses unique(user,Digest,creation-local-date), frequency captured once; edits cannot produce a second slot on that date. Existing captured slot schedule/window remains immutable despite later time/frequency/zone changes.

ReadDueAsync classifies existing slot work separately before applying new-slot day/time/watermark rules. Tuesday may safely recompose Monday's slot or dispatch/retry its pinned email under D's current policy. It is not a new Tuesday weekly slot. The legacy weekend early return, current chosen-time check or advanced LastDigestAt must not hide that existing work. Existing work still obeys current recipient/policy/hold/uncertainty/backoff/budget gates; scheduling classification does not authorize sending.

GetOrCreateDueSlotAsync rechecks the due/policy under the same lock. On creation, capture Through=nowUtc and From=the prior preparation watermark, or preceding24h Daily/7days Weekly when absent, **before** advancing that watermark. Incorporate safely unsent uncovered carry-forward From without rewriting an old slot. Never substitute a newer LastDigestAt during recomposition. Do not include newer-than-Through occurrences; held eligibility can include older-than-From pending overflow at/before Through.

Carry-forward needs a source-backed coverage rule, not an eternal minimum of every historical Exhausted window or LastDigestAt alone. Proposed rule for root/D review: a later concrete valid slot whose immutable window contains an earlier safely exhausted window owns that coverage; if it safely exhausts, its own From carries it onward. Queued/in-flight/uncertain/accepted/simulated/unknown history must not be treated as safely unsent replay authority; a conclusive Empty composition may close a truly empty preparation range without pretending mail acceptance. Core must establish safety from matching persisted payload/state/uncertainty facts. If root cannot implement this derivation unambiguously with current columns, freeze an explicit root-owned coverage marker instead; no guessed acceptance or new table by C.

D's proposed reservation seam is still to freeze against its core doc:

```csharp
// D-owned opaque NoticeDigestPreparation binds context/recipient/slot/window/next version/candidates.
// public static Task<NoticeDigestPreparation> PrepareDigestAsync(NoticeRecipientContext context,
//     Guid slotId, int maxParents, CancellationToken ct = default); // maxParents must equal10 here
// public static Task<NoticeDigestCompositionResult> ReserveDigestAsync(NoticeRecipientContext context,
//     NoticeDigestPreparation preparation, NoticeDigestRenderedPayload payload,
//     CancellationToken ct = default);
// public static Task<NoticeDigestCompositionResult> CompleteEmptyDigestAsync(NoticeRecipientContext context,
//     NoticeDigestPreparation preparation, CancellationToken ct = default);
```

ComposeAsync asks D to prepare, builds once with the immutable window/current read instant and admitted candidates, then passes the actual rendered payload to D. ReserveDigestAsync validates same-user/slot/window, exact subset membership/hash/key scopes, absence of another parent pointer, policy witness, version and safety. It atomically inserts a new immutable linked email, reserves only actual rendered memberships, advances composition once and updates preparation bookkeeping; one outer Save/commit convention is frozen by root/D. No source/app/intent/email/receipt is partially committed when admission/encoding/formatting fails. No caller-provided recipient/address/scope or current app count can substitute for trusted facts.

Initial payload version1, at most3 total compositions; the slot's aggregate8 external sender reservations never resets across payloads. C never claims/sends, increments attempts, clears uncertainty or releases pinned work itself. D alone admits safe pre-send suppression/release/recomposition. A previously InFlight/sticky-uncertain payload cannot release **any** membership or replace its immutable bytes; only D may consider the same pinned payload's explicitly admitted current-policy retry. Safe exhaustion retains allowed held work/window for a later eligible slot; it is not a budget reset. Accepted payloads are not retried; Log/unknown success is not actual email and is not automatically replayed after a transport switch.

If an enabled build has neither ordinary content nor rendered held lines, CompleteEmptyDigestAsync records Empty/preparation progress with no fabricated email or reservation; composition remains0 if no payload was ever composed. Disabled/all-off/away/unknown policy is a hold, not Empty. LastDigestAt remains preparation bookkeeping, never EmailedAt/DigestIncludedAt/ProviderAcceptedAt evidence.

## DigestJob, reminder and Operations integration

DigestJob's first branch checks current people_screens. Off executes the existing unlinked job path exactly; linked dormant work never falls through it. On selects bounded active recipients plus existing due work through a root/D-frozen cursor, enters a recipient transaction, reads the shared due result, processes the selected existing safe composition work and optionally creates today's eligible slot. Claim/dispatch of already queued emails stays in D/EmailJob, not DigestJob. Filter terminal/dormant/not-yet-due candidates before bounded selection so100 inert rows cannot starve actual work; expose HasMoreExisting rather than calling a truncated read complete. Root freezes exact job-run statistics; legacy `sent` is a queue count, not a new real-delivery claim.

B's reminder eligibility is separate: current local week only, once user/TimeReminder/localMonday, own app+Emitted atomically, no email/Intent/quota/capacity/Time-hours reads. It consumes the same NotificationDelivery.ResolveLocal/WeekMonday/context. Its proposed exact public reader is `Task<TimeEntryReminderDue> TimeEntryReminderJob.ReadDueAsync(NoticeRecipientContext context, DateTimeOffset nowUtc, CancellationToken ct = default)`, with Due/NotDue/Disabled/AlreadyEmitted/Unavailable distinctions. Root Operations consumes the same C digest due reader and this B reader. It must not duplicate offset math, mark a weekly user late merely because Tuesday has no new slot, count deliberate holds as failures, or equate SentAt/configuration with real acceptance. The existing08:00 alert policy remains a legacy source fact; enhanced severity/grace/“late” policy needs root's explicit integration, not an invented new threshold in C.

## Freeze items and meaningful future acceptance

Two genuine seams remain explicit: **ordinary rendered-policy persistence** and **carry-forward closure**. D acknowledges the policy gap and proposes root-owned nullable `EmailMessage.NoticeDigestPolicyJson`, closed version1, separate from BulkRenderedMembershipJson. C's RenderedPolicy above supplies the actual output dependencies: codes, update-project IDs, printed staff-summary direct-report IDs, own StaffAssignment notification IDs, and contributing update activity IDs grouped by project (counts included). Those are declared IDs/facts, not a general predicate language or arbitrary caller-provided policy.

At claim, ordinary section switches apply only to actually rendered codes, while each update project must still be followed at AllActivity. Existing already-off/unrendered codes cannot trigger an endless recompose. Staff aggregate eligibility currently uses **Users.SupervisorId==recipient AND IsActive, with no explicit Supervisor role gate** in Digest.Build; reuse that exact predicate for StaffDirectReportIds, not a new ActOnStaff/edit-role rule. StaffAssignment notice lines instead retain exact own-notification/current-scope provenance and must not be relabelled as report aggregates. `Digest.VisibleActivity` also enforces TimeEntry readership (own/PM/lead/supervisor/direct report) and private Calendar exclusion; project access and AllActivity alone do not prove those contributions remain visible. Reuse that existing canonical visibility against the stored contributing IDs rather than inventing another privacy policy or parsing the body. Source-field projection/redaction flags and strict witness byte/count admission remain explicit **not-yet-frozen D/root details**: a visible activity ID alone does not certify every originally projected field. No C source/schema/extra shared-helper edit is implied.

Root freezes the exact witness DTO/codec/private field/immutable guard/migration and bounded refusal behavior, plus the coverage derivation described above. C's payload field is now RenderedPolicy rather than an unpersisted bare RenderedSectionCodes array. Neither C nor D silently repurposes a bulk hash, treats current configuration as historical evidence or marks these holes complete. No source loan follows merely from this document being READY.

| Future provider/fake-only case | Required evidence |
|---|---|
| Legacy parity | Existing Build/Job off results and20 section switches, severity dedup/caps/subject/body/empty watermark behavior; WeeklySummary untouched. No linked payload enters legacy, and no real sender is invoked. |
| Exact ordinary scope | Visible unused project excluded; printed rows and multi-project staff/notification/update aggregates included; a capped-out eleventh P2 row that contributes to printed Total11 still requires P2. Off sections/unused facts add nothing. Planning Cap+1 contributors require actual planning IDs. No URL/text inference. |
| Window boundary | From excluded, Through included, newer activity/staff notices excluded; old held overflow eligible. Advance LastDigestAt then recompose: same slot/window includes earlier ordinary history and excludes new events. Dated state is current, never advertised as historical reconstruction. |
| Held membership |11 parent intents render exactly10 unique labelled parent lines, no grouping; ordinary same-item line consumes none. Two bulk parents with different eligible key subsets carry exact maps/counts/scopes; partial refusal never edits original app visibility/title or revives terminal keys. |
| Config/empty policy | All20 off including Admin inline possibility yields HeldNoDigest/no auto-enable/no mail. Some switches enabled plus held-only content can compose; genuinely no content yields Empty/no fabricated email. Missing provenance or malformed policy refuses atomically. |
| Window/budget safety | Safe ACL/section/planning withdrawal recomposes within the same3/8 slot limits; a previously off unrendered code causes no recompose loop. Current AllActivity/report relationship/activity privacy rechecks the stored output dependencies. Uncertainty or orphaned InFlight never releases;8 sender reservations total across versions. Exhausted safe coverage carries once and closes through a proven later slot, not forever; uncertainty is never called unsent. |
| Weekly/day/DST | Monday creation/Tuesday same-slot retry; no new Tuesday weekly slot or prior-week reminder. Away/missing capacity/zero-normal distinctions use the separate lazy canonical hold policy. Base ReadRecipientPolicyAsync and reminder scheduling read neither capacity nor Time hours. Gap first-valid/ambiguous earliest-UTC, invalid-zone fallback, captured HH:mm basis/actual instant and edits retain one slot. Job/Operations/reminder agree on shared schedule facts. |
| Atomicity/concurrency/privacy | Same-recipient competing jobs/opt-out/source transaction cancellation; one current pointer and immutable membership/window/body; no partial watermark/mail/reservation after fault. Cross-user/project/planning/malformed map fails closed. Large bounded candidates do not starve work or scan the whole user directory in a per-recipient build. Logs/Unknown are never actual acceptance. |

Everything in this table is **UNRUN**. Proposed focused filter is `FullyQualifiedName~NotificationDigestCompositionTests`, plus the existing legacy digest/weekly/planning/privacy checks justified by the exact touched seams after root freezes source. Root owns validation, mail-fake wiring, model parity and any later local closure; no Accepted/task checkbox/decision status changes here.
