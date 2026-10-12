"""Packet 036 fixtures (verification.md section 8) for the isolated local preview or hosted review.

Default: [base-url] verifies pm-tuesday-preview-db at 127.0.0.1:55433 and
Development authentication; --hosted-review is the guarded review transport.
See fixture_api.py. Run seed_resources.py first.

Every record is invented. Every write uses the API as the person who would make
it, so validation, permissions, audit and notifications apply. Dates follow the
organisation's today. SYN-105 is the relative-date browser version of the
section 8.2 worked example: everything happens today and the holiday falls two
working days ahead, so today's state is the table's date B. The API has no
clock override, so "25 days ago" and "created 10 days ago" cannot be seeded:
the stalled items and the no-start task start their clocks today. Creating a
deliverable always names an owner (the discipline lead by default) and creating
a milestone needs a date; only a copied structure or a template leaves either
blank. So SYN-105 comes from a one-milestone template ("Client workshop", no
date), and the two unowned SYN-101 deliverables are left to the API tests.
Each module's marker record already present means no writes for that module;
partially completed earlier runs require inspection. Refused API steps are
reported and fail the process.
"""
from fixture_api import connect, FixtureError
import datetime as dt
import sys
import time
import uuid

api_client = connect()
BASE = api_client.base
refusals, skipped = [], []
REASON = "Work can start before a readiness assessment for this example."
PROFILES = ("Harbour Road Phase 2 — storm sewer and sanitary profiles, utility relocation conflict resolution "
            "and sheet set coordination")  # seed_resources.py task for Alex in week W
TEMPLATE = "Riverside workshop starter"


class Refused(Exception):
    def __init__(self, status, text):
        super().__init__(text)
        self.status = status


def call(path, user, method="GET", body=None):
    try:
        return api_client.call(path, user, method, body)
    except FixtureError as e:
        raise Refused(e.status, str(e)) from None


def attempt(label, step):
    """Runs one permissioned step; a refusal is recorded as-is, never worked around."""
    try:
        return step()
    except Refused as e:
        refusals.append(f"{label}: {e}")


def rid():
    return str(uuid.uuid4())


def iso(d):
    return d.isoformat() if d else None


today = dt.date.fromisoformat(call("me", "priya")["settings"]["today"])
monday = today - dt.timedelta(days=today.weekday())


def day(week, weekday=0):
    """A date in the week `week` weeks after this one; weekday 0 is Monday, 4 is Friday."""
    return monday + dt.timedelta(days=7 * week + weekday)


def weekdays_after(start, n, skip=()):
    d = start
    while n > 0:
        d += dt.timedelta(days=1)
        if d.weekday() < 5 and d not in skip:
            n -= 1
    return d


HOLIDAY = weekdays_after(today, 2)  # today + 2 working days (verification.md 8.2, browser version)


def wd(n):
    """The n-th working day after today, skipping weekends and the SYN-105 office holiday."""
    return weekdays_after(today, n, {HOLIDAY})


def by_name(rows, name, field="name"):
    return next((r for r in (rows["items"] if isinstance(rows, dict) else rows) if r[field] == name), None)


def exists(number):
    try:
        return call(f"projects/{number}", "jordan")
    except Refused as e:
        if e.status != 404:
            raise
        return None


try:
    syn = {n: call(f"projects/{n}", "jordan") for n in ("SYN-101", "SYN-103")}
except Refused as e:
    sys.exit(f"Run tools/preview/seed_resources.py first ({e}).")
p1, p3 = syn["SYN-101"]["id"], syn["SYN-103"]["id"]
d1 = {d["code"]: d["id"] for d in syn["SYN-101"]["disciplines"]}
d3 = {d["code"]: d["id"] for d in syn["SYN-103"]["disciplines"]}
users = call("admin/users", "jordan")
uid = {u["email"].split("@")[0]: u["id"] for u in users}
ref = call("reference", "jordan")
disc = {d["code"]: d["id"] for d in ref["disciplines"]}
dtype = {t["name"]: t["id"] for t in ref["deliverableTypes"]}
client_id = next(c["id"] for c in ref["clients"] if c["isActive"])
head_office = next(o["id"] for o in ref["offices"] if o["isActive"])


