"""Small, fail-closed API transport shared by synthetic preview seed scripts.

The ordinary path is deliberately boring: it verifies the named local preview
target, requires Development authentication, and sends the existing
``X-Dev-User`` header.  The hosted-review path is an explicit operator action;
it validates the prepared homedev release and logs in every fixed synthetic
persona from a private handoff before allowing any API calls.
"""

from __future__ import annotations

import argparse
import http.client
import json
import os
from pathlib import Path
import re
import stat
import subprocess
import sys
import time
from typing import Any, Callable
from urllib.parse import unquote, urlsplit

from preview_target import verify_preview_target


PREVIEW_BASE = "http://localhost:5080"
HOSTED_BASE = "http://127.0.0.1:3080"
PUBLIC_HOST = "pm.engcalchub.com"
PUBLIC_ORIGIN = "https://pm.engcalchub.com"
PERSONAS = ("jordan", "lena", "sam", "priya", "marc", "alex", "jill", "diane", "omar", "rita")
PERSONA_SET = frozenset(PERSONAS)
RELEASE = re.compile(r"[0-9a-f]{40}\Z")
WHO = re.compile(r"[a-z][a-z0-9._-]*\Z")


class FixtureError(RuntimeError):
    """A safe, concise transport/setup error (never includes credentials)."""

    def __init__(self, status: int | None, text: str):
        self.status = status
        super().__init__(text)


def _parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("base", nargs="?", help=f"local preview base (default {PREVIEW_BASE})")
    parser.add_argument("--hosted-review", action="store_true", help="use the guarded homedev review transport")
    parser.add_argument("--credentials", help="private review-login handoff JSON")
    parser.add_argument("--release", help="40-character prepared homedev release SHA")
    return parser


def _safe_path(path: str) -> str:
    if not isinstance(path, str) or not path:
        raise FixtureError(None, "Refusing unsafe API path")
    decoded = path
    for _ in range(8):
        next_value = unquote(decoded)
        if next_value == decoded:
            break
        decoded = next_value
    else:
        raise FixtureError(None, "Refusing unsafe API path")
    parsed = urlsplit(decoded)
    decoded_path = parsed.path
    if (parsed.scheme or parsed.netloc or parsed.fragment or decoded_path.startswith(("/", "\\"))
            or "\\" in decoded_path or "//" in decoded_path):
        raise FixtureError(None, "Refusing unsafe API path")
    if any(part in ("", ".", "..") for part in decoded_path.split("/")):
        raise FixtureError(None, "Refusing unsafe API path")
    return path


def _safe_who(who: str) -> str:
    if not isinstance(who, str) or not WHO.fullmatch(who):
        raise FixtureError(None, "Refusing unsafe synthetic persona")
    return who


def _error(status: int | None, method: str, path: str) -> FixtureError:
    # Keep response bodies out of diagnostics. They can contain reflected input,
    # cookie material, or implementation details that should not reach a log.
    return FixtureError(status, f"{method} API request failed: HTTP {status or 'transport error'}")


def _private_path(value: str) -> Path:
    path = Path(value).expanduser()
    if not path.is_absolute():
        path = Path.cwd() / path
    path = Path(os.path.abspath(path))
    if path.is_symlink() or not path.is_file():
        raise ValueError

    known_runtime = Path(os.path.abspath(Path.cwd() / ".runtime"))
    system_aliases = {Path("/var"), Path("/tmp")}
    candidate = path.parent
    while candidate != candidate.parent:
        if candidate.is_symlink() and candidate != known_runtime and candidate not in system_aliases:
            raise ValueError
        candidate = candidate.parent
    if path.parent.is_symlink():
        # Prepared releases have exactly one intentional link: release/.runtime
        # -> the shared review runtime. Canonicalize that link before checking
        # ownership and modes; arbitrary symlink ancestors remain forbidden.
        if path.parent != known_runtime or Path.cwd().name != path.parent.parent.name:
            raise ValueError
        path = path.resolve(strict=True)
    return path


