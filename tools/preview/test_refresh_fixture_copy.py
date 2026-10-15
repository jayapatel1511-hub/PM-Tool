import copy
import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent))

from fixture_api import FixtureError
import refresh_fixture_copy as refresh


class FakeApi:
    def __init__(self):
        self.calls = []
        self.projects = {
            "SYN-101": {
                "id": "p101", "projectNumber": "SYN-101", "rowVersion": 3,
                "name": "Synthetic preview — Harbour Road Rehabilitation with a very long reviewer-facing label",
                "description": "Reviewer wording stays exactly as entered",
                "clientReference": "Synthetic, not a real client",
            },
            "SYN-102": {"id": "p102", "projectNumber": "SYN-102", "rowVersion": 1, "name": "Waterfront site servicing", "description": "Good description", "clientReference": "Client 2"},
            "SYN-103": {"id": "p103", "projectNumber": "SYN-103", "rowVersion": 1, "name": "Restricted client study", "description": "Good description", "clientReference": "Client 3"},
        }
        self.rows = {"tasks": [{"id": "t1", "rowVersion": 2, "name": "Reviewer task", "description": "Synthetic preview task."}],
                     "milestones": [], "deliverables": [], "decisions": [], "risks": [], "issues": [],
                     "meetings": [{"id": "m1", "rowVersion": 4, "title": "Synthetic preview — weekly review", "meetingDate": "2026-10-05", "meetingType": "Coordination", "notesLink": None, "calendarEventId": None}],
                     "actions": [{"id": "a1", "rowVersion": 2, "text": "Synthetic preview action: issue the meeting notes.", "dueDate": "2026-10-06", "ownerType": "User"}]}
        self.planning = {"id": "pl1", "rowVersion": 7, "label": "Synthetic preview — weekly planning", "notes": "Reviewer note"}
        self.fail_patch = False
        self.header_calls = []
        self.link_title = "Synthetic preview — source packet"

    def call(self, path, who, method="GET", body=None):
        self.calls.append((path, who, method, copy.deepcopy(body)))
        if method == "PATCH":
            if self.fail_patch:
                raise FixtureError(403, "forbidden")
            if path.startswith("projects/"):
                pid = path.split("/")[1]
                project = next(p for p in self.projects.values() if p["id"] == pid)
                project.update({k: v for k, v in body.items() if k != "rowVersion"})
                project["rowVersion"] += 1
            elif path == "tasks/t1":
                row = self.rows["tasks"][0]
                row.update({k: v for k, v in body.items() if k != "rowVersion"})
                row["rowVersion"] += 1
            elif path == "planning/entries/pl1":
                self.planning.update({k: v for k, v in body.items() if k != "rowVersion"})
                self.planning["rowVersion"] += 1
            elif path == "actions/a1":
                self.rows["actions"][0].update({k: v for k, v in body.items() if k != "rowVersion"})
                self.rows["actions"][0]["rowVersion"] += 1
            elif path == "links/l1":
                self.link_title = body["title"]
            return {"rowVersion": body.get("rowVersion", 0) + 1}
        if path.startswith("projects/") and path.count("/") == 1:
            return self.projects[path.split("/")[1]]
        if path == "tasks/t1":
            row = self.rows["tasks"][0]
            return {"task": {"id": row["id"], "rowVersion": row["rowVersion"], "name": row["name"]}, "description": row["description"]}
        if "/tasks" in path:
            return {"items": self.rows["tasks"] if path.startswith("projects/p101") else []}
        for kind in ("milestones", "deliverables", "decisions", "risks", "issues", "meetings"):
            if f"/{kind}" in path:
                return {"items": self.rows[kind]}
        if path.startswith("calendar?"):
            return {"entries": []}
        if path.startswith("items/Project/") and path.endswith("/links"):
            return {"links": [{"id": "l1", "title": self.link_title, "url": "https://docs.example.test/source", "canChange": True}], "inherited": []}
        if path.startswith("projects/") and path.endswith("/meetings"):
            return {"items": self.rows["meetings"]}
        if path.startswith("projects/") and path.endswith("/actions"):
            return {"items": self.rows["actions"]}
        if path.startswith("planning/entries"):
            return {"items": [self.planning] if who == "jordan" else []}
        raise AssertionError(f"unexpected GET {path}")

    def call_with_headers(self, path, who, method, body, headers):
        self.header_calls.append((path, who, method, copy.deepcopy(body), dict(headers)))
        if path == "meetings/m1":
            self.rows["meetings"][0].update(body)
        return {"rowVersion": 5}


