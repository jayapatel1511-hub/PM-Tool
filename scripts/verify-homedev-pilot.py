#!/usr/bin/env python3
"""Private localhost pilot smoke verification with the public host modeled."""
import argparse, http.client, json, os, pathlib, stat, sys, urllib.parse

HOST = "pm.engcalchub.com"
LOOPBACK = "http://127.0.0.1:3081"

def private_credentials(path):
    p = pathlib.Path(path).expanduser().resolve()
    st = p.stat()
    if not stat.S_ISREG(st.st_mode) or st.st_uid != os.getuid() or st.st_mode & 0o077:
        raise ValueError("credential file must be an owner-private regular file")
    if "review" in str(p).lower() or p.name == "review-login-handoff.json":
        raise ValueError("review credential handoff is refused")
    repo = pathlib.Path(__file__).resolve().parents[1]
    try:
        p.relative_to(repo)
    except ValueError:
        pass
    else:
        raise ValueError("credential file must be outside the release source tree")
    value = json.loads(p.read_text())
    if not isinstance(value, dict) or not isinstance(value.get("login"), str) or not isinstance(value.get("password"), str):
        raise ValueError("credential file must contain login and password strings")
    return value["login"], value["password"]

def validate_base(value):
    parsed = urllib.parse.urlsplit(value)
    if (parsed.scheme, parsed.hostname, parsed.port or 80, parsed.path.rstrip("/"), parsed.query, parsed.fragment, parsed.username, parsed.password) != ("http", "127.0.0.1", 3081, "", "", "", None, None):
        raise ValueError(f"base URL must be exactly {LOOPBACK}")
    return LOOPBACK

def call(base, method, path, body=None, host=HOST, origin=None, cookie=None):
    parsed = urllib.parse.urlsplit(base)
    conn = http.client.HTTPConnection(parsed.hostname, parsed.port, timeout=5)
    headers = {"Host": host, "Accept": "application/json", "X-Forwarded-Proto": "https"}
    if body is not None:
        headers["Content-Type"] = "application/json"
    if origin:
        headers["Origin"] = origin
    if cookie:
        headers["Cookie"] = cookie
    try:
        conn.request(method, path, json.dumps(body) if body is not None else None, headers)
        response = conn.getresponse()
        data = response.read()
        set_cookie = next((value for key, value in response.getheaders() if key.lower() == "set-cookie"), "")
        return response.status, data, set_cookie
    except (OSError, http.client.HTTPException):
        return 0, b"", ""
    finally:
        conn.close()


def cookie_pair(header):
    return header.split(";", 1)[0] if header else ""

def secure_cookie(header):
    fields = {part.strip().lower() for part in header.split(";")[1:]}
    return (header.startswith("__Host-hub-review=") and "secure" in fields and "httponly" in fields
            and "samesite=strict" in fields and "path=/" in fields
            and not any(part.strip().lower().startswith("domain=") for part in header.split(";")[1:]))

def run(argv, call_fn=call):
    parser = argparse.ArgumentParser()
    parser.add_argument("base_url", nargs="?", default=LOOPBACK)
    parser.add_argument("--credentials-file")
    args = parser.parse_args(argv)
    try:
        base = validate_base(args.base_url)
        credentials = private_credentials(args.credentials_file) if args.credentials_file else None
    except (OSError, ValueError, json.JSONDecodeError) as exc:
        print(f"FAIL preflight: {exc}")
        print("RESULT FAIL")
        return 2

    failures = 0
    def check(label, ok, detail=""):
        nonlocal failures
        failures += not ok
        print(f"{'PASS' if ok else 'FAIL'} {label}{' — ' + detail if detail else ''}")

    status, body, _ = call_fn(base, "GET", "/health")
    check("health", status == 200 and b"Healthy" in body, f"HTTP {status}")
    status, _, _ = call_fn(base, "GET", "/api/v1/me")
    check("anonymous /me denied", status == 401, f"HTTP {status}")
    status, _, _ = call_fn(base, "GET", "/health", host="evil.example")
    check("invalid Host denied", status == 400, f"HTTP {status}")
    status, _, _ = call_fn(base, "POST", "/api/v1/auth/local/sign-in", {"userName": "x", "password": "y"}, origin="https://evil.example")
    check("unsafe Origin POST denied", status == 403, f"HTTP {status}")
    if credentials is None:
        print("UNPROVEN auth browser check: explicit private verifier file was not provided")
        print("RESULT FAIL")
        return 2

    login, password = credentials
    good = f"https://{HOST}"
    status, _, _ = call_fn(base, "POST", "/api/v1/auth/local/sign-in", {"userName": login, "password": "wrong"}, origin=good)
    check("wrong password denied", status == 401, f"HTTP {status}")
    status, _, set_cookie = call_fn(base, "POST", "/api/v1/auth/local/sign-in", {"userName": login, "password": password}, origin=good)
    cookie = cookie_pair(set_cookie)
    check("private sign-in", status == 204 and bool(cookie) and secure_cookie(set_cookie), f"HTTP {status}")
    status, _, _ = call_fn(base, "GET", "/api/v1/me", cookie=cookie)
    check("signed-in /me", status == 200, f"HTTP {status}")
    status, _, _ = call_fn(base, "GET", "/api/v1/projects", cookie=cookie)
    check("signed-in project list", status == 200, f"HTTP {status}")
    status, _, cleared_header = call_fn(base, "POST", "/api/v1/auth/local/sign-out", {}, origin=good, cookie=cookie)
    cleared = cookie_pair(cleared_header)
    check("sign-out", status == 204, f"HTTP {status}")
    status, _, _ = call_fn(base, "GET", "/api/v1/me", cookie=cleared or None)
    check("/me after sign-out denied", status == 401, f"HTTP {status}")
    print("RESULT", "PASS" if failures == 0 else f"FAIL ({failures})")
    return 1 if failures else 0

if __name__ == "__main__":
    sys.exit(run(sys.argv[1:]))
