import contextlib
import io
import itertools
import runpy
import sys
import types
import unittest
from pathlib import Path

ROOT = Path(__file__).parent
sys.path.insert(0, str(ROOT))
PEOPLE = ("jordan", "lena", "sam", "priya", "marc", "alex", "jill", "diane", "omar", "rita")
PROFILES = ("Harbour Road Phase 2 — storm sewer and sanitary profiles, utility relocation conflict resolution "
            "and sheet set coordination")
WRITES = {"POST", "PUT", "PATCH", "DELETE"}


class FakeFixtureError(Exception):
    def __init__(self, status, text):
        super().__init__(text)
        self.status = status


class FakeFixtureApi:
    """Answers every call seed_staffing.py makes with the smallest plausible payload and records it."""
    base = "http://fixture.test"

    def __init__(self, today="2026-10-12", present=False):
        self.today, self.calls, self.ids = today, [], itertools.count(1)
        self.projects = {"SYN-101": "p1", "SYN-103": "p3"}
        self.tasks = {"p1": [{"id": "profiles", "name": PROFILES}]}
        self.entries = []
        if present:
            self.projects.update({"SYN-104": "p4", "SYN-105": "p5"})
            self.tasks["p1"] += [{"id": "t21a", "name": "Utility conflict log"}, {"id": "hold", "name": "Culvert crossing options"}]
            self.entries.append({"label": "Proposal support", "startWeek": "2026-10-19"})

    def writes(self):
        return [c for c in self.calls if c[2] in WRITES]

    def call(self, path, who, method="GET", body=None):
        self.calls.append((path, who, method, body))
        parts = path.split("?")[0].split("/")
        if method == "GET":
            if path == "me":
                return {"settings": {"today": self.today, "workingDaysEnabled": False}}
            if path == "admin/users":
                return [{"email": f"{n}@hub.test", "id": f"{n}-id", "rowVersion": 3, "homeDisciplineId": None} for n in PEOPLE]
            if path == "reference":
                return {"disciplines": [{"code": "CIV", "id": "civ"}, {"code": "GEO", "id": "geo"}],
                        "deliverableTypes": [{"name": n, "id": n} for n in ("Report", "Memo", "Permit Submission")],
                        "clients": [{"id": "client", "isActive": True}], "offices": [{"id": "head-office", "isActive": True}]}
            if parts[0] == "projects" and len(parts) == 2:
                pid = self.projects.get(parts[1], parts[1] if parts[1] in self.projects.values() else None)
                if pid is None:
                    raise FakeFixtureError(404, "not found")
                return {"id": pid, "status": "Active", "rowVersion": 1, "disciplines": [{"code": "CIV", "id": f"{pid}-civ"}, {"code": "GEO", "id": f"{pid}-geo"}]}
            if parts[0] == "projects" and parts[2] == "tasks":
                return {"items": self.tasks.get(parts[1], [])}
            if parts[0] == "projects" and parts[2] == "open-roles":
                return []
            if parts[-1] == "confirmation-preview":
                return {"rowVersion": 0, "days": [{"date": "2026-10-19", "overByHours": 2, "dateVersion": 0}]}
            if path.startswith("planning/entries"):
                return {"items": self.entries}
            if path in ("admin/offices",) or path.startswith("admin/holidays"):
                return []
            if path == "templates":
                return {"templates": []}
            if parts[0] == "templates":
                return {"id": parts[1], "rowVersion": 2}
            if parts[0] == "tasks" and parts[2] == "dependencies":
                return {"dependsOn": [], "blocks": []}
            raise AssertionError(f"unexpected fixture call: {method} {path}")
        new = {"id": f"id-{next(self.ids)}", "rowVersion": 1, "status": "Open"}
        if path == "projects":
            self.projects[body["projectNumber"]] = new["id"]
        elif parts[0] == "projects" and parts[-1] == "tasks":
            self.tasks.setdefault(parts[1], []).append({"id": new["id"], "name": body["name"], "status": "Not Started"})
        return new


def run(api):
    fixture_api = types.ModuleType("fixture_api")
    fixture_api.connect = lambda: api
    fixture_api.FixtureError = FakeFixtureError
    old = sys.modules.get("fixture_api")
    sys.modules["fixture_api"] = fixture_api
    try:
        with contextlib.redirect_stdout(io.StringIO()):
            runpy.run_path(str(ROOT / "seed_staffing.py"), run_name="__main__")
    finally:
        if old is None:
            sys.modules.pop("fixture_api", None)
        else:
            sys.modules["fixture_api"] = old


