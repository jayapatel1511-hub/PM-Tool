#!/usr/bin/env python3
"""Pull request checks (§43). `size`: hand-written changed lines against the limits of FR-DLV-06 (039-D4).
`migrations`: the expand-only lint of FR-DLV-15 over migrations the pull request adds.
`newest-contract`: the newest migration in the tree whose Up() holds a contract operation, or `none`; CI records it
as the release image's contract-migration label (FR-DLV-15).
`tier`: require Jay's `tier-b-ok` label for Tier B paths or an explicit `tier-b` label (FR-DLV-28).

    python3 tools/pr_checks.py size --base <sha> [--head HEAD] [--labels large,other]
    python3 tools/pr_checks.py migrations --base <sha> [--head HEAD] [--labels contract,other]
    python3 tools/pr_checks.py newest-contract
    python3 tools/pr_checks.py tier --base <sha> [--head HEAD] [--labels tier-b,tier-b-ok]
"""
import pathlib
import argparse
import re
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


MIGRATIONS = "src/Hub.Api/Data/Migrations/"
CONTRACT_CALL = re.compile(r"\.\s*(Drop\w*|Rename\w*|AlterColumn)\s*[<(]")
SQL_CALL = re.compile(r"\.\s*Sql\s*\(\s*")
CONTRACT_SQL = re.compile(r"\b(DROP|TRUNCATE|DELETE|RENAME)\b|\bALTER\b[^;]*?\bTYPE\b", re.I)
EVENT = r"(?:INSERT|UPDATE(?:\s+OF\s+[\w\s,\"]+?)?|DELETE|TRUNCATE)"
NOT_AN_OPERATION = re.compile(r"\bON\s+(DELETE|UPDATE)\b|'(?:[^']|'')*'|--[^\n]*"
                              rf"|\b(?:BEFORE|AFTER|INSTEAD\s+OF)\s+{EVENT}(?:\s+OR\s+{EVENT})*\s+ON\b", re.I)
DYNAMIC_SQL = re.compile(r"\bEXECUTE\b(?!\s+(?:FUNCTION|PROCEDURE)\b)", re.I)
UNVERIFIABLE = "Sql argument that is not a single literal"


def up_body(source):
    """Text of Up(): EF Core scaffolds Up before Down, so it runs from `void Up(` to `void Down(` or the end."""
    start = source.find("void Up(")
    if start < 0:
        return ""
    end = source.find("void Down(", start)
    return source[start:] if end < 0 else source[start:end]


def sql_literal(text):
    """The text of a Sql( argument that is one complete string literal (raw, verbatim or regular, with $ and @
    prefixes), optionally followed by `suppressTransaction:`. None when the SQL cannot be read: an identifier, a
    call, or a literal joined with `+` or followed by another argument."""
    m = re.match(r'\$*@?\$*("{3,}|")', text)
    if not m:
        return None
    quote, start = m.group(1), m.end()
    if len(quote) >= 3:
        end = text.find(quote, start)
        sql, after = (text[start:end], text[end + len(quote):]) if end >= 0 else (text[start:], "")
    else:
        verbatim = "@" in text[:m.start(1)]
        sql = re.match(r'(?:[^"]|"")*' if verbatim else r'(?:[^"\\]|\\.)*', text[start:], re.S).group(0)
        after = text[start + len(sql) + 1:]
    return sql if re.match(r"\s*(?:\)|,\s*suppressTransaction\s*:)", after) else None


def contract_operations(source):
    """Contract operations in a migration's Up(): drop, rename and alter-column calls, and Sql text with
    DROP, TRUNCATE, DELETE, RENAME or ALTER ... TYPE. Quoted SQL text, `--` comments, ON DELETE / ON UPDATE
    referential actions and trigger events (BEFORE UPDATE OR DELETE ON) are not operations. SQL the lint cannot
    read, a non-literal Sql argument or dynamic EXECUTE, counts as one."""
    body = up_body(source)
    found = [m.group(1) for m in CONTRACT_CALL.finditer(body)]
    for m in SQL_CALL.finditer(body):
        literal = sql_literal(body[m.end():])
        if literal is None:
            found.append(UNVERIFIABLE)
            continue
        sql = NOT_AN_OPERATION.sub("", literal)
        found += [f"Sql {(k.group(1) or 'ALTER ... TYPE').upper()}" for k in CONTRACT_SQL.finditer(sql)]
        found += ["Sql dynamic EXECUTE" for _ in DYNAMIC_SQL.finditer(sql)]
    return found


def added_migrations(base, head="HEAD", cwd=None):
    """Migration files the pull request adds; Designer files and the model snapshot are generated."""
    paths = git("diff", "--name-only", "-z", "--diff-filter=A", base, head, "--", MIGRATIONS, cwd=cwd).split("\0")
    return [p for p in paths if p.endswith(".cs") and not p.endswith(".Designer.cs") and not p.endswith("ModelSnapshot.cs")]


