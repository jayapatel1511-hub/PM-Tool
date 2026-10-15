"""Check the social deployment mode and probes without Docker, root or credentials."""
import contextlib
import io
import json
import runpy
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch
from urllib.parse import urlencode

ROOT = Path(__file__).resolve().parents[1]


class SocialActivationTests(unittest.TestCase):
    def probe(self, change=None, origin=None):
        requests = []

        class Connection:
            def __init__(self, *args, **kwargs):
                pass

            def request(self, method, path, body, headers):
                self.path, self.headers = path, headers
                requests.append(path)

            def getresponse(self):
                status, body, headers = 200, b"Healthy", []
                if self.headers["Host"] == "evil.example":
                    status = 400
                elif self.path.endswith("/me"):
                    status = 401
                elif self.path.endswith("/sign-out"):
                    status = 403
                elif self.path.endswith("/config"):
                    body = json.dumps({"authMode": "Social", "providers": ["Microsoft", "Google"]}).encode()
                elif "/sign-in/" in self.path:
                    provider = self.path.rsplit("/", 1)[1]
                    query = {"redirect_uri": "https://pm.engcalchub.com/signin-" + provider.lower(),
                             "state": "synthetic-state", "code_challenge_method": "S256"}
                    authority = "login.microsoftonline.com" if provider == "Microsoft" else "accounts.google.com"
                    if change:
                        authority, query = change(authority, query)
                    status = 302
                    headers = [("Location", "https://" + authority + "/authorize?" + urlencode(query)),
                               ("Set-Cookie", "correlation=synthetic; Secure; HttpOnly; SameSite=None")]
                self.status, self.body, self.response_headers = status, body, headers
                return self

            def read(self):
                return self.body

            def getheaders(self):
                return self.response_headers

            def close(self):
                pass

        argv = ["verify", "--social"] + ([origin] if origin else [])
        output = io.StringIO()
        with patch.object(sys, "argv", argv), patch("http.client.HTTPConnection", Connection), \
             patch("http.client.HTTPSConnection", Connection), patch("builtins.open", side_effect=AssertionError("No password file read")), \
             contextlib.redirect_stdout(output), self.assertRaises(SystemExit) as ended:
            runpy.run_path(str(ROOT / "scripts/verify-homedev-review.py"), run_name="__main__")
        return ended.exception.code, output.getvalue(), requests

    def test_social_probes_use_both_providers_without_password_credentials(self):
        for origin in (None, "https://pm.engcalchub.com"):
            code, output, requests = self.probe(origin=origin)
            self.assertEqual(code, 0, output)
            self.assertIn("/api/v1/auth/social/sign-in/Microsoft", requests)
            self.assertIn("/api/v1/auth/social/sign-in/Google", requests)
            self.assertNotIn("/api/v1/auth/local/sign-in", requests)

    def test_wrong_authority_callback_or_pkce_fails(self):
        for change in (lambda a, q: ("evil.example", q),
                       lambda a, q: (a, {**q, "redirect_uri": "https://evil.example/callback"}),
                       lambda a, q: (a, {**q, "code_challenge_method": "plain"})):
            code, output, _ = self.probe(change)
            self.assertEqual(code, 1, output)

    def test_secret_in_redirect_is_rejected_without_logging_it(self):
        code, output, _ = self.probe(lambda a, q: (a, {**q, "client_secret": "synthetic-secret-never-log"}))
        self.assertEqual(code, 1, output)
        self.assertNotIn("synthetic-secret-never-log", output)

    def test_unexpected_origin_is_refused_before_any_request(self):
        code, _, requests = self.probe(origin="https://evil.example")
        self.assertEqual(code, "Refusing unexpected verification origin")
        self.assertEqual(requests, [])

    def test_activation_selects_social_override_and_verifier_only_when_requested(self):
        source = (ROOT / "scripts/activate-homedev-review.sh").read_text().split("\nlog() {", 1)[0]
        source += '\nprintf "%s\\n" "${compose[@]}" "${verify_args[@]}"\n'
        with tempfile.TemporaryDirectory() as name:
            base = Path(name)
            revision = "a" * 40
            release = base / "releases" / revision
            release.mkdir(parents=True)
            runtime = base / ".runtime"
            runtime.mkdir()
            (runtime / "review.env").touch()
            (release / ".runtime").symlink_to(runtime, target_is_directory=True)
            (release / "data").symlink_to(base, target_is_directory=True)

            def activate(*options):
                return subprocess.run(["bash", "-c", source, "activate", revision, *options], cwd=release, capture_output=True, text=True)

            missing = activate("--social-auth")
            self.assertNotEqual(missing.returncode, 0)
            self.assertIn("social-auth.env missing", missing.stderr)
            (runtime / "social-auth.env").touch()
            social = activate("--social-auth")
            self.assertEqual(social.returncode, 0, social.stderr)
            self.assertIn("hosting/social-auth.override.yml", social.stdout)
            self.assertIn("--social", social.stdout)
            original = activate()
            self.assertEqual(original.returncode, 0, original.stderr)
            self.assertNotIn("social-auth.override", original.stdout)
            self.assertNotIn("--social", original.stdout)
            self.assertNotEqual(activate("--unknown").returncode, 0)


if __name__ == "__main__":
    unittest.main()