def project(pm, number, name, members, leads, office, active, **extra):
    """Created by its PM with members and discipline leads; made Active when asked. Returns (id, discipline ids by code)."""
    pid = call("projects", pm, "POST", {**extra,
        "projectNumber": number, "name": name, "clientId": client_id, "officeId": office,
        "description": 'Municipal servicing and grading design.', "startDate": iso(today - dt.timedelta(days=14)),
        "targetCompletionDate": iso(today + dt.timedelta(days=150)),
        "disciplines": [{"disciplineId": disc[code], "leadUserId": uid[lead]} for code, lead in leads.items()],
        "members": [{"userId": uid[who], "roles": ["TeamMember"], "disciplineId": disc[code]} for who, code in members]})["id"]
    p = call(f"projects/{pid}", pm)
    if active:
        call(f"projects/{pid}/transition", pm, "POST", {"toStatus": "Active", "rowVersion": p["rowVersion"]})
    return pid, {d["code"]: d["id"] for d in p["disciplines"]}


def task(pid, pd, pm, who, name, code="CIV", start=None, due=None, est=None, **extra):
    return call(f"projects/{pid}/tasks", pm, "POST", {
        "name": name, "description": 'Prepare the assigned design work and coordinate its review.', "projectDisciplineId": pd[code],
        "assigneeId": uid[who], "estimatedHours": est, "startDate": iso(start), "dueDate": iso(due), **extra})


def move(t, who, to, **extra):
    return {**t, **call(f"tasks/{t['id']}/transition", who, "POST", {"toStatus": to, "rowVersion": t["rowVersion"], **extra})}


def start(t, who, pm="priya"):
    """The PM authorises a start without a readiness assessment; the assignee then starts the work."""
    call(f"tasks/{t['id']}/start-authorisations", pm, "POST", {"requestId": rid(), "acknowledged": True, "reason": REASON})
    return move(t, who, "In Progress", acknowledgeReadiness=True, reason=REASON)


def deliverable(pid, pd, pm, name, code, kind, owner=None, reviewer=None, review=False, due=None):
    return call(f"projects/{pid}/deliverables", pm, "POST", {
        "name": name, "projectDisciplineId": pd[code], "deliverableTypeId": dtype[kind], "description": 'Prepare and review the design package.',
        "ownerId": uid[owner] if owner else None, "reviewerId": uid[reviewer] if reviewer else None, "dueDate": iso(due), "requiresReview": review})


def dmove(d, who, to, **extra):
    return {**d, **call(f"deliverables/{d['id']}/transition", who, "POST", {"toStatus": to, "rowVersion": d["rowVersion"], **extra})}


def weekdays(first, last):
    return [first + dt.timedelta(days=n) for n in range((last - first).days + 1) if (first + dt.timedelta(days=n)).weekday() < 5]


def propose(pid, pm, who, first, last, hours, task_id, role_id=None):
    """A Production allocation from `first` to `last`, linked to the person's task on each weekday."""
    return call(f"projects/{pid}/allocations", pm, "POST", {
        "requestId": rid(), "personId": uid[who], "purpose": "Production", "fromDate": iso(first), "throughDate": iso(last),
        "plannedHours": hours, "days": [], "openRoleId": role_id,
        "links": [{"workType": "Task", "workId": task_id, "workDate": iso(d)} for d in weekdays(first, last)]})


def confirm(pid, a, supervisor):
    pv = call(f"projects/{pid}/allocations/{a['id']}/confirmation-preview", supervisor)
    over = [d["date"] for d in pv["days"] if d["overByHours"] > 0]
    return call(f"projects/{pid}/allocations/{a['id']}/confirm", supervisor, "POST", {
        "requestId": rid(), "rowVersion": pv["rowVersion"],
        "dateVersions": [{"workDate": d["date"], "rowVersion": d["dateVersion"]} for d in pv["days"]],
        "overCapacityReason": f"Accepted over capacity on {', '.join(over)}." if over else None})


def role(pid, pm, label, first, last, hours, discipline=None):
    return call(f"projects/{pid}/open-roles", pm, "POST", {"roleLabel": label, "projectDisciplineId": discipline, "fromDate": iso(first),
                                                           "throughDate": iso(last), "plannedHours": hours, "note": 'Name to be confirmed.'})


