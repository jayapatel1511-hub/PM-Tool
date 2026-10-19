#!/usr/bin/env python3
"""Small, dependency-free source ratchets for packet 040.

Counters are deliberately conservative: until a replacement exists they are
report-only.  A baseline may only move down with ``--update``.
"""
from __future__ import annotations

import argparse
import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
BASELINE = ROOT / "tools" / "ratchet.json"

PATTERNS = {
    "api_helpers_without_type": ("web/src", re.compile(r"\b(?:get|post|put|patch|del)\s*\(")),
    "task_object_handlers": ("src/Hub.Api/Features", re.compile(r"Task<object>")),
    "untyped_results": ("src/Hub.Api/Features", re.compile(r"\bResults\.(?:Ok|Created|BadRequest|NotFound|NoContent|Conflict)\b")),
    "anonymous_objects": ("src/Hub.Api/Features", re.compile(r"\bnew\s*\{")),
    "direct_invalidate_queries": ("web/src", re.compile(r"\binvalidateQueries\s*\(")),
    "runtime_text_keys": ("web/src", re.compile(r"\bruntimeText\s*\(")),
    "html_tables": ("web/src", re.compile(r"<table\b")),
    "load_percentage_literals": ("web/src", re.compile(r"(?:(?:\b[\w$]+\??\.pct|\b(?:loadPct|loadPercent|loadPercentage))\s*(?:>=|<=|>|<|===?|!==?)\s*\d|\b\d+(?:\.\d+)?\s*(?:>=|<=|>|<|===?|!==?)\s*(?:[\w$]+\??\.pct|(?:loadPct|loadPercent|loadPercentage))\b)")),
    "page_permission_derivations": ("web/src/pages", re.compile(r"\bpermissions\s*\.\s*(?:isPm|leadOf)\b|\bmanageDisciplineIds\s*\.\s*(?:length\b|includes\s*\()")),
    "client_permission_derivations": ("web/src", re.compile(r"\b(?:can|hasPermission|permission)\w*\s*=")),
}

UN_TYPED_HELPER = re.compile(r"(?<![.\w])(?:get|post|put|patch|del)\s*\(")


def _files(path: Path):
    if path.is_file():
        yield path
    elif path.exists():
        yield from (p for p in path.rglob("*") if p.is_file() and ".git" not in p.parts and "node_modules" not in p.parts)


def count_sources() -> dict[str, int]:
    values: dict[str, int] = {}
    for name, (relative, pattern) in PATTERNS.items():
        if name == "api_helpers_without_type":
            values[name] = sum(len(UN_TYPED_HELPER.findall(p.read_text(encoding="utf-8", errors="ignore"))) for p in _files(ROOT / relative) if p.suffix in {".ts", ".tsx"})
            continue
        values[name] = sum(len(pattern.findall(p.read_text(encoding="utf-8", errors="ignore"))) for p in _files(ROOT / relative)
                            if not (name == "direct_invalidate_queries" and p.name == "keys.ts")
                            and not (name == "html_tables" and p.as_posix().endswith("components/ui/table.tsx")))
    return values


def _parse_text(text: str, server: bool) -> dict[str, str]:
    if server:
        return {json.loads('"' + key + '"'): json.loads(value) for key, value in re.findall(r'\["((?:\\.|[^"\\])*)"\]\s*=\s*("(?:\\.|[^"\\])*")', text)}
    import ast
    return {key: ast.literal_eval(value) for key, value in re.findall(r"['\"]([^'\"]+)['\"]\s*:\s*((?:\"(?:\\.|[^\"\\])*\")|(?:'(?:\\.|[^'\\])*'))", text)}


def _text_keys(path: Path) -> dict[str, str]:
    return _parse_text(path.read_text(encoding="utf-8"), path.suffix == ".cs") if path.exists() else {}


def hard_checks() -> list[str]:
    errors: list[str] = []
    server = _text_keys(ROOT / "src/Hub.Api/Text.cs")
    client = _text_keys(ROOT / "web/src/i18n/en.ts")
    for key in sorted(set(server) & set(client)):
        if server[key] != client[key]:
            # Existing differences are recorded in the baseline; only a new
            # difference is a failure.
            allowed = json.loads(BASELINE.read_text()).get("text_differences", {}) if BASELINE.exists() else {}
            if allowed.get(key) != {"server": server[key], "client": client[key]}:
                errors.append(f"text mismatch: {key}")
    for path in (ROOT / "web/src/pages").rglob("*.tsx") if (ROOT / "web/src/pages").exists() else []:
        text = path.read_text(encoding="utf-8", errors="ignore")
        if "ViewMenu" in text or "SavedView" in text or "SavedViews" in text:
            for match in re.finditer(r"(?:searchParams|params|sp)\.(?:get|set)\(['\"]view['\"]", text):
                errors.append(f"reserved view parameter: {path}:{text[:match.start()].count(chr(10)) + 1}")
    return errors


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--update", action="store_true", help="lower the committed baseline")
    args = parser.parse_args(argv)
    current = count_sources()
    baseline = json.loads(BASELINE.read_text()) if BASELINE.exists() else {"counters": {}}
    old = baseline.setdefault("counters", {})
    failures = []
    for name, value in current.items():
        previous = old.get(name, {"value": value, "ratchet": False})
        if args.update:
            if value > previous.get("value", value):
                failures.append(f"{name}: update would raise {previous.get('value')} to {value}")
            else:
                old[name] = {"value": value, "ratchet": previous.get("ratchet", False)}
        elif previous.get("ratchet", False) and value > previous.get("value", value):
            failures.append(f"{name}: {value} > baseline {previous['value']}")
        print(f"{name}: {value} (baseline {previous.get('value', value)})")
    failures.extend(hard_checks())
    if args.update and not failures:
        BASELINE.write_text(json.dumps(baseline, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    for failure in failures:
        print(f"FAIL: {failure}", file=sys.stderr)
    return 1 if failures else 0


if __name__ == "__main__":
    raise SystemExit(main())
