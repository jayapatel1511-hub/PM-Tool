#!/usr/bin/env python3
"""Probe the private homedev review origin. Never prints passwords or cookies.

Run on homedev from a prepared release:  python3 scripts/verify-homedev-review.py [base-url] [--social]
Default base is the loopback origin with the public Host header; pass
https://pm.engcalchub.com to repeat the checks through the tunnel.
"""
import argparse, http.client, json, sys, urllib.parse

HOST = "pm.engcalchub.com"
parser = argparse.ArgumentParser()
parser.add_argument("base_url", nargs="?", default="http://127.0.0.1:3080")
parser.add_argument("--social", action="store_true", help="Check Microsoft/Google redirects without reading password credentials")
args = parser.parse_args()
if args.base_url.rstrip("/") not in {"http://127.0.0.1:3080", "https://pm.engcalchub.com"}:
    sys.exit("Refusing unexpected verification origin")
base = urllib.parse.urlsplit(args.base_url)
public = base.scheme == "https"
handoff = ".runtime/review-login-handoff.json"
failures = 0


def call(method, path, body=None, host=HOST, origin=None, cookie=None):
    conn = (http.client.HTTPSConnection if public else http.client.HTTPConnection)(base.hostname, base.port, timeout=15)
    headers = {"Host": host, "Accept": "application/json"}
    if not public:
        headers["X-Forwarded-Proto"] = "https"
    if body is not None:
        headers["Content-Type"] = "application/json"
    if origin:
        headers["Origin"] = origin
    if cookie:
        headers["Cookie"] = cookie
    conn.request(method, path, json.dumps(body) if body is not None else None, headers)
    r = conn.getresponse()
    data = r.read()
    cookies = "; ".join(c.split(";", 1)[0] for k, c in r.getheaders() if k.lower() == "set-cookie")
    conn.close()
    return r.status, data, cookies, {k.lower(): v for k, v in r.getheaders()}


def check(name, ok, detail=""):
    global failures
    failures += not ok
    print(f"{'PASS' if ok else 'FAIL'} {name}{' — ' + detail if detail else ''}")


good = f"https://{HOST}"
s, d, _, _ = call("GET", "/health")
check("health", s == 200 and b"Healthy" in d, f"HTTP {s}")
s, _, _, _ = call("GET", "/api/v1/me")
check("anonymous /me denied", s == 401, f"HTTP {s}")
if not public:
    s, _, _, _ = call("GET", "/health", host="evil.example")
    check("invalid Host denied", s == 400, f"HTTP {s}")
s, _, _, _ = call("POST", "/api/v1/auth/social/sign-out" if args.social else "/api/v1/auth/local/sign-in", {"userName": "x", "password": "y"}, origin="https://evil.example")
check("unsafe Origin POST denied", s == 403, f"HTTP {s}")

if args.social:
    s, d, _, _ = call("GET", "/api/v1/config")
    config = json.loads(d) if s == 200 else {}
    check("both social providers configured", config.get("authMode") == "Social" and set(config.get("providers", [])) == {"Microsoft", "Google"})
    for provider, authority in (("Microsoft", "login.microsoftonline.com"), ("Google", "accounts.google.com")):
        s, _, _, headers = call("GET", "/api/v1/auth/social/sign-in/" + provider)
        location = urllib.parse.urlsplit(headers.get("location", ""))
        query = urllib.parse.parse_qs(location.query)
        check(provider + " HTTPS provider redirect", s == 302 and location.scheme == "https" and location.hostname == authority)
        check(provider + " callback and PKCE", query.get("redirect_uri") == [good + "/signin-" + provider.lower()]
              and query.get("code_challenge_method") == ["S256"] and bool(query.get("state")) and "client_secret" not in query)
        cookie = headers.get("set-cookie", "").lower()
        check(provider + " secure correlation cookie", "secure" in cookie and "httponly" in cookie and "samesite=none" in cookie)
    print("Real-account sign-in and consent require browser verification.")
    print("RESULT", "PASS" if failures == 0 else f"FAIL ({failures})")
    sys.exit(1 if failures else 0)

try:
    creds = json.load(open(handoff))["credentials"]
except (OSError, KeyError, ValueError):
    creds = []
    check("credential handoff readable", False, f"{handoff} missing; sign-in not probed")
if creds:
    c = creds[0]
    s, _, _, _ = call("POST", "/api/v1/auth/local/sign-in", {"userName": c["login"], "password": c["password"] + "x"}, origin=good)
    check("wrong password denied", s == 401, f"HTTP {s}")
    s, _, cookie, _ = call("POST", "/api/v1/auth/local/sign-in", {"userName": c["login"], "password": c["password"]}, origin=good)
    check("individual sign-in", s == 204 and bool(cookie), f"HTTP {s}")
    s, d, _, _ = call("GET", "/api/v1/me", cookie=cookie)
    who = json.loads(d).get("email", "?") if s == 200 else "?"
    check("signed-in /me", s == 200, f"HTTP {s} as {who}")
    s, _, _, _ = call("GET", "/api/v1/projects", cookie=cookie)
    check("signed-in project list", s == 200, f"HTTP {s}")
    s, _, cleared, _ = call("POST", "/api/v1/auth/local/sign-out", {}, origin=good, cookie=cookie)
    check("sign-out", s == 204, f"HTTP {s}")
    s, _, _, _ = call("GET", "/api/v1/me", cookie=cleared or None)
    check("/me after sign-out denied", s == 401, f"HTTP {s}")
    s, _, _, _ = call("GET", "/api/v1/me", cookie=cookie)
    check("copied cookie after sign-out denied", s == 401, f"HTTP {s}")

print("RESULT", "PASS" if failures == 0 else f"FAIL ({failures})")
sys.exit(1 if failures else 0)