def _read_private_credentials(value: str) -> dict[str, str]:
    try:
        path = _private_path(value)
        parent = path.parent
        if not parent.is_dir():
            raise ValueError
        if stat.S_IMODE(parent.stat().st_mode) & 0o077 or stat.S_IMODE(path.stat().st_mode) & 0o077:
            raise ValueError
        uid = os.getuid()
        if path.stat().st_uid != uid or parent.stat().st_uid != uid:
            raise ValueError
        payload = json.loads(path.read_text())
        entries = payload["credentials"]
        if not isinstance(entries, list) or len(entries) != len(PERSONAS):
            raise ValueError
        result: dict[str, str] = {}
        for entry in entries:
            if not isinstance(entry, dict) or set(entry) != {"login", "password"}:
                raise ValueError
            login, password = entry["login"], entry["password"]
            if login not in PERSONA_SET or not isinstance(password, str) or not password or login in result:
                raise ValueError
            result[login] = password
        if frozenset(result) != PERSONA_SET:
            raise ValueError
        return result
    except (OSError, TypeError, ValueError, KeyError, json.JSONDecodeError):
        raise FixtureError(None, "Refusing hosted review credentials: private ten-person handoff required") from None


def _env_map(env: list[str]) -> dict[str, str]:
    result: dict[str, str] = {}
    for item in env:
        key, sep, value = item.partition("=")
        if sep:
            if key in result and result[key] != value:
                raise ValueError
            result[key] = value
    return result


def _connection_target(value: str) -> dict[str, str]:
    """Parse only enough of the connection string to prove its database target."""
    values: dict[str, str] = {}
    for item in value.split(";"):
        if not item.strip():
            continue
        key, sep, raw = item.partition("=")
        if not sep or not key.strip():
            raise ValueError
        key = key.strip().lower()
        raw = raw.strip()
        # Npgsql accepts aliases such as Server and Initial Catalog. Only the
        # canonical runbook fields may establish this fixed target.
        if key not in {"host", "port", "database", "username", "password"}:
            raise ValueError
        if key in values and values[key] != raw:
            raise ValueError
        values[key] = raw
    if values.get("host") != "db" or values.get("database") != "hub_review" or values.get("username") != "hub_review":
        raise ValueError
    if values.get("port", "5432") != "5432":
        raise ValueError
    return values


def _docker_inspect(release: str) -> list[dict[str, Any]]:
    try:
        raw = subprocess.check_output(
            ["sudo", "-n", "docker", "inspect", "pm-tool-review-api-1", "pm-tool-review-db-1"],
            text=True,
            stderr=subprocess.DEVNULL,
        )
        value = json.loads(raw)
        if not isinstance(value, list) or len(value) != 2:
            raise ValueError
        return value
    except (OSError, subprocess.CalledProcessError, TypeError, ValueError, json.JSONDecodeError):
        raise FixtureError(None, "Refusing hosted review: exact review containers could not be inspected") from None


