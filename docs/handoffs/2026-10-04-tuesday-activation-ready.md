# Tuesday full-example release — ready for activation

## Final technical deployment verification PASS — 5 October 2026

Release `bbac1f6daf595a9411908f9795201c20f812079c` is active at [Tuesday](https://pm.engcalchub.com/?review=bbac1f6d). Exact CI, full Tuesday fixture import, private and public authentication checks, all ten public identity/project/own-planning/sign-out checks, fresh backup restore and restart persistence PASS. Root directly inspected the hosted refreshed landing at desktop/375px, signed in as Priya, inspected My Work at 1440px, Tasks at 1440/375px and Weekly Planner at 1440/375px. These bounded hosted checks show no page/menu sideways overflow, no visible text below 12px, no fixture notices and no captured browser console errors. Project navigation wraps to three rows at 1440px and eight at 375px. Negative remaining capacity, time away and distinct source/confidence/visibility controls render in the desktop planner. Phone tasks use cards. The phone planner displays the desktop/tablet notice required by FR-PLN-26/AC-PLN-18; full phone planning is outside the canonical scope. Full hosted six-role/state acceptance is not claimed; the complete unchanged-source local 1,956-case sweep remains the broader evidence.

Hosted IDs: `priya`, `sam`, `lena`, `jordan`, `alex`, `rita`, `marc`, `jill`, `diane`, `omar`. Existing legacy sign-ins/credentials are preserved. Passwords remain only in the owner-only host `.runtime/tuesday-review-login-handoff.json`; the one-person temporary browser credential copy was removed, and no password/cookie/verifier appeared in logs or Git. Local preview remains [localhost:5173](http://localhost:5173/), with its Development person selector and no preview passwords. Separate pilot remains unchanged. Draft PR #37 is pushed and unmerged; post-commit evidence docs stay local and the three unrelated dirty files are preserved.

Current hosted screenshots: `/Users/jaypatel/.codex/visualizations/2026/10/04/01a104af-b670-70b0-872e-f9915f405535/tuesday-hosted-bbac1f6d.jpg`, `tuesday-hosted-bbac-phone.png`, `tuesday-hosted-mywork-bbac-desktop.png`, `tuesday-hosted-tasks-bbac-{desktop,phone,phone-content}.png` and `tuesday-hosted-planner-bbac-{desktop,phone}.png`. The temporary browser viewport was reset. Final clean public evidence is `/private/tmp/tuesday-bbac-public-users-final.log` (root serialized run, exit 0) and `/private/tmp/tuesday-bbac-public-auth.log` (exit 0); the earlier overlapped 429 probe remains failed historical evidence. No rate-limit/security workaround was used.

Overall human acceptance remains incomplete: T023 manual screen-reader, SC-001 supervisor exercise and Jay's final visual review are UNRUN. Readiness.cs:108 remains separately tracked with its decision/task seed edge omitted. Protected history/benchmark copy and copied-cookie replay/off-host backup retrieval limitations remain documented. Open decisions: brand mark, approval of the completed hero recapture/blend, holiday and manual-role removal confirmations, and per-notification Mark read. Do not mark the full original goal complete from deployment alone.

## Public verification PASS — 5 October 2026

Independent public gates PASS, including health, anonymous 401, unsafe Origin 403, wrong password 401 and legacy sign-in/sign-out. Root's serialized ten-person probe exited 0: exactly ten unique expected identities, each sign-in 204, same-person /me, project list 200, own-only My Week 200 and sign-out 204. The earlier overlapped verification log hit 429 and is retained as failed attempt evidence; no rate limit was weakened. Final clean evidence: `/private/tmp/tuesday-bbac-public-users-final.log` and `/private/tmp/tuesday-bbac-public-auth.log`; independent helper audit confirms exact SHA/statuses and no NULs. The bounded hosted signed-in browser review subsequently PASS as recorded above.

## Verified deployment — 5 October 2026; public and bounded signed-in browser checks PASS

Root independently read the final `RESULT post-deployment safety gates PASS` for `bbac1f6daf595a9411908f9795201c20f812079c`. Exact review image and `pm-tool-review-db` match; both planning migrations are applied. Full Tuesday fixtures completed through individual permissioned APIs: ten accounts, three SYN projects (101/102 Open; 103 Restricted), 14 tasks, seven milestones, nine deliverables, five decisions, four risks, five issues, three meetings, six allocations, two handoffs, one review package, one change notice, one submission, two design-basis entries, two constraints, two output commitments, seven planning entries and one template. The ten-account private handoff and fixture manifest are mode 0600 in mode-0700 directories.

Fresh backup `hub-review-20261005T110702731530105Z.dump` is 1,385,277 bytes, mode 0600. Its isolated restore drill restored seven projects and cleaned up the temporary database. Restart persistence is `projects|people|planning = 7|13|7`. Transient curl 56 connection resets during restart recovered before health 200 and the final PASS. Private authentication/Host/Origin gates PASS; stateless copied-cookie replay 200 remains a disclosed existing limitation. Current pointer is `bbac1f6daf595a9411908f9795201c20f812079c`; separate pilot remains `29b3d7036f529b513fdc2ad47cff62a0b75f0b59`. Independent public ten-person and hosted signed-in browser verification are pending; T023, SC-001 and Jay's acceptance remain UNRUN.

Completed release: `bbac1f6daf595a9411908f9795201c20f812079c`, committed/pushed on `claude/tuesday-visual-batch1`; CI, preparation, activation, full fixture import and private post-deployment gates PASS. The command below has already run successfully; do not rerun it for verification. Public sign-in and bounded hosted signed-in browser checks PASS; the separate pilot is unchanged.

## Completed corrected operator command — retained for history

[Exact-head CI 37296543742](https://github.com/jayapatel1511-hub/PM-Tool/actions/runs/37296543742) PASS for `bbac1f6daf595a9411908f9795201c20f812079c`: 1,040 backend tests with zero failures/skips, rules branch coverage 96.8%, service line coverage 94.5%, 34 hosted-helper regressions, 19 backup/pilot regressions, all five browser suites, build/type-check/lint (87 existing warnings), and spec trace 660 IDs/252 sections with zero uncited/unknown. Planner covers 14 base plus six recovery flows, 177 API calls and seven zero-finding axe scans. All 742 prepared regular source files match the commit; both reviewed private helpers match their hashes and mode 0600. The app/landing sources remain unchanged from the complete 1,956-case sweep and eight public cold checks.

The prior 98ed319a activation succeeded, then fixture seeding failed before project/resource writes because its transport variable was overwritten by a reference client ID. The corrected candidate preserves the ten already provisioned credentials and three legacy accounts; it resumes the full fresh fixture recipe through existing permissioned APIs. No pilot data is touched. Do not rerun the earlier command.

Deployment is already authorized. Run in Jay's own Mac Terminal; enter Ubuntu sudo only there. The existing [runbook](../runbooks/homedev-review.md) requires operator terminal authentication, with batch stdin isolated using `/dev/null`. Keep the terminal open until the final PASS:

```bash
ssh -t homedev 'set -o pipefail && umask 077 && bash /home/jaypatel04/Workspace/Projects/pm-tool/incoming/finish-tuesday-bbac1f6d.sh </dev/null 2>&1 | tee /home/jaypatel04/Workspace/Projects/pm-tool/data/activation-bbac1f6d.log'
```

The exact-SHA helper activates/builds the corrected image with pre-release backup, imports the full examples, then runs exact image/volume, both migrations, private backup, isolated restore drill, restart persistence and private authentication checks. Expected final line: `RESULT post-deployment safety gates PASS`.

Reviewed helpers (owner-only mode 0600): `finish-tuesday-bbac1f6d.sh` SHA-256 `ae340cb08ad5593ac4fc2559bfb9d055400456818db6298cb583ef13cf27a936`; `postdeploy-bbac1f6d.sh` SHA-256 `579480f0ff1f9449de0574ef9ee17fd7a1646695a7a896b193cb083f9d62c35b`.

After execution, independently inspect `data/activation-bbac1f6d.log`, exact image/pointer/volume, both migrations, fixture manifest/counts, all ten public sign-ins and scoped planning responses, and hosted browser hero/app views. No deployment or seed PASS follows from preparation or a reported PASS alone.

## Verified candidate evidence

- [Exact-head CI 37254797823](https://github.com/jayapatel1511-hub/PM-Tool/actions/runs/37254797823): 1,040 backend tests, zero failures/skips; rules branch coverage 96.8%, service line coverage 94.5%; 33 hosted helper tests; all five browser suites; type-check/build/lint (87 existing warnings); spec/trace 660 IDs and 252 sections, zero missing/unknown.
- Complete live preview sweep: 1,956 unique cases at 1440/1024/768/375 for Priya/Sam/Lena/Jordan/Alex/Rita plus public pages. Zero page errors, axe violations, page/main/menu horizontal overflow, text below 12 px, raw keys or old palette findings. Eight public cold cache rechecks PASS. `/private/tmp/tuesday-sweep-natural-complete/` and `/private/tmp/tuesday-sweep-natural-public-final/` retain original/recheck evidence.
- Final 148 web/landing source hashes unchanged. The only intentional change during the full pass was the public hero image URL version, covered by the cold checks. Direct phone review confirms wrapping menu rows and long labels.
- Local preview: `http://localhost:5173/`; API `http://localhost:5080`, named preview database only at 55433. Local sign-in uses the existing development person selector.

The ten hosted login IDs are `priya`, `sam`, `lena`, `jordan`, `alex`, `rita`, `marc`, `jill`, `diane`, `omar`. New passwords are generated only during activation setup and stored owner-only at `/home/jaypatel04/Workspace/Projects/pm-tool/.runtime/tuesday-review-login-handoff.json`; do not put them in logs or Git. Normal visible app copy follows Jay's explicit override; fixture provenance remains in source/private setup records.

## Remaining limitations and decisions

T023 manual screen-reader validation remains BLOCKED/UNRUN after VoiceOver access timed out; keyboard/axe evidence does not replace it. SC-001's actual-supervisor exercise and Jay's visual acceptance remain unrun. Protected historical records and the local 12-person/500-entry/50-allocation performance fixture retain legacy labels under their existing edit/idempotence contracts; that benchmark is excluded from the hosted ten-person recipe. See the main handoff's Copy limits section. Readiness.cs:108 is unchanged; the decision-to-task fixture edge is omitted and not verified. Existing stateless cookie replay after logout and off-host backup retrieval remain documented limitations.

Open decisions: final brand mark, approval of the completed current hero recapture/blend, holiday/manual-role removal confirmations, and per-notification Mark read. The prior 29e9b15c activation command is superseded. Evidence documents are updated locally after the pinned code commit; the three unrelated dirty files remain preserved.
