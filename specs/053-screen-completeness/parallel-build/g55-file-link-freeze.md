# Generation55 — S44 file-link contract freeze

Root freeze, 2026-11-02. Implements FR/AC-SCR-77 and relevant AC-SCR-84 under
`people_screens`. The reviewed A proposal at
`home-visual/requests/g54-file-link-context-contract.md` governs semantics except
the explicit wire/interface refinements below. S44 is not implemented yet.
Checkpoint26 is closed locally for R2; S35/R3 and both unanswered questions remain
open. No dependent external-review implementation is authorized by this freeze.

## Scope and state precedence

Preserve the existing Files/Links design, HTTP(S)/UNC actions, ownership and
inheritance. No upload, connector, remote fetch, content verification,
SourceRevision/SourceHead mutation or new permission. All source registration is
explicit/manual; actual original AddedBy/AddedAt never change.

Root adds exactly six nullable DocumentLink fields: SourceSystem(max200),
StableSourceId(max200), DeclaredRevision(max50), ReferenceRegisteredBy(Guid?),
ReferenceRegisteredAt(DateTimeOffset?), ReferenceDeliverableId(Guid?). No backfill.
All-null is legacy/unregistered. Registered values require nonblank SourceSystem,
actual authenticated registrar and one server instant; target may be null.
Root owns nullable FKs, complete-registration check, registry, migration/snapshot.

Comparison states are NotRecorded, NoDeliverable, TargetChanged, TargetUnavailable,
CurrentNotRecorded, Matches, Different. Unknown/missing never means a match.
For a registered reference with a declaration, **Task parent changes precede
NoDeliverable, including NULL→ID and ID→NULL**. Require explicit re-registration;
never compare against a new parent. No declaration remains NotRecorded. Invalid,
foreign/deleted/unreadable targets expose no hidden ID/name/revision or rights.
Known trimmed revision labels compare ordinally, without inferred ordering.
Matches is a neutral manual fact; Different uses the existing warning treatment
and the exact “Declared C, current D” meaning. RowVersion is not a drawing revision.

Keep `Owner().DeliverableId` unchanged: its null for a direct Deliverable prevents
self-inheritance. A separate reference resolver captures direct Deliverable ID,
Task's current coherent same-project deliverable, or null for Project/standalone
Task. URL changes invalidate all six fields unless a complete new reference is
explicitly supplied, including old URL edits while the flag is off.

## C-owned typed shared reference contracts

Use uniquely prefixed public records in new `DocumentLinkReferences.cs`:

- `DocumentLinkReferenceInput`: nullable/default-null sourceSystem, stableSourceId,
  declaredRevision. Preserve unknown nested properties with JsonExtensionData so
  the enabled validator rejects them; never accept registrar/time/target/verified
  fields. Usual malformed JSON binding can refuse400. Valid feature-bearing bodies
  refuse404 while off. Append optional null-omitted AddBody.Reference at the end.
- `DocumentLinkReferenceUser`: id, displayName, isActive; only an actual matching
  User gets this descriptor. No email/person facts or guessed User IDs.
- `DocumentLinkReferenceValue`: sourceSystem, stableSourceId?, declaredRevision?,
  registeredBy, registeredByUser?, registeredAt, registrationKind="Manual".
- `DocumentLinkReferenceTarget`: id, key, name, status, currentRevision?, rowVersion.
- `DocumentLinkReferenceComparison`: state, declaredRevision?, currentRevision?.
- `DocumentLinkReferenceContext`: reference?, target?, comparison. This contains
  presentation only, no CanChange grant. A changed/unavailable comparison target
  must not display a new/foreign revision as the registered comparison.

Append optional null-omitted **LinkRow.ReferenceContext**; reuse that same context
inside Files metadata. It is omitted off, including stored-on values. Command
LinkResponse remains its old five properties. No new endpoint is needed.

Expose existing CanAdd/CanChange as **internal**, with their bodies unchanged for
the feature read helper. Keep commands authoritative. The shared public `Add`
creator gets an optional `bool referenceEnabled=false` at the end: **it must reject
nonnull new reference input when false**, protecting every existing producer,
not just the HTTP route. The HTTP route passes the actual flag. Old producers
continue with omitted/null references and unchanged replies.

Shared helper interface for B/C (keep these names/signatures; propose refinements
to root rather than editing another lane):

- `DocumentLinkReferences.Flag = "people_screens"`.
- `Task Apply(HubDb db, DocumentLink link, DocumentLinkReferenceInput input,
  Guid actorId, DateTimeOffset now)` validates/replaces all six fields atomically.
- `void Clear(DocumentLink link)` clears all six without changing AddedBy/AddedAt.
- `Task<Dictionary<Guid, DocumentLinkReferenceContext>> ReadContexts(HubDb db,
  IReadOnlyCollection<DocumentLink> links, IReadOnlyCollection<Guid> readableProjectIds)`
  batches actual owner/target/registrar queries, validates coherence and returns
  one context per shown link. Caller supplies already-authorized project IDs;
  the helper never broadens them. No per-link database queries or writes.

