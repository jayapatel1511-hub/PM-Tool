#!/usr/bin/env python3
"""Create private, independent homedev review credentials without printing secrets."""
import json
import os
import secrets
from pathlib import Path


def create_private(path: Path, content: str) -> None:
    fd = os.open(path, os.O_CREAT | os.O_EXCL | os.O_WRONLY, 0o600)
    try:
        with os.fdopen(fd, "w") as out:
            out.write(content)
            out.flush()
            os.fsync(out.fileno())
    except BaseException:
        path.unlink(missing_ok=True)
        raise


root = Path(__file__).resolve().parents[1]
runtime = root / ".runtime"
runtime.mkdir(mode=0o700, exist_ok=True)
if runtime.stat().st_mode & 0o077:
    raise SystemExit(".runtime must be private to its owner (chmod 700)")
env = runtime / "review.env"
users = runtime / "review-users.json"
if env.exists() or users.exists():
    raise SystemExit("Review credentials already exist; preserving them unchanged")
(runtime / "keys").mkdir(mode=0o700, exist_ok=True)
password = secrets.token_urlsafe(48)
create_private(env,
    f"REVIEW_DB_PASSWORD={password}\n"
    f"ConnectionStrings__Hub=Host=db;Port=5432;Database=hub_review;Username=hub_review;Password={password}\n")
create_private(users, json.dumps({"users": []}, indent=2) + "\n")
print("Private review database settings, empty reviewer list and persistent key directory created.")
