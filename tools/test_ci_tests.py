import contextlib
import io
import pathlib
import re
import shutil
import sys
import tempfile
import unittest

ROOT = pathlib.Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "tools"))
import ci_tests  # noqa: E402


class ShardTests(unittest.TestCase):
    """AC-DLV-10: every API test class in exactly one of three shards; a new class needs no edit."""

    def test_every_class_in_exactly_one_shard(self):
        classes = ci_tests.api_classes()
        declared = set()
        for f in ci_tests.API_TESTS.glob("*.cs"):
            text = f.read_text()
            if re.search(r"\[(?:Fact|Theory)\b", text):
                declared |= {m for m in re.findall(r"^public sealed class (\w+Tests)\b", text, re.M)}
        self.assertTrue(declared <= set(classes), declared - set(classes))
        parts = ci_tests.shards(classes)
        self.assertEqual(len(parts), 3)
        flat = [c for p in parts for c in p]
        self.assertEqual(sorted(flat), sorted(classes))
        self.assertEqual(len(flat), len(set(flat)))
        self.assertTrue(all(parts), "no shard is empty")
        self.assertEqual(sum(classes.values()), sum(len(re.findall(r"\[(?:Fact|Theory)\b", f.read_text())) for f in ci_tests.API_TESTS.glob("*.cs")))

    def test_filters_cover_each_class_once_and_match_whole_names(self):
        filters = [ci_tests.shard_filter(n) for n in (1, 2, 3)]
        for name in ci_tests.api_classes():
            term = f"FullyQualifiedName~Hub.Tests.Api.{name}."
            self.assertEqual(sum(term in f.split("|") for f in filters), 1, name)

    def test_new_class_lands_without_edits(self):
        folder = pathlib.Path(tempfile.mkdtemp())
        self.addCleanup(shutil.rmtree, folder)
        for f in ci_tests.API_TESTS.glob("*.cs"):
            shutil.copy(f, folder)
        (folder / "BrandNewTests.cs").write_text(
            "namespace Hub.Tests.Api;\n[Collection(\"api\")]\npublic sealed class BrandNewTests(HubFactory f)\n{\n"
            "    [Fact] public void A() { }\n    [Theory, InlineData(1)] public void B(int x) { }\n}\n"
            "public sealed class SecondNewTests\n{\n    [Fact] public void C() { }\n}\n")
        classes = ci_tests.api_classes(folder)
        self.assertEqual((classes["BrandNewTests"], classes["SecondNewTests"]), (2, 1))
        hits = [n for n in (1, 2, 3) if "Hub.Tests.Api.BrandNewTests." in ci_tests.shard_filter(n, folder)]
        self.assertEqual(len(hits), 1)

    def test_class_without_tests_is_not_sharded(self):
        self.assertNotIn("HubFactory", ci_tests.api_classes())
        self.assertNotIn("TestData", ci_tests.api_classes())

    def test_shard_argument(self):
        out = io.StringIO()
        with contextlib.redirect_stdout(out):
            self.assertEqual(ci_tests.main(["shard", "api-2"]), 0)
        self.assertEqual(out.getvalue().strip(), ci_tests.shard_filter(2))
        with self.assertRaises(SystemExit):
            ci_tests.main(["shard", "4"])


class StepsTests(unittest.TestCase):
    """AC-DLV-10: a line added to tools/ci_steps.txt runs in its job with no edit under .github/."""

    def setUp(self):
        self.dir = pathlib.Path(tempfile.mkdtemp())
        self.addCleanup(shutil.rmtree, self.dir)

    def run_job(self, steps, job):
        file = self.dir / "steps.txt"
        file.write_text(steps)
        with contextlib.redirect_stdout(io.StringIO()):
            return ci_tests.run_steps(job, file, self.dir)

    def test_new_line_runs_in_its_job_only(self):
        steps = "# comment\n\nweb: echo web >> ran\nchecks: echo checks >> ran\nweb,gate: echo \"$CI_JOB\" >> ran\n"
        self.assertEqual(self.run_job(steps, "web"), 0)
        self.assertEqual((self.dir / "ran").read_text().split(), ["web", "web"])
        self.assertEqual(self.run_job(steps, "gate"), 0)
        self.assertEqual((self.dir / "ran").read_text().split(), ["web", "web", "gate"])

    def test_failure_stops_later_steps_except_always(self):
        steps = "web: echo one >> ran\nweb: false\nweb: echo skipped >> ran\nweb [always]: echo evidence >> ran\n"
        self.assertEqual(self.run_job(steps, "web"), 1)
        self.assertEqual((self.dir / "ran").read_text().split(), ["one", "evidence"])

    def test_pipeline_failure_fails_the_step(self):
        self.assertEqual(self.run_job("web: false | cat\n", "web"), 1)

    def test_unknown_job_and_bad_line(self):
        with self.assertRaises(SystemExit):
            self.run_job("web: true\n", "nightly")
        with self.assertRaises(ValueError):
            ci_tests.parse_steps("web echo missing colon\n")

    def test_repository_step_list(self):
        steps = ci_tests.parse_steps(ci_tests.STEPS.read_text())
        jobs = {j for js, _, _ in steps for j in js}
        self.assertEqual(jobs, {"domain", "api-1", "api-2", "api-3", "web", "checks", "gate"})
        commands = " ".join(cmd for _, _, cmd in steps)
        for kept in ("test:handoffs", "test:sign-in", "test:coordination", "test:design-basis", "test:planner",
                     "test:resources", "test:supervisor-capacity", "test:ui-review", "npm audit", "npm run lint",
                     "--vulnerable", "trace_spec.py --check", "coverage_gate.py", "pr_checks.py size",
                     "test_backup_helpers", "test_pilot_initializer", "test_pilot_operations", "test_pilot_pitr",
                     "test_social_activation", "test_fixture_api", "test_seed_homedev_tuesday_review"):
            self.assertIn(kept, commands)


if __name__ == "__main__":
    unittest.main()
