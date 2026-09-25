#!/usr/bin/env python3
"""Fails when coverage is below the constitution's quality gates (§22):
rules engine (Hub.Domain) branch coverage >= 95 %, application services (Hub.Api Features) line coverage >= 70 %.
Usage: python3 tools/coverage_gate.py <results-directory>"""
import glob, sys, xml.etree.ElementTree as ET

def totals(files, pkg, path_part=None):
    lines = lv = br = bv = 0
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
                    lines += 1
                    lv += int(line.get("hits", "0")) > 0
                    cond = line.get("condition-coverage")
                    if cond:
                        a, b = cond.split("(")[1].rstrip(")").split("/")
                        bv += int(a); br += int(b)
    return (lv / lines * 100 if lines else 100.0), (bv / br * 100 if br else 100.0)

if __name__ == "__main__":
    files = glob.glob(f"{sys.argv[1] if len(sys.argv) > 1 else 'coverage'}/**/coverage.cobertura.xml", recursive=True)
    if not files:
        sys.exit("no coverage files found")
    _, rules_branches = totals(files, "Hub.Domain")
    services_lines, _ = totals(files, "Hub.Api", "/Features/")
    print(f"Rules engine branch coverage: {rules_branches:.1f} % (gate 95 %)")
    print(f"Service line coverage: {services_lines:.1f} % (gate 70 %)")
    ok = rules_branches >= 95 and services_lines >= 70
    sys.exit(0 if ok else 1)
