#!/usr/bin/env python3
"""Safely append the Tuesday hosted review fixture after an exact release activation.

This is an operator wrapper. It is intentionally fail-closed and performs no
host action until the shared hosted-release guard passes. Passwords stay in
owner-only handoff files and are never logged or printed.
"""

from __future__ import annotations

import argparse
import hashlib
import hmac
import importlib.util
import json
import os
import re
import stat
import subprocess
import sys
import tempfile
import time
import uuid
from datetime import UTC, datetime
from pathlib import Path
from typing import Any, Callable

ROOT = Path(__file__).resolve().parents[1]
RELEASE = re.compile(r"[0-9a-f]{40}\Z")
PERSONAS = ("jordan", "lena", "sam", "priya", "marc", "alex", "jill", "diane", "omar", "rita")
PERSONA_SET = frozenset(PERSONAS)
LEGACY = ("taylor", "jay", "yagmur")
ALL_IDENTITIES = LEGACY + PERSONAS
ALL_IDENTITY_SET = frozenset(ALL_IDENTITIES)
EXPECTED_EMAILS = {name: f"{name}@hub.test" for name in ALL_IDENTITIES}
EXPECTED_ENTRA = {name: f"dev-{EXPECTED_EMAILS[name]}" for name in ALL_IDENTITIES}
COMPOSE = ("sudo", "-n", "docker", "compose", "--env-file", ".runtime/review.env", "-f", "hosting/homedev.compose.yml")


class WrapperError(RuntimeError):
    """Safe operator error; never includes command output or credentials."""


def private_file(path: Path, *, required: bool = True) -> None:
    if required and (not path.exists() or not path.is_file()):
        raise WrapperError(f"required private file missing: {path.name}")
    if path.exists() and (path.is_symlink() or stat.S_IMODE(path.stat().st_mode) & 0o077 or path.stat().st_uid != os.getuid()):
        raise WrapperError(f"unsafe private file: {path.name}")


def private_dir(path: Path) -> None:
    if not path.exists() or not path.is_dir() or path.is_symlink():
        raise WrapperError("runtime/data directory is not a private directory")
    if stat.S_IMODE(path.stat().st_mode) & 0o077 or path.stat().st_uid != os.getuid():
        raise WrapperError("runtime/data directory must be private and owner-owned")


def atomic_json(path: Path, value: dict[str, Any]) -> None:
    fd, temporary = tempfile.mkstemp(prefix=f".{path.name}-", dir=path.parent)
    try:
        os.fchmod(fd, 0o600)
        with os.fdopen(fd, "w") as output:
            json.dump(value, output, indent=2)
            output.write("\n")
            output.flush()
            os.fsync(output.fileno())
        os.replace(temporary, path)
    finally:
        if os.path.exists(temporary):
            os.unlink(temporary)


def load_module(path: Path, name: str) -> Any:
    preview_dir = str(path.parent)
    if preview_dir not in sys.path:
        sys.path.insert(0, preview_dir)
    spec = importlib.util.spec_from_file_location(name, path)
    if not spec or not spec.loader:
        raise WrapperError(f"cannot load {name}")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def verify_exact_release(release: str, root: Path = ROOT, verify: Callable[[str], None] | None = None) -> None:
    if not RELEASE.fullmatch(release):
        raise WrapperError("release must be a 40-character lowercase SHA")
    if root.name != release:
        raise WrapperError("run from the requested prepared release directory")
    guard = verify or load_module(root / "tools/preview/fixture_api.py", "fixture_api")._verify_hosted
    guard(release)


def run_checked(command: list[str], *, cwd: Path, runner: Callable[..., Any] = subprocess.run) -> Any:
    try:
        return runner(command, cwd=cwd, stdin=subprocess.DEVNULL, stdout=subprocess.PIPE,
                      stderr=subprocess.PIPE, text=True, check=True)
    except (OSError, subprocess.CalledProcessError) as exc:
        raise WrapperError("operator command failed") from exc


