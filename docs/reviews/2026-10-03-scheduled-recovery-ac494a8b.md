# Scheduled synthetic recovery checkpoint — 2026-10-03

Executable `ac494a8ba70d50517f540ad7e0acced85d84897e` remains live in the isolated public review and private pilot.
[PR #33](https://github.com/jayapatel1511-hub/PM-Tool/pull/33) merged at
`56e2abc9e6d82fc3688ae821ac87b4726e1d08db`; its candidate and
[merged-main CI](https://github.com/jayapatel1511-hub/PM-Tool/actions/runs/37155845870) passed.
The documentation merge did not require an application activation.

## First automatic review backup and approved encrypted recovery

**PASS, bounded review recovery:** the installed system timer first triggered at 22:00:01 UTC.
Its service finished successfully at 22:00:02 with exit zero, retaining the owner-1000, mode-600
`hub-review-20261003T220001662776Z.dump` (1,131,363 bytes) under the private review backup directory.
The next timer run is 2026-10-04 at 22:00 UTC. The dump SHA-256 is
`b8001e83193e5c6ce821dcc795233ea4462fc20865ad89348254d99d992ff053`.

Jay's existing review approval covered the dump, review runtime configuration, password verifiers and protection
keys in the existing encrypted Mac restic repository. Only those four recovery members and a hash manifest were
packaged; plaintext login handoffs and all pilot files were excluded. Snapshot
`fce8cfaf21d46d061f5df0f88b70473c3bb6d19167f8e5bbb4ebd23198b06539` was retrieved and every member's hash matched.
The retrieved dump restored successfully in a disposable PostgreSQL 17 container with no network, published host
ports or existing database volumes. Checks at 22:03 UTC found:

- 29 migrations, 3 review users and 4 review projects.
- Both original tasks' recorded progress, versions and dates, plus the original persistence marker.
- All 6 fixture promises: 4 Committed, 1 Withdrawn and 1 Met.
- The original frozen count of 5, the later count of 1 and the latest Met promise.
- The explicitly unresolved synthetic basis conflict.

The first checker used the wrong schema name after restoration. It was corrected to the maintained `hub` schema
and `hub.__ef_migrations` table; the retry retrieved the same encrypted snapshot. This was a drill-helper
correction, not an application or database fix. The final retrieval/restore/check phase took 1.8 seconds; that
measurement does not establish full application recovery or the production RTO.

Owned disposable resources and temporary plaintext copies on the Mac and homedev were removed. The original
scheduled dump, source runtime/key ring and encrypted snapshot remain. Credential-free evidence is
`pm-tool/data/scheduled-review-backup-20261003.json` and `scheduled-review-recovery-20261003.json`.
The archive and recovery contents are outside Git.

## Submission metadata and missing-owner label

A new labelled `SYNTH-GATES-1003-SUB002` package passed a bounded public-HTTPS metadata edit at 22:09 UTC.
Jay's edit changed title, purpose, recipient reference and target date, advanced the stored package from Draft
to Checking and manifest version 1 to 2, and preserved its source revision, source head and milestone. Taylor
and Yagmur edit attempts returned 403; a stale Jay edit returned 409. Complete detail comparisons showed no
changes from the refused commands. The actual JSON evidence export retained both manifest versions and all
14 checks, with the original manifest and seven original checks unchanged. Only the new synthetic package
was changed; existing packages and the linked source/review were not edited.

The initial signed-in browser showed the edited metadata, Jay as coordinator, manifest version 2 and the
expected unapproved-review blocker. Full reload reached sign-in, so browser persistence remains UNPROVEN.
Credential-free API evidence is `pm-tool/data/hosted-submission-metadata-ac494a8b.json`.

That view exposed a separate UI wording defect: a blocker with a null reviewer ID was labelled unavailable or
no longer a participant. The candidate displays the existing Unassigned label for a missing ID and retains
the unavailable label when a named ID cannot be resolved. The readiness calculation, assigned people,
permissions, API, records and migrations are unchanged. This wording fix requires a new application activation
and signed-in browser verification; it is not already deployed in `ac494a8b`.

## First automatic pilot backup

**PASS, local scheduled backup:** the pilot timer triggered at 22:15:03 UTC and its service completed at
22:15:06 with exit zero. It retained owner-1000, mode-600 `hub-pilot-20261003T221503657086Z.dump`
(489,600 bytes; SHA-256 `c9f7019bac9ee230e4898cc0db8cbf34e7644b2cb5b82c729004a668b8953996`).
The same run's journal recorded `backup successfully verified` and physical base
`20261003T221504833773Z`. The installed root helper matches the activated release's helper byte-for-byte
(SHA-256 `014993c2057ed907724c8d90a9a14eac14302aacf61681b7e76f407ee730d847`).
The next pilot timer run is October 4 at 22:15 UTC.

Credential-free evidence is `pm-tool-pilot/data/scheduled-pilot-backup-20261003.json`. This fresh pilot dump
was not restored in this checkpoint, and neither the dump nor physical base/WAL was transferred off-host.
The earlier 18:24 on-host logical/PITR rehearsal remains historical; the new automatic run does not repeat it.

## Remaining recovery and acceptance gates

Both first automatic local backup runs have now passed. The separate sensitive
pilot recovery transfer is awaiting its specific approval; no pilot dump, verifier, runtime configuration or key
was transferred to the Mac by this review drill.

A successful local scheduled dump plus a manual encrypted retrieval does not establish an automatic off-host
schedule, off-host pilot WAL replay, full application disaster recovery, a 15-minute RPO or an eight-hour RTO.
Native print remains unproven because the print dialog was cancelled. Full packet/manual accessibility, company
participants' hosting/privacy approval and pilot go/no-go, and production deployment remain separate gates.
No Azure deployment or new paid service was introduced. See the
[hosted acceptance checkpoint](2026-10-03-hosted-acceptance-ac494a8b.md) for the earlier bounded workflow results.

## Candidate validation

The missing-owner wording change passed frontend TypeScript, lint and a fresh production build in a private
output directory. Lint retained its existing warnings. No API/database code or migrations changed, so the full
API suite was not repeated locally for this text change. Exact-candidate and merged-main CI, activation and
fresh signed-in browser verification must be recorded separately before claiming the new label is deployed.
