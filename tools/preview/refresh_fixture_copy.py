#!/usr/bin/env python3
"""Remove legacy fixture prefixes through the normal, versioned HTTP API.

The command is deliberately conservative: dry-run is the default, only the
three Tuesday fixture projects are considered, and a field is eligible only
when it still carries a known fixture marker (or one of the exact legacy
placeholders). Reviewer-written copy is therefore left alone.
"""
from __future__ import annotations

import argparse
import datetime as dt
import sys
from typing import Any

from fixture_api import FixtureError, connect
from fixture_copy import natural_copy

PROJECTS = ("SYN-101", "SYN-102", "SYN-103")
ACTOR = "jordan"
PEOPLE = ("jordan", "lena", "sam", "priya", "marc", "alex", "jill", "diane", "omar", "rita")
PREFIXES = ("Synthetic preview",)
EXACT = {
    "Synthetic preview task.",
    "Synthetic preview data for local UI review; not a real project.",
    "Synthetic, not a real client",
    "Synthetic preview fixture; fictional hours and work only.",
}
KNOWN_TARGETS = {
    "Synthetic preview: edited elsewhere to test the conflict state.": "Review the latest coordination comments before the next issue.",
    "Synthetic preview task.": "Prepare the assigned design work and coordinate its review.",
    "Synthetic preview: edited elsewhere to test conflict state.": "Review the latest coordination comments before the next issue.",
    "Synthetic preview milestone for local UI review.": "Coordinate the agreed design programme.",
    "Synthetic preview deliverable for local UI review.": "Prepare and review the design package.",
    "Synthetic preview decision for local UI review.": "Resolve the design question and record the agreed approach.",
    "Synthetic preview risk for local UI review.": "Track the design and programme impact.",
    "Synthetic preview issue for local UI review.": "Resolve the open design issue.",
    "Synthetic preview issue raised from a realised risk.": "Issue raised from a realised risk.",
    "Synthetic preview calendar entry for local UI review.": "Design coordination and scheduled work.",
    "Synthetic preview fixture; fictional hours and work only.": "Coordinate weekly work and capacity with the team.",
}
PROJECT_DESCRIPTIONS = {
    "SYN-101": "Road rehabilitation, stormwater separation and utility coordination.",
    "SYN-102": "Waterfront site servicing and drainage coordination.",
    "SYN-103": "Restricted study coordination and review.",
}


def _items(value: Any) -> list[dict[str, Any]]:
    if isinstance(value, dict):
        for key in ("items", "entries", "results"):
            if isinstance(value.get(key), list):
                return [x for x in value[key] if isinstance(x, dict)]
        return [value]
    return [x for x in value if isinstance(x, dict)] if isinstance(value, list) else []


def _eligible(raw: Any, target: str) -> bool:
    return isinstance(raw, str) and raw != target and (raw in EXACT or any(raw.startswith(p) for p in PREFIXES))


def _target(raw: str) -> str:
    return KNOWN_TARGETS.get(raw, natural_copy(raw))


def _version(row: dict[str, Any]) -> Any:
    return row.get("rowVersion", row.get("RowVersion"))


def _plan(report: list[dict[str, Any]], client: Any, path: str, actor: str, row: dict[str, Any], field: str, target: str, *, label: str) -> None:
    raw = row.get(field)
    if not _eligible(raw, target):
        return
    if _version(row) is None:
        report.append({"kind": "unsupported", "label": label, "reason": "missing rowVersion; skipped"})
        return
    report.append({"kind": "update", "label": label, "path": path, "actor": actor, "field": field, "from": raw, "to": target, "row": row})


def _apply(report: list[dict[str, Any]], client: Any) -> None:
    for entry in report:
        if entry["kind"] != "update":
            continue
        body = ({entry["field"]: entry["to"]} if entry.get("noVersion")
                else {"rowVersion": _version(entry["row"]), entry["field"]: entry["to"]})
        if entry.get("meetingBody"):
            body = {entry["field"]: entry["to"], **entry["meetingBody"]}
        # No retries: a conflict or permission failure is actionable evidence.
        if entry.get("requiresIfMatch"):
            call_with_headers = getattr(client, "call_with_headers", None)
            if call_with_headers is None:
                raise FixtureError(428, "meeting cleanup requires an If-Match transport helper")
            result = call_with_headers(entry["path"], entry["actor"], "PATCH", body,
                                       {"If-Match": f'"{_version(entry["row"])}"'})
        else:
            result = client.call(entry["path"], entry["actor"], "PATCH", body)
        if isinstance(result, dict) and result.get("rowVersion") is not None:
            entry["row"]["rowVersion"] = result["rowVersion"]


