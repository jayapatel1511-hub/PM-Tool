# Packets 052–053: public synthetic review deployment

Date: 2026-11-11, America/Halifax (2026-11-12 UTC). Target: **public synthetic review only**, `https://pm.engcalchub.com`,
homedev port 3080, existing `pm-tool-review-db`. Pilot and production are separate and unchanged.

Jay asked to finish the build, review it, then "deploy once done and verify". Release gates, the independent reviews and the
fixes they produced are in [053 verification](../../specs/053-screen-completeness/verification.md), "Build completion · 2026-11-11".

**State: activation PASS.** Public synthetic review runs `88b33103c1102058b85ec797cb15eb57e81cc541` (previous
`bf8a39d7290d8bad8f9138872582b164baaf1894`). Every packet 052/053 flag, `task_pickup` and `verifiable_log` is stored off.

## Preparation (Claude, from the Mac)

- Host preflight, read-only: `current` = `bf8a39d7`, private health Healthy with that build, Social sign-in with tester passwords,
  review deploy timer and service inactive, the 22:00 UTC review dump written, tunnel active, 202 GB disk and 4.2 GiB memory free.
- `scripts/prepare-homedev-review.sh 88b33103…` staged the committed tree as `releases/88b33103…`; a blob-hash comparison of all
  4,344 regular files against the commit matched. An earlier staging of `d83b150b` is left unused in `releases/`.
- A one-use guard in `incoming/deploy-052-053-20261111/` (SHA-256 `2afcd919…`), adapted from the 039–051 guard: it requires
  the previous release and image `bf8a39d7`, an idle deploy timer and service, and all ten guarded flags off; runs
  `scripts/activate-homedev-review.sh <sha> --social-auth` and the public `verify-homedev-review.py --social --expect-build`;
  expects 56 migrations, no lost migration, the immutable triggers and the unit null-equality index, an unchanged credential
  verifier file and the candidate image running as UID 1000; on failure it restores `bf8a39d7` and checks it.
- No new migration adds a required column without a default, so the previous image can still write the upgraded schema. Jay's
  deploy instruction stands in for the `contract` label on `NoticeDeliveryPolicyWitness` and `ExternalIssuedReviewSources`.

## Activation (Jay, homedev terminal, his own sudo)

Started 01:50:37 UTC, PASS at 01:56:34 UTC. A second launch from Claude's terminal tab a few seconds later stopped at the guard's
one-use check before any sudo or Docker step. Private result: `data/deploy-88b33103…/{before,after,result}.json`.

| Check | Result |
|---|---|
| Migrations | 42 → 56, all 42 kept; the 14 new ones are the 052/053 set ending `DateReasonCategories` |
| Guarded flags after | all ten stored false |
| Tables | 130 → 147; none dropped, none with fewer rows |
| Core records (`app_user`, `project`, `task`, `comment`) | unchanged (15, 8, 46, 8) |
| Seeded or backfilled | 7 date-move reasons, 5 issue purposes, 4 return codes, 124 work-assignment occurrences |
| Other row changes during the window | `org_setting` +13 (new settings), `job_run` +13, `activity_log` +24; the review stayed writable, so these are not attributed |
| Evaluation events at the attempt limit | 0 before, 0 after |
| Immutable triggers, unit null-equality index, credential verifiers, image user | present, present, unchanged, `1000:1000` |
| Guard's public verification | PASS (health, anonymous/Host/Origin denial, wrong-password denial, individual sign-in, `/me`, sign-out) |

## Independent checks after activation (Claude)

- Private origin: Healthy, build `88b33103…`.
- Public, through the tunnel: `/health` Healthy with build `88b33103…`; config Social with tester passwords and both providers;
  anonymous `/api/v1/me` and `/api/v1/admin/settings` 401; unsafe-Origin POST 403; `/.env` 404; HSTS, CSP and nosniff present.
- Browser (built-in, signed out): the landing page and `/login` render with Microsoft, Google and the tester form, no
  `data-visual` attribute (visual flag off). Console: the expected anonymous `/me` 401, and Cloudflare's injected analytics
  beacon blocked by the app's CSP (`script-src 'self'`), which is Cloudflare's edge injection, not this release.

## Not covered

Signed-in hosted screens after activation were not opened by Claude (no credentials are used here); the guard's verification
covers sign-in and `/me`. No 052/053 flag was turned on in the hosted review. Pilot and production, hosted and human acceptance,
off-host backup of this release's dump, and pushing the commits remain open. Turning any 052/053 flag on in pilot still waits
on 052-D1–D10 and the AC-CALM-05 word-budget decision.