## B-owned Files read

Existing Files handler keeps its exact off branch. New raw-string query arguments:
disciplineId, addedById, addedThisWeek. Off ignores malformed new controls. On,
empty means absent, IDs require nonempty single GUIDs and week accepts exact
true/false. Apply actual global Discipline.Id, actual DocumentLink.AddedBy and
org-local Monday-through-Sunday AddedAt through the captured asOf before2000 cap.
Stable title/ID order. Scope/privacy/owner-project coherence precede figures.

Append optional null-omitted `FilesRead.Screen` with these records:

- `FileScreenRead`: asOf, orgTimeZone, weekFrom, weekThrough, applied,
  disciplines[], disciplinesTruncated, authors[], authorsTruncated, links[],
  missingDeliverables.
- `FileScreenApplied`: disciplineId?, addedById?, addedThisWeek(bool).
- `FileScreenDisciplineChoice`: id, name, isActive (global discipline ID).
- `FileScreenAuthorChoice`: id, displayName, isActive (actual visible link author).
- `FileScreenLinkContext`: linkId, rowVersion, canChange, discipline?,
  referenceContext(DocumentLinkReferenceContext).
- `FileScreenMissingDeliverables`: total, truncated, items[]. Each
  `FileScreenMissingDeliverable`: id, projectId, projectNumber, projectName, key,
  name, status, disciplineId, disciplineName, currentRevision?, rowVersion,
  canAddLink. DisciplineId here is global, consistent with the filter.

Choices derive from scoped coherent items/links, independent of selected link
filters; cap200+sentinel each. Missing is an independent direct-live-link anti-join
over canonical nonterminal deliverables, not “no filtered links.” Scope,
discipline and deliverable-key/name search apply; link type/author/week do not
change membership. Count exactly, return stable project/key/ID first2000+sentinel,
show truncation honestly. Task/project links do not satisfy a direct deliverable
link. Any live direct link does, regardless of metadata or current link filters.

Read counts/options/rows/reference contexts from one repeatable snapshot. Reuse
the canonical permission functions. Batch ProjectContext member/lead facts using
the same inputs as Access.Context; avoid new permission rules or per-item context
queries. Existing off ordering/count meaning, types, AddedByUser and visual colour
projection remain untouched. No Seen/private-visit/business writes.

## UI ownership and shared components

D owns existing `components/hub/links.tsx` and new
`components/hub/document-link-reference.tsx`:

- Export `LinkReferenceSummary({context,focusKey})` for A and LinksPanel. Uses
  generated DocumentLinkReferenceContext, honest states/manual provenance and
  known-User registrar triggers with unique source keys. No new match colour.
- Export `LinkReferenceEditor({link,onClose,onDone})`; link is a Pick of generated
  LinkRow id/title/rowVersion/referenceContext. PATCH only reference replacement
  or null with the version actually shown. No fresh-version promotion after
  refusal/uncertainty, no inherited mutation. Cancel/guard/focus follow existing
  patterns; do not invent a Person-navigation exception.
- Add optional explicit source-registration fields to flag-on AddLink, retaining
  the old form/body/behaviour while off. Preserve HTTP(S)/UNC detection, original
  rights and callbacks. Use existing request/idempotency facilities for uncertain
  creates; never silently duplicate a POST or re-register with a fresh version.
  No new library/global guard or actor fields in the request.

A owns existing Files.tsx plus new `file-screen.cjs`/`file-screen.fixture.cjs`.
Reuse the current grouping/layout and D components. Metadata aligns by actual
link ID/version from one live envelope. Missing rows use actual Deliverable
inspectors and canonical AddLink; preserve exact URL/grouping/scroll/focus/file
actions and known-ID author Person triggers. Query keys separate viewer/flag/
scope/filter; errors do not show refused cached metadata. Show selected unavailable
choices removably and explain which filters apply to the missing section.

D also owns new `document-link-reference.cjs`/fixture for creation/replacement/
clear/inheritance/uncertainty/Cancel/phone/off. Use typed future generated records;
no shadow DTO/any or weakening strict request guards. Test references must cite
AC-SCR-77/84. Root owns text, registry/cache/generated and test inventory.

## Execution boundary

All four lanes may author only their exact loans. Root is the single schema and
integration writer. No .NET, EF, generation, production build, API, browser or
test execution until relevant writers are READY/HOLD and root freezes inputs.
No helper agents, dependencies, operational flags, commit/push/deploy/Accepted.
Old QA24 and all prior outputs remain preserved pending explicit cleanup approval.
Root reviews source, generates one additive migration, executes the smallest
meaningful provider/Chrome/off checks and records final evidence before S44 closes.
