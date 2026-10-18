#!/usr/bin/env python3
"""CI helpers (§43.5, FR-DLV-11).

  python3 tools/ci_tests.py steps <job>     run the job's lines of tools/ci_steps.txt in order
  python3 tools/ci_tests.py shard <n|api-n> print the dotnet test filter for API shard n of 3
  python3 tools/ci_tests.py count <dir> [--main FILE] [--labels a,b] [--event NAME] [--out FILE]
                                            test-count ratchet (FR-DLV-13): executed backend cases (*.trx
                                            under <dir>) must equal the listed cases (listed-tests.txt under
                                            <dir>); on pull requests the backend and browser-suite counts
                                            must not fall below --main unless labelled replace-test
  python3 tools/ci_tests.py image <ref> --sha SHA
                                            release image check (FR-DLV-14): the local image's OCI revision
                                            label and HUB_BUILD both equal SHA

ci_steps.txt: one step per line, `<job>[,<job>...]: <command>`; `#` starts a comment. A job name
followed by ` [always]` runs that step even after an earlier step failed (evidence copies).
Commands run from the repository root under bash with CI_JOB set to the job name."""
import argparse
import collections
import html
import json
import os
import pathlib
import re
import subprocess
import sys

ROOT = pathlib.Path(__file__).resolve().parents[1]
STEPS = ROOT / "tools" / "ci_steps.txt"
API_TESTS = ROOT / "tests" / "Hub.Tests" / "Api"
SHARDS = 3
TOP_CLASS = re.compile(r"^(?:public |internal )?(?:sealed |static |abstract |partial )*class (\w+)", re.M)


def parse_steps(text):
    """[(jobs, always, command)] in file order."""
    steps = []
    for n, raw in enumerate(text.splitlines(), 1):
        line = raw.strip()
        if not line or line.startswith("#"):
            continue
        head, sep, command = line.partition(": ")
        if not sep or not command.strip():
            raise ValueError(f"ci_steps.txt:{n}: expected '<job>: <command>'")
        always = head.endswith(" [always]")
        jobs = [j.strip() for j in head.removesuffix(" [always]").split(",")]
        steps.append((jobs, always, command.strip()))
    return steps


def run_steps(job, steps_file=STEPS, cwd=ROOT):
    """Runs the job's steps; each outcome goes to ci-artifacts/step-outcomes.jsonl for `trace_spec.py --report`."""
    steps = [(always, cmd) for jobs, always, cmd in parse_steps(pathlib.Path(steps_file).read_text()) if job in jobs]
    if not steps:
        sys.exit(f"no steps for job {job!r} in {steps_file}")
    env = {**os.environ, "CI_JOB": job}
    failed = None
    log = pathlib.Path(cwd) / "ci-artifacts" / "step-outcomes.jsonl"
    log.parent.mkdir(parents=True, exist_ok=True)

    def record(cmd, outcome):
        with log.open("a") as f:
            f.write(json.dumps({"job": job, "command": cmd, "outcome": outcome}) + "\n")

    for always, cmd in steps:
        if failed and not always:
            print(f"::group::skip {cmd}\n::endgroup::", flush=True)
            record(cmd, "skip")
            continue
        print(f"::group::{cmd}", flush=True)
        code = subprocess.run(["bash", "-eo", "pipefail", "-c", cmd], cwd=cwd, env=env).returncode
        print("::endgroup::", flush=True)
        record(cmd, "fail" if code else "pass")
        if code and not failed:
            failed = (cmd, code)
            print(f"::error::step failed ({code}): {cmd}", flush=True)
    return 1 if failed else 0


def api_classes(folder=API_TESTS):
    """{class name: test count} for every top-level class in the API test files that has tests."""
    found = {}
    for f in sorted(pathlib.Path(folder).glob("*.cs")):
        text = f.read_text()
        heads = list(TOP_CLASS.finditer(text))
        for i, m in enumerate(heads):
            body = text[m.end(): heads[i + 1].start() if i + 1 < len(heads) else len(text)]
            tests = len(re.findall(r"\[(?:Fact|Theory)\b", body))
            if tests:
                found[m.group(1)] = tests
    return found


def shards(classes, count=SHARDS):
    """Greedy by test count: largest class first onto the lightest shard. Every class lands in exactly one."""
    out = [[] for _ in range(count)]
    load = [0] * count
    for name, tests in sorted(classes.items(), key=lambda kv: (-kv[1], kv[0])):
        i = load.index(min(load))
        out[i].append(name)
        load[i] += tests
    return [sorted(s) for s in out]


def shard_filter(n, folder=API_TESTS):
    names = shards(api_classes(folder))[n - 1]
    return "|".join(f"FullyQualifiedName~Hub.Tests.Api.{c}." for c in names)


def listed_cases(text):
    """Multiset of test names from `dotnet test --list-tests` output."""
    _, _, tail = text.partition("The following Tests are available:")
    return collections.Counter(line.strip() for line in tail.splitlines() if line.startswith("    ") and line.strip())


