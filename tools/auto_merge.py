#!/usr/bin/env python3
"""Safely enable Tier A squash auto-merge after trusted CI and reviewer status pass."""
import argparse
import json
import os
import re
import subprocess

REQUIRED = {"domain", "api-1", "api-2", "api-3", "web", "checks", "gate", "tier"}
EXACT_TIER_B = {
    "src/Hub.Domain/Permissions.cs", "src/Hub.Api/Infrastructure/Auth.cs",
    "src/Hub.Api/Infrastructure/SocialAuth.cs", "specs/DECISIONS.md", "specs/human-only.txt",
}


def tier_b_paths(paths, contract_migrations=()):
    result = set()
    for path in paths:
        if (path in EXACT_TIER_B or path.startswith(("spec-parts/", ".specify/", "hosting/", ".github/"))
                or (path.startswith("specs/") and path.endswith("/spec.md"))
                or (path.startswith("scripts/") and "homedev" in path.rsplit("/", 1)[-1])
                or path in contract_migrations):
            result.add(path)
    return result


def checks_green(check_runs):
    latest = {}
    for run in check_runs:
        old = latest.get(run['name'])
        if old is None or run.get('id', 0) > old.get('id', 0):
            latest[run['name']] = run
    return all(latest.get(name, {}).get("conclusion") == "success" for name in REQUIRED)


def review_green(statuses, reviewers):
    """The newest `review` status set by a trusted reviewer account decides; `statuses` is newest first."""
    for status in statuses:
        if status.get("context") == "review" and (status.get("creator") or {}).get("login") in reviewers:
            return status.get("state") == "success"
    return False


def gh(repo, path):
    return json.loads(subprocess.run(["gh", "api", f"repos/{repo}/{path}"], capture_output=True, text=True, check=True).stdout)


def main(argv=None):
    parser = argparse.ArgumentParser()
    parser.add_argument("--repo", required=True)
    parser.add_argument("--run-id")
    parser.add_argument("--sha")
    args = parser.parse_args(argv)
    if not args.run_id and not args.sha:
        parser.error("one of --run-id or --sha is required")
    if args.run_id:
        run = gh(args.repo, f"actions/runs/{args.run_id}")
        if run.get("conclusion") != "success" or not run.get("pull_requests"):
            return 0
        sha = run.get("head_sha")
        pr_number = run["pull_requests"][0]["number"]
    else:
        sha = args.sha
        prs = gh(args.repo, f"commits/{sha}/pulls")
        if not prs:
            return 0
        pr_number = prs[0]["number"]
    pr = gh(args.repo, f"pulls/{pr_number}")
    if not sha or pr.get("head", {}).get("sha") != sha or pr.get("base", {}).get("ref") != "main":
        return 0
    labels = {label["name"] for label in pr.get("labels", [])}
    if labels & {"tier-b", "tier-b-ok", "large", "replace-test"}:
        return 0
    files = gh(args.repo, f"pulls/{pr_number}/files?per_page=100")
    # shortcut: auto-merge inspects one page of at most 100 files; larger changes wait for Jay.
    if pr.get('changed_files', len(files)) > len(files):
        return 0
    contract_migrations = {
        file["filename"] for file in files
        if file["filename"].startswith("src/Hub.Api/Data/Migrations/")
        and (not file.get("patch") or re.search(r"\+[^\n]*(Drop|Rename|AlterColumn|DropCheckConstraint|DropForeignKey|Sql)", file["patch"]))
    }
    changed_steps = {file["filename"] for file in files if file["filename"] == "tools/ci_steps.txt"
                     and any(line.startswith("-") and not line.startswith("---") for line in file.get("patch", "").splitlines())}
    if tier_b_paths([file["filename"] for file in files if file["filename"] not in changed_steps], contract_migrations) or changed_steps:
        return 0
    checks = gh(args.repo, f"commits/{sha}/check-runs?per_page=100").get("check_runs", [])
    # The full status list carries each creator; the combined status endpoint does not.
    statuses = gh(args.repo, f"commits/{sha}/statuses?per_page=100")
    reviewers = {login for login in os.environ.get("REVIEWER_LOGINS", "").split(",") if login} or {args.repo.split("/")[0]}
    if not checks_green(checks) or not review_green(statuses, reviewers):
        return 0
    subprocess.run(["gh", "pr", "merge", "-R", args.repo, str(pr_number), "--auto", "--squash", "--delete-branch",
                    "--match-head-commit", sha], check=True)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
