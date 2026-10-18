#!/usr/bin/env python3
"""Pull request checks (§43). `size`: hand-written changed lines against the limits of FR-DLV-06 (039-D4).
`migrations`: the expand-only lint of FR-DLV-15 over migrations the pull request adds.

    python3 tools/pr_checks.py size --base <sha> [--head HEAD] [--labels large,other]
    python3 tools/pr_checks.py migrations --base <sha> [--head HEAD] [--labels contract,other]
"""
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
NOT_AN_OPERATION = re.compile(r"\bON\s+(DELETE|UPDATE)\b|'(?:[^']|'')*'|--[^\n]*", re.I)


def up_body(source):
    """Text of Up(): EF Core scaffolds Up before Down, so it runs from `void Up(` to `void Down(` or the end."""
    start = source.find("void Up(")
    if start < 0:
        return ""
    end = source.find("void Down(", start)
    return source[start:] if end < 0 else source[start:end]


def sql_literal(text):
    """The string literal at the start of a Sql( argument: raw, verbatim or regular, with $ and @ prefixes.
    Not a literal (a variable or call): the rest of the line, so its keywords are still seen."""
    m = re.match(r'\$*@?\$*("{3,}|")', text)
    if not m:
        return text.split("\n", 1)[0]
    quote, body = m.group(1), text[m.end():]
    if len(quote) >= 3:
        return body.split(quote, 1)[0]
    if "@" in text[:m.start(1)]:
        return re.match(r'(?:[^"]|"")*', body).group(0)
    return re.match(r'(?:[^"\\]|\\.)*', body, re.S).group(0)


def contract_operations(source):
    """Contract operations in a migration's Up(): drop, rename and alter-column calls, and Sql text with
    DROP, TRUNCATE, DELETE, RENAME or ALTER ... TYPE. Quoted SQL text, `--` comments and ON DELETE / ON UPDATE
    referential actions are not operations."""
    body = up_body(source)
    found = [m.group(1) for m in CONTRACT_CALL.finditer(body)]
    for m in SQL_CALL.finditer(body):
        sql = NOT_AN_OPERATION.sub("", sql_literal(body[m.end():]))
        found += [f"Sql {(k.group(1) or 'ALTER ... TYPE').upper()}" for k in CONTRACT_SQL.finditer(sql)]
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
    args = parser.parse_args(argv)
    check = size if args.check == "size" else migrations
    code, message = check(args.base, args.head, [l.strip() for l in args.labels.split(",") if l.strip()])
    print(message)
    return code


if __name__ == "__main__":
    sys.exit(main())