def reserved_ids(root: Path, runner: Callable[..., Any] = subprocess.run) -> dict[str, str]:
    sql = "SELECT email, entra_object_id, id::text FROM hub.app_user WHERE email IN (" + ",".join(
        "'" + EXPECTED_EMAILS[name] + "'" for name in PERSONAS
    ) + "," + ",".join("'" + EXPECTED_EMAILS[name] + "'" for name in LEGACY) + ") AND is_active ORDER BY email;"
    command = [*COMPOSE, "exec", "-T", "db", "psql", "-U", "hub_review", "-d", "hub_review", "-At", "-F", "|", "-c", sql]
    result = run_checked(command, cwd=root, runner=runner)
    found: dict[str, str] = {}
    rows = 0
    for line in (result.stdout or "").splitlines():
        rows += 1
        parts = line.strip().split("|")
        if len(parts) != 3:
            raise WrapperError("database returned an unexpected reserved identity")
        email, entra, raw_id = parts
        if email not in EXPECTED_EMAILS.values() or entra != EXPECTED_ENTRA[email.removesuffix("@hub.test")]:
            raise WrapperError("database returned an unexpected reserved identity")
        try:
            found[email.removesuffix("@hub.test")] = str(uuid.UUID(raw_id))
        except ValueError as exc:
            raise WrapperError("database returned an invalid reserved user ID") from exc
    if rows != len(ALL_IDENTITIES) or set(found) != ALL_IDENTITY_SET or len(set(found.values())) != len(ALL_IDENTITIES):
        raise WrapperError("database does not contain exactly thirteen active reserved review identities")
    return found


def read_handoff(path: Path, expected_count: int) -> dict[str, str]:
    private_file(path)
    try:
        payload = json.loads(path.read_text())
        entries = payload["credentials"]
        if not isinstance(entries, list):
            raise TypeError
        result = {}
        for entry in entries:
            if not isinstance(entry, dict) or set(entry) != {"login", "password"}:
                raise TypeError
            result[entry["login"]] = entry["password"]
    except (OSError, TypeError, ValueError, KeyError, json.JSONDecodeError):
        raise WrapperError("review handoff is malformed") from None
    if len(entries) != expected_count or len(result) != expected_count or set(result) - PERSONA_SET or any(not isinstance(p, str) or not p for p in result.values()):
        raise WrapperError("review handoff does not contain the expected complete account set")
    return result


def verifier_users(path: Path) -> list[dict[str, Any]]:
    verifier = load_module(ROOT / "scripts/extend-review-credentials.py", "extend_review_credentials")
    try:
        _, users = verifier.read_verifiers(path)
        return users
    except (OSError, ValueError) as exc:
        raise WrapperError("review verifier is malformed or unsafe") from exc


def credentials_match(users: list[dict[str, Any]], credentials: dict[str, str]) -> bool:
    by_login = {str(user.get("userName", "")).casefold(): user for user in users}
    if not set(credentials).issubset(by_login):
        return False
    for login, password in credentials.items():
        user = by_login.get(login)
        try:
            salt = bytes.fromhex(user["salt"])
            expected = bytes.fromhex(user["hash"])
            actual = hashlib.pbkdf2_hmac("sha256", password.encode(), salt, 600_000)
        except (AttributeError, KeyError, TypeError, ValueError):
            return False
        if not hmac.compare_digest(actual, expected):
            return False
    return True


def extend_credentials(root: Path, mappings: list[str], target: Path,
                       runner: Callable[..., Any] = subprocess.run) -> None:
    if target.exists():
        raise WrapperError("existing Tuesday handoff requires a matching completed manifest")
    command = [sys.executable, str(root / "scripts/extend-review-credentials.py"),
               "--verifier-file", str((root / ".runtime").resolve() / "review-users.json"),
               "--new-private-handoff-file", str(target), *mappings]
    run_checked(command, cwd=root, runner=runner)
    new_credentials = read_handoff(target, len(PERSONAS))
    if set(new_credentials) != PERSONA_SET:
        raise WrapperError("new handoff does not contain exactly the ten Tuesday accounts")


def restart_api(root: Path, release: str, runner: Callable[..., Any] = subprocess.run) -> None:
    command = ["sudo", "-n", "env", f"RELEASE_SHA={release}", "docker", "compose", *COMPOSE[4:],
               "up", "-d", "--no-build", "--force-recreate", "api"]
    run_checked(command, cwd=root, runner=runner)


def wait_health(root: Path, runner: Callable[..., Any] = subprocess.run,
                sleeper: Callable[[float], None] = time.sleep) -> None:
    """Wait for the recreated API without exposing its response body."""
    command = ["curl", "-fsS", "--max-time", "3", "-H", "Host: pm.engcalchub.com",
               "-H", "X-Forwarded-Proto: https", "http://127.0.0.1:3080/health"]
    for _ in range(60):
        try:
            result = runner(command, cwd=root, stdin=subprocess.DEVNULL,
                            stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True)
            if getattr(result, "returncode", 0) == 0:
                return
        except OSError:
            pass
        sleeper(2)
    raise WrapperError("review API health did not become ready")


def seed(root: Path, release: str, handoff: Path, script: str, runner: Callable[..., Any] = subprocess.run) -> Any:
    command = [sys.executable, str(root / "tools/preview" / script), "--hosted-review",
               "--credentials", str(handoff), "--release", release]
    return run_checked(command, cwd=root, runner=runner)



