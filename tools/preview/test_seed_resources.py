import datetime as dt
import runpy
import sys
import types
import unittest
from pathlib import Path

ROOT = Path(__file__).parent
sys.path.insert(0, str(ROOT))


class StopAfterFirstProject(Exception):
    pass


class FakeFixtureApi:
    base = "http://fixture.test"

    def __init__(self):
        self.calls = []
        self.settings_put = None
        self.project_body = None

    def call(self, path, who, method="GET", body=None):
        self.calls.append((path, who, method, body))
        if path == "projects/SYN-101":
            raise FakeFixtureError(404, "not found")
        if path == "me":
            return {"settings": {"today": "2026-10-05"}}
        if path.startswith("users"):
            return [{"email": f"{name}@hub.test", "id": f"{name}-id"} for name in
                    ("jordan", "lena", "sam", "priya", "marc", "alex", "jill", "diane", "omar", "rita")]
        if path == "reference":
            return {"disciplines": [{"code": "CIV", "id": "civ-id"}, {"code": "GEO", "id": "geo-id"}],
                    "clients": [{"id": "client-id", "isActive": True}],
                    "offices": [{"id": "office-id", "isActive": True}]}
        if path == "admin/settings/restricted_projects_enabled" and method == "PUT":
            self.settings_put = body
            return {}
        if path == "projects" and method == "POST":
            self.project_body = body
            raise StopAfterFirstProject()
        raise AssertionError(f"unexpected fixture call: {method} {path}")


class FakeFixtureError(Exception):
    def __init__(self, status, text):
        super().__init__(text)
        self.status = status


class SeedResourcesTests(unittest.TestCase):
    def test_first_run_keeps_api_client_when_reference_has_client_id(self):
        api = FakeFixtureApi()
        fixture_api = types.ModuleType("fixture_api")
        fixture_api.connect = lambda: api
        fixture_api.FixtureError = FakeFixtureError
        old = sys.modules.get("fixture_api")
        sys.modules["fixture_api"] = fixture_api
        try:
            with self.assertRaises(StopAfterFirstProject):
                runpy.run_path(str(ROOT / "seed_resources.py"), run_name="__main__")
        finally:
            if old is None:
                sys.modules.pop("fixture_api", None)
            else:
                sys.modules["fixture_api"] = old
        self.assertEqual(api.settings_put, {"value": True})
        self.assertEqual(api.project_body["clientId"], "client-id")
        self.assertEqual(api.project_body["officeId"], "office-id")
        self.assertFalse(any(method == "PATCH" for _, _, method, _ in api.calls))


if __name__ == "__main__":
    unittest.main()
