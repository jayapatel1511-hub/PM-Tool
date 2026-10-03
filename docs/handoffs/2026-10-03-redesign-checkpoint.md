# PM-Tool checkpoint before redesign — 2026-10-03

Jay requested committing completed work and recording the remaining work because the
incoming UI redesign may also change behaviour. Further implementation, browser work
and acceptance mutations stopped at this checkpoint. Do not resume the old acceptance
queue automatically or polish the outgoing UI. Reconcile the redesign scope first.
This is a handoff, not full release acceptance.

## Source and live state

- Use the isolated worktree `/Users/jaypatel/.codex/worktrees/pm-review-release/PM-Tool`,
  branch `codex/pm-review-release`. The original `/Users/jaypatel/PM-Tool` remains at
  `16399682` with unrelated untracked work; it was not changed or committed.
- The latest merged documentation baseline is PR #35 at
  `49b2a4abc838479be3bf872afd3fa178606bafbd`. Candidate CI `37160197276` and
  [main CI `37161002703`](https://github.com/jayapatel1511-hub/PM-Tool/actions/runs/37161002703)
  passed. The isolated branch integrated that baseline in `4b853602` before this checkpoint.
- Both homedev runtimes still execute
  **`29b3d7036f529b513fdc2ad47cff62a0b75f0b59`**. PR #34 merged at `fbb6f34e`;
  candidate/main CI passed 858 tests each. Later documentation/test commits do not
  replace this executable or require an activation.
- Public synthetic review: `https://pm.engcalchub.com` → loopback `3080`.
  Separate private synthetic pilot: loopback `3081`, 30 fictional users and 10 Setup
  projects. Its disciplines are PM, Civil, Structural, Geotechnical, Materials,
  Mechanical, Electrical and Environmental, with 10 project categories.
- Keep homedev, the existing hostname/tunnel and individual passwords. No Azure or
  new paid service is required. Preserve independent volumes, runtime files,
  verifiers and protection keys; never copy review data into company production.
- The checkpoint commit contains a regression test and evidence/documentation only.
  Locate its exact hash with `git log`; CI, push and merge for that new commit are
  separate from the already successful PR #35 baseline.

## Completed and verified

| Gate | Result | Evidence and limit |
|---|---|---|
| Application code/merge | PASS for deployed label release | PR #34 merged; exact candidate/main CI passed. This does not close all packet acceptance. |
| Review/pilot activation | PASS, bounded | Private probes 10/10 on each runtime, public review probes 9/9, exact current pointers and runtime/key/verifier preservation. The activation wrapper's final sign-out path was wrong; the independent follow-up used the correct path and passed. |
| Persistent review records | PASS | Original tasks and marker, edited submission with both manifest versions/14 checks, six promises with frozen denominators 5 and 1, and unresolved basis conflict/current uses survived activation. |
| First local backup schedules | PASS | Review 22:00 UTC and pilot 22:15 UTC automatic runs completed successfully. The first scheduled pilot dump restored into an isolated drill database with 10 projects. |
| Review encrypted recovery | PASS, bounded | Approved review recovery payload stored/retrieved from existing Mac restic; hashes matched and logical dump restored in isolated PostgreSQL 17. Full application DR/RPO/RTO is not proved. |
| Pilot inactive-account control | PASS, bounded at 23:40 UTC | One fictional non-Admin was refused with existing cookie and fresh sign-in while inactive; reactivation restored access. All 30 active users, 10 projects, roles/capacity/identities, verifiers and prior keys were preserved. |
| Pilot operations observation | PASS, observation only | At that account check: pending changes 0, seven jobs Succeeded with 0 failures in 24h, problem count 0. No load/uptime/notification acceptance claim. |
| Template provisional-date boundary | PASS locally | `TemplatesTests` 7/7. A copied Proposed basis with unknown date cannot be confirmed; explicit project-date edit permits independent confirmation. No application behaviour changed. Independent changed-test review found no actionable findings. |

The inactive-account result is reversible: the same unexpired cookie works again after
reactivation if its verifier is unchanged. Permanent offboarding requires verifier
removal/rotation; normal sign-out merely clears the browser cookie. That separate
revocation gate has not been completed on the company stack.

Evidence checkpoints:

- [Current activation/preservation](../reviews/2026-10-03-submission-label-29b3d703.md)
- [Earlier bounded workflow acceptance](../reviews/2026-10-03-hosted-acceptance-ac494a8b.md)
- [Scheduled backup and approved review recovery](../reviews/2026-10-03-scheduled-recovery-ac494a8b.md)
- [Pilot readiness](../pilot/pilot-readiness.md) and [production readiness](../pilot/production-readiness.md)

Credential-free inactive-account evidence:
`/home/jaypatel04/Workspace/Projects/pm-tool-pilot/data/hosted-pilot-inactive-account-29b3d703.json`.
Its owner-only journal records the reversible commands and preserved identities.
Local evidence is `/private/tmp/pm-hosted-pilot-inactive-account-29b3d703.json`;
template test log is `/private/tmp/pm-template-provisional-49b2a4ab-20261003.log`.
Temporary paths are conveniences, not durable release authority. Password handoffs,
sessions, runtime configuration, dumps, verifiers and key material remain outside Git.

## Remaining work, in order

1. **Agree the redesign contract.** Identify UI-only changes versus changes to
   workflow, permissions, statuses, APIs or data. Reconcile changed requirements with
   canonical `spec-parts/` and Spec Kit before implementing. Retain existing
   concurrency, audit, identity and data-preservation guarantees. Do not spend time
   completing acceptance on behaviour scheduled for replacement.
2. **Implement/integrate the redesign.** One writer per subsystem, within the
   configured agent limit (Jay permits up to 10, subject to the actual tool limit).
   Preserve concurrent branches and dirty work. Main agent owns integration and the
   focused implementation/test/independent-review/fix loop.
3. **Close full packet acceptance on the final combined revision.** Existing
   PASS results are bounded slices, not all of packets 025–033. Re-derive each
   packet's current requirements and verification gaps. Include role/privacy and
   stale/concurrent writes; source/history retention; migrations; export parity;
   actual notifications; keyboard/assistive-technology and responsive flows.
   For 031, replacement/adoption/withdrawal, source-decision reopening, assumption
   dispositions, template copies and restricted/lifecycle combinations still need
   final hosted acceptance. For 032, complete the remaining actor/restricted/lifecycle
   combinations and browser forms beyond the already verified weekly slices.
4. **Verify the final UI after deployment.** Fresh signed-in save and full reload,
   assigned/unassigned people, readiness/coordination counts and complete flows.
   The previous submission label/reload check remains UNPROVEN and is deferred at
   Jay's request. Native print was cancelled; no PDF was saved. Native downloads and
   complete manual accessibility remain UNPROVEN. Rerun against the redesigned UI.
5. **Complete recovery proof.** Specific approval for the pilot sensitive recovery
   payload is still unanswered. No pilot dump/config/verifier/key/base/WAL was
   transferred off-host. After approval, prove encrypted retrieval plus isolated
   restore, automatic off-host scheduling, retrieved WAL replay and full app recovery.
   Measure and accept the required 15-minute RPO/8-hour RTO; daily dumps alone do
   not establish those targets. Existing on-host native PITR was a bounded synthetic
   drill, not host-loss recovery. Preserve the separate whole-server backup job.
6. **Finish hosted security and operations.** Record the company threat-model
   walkthrough, current account/role/privacy tests, verifier rotation/removal denying
   an existing cookie, capacity/load, monitoring and actionable alerts, notification
   mode, outage procedure and rollback. The private pilot runtime uses least-privilege
   `hub_pilot_app`; review-role/company-wide acceptance remains distinct. Email is
   Log mode; successful job status does not mean email was delivered.
7. **Company pilot go/no-go.** Obtain real sponsor/participants/projects and company
   approval for the home host, Cloudflare boundary, personal data, local-password
   exception and recovery policy. The current 30/10 fixture is fictional rehearsal;
   it does not constitute employee acceptance. Do not convert synthetic identities
   into real company users or treat generated project data as operational data.
8. **Production cutover only after those gates.** Confirm the accepted dataset,
   exact-head CI, merge, pre-release verified backup, compatible previous image,
   final health/sign-in/browser checks and rollback. Route the designated hostname
   to the accepted company origin only at cutover; retain review privately. Production
   deployment and company acceptance are currently NOT PERFORMED.

## Packet acceptance index

Current task files leave **T006 open in all nine packets**. Use each packet’s
verification record to distinguish older failures, later fixes and bounded PASS
slices; an open full-acceptance task is not proof that all implementation is missing.

| Packet | Full acceptance required | Authoritative verification record |
|---|---|---|
| 025 Handoffs | AC-HND-01–05 plus concurrency/access and complete UI scenarios | [025 verification](../../specs/025-discipline-handoffs/verification.md) |
| 026 Reviews | AC-MRV-01–05 plus concurrency/access and complete UI scenarios | [026 verification](../../specs/026-multidisciplinary-reviews/verification.md) |
| 027 Change impact | AC-CHG-01–05 plus concurrency/access and complete UI scenarios | [027 verification](../../specs/027-revision-change-impact/verification.md) |
| 028 Submissions | AC-SUB-01–05 plus concurrency/access and complete UI scenarios | [028 verification](../../specs/028-submission-readiness/verification.md) |
| 029 Capacity | AC-CAP-01–05 plus concurrency/access and complete UI scenarios | [029 verification](../../specs/029-dated-capacity-allocations/verification.md) |
| 030 Coordination | AC-DCV-01–05 plus concurrency/access and complete UI scenarios | [030 verification](../../specs/030-discipline-coordination-view/verification.md) |
| 031 Design basis | AC-BAS-01–05 plus concurrency/access and complete UI scenarios | [031 verification](../../specs/031-design-basis-assumptions/verification.md) |
| 032 Readiness/weekly | AC-RDY-01–05 plus concurrency/access and complete UI scenarios | [032 verification](../../specs/032-readiness-weekly-commitments/verification.md) |
| 033 Located issues | AC-LOC-01–05 plus concurrency/access and complete UI scenarios | [033 verification](../../specs/033-location-linked-issues/verification.md) |

Metadata reconciliation also remains: 028–033 task files still show T001–T005
unchecked despite implementation and verification records. Reconcile each task
against actual final source/evidence after agreeing the redesign; do not mark them
complete merely to match a summary or implement them again solely because a box
is unchecked. This checkpoint does not change those task statuses.

## Prepared work that must not be mistaken for completed work

- `/private/tmp/pm_hosted_basis_lifecycle_29b3d703.py` and
  `/private/tmp/pm_run_basis_lifecycle_29b3d703.py`: **not staged or executed**.
  The packet syntax/DTO review passed, but independent review requested stronger
  all-page/original-record preservation, immutable A/B source and complete use-history
  assertions, and correction of a misleading “no live execution” label. The wrapper
  supplies additional snapshots but has not undergone live validation. Reconcile
  against redesigned behaviour, fix/review, then execute only a fresh labelled packet.
  Do not treat its prepared commands or syntax checks as hosted PASS.
- `/private/tmp/pm_package_pilot_recovery_29b3d703.py`: prepared, dry/fake-layout checks
  passed; **not staged or executed**. Its approval flags do not supply human approval.
  Keep the review-only encrypted recovery approval distinct from pilot transfer.
- The inactive-account helper was executed once and passed. Its existing evidence
  and journal deliberately prevent blind repeats. Preserve normal sign-in/audit
  metadata; do not roll the database back to erase the rehearsal.

## Safe restart

Read this checkpoint before the older October 1 restart prompt. Re-derive Git status,
remote main/PRs/exact-head CI, current specs, deployed full SHAs, ports, timers and
backups; later agents may have changed them. Read the homedev guide before operations.
Do not execute old activation commands or acceptance scripts blindly. Keep secrets
on homedev and require interactive sudo in Jay's terminal when the host requires it.
Start with the agreed redesign scope, then run the smallest meaningful checks for
changed behaviour and required repository checks. Earlier passing CI or a health
endpoint never substitutes for final browser or real company acceptance.
