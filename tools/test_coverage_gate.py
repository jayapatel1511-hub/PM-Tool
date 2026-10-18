import pathlib
import shutil
import subprocess
import sys
import tempfile
import unittest
import xml.etree.ElementTree as ET

ROOT = pathlib.Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "tools"))
import coverage_gate  # noqa: E402


def before_merge(files, pkg, path_part=None):
    """The gate's summing totals before FR-DLV-12, kept as the one-file reference."""
    lines = lv = br = bv = 0
    for f in files:
        for p in ET.parse(f).getroot().iter("package"):
            if p.get("name") != pkg:
                continue
            for c in p.iter("class"):
                name = c.get("filename", "").replace("\\", "/")
                if name.endswith(".g.cs") or "/obj/" in name:
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


def cobertura(domain_lines, api_lines):
    """Lines are (number, hits, condition or None); condition is (covered, total)."""
    def lines(rows):
        out = []
        for n, hits, cond in rows:
            extra = f' branch="True" condition-coverage="{cond[0] * 100 // cond[1]}% ({cond[0]}/{cond[1]})"' if cond else ""
            out.append(f'<line number="{n}" hits="{hits}"{extra} />')
        return "".join(out)
    return ('<?xml version="1.0"?><coverage><packages>'
            f'<package name="Hub.Domain"><classes><class name="Hub.Domain.Rules" filename="src/Hub.Domain/Rules.cs"><lines>{lines(domain_lines)}</lines></class>'
            '<class name="Gen" filename="src/Hub.Domain/obj/Gen.g.cs"><lines><line number="1" hits="0" /></lines></class></classes></package>'
            f'<package name="Hub.Api"><classes><class name="Hub.Api.Features.Tasks" filename="src/Hub.Api/Features/Tasks.cs"><lines>{lines(api_lines)}</lines></class>'
            '<class name="Hub.Api.Program" filename="src/Hub.Api/Program.cs"><lines><line number="1" hits="0" /></lines></class></classes></package>'
            '</packages></coverage>')


class MergeTests(unittest.TestCase):
    """AC-DLV-11: shard files merge by file and line; one file gives today's numbers."""

    def setUp(self):
        self.dir = pathlib.Path(tempfile.mkdtemp())
        self.addCleanup(shutil.rmtree, self.dir)

    def write(self, shard, xml):
        path = self.dir / shard / "guid" / "coverage.cobertura.xml"
        path.parent.mkdir(parents=True)
        path.write_text(xml)
        return str(path)

    def test_line_hit_only_in_second_file_counts_once(self):
        first = self.write("domain", cobertura([(1, 1, None), (2, 0, None), (3, 1, (1, 2))], [(10, 0, None), (11, 1, None)]))
        second = self.write("api-1", cobertura([(1, 4, None), (2, 3, None), (3, 1, (2, 2))], [(10, 2, None), (11, 0, None)]))
        rules_lines, rules_branches = coverage_gate.totals([first, second], "Hub.Domain")
        self.assertEqual(rules_lines, 100.0)  # three lines, each counted once
        self.assertEqual(rules_branches, 100.0)  # highest covered count, 2 of 2, not 3 of 4
        services_lines, _ = coverage_gate.totals([first, second], "Hub.Api", "/Features/")
        self.assertEqual(services_lines, 100.0)
        # Summing per file, as before, would report five sixths of the Domain lines and 3 of 4 branches.
        self.assertAlmostEqual(before_merge([first, second], "Hub.Domain")[0], 500 / 6)

    def test_lower_branch_count_never_over_reports(self):
        a = self.write("a", cobertura([(3, 1, (1, 4))], []))
        b = self.write("b", cobertura([(3, 1, (2, 4))], []))
        self.assertEqual(coverage_gate.totals([a, b], "Hub.Domain")[1], 50.0)

    def test_one_file_equals_before(self):
        f = self.write("one", cobertura([(1, 1, (1, 2)), (2, 0, None), (3, 2, (3, 4)), (4, 0, (0, 2))], [(1, 1, None), (2, 0, None), (3, 0, None)]))
        for pkg, part in (("Hub.Domain", None), ("Hub.Api", "/Features/"), ("Hub.Api", None)):
            self.assertEqual(coverage_gate.totals([f], pkg, part), before_merge([f], pkg, part))

    def test_one_real_file_equals_before(self):
        real = sorted(ROOT.glob("coverage/**/coverage.cobertura.xml")) + sorted(ROOT.glob("ci-artifacts/**/coverage.cobertura.xml"))
        if not real:
            self.skipTest("no local coverage run")
        for f in real[:2]:
            for pkg, part in (("Hub.Domain", None), ("Hub.Api", "/Features/")):
                self.assertEqual(coverage_gate.totals([str(f)], pkg, part), before_merge([str(f)], pkg, part), f)

    def test_gate_exit_codes(self):
        self.write("ok", cobertura([(1, 1, (2, 2))], [(1, 1, None)]))
        run = subprocess.run([sys.executable, ROOT / "tools" / "coverage_gate.py", self.dir], capture_output=True, text=True)
        self.assertEqual(run.returncode, 0, run.stdout + run.stderr)
        self.write("low", cobertura([(9, 1, (0, 2))], []))
        self.assertEqual(subprocess.run([sys.executable, ROOT / "tools" / "coverage_gate.py", self.dir], capture_output=True).returncode, 1)
        empty = tempfile.mkdtemp()
        self.addCleanup(shutil.rmtree, empty)
        self.assertNotEqual(subprocess.run([sys.executable, ROOT / "tools" / "coverage_gate.py", empty], capture_output=True).returncode, 0)


if __name__ == "__main__":
    unittest.main()