def staffing():
    """8.1: SYN-104 in Setup with open roles before names are known, and one restricted SYN-103 role."""
    if exists("SYN-104"):
        return skipped.append("8.1 staffing: SYN-104 already exists")
    p4, d4 = project("priya", "SYN-104", "Lakeside subdivision servicing (proposal)", [("alex", "CIV"), ("jill", "CIV"), ("omar", "GEO")],
                     {"CIV": "marc", "GEO": "omar"}, head_office, active=False)
    concept = task(p4, d4, "priya", "jill", "Servicing concept layout", start=day(2), due=day(5, 4), est=60)
    desktop = task(p4, d4, "priya", "omar", "Geotechnical desktop study", "GEO", start=day(2), due=day(3, 4), est=40)
    attempt("SYN-104 Civil technologist role, by Priya", lambda: role(p4, "priya", "Civil technologist", day(2), day(5, 4), 80, d4["CIV"]))
    attempt("SYN-104 Senior reviewer role, by Priya", lambda: role(p4, "priya", "Senior reviewer", day(3), day(4, 4), 20))
    geo = attempt("SYN-104 Geotechnical engineer role, by Priya", lambda: role(p4, "priya", "Geotechnical engineer", day(2), day(3, 4), 40, d4["GEO"]))
    if geo:
        attempt("SYN-104 Geotechnical engineer filled by a Proposed allocation for Omar, by Priya",
                lambda: propose(p4, "priya", "omar", day(2), day(3, 4), 40, desktop["id"], geo["id"]))
    drafting = attempt("SYN-104 Drafting support role, by Priya", lambda: role(p4, "priya", "Drafting support", day(4), day(5, 4), 24, d4["CIV"]))

    def declined():
        a = propose(p4, "priya", "jill", day(4), day(5, 4), 24, concept["id"], drafting["id"])
        return call(f"projects/{p4}/allocations/{a['id']}/decline", "sam", "POST",
                    {"requestId": rid(), "rowVersion": a["rowVersion"], "reason": "Jill is committed to the Harbour Road drawings those weeks."})
    if drafting:
        attempt("SYN-104 Drafting support proposed for Jill by Priya, declined by Sam (role back to Open)", declined)
    attempt("SYN-103 Hydraulic modeller role, by Marc", lambda: role(p3, "marc", "Hydraulic modeller", day(2), day(3, 4), 30, d3["CIV"]))


def stalled():
    """8.3: stalled items on SYN-101 (their clocks start today; see the module note)."""
    if by_name(call(f"projects/{p1}/tasks?pageSize=200", "jordan"), "Utility conflict log"):
        return skipped.append("8.3 stalled items: 'Utility conflict log' already exists")
    a = start(task(p1, d1, "priya", "jill", "Utility conflict log", due=today + dt.timedelta(days=20), est=16), "jill")
    call(f"items/Task/{a['id']}/comments", "priya", "POST", {"body": 'please add the two new crossings from the survey.'})
    attempt("SYN-101 'Pavement design memo' started by Omar", lambda: start(
        task(p1, d1, "priya", "omar", "Pavement design memo", "GEO", due=today + dt.timedelta(days=25), est=12), "omar"))

    def review():
        c = start(task(p1, d1, "priya", "alex", "Storm profiles review", due=today + dt.timedelta(days=10), est=8,
                       requiresReview=True, reviewerId=uid["diane"]), "alex")
        c = move(move(c, "alex", "Ready for Review"), "diane", "In Review")
        call(f"items/Task/{c['id']}/comments", "diane", "POST", {"body": 'two profiles still show the old invert levels.'})
    attempt("SYN-101 'Storm profiles review' started by Alex, review requested, taken into review by Diane", review)

    def report():
        r = dmove(dmove(deliverable(p1, d1, "priya", "Geotechnical report", "GEO", "Report", "omar", "diane", review=True,
                                    due=today + dt.timedelta(days=21)), "omar", "In Progress"), "omar", "In Review")
        call(f"deliverables/{r['id']}", "omar", "PATCH", {"description": 'Borehole summary and pavement recommendations, revised for review.',
                                                         "rowVersion": r["rowVersion"]})
    attempt("SYN-101 'Geotechnical report' sent to review and edited by Omar", report)


