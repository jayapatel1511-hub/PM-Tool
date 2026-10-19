import contextlib
import io
import json
import pathlib
import re
import shutil
import subprocess
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
        self.assertEqual(jobs, {"domain", "api-1", "api-2", "api-3", "web", "checks", "gate", "report", "image", "rollback", "main-tag", "nightly", "tier"})
        commands = " ".join(cmd for _, _, cmd in steps)
        for kept in ("test:handoffs", "test:sign-in", "test:coordination", "test:design-basis", "test:planner",
                     "test:resources", "test:supervisor-capacity", "test:ui-review", "npm audit", "npm run lint",
                     "--vulnerable", "trace_spec.py --check", "trace_spec.py --report", "test_trace_report", "coverage_gate.py", "pr_checks.py size",
                     "test_backup_helpers", "test_pilot_initializer", "test_pilot_operations", "test_pilot_pitr",
                     "test_social_activation", "test_fixture_api", "test_seed_homedev_tuesday_review"):
            self.assertIn(kept, commands)


class ImageTests(unittest.TestCase):
    """AC-DLV-13: the pushed image carries the commit as its OCI revision label and HUB_BUILD."""

    SHA = "0123456789abcdef0123456789abcdef01234567"

    def inspect(self, label, env):
        return [{"Config": {"Labels": {ci_tests.REVISION: label} if label else None, "Env": env}}]

    def test_matching_label_and_build_pass(self):
        self.assertEqual(ci_tests.image_errors(self.inspect(self.SHA, ["PATH=/bin", f"HUB_BUILD={self.SHA}"]), self.SHA), [])

    def test_missing_or_wrong_values_fail(self):
        self.assertEqual(len(ci_tests.image_errors(self.inspect(None, None), self.SHA)), 2)
        self.assertEqual(len(ci_tests.image_errors(self.inspect("dev", ["HUB_BUILD=dev"]), self.SHA)), 2)
        self.assertEqual(len(ci_tests.image_errors(self.inspect(self.SHA, ["HUB_BUILD=dev"]), self.SHA)), 1)

    def test_contract_label_checked_when_given(self):
        inspect = self.inspect(self.SHA, [f"HUB_BUILD={self.SHA}"])
        self.assertEqual(len(ci_tests.image_errors(inspect, self.SHA, "20990101000000_Drop")), 1)
        inspect[0]["Config"]["Labels"][ci_tests.CONTRACT] = "20990101000000_Drop"
        self.assertEqual(ci_tests.image_errors(inspect, self.SHA, "20990101000000_Drop"), [])
        self.assertEqual(len(ci_tests.image_errors(inspect, self.SHA, "none")), 1)

    def test_main_workflow_alone_writes_packages(self):
        workflow = (ROOT / ".github" / "workflows" / "ci.yml").read_text()
        self.assertIn("permissions: { contents: read }\n", workflow)
        # The image job alone pushes and moves main after compatibility passes.
        self.assertEqual(workflow.count("packages: write"), 1)
        image = workflow[workflow.index("\n  image:"):]
        self.assertIn("if: github.event_name == 'push' && github.ref == 'refs/heads/main'", image)
        for needed in ("needs: [domain, api, web, checks, gate]", "hosting/Dockerfile.review", "linux/amd64",
                       "RELEASE_SHA=${{ github.sha }}", "attest-build-provenance", "steps image"):
            self.assertIn(needed, image)
        self.assertIn("LABEL org.opencontainers.image.revision=$RELEASE_SHA",
                      (ROOT / "hosting" / "Dockerfile.review").read_text())


