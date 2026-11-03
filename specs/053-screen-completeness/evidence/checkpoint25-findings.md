# Checkpoint25 — Search and the review findings read side

**Generation53, 2026-11-02. Local verification; new behaviour remains default-off.**
S46 implements FR/AC-SCR-79 with relevant FR/AC-SCR-84 regression coverage.
S35 is **partial R1 only**: its cross-package findings reader and UI are verified;
response outcomes, response-sheet export and external provenance remain required.
Nothing here establishes whole-packet, hosted, pilot or human acceptance.

## Implemented behaviour

- Search offers server-defined project, status and discipline filters where each
  actual record type supports them. Counts use the filtered cohort before paging;
  unsupported filters are removed on tab changes. An explicit incompatible URL
  remains an honest error with a way to clear it. Canonical milestone completion
  takes precedence over cancellation. All sixteen backend record types are tested.
- People search includes actual job title and home discipline without reading
  private person facts. Exact User identity, work visibility and Person return
  behaviour remain intact. Search keeps no main-sidebar item selected (052-D12).
- The last ten searches are local to this browser and actual viewer. Storage is
  bounded, deduplicated and safe under malformed/denied storage. Clear reports
  actual storage success. Filter/page/Person navigation does not manufacture a new
  search. Failed reads remove cached result names, counts and Person affordances.
- The separate project findings register defaults to unresolved current-round
  findings, including blocking withdrawal acknowledgements. Explicit All rounds
  includes historical unresolved rows with real carry identities. Four filters,
  counts/options before paging, real provenance, terminal/read-only states and
  actual User versus external-party identity remain distinct. Exact finding
  inspection works with register_screens alone; this adds no command authority.

## Executed verification

Raw artifacts are in [checkpoint25-runtime](checkpoint25-runtime/).

| Check | Result and evidence |
|---|---|
| Isolated solution build2 | PASS, 0 errors / 1,163 existing analyzer warnings; `solution-build2.log` |
| Search provider/API cases | PASS16, `search-screen2.trx`; all supported types, filters, counts, authorization, flag-off and canonical precedence |
| Findings register provider/API cases | PASS24, `screen-readers1.trx`; scope, paging, provenance, actionability, read-only and flag combinations |
| Contract/registry and adjacent API cases | PASS59, `contract-adjacent1.trx`; **99 distinct passing .NET cases** with the two focused groups |
| Pure Node | PASS7 recent-search cases +18 cache cases, `recent-node1.log` / `keys-node1.log` |
| Contract generation | PASS generation plus plain golden test; one added path, fourteen prefixed schemas, two intentional existing-schema extensions, no removals; `contract-delta1.json` |
| Frontend | App/fixture types, lint with existing warnings, SSR review and isolated production Vite build1 PASS; individual logs retained |
| Search Chrome3 | PASS4 observations,55 Search reads,2 Person reads,0 business writes,0 unexpected reads; `browser-search3.json` |
| Register Chrome3 | PASS7 cases,23 register reads,1 Person read,0 business writes; `browser-review3.json` |
| Adjacent Search/Person Chrome1 | PASS desktop/375px,20 Search reads,12 Person reads,0 business writes,0 forbidden reads; `browser-person-search1.json` |
| Adjacent finding-context Chrome1 | PASS exact historical round, withdrawal, return scroll and package isolation; `browser-finding-context1.log` |
| Visual capture2 | PASS read-only desktop/375px captures,0 writes/errors/unexpected reads; `visual-capture2.json`; root inspected final Search and findings images |
| Final source/output parity | PASS606 C# files and443 web baseline inputs at browser completion, with only the two observed browser-driver repairs changed;129 new served files match build bytes. Old129 files and old API DLL remain unchanged; `final-source-parity1.json` / `final-served-parity1.json`. Later citation-only changes are separately proved in `post-verification-metadata1.json`. |
| Acceptance test linkage | PASS actual trace parser maps all7 Search API declarations (16 executed cases) and the registered Search browser suite to AC-SCR-79/84; `test-traceability1.json` |
| Specification checks | PASS combined spec freshness,1546 IDs /438 sections /zero unknown,8 decision tests; final whitespace check recorded separately |

Chrome executes the actual isolated production SPA with synthetic API fixtures.
The .NET cases execute real provider/database API paths in factory-owned test
databases. **A new live Kestrel/API/browser join was not run.** Port8854 is a
static verification preview with no API5089; it is not a working review instance
for normal unmocked use. Earlier real QA24 evidence is not relabelled as generation53.

## Failures preserved and repaired

- First focused API run was38/40: metadata tab order differed from the canonical
  Search order, and a planning fixture violated `ck_planning_self_visible`. The
  exact helper/fixture repairs were reviewed; all16 Search cases then passed.
  All24 register cases had already passed. Raw initial TRX/log remain preserved.
- Contract-update execution intentionally reports golden drift while writing its
  new golden; the subsequent normal golden run passed. No endpoint/schema was
  removed to satisfy it.
- Two impossible nested `has` selectors in the new register driver were corrected
  and independently rereviewed before execution. Register runs1/2 then exposed
  closing-popover and controlled-URL synchronization assumptions. The driver now
  observes real hidden/expanded state and URL/native checked/77-row All-rounds
  proof. Existing application and fixture bytes were unchanged.
- Search runs1/2 exposed closing desktop-to-phone controls and a dependent EIT
  search issued before Clear filters had reached its URL state. The driver waits
  for actual Sheet/Popover and cleared URL/chips; expected EIT2/Electrical1 and all
  privacy, focus, storage and request assertions remain intact. Final run3 passes.
- Initial visual capture1 caught a transient repeated header after resizing.
  Capture2 waits for fonts, two animation frames, one actual header and the real
  fixed bottom-navigation rectangle. Final phone image is correct without any
  application change. Both original and settled images are retained.

The initial setup cwd mistakes happened before dependent evidence setup and were
corrected with an explicit build-root path; they are not passing setup evidence.
No global cache policy, command guard or expected count was weakened for a test.

Independent acceptance review caught missing AC citations/test registration.
Root added only XML/comment citations in the API/recents/browser tests and one
`test:search-screen` package script. Exact removal of each addition reconstructs
its tested source hash (`post-verification-metadata1.json`). No test assertion,
application, fixture, dependency or build hook changed; checks were not rerun
merely for comments. The trace parser confirms the actual linkage. The first
mapping helper guessed15 declarations and refused; inspection established the
actual7 declarations expanding to16 cases, and the corrected helper passed.

## Limits and retained runtime

S35 response/outcome/export/external provenance, the other unchecked053 slices,
and broad052 visual/native/200% zoom/human gates remain open. Formal Open decisions
remain Open except Jay's already recorded052-D11/D12 choices. No commit, push,
deployment, pilot activation or formal Accepted transition occurred.

Automatic approval review rejected stopping disposable QA24 API4323 and deleting
only `hub_qa_checkpoint24`, requiring explicit user confirmation. **Nothing was
stopped or deleted.** The confirmation is pending; old API5088, DB and preview8853
are preserved. New builds use an isolated artifacts directory and static8854;
they do not bypass the rejected cleanup. Shared PostgreSQL55432 is retained.
Old QA24 health was Degraded because its jobs-disabled outbox aged; this is not
worker/delivery health proof. No queue was cleared or health suppression added.

Next contract: `parallel-build/projects-reports/requests/g54-response-outcome-contract.md`.
It reuses the existing mandatory response explanation, adds a nullable explicit
outcome to finding/event, preserves old receipt hashes/replays, and proposes an
all-round package response sheet. It is a contract, not executed implementation.
