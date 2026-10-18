#!/usr/bin/env python3
"""Fails when coverage is below the constitution's quality gates (§22):
rules engine (Hub.Domain) branch coverage >= 95 %, application services (Hub.Api Features) line coverage >= 70 %.
Shard files are merged by file and line (§43.5, FR-DLV-12): a line is covered when any file hit it, and its
covered branches are the highest count any file reports (a lower bound, so the merge never over-reports).
Usage: python3 tools/coverage_gate.py <results-directory>"""
import glob, sys, xml.etree.ElementTree as ET

def merged(files, pkg, path_part=None):
    """{(class, file, line): [hit, covered branches, branches]} across every file."""
    out = {}
    for f in files:
        for p in ET.parse(f).getroot().iter("package"):
            if p.get("name") != pkg:
                continue
            for c in p.iter("class"):
                name = c.get("filename", "").replace("\\", "/")
                if name.endswith(".g.cs") or "/obj/" in name:  # source-generated code (e.g. [GeneratedRegex]) is not ours to test
                    continue
                if path_part and path_part not in name:
                    continue
                for line in c.iter("line"):
                    a = b = 0
                    cond = line.get("condition-coverage")
                    if cond:
                        a, b = map(int, cond.split("(")[1].rstrip(")").split("/"))
                    # Keyed by class too, so one file's duplicate lines (one per class) count as today's script counts them.
                    seen = out.setdefault((c.get("name"), name, line.get("number")), [False, 0, 0])
                    seen[0] |= int(line.get("hits", "0")) > 0
                    seen[1] = max(seen[1], a); seen[2] = max(seen[2], b)
    return out

def totals(files, pkg, path_part=None):
    rows = merged(files, pkg, path_part).values()
    lines, lv = len(rows), sum(r[0] for r in rows)
    bv, br = sum(r[1] for r in rows), sum(r[2] for r in rows)
    return (lv / lines * 100 if lines else 100.0), (bv / br * 100 if br else 100.0)

if __name__ == "__main__":
    files = glob.glob(f"{sys.argv[1] if len(sys.argv) > 1 else 'coverage'}/**/coverage.cobertura.xml", recursive=True)
    if not files:
        sys.exit("no coverage files found")
    _, rules_branches = totals(files, "Hub.Domain")
    services_lines, _ = totals(files, "Hub.Api", "/Features/")
    print(f"Coverage files merged: {len(files)}")
    print(f"Rules engine branch coverage: {rules_branches:.1f} % (gate 95 %)")
    print(f"Service line coverage: {services_lines:.1f} % (gate 70 %)")
    ok = rules_branches >= 95 and services_lines >= 70
    sys.exit(0 if ok else 1)
