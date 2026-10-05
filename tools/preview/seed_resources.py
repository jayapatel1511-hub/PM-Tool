"""Permissioned fictional Tuesday fixtures for the isolated local preview or hosted review.

Default: [base-url] verifies pm-tuesday-preview-db at 127.0.0.1:55433 and
Development authentication. Explicit hosted mode: --hosted-review --release SHA
--credentials PRIVATE_FILE verifies the exact prepared review image, review-only
database, LocalPassword authentication and ten individual accounts. It never
enables development authentication on the host. See fixture_api.py.

Every record in this recipe is invented. Jay requested normal visible copy;
fixture provenance is retained here and in the private deployment manifest.
Every write uses the API as the person who would make it, preserving validation,
permissions, audit and notifications. Dates follow the organisation's today.
Existing fixture markers are preserved; refused API steps fail the process.
SYN-101 already present means no resource writes; partial setup requires
inspection. The Admin enables restricted projects for the SYN-103 scenario.
"""
from fixture_api import connect, FixtureError
from fixture_copy import natural_copy
import datetime as dt
import sys
import uuid

client = connect()
BASE = client.base
NUMBERS = ("SYN-101", "SYN-102", "SYN-103")
refusals = []


class Refused(Exception):
    def __init__(self, status, text):
        super().__init__(text)
        self.status = status


def call(path, user, method="GET", body=None):
    try:
        return client.call(path, user, method, body)
    except FixtureError as e:
        raise Refused(e.status, str(e)) from None


def attempt(label, step):
    """Runs one permissioned step; a refusal is recorded as-is, never worked around."""
    try:
        return step()
    except Refused as e:
        refusals.append(f"{label}: {e}")


today = dt.date.fromisoformat(call("me", "priya")["settings"]["today"])
monday = today - dt.timedelta(days=today.weekday())


def day(week, weekday=0):
    """A date in the week `week` weeks after this one; weekday 0 is Monday, 4 is Friday."""
    return monday + dt.timedelta(days=7 * week + weekday)


def iso(d):
    return d.isoformat() if d else None


def summary():
    print(f"Synthetic preview data at {BASE} (organisation today {today}):")
    for number in NUMBERS:
        p = call(f"projects/{number}", "jordan")
        print(f"  {number} {p['status']}, {p['visibility']}, PM {p['pmName']}: {p['name']}")
        for a in call(f"projects/{p['id']}/allocations", "jordan")["items"]:
            print(f"    allocation {a['personName']} {a['fromDate']}..{a['throughDate']} {a['plannedHours']:g} h {a['status']}")
    print("  People (Admin view; committed/available hours for this week, next week and the week after):")
    shown = {"alex", "jill", "marc", "omar", "diane"}
    for p in call("workload", "jordan")["people"]:
        if p["displayName"].split()[0].lower() in shown:
            weeks = ", ".join(f"{c['committed']:g}/{c['available']:g}" for c in p["cells"][:3])
            print(f"    {p['displayName']}: capacity {p['capacity']:g} h{' (custom)' if p['capacityOverride'] else ''}; {weeks}; "
                  f"{p['openTasks']} open, {p['unestimated']} unestimated, {p['overdue']} overdue; {p['indicator']}")
    for r in refusals:
        print(f"  REFUSED {r}")


try:
    call("projects/SYN-101", "jordan")
    print("SYN-101 already exists; nothing created.")
    summary()
    sys.exit()
except Refused as e:
    if e.status != 404:
        raise

uid = {u["email"].split("@")[0]: u["id"] for u in call("users?limit=500", "jordan")}
ref = call("reference", "jordan")
disc = {d["code"]: d["id"] for d in ref["disciplines"]}
client = next(c["id"] for c in ref["clients"] if c["isActive"])
office = next(o["id"] for o in ref["offices"] if o["isActive"])
call("admin/settings/restricted_projects_enabled", "jordan", "PUT", {"value": True})