def executed_cases(folder):
    """Multiset of test names in every TRX file under folder (each shard writes one)."""
    found = collections.Counter()
    for f in sorted(pathlib.Path(folder).rglob("*.trx")):
        found.update(html.unescape(n) for n in re.findall(r'<UnitTestResult [^>]*?testName="([^"]*)"', f.read_text()))
    return found


def browser_suites(steps_text):
    """Browser suites CI runs: the web lines of ci_steps.txt that run an `npm run test:` script."""
    return sum(cmd.count("npm run test:") for jobs, _, cmd in parse_steps(steps_text) if "web" in jobs)


def diff_names(label, counter, limit=20):
    names = sorted(counter.elements())
    more = f" (+{len(names) - limit} more)" if len(names) > limit else ""
    return f"{label} {len(names)}: " + "; ".join(names[:limit]) + more


def count(folder, main_file=None, labels="", event="", out=None, steps_file=STEPS):
    """FR-DLV-13. Returns (exit code, messages); writes the counts JSON to out."""
    errors = []
    listed_files = sorted(pathlib.Path(folder).rglob("listed-tests.txt"))
    listed = collections.Counter()
    for f in listed_files:
        listed.update(listed_cases(f.read_text()))
    executed = executed_cases(folder)
    if not listed_files:
        errors.append("no listed-tests.txt: the domain job must list the test project's cases")
    elif not executed:
        errors.append("no .trx results from the domain and API jobs")
    elif executed != listed:
        msg = f"executed backend cases ({sum(executed.values())}) differ from listed cases ({sum(listed.values())})"
        if listed - executed:
            msg += "; " + diff_names("not run", listed - executed)
        if executed - listed:
            msg += "; " + diff_names("run but not listed", executed - listed)
        errors.append(msg)
    counts = {"backend": sum(executed.values()), "browser_suites": browser_suites(pathlib.Path(steps_file).read_text())}
    notes = [f"counts: backend {counts['backend']}, browser suites {counts['browser_suites']}"]
    main_counts = json.loads(pathlib.Path(main_file).read_text()) if main_file and pathlib.Path(main_file).is_file() else None
    if main_counts is None:
        notes.append("no earlier main count: passes and publishes this run's counts")
    else:
        drops = [f"{k} {counts[k]} < main {main_counts[k]}" for k in counts if k in main_counts and counts[k] < main_counts[k]]
        if drops and "replace-test" in [x.strip() for x in labels.split(",")]:
            notes.append("below main, accepted by label replace-test: " + ", ".join(drops))
        elif drops and event != "pull_request":
            # The pull request already passed this check (or carried replace-test); main stays green.
            notes.append("below the previous main count after merge: " + ", ".join(drops))
        elif drops:
            errors.append("test count below the latest green main run: " + ", ".join(drops) + " (Jay may apply replace-test)")
    if out:
        pathlib.Path(out).parent.mkdir(parents=True, exist_ok=True)
        pathlib.Path(out).write_text(json.dumps(counts, indent=2) + "\n")
    return (1 if errors else 0), notes + [f"::error::{e}" for e in errors]


REVISION = "org.opencontainers.image.revision"


def image_errors(inspect, sha):
    """FR-DLV-14: what is wrong with `docker image inspect` output for an image built from sha."""
    config = inspect[0].get("Config") or {}
    label = (config.get("Labels") or {}).get(REVISION)
    builds = [e.partition("=")[2] for e in config.get("Env") or [] if e.startswith("HUB_BUILD=")]
    errors = []
    if label != sha:
        errors.append(f"label {REVISION} is {label!r}, expected {sha!r}")
    if builds != [sha]:
        errors.append(f"HUB_BUILD is {builds[0] if builds else None!r}, expected {sha!r}")
    return errors


def main(argv):
    if argv and argv[0] == "image":
        p = argparse.ArgumentParser(prog="ci_tests.py image")
        p.add_argument("ref")
        p.add_argument("--sha", required=True)
        a = p.parse_args(argv[1:])
        out = subprocess.run(["docker", "image", "inspect", a.ref], capture_output=True, text=True, check=True).stdout
        errors = image_errors(json.loads(out), a.sha)
        print("\n".join(f"::error::{e}" for e in errors) or f"{a.ref}: revision and HUB_BUILD are {a.sha}")
        return 1 if errors else 0

    if argv and argv[0] == "count":
        p = argparse.ArgumentParser(prog="ci_tests.py count")
        p.add_argument("dir")
        p.add_argument("--main")
        p.add_argument("--labels", default="")
        p.add_argument("--event", default="")
        p.add_argument("--out", default="ci-artifacts/test-counts.json")
        a = p.parse_args(argv[1:])
        code, messages = count(a.dir, a.main, a.labels, a.event, a.out)
        print("\n".join(messages))
        return code

    if len(argv) == 2 and argv[0] == "steps":
        return run_steps(argv[1])
    if len(argv) == 2 and argv[0] == "shard":
        n = int(argv[1].removeprefix("api-"))
        if not 1 <= n <= SHARDS:
            sys.exit(f"shard must be 1 to {SHARDS}")
        print(shard_filter(n))
        return 0
    sys.exit(__doc__)


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
