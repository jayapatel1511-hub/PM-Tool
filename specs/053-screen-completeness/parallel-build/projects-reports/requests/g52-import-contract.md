# Generation 52 atomic drawing intake source contract

Source authoring only. The reviewed pure parser/test are READY/HOLD; C now owns
only `DrawingImport.cs`, `DrawingImportRows.cs`, and `DrawingImportTests.cs` in
the structured CONTROL. Root owns creator, receipts, middleware, module, UI
integration, locales and generated contracts. No execution while writers run.
S34 remains open until full CSV/XLSX import and export acceptance.

## Fixed route/body contract

- GET `/projects/{projectId:guid}/deliverables/import-options`:
  projectRowVersion; correctionReasonRequired; canImport; active project disciplines
  with real project-discipline ID/name/canCreate; active deliverable types ID/name.
  Return only normal project-visible configuration; no new directory scope.
- POST `/projects/{projectId:guid}/deliverables/import-preview`:
  `{format:"csv"|"xlsx",contentBase64,worksheet?,sourceReference?,defaults?:
  {projectDisciplineId?,deliverableTypeId?},reason?}`.
  Return source/sheet/hash/fingerprint/projectRowVersion, parser diagnostics and all
  row errors/warnings, Creates/Errors/CanCommit and normalized effective create values.
  Optional sourceReference is provenance, never required identity. Ordinary forty-row
  title-only files work with explicit discipline/type defaults. No preview persistence.
- POST `/projects/{projectId:guid}/deliverables/import`: same input plus
  `{previewFingerprint,projectRowVersion}` and required bounded Idempotency-Key.
  Return created rows `{sourceRow,id,key,rowVersion,projectDisciplineId,warnings}`
  and total. No upload bytes/raw file in result or receipt. Exact typed names may
  be chosen consistently; send final DTO shape to root before frontend work.
- Both POSTs carry `new DrawingImportReceipts.AtomicRequest()` metadata. All new
  routes require `register_screens` in handler and route metadata; preview with an
  Idempotency-Key must remain completely read-only. Bound encoded body before base64
  decoding and add an appropriate endpoint request-size limit (~3MiB for2MiBXLSX).
  No implicit file storage, no schema/dependency/operational mutation.

## Shared root interfaces (authored, uncompiled)

`DeliverableEndpoints.PrepareCreate(projectId, CreateBody, access, db, store)` returns
read-only `CreatePlan`: normalized body/project/context/discipline/owner/milestone,
effective due/client number/settings. It shares manual validations, including dates
and lead fallback. It does not reserve keys or add members. The manual endpoint is
now a wrapper over this same plan/apply path. Import rejects FromTask entirely.

`DeliverableEndpoints.CreatePrepared(plan, access, db, team, notify, importSnapshot?)`
returns the normal `DeliverableCreated`. It reuses the ambient transaction; normal
keys, membership, Created log, assignment publication and notification/outbox remain
in it. Snapshot is attached to that one Created event before its first save. All
batch plans must be prepared/revalidated under the commit lock before ANY apply.
Do not call HTTP, independently reimplement creator, or apply as you parse rows.

`DrawingImportReceipts.Execute<T>(projectId, payload, http, access, db, store, clock,
createCallback(Project,ProjectContext), replayAuthorise(T,Project,ProjectContext))`
serializes user/key across projects then locks the project; rechecks flag/project and
Writable; hash-binds the exact path/body and atomically stores a successful result
in the existing one-day IdempotencyRecord with all business effects. Replay rechecks
today's per-discipline CreateDeliverable authority via the callback before returning
the exact committed result; no stale preview/version test on an already committed
same-body retry. Different body/path/key misuse refuses. Expired keys are new intent,
not permanent cross-file deduplication. No provenance table/new migration.

## Validation and fingerprint

Use the full g51 contract for canonical headers and CSV/XLSX/date/text/unknown handling.
No fuzzy aliases, display-name identity, guessed defaults, issue/history/status
imports, updates/deletes/restores. Every nonblank row checked before canCommit.
Include hidden rows with warning; explicit worksheet choice for multiple visible
sheets; reject unsafe/formula/numeric-identity cells and unsupported nonempty columns.
Keep all parser diagnostics with real source locations; one bad title in40 blocks all.

Resolve exact active discipline/type, active email IDs, same-project milestone.
Use normal creator ownership/permissions. Import additionally preflights restricted
auto-membership: a DL cannot assign nonmembers where EnsureMember would require PM.
Check both owner/reviewer before promising canCommit. Complete project intake needs
the normal correction reason; preserve manual-create behavior separately. Do not
allow a global role to invent project authority or relax ReadOnly/restricted denial.

Fingerprint content/format/sheet/defaults/source reference/normalized effective values,
project version/numbering and resolved references+versions/authority facts affecting
the preview. Recompute under the commit transaction. Missing baseline428, stale409;
all row errors refuse all before keys. No keys promised by preview. Possible duplicate
title/client-number warnings are bounded hints, not identity/Exists/upsert. No false
permanent deduplication promise. Same key exact committed retry returns original IDs.

Set Source=Import for actual creates. Bounded per-created snapshot may contain batch
ID,row,contentHash,format,sheet and supplied sourceReference/sourceRowKey. No file,
invented external author/time, extra per-item Imported event or batch summary needed.

## Smallest meaningful source test set

Both formats:40 rows/one blank title preview+directcommit creates0; corrected file
creates40 normal records/keys/Created logs with no issue evidence. Whole transaction
rolls back late failure including keys,members,notifications,private assignment
publication and receipt. Same-key retry/concurrent/lost-success yields one set;
changed-body/path reuse refused; preview creates no receipt. Current authority/flag
rechecked on replay. PMtwo disciplines, DLown-only, mixed unauthorized, ReadOnly,
restricted nonmember assignment, removed membership/lead, archived and Complete reason.
Changed reference/version/fingerprint requires preview; warnings(C12pattern) nonfatal;
duplicates never mutate existing/soft-deleted records. Original manual create and
client export stay unchanged. Root runs tests after all writers READY/HOLD.
