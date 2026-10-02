#!/usr/bin/env python3
"""Private, localhost-only pilot smoke verification."""
import argparse, http.cookiejar, json, os, pathlib, stat, sys, urllib.error, urllib.request

HOST = "pm.engcalchub.com"

def fail(message):
    print(f"FAIL {message}")
    return False

def request(base, path, method="GET", data=None, headers=None, opener=None):
    req = urllib.request.Request(base + path, data=data, method=method, headers=headers or {})
    try:
        with ((opener.open if opener else urllib.request.urlopen)(req, timeout=5)) as response:
            return response.status, response.read().decode("utf-8", "replace")
    except urllib.error.HTTPError as exc:
        return exc.code, exc.read().decode("utf-8", "replace")
    except urllib.error.URLError:
        return 0, ""

def private_credentials(path):
    p = pathlib.Path(path).expanduser().resolve()
    st = p.stat()
    if not stat.S_ISREG(st.st_mode) or st.st_uid != os.getuid() or st.st_mode & 0o077:
        raise ValueError("credential file must be an owner-private regular file")
    if "review" in str(p).lower() or p.name == "review-login-handoff.json":
        raise ValueError("review credential handoff is refused")
    value = json.loads(p.read_text())
    if not isinstance(value, dict) or not isinstance(value.get("login"), str) or not isinstance(value.get("password"), str):
        raise ValueError("credential file must contain login and password strings")
    return value["login"], value["password"]

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("base_url", nargs="?", default="http://127.0.0.1:3081")
    parser.add_argument("--credentials-file")
    args = parser.parse_args()
    base = args.base_url.rstrip("/")
    checks = []
    def check(label, condition):
        checks.append(condition)
        print(("PASS " if condition else "FAIL ") + label)
    status, body = request(base, "/health")
    check("health", status == 200 and "Healthy" in body)
    status, _ = request(base, "/api/auth/me")
    check("anonymous auth rejected", status == 401)
    status, _ = request(base, "/api/auth/me", headers={"Host": "not-the-pilot-host.invalid"})
    check("unexpected host rejected", status in (400, 403))
    status, _ = request(base, "/api/auth/sign-in", "POST", b"{}", {"Content-Type": "application/json", "Origin": "https://evil.invalid"})
    check("unsafe origin rejected", status in (400, 403))
    if not args.credentials_file:
        print("UNPROVEN auth browser check: explicit private verifier file was not provided")
        print("RESULT FAIL")
        return 2
    try:
        login, password = private_credentials(args.credentials_file)
    except (OSError, ValueError, json.JSONDecodeError) as exc:
        print(f"FAIL credentials: {exc}")
        print("RESULT FAIL")
        return 2
    jar = http.cookiejar.CookieJar()
    opener = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(jar))
    payload = json.dumps({"login": login, "password": "wrong"}).encode()
    status, _ = request(base, "/api/auth/sign-in", "POST", payload, {"Content-Type": "application/json", "Origin": f"https://{HOST}"}, opener)
    check("wrong password rejected", status == 401)
    payload = json.dumps({"login": login, "password": password}).encode()
    status, _ = request(base, "/api/auth/sign-in", "POST", payload, {"Content-Type": "application/json", "Origin": f"https://{HOST}"}, opener)
    check("private sign-in", status in (200, 204))
    status, _ = request(base, "/api/auth/me", opener=opener)
    check("authenticated me", status == 200)
    status, _ = request(base, "/api/projects", opener=opener)
    check("authenticated projects", status == 200)
    status, _ = request(base, "/api/auth/sign-out", "POST", b"", {"Origin": f"https://{HOST}"}, opener)
    check("sign-out", status in (200, 204))
    status, _ = request(base, "/api/auth/me", opener=opener)
    check("signed-out auth rejected", status == 401)
    result = all(checks)
    print("RESULT " + ("PASS" if result else "FAIL"))
    return 0 if result else 1

if __name__ == "__main__":
    sys.exit(main())
