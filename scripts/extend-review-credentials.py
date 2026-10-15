#!/usr/bin/env python3
"""Append whitelisted synthetic review logins without rotating existing credentials.

This helper is for an operator who already has the review verifier file and the
reserved AppUser IDs. It never prints passwords, hashes, salts or cookies. A
new handoff file is written only when at least one mapping is new; existing
handoff files are never overwritten.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import secrets
import tempfile
import uuid
from pathlib import Path
from typing import Any

PBKDF2_ROUNDS = 600_000
MAX_ACCOUNTS = 64
ALLOWED_LOGINS = frozenset({
    "jordan", "lena", "sam", "priya", "marc",
    "alex", "jill", "diane", "omar", "rita",
})


def private_parent(path: Path) -> None:
    if not path.parent.exists() or not path.parent.is_dir():
        raise ValueError(f"parent directory does not exist: {path.parent}")
    if path.parent.stat().st_uid != os.getuid():
        raise ValueError("parent directory must be owned by the current user")
    if path.parent.stat().st_mode & 0o077:
        raise ValueError("parent directory must be private")


def safe_path(raw: Path) -> Path:
    path = raw.expanduser()
    if not path.is_absolute():
        path = Path.cwd() / path
    for ancestor in (path, *path.parents):
        if ancestor.is_symlink():
            raise ValueError("symlink paths and ancestors are not permitted")
    return path.resolve()


def read_verifiers(path: Path) -> tuple[dict[str, Any], list[dict[str, Any]]]:
    if path.is_symlink() or not path.exists() or not path.is_file():
        raise ValueError("verifier file must be an existing regular file")
    if path.stat().st_uid != os.getuid():
        raise ValueError("verifier file must be owned by the current user")
    if path.stat().st_mode & 0o077:
        raise ValueError("verifier file must be private to its owner")
    try:
        value = json.loads(path.read_text())
    except (OSError, json.JSONDecodeError) as exc:
        raise ValueError("verifier file is not valid JSON") from exc
    if not isinstance(value, dict) or set(value) != {"users"} or not isinstance(value["users"], list):
        raise ValueError('verifier file must contain only a "users" list')
    users = value["users"]
    if len(users) > MAX_ACCOUNTS:
        raise ValueError("verifier file exceeds the 64-account limit")
    seen_logins: set[str] = set()
    seen_ids: set[str] = set()
    for user in users:
        if not isinstance(user, dict) or not isinstance(user.get("userName"), str) or not isinstance(user.get("userId"), str):
            raise ValueError("verifier contains an invalid account mapping")
        login = user["userName"].casefold()
        if not login or len(login) > 64 or any(c not in "abcdefghijklmnopqrstuvwxyz0123456789._@-" for c in login):
            raise ValueError("verifier contains an invalid login")
        if not isinstance(user.get("salt"), str) or not isinstance(user.get("hash"), str):
            raise ValueError("verifier contains incomplete credential material")
        try:
            salt = bytes.fromhex(user["salt"])
            digest = bytes.fromhex(user["hash"])
        except ValueError as exc:
            raise ValueError("verifier contains malformed credential material") from exc
        if len(salt) != 16 or len(digest) != 32:
            raise ValueError("verifier contains malformed credential material")
        try:
            user_id = str(uuid.UUID(user["userId"]))
        except (ValueError, AttributeError) as exc:
            raise ValueError("verifier contains an invalid user ID") from exc
        if login in seen_logins or user_id in seen_ids:
            raise ValueError("verifier contains duplicate account mappings")
        seen_logins.add(login)
        seen_ids.add(user_id)
    return value, users


def atomic_write(path: Path, value: dict[str, Any]) -> None:
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


def extend(verifier_path: Path, handoff_path: Path, mappings: list[str]) -> int:
    verifier = safe_path(verifier_path)
    handoff = safe_path(handoff_path)
    if verifier == handoff:
        raise ValueError("verifier and handoff paths must be different")
    if verifier_path.is_symlink() or handoff_path.is_symlink():
        raise ValueError("symlink paths are not permitted")
    private_parent(verifier)
    private_parent(handoff)
    value, users = read_verifiers(verifier)
    if len(mappings) > MAX_ACCOUNTS:
        raise ValueError("too many account mappings")

    existing_by_login = {u["userName"].casefold(): u for u in users}
    existing_by_id = {str(uuid.UUID(u["userId"])): u for u in users}
    requested: list[tuple[str, str]] = []
    requested_logins: set[str] = set()
    requested_ids: set[str] = set()
    for raw in mappings:
        login, separator, raw_id = raw.partition(":")
        if not separator or login not in ALLOWED_LOGINS:
            raise ValueError("login is not on the review whitelist")
        try:
            user_id = str(uuid.UUID(raw_id))
        except ValueError as exc:
            raise ValueError("mapping contains an invalid user ID") from exc
        if login in requested_logins or user_id in requested_ids:
            raise ValueError("duplicate login or user ID in input")
        requested_logins.add(login)
        requested_ids.add(user_id)
        old_login = existing_by_login.get(login)
        old_id = existing_by_id.get(user_id)
        if old_login is not None and str(uuid.UUID(old_login["userId"])) != user_id:
            raise ValueError("login is already mapped to another user ID")
        if old_id is not None and old_id["userName"].casefold() != login:
            raise ValueError("user ID is already mapped to another login")
        if old_login is None and old_id is None:
            requested.append((login, user_id))

    if not requested:
        return 0
    if handoff.exists():
        raise ValueError("refusing to overwrite existing handoff file")
    if len(users) + len(requested) > MAX_ACCOUNTS:
        raise ValueError("account limit exceeded")

    credentials: list[dict[str, str]] = []
    for login, user_id in requested:
        password = secrets.token_urlsafe(32)
        salt = os.urandom(16)
        users.append({
            "userId": user_id,
            "userName": login,
            "salt": salt.hex(),
            "hash": hashlib.pbkdf2_hmac("sha256", password.encode(), salt, PBKDF2_ROUNDS).hex(),
        })
        credentials.append({"login": login, "password": password})

    # Commit the private password handoff first. If verifier replacement fails,
    # the generated credentials remain recoverable and the existing verifier
    # remains untouched for an operator to repair or retry safely.
    atomic_write(handoff, {"credentials": credentials})
    atomic_write(verifier, value)
    return len(requested)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--verifier-file", required=True, type=Path)
    parser.add_argument("--new-private-handoff-file", required=True, type=Path)
    parser.add_argument("mappings", nargs="+", metavar="login:existing-AppUser-UUID")
    args = parser.parse_args()
    try:
        added = extend(args.verifier_file, args.new_private_handoff_file, args.mappings)
    except (OSError, ValueError) as exc:
        parser.error(str(exc))
    if added:
        print(f"Prepared {added} new private review login(s); read and remove the handoff file after distribution.")
    else:
        print("No new review logins were needed; existing verifier mappings were preserved.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
