#!/usr/bin/env python3
"""Fail when server code uses a message key that src/Hub.Api/Text.cs does not define.

Text.Get falls back to the key itself, so a missing entry reaches people as a raw key such as
"perm.pickup_team". This scans the calls that take a message key and lists any without an entry.
"""
import pathlib
import re
import sys

ROOT = pathlib.Path(__file__).resolve().parents[1]
KEY_CALLS = [
    r'Allow\.No\("([a-z][\w.]*)"',
    r'Text\.Get\("([a-z][\w.]*)"',
    r'ApiException\.Forbidden\("([a-z][\w.]*)"',
    r'ApiException\.Invalid\("[^"]*",\s*"([a-z][\w.]*)"',
    r'ApiException\.Rule\("[^"]*",\s*"([a-z][\w.]*)"',
    r'Check\.(?:That|Reason|OneOf)\([^;]*?,\s*"[^"]*",\s*"([a-z][\w.]*)"',
]


def defined(root):
    return set(re.findall(r'\["([^"]+)"\]\s*=', (root / "src/Hub.Api/Text.cs").read_text(encoding="utf-8")))


def used(root):
    found = {}
    for path in sorted((root / "src").rglob("*.cs")):
        if "Migrations" in path.parts or "bin" in path.parts or "obj" in path.parts:
            continue
        text = path.read_text(encoding="utf-8")
        for pattern in KEY_CALLS:
            for m in re.finditer(pattern, text):
                if "." in m.group(1):
                    found.setdefault(m.group(1), f"{path.relative_to(root)}:{text.count(chr(10), 0, m.start()) + 1}")
    return found


def missing(root=ROOT):
    have = defined(root)
    return {key: where for key, where in used(root).items() if key not in have}


def main():
    gaps = missing()
    for key, where in sorted(gaps.items()):
        print(f"missing server text key {key} (first used at {where})")
    print(f"text keys: {len(gaps)} missing")
    return 1 if gaps else 0


if __name__ == "__main__":
    sys.exit(main())
