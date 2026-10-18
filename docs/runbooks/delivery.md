# Runbook: delivery

How changes reach `main` and the review stack (§43, packet 039). FR-DLV-01, FR-DLV-02, FR-DLV-03, FR-DLV-20.
Jay applies the repository settings below, or authorizes an agent to apply them; agents never change them on
their own. Until he does, the protection is not in effect.

## Protection of main (FR-DLV-01)

`main` is the only integration branch. Changes arrive only through pull requests and merge by squash. Direct
pushes and force pushes are refused, including Jay's own (admins are included). There are no release branches: a
release is a green `main` commit.

| Setting | Value |
|---|---|
| Merge methods | Squash only; merge commits and rebase merges off |
| Squash commit title | The pull request title (`039 S03: ...`) |
| Head branch | Deleted automatically on merge |
| Pull request required | Yes, with no required approvals (agents push as Jay's account; Tier B is a label control, D2) |
| Required checks | `domain`, `api-1`, `api-2`, `api-3`, `web`, `checks`, `gate` |
| Require branches to be up to date | Off, because main's own CI run gates the release image |
| Include administrators | On |
| Force pushes and deletion of `main` | Refused |

The required checks are the jobs of `.github/workflows/ci.yml` from slices S01 to S04. `checks` carries the size
check (S01), the migration lint (S04) and the trace check `python3 tools/trace_spec.py --check`; `gate` carries the
coverage merge and the test-count ratchet (S02, S03). Slice S13 adds the `tier` and `review` checks and automatic
merge; add them to this list then.

Apply, from Jay's terminal with `gh` signed in as the repository owner:

```sh
repo=jayapatel1511-hub/PM-Tool
gh api -X PATCH "repos/$repo" \
  -F allow_squash_merge=true -F allow_merge_commit=false -F allow_rebase_merge=false \
  -F delete_branch_on_merge=true -f squash_merge_commit_title=PR_TITLE -f squash_merge_commit_message=PR_BODY
gh api -X PUT "repos/$repo/branches/main/protection" --input - <<'JSON'
{
  "required_status_checks": {
    "strict": false,
    "contexts": ["domain", "api-1", "api-2", "api-3", "web", "checks", "gate"]
  },
  "enforce_admins": true,
  "required_pull_request_reviews": { "required_approving_review_count": 0 },
  "restrictions": null,
  "allow_force_pushes": false,
  "allow_deletions": false
}
JSON
```

Read back, and paste both outputs into `specs/039-delivery-process/acceptance.md` for AC-DLV-01:

```sh
gh api "repos/$repo" --jq '{allow_squash_merge, allow_merge_commit, allow_rebase_merge, delete_branch_on_merge, squash_merge_commit_title}'
gh api "repos/$repo/branches/main/protection" --jq '{checks: .required_status_checks.contexts, strict: .required_status_checks.strict, admins: .enforce_admins.enabled, reviews: .required_pull_request_reviews.required_approving_review_count, force: .allow_force_pushes.enabled, deletions: .allow_deletions.enabled}'
```

Then confirm AC-DLV-01 by hand: a direct `git push origin HEAD:main` is refused; a pull request with a failing
required check shows no merge button; a merged pull request lands as one squash commit and its head branch is gone.
AC-DLV-01 is listed in `specs/human-only.txt`.

## Branches (FR-DLV-02)

- Start every work branch from `origin/main` and name it `a/<packet>-<slice>`, for example `a/039-s03`. One slice per
  branch.
- Merge `origin/main` into the branch before each push.
- Title the pull request with the packet and slice: `039 S03: ...`.
- Do not create long-lived `claude/*` or `codex/*` integration branches.

### Drift limit (D3)

A branch may fall behind `origin/main` by at most 50 commits on main since it started. The limit counts commits, not
time. Check it with:

```sh
git fetch origin
git rev-list --count "$(git merge-base HEAD origin/main)..origin/main"
```

Above 50, either merge the branch dark behind its flag, or push it as `archive/<branch>` and close it:

```sh
git push origin "HEAD:refs/heads/archive/<branch>"
```

## Red main stops the line (FR-DLV-03)

When CI fails on a `main` commit, the next change merged is a revert of the failing change or a forward fix of that
failure, and nothing else. No release image is published from a red commit, so the review stack never receives it.
A revert is `git revert <sha>` on a fresh `a/<packet>-<slice>` branch, merged through a pull request like any other
change.

## Rollback order (FR-DLV-20)

Use the cheapest step that works, in this order:

1. Turn the flag off in Admin. It is audited and needs no deploy.
2. The review deploy timer's automatic return to the previous digest (slice S14).
3. A manual return to a named digest.
4. Restore the pre-deploy dump into a new isolated database, as the
   [homedev review runbook](homedev-review.md#rollback-and-recovery) requires, test it there, then choose a controlled
   cutover.

A down migration is never run and a database volume is never deleted. The daily dump timers and restore drills stay
unchanged; see [restore](restore.md).