class RollbackTests(unittest.TestCase):
    """AC-DLV-15: previous image, new image, previous again on one fresh stack; `main` moves only on pass."""

    PREV, NEW = "ghcr.io/o/pm-tool:main", "ghcr.io/o/pm-tool@sha256:abc"

    def setUp(self):
        self.dir = pathlib.Path(tempfile.mkdtemp())
        self.addCleanup(shutil.rmtree, self.dir)
        self.calls, self.local, self.failing_probe, self.volume = [], set(), None, False
        self.probes = 0

    def sh(self, cmd, cwd=None, env=None, check=True, capture=False):
        self.calls.append((cmd, (env or {}).get("RELEASE_SHA")))
        code, out, err = 0, "", ""
        if cmd[:3] == ["docker", "volume", "inspect"]:
            code = 0 if self.volume else 1
        elif cmd[:3] == ["docker", "image", "inspect"]:
            code = 0 if cmd[3] in self.local else 1
        elif cmd[:2] == ["docker", "pull"]:
            code, err = (1, "manifest unknown") if cmd[2] not in self.remote else (0, "")
        elif cmd[-1] == "scripts/init-homedev-review.py":
            (self.dir / ".runtime" / "keys").mkdir(parents=True, mode=0o700)
            (self.dir / ".runtime" / "review-users.json").write_text('{"users": []}')
            (self.dir / ".runtime" / "review-users.json").chmod(0o600)
        elif "up" in cmd or "restart" in cmd:
            self.assertEqual((self.dir / ".runtime" / "review-users.json").stat().st_mode & 0o077, 0)
            self.assertEqual((self.dir / ".runtime" / "keys").stat().st_mode & 0o077, 0)
        elif "psql" in cmd:
            out = "11111111-1111-1111-1111-111111111111\n"
        elif cmd[-1] == "scripts/verify-homedev-review.py":
            self.probes += 1
            code = 1 if self.probes == self.failing_probe else 0
        if check and code:
            raise ci_tests.RollbackError("failed")
        return subprocess.CompletedProcess(cmd, code, out, err)

    def run_rollback(self, new=None):
        self.remote = getattr(self, "remote", {self.PREV, self.NEW})
        return ci_tests.rollback(self.PREV, new or self.NEW, "f" * 40, cwd=self.dir, sh=self.sh, wait=lambda: None)

    def ups(self):
        return [tag for cmd, tag in self.calls if "up" in cmd]

    def test_additive_schema_passes_in_order(self):
        code, notes = self.run_rollback()
        self.assertEqual(code, 0, notes)
        self.assertEqual(self.ups(), ["previous", "new", "previous"])
        self.assertEqual(self.probes, 3)
        self.assertEqual(sum(c[:3] == ["sudo", "chown", "1000:1000"] for c, _ in self.calls), 2)
        self.assertEqual([n for n in notes if n.startswith("PASS")], [f"PASS {s}" for s in ci_tests.STAGES])
        bootstrap = next(c for c, _ in self.calls if "scripts/bootstrap-review-credentials.py" in c)
        self.assertEqual(bootstrap[-1], "taylor:11111111-1111-1111-1111-111111111111")
        restart = self.calls.index(next(x for x in self.calls if "restart" in x[0]))
        self.assertLess(self.calls.index(next(x for x in self.calls if "bootstrap-review-credentials.py" in " ".join(x[0]))), restart)
        self.assertIn(["docker", "tag", self.PREV, "pm-tool-review:previous"], [c for c, _ in self.calls])
        self.assertIn(["docker", "tag", self.NEW, "pm-tool-review:new"], [c for c, _ in self.calls])

    def test_previous_image_failing_on_new_schema_fails(self):
        self.failing_probe = 3
        code, notes = self.run_rollback()
        self.assertEqual(code, 1)
        self.assertIn("previous image on the new schema", notes[-1])

    def test_no_previous_image_passes_and_says_so(self):
        self.remote = {self.NEW}
        code, notes = self.run_rollback()
        self.assertEqual(code, 0)
        self.assertIn("no previous main image", notes[-1])
        self.assertEqual(self.ups(), [])

    def test_existing_volume_is_refused_and_never_deleted(self):
        self.volume = True
        code, notes = self.run_rollback()
        self.assertEqual(code, 1)
        self.assertEqual(self.ups(), [])
        self.assertFalse(any("down" in c or "rm" in c for c, _ in self.calls))

    def test_pull_error_other_than_missing_fails(self):
        def sh(cmd, **kw):
            if cmd[:2] == ["docker", "pull"]:
                return subprocess.CompletedProcess(cmd, 1, "", "denied: unauthorized")
            return self.sh(cmd, **kw)
        code, notes = ci_tests.rollback(self.PREV, self.NEW, cwd=self.dir, sh=sh, wait=lambda: None)
        self.assertEqual(code, 1)
        self.assertIn("unauthorized", notes[-1])

    def test_manual_run_builds_this_branch(self):
        code, _ = self.run_rollback("build")
        self.assertEqual(code, 0)
        build = next(c for c, _ in self.calls if c[:2] == ["docker", "build"])
        self.assertIn("RELEASE_SHA=" + "f" * 40, build)
        self.assertIn("pm-tool-review:new", build)

    def test_workflow_moves_main_only_after_rollback_on_main(self):
        workflow = (ROOT / ".github" / "workflows" / "ci.yml").read_text()
        self.assertIn("workflow_dispatch:", workflow)
        rollback = workflow[workflow.index("\n  rollback:"):]
        self.assertIn("permissions: { contents: read, packages: read }", rollback)
        self.assertIn("github.event_name == 'workflow_dispatch'", rollback)
        image = workflow[workflow.index("\n  image:"):workflow.index("\n  rollback:")]
        self.assertLess(image.index("steps rollback"), image.index("steps main-tag"))
        self.assertIn('commits/main', image)
        self.assertEqual(workflow.count('packages: write'), 1)
        steps = ci_tests.parse_steps(ci_tests.STEPS.read_text())
        self.assertTrue(any("main-tag" in j and "imagetools create" in c for j, _, c in steps))


