# Engineering Project Coordination Hub agent rules

Product behaviour is defined by `Engineering-Project-Coordination-Hub-Specification.md`; packet behaviour and
acceptance are defined by `specs/`.

- No AI, machine-learning or LLM features are permitted in the product.
- Work from `origin/main` on one `a/<packet>-<slice>` branch with disjoint path ownership. At most five agents write
  concurrently, with one schema-lane writer and one separate reviewer per pull request.
- Tier A covers dark additions, guards, tests, documentation, unchanged-test refactors, added `tools/ci_steps.txt`
  lines and patch or minor dependency updates. Tier B covers specification and decision files, `.github/`, hosting,
  homedev scripts, auth and permissions, contract migrations, approved behaviour and any `large` or `replace-test` change.
- Agents never apply `tier-b-ok`, `contract`, `large` or `replace-test`; never change decision status, turn on a flag,
  deploy or delete a branch/worktree. Jay owns Tier B merges.
- The boy-scout rule allows current-pattern cleanup only inside files already changed by the slice, with tests and
  behaviour unchanged. It never expands the pull request.
- Hostnames, origins and deployment targets are environment-specific. Verify the current target before treating a
  hostname observation as evidence; local or fixture results do not prove hosted acceptance.
- Record passing, failing, skipped and unrun checks in the packet's `verification.md` until the generated report is
  published on main. Do not commit or push unless Jay explicitly requests it.