def capacity():
    """8.4: week W (next week) for Alex and Jill, and home disciplines."""
    if any(e["label"] == "Proposal support" and e["startWeek"] == iso(day(1)) for e in call("planning/entries?mine=true&pageSize=200", "sam")["items"]):
        return skipped.append("8.4 capacity: Sam's 'Proposal support' entry for week W already exists")
    call("planning/entries", "sam", "POST", {
        "personId": uid["alex"], "hoursPerWeek": 8, "startWeek": iso(day(1)), "endWeek": iso(day(1)), "label": "Proposal support",
        "sourceCategory": "Proposal", "confidence": "Expected", "visibility": "Published", "notes": 'Manager entry outside any project.'})
    attempt("Alex 10 h private draft in week W, by Jordan", lambda: call("planning/entries", "jordan", "POST", {
        "personId": uid["alex"], "hoursPerWeek": 10, "startWeek": iso(day(1)), "endWeek": iso(day(1)), "label": "Possible proposal support",
        "sourceCategory": "Proposal", "confidence": "Possible", "visibility": "Draft", "notes": 'Private draft; not yet shared.'}))
    profiles = by_name(call(f"projects/{p1}/tasks?pageSize=200", "jordan"), PROFILES)
    if profiles:
        attempt("SYN-101 Alex 20 h in week W, proposed by Priya, confirmed by Sam",
                lambda: confirm(p1, propose(p1, "priya", "alex", day(1), day(1, 4), 20, profiles["id"]), "sam"))
    else:
        refusals.append("SYN-101 Alex 20 h in week W: the seed_resources task for Alex was not found")
    for weekday in (0, 1):
        attempt(f"Jill unavailable {day(1, weekday)}, by Sam", lambda: call(
            f"users/{uid['jill']}/availability/{iso(day(1, weekday))}", "sam", "PUT", {"expectedRowVersion": 0, "availableHours": 0, "category": "Unavailable"}))
    for who, code in (("alex", "CIV"), ("jill", "CIV"), ("omar", "GEO")):
        version = next(u["rowVersion"] for u in users if u["id"] == uid[who])
        attempt(f"{who} home discipline {code}, by Jordan", lambda: call(
            f"admin/users/{uid[who]}", "jordan", "PATCH", {"homeDisciplineId": disc[code], "rowVersion": version}))


def counts():
    """8.5: the SYN-101 On Hold task (unowned deliverables cannot be created through the API; see the module note)."""
    if by_name(call(f"projects/{p1}/tasks?pageSize=200", "jordan"), "Culvert crossing options"):
        return skipped.append("8.5 counts: 'Culvert crossing options' already exists")
    move(task(p1, d1, "priya", "alex", "Culvert crossing options", est=10), "priya", "On Hold", reason="Waiting for the client's culvert survey.")