def _verify_hosted(release: str) -> None:
    if not RELEASE.fullmatch(release):
        raise FixtureError(None, "Refusing hosted review: release must be a 40-character lowercase SHA")
    if Path.cwd().name != release:
        raise FixtureError(None, "Refusing hosted review: current directory is not the requested prepared release")
    inspected = _docker_inspect(release)
    api, db = inspected
    api_labels = api.get("Config", {}).get("Labels") or {}
    try:
        api_env = _env_map(api.get("Config", {}).get("Env") or [])
        _connection_target(api_env["ConnectionStrings__Hub"])
    except (KeyError, TypeError, ValueError):
        raise FixtureError(None, "Refusing hosted review: API database target is not the review database") from None
    if api.get("Config", {}).get("Image") != f"pm-tool-review:{release}":
        raise FixtureError(None, "Refusing hosted review: API image is not the requested review release")
    if api.get("Name") != "/pm-tool-review-api-1":
        raise FixtureError(None, "Refusing hosted review: API container name is not the review container")
    if api_labels.get("com.docker.compose.project") != "pm-tool-review" or api_labels.get("com.docker.compose.service") != "api":
        raise FixtureError(None, "Refusing hosted review: API compose identity is not pm-tool-review/api")
    networks = api.get("NetworkSettings", {}).get("Networks") or {}
    if set(networks) != {"pm-tool-review"}:
        raise FixtureError(None, "Refusing hosted review: API is attached to an unexpected network")
    ports = api.get("NetworkSettings", {}).get("Ports") or {}
    if ports != {"8080/tcp": [{"HostIp": "127.0.0.1", "HostPort": "3080"}]}:
        raise FixtureError(None, "Refusing hosted review: API is not bound only to loopback port 3080")
    required = {
        "ASPNETCORE_ENVIRONMENT": "Staging",
        "Auth__Mode": "LocalPassword",
        "Seed__ReviewDemo": "true",
        "Seed__DevUsers": "false",
        "Hosting__LocalTunnelProxy": "true",
    }
    if any(api_env.get(key) != value for key, value in required.items()):
        raise FixtureError(None, "Refusing hosted review: API runtime configuration is not the review configuration")

    db_labels = db.get("Config", {}).get("Labels") or {}
    mounts = db.get("Mounts") or []
    has_review_data = any(
        mount.get("Destination") == "/var/lib/postgresql/data" and mount.get("Name") == "pm-tool-review-db"
        for mount in mounts
    )
    if db.get("Name") != "/pm-tool-review-db-1":
        raise FixtureError(None, "Refusing hosted review: database container name is not the review container")
    if set(db.get("NetworkSettings", {}).get("Networks") or {}) != {"pm-tool-review"}:
        raise FixtureError(None, "Refusing hosted review: database is attached to an unexpected network")
    if db_labels.get("com.docker.compose.project") != "pm-tool-review" or db_labels.get("com.docker.compose.service") != "db" or not has_review_data:
        raise FixtureError(None, "Refusing hosted review: database is not the pm-tool-review-db volume")