class SeedStaffingTests(unittest.TestCase):
    def setUp(self):
        self.sleep = __import__("time").sleep
        __import__("time").sleep = lambda _: None

    def tearDown(self):
        __import__("time").sleep = self.sleep

    def test_first_run_seeds_section_8_as_the_acting_people(self):
        api = FakeFixtureApi()
        run(api)  # a refused step would raise SystemExit
        writes = api.writes()
        by = lambda path, method="POST": [(who, body) for p, who, m, body in writes if p == path and m == method]
        projects = {body["projectNumber"]: (who, body) for who, body in by("projects")}
        self.assertEqual(projects["SYN-104"][1]["name"], "Lakeside subdivision servicing (proposal)")
        self.assertEqual({who for who, _ in projects.values()}, {"priya"})
        self.assertFalse(by(f"projects/{api.projects['SYN-104']}/transition"), "SYN-104 stays in Setup")
        self.assertTrue(by(f"projects/{api.projects['SYN-105']}/transition"))
        template = next(who for who, body in by("templates"))
        structure = next(body for p, who, m, body in writes if p.endswith("/structure"))
        self.assertEqual((template, structure["milestones"][0]["name"], structure["milestones"][0]["anchor"]), ("jordan", "Client workshop", "None"))
        self.assertTrue(projects["SYN-105"][1]["templateId"], "the undated milestone comes from a template")
        self.assertFalse([body for p, who, m, body in writes if p == f"projects/{api.projects['SYN-105']}/milestones"])
        roles = {body["roleLabel"]: (who, body) for p, who, m, body in writes if p.endswith("/open-roles")}
        self.assertEqual((roles["Civil technologist"][1]["fromDate"], roles["Civil technologist"][1]["throughDate"],
                          roles["Civil technologist"][1]["plannedHours"]), ("2026-10-26", "2026-11-20", 80))
        self.assertEqual(roles["Hydraulic modeller"][0], "marc")
        self.assertEqual(sorted(roles), ["Civil technologist", "Drafting support", "Geotechnical engineer", "Hydraulic modeller", "Senior reviewer"])
        fills = [(who, body["personId"]) for p, who, m, body in writes if p.endswith("/allocations") and body.get("openRoleId")]
        self.assertEqual(fills, [("priya", "omar-id"), ("priya", "jill-id")])
        self.assertTrue(any(p.endswith("/decline") and who == "sam" for p, who, m, _ in writes))
        self.assertEqual([body for _, body in by("admin/holidays")][0]["date"], "2026-10-14")  # Monday + 2 working days
        self.assertIsNotNone([body for _, body in by("admin/holidays")][0]["officeId"])
        links = [(body["type"], body["lagDays"]) for p, who, m, body in writes if p.endswith("/dependencies")]
        self.assertEqual(links, [("FinishToStart", 0), ("FinishToStart", 2), ("StartToStart", 0), ("StartToStart", 2),
                                 ("FinishToFinish", 0), ("FinishToFinish", 1)])
        entries = {who: body for who, body in by("planning/entries")}
        self.assertEqual((entries["sam"]["hoursPerWeek"], entries["sam"]["visibility"], entries["sam"]["startWeek"]), (8, "Published", "2026-10-19"))
        self.assertEqual((entries["jordan"]["hoursPerWeek"], entries["jordan"]["visibility"]), (10, "Draft"))
        self.assertEqual({p.split("/")[3] for p, who, m, _ in writes if "/availability/" in p and who == "sam"}, {"2026-10-19", "2026-10-20"})
        self.assertEqual({p: body["homeDisciplineId"] for p, who, m, body in writes if p.startswith("admin/users/")},
                         {"admin/users/alex-id": "civ", "admin/users/jill-id": "civ", "admin/users/omar-id": "geo"})
        self.assertEqual({who for p, who, m, _ in writes if p.startswith("admin/")}, {"jordan"})
        starts = [who for p, who, m, body in writes if p.endswith("/transition") and body.get("acknowledgeReadiness")]
        self.assertNotIn("priya", starts, "assignees start their own work after the PM's authorisation")

    def test_present_markers_mean_no_writes(self):
        api = FakeFixtureApi(present=True)
        run(api)
        self.assertEqual(api.writes(), [])

    def test_holiday_and_working_days_skip_weekends(self):
        api = FakeFixtureApi(today="2026-10-15")  # a Thursday: the holiday is Monday, the next working day after it Tuesday
        run(api)
        holiday = next(body for p, _, m, body in api.writes() if p == "admin/holidays")
        self.assertEqual(holiday["date"], "2026-10-19")
        design = next(body for p, _, m, body in api.writes() if p.endswith("/tasks") and body["name"] == "Grading design")
        self.assertEqual(design["startDate"], "2026-10-20")


if __name__ == "__main__":
    unittest.main()
