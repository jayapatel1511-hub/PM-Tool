# Packets 039–051: hosted review verification

**PASS — public synthetic review deployed** to [pm.engcalchub.com](https://pm.engcalchub.com). Running release: `bf8a39d7290d8bad8f9138872582b164baaf1894`; previous release: `f6f31d7e4b7b24875371443ed222d994ee0a4330`. Jay ran the prepared activation in his own homedev sudo terminal. Activation was verified at **2026-10-29T22:33:51.709978-03:00** (Halifax), with the final independent Admin/API check at `2026-10-30T01:49:29.805416+00:00`. This is review deployment only; no pilot/production promotion, flag activation, push, main merge or Accepted claim.

## Observed deployment

| Check | Result |
|---|---|
| Source archive | 1,465 committed file/symlink entries matched the prepared release; no working-tree files or credentials transferred |
| Current pointer, health/build | Exact `bf8a39d7…`; private/public backend and served frontend build stamps match. The stamp is in the imported common chunk, not the entry bundle |
| Operator image check | Exact `pm-tool-review:<full-sha>` tag and UID 1000:1000 PASS; candidate immutable image ID was not captured in the receipt |
| Migrations | **38 → 42**, all previous IDs retained; four additive migrations |
| Core record counts | **15 users, 8 projects, 46 tasks, 8 comments**, identical before/after. This checks counts, not a byte-for-byte row comparison; concurrent writes were not excluded |
| Immutable records | Admin review and deliverable baseline UPDATE/DELETE and TRUNCATE trigger names present |
| Org units | Unique index includes `NULLS NOT DISTINCT` |
| Flags | **33 visible flags false**, including pickup, verifiable log, Backlog, admin review and data-entry safety |
| Auth | Operator private/public probes PASS; independent host-side synthetic Admin sign-in/read/Social logout PASS; copied cookie returns 401. Credentials remained on homedev |
| Public browser | Reloaded live login renders Microsoft, Google, User ID/password and Sign in. Anonymous desktop rendering only; provider consent and an authenticated browser session were not repeated |
| Operations | **0 problems, 0 pending changes, 0 dead letters**; no failed job runs reported in the last 24 h |
| Pre-upgrade backup | `hub-review-20261030T013324507374556Z.dump`, **4,013,982 bytes**, mode 600. The activation helper checked nonempty/restore-list structure before replacement; no new restore drill or off-host retrieval |

## Background jobs

All seven latest persisted runs are Succeeded, with `failed24h = 0`. Five started after the activation-verification snapshot; the other two successful runs are older evidence and are not claimed as fresh runs of this release. No job was manually forced for this check.

| Job | Latest status | Started UTC | After activation verification? |
|---|---|---|---|
| daily-digest | Succeeded | 2026-10-30T01:45:26.578002+00:00 | Yes |
| directory-sync | Succeeded | 2026-10-29T04:00:19.46957+00:00 | No |
| email-send | Succeeded | 2026-10-30T01:49:06.578764+00:00 | Yes |
| evaluation-retry | Succeeded | 2026-10-30T01:45:46.576423+00:00 | Yes |
| nightly-evaluation | Succeeded | 2026-10-29T03:15:19.429189+00:00 | No |
| ops-watchdog | Succeeded | 2026-10-30T01:49:26.578844+00:00 | Yes |
| weekly-summary | Succeeded | 2026-10-30T01:45:26.578429+00:00 | Yes |

## Corrections and verification boundaries

Final sampled checks at `2026-10-30T01:49:29.805416+00:00` remain Healthy with zero operations problems/pending changes/dead letters, seven latest successful job runs, unchanged dark flags and successful sign-in/logout replay denial, **938 seconds after activation verification**. These are sampled checks, not continuous request/error/latency metrics. Public asset verification at `2026-10-30T01:47:09.689031+00:00` found the exact frontend stamp in `/assets/common-DOJhtx8J.js`; the machine-readable record includes its SHA-256.

The deployed application preserves the already-hosted Social-plus-tester login omitted by the earlier integration base. The focused final local set passed **83/83 backend tests**, web build/typecheck/lint, the combined-login/account-menu browser suite (synthetic API, zero page errors/axe findings), and 8 probe tests. The original full-suite/coverage/parity/image rehearsal remains evidence for `2dcdaec4`; it was not rerun or relabelled as evidence of the later auth port.

The one-use operator guard initially rejected the host because `/etc/hostname` says `home_dev` while the active kernel hostname is `homedev`. It now checks the active short hostname; a wrong-host negative control remains. A final real PostgreSQL probe also exposed that the information-schema trigger view omits TRUNCATE-only triggers, so the guard uses `pg_catalog.pg_trigger`. Six synthetic guard flows and the rolled-back temporary PostgreSQL metadata test pass. Old guard versions and checksum revisions are preserved in the private operator handoff. These changes did not alter the committed app archive.

One independent Mac Python HTTP probe returned 403; standard curl and the real browser succeeded, and no security setting was changed. The legacy default probe identity lacked Admin capability; Admin-only reads used the existing synthetic Admin instead, without privilege changes.

Host rollback was **not performed** because activation passed. The existing previous image/release and verified pre-upgrade dump are retained. Earlier local main-image rollback is separate evidence. No candidate immutable image ID, real provider consent, full hosted workflow sweep, assistive-technology session, pilot acceptance or human visual acceptance is claimed.

The inactive-assignee pickup P2 remains open, with `flag.task_pickup` off. All other release flags remain off; enabling them and later retirement need their existing review/pilot gates. Repository controls/Tier B, 039 S18, 040 S16, human/pilot and whole-specification acceptance remain open.

The [portable evidence record](2026-10-29-packets-039-051-evidence.json) contains exact migration IDs, counts, flags, job timestamps and scope. Private source/guard provenance remains in `incoming/deploy-039-051-20261029/` on homedev. Raw operator records are in `data/deploy-bf8a39d7290d8bad8f9138872582b164baaf1894/`; sanitized Mac copies are under `/private/tmp/pm-tool-deploy-20261029/`. No database dump, verifier, password or cookie was copied to the Mac.

A later documentation-only commit may advance the local branch beyond the running release. It does not require rebuilding the app; deployment identity stays `bf8a39d7290d8bad8f9138872582b164baaf1894`.