class RefreshFixtureCopyTests(unittest.TestCase):
    def test_dry_run_never_mutates_and_preserves_reviewer_copy(self):
        api = FakeApi()
        report = refresh.refresh_fixture_copy(api)
        self.assertTrue(any(x["kind"] == "update" and x["field"] == "clientReference" for x in report))
        self.assertTrue(any(x["kind"] == "update" and x["field"] == "description" for x in report))
        self.assertFalse(any(call[2] == "PATCH" for call in api.calls))
        self.assertEqual(api.projects["SYN-101"]["description"], "Reviewer wording stays exactly as entered")
        project_update = next(x for x in report if x.get("kind") == "update" and x.get("field") == "name")
        self.assertEqual(project_update["to"], "Harbour Road Rehabilitation with a very long reviewer-facing label")

    def test_engineering_word_synthetic_is_not_a_fixture_marker(self):
        self.assertFalse(refresh._eligible("Synthetic fibre reinforcement", "fibre reinforcement"))

    def test_apply_is_versioned_and_idempotent(self):
        api = FakeApi()
        first = refresh.refresh_fixture_copy(api, apply=True)
        patches = [c for c in api.calls if c[2] == "PATCH"]
        self.assertGreaterEqual(len(patches), 3)
        for path, _who, _method, body in patches:
            if path != "links/l1":
                self.assertIn("rowVersion", body)
            self.assertTrue(set(body) - {"rowVersion"} <= {"name", "description", "clientReference", "label", "notes", "title", "meetingDate", "meetingType", "notesLink", "calendarEventId", "text"})
            self.assertNotIn("hoursPerWeek", body)
        api.calls.clear()
        second = refresh.refresh_fixture_copy(api)
        self.assertFalse(any(x["kind"] == "update" for x in second))
        self.assertFalse(any(c[2] == "PATCH" for c in api.calls))

    def test_permission_failure_propagates_without_retry(self):
        api = FakeApi()
        api.fail_patch = True
        with self.assertRaises(FixtureError) as caught:
            refresh.refresh_fixture_copy(api, apply=True)
        self.assertEqual(caught.exception.status, 403)
        self.assertEqual(sum(c[2] == "PATCH" for c in api.calls), 1)

    def test_meeting_uses_complete_body_and_if_match(self):
        api = FakeApi()
        report = refresh.refresh_fixture_copy(api, apply=True)
        meeting = next(x for x in report if x.get("requiresIfMatch"))
        call = api.header_calls[0]
        self.assertEqual(call[0], "meetings/m1")
        self.assertEqual(call[4], {"If-Match": '"4"'})
        self.assertEqual(set(call[3]), {"title", "meetingDate", "meetingType", "notesLink", "calendarEventId"})
        self.assertEqual(meeting["to"], "weekly review")

    def test_owned_link_changes_title_only(self):
        api = FakeApi()
        report = refresh.refresh_fixture_copy(api, apply=True)
        self.assertTrue(any(x.get("path") == "links/l1" for x in report))
        link_call = next(c for c in api.calls if c[0] == "links/l1")
        self.assertEqual(link_call[3], {"title": "source packet"})

    def test_action_cleanup_changes_text_and_version_only(self):
        api = FakeApi()
        refresh.refresh_fixture_copy(api, apply=True)
        action_call = next(c for c in api.calls if c[0] == "actions/a1")
        self.assertEqual(action_call[3], {"rowVersion": 2, "text": "issue the meeting notes."})

    def test_only_allowlisted_projects_are_read(self):
        api = FakeApi()
        refresh.refresh_fixture_copy(api)
        paths = [c[0] for c in api.calls]
        self.assertFalse(any("SYN-104" in p for p in paths))
        self.assertEqual({p for p in paths if p.startswith("projects/") and p.count("/") == 1}, {"projects/SYN-101", "projects/SYN-102", "projects/SYN-103"})


if __name__ == "__main__":
    unittest.main()