FIXTURE_MINIMUMS = {
    "task": 14, "milestone": 7, "deliverable": 9, "decision": 5,
    "risk": 4, "issue": 5, "meeting": 3, "resource_allocation": 6,
    "handoff": 2, "review_package": 1, "change_notice": 1,
    "submission_package": 1, "design_basis_entry": 2,
    "work_constraint": 2, "output_commitment": 2,
}


def dataset_snapshot(root: Path, runner: Callable[..., Any] = subprocess.run) -> dict[str, Any]:
    """Read only counts and fixture IDs from the already-guarded review database."""
    fields = []
    for table in FIXTURE_MINIMUMS:
        soft_delete = "" if table in {"resource_allocation", "work_constraint", "output_commitment"} else " AND deleted_at IS NULL"
        fields.append("'%s', (SELECT count(*) FROM hub.%s WHERE project_id IN (SELECT id FROM fixture_projects)%s)" % (table, table, soft_delete))
    sql = """WITH fixture_projects AS (
        SELECT id, project_number, visibility FROM hub.project
        WHERE project_number IN ('SYN-101','SYN-102','SYN-103')
    ) SELECT json_build_object(
        'projects', (SELECT coalesce(json_agg(json_build_object('id', id, 'number', project_number, 'visibility', visibility) ORDER BY project_number), '[]'::json) FROM fixture_projects),
        'counts', json_build_object(""" + ",".join(fields) + """,
        'planning_entry', (SELECT count(*) FROM hub.planning_entry WHERE notes IN ('Coordinate weekly work and capacity with the team.', 'Synthetic preview fixture; fictional hours and work only.') AND deleted_at IS NULL),
        'project_template', (SELECT count(*) FROM hub.project_template WHERE name IN ('Small site servicing template', 'Synthetic preview — Small site servicing template'))
    ));"""
    result = run_checked([*COMPOSE, "exec", "-T", "db", "psql", "-U", "hub_review", "-d", "hub_review", "-At", "-c", sql], cwd=root, runner=runner)
    try:
        snapshot = json.loads(result.stdout)
        projects, counts = snapshot["projects"], snapshot["counts"]
        if {row["number"] for row in projects} != {"SYN-101", "SYN-102", "SYN-103"} or len(projects) != 3:
            raise ValueError
        if next(row for row in projects if row["number"] == "SYN-103")["visibility"] != "Restricted":
            raise ValueError
        if any(counts.get(table, 0) < minimum for table, minimum in FIXTURE_MINIMUMS.items()):
            raise ValueError
        if counts.get("planning_entry", 0) < 7 or counts.get("project_template", 0) < 1:
            raise ValueError
        for row in projects:
            uuid.UUID(row["id"])
        return snapshot
    except (KeyError, TypeError, ValueError, StopIteration):
        raise WrapperError("Tuesday fixture records are incomplete; inspect the private seed logs") from None


def validate_reserved_mappings(users: list[dict[str, Any]], ids: dict[str, str], *, complete: bool) -> None:
    by_login = {u["userName"].casefold(): u for u in users}
    needed = ALL_IDENTITIES if complete else LEGACY
    if any(name not in by_login or str(uuid.UUID(by_login[name]["userId"])) != ids[name] for name in needed):
        raise WrapperError("review verifier does not match the reserved account IDs")