def project(pm, number, name, members, leads, restricted=False):
    """Created by its PM with the given members and discipline leads, then made Active; returns (id, discipline ids by code)."""
    pid = call("projects", pm, "POST", {
        "projectNumber": number, "name": name, "clientId": client, "officeId": office,
        "clientReference": 'Harbour Road design programme', "description": 'Road rehabilitation, stormwater separation and utility coordination.',
        "startDate": iso(today - dt.timedelta(days=30)), "targetCompletionDate": iso(today + dt.timedelta(days=120)),
        "disciplines": [{"disciplineId": disc[code], "leadUserId": uid[lead] if lead else None} for code, lead in leads.items()],
        "members": [{"userId": uid[who], "roles": [role], "disciplineId": disc.get(code)} for who, role, code in members]})["id"]
    if restricted:
        call(f"projects/{pid}", pm, "PATCH", {"visibility": "Restricted", "rowVersion": call(f"projects/{pid}", pm)["rowVersion"]})
    p = call(f"projects/{pid}", pm)
    call(f"projects/{pid}/transition", pm, "POST", {"toStatus": "Active", "rowVersion": p["rowVersion"]})
    return pid, {d["code"]: d["id"] for d in p["disciplines"]}


p1, d1 = project("priya", "SYN-101", "Harbour Road Rehabilitation, Stormwater Separation and Utility Relocation (Phase 2 detailed design)",
                 [("alex", "TeamMember", "CIV"), ("jill", "TeamMember", "CIV"), ("omar", "TeamMember", "GEO"), ("diane", "Reviewer", None)],
                 {"CIV": "marc", "GEO": "omar"})
p2, d2 = project("marc", "SYN-102", "Waterfront site servicing", [("alex", "TeamMember", "CIV"), ("jill", "TeamMember", "CIV")], {"CIV": "marc"})
p3, d3 = project("marc", "SYN-103", "Restricted client study", [("jill", "TeamMember", "CIV")], {"CIV": "marc"}, restricted=True)


def task(pid, pd, pm, who, name, est, start=None, due=None, progress=0, code="CIV"):
    t = call(f"projects/{pid}/tasks", pm, "POST", {"name": name, "description": 'Prepare the assigned design work and coordinate its review.', "projectDisciplineId": pd[code],
                                                   "assigneeId": uid[who], "estimatedHours": est, "startDate": iso(start), "dueDate": iso(due)})
    if progress:
        call(f"tasks/{t['id']}", pm, "PATCH", {"progressPct": progress, "rowVersion": t["rowVersion"]})
    return t["id"]


week_end = max(today, day(0, 4))  # this Friday, or today at a weekend, so the hours land in the current week
# Alex: over-assigned this week (overdue + due-this-week work) and next week (confirmed reservation + SYN-102 work).
task(p1, d1, "priya", "alex", "Survey base plan QA and topographic gap list", 20, today - dt.timedelta(days=10), today - dt.timedelta(days=3), 40)
task(p2, d2, "marc", "alex", "Existing services review for the waterfront lots", 50, today, week_end, 20)
a3 = task(p1, d1, "priya", "alex", "Harbour Road Phase 2 — storm sewer and sanitary profiles, utility relocation conflict resolution "
                                   "and sheet set coordination", 24, day(1), day(1, 4))
a4 = task(p2, d2, "marc", "alex", "Site grading and servicing plan, 60 % submission", 40, day(1), day(2, 4))
task(p1, d1, "priya", "alex", "Municipal design standards check", 8)
a6 = task(p1, d1, "priya", "alex", "60 % drawing set markups and redline pickup", 12, day(2), day(2, 4))
# Jill: a moderate load on a 30 h week, part of it on the restricted project.
task(p1, d1, "priya", "jill", "Drainage area plan and pre/post-development runoff summary", 20, today, week_end)
j2 = task(p1, d1, "priya", "jill", "Storm sewer design sheets and HGL check", 24, day(1), day(2, 4))
task(p2, d2, "marc", "jill", "Watermain and hydrant layout", 30, day(1), day(3, 4))
task(p3, d3, "marc", "jill", "Options memo inputs for the restricted study", 8, day(1), day(2, 4))
j5 = task(p1, d1, "priya", "jill", "Utility relocation conflict log update", 12, day(3), day(3, 4))
# Omar: two unestimated tasks and one small estimate. Marc and Diane get no tasks.
task(p1, d1, "priya", "omar", "Borehole log interpretation and groundwater summary", None, day(1), day(2, 4), code="GEO")
task(p1, d1, "priya", "omar", "Pavement structure and trench backfill recommendations", None, day(2), day(3, 4), code="GEO")
o3 = task(p1, d1, "priya", "omar", "Trench support assumptions for the utility relocation", 6, day(1), day(1, 4), code="GEO")

