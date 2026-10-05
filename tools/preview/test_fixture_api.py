import json
import os
from pathlib import Path
import sys
import tempfile
import unittest
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).parent))
import fixture_api


SHA = "0123456789abcdef0123456789abcdef01234567"


def inspected(*, image=None, env=None, volume="pm-tool-review-db"):
    api = {
        "Config": {
            "Image": image or f"pm-tool-review:{SHA}",
            "Labels": {"com.docker.compose.project": "pm-tool-review", "com.docker.compose.service": "api"},
            "Env": env or [
                "ASPNETCORE_ENVIRONMENT=Staging", "Auth__Mode=LocalPassword", "Seed__ReviewDemo=true",
                "Seed__DevUsers=false", "Hosting__LocalTunnelProxy=true",
                "ConnectionStrings__Hub=Host=db;Port=5432;Database=hub_review;Username=hub_review;Password=secret",
            ],
        },
        "Name": "/pm-tool-review-api-1",
        "NetworkSettings": {"Networks": {"pm-tool-review": {}}, "Ports": {"8080/tcp": [{"HostIp": "127.0.0.1", "HostPort": "3080"}]}},
    }
    db = {
        "Config": {"Labels": {"com.docker.compose.project": "pm-tool-review", "com.docker.compose.service": "db"}},
        "Mounts": [{"Destination": "/var/lib/postgresql/data", "Name": volume}],
        "Name": "/pm-tool-review-db-1",
        "NetworkSettings": {"Networks": {"pm-tool-review": {}}},
    }
    return [api, db]


class Response:
    def __init__(self, status, body=b"", headers=()):
        self.status = status
        self._body = body
        self._headers = list(headers)

    def read(self):
        return self._body

    def getheaders(self):
        return self._headers


class FakeConnection:
    requests = []
    me_email = None

    def __init__(self, host, port, timeout):
        self.request_data = None

    def request(self, method, path, body, headers):
        self.request_data = (method, path, body, headers)
        FakeConnection.requests.append(self.request_data)

    def getresponse(self):
        method, path, body, headers = self.request_data
        if path.endswith("/config"):
            return Response(200, b'{"authMode":"LocalPassword"}')
        if path.endswith("/auth/local/sign-in"):
            who = json.loads(body)["userName"]
            return Response(204, headers=[("Set-Cookie", f"review-{who}=opaque; Path=/; HttpOnly")])
        if path.endswith("/me"):
            cookie = headers.get("Cookie", "")
            who = cookie.removeprefix("review-").split("=", 1)[0]
            email = FakeConnection.me_email or f"{who}@hub.test"
            return Response(200, json.dumps({"email": email}).encode())
        return Response(201, b"{}")

    def close(self):
        pass


