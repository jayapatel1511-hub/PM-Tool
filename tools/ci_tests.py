#!/usr/bin/env python3
"""CI helpers (§43.5, FR-DLV-11).

  python3 tools/ci_tests.py steps <job>     run the job's lines of tools/ci_steps.txt in order
  python3 tools/ci_tests.py shard <n|api-n> print the dotnet test filter for API shard n of 3
  python3 tools/ci_tests.py count <dir> [--main FILE] [--labels a,b] [--event NAME] [--out FILE]
                                            test-count ratchet (FR-DLV-13): executed backend cases (*.trx
                                            under <dir>) must equal the listed cases (listed-tests.txt under
                                            <dir>); on pull requests the backend and browser-suite counts
                                            must not fall below --main unless labelled replace-test
  python3 tools/ci_tests.py image <ref> --sha SHA [--contract ID]
                                            release image check (FR-DLV-14): the local image's OCI revision
                                            label and HUB_BUILD both equal SHA; its contract-migration label
                                            equals ID (FR-DLV-15)
  python3 tools/ci_tests.py rollback --previous REF --new REF|build
                                            rollback compatibility (FR-DLV-17) on a fresh review stack:
                                            previous image, new image, previous image again, probes each time

ci_steps.txt: one step per line, `<job>[,<job>...]: <command>`; `#` starts a comment. A job name
followed by ` [always]` runs that step even after an earlier step failed (evidence copies).
Commands run from the repository root under bash with CI_JOB set to the job name."""
import argparse
import collections
import html
import http.client
import json
import os
import pathlib
import re
import subprocess
import sys
import time

ROOT = pathlib.Path(__file__).resolve().parents[1]
STEPS = ROOT / "tools" / "ci_steps.txt"
API_TESTS = ROOT / "tests" / "Hub.Tests" / "Api"
SHARDS = 3
TOP_CLASS = re.compile(r"^(?:public |internal )?(?:sealed |static |abstract |partial )*class (\w+)", re.M)
# [Fact], [Theory] and custom fact attributes such as [ScaleMeasurementFact] each declare one test method.
TEST_ATTRIBUTE = re.compile(r"\[(?:\w*Fact|Theory)\b")


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
            tests = len(TEST_ATTRIBUTE.findall(body))
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
CONTRACT = "hub.contract-migration"


def image_errors(inspect, sha, contract=None):
    """FR-DLV-14: what is wrong with `docker image inspect` output for an image built from sha. With contract,
    the contract-migration label must equal it (FR-DLV-15)."""
    config = inspect[0].get("Config") or {}
    labels = config.get("Labels") or {}
    label = labels.get(REVISION)
    builds = [e.partition("=")[2] for e in config.get("Env") or [] if e.startswith("HUB_BUILD=")]
    errors = []
    if label != sha:
        errors.append(f"label {REVISION} is {label!r}, expected {sha!r}")
    if builds != [sha]:
        errors.append(f"HUB_BUILD is {builds[0] if builds else None!r}, expected {sha!r}")
    if contract is not None and labels.get(CONTRACT) != contract:
        errors.append(f"label {CONTRACT} is {labels.get(CONTRACT)!r}, expected {contract!r}")
    return errors


# Rollback compatibility (FR-DLV-17): the review stack as it is, on a fresh database. Never deletes a volume.
REVIEW_COMPOSE = ["docker", "compose", "--env-file", ".runtime/review.env", "-f", "hosting/homedev.compose.yml"]
REVIEW_VOLUME = "pm-tool-review-db"
REVIEWER = "taylor"  # a synthetic review person every review seed creates
STAGES = ("previous image on its own schema", "new image after migrating", "previous image on the new schema")


class RollbackError(Exception):
    pass


def shell(cmd, cwd=ROOT, env=None, check=True, capture=False):
    r = subprocess.run(cmd, cwd=cwd, env={**os.environ, **(env or {})}, text=True, capture_output=capture)
    if check and r.returncode:
        raise RollbackError(f"{' '.join(cmd[:4])} ... exited {r.returncode}" + (f": {r.stderr.strip()}" if capture else ""))
    return r


def wait_healthy(timeout=240):
    """Polls the review origin's /health as the probes call it, until 200."""
    deadline = time.monotonic() + timeout
    while time.monotonic() < deadline:
        try:
            conn = http.client.HTTPConnection("127.0.0.1", 3080, timeout=5)
            conn.request("GET", "/health", headers={"Host": "pm.engcalchub.com", "X-Forwarded-Proto": "https"})
            if conn.getresponse().status == 200:
                return
        except OSError:
            pass
        time.sleep(3)
    raise RollbackError(f"/health not ready within {timeout}s")