attempt("Jill's weekly capacity 30 h, by Sam", lambda: call(f"users/{uid['jill']}/capacity", "sam", "PUT", {"hours": 30}))
attempt("Alex unavailable next Wednesday, by Sam", lambda: call(f"users/{uid['alex']}/availability/{iso(day(1, 2))}", "sam", "PUT",
                                                                {"expectedRowVersion": 0, "availableHours": 0, "category": "Unavailable"}))


def propose(pid, pm, who, week, hours, task_id):
    """A Production allocation for Monday to Friday of `week`, linked to the task on each of those days."""
    days = [day(week, i) for i in range(5)]
    return call(f"projects/{pid}/allocations", pm, "POST", {
        "requestId": str(uuid.uuid4()), "personId": uid[who], "purpose": "Production", "fromDate": iso(days[0]), "throughDate": iso(days[-1]),
        "plannedHours": hours, "days": [], "links": [{"workType": "Task", "workId": task_id, "workDate": iso(d)} for d in days]})


def confirm(pid, a, supervisor):
    pv = call(f"projects/{pid}/allocations/{a['id']}/confirmation-preview", supervisor)
    over = [d["date"] for d in pv["days"] if d["overByHours"] > 0]
    return call(f"projects/{pid}/allocations/{a['id']}/confirm", supervisor, "POST", {
        "requestId": str(uuid.uuid4()), "rowVersion": pv["rowVersion"],
        "dateVersions": [{"workDate": d["date"], "rowVersion": d["dateVersion"]} for d in pv["days"]],
        "overCapacityReason": f"Accepted over capacity on {', '.join(over)}." if over else None})


def finish(pid, a, user, verb, reason):
    return call(f"projects/{pid}/allocations/{a['id']}/{verb}", user, "POST",
                {"requestId": str(uuid.uuid4()), "rowVersion": a["rowVersion"], "reason": f"{reason}"})


attempt("SYN-101 Alex 30 h next week, proposed by Priya, confirmed by Sam", lambda: confirm(p1, propose(p1, "priya", "alex", 1, 30, a3), "sam"))
attempt("SYN-101 Jill 16 h next week, proposed by Priya", lambda: propose(p1, "priya", "jill", 1, 16, j2))
attempt("SYN-101 Alex 12 h in two weeks, proposed by Priya, declined by Sam",
        lambda: finish(p1, propose(p1, "priya", "alex", 2, 12, a6), "sam", "decline", "Alex is already fully committed that week."))
attempt("SYN-101 Jill 8 h in three weeks, proposed and cancelled by Priya",
        lambda: finish(p1, propose(p1, "priya", "jill", 3, 8, j5), "priya", "cancel", "the conflict log moved to the next phase."))
attempt("SYN-101 Omar 6 h next week, proposed by Priya, confirmed by Lena, completed by Priya",
        lambda: finish(p1, confirm(p1, propose(p1, "priya", "omar", 1, 6, o3), "lena"), "priya", "complete", "trench support note delivered."))
attempt("SYN-102 Alex 20 h in two weeks, proposed by Marc", lambda: propose(p2, "marc", "alex", 2, 20, a4))
summary()

if refusals:
    raise SystemExit("Fixture setup stopped with refused API steps; review the reported failures before continuing.")