class FixtureApiTests(unittest.TestCase):
    def setUp(self):
        FakeConnection.requests = []
        FakeConnection.me_email = None

    def private_credentials(self):
        directory = tempfile.TemporaryDirectory()
        root = Path(directory.name)
        root.chmod(0o700)
        path = root / "review-login-handoff.json"
        path.write_text(json.dumps({"credentials": [{"login": who, "password": f"pw-{who}"} for who in fixture_api.PERSONAS]}))
        path.chmod(0o600)
        return directory, path

    def hosted_patches(self, inspect_value=None):
        return (
            patch.object(fixture_api.Path, "cwd", return_value=Path("/srv/releases") / SHA),
            patch.object(fixture_api.subprocess, "check_output", return_value=json.dumps(inspect_value or inspected())),
            patch.object(fixture_api.http.client, "HTTPConnection", FakeConnection),
            patch.object(fixture_api.time, "sleep"),
        )

    def test_hosted_logs_in_each_persona_and_never_sends_dev_header(self):
        directory, handoff = self.private_credentials()
        try:
            patches = self.hosted_patches()
            with patches[0], patches[1], patches[2], patches[3]:
                api = fixture_api.connect(["--hosted-review", "--credentials", str(handoff), "--release", SHA])
                self.assertEqual(api.base, fixture_api.HOSTED_BASE)
                self.assertEqual(set(api._cookies), set(fixture_api.PERSONAS))
                api.call("projects", "jordan", "POST", {"name": "synthetic"})
            signins = [r for r in FakeConnection.requests if r[1].endswith("/auth/local/sign-in")]
            self.assertEqual(len(signins), 10)
            write = FakeConnection.requests[-1]
            self.assertEqual(write[3]["Host"], fixture_api.PUBLIC_HOST)
            self.assertEqual(write[3]["X-Forwarded-Proto"], "https")
            self.assertEqual(write[3]["Origin"], fixture_api.PUBLIC_ORIGIN)
            self.assertNotIn("X-Dev-User", write[3])
        finally:
            directory.cleanup()

    def test_meeting_if_match_preserves_auth_headers(self):
        api = fixture_api.FixtureApi(fixture_api.HOSTED_BASE, True, {"jordan": "opaque"}, connection_factory=FakeConnection)
        api.call_with_headers("meetings/123", "jordan", "PATCH", {"title": "Coordination", "meetingDate": "2026-10-04"}, {"If-Match": '"4"'})
        request = FakeConnection.requests[-1]
        self.assertEqual(request[3]["If-Match"], '"4"')
        self.assertEqual(request[3]["Cookie"], "opaque")
        self.assertNotIn("X-Dev-User", request[3])
        with self.assertRaises(fixture_api.FixtureError):
            api.call_with_headers("meetings/123", "jordan", "PATCH", {}, {"Host": "elsewhere"})

    def test_hosted_rejects_alternate_origin_and_unsafe_path(self):
        with self.assertRaises(fixture_api.FixtureError):
            fixture_api.connect(["http://evil.example", "--hosted-review", "--credentials", "x", "--release", SHA])
        api = fixture_api.FixtureApi(fixture_api.HOSTED_BASE, True, {"jordan": "opaque"})
        with self.assertRaises(fixture_api.FixtureError):
            api.call("../projects", "jordan")
        with self.assertRaises(fixture_api.FixtureError):
            api.call("https://evil.example/projects", "jordan")
        with self.assertRaises(fixture_api.FixtureError):
            api.call("%252fprojects", "jordan")
        with self.assertRaises(fixture_api.FixtureError):
            api.call("projects/%252e%252e/me", "jordan")

    def test_hosted_rejects_wrong_image_environment_and_pilot_volume(self):
        directory, handoff = self.private_credentials()
        try:
            cases = [
                inspected(image=f"pm-tool-pilot:{SHA}"),
                inspected(env=["ASPNETCORE_ENVIRONMENT=Staging", "Auth__Mode=Development", "Seed__ReviewDemo=true", "Seed__DevUsers=false", "Hosting__LocalTunnelProxy=true"]),
                inspected(volume="pm-tool-pilot-db"),
                inspected(env=["ASPNETCORE_ENVIRONMENT=Staging", "Auth__Mode=LocalPassword", "Seed__ReviewDemo=true", "Seed__DevUsers=false", "Hosting__LocalTunnelProxy=true", "ConnectionStrings__Hub=Host=pm-tool-pilot-db;Database=hub_review;Username=hub_review"]),
                inspected(env=["ASPNETCORE_ENVIRONMENT=Staging", "Auth__Mode=LocalPassword", "Seed__ReviewDemo=true", "Seed__DevUsers=false", "Hosting__LocalTunnelProxy=true", "ConnectionStrings__Hub=Host=db;Database=hub_review;Username=hub_review", "ConnectionStrings__Hub=Host=db;Database=hub_pilot;Username=hub_review"]),
            ]
            for value in cases:
                with self.subTest(value=value):
                    patches = self.hosted_patches(value)
                    with patches[0], patches[1]:
                        with self.assertRaises(fixture_api.FixtureError):
                            fixture_api.connect(["--hosted-review", "--credentials", str(handoff), "--release", SHA])
        finally:
            directory.cleanup()

    def test_connection_target_rejects_aliases_that_could_override_review_database(self):
        base = "Host=db;Port=5432;Database=hub_review;Username=hub_review;Password=private"
        fixture_api._connection_target(base)
        for suffix in (";Server=pm-tool-pilot", ";Initial Catalog=hub_pilot", ";User ID=pilot", ";Host=pilot", ";Search Path=pilot"):
            with self.subTest(suffix=suffix), self.assertRaises(ValueError):
                fixture_api._connection_target(base + suffix)

    def test_hosted_credentials_must_be_private_regular_file(self):
        directory, handoff = self.private_credentials()
        try:
            handoff.chmod(0o644)
            with self.assertRaises(fixture_api.FixtureError):
                fixture_api._read_private_credentials(str(handoff))
            handoff.chmod(0o600)
            link = handoff.with_name("link.json")
            link.symlink_to(handoff)
            with self.assertRaises(fixture_api.FixtureError):
                fixture_api._read_private_credentials(str(link))
        finally:
            directory.cleanup()

    def test_hosted_credentials_reject_wrong_uid_and_arbitrary_symlink_ancestor_but_allow_release_runtime_link(self):
        directory, handoff = self.private_credentials()
        try:
            with patch.object(fixture_api.os, "getuid", return_value=os.getuid() + 1):
                with self.assertRaises(fixture_api.FixtureError):
                    fixture_api._read_private_credentials(str(handoff))

            target = Path(directory.name) / "shared-runtime"
            target.mkdir(mode=0o700)
            target_file = target / handoff.name
            target_file.write_text(handoff.read_text())
            target_file.chmod(0o600)
            arbitrary = Path(directory.name) / "arbitrary-link"
            arbitrary.symlink_to(target, target_is_directory=True)
            with self.assertRaises(fixture_api.FixtureError):
                fixture_api._read_private_credentials(str(arbitrary / handoff.name))

            release = Path(directory.name) / SHA
            release.mkdir(mode=0o700)
            runtime = release / ".runtime"
            runtime.symlink_to(target, target_is_directory=True)
            with patch.object(fixture_api.Path, "cwd", return_value=release):
                self.assertEqual(fixture_api.PERSONA_SET, set(fixture_api._read_private_credentials(str(runtime / handoff.name))))
        finally:
            directory.cleanup()

    def test_hosted_rejects_wrong_network_name_and_non_loopback_api_port(self):
        directory, handoff = self.private_credentials()
        try:
            cases = [inspected()]
            cases[0][0]["NetworkSettings"]["Ports"] = {"0.0.0.0:3080/tcp": [{"HostIp": "0.0.0.0", "HostPort": "3080"}]}
            cases.append(inspected())
            cases[1][0]["NetworkSettings"]["Networks"] = {"pm-tool-pilot": {}}
            for value in cases:
                patches = self.hosted_patches(value)
                with patches[0], patches[1]:
                    with self.assertRaises(fixture_api.FixtureError):
                        fixture_api.connect(["--hosted-review", "--credentials", str(handoff), "--release", SHA])
        finally:
            directory.cleanup()

    def test_hosted_fails_when_me_identity_does_not_match_persona(self):
        directory, handoff = self.private_credentials()
        try:
            FakeConnection.me_email = "rita@hub.test"
            patches = self.hosted_patches()
            with patches[0], patches[1], patches[2], patches[3]:
                with self.assertRaises(fixture_api.FixtureError) as error:
                    fixture_api.connect(["--hosted-review", "--credentials", str(handoff), "--release", SHA])
            self.assertNotIn("pw-", str(error.exception))
            self.assertNotIn("opaque", str(error.exception))
        finally:
            directory.cleanup()

    def test_preview_target_guard_and_development_config_remain_mandatory(self):
        with patch.object(fixture_api, "verify_preview_target") as verify, \
             patch.object(fixture_api.http.client, "HTTPConnection", FakeConnection):
            FakeConnection.requests = []
            # FakeConnection is local-mode aware for this assertion.
            original = FakeConnection.getresponse
            FakeConnection.getresponse = lambda self: Response(200, b'{"authMode":"Development"}')
            try:
                api = fixture_api.connect([])
            finally:
                FakeConnection.getresponse = original
            verify.assert_called_once_with(fixture_api.PREVIEW_BASE)
            self.assertEqual(api.base, fixture_api.PREVIEW_BASE)
            with self.assertRaises(fixture_api.FixtureError):
                fixture_api._safe_path("/api/v1/me")


if __name__ == "__main__":
    unittest.main()