class NightlyWiringTests(unittest.TestCase):
    """AC-DLV-23: fixed guard target and persisted reports; hosted execution stays a separate gate."""
    def test_seed_guard_and_evidence_contract(self):
        workflow = (ROOT/'.github/workflows/nightly.yml').read_text()
        for required in ['--ConnectionStrings:Hub=', '127.0.0.1:55433:5432', 'pm-tuesday-preview-db',
                         'playwright install --with-deps chromium', '--branch main', 'ci-artifacts/']:
            self.assertIn(required, workflow)
        self.assertNotIn('[ "$base" != "$GITHUB_SHA" ]', workflow)
        nightly = [command for jobs, _, command in ci_tests.parse_steps(ci_tests.STEPS.read_text()) if 'nightly' in jobs]
        self.assertEqual(sum('node web/tests/' in command or 'test:counts' in command for command in nightly), 8)
        for suite in ('smoke-live.cjs', 'live-real.cjs'):
            self.assertEqual(sum(suite in command for command in nightly), 1)
        self.assertTrue(any('COUNTS_REPORT=' in command and 'ci-artifacts/nightly/' in command for command in nightly))
        self.assertTrue(any('CALM_OUT=' in command and 'calm-acceptance' in command for command in nightly))
        self.assertTrue(any('base.json' in command and 'now.json' in command for command in nightly))
        # The last green nightly already has the current route layout; base means the comparison URL.
        self.assertFalse(any('CALM_VERSION=base' in command for command in nightly))

    def test_dependency_configuration_is_monthly_grouped_and_positive_only(self):
        """AC-DLV-24 configuration part: all ecosystems grouped; unknown/major types never auto-enable."""
        config = (ROOT/'.github/dependabot.yml').read_text()
        self.assertEqual(config.count('schedule: { interval: monthly }'), 3)
        self.assertEqual(config.count('update-types: ["minor", "patch"]'), 3)
        for ecosystem in ['nuget', 'npm', 'github-actions']:
            self.assertIn('package-ecosystem: '+ecosystem, config)
        merge = (ROOT/'.github/workflows/dependency-merge.yml').read_text()
        self.assertIn("== 'version-update:semver-patch'", merge)
        self.assertIn("== 'version-update:semver-minor'", merge)
        self.assertNotIn("!= 'version-update:semver-major'", merge)
        self.assertIn("github.actor == 'dependabot[bot]'", merge)
        self.assertNotIn('actions/checkout', merge)