class FixtureApi:
    def __init__(
        self,
        base: str,
        hosted: bool,
        cookies: dict[str, str] | None = None,
        *,
        connection_factory: Callable[[str, int, float], Any] | None = None,
    ):
        self.base = base.rstrip("/")
        self.hosted = hosted
        self._cookies = cookies or {}
        self._connection_factory = connection_factory or http.client.HTTPConnection

    def _request(self, method: str, path: str, body: Any = None, *, who: str | None = None) -> tuple[int, bytes, str]:
        parsed = urlsplit(self.base)
        conn = self._connection_factory(parsed.hostname or "", parsed.port or 80, 30)
        headers = {"Accept": "application/json", "Host": PUBLIC_HOST if self.hosted else (parsed.hostname or "localhost")}
        if self.hosted:
            headers["X-Forwarded-Proto"] = "https"
            if who is not None and who in self._cookies:
                headers["Cookie"] = self._cookies[who]
            if method not in {"GET", "HEAD", "OPTIONS"}:
                headers["Origin"] = PUBLIC_ORIGIN
        elif who is not None:
            headers["X-Dev-User"] = f"{who}@hub.test"
        encoded = None
        if isinstance(body, dict) and "rowVersion" in body and method not in {"GET", "HEAD", "OPTIONS"}:
            version = body["rowVersion"]
            if type(version) is not int or version < 0:
                raise FixtureError(None, "Refusing invalid write version")
            headers["If-Match"] = f'"{version}"'
        if body is not None:
            encoded = json.dumps(body).encode()
            headers["Content-Type"] = "application/json"
        try:
            conn.request(method, parsed.path.rstrip("/") + path, encoded, headers)
            response = conn.getresponse()
            data = response.read()
            cookie = "; ".join(value.split(";", 1)[0] for key, value in response.getheaders() if key.lower() == "set-cookie")
            status = response.status
        except OSError:
            raise _error(None, method, path) from None
        finally:
            conn.close()
        return status, data, cookie

    def _config(self) -> dict[str, Any]:
        status, data, _ = self._request("GET", "/api/v1/config")
        if status != 200:
            raise _error(status, "GET", "/api/v1/config")
        try:
            value = json.loads(data)
        except (TypeError, ValueError):
            raise FixtureError(status, "Refusing to run: config response was not JSON") from None
        if value.get("authMode") != ("LocalPassword" if self.hosted else "Development"):
            raise FixtureError(status, f"Refusing to run: config authMode is not the expected {'LocalPassword' if self.hosted else 'Development'} mode")
        return value

    def call_with_headers(self, path: str, who: str, method: str, body: Any, headers: dict[str, str]) -> Any:
        # The only extra header allowed is the normal optimistic-concurrency token.
        if set(headers) != {"If-Match"} or not re.fullmatch(r'"[0-9]+"', headers["If-Match"]):
            raise FixtureError(None, "Refusing unsupported fixture header")
        if not isinstance(body, dict):
            raise FixtureError(None, "Refusing unversioned fixture body")
        return self.call(path, who, method, {**body, "rowVersion": int(headers["If-Match"].strip('"'))})

    def call(self, path: str, who: str, method: str = "GET", body: Any = None) -> Any:
        path = _safe_path(path)
        who = _safe_who(who)
        if self.hosted and who not in PERSONA_SET:
            raise FixtureError(None, "Refusing hosted review: persona is not in the fixed synthetic set")
        if self.hosted and who not in self._cookies:
            raise FixtureError(None, "Refusing hosted review: persona was not authenticated")
        status, data, _ = self._request(method.upper(), "/api/v1/" + path, body, who=who)
        if status < 200 or status >= 300:
            raise _error(status, method.upper(), path)
        if not data:
            return None
        try:
            return json.loads(data)
        except (TypeError, ValueError):
            raise FixtureError(status, f"{method.upper()} API response was not JSON") from None


def _hosted_login(api: FixtureApi, credentials: dict[str, str], sleep_fn: Callable[[float], None]) -> None:
    for index, who in enumerate(PERSONAS):
        if index:
            # Program.cs permits five local sign-ins per minute per client key.
            # A new process may inherit a previous process's window; operators
            # should wait a full minute before starting another hosted run.
            sleep_fn(13.0)
        status, data, cookie = api._request(
            "POST", "/api/v1/auth/local/sign-in", {"userName": who, "password": credentials[who]}
        )
        if status != 204 or not cookie:
            raise _error(status, "POST", "/api/v1/auth/local/sign-in")
        api._cookies[who] = cookie
        status, data, _ = api._request("GET", "/api/v1/me", who=who)
        if status != 200:
            raise _error(status, "GET", "/api/v1/me")
        try:
            email = json.loads(data).get("email")
        except (TypeError, ValueError):
            email = None
        if email != f"{who}@hub.test":
            raise FixtureError(status, "Refusing hosted review: authenticated identity did not match the requested persona")


def connect(argv: list[str] | None = None) -> FixtureApi:
    """Parse seed-script arguments, verify the target, and return a guarded API client."""
    args = _parser().parse_args(sys.argv[1:] if argv is None else argv)
    if args.hosted_review:
        if args.base is not None or not args.credentials or not args.release:
            raise FixtureError(None, "Hosted review requires --credentials and --release and no alternate base URL")
        _verify_hosted(args.release)
        credentials = _read_private_credentials(args.credentials)
        api = FixtureApi(HOSTED_BASE, True)
        api._config()
        _hosted_login(api, credentials, time.sleep)
        return api
    if args.credentials or args.release:
        raise FixtureError(None, "--credentials and --release require --hosted-review")
    base = args.base or PREVIEW_BASE
    verify_preview_target(base)
    api = FixtureApi(base, False)
    api._config()
    return api
