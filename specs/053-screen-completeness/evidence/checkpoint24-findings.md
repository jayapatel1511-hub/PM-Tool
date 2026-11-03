# Checkpoint 24 — generation 52, local verification complete

**S34 and S43 local acceptance PASS;33/49 slices locally checked.** No commit, push, deployment, pilot activation or
formal Accepted status. The primary checkout and prototypes are preserved.

## Implemented

- Create-only drawing-list CSV/XLSX import checks every row, shows normalized values
  and errors, and creates all rows or none through the canonical manual-create path.
  Existing deliverables are never updated or deleted. Each created record has one
  Created activity entry carrying import provenance. No issue history is invented.
- Bounded parser, exact authority/reference preflight and preview fingerprint,
  project/version locking and atomic one-day command receipt. Retries retain the
  exact original body/key. A pending foreign receipt cannot be stolen as expired.
  Only the two marked import POSTs accept the larger JSON envelope.
- Mounted import files remain memory-only through permitted Person inspection;
  source close and unrelated route changes retain the unsaved-work guard. A lost
  reply stays uncertain through later 403/404 refusals rather than creating a new
  intent or claiming nothing was created.
- Search and Files expose optional known-User metadata behind `people_screens`,
  preserving legacy fields, access, ordering and list limits. Search/Reassign,
  project/group headings, Calendar, Staff, time away, settings, Files, Home/account
  and Pickup now use exact User IDs for passive Person inspection. Pickers and
  existing primary actions retain their action.
- The Account return-focus guard now permits a visible closed dropdown trigger
  while excluding hidden or closed content. A minimal actual-browser reproduction
  captured the prior focus-to-main failure and the corrected trigger return.

## Executed evidence

Artifacts are in [checkpoint24-runtime](checkpoint24-runtime/).

- **137 distinct .NET cases PASS:** parser69, Search/Files readers6, atomic import16,
  and contract/adjacent46. See TRX files and `verification-rollup1.json`.
- Seven Node unsaved-navigation guard checks PASS; application/fixture types PASS;
  lint and SSR PASS. Solution build3 and web build3 PASS. Compiler, lint, bundle and
  ineffective-dynamic-import warnings remain; this is not a warning-free build.
- All602 compiled C# source inputs remain byte-identical to their captured build;
  `web-build3-source.json` records the exact frontend/test delta since build1.
  All129 final served files match build3 bytes (`preview-build3.json`).
- Search Chrome3 PASS: four observations,12 Person reads,20 search reads,0 business
  writes. Remaining-surfaces Chrome4 PASS: eight cases,14 Person reads,0 business
  writes, including mounted drafts, nested return, phone and flag-off behavior.
- Import Chrome5 PASS: five observations,10 previews,6 explicit commit requests
  across3 keys,2 successful replies and2 Person reads;0 unexpected business writes.
  Its XLSX bytes/worksheet behavior is mocked UI evidence, not parser proof.
- Pickup Chrome1 PASS: seven cases at375px,3 Person reads,0 business writes. This
  synthetic UI check proves exact offer/User identity, natural return, answered404,
  unchanged actions and flag-off behavior; it does not claim server authority.
- Actual Kestrel/PostgreSQL import API1 PASS: bad40-row CSV and XLSX previews and
  refused commits leave every hub table unchanged; corrected files each create40
  records and40 provenance-bearing Created entries. Exact-key retries change no
  hub table, no issue entries appear, and the pre-existing sentinel stays unchanged.
  The independent API suite also covers late-row rollback and concurrent replay.
- A valid XLSX preview with a1,270,338-byte JSON body succeeds on the real import
  endpoint without database changes; an ordinary1.1MB create remains413. The
  fixtures are deterministic synthetic files, not operational records.
- Actual Chrome/API/SQL join PASS in `real-import-browser1.json`, with seven
  screenshots and append-only recovery progress. The bad real XLSX previews39/1
  with no business-table changes; corrected XLSX creates exactly40 more records,
  40 Created/Import entries,40 source rows and one batch. Counts move81→121;
  issues remain0 and the sentinel hash is identical. Actual Marc Person inspection
  retains the workbook/hash, disclosures, genuine list scroll and exact focus.
  Actual Search Alex and Files Jill Person links return naturally to their source;
  Search has no main-sidebar selection. One expected private PM Project-open
  acknowledgement is recorded separately from the sole business import command.

## Preserved failures and corrections

Raw failed runs remain alongside passing reruns. The initial solution command used
the nonexistent `.sln` instead of `Hub.slnx`; one API fixture needed the correct
Planning source category; contract generation intentionally required a golden
update; fixture types needed numeric diagnostic severity. No such run is a pass.

Search/browser harness repairs covered encoded IDs, natural close-focus settling,
visible phone controls and an ambiguous Close locator. The real Account focus bug
was fixed in maintained source and independently reviewed, then reproduced as PASS.

Import runs1–4 preserved false harness assumptions about outer-dialog overflow,
the shared refresh count and changing synthetic project pulses. The final check
uses the genuinely overflowing row list, a stable idle notification pulse and exact
existing-cycle counts: three register reads (overlapping immediate prefixes plus a
delayed pass), two header reads, and no success refresh during uncertain retries.
No production cache behavior was changed to satisfy these assertions.

## Outstanding gates

- Independent S34/S43 final local acceptance PASS. Spec/trace/8 decision checks and
  whitespace PASS. 602 compiled C# files match; final build3 served parity129 PASS.
- QA24 cleanup BLOCKED by automatic approval review: stopping API4323 and dropping
  only root-created `hub_qa_checkpoint24` require explicit confirmation. No part of
  that rejected action executed. Both are preserved; test PostgreSQL and preview
  also remain. See `qa24-cleanup1.json`. No workaround was attempted.
- All other unchecked052/053 slices remain required. Broad visual census, native
  visibility/zoom and human/pilot/hosted acceptance are not supplied by this batch.
  052-D11/D12 retain the user's choices; earlier open decisions remain Open.