def run(argv: list[str] | None = None, *, root: Path = ROOT, verify: Callable[[str], None] | None = None,
        runner: Callable[..., Any] = subprocess.run, sleeper: Callable[[float], None] = time.sleep,
        hosted_verify: Callable[[str, Path], None] | None = None, now: Callable[[], str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("release")
    args = parser.parse_args(argv)
    release = args.release
    verify_exact_release(release, root, verify)

    runtime = (root / ".runtime").resolve()
    data = (root / "data").resolve()
    private_dir(runtime)
    private_dir(data)
    verifier_path = runtime / "review-users.json"
    private_file(verifier_path)
    target_handoff = runtime / "tuesday-review-login-handoff.json"
    if target_handoff.exists():
        private_file(target_handoff)
    manifest = data / f"tuesday-fixtures-{release}.json"
    ids = reserved_ids(root, runner)
    users = verifier_users(verifier_path)
    validate_reserved_mappings(users, ids, complete=False)
    if manifest.exists():
        private_file(manifest)
        try:
            existing = json.loads(manifest.read_text())
        except (OSError, ValueError, json.JSONDecodeError) as exc:
            raise WrapperError("existing Tuesday fixture manifest is malformed") from exc
        validate_reserved_mappings(users, ids, complete=True)
        credentials = read_handoff(target_handoff, len(PERSONAS))
        recorded = {row.get("login"): row.get("userId") for row in existing.get("reservedAppUsers", [])} if isinstance(existing, dict) else {}
        if (not isinstance(existing, dict) or existing.get("release") != release or set(credentials) != PERSONA_SET
                or recorded != {name: ids[name] for name in PERSONAS} or not credentials_match(users, credentials)):
            raise WrapperError("existing Tuesday fixture manifest or handoff does not match the release")
        dataset_snapshot(root, runner)
        return 0

    by_login = {u["userName"]: u for u in users}
    if len(users) < len(LEGACY) or any(login not in by_login or str(uuid.UUID(by_login[login]["userId"])) != ids[login] for login in LEGACY):
        raise WrapperError("existing verifier does not preserve the three legacy identities")
    mappings = [f"{name}:{ids[name]}" for name in PERSONAS]
    if target_handoff.exists():
        final_credentials = read_handoff(target_handoff, len(PERSONAS))
        validate_reserved_mappings(users, ids, complete=True)
        if not credentials_match(users, final_credentials):
            raise WrapperError("existing Tuesday handoff does not match the current verifier; refusing rotation")
    else:
        extend_credentials(root, mappings, target_handoff, runner)
        final_credentials = read_handoff(target_handoff, len(PERSONAS))
        updated_users = verifier_users(verifier_path)
        validate_reserved_mappings(updated_users, ids, complete=True)
        if not credentials_match(updated_users, final_credentials):
            raise WrapperError("new handoff does not match the saved verifier")
    if set(final_credentials) != PERSONA_SET:
        raise WrapperError("complete ten-person handoff was not prepared")

    print("Checking thirteen review identities; existing credentials preserved.", flush=True)
    restart_api(root, release, runner)
    wait_health(root, runner, sleeper)
    print("Checking individual Tuesday sign-ins with the existing rate limit.", flush=True)
    if hosted_verify:
        hosted_verify(release, target_handoff)
    else:
        fixture_api = load_module(root / "tools/preview/fixture_api.py", "fixture_api_verify")
        fixture_api.connect(["--hosted-review", "--credentials", str(target_handoff), "--release", release])

    log_path = runtime / f"tuesday-fixtures-{release}.log"
    sleeper(60)
    log_lines = [f"release={release}", "provenance=hosted review synthetic Tuesday fixture", "transport=local-password same-person API",
                 f"reserved_ids_validated={len(ids)}"]
    atomic_json(log_path, {"release": release, "provenance": log_lines})
    for index, script in enumerate(("seed_resources.py", "seed_modules.py", "seed_planning.py")):
        print(f"Running {script} through permissioned review API calls…", flush=True)
        try:
            result = seed(root, release, target_handoff, script, runner)
        except WrapperError as exc:
            cause = exc.__cause__
            output = {"script": script, "status": "failed", "stdout": getattr(cause, "stdout", "") or "", "stderr": getattr(cause, "stderr", "") or ""}
            atomic_json(runtime / f"{script}-{release}.log.json", output)
            raise WrapperError(f"{script} failed; inspect its private runtime log") from exc
        atomic_json(runtime / f"{script}-{release}.log.json", {"script": script, "status": "passed", "stdout": result.stdout or "", "stderr": result.stderr or ""})
        log_lines.append(f"{script}: completed; captured_lines={len((result.stdout or '').splitlines())}")
        atomic_json(log_path, {"release": release, "provenance": log_lines})
        if index < 2:
            sleeper(60)
    atomic_json(log_path, {"release": release, "provenance": log_lines})
    snapshot = dataset_snapshot(root, runner)
    atomic_json(manifest, {
        "release": release,
        "provenance": {
            "source": "Tuesday hosted synthetic review fixture",
            "transport": "local-password same-person API",
            "database": "pm-tool-review-db",
            "scripts": ["seed_resources.py", "seed_modules.py", "seed_planning.py"],
        },
        "reservedAppUsers": [{"login": name, "email": EXPECTED_EMAILS[name], "userId": ids[name]} for name in PERSONAS],
        "counts": {"accounts": len(PERSONAS), **snapshot["counts"]},
        "projects": snapshot["projects"],
        "log": log_path.name,
        "completedAt": (now or (lambda: datetime.now(UTC).isoformat()))(),
    })
    print(f"Prepared Tuesday hosted fixture for release {release}; credentials and provenance remain in private runtime files.")
    return 0


def main() -> int:
    try:
        return run()
    except WrapperError as exc:
        print(f"Refused: {exc}", file=sys.stderr)
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
