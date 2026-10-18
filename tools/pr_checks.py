#!/usr/bin/env python3
"""Pull request checks (§43). `size`: hand-written changed lines against the limits of FR-DLV-06 (039-D4).

    python3 tools/pr_checks.py size --base <sha> [--head HEAD] [--labels large,other]
"""
import argparse
import subprocess
import sys

WARN_ABOVE, FAIL_ABOVE = 400, 1000


def git(*args, stdin=None, cwd=None):
    return subprocess.run(["git", *args], input=stdin, capture_output=True, text=True, check=True, cwd=cwd).stdout


def changed_lines(base, head="HEAD", cwd=None):
    """{path: added + deleted} between base and head; renames count under the new path, binary files as 0."""
    fields = git("diff", "--numstat", "-z", base, head, cwd=cwd).split("\0")
    counts, i = {}, 0
    while i < len(fields) and fields[i]:
        added, deleted, path = fields[i].split("\t", 2)
        if not path:  # rename: old and new paths follow as separate fields
            path, i = fields[i + 2], i + 2
        counts[path] = (int(added) if added != "-" else 0) + (int(deleted) if deleted != "-" else 0)
        i += 1
    return counts


def generated(paths, cwd=None):
    """Paths that .gitattributes marks linguist-generated (FR-DLV-05)."""
    if not paths:
        return set()
    out = git("check-attr", "-z", "--stdin", "linguist-generated", stdin="\0".join(paths) + "\0", cwd=cwd).split("\0")
    return {out[i] for i in range(0, len(out) - 2, 3) if out[i + 2] in ("set", "true")}


def size_verdict(lines, labels):
    """(exit code, message) for a count of hand-written changed lines."""
    if lines > FAIL_ABOVE and "large" not in labels:
        return 1, f"::error::{lines} hand-written changed lines, above {FAIL_ABOVE}. Split the pull request, or Jay applies the label `large`."
    if lines > WARN_ABOVE:
        return 0, f"::warning::{lines} hand-written changed lines, above {WARN_ABOVE}" + (" (label `large` applied)." if lines > FAIL_ABOVE else ".")
    return 0, f"{lines} hand-written changed lines."


def size(base, head="HEAD", labels=(), cwd=None):
    counts = changed_lines(base, head, cwd)
    skip = generated(list(counts), cwd)
    return size_verdict(sum(n for path, n in counts.items() if path not in skip), set(labels))


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    sub = parser.add_subparsers(dest="check", required=True)
    s = sub.add_parser("size")
    s.add_argument("--base", required=True)
    s.add_argument("--head", default="HEAD")
    s.add_argument("--labels", default="", help="comma-separated pull request labels")
    args = parser.parse_args(argv)
    code, message = size(args.base, args.head, [l.strip() for l in args.labels.split(",") if l.strip()])
    print(message)
    return code


if __name__ == "__main__":
    sys.exit(main())