def refresh_fixture_copy(client: Any, *, apply: bool = False) -> list[dict[str, Any]]:
    report: list[dict[str, Any]] = []
    for number in PROJECTS:
        project = client.call(f"projects/{number}", ACTOR)
        if project.get("projectNumber", project.get("number")) != number:
            raise FixtureError(409, f"project identity mismatch for {number}")
        pid = project.get("id")
        if not pid:
            raise FixtureError(409, f"project {number} has no id")
        # Names are normalized in place so a long reviewer-facing label is preserved.
        # Exact legacy descriptions get the maintained natural target below.
        project_name = natural_copy(project.get("name"))
        project_description = PROJECT_DESCRIPTIONS[number]
        for field, target in (("name", project_name), ("description", project_description)):
            _plan(report, client, f"projects/{pid}", ACTOR, project, field, target, label=f"{number} project {field}")
        _plan(report, client, f"projects/{pid}", ACTOR, project, "clientReference", "Harbour Road design programme" if number == "SYN-101" else "Project client", label=f"{number} client reference")

        collections = (
            (f"projects/{pid}/tasks?pageSize=200", "tasks", "tasks", ("name", "description")),
            (f"projects/{pid}/milestones?showCompleted=true", "milestones", "milestones", ("name", "description")),
            (f"projects/{pid}/deliverables", "deliverables", "deliverables", ("name", "description")),
            (f"projects/{pid}/decisions", "decisions", "decisions", ("subject", "description")),
            (f"projects/{pid}/risks", "risks", "risks", ("title", "description", "mitigation", "triggerIndicator")),
            (f"projects/{pid}/issues", "issues", "issues", ("title", "description", "mitigation", "triggerIndicator")),
        )
        for list_path, kind, route_kind, fields in collections:
            for row in _items(client.call(list_path, ACTOR)):
                rid = row.get("id")
                if not rid:
                    continue
                detail = client.call(f"{route_kind}/{rid}", ACTOR)
                if isinstance(detail, dict):
                    nested_key = {"tasks": "task", "milestones": "milestone", "deliverables": "deliverable",
                                  "decisions": "decision", "risks": "risk", "issues": "issue"}.get(kind)
                    nested = detail.get(nested_key) if nested_key else None
                    # Detail endpoints wrap task/deliverable/milestone/risk/issue rows;
                    # retain outer descriptive fields (for example Task.Description).
                    row = {**row, **detail, **(nested if isinstance(nested, dict) else {})}
                for field in fields:
                    raw = row.get(field)
                    if not isinstance(raw, str) or not _eligible(raw, natural_copy(raw)):
                        continue
                    _plan(report, client, f"{route_kind}/{rid}", ACTOR, row, field, _target(raw), label=f"{number} {kind} {rid} {field}")
        meetings = _items(client.call(f"projects/{pid}/meetings", ACTOR))
        for meeting in meetings:
            mid = meeting.get("id")
            raw = meeting.get("title")
            if not mid or not isinstance(raw, str) or not _eligible(raw, _target(raw)):
                continue
            required = ("meetingDate", "meetingType", "notesLink", "calendarEventId")
            if _version(meeting) is None or any(key not in meeting for key in required):
                report.append({"kind": "unsupported", "label": f"{number} meeting {mid}", "reason": "complete meeting body or rowVersion unavailable; left unchanged"})
                continue
            report.append({"kind": "update", "label": f"{number} meeting {mid} title", "path": f"meetings/{mid}",
                          "actor": ACTOR, "field": "title", "from": raw, "to": _target(raw), "row": meeting,
                          "requiresIfMatch": True, "meetingBody": {key: meeting[key] for key in ("meetingDate", "meetingType", "notesLink", "calendarEventId")}})

        links = client.call(f"items/Project/{pid}/links", ACTOR)
        for link in _items(links.get("links", []) if isinstance(links, dict) else links):
            title = link.get("title")
            if link.get("id") and link.get("canChange") and isinstance(title, str) and _eligible(title, _target(title)):
                report.append({"kind": "update", "label": f"{number} project link {link['id']} title", "path": f"links/{link['id']}",
                              "actor": ACTOR, "field": "title", "from": title, "to": _target(title), "row": link, "noVersion": True})

        # Calendar has a bounded, project-scoped read API. Only event rows are editable;
        # deadline projections are immutable and are intentionally reported as skipped.
        today = dt.date.today()
        calendar = client.call(f"calendar?from={(today - dt.timedelta(days=46)).isoformat()}&to={(today + dt.timedelta(days=46)).isoformat()}&projectIds={pid}", ACTOR)
        for row in _items(calendar):
            if row.get("kind") != "event" or not row.get("id"):
                continue
            detail = client.call(f"calendar/events/{row['id']}", ACTOR)
            if isinstance(detail, dict):
                row = {**row, **detail}
            for field in ("title", "description"):
                raw = row.get(field)
                if isinstance(raw, str) and _eligible(raw, _target(raw)):
                    _plan(report, client, f"calendar/events/{row['id']}", ACTOR, row, field, _target(raw), label=f"{number} calendar {row['id']} {field}")

    for who in PEOPLE:
        for row in _items(client.call("planning/entries?mine=true&pageSize=200", who)):
            for field in ("label", "notes"):
                raw = row.get(field)
                if isinstance(raw, str) and _eligible(raw, _target(raw)):
                    _plan(report, client, f"planning/entries/{row['id']}", who, row, field, _target(raw), label=f"planning {row['id']} {field}")

    # Comments and document links are editable endpoints, but rewriting them would
    # require author/ownership selection and would alter reviewer-authored history or
    # source references. Source revisions, notifications and audit history are also
    # deliberately excluded from this display-copy migration.
    report.append({"kind": "unsupported", "label": "comments/source revisions/notifications/audit history", "reason": "comments are author/time-window governed (including the 48-hour edit limit); historical records intentionally preserved"})
    if apply:
        _apply(report, client)
    return report


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--apply", action="store_true", help="apply eligible PATCH requests; dry-run is the default")
    known, rest = parser.parse_known_args(argv)
    try:
        client = connect(rest)
        report = refresh_fixture_copy(client, apply=known.apply)
    except FixtureError as exc:
        print(f"fixture refresh refused ({exc.status}): {exc}", file=sys.stderr)
        return 2
    updates = sum(x["kind"] == "update" for x in report)
    skipped = sum(x["kind"] == "unsupported" for x in report)
    print(f"{'APPLIED' if known.apply else 'DRY-RUN'} fixture copy: {updates} eligible update(s), {skipped} protected/unsupported area(s)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
