#!/usr/bin/env python3
"""Generate private individual review logins for already-provisioned synthetic users.

The output file is a temporary operator handoff. Deliver its passwords privately,
then remove it. Never put either file in the repository or terminal output.
"""
import argparse
import hashlib
import json
import os
import secrets
import tempfile
import uuid
from pathlib import Path


def private_write(path: Path, value: dict, *, replace: bool = False) -> None:
    if path.exists() and not replace:
        raise SystemExit(f"Refusing to overwrite {path.name}")
    path.parent.mkdir(mode=0o700, parents=True, exist_ok=True)
    if path.parent.stat().st_mode & 0o077:
        raise SystemExit("Output directory must be private")
    fd, temp = tempfile.mkstemp(prefix=f".{path.name}-", dir=path.parent)
    try:
        os.fchmod(fd, 0o600)
        with os.fdopen(fd, "w") as out:
            json.dump(value, out, indent=2)
            out.write("\n")
            out.flush()
            os.fsync(out.fileno())
        os.replace(temp, path)
    finally:
        if os.path.exists(temp):
            os.unlink(temp)


parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("verifiers", type=Path)
parser.add_argument("handoff", type=Path)
parser.add_argument("users", nargs="+", help="login:existing-app-user-uuid")
args = parser.parse_args()
verifiers = args.verifiers.expanduser().resolve()
handoff = args.handoff.expanduser().resolve()
if verifiers == handoff or handoff.exists():
    parser.error("handoff must be a new, separate private file")
if not verifiers.exists() or verifiers.stat().st_mode & 0o077:
    parser.error("verifier file must already exist and be private")
stored = json.loads(verifiers.read_text())
if stored != {"users": []}:
    parser.error("initial bootstrap requires an empty verifier file")
if len(args.users) > 64:
    parser.error("too many users")
entries = []
credentials = []
names = set()
ids = set()
for item in args.users:
    name, sep, raw_id = item.partition(":")
    if not sep or not name or any(c not in "abcdefghijklmnopqrstuvwxyz0123456789._-" for c in name):
        parser.error("login must use lowercase letters, numbers, dots, underscores or hyphens")
    user_id = str(uuid.UUID(raw_id))
    if name in names or user_id in ids:
        parser.error("duplicate login or user ID")
    names.add(name)
    ids.add(user_id)
    password = secrets.token_urlsafe(32)
    salt = os.urandom(16)
    entries.append({"userId": user_id, "userName": name, "salt": salt.hex(),
                    "hash": hashlib.pbkdf2_hmac("sha256", password.encode(), salt, 600_000).hex()})
    credentials.append({"login": name, "password": password})
private_write(handoff, {"credentials": credentials})
private_write(verifiers, {"users": entries}, replace=True)
print(f"Prepared {len(entries)} private review logins. Read and remove {handoff.name} after distribution.")