def dependencies():
    """8.2 (browser version) and the 8.5 timeline cases: SYN-105 in its own office, whose only holiday is HOLIDAY."""
    if exists("SYN-105"):
        return skipped.append("8.2 dependencies: SYN-105 already exists")
    if not call("me", "jordan")["settings"]["workingDaysEnabled"]:
        call("admin/settings/working_days_enabled", "jordan", "PUT", {"value": True})
    # An office of its own keeps the holiday off every person's capacity and every other project.
    office = by_name(call("admin/offices", "jordan"), "Riverside office") or call(
        "admin/offices", "jordan", "POST", {"name": "Riverside office", "code": "RIV", "timeZone": "America/Halifax"})
    if not any(h["date"] == iso(HOLIDAY) for h in call(f"admin/holidays?officeId={office['id']}", "jordan")):
        call("admin/holidays", "jordan", "POST", {"officeId": office["id"], "date": iso(HOLIDAY), "name": "Riverside civic holiday"})
    template = next((x for x in call("templates", "jordan")["templates"] if x["name"] == TEMPLATE and x["status"] == "Published"), None)
    if not template:
        template = call("templates", "jordan", "POST", {"name": TEMPLATE, "description": 'An unscheduled client workshop milestone.'})
        call(f"templates/{template['id']}/structure", "jordan", "PUT", {
            "rowVersion": template["rowVersion"], "disciplines": [{"disciplineId": disc["CIV"], "isDefaultIncluded": True}],
            "milestones": [{"ref": "m1", "name": "Client workshop", "milestoneType": "Client Workshop", "anchor": "None", "isClientFacing": True}],
            "deliverables": [], "tasks": [], "dependencies": []})
        call(f"templates/{template['id']}/publish", "jordan", "POST", {"rowVersion": call(f"templates/{template['id']}", "jordan")["rowVersion"]})
    p5, d5 = project("priya", "SYN-105", "Riverside lane grading and drainage", [("alex", "CIV"), ("jill", "CIV")], {"CIV": "marc"},
                     office["id"], active=True, templateId=template["id"])
    t = today
    pairs = [  # link, type, lag, predecessor (name, start, due) by Alex, successor (name, start, due) by Jill
        ("L1", "FinishToStart", 0, ("Topographic survey processing", None, wd(1)), ("Grading design", wd(2), wd(9))),
        ("L2", "FinishToStart", 2, ("Utility locate review", None, t), ("Utility conflict drawings", t, wd(6))),
        ("L3", "StartToStart", 0, ("Storm sewer layout", t, wd(6)), ("Storm sewer profiles", t, wd(6))),
        ("L4", "StartToStart", 2, ("Grading model", t, wd(9)), ("Drainage area plan", t, wd(9))),
        ("L5", "FinishToFinish", 0, ("Specifications", None, wd(6)), ("Quantity estimate", t, wd(5))),
        ("L6", "FinishToFinish", 1, ("Design report", None, wd(1)), ("Drawing checklist", t, wd(2))),
    ]
    made = {}
    for link, kind, lag, (pn, ps, pdue), (sn, ss, sdue) in pairs:
        made[link] = (task(p5, d5, "priya", "alex", pn, start=ps, due=pdue, est=8, requiresReview=False),
                      task(p5, d5, "priya", "jill", sn, start=ss, due=sdue, est=8, requiresReview=False))
        call(f"tasks/{made[link][1]['id']}/dependencies", "priya", "POST",
             {"predecessorTaskId": made[link][0]["id"], "lagDays": lag, "type": kind, "note": f"{link} worked example."})
    for link in ("L1", "L2"):  # predecessors finished today
        attempt(f"SYN-105 {link} predecessor started and completed by Alex", lambda: move(start(made[link][0], "alex"), "alex", "Complete"))
    for link in ("L3", "L4", "L5"):  # predecessors started today
        attempt(f"SYN-105 {link} predecessor started by Alex", lambda: start(made[link][0], "alex"))
    for link in ("L5", "L6"):  # successors in progress
        attempt(f"SYN-105 {link} successor started by Jill", lambda: start(made[link][1], "jill"))
    task(p5, d5, "priya", "jill", "No start date task", due=today + dt.timedelta(days=10), est=6)
    permit = deliverable(p5, d5, "priya", "Permit drawings", "CIV", "Permit Submission", "alex")
    task(p5, d5, "priya", "alex", "Permit drawing markups", start=wd(1), due=wd(6), est=12, deliverableId=permit["id"])


def summary():
    print(f"Packet 036 preview data at {BASE} (organisation today {today}, week W {day(1)}, SYN-105 holiday {HOLIDAY}):")
    for number in ("SYN-104", "SYN-103"):
        p = exists(number)
        if p:
            roles = call(f"projects/{p['id']}/open-roles", "jordan")
            print(f"  {number} {p['status']}: " + "; ".join(
                f"{r['roleLabel']} {r['fromDate']}..{r['throughDate']} {r['plannedHours']:g} h {r['status']}" for r in roles))
    p5 = exists("SYN-105")
    for row in call(f"projects/{p5['id']}/tasks?pageSize=200", "jordan")["items"] if p5 else []:
        deps = call(f"tasks/{row['id']}/dependencies", "jordan")["dependsOn"]
        flags = [k for k in ("isBlocked", "isWaiting", "isDateInconsistent") if (row.get("state") or {}).get(k)]
        link = "; ".join(f"after {d['task']['name']}, {d['type']} lag {d['lagDays']}, satisfied {d['satisfied']}" for d in deps)
        print(f"    SYN-105 {row['name']} [{row['status']}] {', '.join(flags) or 'clear'}{(' — ' + link) if link else ''}")
    print("  Home disciplines: " + ", ".join(f"{u['email'].split('@')[0]} {u['homeDisciplineId'] or 'not set'}"
                                             for u in call("admin/users", "jordan") if u["email"].split("@")[0] in ("alex", "jill", "omar", "diane")))
    for s in skipped:
        print(f"  SKIPPED {s}")
    for r in refusals:
        print(f"  REFUSED {r}")


for module in (staffing, stalled, capacity, counts, dependencies):
    attempt(module.__name__, module)
time.sleep(2)  # the evaluation worker recomputes states a moment after each save
summary()

if refusals:
    raise SystemExit("Fixture setup stopped with refused API steps; review the reported failures before continuing.")