def rollback(previous, new, sha="dev", cwd=ROOT, sh=shell, wait=wait_healthy):
    """(exit code, messages). previous and new are image references; new == "build" builds this tree."""
    cwd = pathlib.Path(cwd)
    notes = []

    def sh_(cmd, **kw):
        return sh(cmd, cwd=cwd, **kw)

    def fetch(ref):
        if sh_(["docker", "image", "inspect", ref], check=False, capture=True).returncode == 0:
            return True
        r = sh_(["docker", "pull", ref], check=False, capture=True)
        if r.returncode == 0:
            return True
        if re.search(r"not found|manifest unknown", r.stderr or "", re.I):
            return False
        raise RollbackError(f"cannot pull {ref}: {(r.stderr or '').strip()}")

    def up(tag):
        sh_(REVIEW_COMPOSE + ["up", "-d", "--no-build"], env={"RELEASE_SHA": tag})
        wait()

    def probe(stage):
        if sh_(["python3", "scripts/verify-homedev-review.py"], check=False).returncode:
            logs = sh_(REVIEW_COMPOSE + ["logs", "--no-color", "--tail", "80", "api"], check=False, capture=True).stdout or ""
            notes.extend(l for l in logs.splitlines() if not re.search(r"password|secret|token", l, re.I))
            raise RollbackError(f"{stage}: probes failed")
        notes.append(f"PASS {stage}")

    try:
        if sh_(["docker", "volume", "inspect", REVIEW_VOLUME], check=False, capture=True).returncode == 0:
            raise RollbackError(f"volume {REVIEW_VOLUME} exists; this job needs a fresh database and never deletes one")
        if not previous or not fetch(previous):
            return 0, notes + [f"no previous main image ({previous or 'none named'}): passes without a rollback check"]
        if new == "build":
            sh_(["docker", "build", "-f", "hosting/Dockerfile.review", "--build-arg", f"RELEASE_SHA={sha}",
                 "-t", "pm-tool-review:new", "."])
        elif fetch(new):
            sh_(["docker", "tag", new, "pm-tool-review:new"])
        else:
            raise RollbackError(f"new image {new} not found")
        sh_(["docker", "tag", previous, "pm-tool-review:previous"])

        sh_(["python3", "scripts/init-homedev-review.py"])
        runtime, users = cwd / ".runtime", cwd / ".runtime" / "review-users.json"
        # Match the image's UID without weakening LocalPasswordStore's owner-only mode guard.
        sh_(["sudo", "chown", "1000:1000", str(runtime / "keys"), str(users)])
        up("previous")
        user_id = sh_(REVIEW_COMPOSE + ["exec", "-T", "db", "psql", "-U", "hub_review", "-d", "hub_review", "-Atc",
                                        f"SELECT id FROM hub.app_user WHERE email = '{REVIEWER}@hub.test' AND is_active"],
                      capture=True).stdout.strip()
        sh_(["sudo", "chown", f"{os.getuid()}:{os.getgid()}", str(users)])
        sh_(["python3", "scripts/bootstrap-review-credentials.py", ".runtime/review-users.json",
             ".runtime/review-login-handoff.json", f"{REVIEWER}:{user_id}"])
        sh_(["sudo", "chown", "1000:1000", str(users)])
        sh_(REVIEW_COMPOSE + ["restart", "api"])
        wait()
        probe(STAGES[0])
        up("new")
        probe(STAGES[1])
        up("previous")
        probe(STAGES[2])
    except RollbackError as e:
        return 1, notes + [f"::error::{e}"]
    return 0, notes + ["rollback compatible: the previous image runs on the new schema"]


def main(argv):
    if argv and argv[0] == "image":
        p = argparse.ArgumentParser(prog="ci_tests.py image")
        p.add_argument("ref")
        p.add_argument("--sha", required=True)
        p.add_argument("--contract")
        a = p.parse_args(argv[1:])
        out = subprocess.run(["docker", "image", "inspect", a.ref], capture_output=True, text=True, check=True).stdout
        errors = image_errors(json.loads(out), a.sha, a.contract)
        print("\n".join(f"::error::{e}" for e in errors) or f"{a.ref}: revision and HUB_BUILD are {a.sha}")
        return 1 if errors else 0

    if argv and argv[0] == "rollback":
        p = argparse.ArgumentParser(prog="ci_tests.py rollback")
        p.add_argument("--previous", default="")
        p.add_argument("--new", required=True)
        p.add_argument("--sha", default="dev")
        a = p.parse_args(argv[1:])
        code, messages = rollback(a.previous, a.new, a.sha)
        print("\n".join(messages))
        return code

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
