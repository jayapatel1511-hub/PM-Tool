#!/usr/bin/env python3
"""CI helpers (§43.5, FR-DLV-11).

  python3 tools/ci_tests.py steps <job>     run the job's lines of tools/ci_steps.txt in order
  python3 tools/ci_tests.py shard <n|api-n> print the dotnet test filter for API shard n of 3

ci_steps.txt: one step per line, `<job>[,<job>...]: <command>`; `#` starts a comment. A job name
followed by ` [always]` runs that step even after an earlier step failed (evidence copies).
Commands run from the repository root under bash with CI_JOB set to the job name."""
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
    steps = [(always, cmd) for jobs, always, cmd in parse_steps(pathlib.Path(steps_file).read_text()) if job in jobs]
    if not steps:
        sys.exit(f"no steps for job {job!r} in {steps_file}")
    env = {**os.environ, "CI_JOB": job}
    failed = None
    for always, cmd in steps:
        if failed and not always:
            print(f"::group::skip {cmd}\n::endgroup::", flush=True)
            continue
        print(f"::group::{cmd}", flush=True)
        code = subprocess.run(["bash", "-eo", "pipefail", "-c", cmd], cwd=cwd, env=env).returncode
        print("::endgroup::", flush=True)
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


def main(argv):
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