def migrations(base, head="HEAD", labels=(), cwd=None):
    """(exit code, message): fails on any contract operation in an added migration unless `contract` is applied."""
    problems = [(path, op) for path in added_migrations(base, head, cwd)
                for op in contract_operations(git("show", f"{head}:{path}", cwd=cwd))]
    if not problems:
        return 0, "No contract operations in added migrations."
    if "contract" in labels:
        return 0, "\n".join(f"::warning file={p}::{op} (label `contract` applied)." for p, op in problems)
    return 1, "\n".join(f"::error file={p}::{op} is a contract operation. Expand only, or Jay applies the label `contract`."
                         for p, op in problems)


def newest_contract(folder=pathlib.Path(__file__).resolve().parents[1] / MIGRATIONS):
    """Newest migration ID (file name without .cs) whose Up() the lint would flag, or "none". IDs start with a
    timestamp, so name order is migration order. SQL the lint cannot read counts, as in the lint."""
    found = [f.stem for f in sorted(pathlib.Path(folder).glob("*.cs"))
             if not f.name.endswith((".Designer.cs", "ModelSnapshot.cs")) and contract_operations(f.read_text())]
    return found[-1] if found else "none"


TIER_B_EXACT = {
    "src/Hub.Domain/Permissions.cs",
    "src/Hub.Api/Infrastructure/Auth.cs",
    "src/Hub.Api/Infrastructure/SocialAuth.cs",
}


def tier_b_paths(base, head="HEAD", cwd=None):
    """Return changed paths that require Jay's Tier B merge control."""
    paths = set(git("diff", "--name-only", base, head, cwd=cwd).splitlines())
    result = {p for p in paths if p in TIER_B_EXACT or p.startswith(("spec-parts/", ".specify/", "hosting/", ".github/"))
              or p in {"specs/DECISIONS.md", "specs/human-only.txt"}
              or (p.startswith("specs/") and p.endswith("/spec.md"))
              or (p.startswith("scripts/") and "homedev" in pathlib.PurePosixPath(p).name)}
    for path in paths:
        if not path.startswith("src/Hub.Api/Data/Migrations/"):
            continue
        for ref in (head, base):
            try:
                source = git("show", f"{ref}:{path}", cwd=cwd)
            except subprocess.CalledProcessError:
                continue
            if contract_operations(source):
                result.add(path)
                break
    if "tools/ci_steps.txt" in paths:
        diff = git("diff", "--unified=0", base, head, "--", "tools/ci_steps.txt", cwd=cwd)
        if any(line.startswith("-") and not line.startswith("---") for line in diff.splitlines()):
            result.add("tools/ci_steps.txt")
    return result


def tier(base, head="HEAD", labels=(), cwd=None):
    """(exit code, message) for the required Tier B label control."""
    labels = set(labels)
    paths = tier_b_paths(base, head, cwd)
    if "tier-b" in labels or "large" in labels or "replace-test" in labels:
        paths.add("(explicit Tier B label)")
    if paths and "tier-b-ok" not in labels:
        return 1, "Tier B merge control required for: " + ", ".join(sorted(paths)) + "; Jay must apply `tier-b-ok`."
    if paths:
        return 0, "Tier B merge control satisfied by `tier-b-ok`."
    return 0, "Tier A paths; automatic merge remains allowed after green checks and a clear `review` status."


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    sub = parser.add_subparsers(dest="check", required=True)
    s = sub.add_parser("size")
    s.add_argument("--base", required=True)
    s.add_argument("--head", default="HEAD")
    s.add_argument("--labels", default="", help="comma-separated pull request labels")
    m = sub.add_parser("migrations")
    m.add_argument("--base", required=True)
    m.add_argument("--head", default="HEAD")
    m.add_argument("--labels", default="", help="comma-separated pull request labels")
    sub.add_parser("newest-contract")
    t = sub.add_parser("tier")
    t.add_argument("--base", required=True)
    t.add_argument("--head", default="HEAD")
    t.add_argument("--labels", default="", help="comma-separated pull request labels")
    args = parser.parse_args(argv)
    if args.check == "newest-contract":
        print(newest_contract())
        return 0
    if args.check == "tier":
        code, message = tier(args.base, args.head, [l.strip() for l in args.labels.split(",") if l.strip()])
        print(message)
        return code
    check = size if args.check == "size" else migrations
    code, message = check(args.base, args.head, [l.strip() for l in args.labels.split(",") if l.strip()])
    print(message)
    return code


if __name__ == "__main__":
    sys.exit(main())
