# Checkpoint 23 — local verification, generation 51

**PASS: S10 locally checked;31/49.** S43 and S34 remain open. No commit, push,
deployment, pilot activation or formal Accepted status. The prototype and primary
checkout are preserved.

## Implemented and verified

- Private assignment/reviewer occurrence evidence, exact-token acknowledgements and
  atomic lifetime request receipts. Preferences retain first/previous/last visit and
  mark-all times; no private state leaks through another person's reads.
- A visible own My Work visit, explicit mark-all across the eligible unfiltered
  snapshot, and visible exact-record detail opening. Prefetch, hidden/cancelled or
  denied reads do not acknowledge work. Uncertain retries retain the request ID and
  immutable payload/cut; late assignments remain New.
- One batched activity/comment read per authorized page union. Event time and actor
  come from the same event. Overview, tasks, Watching/Requested and Coordination use
  typed evidence; Comments opens the exact source record. All nine detail readers
  and assignment producer paths are covered by API checks.
- Wider passive User names across Team/Organisation, history/comments, planner and
  coordination surfaces retain source focus, scroll, drafts, tabs and native folds.
  This is progress toward S43; Search/Reassign and the final global inventory remain.
- Client register CSV/XLSX export explicitly projects eight allowed fields and the
  recorded current issue bundle. Atomic drawing-list import remains unimplemented.

## Evidence

All paths below are in [checkpoint23-runtime](checkpoint23-runtime/).

- API/solution builds PASS after compiler repairs. The additive migration
  `20261102045615_PrivateWorkSeenLedger` includes a reviewed truthful baseline of
  twelve roles, including version-zero records and hidden/closed legacy records.
  Upgrade verification passes against a disposable test database. No guessed source
  event/time is introduced by baseline seeding.
- **117 distinct .NET checks PASS**: foundation28, affected71 with12 overlap, plus
  Contract/Registry30. Final affected export/envelope checks overlap the prior sets.
  Initial compiler, golden-update and synchronous race-harness failures remain in
  raw logs/TRX; actual async lock-race rerun and final groups pass.
- Node cache18 PASS; app/fixture types PASS; lint PASS with warnings; SSR PASS;
  production build2 PASS. Legacy ClientRow/Facts schemas stay unchanged after naming
  new DTOs uniquely. Contract delta removes zero schemas and adds4paths/7schemas.
- Four actual Chrome suites PASS: seen4 (10 observations), PersonOrganisation5
  (5 observations/18Personreads), PersonSurface9 (15 scenarios/18Personreads),
  Time1 (named hatch/off-on checks plus existing week-grid interactions). Synthetic
  writes are explicitly separated: PersonSurface has1privatevisit/1preview and
  zero business writes. Other suites retain their own exact request assertions.
- [real-seen-browser1.json](checkpoint23-runtime/real-seen-browser1.json): actual
  builtSPA/QA23 API/database. First visit baselines old work; a second visit preserves
  New; opening acknowledges exactly the displayed tokens and request ID; mark-all
  includes the assignment hidden by the selected project. Nine canonical table
  contents and work-event counts remain unchanged by the4personal commands. Foreign
  view at375px omits seen state/controls and sends no personal command.
- [real-person-export-browser1.json](checkpoint23-runtime/real-person-export-browser1.json):
  two real time entries have distinct User openers and natural focus return at1440/375.
  Actual filtered menu downloads in both formats have eight columns, client number
  `00123`, current issue revisionA/purpose/returnB and exact date. Internal people,
  notes, working revision and reference metadata are excluded; canonical detail is
  unchanged. One private Project-header open is recorded separately; zero non-GET
  business requests. Existing Exported audit entries are expected for downloads.
- `final-source-parity1.json`:593 compiled C# inputs unchanged. `final-web-app-source1.json`
  captures254 final app/config inputs. The eleven deltas from pre-build1 are the six
  reviewed build2 cache partitions and five subsequent browser fixture/test repairs.
  [final-served-assets1.json](checkpoint23-runtime/final-served-assets1.json) verifies
  all130 manifest assets/index bytes against the actual preview.
- Combined spec up to date; trace1546IDs/438sections/zero unknown; decision checks8
  PASS; whitespace check PASS. Only user-confirmed052-D11/D12 changed decision status.

## Repairs and limits

The initial browser failures are preserved. Test repairs use actual view controls,
coherent current-round/date fixtures and the real inner dialog scroll container.
Strict unknown-read/business-write guards remain. Native document visibility uses
a fresh owned Chrome profile/default context with CDP `noDefaults`, actual tab
switching and owned process cleanup; no visibility property is spoofed.

Independent source reviewers approved the backend/read bridges, cache/lifecycle UI,
Person repairs and final S10 local acceptance. The pure CSV/XLSX parser draft received
a text-escape correction and source PASS; it remains uncompiled/unrun and does not
establish atomic import, replay safety or S34 completion.

S43 global completeness, all other unchecked slices,052 broad census/contrast/native
device/200%zoom gates, physical SSE transport and hosted/human/pilot acceptance remain
unproven. Open decisions are not silently approved by dark local implementation.
QA23 uses only a newly created synthetic database with its ownership/startup/cleanup
receipt. Source defaults and operational flags remain unchanged.


QA23 cleanup PASS: identity-verified API77813 stopped with SIGINT; only newly created hub_qa_checkpoint23 dropped after zero-client check. Existing testPG55432/preview8853 retained. See evidence/checkpoint23-runtime/qa23-cleanup1.json (relative to packet root).
