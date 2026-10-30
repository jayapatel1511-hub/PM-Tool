# Packets 039–051: deployment preflight

Date: 2026-10-29, America/Halifax. Target: **public synthetic review only**, `https://pm.engcalchub.com`, homedev port 3080, existing `pm-tool-review-db`. Pilot and production are separate. Jay requested “deploy and make tree clean”; this authorizes the deployment work but does not activate feature flags or mark packets Accepted.

**State: awaiting operator activation.** Homedev requires interactive sudo in Jay's own terminal. The source is prepared for a committed archive and one-use operator handoff; the live `current` pointer remains `f6f31d7e4b7b24875371443ed222d994ee0a4330` until activation succeeds. The exact prepared SHA and source manifest are recorded under `/home/jaypatel04/Workspace/Projects/pm-tool/incoming/deploy-039-051-20261029/` after this record is committed.

## Deployment compatibility correction

Live inspection found Social mode with **both Microsoft/Google and existing tester-password access** enabled. The integration branch shared an earlier base and omitted the deployed `f6f31d7e` compatibility change. Deploying it as-is would have removed existing testers' login.

The current source restores that deployed intent: an explicit `Auth:Social:AllowLocalPassword` option, false by default and refused in Production; the review Social override enables it. Existing user IDs, roles, verifier files, keys and PostgreSQL-backed sessions are retained. Password-origin sessions use the Social cookie/sessions in this mode, retain verifier validation, and are revoked through both logout routes. Social identities continue independently of the password verifier. The UI offers both paths and retains provider connection in Preferences. `ClientConfig.localPasswordEnabled` is an additive public boolean; the typed record and generated schema/client include it. No new password-registration flow, provider credential or schema migration is introduced by this correction.

The known inactive-assignee pickup P2 remains open. The operator guard checks the actual stored `flag.task_pickup` and `flag.verifiable_log` values and refuses activation if either is on. Missing values before migration are treated as their declared false defaults; after seeding, both must still be false. It does not flip them.

## Verification

| Check | Result and boundary |
|---|---|
| Auth, local-account, persistent-session, provisioning and contract tests | PASS, 83/83 (0 failures/skips), final Release test run, 59 s |
| Focused tester-preservation regression | PASS, 1/1 on actual API/PostgreSQL, including both logout routes, copied-cookie rejection, profile/role preservation, provider connection, verifier rotation and disablement with a valid cookie |
| Built-app sign-in browser regression | PASS: provider-only and combined login, wrong/correct tester password, protected Preferences return, actual Account → Sign out and Social POST, provider linking; no page errors or detected WCAG AA violations. Synthetic API/provider fixtures; not real provider consent |
| Web typecheck, lint and production build | PASS; existing lint/bundler warnings only |
| Operator/probe tests | PASS, 8/8 `tools/test_social_activation.py` |
| One-use activation guard | PASS, 3 synthetic flow tests: concurrent counts recorded without spurious rollback; public-probe failure restores Social image after readiness; missing tester login refuses success. Source independently reviewed; no real rollback claimed |
| Generated contract | PASS; public config boolean added, existing fields retained |
| Ratchet / diff | PASS (exit 0); strict counters unchanged. Report-only `client_permission_derivations` is 63 against baseline 62 for the compatibility visibility check; baseline not raised. `git diff --check` passes |
| Original full release gates | Historical PASS at `2dcdaec4`, as recorded in the release handoff. Full suite, coverage, all-browser/parity matrix, audits and exact-image rehearsal were not repeated for this auth preservation change |

During verification, the new long auth scenario initially met the unchanged five-attempt limiter shared by password and provider challenges. Independent session phases now use a fresh test host with the same keys/database. Production throttling is unchanged. Module-contract expectations now include the restored boolean; a disabled password POST is denied as 401 because fresh sign-in deliberately ignores cookies. A valid social GET proves the disabled route is absent (404). The browser test uses the real account-menu action, rather than a direct test-issued logout request.

Local logs are under `/private/tmp/pm-tool-deploy-20261029/`. Earlier release evidence remains under `/private/tmp/pm-tool-release-20261029/`; it is not relabelled as a check of the new auth source.

## Current host preflight

- Host identity `homedev`, user `jaypatel04`, Linux; correct review `current` pointer `f6f31d7e…`.
- Private and public health respond Healthy. Public config reports Social, tester login enabled and both providers. Existing host-side provider/auth/logout/copied-cookie probes PASS; credentials stay on homedev.
- Review tunnel active. Daily backup timer active; latest successful archive was written at 2026-10-29 22:00 UTC. This is an on-host dump, not an off-host recovery claim.
- CI deployment timer inactive. The guard rechecks timer and service immediately before activation.
- 264 GiB disk available and approximately 4.0 GiB memory available at preflight. Runtime directory/key directory remain owner-only.
- `sudo -n` is unavailable. The [server guide](</Users/jaypatel/Personal Server/AGENT-GUIDE.md>) and [review runbook](../runbooks/homedev-review.md) require the administrator to enter sudo in their own terminal; no permission weakening or password collection is used.

## One operator command

After the source archive and handoff have been staged, run in Jay's **homedev terminal**:

```bash
bash /home/jaypatel04/Workspace/Projects/pm-tool/incoming/deploy-039-051-20261029/activate.sh
```

The wrapper names the exact committed SHA; it never takes an uncommitted tree. Its reviewed guard requires the expected previous image/current release, inactive deploy timer/service, and dark pickup/verifiable-log flags. It records before-state metadata, calls the existing activation helper with `--social-auth` (build → verified private dump → existing-volume replacement → private probes), then checks public probes, build stamp, 42 migrations, four immutable triggers, the null-equality index, guarded flags, verifier preservation and image UID. Row-count deltas are recorded for review because the public review stays writable during the build.

If verification fails, the guard restores the prior immutable image using its **Social override**, waits up to 120 seconds for readiness, verifies image identity and the old probes, then restores `current`. It keeps the migrated database and key/verifier files. It never runs a down migration or deletes a volume. The prior `f6f31d7e` image rollback is a prepared recovery path, not a completed host rehearsal.

Private operator results will be written to `data/deploy-<prepared-sha>/{before,after,result}.json`. After operator completion, inspect those records and repeat public build/auth checks before reporting deployed. Actual migrations, post-upgrade flags/counts/triggers/index/jobs, host image identity and hosted/browser acceptance remain UNPROVEN until that happens. New provider consent, pilot/production promotion, Tier B/repository controls, human/visual acceptance, and the known pickup fix remain separate.