class CountTests(unittest.TestCase):
    """AC-DLV-12: missed shard, drop below main, replace-test label, first run."""

    STEPS = "web: cd web && npm run build\nweb: cd web && npm run test:a\nweb: cd web && npm run test:b\nchecks: npm run test:x\n"

    def setUp(self):
        self.dir = pathlib.Path(tempfile.mkdtemp())
        self.addCleanup(shutil.rmtree, self.dir)
        (self.dir / "steps.txt").write_text(self.STEPS)
        (self.dir / "in" / "domain").mkdir(parents=True)
        (self.dir / "in" / "domain" / "listed-tests.txt").write_text(
            "Test run for Hub.Tests.dll\nThe following Tests are available:\n"
            "    Hub.Tests.Domain.A.One\n    Hub.Tests.Domain.A.Two(x: \"1\")\n    Hub.Tests.Api.B.Three\n")

    def trx(self, job, names):
        folder = self.dir / "in" / job / "coverage"
        folder.mkdir(parents=True, exist_ok=True)
        rows = "".join(f'<UnitTestResult executionId="{i}" testName="{n.replace(chr(34), "&quot;")}" outcome="Passed" />' for i, n in enumerate(names))
        (folder / f"{job}.trx").write_text(f'<TestRun><Results>{rows}</Results></TestRun>')

    def count(self, main=None, labels="", event="pull_request"):
        main_file = None
        if main is not None:
            main_file = self.dir / "main.json"
            main_file.write_text(json.dumps(main))
        out = self.dir / "out" / "test-counts.json"
        code, messages = ci_tests.count(self.dir / "in", main_file, labels, event, out, self.dir / "steps.txt")
        return code, "\n".join(messages), json.loads(out.read_text())

    def all_shards(self):
        self.trx("domain", ["Hub.Tests.Domain.A.One", 'Hub.Tests.Domain.A.Two(x: "1")'])
        self.trx("api-1", ["Hub.Tests.Api.B.Three"])

    def test_first_run_passes_and_publishes(self):
        self.all_shards()
        code, text, counts = self.count()
        self.assertEqual(code, 0, text)
        self.assertEqual(counts, {"backend": 3, "browser_suites": 2})
        self.assertIn("no earlier main count", text)

    def test_missed_shard_fails_and_names_the_cases(self):
        self.trx("domain", ["Hub.Tests.Domain.A.One", 'Hub.Tests.Domain.A.Two(x: "1")'])
        code, text, _ = self.count()
        self.assertEqual(code, 1)
        self.assertIn("executed backend cases (2) differ from listed cases (3)", text)
        self.assertIn("not run 1: Hub.Tests.Api.B.Three", text)

    def test_case_run_twice_fails(self):
        self.all_shards()
        self.trx("api-2", ["Hub.Tests.Api.B.Three"])
        code, text, _ = self.count()
        self.assertEqual(code, 1)
        self.assertIn("run but not listed 1: Hub.Tests.Api.B.Three", text)

    def test_missing_list_fails(self):
        (self.dir / "in" / "domain" / "listed-tests.txt").unlink()
        self.all_shards()
        self.assertEqual(self.count()[0], 1)

    def test_drop_below_main_fails(self):
        self.all_shards()
        code, text, _ = self.count({"backend": 4, "browser_suites": 2})
        self.assertEqual(code, 1)
        self.assertIn("backend 3 < main 4", text)
        code, text, _ = self.count({"backend": 3, "browser_suites": 3})
        self.assertEqual(code, 1)
        self.assertIn("browser_suites 2 < main 3", text)

    def test_equal_or_higher_than_main_passes(self):
        self.all_shards()
        self.assertEqual(self.count({"backend": 3, "browser_suites": 1})[0], 0)

    def test_replace_test_label_passes(self):
        self.all_shards()
        code, text, _ = self.count({"backend": 9, "browser_suites": 9}, labels="tier-b,replace-test")
        self.assertEqual(code, 0, text)
        self.assertIn("replace-test", text)

    def test_label_does_not_excuse_a_missed_shard(self):
        self.trx("domain", ["Hub.Tests.Domain.A.One"])
        self.assertEqual(self.count(labels="replace-test")[0], 1)

    def test_main_push_after_accepted_drop_stays_green(self):
        self.all_shards()
        code, text, _ = self.count({"backend": 9, "browser_suites": 2}, event="push")
        self.assertEqual(code, 0, text)

    def test_repository_lists_cases_and_counts_suites(self):
        steps = ci_tests.STEPS.read_text()
        self.assertGreaterEqual(ci_tests.browser_suites(steps), 8)
        commands = [cmd for jobs, _, cmd in ci_tests.parse_steps(steps) if "domain" in jobs or "api-1" in jobs]
        self.assertTrue(any("--list-tests" in c and "listed-tests.txt" in c for c in commands))
        self.assertEqual(sum("--logger \"trx;" in c for c in commands), 2)
        gate = [cmd for jobs, _, cmd in ci_tests.parse_steps(steps) if "gate" in jobs]
        self.assertTrue(any("ci_tests.py count" in c for c in gate))

    def test_listed_cases_parses_dotnet_output(self):
        text = "Build...\nThe following Tests are available:\n    A.B.C\n    A.B.C\n    A.B.D(x: 1)\n"
        self.assertEqual(ci_tests.listed_cases(text), {"A.B.C": 2, "A.B.D(x: 1)": 1})


if __name__ == "__main__":
    unittest.main()
