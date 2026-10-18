#!/usr/bin/env python3
"""AC-DLV-19: `trace_spec.py --report` shows pass, fail, unrun and human, each with the run URL, prints
each packet's records ratio, and `--check` gives today's result on today's tree (FR-DLV-22, FR-DLV-25)."""
import pathlib
import subprocess
import sys
import tempfile
import unittest

ROOT = pathlib.Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "tools"))
import ci_tests  # noqa: E402
import trace_spec  # noqa: E402

URL = "https://example.test/actions/runs/42"

SPEC = """# Spec

## 1. Fixture

- **FR-FIX-01.** Something.
- **AC-FIX-01.** Passes. **AC-FIX-02.** Fails. **AC-FIX-03.** Nobody tests it. **AC-FIX-04.** A person checks it.
- **AC-FIX-05.** A browser suite checks it.
"""

TESTS = """namespace Hub.Tests.Api;

/// Fixture class (FR-FIX-01).
[Collection("api")]
public sealed class FixtureTests(HubFactory f)
{
    [Fact]
    public async Task First_passes() // AC-FIX-01
    {
    }

    [Fact]
    [Trait("Spec", "AC-FIX-02")]
    public void Second_fails()
    {
    }

    [Theory]
    public void AC_FIX_04_is_human_whatever_the_test_says(int n)
    {
    }
}
"""

TRX = """<TestRun><Results>
<UnitTestResult testId="1" testName="Hub.Tests.Api.FixtureTests.First_passes" outcome="Passed" />
<UnitTestResult testId="2" testName="Hub.Tests.Api.FixtureTests.Second_fails" outcome="Failed" />
<UnitTestResult testId="3" testName="Hub.Tests.Api.FixtureTests.AC_FIX_04_is_human_whatever_the_test_says(n: 1)" outcome="Passed" />
</Results></TestRun>
"""


def git(root, *args):
    subprocess.run(["git", "-C", str(root), *args], check=True, capture_output=True)


class ReportTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.root = root = pathlib.Path(self.tmp.name) / "repo"
        files = {
            "Engineering-Project-Coordination-Hub-Specification.md": SPEC,
            "spec-parts/99-fixture.md": "## 99. Fixture\n\nline\n",
            "specs/999-fixture/spec.md": "# Fixture\n\n**Source**: §99 in `spec-parts/99-fixture.md`.\n\n"
                                         "AC-FIX-01..05, FR-FIX-01. Flag `flag.fixture_thing`.\n",
            "specs/999-fixture/tasks.md": "- [x] S01 one\n- [ ] S02 two\n",
            "specs/DECISIONS.md": "| ID | Packet |\n|---|---|\n| 999-D1 | 999 | x |\n| 998-D1 | 998 | y |\n",
            "specs/human-only.txt": "# people check these\nAC-FIX-04  # a human session\n",
            "tests/Hub.Tests/Api/FixtureTests.cs": TESTS,
            "web/package.json": '{"scripts": {"build": "x", "test:fix": "node tests/fix.cjs"}}',
            "web/tests/fix.cjs": "// AC-FIX-05\n",
        }
        for path, text in files.items():
            (root / path).parent.mkdir(parents=True, exist_ok=True)
            (root / path).write_text(text)
        git(root, "init", "-q")
        git(root, "-c", "user.name=t", "-c", "user.email=t@example.test", "add", ".")
        git(root, "-c", "user.name=t", "-c", "user.email=t@example.test", "commit", "-qm", "Initial fixture")
        (root / "src").mkdir()
        (root / "src/Code.cs").write_text("a\n" * 30)
        (root / "specs/999-fixture/notes.md").write_text("not code\n" * 50)
        git(root, "add", ".")
        git(root, "-c", "user.name=t", "-c", "user.email=t@example.test", "commit", "-qm", "999-S01: code")
        (root / "src/Other.cs").write_text("b\n" * 500)
        git(root, "add", ".")
        git(root, "-c", "user.name=t", "-c", "user.email=t@example.test", "commit", "-qm", "998-S01: another packet")

        self.results = pathlib.Path(self.tmp.name) / "artifacts"
        (self.results / "ci-artifacts-api-1/coverage").mkdir(parents=True)
        (self.results / "ci-artifacts-api-1/coverage/api-1.trx").write_text(TRX)

    def tearDown(self):
        self.tmp.cleanup()

    def row(self, text, ident):
        return next(line for line in text.splitlines() if line.startswith(f"| {ident} |")).split(" | ")

    def test_pass_fail_unrun_and_human_each_with_run_url(self):
        (self.results / "ci-artifacts-web").mkdir()
        steps = self.root / "steps.txt"
        steps.write_text("web: true\nweb: false && npm run test:fix\n")
        # The browser suite's outcome comes from the step log tools/ci_tests.py writes; the step itself fails here.
        self.assertEqual(ci_tests.run_steps("web", steps, self.results / "ci-artifacts-web"), 1)
        text = trace_spec.report(self.root, self.results, URL)
        expected = {"AC-FIX-01": "pass", "AC-FIX-02": "fail", "AC-FIX-03": "unrun", "AC-FIX-04": "human",
                    "AC-FIX-05": "fail", "FR-FIX-01": "fail"}
        for ident, status in expected.items():
            cols = self.row(text, ident)
            self.assertEqual(cols[3], status, ident)
            self.assertEqual(cols[5].rstrip(" |"), URL, ident)
        self.assertIn("FixtureTests.First_passes", self.row(text, "AC-FIX-01")[2])
        self.assertIn("browser:test:fix", self.row(text, "AC-FIX-05")[2])
        self.assertEqual(self.row(text, "AC-FIX-04")[4], "yes")
        self.assertEqual(self.row(text, "AC-FIX-03")[2], "-")

    def test_without_results_everything_tested_is_unrun(self):
        text = trace_spec.report(self.root, None, "")
        self.assertEqual(self.row(text, "AC-FIX-01")[3], "unrun")
        self.assertEqual(self.row(text, "AC-FIX-04")[3], "human")
        self.assertIn("(local run)", self.row(text, "AC-FIX-01")[5])

    def test_packet_row_has_acs_slices_flag_and_records_ratio(self):
        cols = self.row(trace_spec.report(self.root, self.results, URL), "999-fixture")
        self.assertEqual(cols[1], "1 of 5")       # AC-FIX-01 passes
        self.assertEqual(cols[2], "1")            # AC-FIX-04 is human-only
        self.assertEqual(cols[3], "1 of 2")
        self.assertEqual(cols[4], "flag.fixture_thing")
        folder = self.root / "specs/999-fixture"
        records = sum(len(f.read_text().splitlines()) for f in folder.iterdir()) + 3 + 1  # spec-parts file, one decision row
        self.assertEqual(int(cols[5]), records)
        self.assertEqual(int(cols[6]), 30)        # only 999's slice commits, records files left out
        self.assertEqual(cols[7].rstrip(" |"), f"{records / 30:.2f}:1 (over 1:1)")

    def test_cli_writes_the_report(self):
        out = pathlib.Path(self.tmp.name) / "out/report.md"
        code = subprocess.run([sys.executable, str(ROOT / "tools/trace_spec.py"), "--report", str(self.results),
                               "--run-url", URL, "--out", str(out)], capture_output=True, text=True)
        self.assertEqual(code.returncode, 0, code.stderr)
        self.assertIn("## Packets", out.read_text())


class CheckUnchangedTests(unittest.TestCase):
    def test_check_gives_todays_result_on_todays_tree(self):
        table = ROOT / "specs/TRACEABILITY.md"
        before = table.read_bytes()
        run = subprocess.run([sys.executable, str(ROOT / "tools/trace_spec.py"), "--check"], capture_output=True, text=True)
        self.assertEqual(run.returncode, 0, run.stdout)
        self.assertRegex(run.stdout, r"^\d+ IDs, 0 not cited; \d+ sections, 0 not cited; 0 unknown\n$")
        self.assertEqual(table.read_bytes(), before)


if __name__ == "__main__":
    unittest.main()
