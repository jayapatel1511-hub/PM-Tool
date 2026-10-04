#!/usr/bin/env python3
"""Seeds synthetic records for every other Tuesday module on SYN-101 to SYN-103, so each screen has something to show.

Usage: python3 tools/preview/seed_modules.py [base-url]    (default http://localhost:5080)
Run tools/preview/seed_resources.py first: this builds on its projects, tasks and people. Synthetic local preview data
only: every record is invented and says so, and links point at the reserved docs.example.test domain. Signs in with the
development header (X-Dev-User) and refuses to run unless GET /api/v1/config reports authMode == "Development"; never aim
it at a real environment. Every write goes through the API as the person who would make it, so validation, permissions,
audit and notifications apply; no setting is changed. Dates are relative to the organisation's today (GET /api/v1/me).
Each module is skipped when its marker (the first record it creates) already exists, so a second run creates nothing.
A refused step is printed with its status and error code; the rest continues."""
import datetime as dt
import json
import sys
import time
import urllib.error
import urllib.request
import uuid

BASE = sys.argv[1] if len(sys.argv) > 1 else "http://localhost:5080"
NUMBERS = ("SYN-101", "SYN-102", "SYN-103")
DEV_USERS = ("jordan", "lena", "sam", "priya", "marc", "alex", "jill", "diane", "omar", "rita")
P = "Synthetic preview — "
URL = "https://docs.example.test/synthetic-preview/"  # reserved test domain: never a real site
FOLDER = r"\\synthetic-preview-fs\projects\SYN-101"
refusals, skipped, created = [], [], {}

# Work created by seed_resources.py (looked up by name, never changed here).
T_SURVEY = "Survey base plan QA and topographic gap list"
T_PROFILES = ("Harbour Road Phase 2 — storm sewer and sanitary profiles, utility relocation conflict resolution "
              "and sheet set coordination")
T_STANDARDS = "Municipal design standards check"
T_REDLINES = "60 % drawing set markups and redline pickup"
T_DRAINAGE = "Drainage area plan and pre/post-development runoff summary"
T_STORM = "Storm sewer design sheets and HGL check"
T_LOG = "Utility relocation conflict log update"
T_SERVICES = "Existing services review for the waterfront lots"
T_WATERMAIN = "Watermain and hydrant layout"
T_RESTRICTED = "Options memo inputs for the restricted study"

# Records this script creates.
KICKOFF, SUB60, WORKSHOP, SUB90 = (P + s for s in ("Project kickoff", "60 % design submission",
                                                   "Utility relocation client workshop", "90 % design submission"))
BRIEF, SERVICING60, OPTIONS = (P + s for s in ("Servicing brief submission", "60 % servicing submission",
                                               "Restricted options workshop"))
D_STORM, D_MEMO, D_REGISTER, D_BRIEF, D_SET90, D_PAVEMENT = (P + s for s in (
    "Storm sewer plan and profile drawings (60 %)", "Geotechnical investigation memo", "Utility relocation conflict register",
    "Drainage design brief", "90 % drawing set", "Pavement design recommendations"))
D_SITE, D_SBRIEF, D_OPTIONS = (P + s for s in ("Site servicing plan (60 %)", "Servicing brief", "Restricted options memo"))
DEC_OUTFALL, DEC_STANDARD, DEC_BACKFILL, DEC_HYDRANT, DEC_SHORTLIST = (P + s for s in (
    "Confirm the storm sewer outfall option", "Adopt the synthetic drainage standard for minor-system sizing",
    "Confirm the trench backfill material", "Confirm hydrant spacing for the waterfront lots", "Shortlist the restricted study options"))
RSK_UTILITIES, RSK_GROUNDWATER, RSK_CLIENT, RSK_LOTS = (P + s for s in (
    "Unrecorded utilities in the corridor", "Groundwater inflow during trenching", "Client review period overruns",
    "Grading constraints on the waterfront lots"))
ISS_SURVEY, ISS_CONFLICT, ISS_CLIENT, ISS_LAYERS, ISS_FLOWTEST = (P + s for s in (
    "Survey gap at the north tie-in", "Storm sewer crosses the relocated watermain", "Client review comments arrived late",
    "Base plan layer names do not follow the CAD standard", "Hydrant flow test not yet scheduled"))
MTG_COORD, MTG_CLIENT, MTG_SERVICING = (P + s for s in ("Weekly coordination meeting", "Client design review",
                                                        "Waterfront servicing coordination"))
CAL_COORD, CAL_SITE, CAL_FOCUS, CAL_WATERFRONT, CAL_RESTRICTED = (P + s for s in (
    "Weekly coordination meeting", "Borehole logging site visit", "Focus time: storm sewer profiles",
    "Waterfront servicing check-in", "Restricted study working session"))
H_ACCEPTED, H_SUBMITTED = (P + s for s in ("Groundwater and bedding inputs for storm sewer design",
                                           "Geotechnical constraints for the utility relocation register"))
REVIEW, SUBMISSION = P + "60 % storm sewer drawings review", P + "60 % design submission package"
STD_ID, STD_TITLE = "SYN-STD-01", P + "Municipal drainage design standard (fictional)"
BASIS_STORM, BASIS_GROUNDWATER = P + "Minor storm design return period", P + "Groundwater stays below trench invert"
TEMPLATE = P + "Small site servicing template"
STORM_URL, MEMO_URL = f"{URL}SYN-101/SYN-101-C-201-P02.pdf", f"{URL}SYN-101/SYN-101-G-001-R0.pdf"


class Refused(Exception):
    def __init__(self, status, text):
        super().__init__(text)
        self.status = status


class Missing(Exception):
    """A record this step builds on is absent (an earlier step or module was refused)."""


def call(path, user, method="GET", body=None):
    req = urllib.request.Request(f"{BASE}/api/v1/{path}", method=method, data=None if body is None else json.dumps(body).encode(),
                                 headers={"X-Dev-User": f"{user}@hub.test", "Content-Type": "application/json"})
    try:
        with urllib.request.urlopen(req, timeout=60) as r:
            data = r.read()
    except urllib.error.HTTPError as e:
        try:
            p = json.loads(e.read())
        except ValueError:
            p = {}
        raise Refused(e.code, f"{method} /api/v1/{path} as {user}: HTTP {e.code} {p.get('code')}: {p.get('detail') or p.get('title')}"
                      + (f" {p['errors']}" if p.get("errors") else "")) from None
    return json.loads(data) if data else None


def attempt(label, step):
    """Runs one permissioned step; a refusal is recorded as-is, never worked around."""
    try:
        return step()
    except Refused as e:
        refusals.append(f"{label}: {e}")
    except Missing as e:
        skipped.append(f"{label}: needs {e}")


def made(module, label):
    created.setdefault(module, []).append(label)


def skip(module, marker):
    skipped.append(f"{module}: marker '{marker.removeprefix(P)}' already exists")


def rid():
    return str(uuid.uuid4())


def find(path, value, field="name"):
    data = call(path, "jordan")
    return next((x for x in (data["items"] if isinstance(data, dict) else data) if x.get(field) == value), None)


def need(record, what):
    if record is None:
        raise Missing(what.removeprefix(P))
    return record


def task(pid, name):
    """A seed_resources task with its current rowVersion."""
    return call(f"tasks/{need(find(f'projects/{pid}/tasks?pageSize=200', name), name)['id']}", "jordan")["task"]


def deliverable(pid, name):
    return need(find(f"projects/{pid}/deliverables", name), name)


def milestone(pid, name):
    return need(find(f"projects/{pid}/milestones?showCompleted=true", name), name)


def current_revision(pid, deliverable_id):
    """The current registered source revision of a deliverable (Changes → Register source revision)."""
    return next((s["revision"] for s in call(f"projects/{pid}/changes/options", "jordan")["sources"]
                 if s["revision"]["deliverableId"] == deliverable_id and s["isCurrent"] and s["published"]), None)


with urllib.request.urlopen(f"{BASE}/api/v1/config", timeout=30) as r:
    mode = json.loads(r.read()).get("authMode")
if mode != "Development":
    sys.exit(f"Refusing to run: {BASE} reports authMode {mode!r}; this script only seeds a Development instance.")

today = dt.date.fromisoformat(call("me", "priya")["settings"]["today"])
monday = today - dt.timedelta(days=today.weekday())


def day(week, weekday=0):
    """A date in the week `week` weeks after this one; weekday 0 is Monday, 4 is Friday."""
    return monday + dt.timedelta(days=7 * week + weekday)


def ago(n):
    return today - dt.timedelta(days=n)


def ahead(n):
    return today + dt.timedelta(days=n)


def iso(d):
    return d.isoformat() if d else None


try:
    proj = {n: call(f"projects/{n}", "jordan") for n in NUMBERS}
except Refused as e:
    sys.exit(f"Run tools/preview/seed_resources.py first ({e}).")
p1, p2, p3 = (proj[n]["id"] for n in NUMBERS)
pd = {n: {d["code"]: d["id"] for d in proj[n]["disciplines"]} for n in NUMBERS}
c1, g1, c2, c3 = pd["SYN-101"]["CIV"], pd["SYN-101"]["GEO"], pd["SYN-102"]["CIV"], pd["SYN-103"]["CIV"]
people = call("users?limit=500", "jordan")
uid = {u["email"].split("@")[0]: u["id"] for u in people}
uname = {u["email"].split("@")[0]: u["displayName"] for u in people}
ref = call("reference", "jordan")
dtype = {t["name"]: t["id"] for t in ref["deliverableTypes"]}
disc = {d["code"]: d["id"] for d in ref["disciplines"]}


def milestones_and_deliverables():
    mod = "Milestones and deliverables"
    if find(f"projects/{p1}/milestones?showCompleted=true", KICKOFF):
        return skip(mod, KICKOFF)
    ms = {}
    for pid, pm, name, kind, date in ((p1, "priya", KICKOFF, "Kickoff", ago(28)), (p1, "priya", SUB60, "Design Submission", ahead(6)),
                                      (p1, "priya", WORKSHOP, "Client Workshop", ahead(20)), (p1, "priya", SUB90, "Design Submission", ahead(60)),
                                      (p2, "marc", BRIEF, "Design Submission", ago(14)), (p2, "marc", SERVICING60, "Design Submission", ahead(30)),
                                      (p3, "marc", OPTIONS, "Client Workshop", ahead(20))):
        ms[name] = call(f"projects/{pid}/milestones", pm, "POST", {"name": name, "milestoneType": kind, "date": iso(date),
                                                                    "description": "Synthetic preview milestone for local UI review."})
        made(mod, ms[name]["key"])
    attempt("SYN-101 60 % submission moved five days later by Priya", lambda: call(f"milestones/{ms[SUB60]['id']}/change-date", "priya", "POST", {
        "newDate": iso(ahead(11)), "reason": "Synthetic preview: the client asked for five more days to return survey comments.",
        "rowVersion": ms[SUB60]["rowVersion"]}))

    def new(pid, pm, name, discipline, kind, owner, target, reviewer=None, review=True, revision=None, due=None):
        d = call(f"projects/{pid}/deliverables", pm, "POST", {
            "name": name, "projectDisciplineId": discipline, "deliverableTypeId": dtype[kind], "description": "Synthetic preview deliverable for local UI review.",
            "ownerId": uid[owner], "reviewerId": uid[reviewer] if reviewer else None, "milestoneId": ms[target]["id"], "dueDate": iso(due),
            "revision": revision, "requiresReview": review})
        made(mod, d["key"])
        return d

    def move(d, who, to, **extra):
        return {**d, **call(f"deliverables/{d['id']}/transition", who, "POST", {"toStatus": to, "rowVersion": d["rowVersion"], **extra})}

    def issue(d, who, days_ago, to):
        return {**d, **call(f"deliverables/{d['id']}/issue", who, "POST", {
            "issuedDate": iso(ago(days_ago)), "issuedTo": to, "transmittalUrl": f"{URL}transmittals/{d['key']}",
            "note": "Synthetic preview issue record.", "rowVersion": d["rowVersion"]})}

    storm = new(p1, "priya", D_STORM, c1, "Drawing Package", "jill", SUB60, "diane", revision="P02")
    memo = new(p1, "priya", D_MEMO, g1, "Memo", "omar", SUB60, review=False, revision="R0", due=ahead(7))
    new(p1, "priya", D_REGISTER, c1, "Report", "alex", WORKSHOP)
    brief = new(p1, "priya", D_BRIEF, c1, "Report", "jill", KICKOFF, review=False, revision="A")
    new(p1, "priya", D_SET90, c1, "Drawing Package", "alex", SUB90, "diane")
    pavement = new(p1, "priya", D_PAVEMENT, g1, "Report", "omar", SUB90)
    site = new(p2, "marc", D_SITE, c2, "Drawing Package", "alex", SERVICING60, "jill", revision="P01")
    sbrief = new(p2, "marc", D_SBRIEF, c2, "Report", "jill", BRIEF, review=False, revision="A")
    options = new(p3, "marc", D_OPTIONS, c3, "Memo", "jill", OPTIONS)

    def complete(m, pm, days_ago):
        return call(f"milestones/{m['id']}/complete", pm, "POST", {"completedDate": iso(ago(days_ago)),
                                                                     "rowVersion": call(f"milestones/{m['id']}", pm)["milestone"]["rowVersion"]})

    attempt("SYN-101 drainage brief issued and accepted by Priya", lambda: move(
        issue(brief, "priya", 29, "Synthetic preview client contact (fictional)"), "priya", "Accepted"))
    attempt("SYN-101 kickoff completed by Priya", lambda: complete(ms[KICKOFF], "priya", 28))
    attempt("SYN-101 storm drawings started and sent for review by Jill", lambda: move(
        move(storm, "jill", "In Progress"), "jill", "In Review", comment="Synthetic preview: 60 % sheets ready for review."))
    attempt("SYN-101 geotechnical memo started and issued by Omar", lambda: issue(
        move(memo, "omar", "In Progress"), "omar", 2, "Synthetic preview civil design team (internal)"))
    attempt("SYN-101 pavement recommendations put on hold by Omar", lambda: move(
        pavement, "omar", "On Hold", reason="Synthetic preview: waiting for the borehole programme before pavement design starts."))
    attempt("SYN-102 site servicing plan started by Alex", lambda: move(site, "alex", "In Progress"))
    attempt("SYN-102 servicing brief issued by Marc", lambda: issue(sbrief, "marc", 15, "Synthetic preview client contact (fictional)"))
    attempt("SYN-102 servicing brief milestone completed by Marc", lambda: complete(ms[BRIEF], "marc", 14))
    attempt("SYN-103 options memo started by Jill", lambda: move(options, "jill", "In Progress"))


def decisions():
    mod = "Decisions"
    if find(f"projects/{p1}/decisions", DEC_OUTFALL, "subject"):
        return skip(mod, DEC_OUTFALL)
    drainage_task = task(p1, T_DRAINAGE)
    sub60 = find(f"projects/{p1}/milestones?showCompleted=true", SUB60)

    def new(pid, who, subject, owner, requested, required, impact, links=()):
        d = call(f"projects/{pid}/decisions", who, "POST", {
            "subject": subject, "description": "Synthetic preview decision for local UI review.", "ownerUserId": uid[owner],
            "dateRequested": iso(requested), "requiredByDate": iso(required), "impactLevel": impact,
            "impactDescription": "Synthetic preview: affects the design programme of this fictional project.", "links": list(links)})
        made(mod, d["key"])
        return d

    # Not linked to the storm task: a pending decision link on a task that also has readiness inputs makes
    # GET .../readiness/window and the discipline coordination views return 500 (EF cannot translate the filter after the
    # LinkedRecord projection in Readiness.cs). Restore the link once that backend defect is fixed. The readiness constraint
    # below still ties this decision to the task.
    new(p1, "marc", DEC_OUTFALL, "priya", ago(3), ahead(6), "High")
    standard = new(p1, "jill", DEC_STANDARD, "marc", ago(6), ahead(2), "Medium", [{"targetType": "Task", "targetId": drainage_task["id"]}])
    attempt("SYN-101 drainage standard decided by Marc", lambda: call(f"decisions/{standard['id']}/transition", "marc", "POST", {
        "toStatus": "Decided", "decisionText": "Synthetic preview decision: size the minor system to the fictional 2025 drainage standard.",
        "decisionDate": iso(ago(1)), "rowVersion": standard["rowVersion"]}))
    backfill = new(p1, "alex", DEC_BACKFILL, "omar", ago(12), ago(5), "Medium",
                   [{"targetType": "Milestone", "targetId": sub60["id"]}] if sub60 else [])
    attempt("SYN-101 overdue backfill decision put under review by Alex", lambda: call(
        f"decisions/{backfill['id']}/transition", "alex", "POST", {"toStatus": "Under Review", "rowVersion": backfill["rowVersion"]}))
    new(p2, "alex", DEC_HYDRANT, "marc", ago(2), ahead(10), "Low")
    new(p3, "jill", DEC_SHORTLIST, "marc", ago(1), ahead(12), "Medium")


def risks_and_issues():
    mod = "Risks and issues"
    if find(f"projects/{p1}/risks", RSK_UTILITIES, "title"):
        return skip(mod, RSK_UTILITIES)

    def risk(pid, who, title, owner, probability, impact, review, discipline=None):
        r = call(f"projects/{pid}/risks", who, "POST", {
            "title": title, "description": "Synthetic preview risk for local UI review.", "ownerId": uid[owner], "probability": probability,
            "impact": impact, "mitigation": "Synthetic preview mitigation: check early and keep the programme float.",
            "triggerIndicator": "Synthetic preview trigger: the next survey or site visit shows the condition.", "reviewDate": iso(review),
            "projectDisciplineId": discipline})
        made(mod, r["key"])
        return r

    def issue(pid, who, title, owner, severity, raised, target, discipline=None, **extra):
        i = call(f"projects/{pid}/issues", who, "POST", {
            "title": title, "description": "Synthetic preview issue for local UI review.", "ownerId": uid[owner], "severity": severity,
            "dateRaised": iso(raised), "targetResolutionDate": iso(target), "projectDisciplineId": discipline, **extra})
        made(mod, i["key"])
        return i

    def move(path, item, who, to, **extra):
        return {**item, **call(f"{path}/{item['id']}/transition", who, "POST", {"toStatus": to, "rowVersion": item["rowVersion"], **extra})}

    risk(p1, "priya", RSK_UTILITIES, "marc", 3, 2, ahead(7), c1)
    groundwater = risk(p1, "omar", RSK_GROUNDWATER, "omar", 1, 2, ago(4), g1)
    attempt("SYN-101 groundwater risk moved to Monitoring by Omar", lambda: move(
        "risks", groundwater, "omar", "Monitoring", reason="Synthetic preview: watch the next borehole readings."))
    client = risk(p1, "priya", RSK_CLIENT, "priya", 2, 2, ahead(3))
    realised = attempt("SYN-101 client review risk realised as an issue by Priya", lambda: move(
        "risks", client, "priya", "Realised", reason="Synthetic preview: the review comments arrived a week late.",
        issue={"title": ISS_CLIENT, "description": "Synthetic preview issue raised from a realised risk.", "ownerId": uid["priya"],
               "severity": "Medium", "targetResolutionDate": iso(ahead(4))}))
    if realised:
        made(mod, realised["issueKey"])
    risk(p2, "marc", RSK_LOTS, "marc", 2, 3, ahead(10), c2)
    issue(p1, "jill", ISS_SURVEY, "alex", "Medium", ago(4), ahead(5), c1)
    conflict = issue(p1, "jill", ISS_CONFLICT, "marc", "High", ago(2), ahead(3), c1, issueType="Coordination", affectedDisciplineIds=[g1],
                     locations=[{"kind": "Alignment", "alignment": "Synthetic preview alignment SA-1", "startStation": 120.0, "endStation": 260.0,
                                 "stationUnits": "m", "assetSystem": "Synthetic preview storm sewer", "rowVersion": 0}],
                     documents=[{"kind": "Drawing", "identifier": "SYN-101-C-201", "revision": "P02", "sourceUrl": STORM_URL,
                                 "isAvailable": True, "rowVersion": 0}])
    attempt("SYN-101 conflict issue started by Marc", lambda: move("issues", conflict, "marc", "In Progress"))
    attempt("SYN-101 conflict issue verifier proposed by Jill", lambda: call(f"issues/{conflict['id']}/verification", "jill", "POST", {
        "verifierId": uid["diane"], "status": "Proposed", "note": "Synthetic preview: Diane checks the revised crossing against the drawing.",
        "rowVersion": call(f"issues/{conflict['id']}", "jill")["issue"]["rowVersion"]}))
    layers = issue(p1, "alex", ISS_LAYERS, "alex", "Low", ago(9), ago(2), c1)
    attempt("SYN-101 layer issue resolved by Alex", lambda: move("issues", layers, "alex", "Resolved", resolvedDate=iso(ago(1)),
                                                                   resolution="Synthetic preview: layer names aligned with the CAD standard."))
    issue(p2, "alex", ISS_FLOWTEST, "jill", "Medium", ago(3), ago(1), c2)


def meetings():
    mod = "Meetings and actions"
    if find(f"projects/{p1}/meetings", MTG_COORD, "title"):
        return skip(mod, MTG_COORD)

    def meeting(pid, who, title, kind, date, notes=None):
        m = call(f"projects/{pid}/meetings", who, "POST", {"title": title, "meetingDate": iso(date), "meetingType": kind, "notesLink": notes})
        made(mod, f"{title.removeprefix(P)} ({kind})")
        return m

    def action(m, who, text, owner=None, discipline=None, due=None):
        a = call(f"meetings/{m['id']}/actions", who, "POST", {
            "text": f"Synthetic preview action: {text}", "ownerType": "Discipline" if discipline else "User",
            "ownerUserId": uid[owner] if owner else None, "ownerDisciplineId": discipline, "dueDate": iso(due)})
        made(mod, a["key"])
        return a

    def move(a, who, to):
        return call(f"actions/{a['id']}/transition", who, "POST", {"toStatus": to, "rowVersion": a["rowVersion"]})

    coord = meeting(p1, "priya", MTG_COORD, "Coordination", day(0, 1), f"{URL}SYN-101/meetings/coordination-notes")
    action(coord, "priya", "send the updated survey gap list to the design team.", "alex", due=ago(2))
    action(coord, "priya", "confirm borehole locations for the north tie-in.", discipline=g1, due=ahead(4))
    hgl = action(coord, "priya", "circulate the HGL check summary.", "jill", due=ahead(2))
    attempt("SYN-101 HGL action started by Jill", lambda: move(hgl, "jill", "In Progress"))
    workshop = action(coord, "priya", "book the utility relocation workshop.", "marc", due=ago(3))
    attempt("SYN-101 workshop action completed by Marc", lambda: move(workshop, "marc", "Complete"))
    client = meeting(p1, "marc", MTG_CLIENT, "Client", day(0, 3))
    action(client, "marc", "issue the meeting notes to the fictional client contact.", "priya", due=ahead(1))
    servicing = meeting(p2, "marc", MTG_SERVICING, "Coordination", day(0, 2))
    action(servicing, "marc", "confirm hydrant flow test dates with the fictional utility.", "alex", due=ahead(5))


def comments():
    mod = "Comments"
    storm = deliverable(p1, D_STORM)
    if any((c["body"] or "").startswith("Synthetic preview comment") for c in call(f"items/Deliverable/{storm['id']}/comments", "priya")["items"]):
        return skip(mod, "Synthetic preview comment on " + storm["key"])

    def at(u):
        return f"@[{uname[u]}]({uid[u]})"

    def comment(kind, item, who, text):
        call(f"items/{kind}/{item['id']}/comments", who, "POST", {"body": f"Synthetic preview comment: {text}"})
        made(mod, f"{item.get('key', kind)} by {who}")

    comment("Deliverable", storm, "priya", f"{at('jill')} please confirm the HGL check covers the 1-in-100-year event before the 60 % review.")
    comment("Task", task(p1, T_STORM), "marc", f"{at('alex')} can you pick up the profile redlines once the HGL check is in?")
    outfall = find(f"projects/{p1}/decisions", DEC_OUTFALL, "subject")
    if outfall:
        comment("Decision", outfall, "jill", f"the outfall choice drives the conflict log; {at('priya')} a decision this week keeps the 60 % date.")
    comment("Task", task(p2, T_WATERMAIN), "alex", f"{at('marc')} hydrant spacing is still open in the decision register.")
    comment("Deliverable", deliverable(p3, D_OPTIONS), "marc", f"{at('jill')} keep the restricted options memo within the project team.")


def calendar_events():
    mod = "Calendar events"
    window = call(f"calendar?from={iso(ago(61))}&to={iso(ahead(31))}&projectIds=all", "priya")["entries"]
    if any(e.get("title") == CAL_COORD for e in window):
        return skip(mod, CAL_COORD)

    def event(who, kind, title, pid, date, start, end, where=None):
        call("calendar/events", who, "POST", {"type": kind, "title": title, "projectId": pid, "start": f"{iso(date)}T{start}",
                                              "end": f"{iso(date)}T{end}", "location": where,
                                              "description": "Synthetic preview calendar entry for local UI review."})
        made(mod, f"{title.removeprefix(P)} ({kind}, {who})")

    event("priya", "Meeting", CAL_COORD, p1, day(1, 1), "10:00", "11:00", "Synthetic preview meeting room")
    event("omar", "Site Work", CAL_SITE, p1, day(1, 3), "08:00", "12:00", "Synthetic preview site, north tie-in")
    event("alex", "Internal Task", CAL_FOCUS, None, day(1, 0), "13:00", "16:00")
    event("marc", "Meeting", CAL_WATERFRONT, p2, day(1, 2), "14:00", "14:30", "Synthetic preview video call")
    event("marc", "Meeting", CAL_RESTRICTED, p3, day(1, 4), "09:00", "10:00", "Synthetic preview meeting room")


def links():
    mod = "Links"
    if any(link["url"] == FOLDER for link in call(f"projects/{p1}", "priya")["links"]):
        return skip(mod, FOLDER)

    def link(path, who, title, url, kind=None):
        call(path, who, "POST", {"title": title, "url": url, "linkType": kind})
        made(mod, title.removeprefix(P))

    link(f"projects/{p1}/links", "priya", P + "Project network folder", FOLDER, "Network Folder")
    link(f"projects/{p1}/links", "priya", P + "Project document library", f"{URL}SYN-101/library")
    link(f"projects/{p2}/links", "marc", P + "Waterfront servicing document library", f"{URL}SYN-102/library")
    link(f"items/Deliverable/{deliverable(p1, D_STORM)['id']}/links", "jill", P + "60 % storm sewer sheet set", STORM_URL)


def time_entries():
    mod = "Time entries"
    if any((e["note"] or "").startswith("Synthetic preview") for e in call(f"time?from={iso(ago(365))}&to={iso(today)}", "alex")["entries"]):
        return skip(mod, "Synthetic preview time entry by alex")
    days = [d for d in (ago(n) for n in range(14, 0, -1)) if d.weekday() < 5]  # the last ten working days
    w1, w2 = days[:5], days[5:]
    for who, pid, name, dates, hours, note in (
            ("alex", p1, T_SURVEY, w1, 4.5, "base plan QA and topographic gap list"),
            ("alex", p1, T_STANDARDS, [w1[1], w1[3], w2[0]], 2, "design standards check"),
            ("alex", p1, T_SURVEY, w2[:3], 3, "gap list follow-up"),
            ("alex", p2, T_SERVICES, w2[3:], 4, "existing services review"),
            ("jill", p2, T_WATERMAIN, [w1[0], w1[2], w1[4]], 3, "watermain layout"),
            ("jill", p3, T_RESTRICTED, [w1[1], w2[3]], 1.5, "restricted options inputs"),
            ("jill", p1, T_DRAINAGE, w2, 4, "drainage areas and runoff summary")):
        tid = task(pid, name)["id"]
        for d in dates:
            call("time", who, "POST", {"taskId": tid, "workDate": iso(d), "hours": hours, "note": f"Synthetic preview: {note}"})
        made(mod, f"{who} {len(dates)} × {hours:g} h ({note})")


def handoffs():
    mod = "Handoffs"
    if find(f"projects/{p1}/handoffs", H_ACCEPTED, "title"):
        return skip(mod, H_ACCEPTED)
    memo = deliverable(p1, D_MEMO)

    def draft(title, receiver, target, needed, promised, use, criteria):
        h = call(f"projects/{p1}/handoffs", "omar", "POST", {
            "requestId": rid(), "title": title, "sourceDeliverableId": memo["id"], "sourceRowVersion": memo["rowVersion"], "declaredRevision": "R0",
            "sourceUrl": MEMO_URL, "receivingDisciplineId": c1, "sendingOwnerId": uid["omar"], "receivingOwnerId": uid[receiver], **target,
            "intendedUse": f"Synthetic preview: {use}", "acceptanceCriteria": f"Synthetic preview: {criteria}", "neededBy": iso(needed),
            "promisedBy": iso(promised)})
        made(mod, title.removeprefix(P))
        return h

    def move(h, who, to, **extra):
        return call(f"projects/{p1}/handoffs/{h['id']}/transition", who, "POST", {"requestId": rid(), "toStatus": to, "rowVersion": h["rowVersion"], **extra})

    first = draft(H_ACCEPTED, "jill", {"targetTaskId": task(p1, T_STORM)["id"]}, ahead(1), today,
                  "groundwater levels and bedding parameters for storm sewer sizing.", "the memo gives groundwater depth and bedding class for every run.")
    attempt("SYN-101 groundwater handoff submitted by Omar and accepted by Jill", lambda: move(
        move(first, "omar", "Submitted", reason="Synthetic preview: memo R0 issued."), "jill", "Accepted",
        criteriaOutcome="Synthetic preview: groundwater depth and bedding class are given for every run."))
    second = draft(H_SUBMITTED, "alex", {"targetDeliverableId": deliverable(p1, D_REGISTER)["id"]}, ahead(3), ahead(5),
                   "trench support limits for the conflict register.", "each conflict location has a trench support note.")
    attempt("SYN-101 register handoff submitted by Omar", lambda: move(second, "omar", "Submitted"))


def review_package():
    mod = "Review package"
    storm = deliverable(p1, D_STORM)
    if current_revision(p1, storm["id"]):
        return skip(mod, f"registered revision of {storm['key']}")
    revision = call(f"projects/{p1}/source-revisions", "jill", "POST", {
        "requestId": rid(), "deliverableId": storm["id"], "deliverableRowVersion": storm["rowVersion"], "projectDisciplineId": c1,
        "ownerId": uid["jill"], "sourceSystem": "Synthetic preview document register", "externalIdentifier": "SYN-101-C-201", "title": D_STORM,
        "revision": "P02", "url": STORM_URL, "issuer": "Synthetic preview civil design team",
        "scope": "Synthetic preview: 60 % storm sewer plan and profile sheets."})
    made(mod, f"revision P02 of {storm['key']} registered by jill")
    package = call(f"projects/{p1}/reviews", "marc", "POST", {
        "requestId": rid(), "title": REVIEW, "purpose": "Synthetic preview: independent 60 % review of the storm sewer sheets.",
        "projectDisciplineId": c1, "coordinatorId": uid["marc"], "sourceRevisionIds": [revision["id"]], "requiredForIssue": True,
        "assignments": [{"projectDisciplineId": c1, "reviewerId": uid["diane"], "dueDate": iso(ahead(5))},
                        {"projectDisciplineId": g1, "reviewerId": uid["omar"], "dueDate": iso(ahead(5))}]})
    made(mod, REVIEW.removeprefix(P))
    root = f"projects/{p1}/reviews/{package['id']}"
    call(f"{root}/action", "marc", "POST", {"requestId": rid(), "rowVersion": package["rowVersion"], "action": "start"})

    def assignment(discipline):
        d = call(root, "marc")
        return need(next((a for a in d["assignments"] if a["roundId"] == d["package"]["currentRoundId"]
                          and a["projectDisciplineId"] == discipline), None), "a current review assignment")

    def decide(who, discipline, status, rationale):
        a = assignment(discipline)
        call(f"{root}/assignments/{a['id']}/decision", who, "POST", {"requestId": rid(), "rowVersion": a["rowVersion"], "status": status,
                                                                    "rationale": f"Synthetic preview: {rationale}"})
        made(mod, f"{status} by {who}")

    attempt("SYN-101 review: Diane requires changes", lambda: decide("diane", c1, "Changes Required", "HGL is above the rim at two manholes."))
    conflict = find(f"projects/{p1}/issues", ISS_CONFLICT, "title")
    if attempt("SYN-101 review: Diane raises a blocking finding", lambda: call(f"{root}/findings", "diane", "POST", {
            "requestId": rid(), "rowVersion": call(root, "diane")["package"]["rowVersion"], "sourceRevisionId": revision["id"],
            "projectDisciplineId": c1, "resolverId": uid["jill"], "severity": "Blocking", "issueId": conflict["id"] if conflict else None,
            "text": "Synthetic preview finding: lower the outlet at the watermain crossing or add a drop structure."})):
        made(mod, "blocking finding by diane")
    attempt("SYN-101 review: Omar approves", lambda: decide("omar", g1, "Approved", "bedding and groundwater values match memo R0."))


def change_notice():
    mod = "Change notice"
    if any(s["revision"]["externalIdentifier"] == STD_ID for s in call(f"projects/{p1}/changes/options", "jordan")["sources"]):
        return skip(mod, f"{STD_ID} source revision")
    source = {"projectDisciplineId": c1, "ownerId": uid["marc"], "sourceSystem": "Synthetic standards library", "externalIdentifier": STD_ID,
              "title": STD_TITLE, "issuer": "Synthetic standards body (fictional)", "scope": "Synthetic preview: storm sewer sizing and HGL criteria."}
    base = call(f"projects/{p1}/source-revisions", "marc", "POST", {"requestId": rid(), **source, "revision": "2025-A",
                                                                    "url": f"{URL}standards/{STD_ID}-2025-A.pdf"})
    made(mod, f"{STD_ID} revision 2025-A registered by marc")
    profiles = task(p1, T_PROFILES)
    call(f"projects/{p1}/input-uses", "alex", "POST", {
        "requestId": rid(), "targetType": "Task", "targetId": profiles["id"], "targetRowVersion": profiles["rowVersion"],
        "sourceRevisionId": base["id"], "expectedCurrentRevisionId": base["id"],
        "intendedUse": "Synthetic preview: HGL criteria for the storm sewer profiles.", "reason": "Synthetic preview: recorded the revision used."})
    made(mod, f"input use on {profiles['key']} recorded by alex")
    head = need(next((h for h in call(f"projects/{p1}/changes/options", "marc")["heads"] if h["currentRevisionId"] == base["id"]), None),
                f"the {STD_ID} source head")
    notice = call(f"projects/{p1}/source-revisions", "marc", "POST", {
        "requestId": rid(), **source, "revision": "2026-B", "url": f"{URL}standards/{STD_ID}-2026-B.pdf", "supersedesId": base["id"],
        "headRowVersion": head["rowVersion"], "description": "Synthetic preview: revision 2026-B raises the minimum HGL freeboard (fictional change).",
        "effectiveDate": iso(today), "assessmentDueDate": iso(ahead(7))})
    root = f"projects/{p1}/changes/{notice['id']}"
    detail = call(root, "marc")
    made(mod, f"{detail['notice']['key']} (revision 2026-B)")
    log = task(p1, T_LOG)
    call(f"{root}/publish", "marc", "POST", {"requestId": rid(), "rowVersion": detail["notice"]["rowVersion"], "headRowVersion": detail["head"]["rowVersion"],
                                             "additionalTargets": [{"targetType": "Task", "targetId": log["id"], "rowVersion": log["rowVersion"]}]})
    made(mod, "published with assessments for alex and jill")
    assessment = need(next((a for a in call(root, "alex")["assessments"] if a["targetId"] == profiles["id"]), None),
                      f"the assessment on {profiles['key']}")
    redlines = task(p1, T_REDLINES)
    if attempt("SYN-101 change assessment recorded as Update Required by Alex", lambda: call(f"{root}/assessments/{assessment['id']}", "alex", "POST", {
            "requestId": rid(), "rowVersion": assessment["rowVersion"], "action": "disposition", "status": "Update Required",
            "rationale": "Synthetic preview: the higher freeboard changes three profile sheets.", "evidenceUrl": f"{URL}SYN-101/assessments/profiles",
            "correctionTaskId": redlines["id"], "correctionTaskRowVersion": redlines["rowVersion"], "effortImpactHours": 6, "dateImpactDays": 2})):
        made(mod, "assessment Update Required by alex")


def submission():
    mod = "Submission package"
    if find(f"projects/{p1}/submissions", SUBMISSION, "title"):
        return skip(mod, SUBMISSION)
    revision = need(current_revision(p1, deliverable(p1, D_STORM)["id"]), "registered revision of " + D_STORM)
    sub60 = milestone(p1, SUB60)
    package = call(f"projects/{p1}/submissions", "priya", "POST", {
        "requestId": rid(), "title": SUBMISSION, "purpose": "Synthetic preview: 60 % design submission to the fictional client.",
        "recipientReference": "Synthetic preview client review team (fictional)", "coordinatorId": uid["marc"], "milestoneId": sub60["id"],
        "targetDate": sub60["date"], "manifest": [{"sourceRevisionId": revision["id"]}],
        "optionalChecks": [{"label": "Synthetic preview: a traffic management plan applies to this submission", "ownerId": uid["alex"],
                            "projectDisciplineId": c1}]})
    root = f"projects/{p1}/submissions/{package['id']}"
    made(mod, call(root, "priya")["package"]["key"])
    call(f"{root}/start", "marc", "POST", {"requestId": rid(), "rowVersion": package["rowVersion"]})
    made(mod, "checking started by marc")
    detail = call(root, "alex")
    check = need(next((c for c in detail["checks"] if c["kind"] == "Applicability"), None), "the optional check")
    if attempt("SYN-101 submission optional check passed by Alex", lambda: call(f"{root}/checks/{check['id']}", "alex", "POST", {
            "requestId": rid(), "packageRowVersion": detail["package"]["rowVersion"], "rowVersion": check["rowVersion"], "action": "Pass",
            "evidenceUrl": f"{URL}SYN-101/traffic-management-plan", "reason": "Synthetic preview: plan checked against the 60 % sheets."})):
        made(mod, "optional check Pass by alex")


def design_basis():
    mod = "Design basis"
    if find(f"projects/{p1}/design-basis", BASIS_STORM, "title"):
        return skip(mod, BASIS_STORM)

    def entry(who, kind, title, owner, discipline, version, approver=None):
        e = call(f"projects/{p1}/design-basis", who, "POST", {
            "requestId": rid(), "kind": kind, "title": title, "ownerId": uid[owner], "projectDisciplineId": discipline,
            "independentApproverId": uid[approver] if approver else None, "version": version})
        made(mod, f"{title.removeprefix(P)} ({kind})")
        return e

    storm = entry("marc", "Criterion", BASIS_STORM, "jill", c1, {
        "scope": "Synthetic preview storm sewer network", "statement": "Synthetic preview: size the minor system for the 1-in-5-year storm.",
        "numericValue": 5, "units": "years", "sourceSystem": "Synthetic standards library", "stableSourceId": f"{STD_ID} §4.2",
        "sourceUrl": f"{URL}standards/{STD_ID}-2025-A.pdf", "declaredRevision": "2025-A", "confirmationDueDate": iso(ahead(5))})
    root = f"projects/{p1}/design-basis/{storm['id']}"
    detail = call(root, "marc")
    version = detail["versions"][0]["version"]
    if attempt("SYN-101 storm criterion confirmed by Marc", lambda: call(f"{root}/versions/{version['id']}/confirm", "marc", "POST", {
            "requestId": rid(), "entryRowVersion": detail["entry"]["rowVersion"], "versionRowVersion": version["rowVersion"],
            "rationale": "Synthetic preview: matches the fictional drainage standard."})):
        made(mod, "criterion confirmed by marc")
        if attempt("SYN-101 storm criterion linked to Jill's task", lambda: call(f"{root}/uses", "jill", "POST", {
                "requestId": rid(), "versionId": version["id"], "targetType": "Task", "targetId": task(p1, T_STORM)["id"],
                "intendedUse": "Synthetic preview: pipe sizing for the 60 % sheets."})):
            made(mod, "criterion used by jill's storm sewer task")
    entry("omar", "Assumption", BASIS_GROUNDWATER, "omar", g1, {
        "scope": "Synthetic preview trench excavations", "statement": "Synthetic preview: groundwater stays below the trench invert during construction.",
        "sourceSystem": "Synthetic preview borehole logs", "stableSourceId": "SYN-BH-03", "sourceUrl": f"{URL}SYN-101/boreholes",
        "declaredRevision": "R0", "confirmationDueDate": iso(ago(2))}, approver="diane")


def readiness():
    mod = "Readiness constraints"
    storm = task(p1, T_STORM)
    base = f"projects/{p1}/readiness/Task"
    if any(c["description"].startswith("Synthetic preview") for c in call(f"{base}/{storm['id']}/constraints", "jordan")):
        return skip(mod, f"Synthetic preview constraint on {storm['key']}")
    outfall = find(f"projects/{p1}/decisions", DEC_OUTFALL, "subject")
    call(f"{base}/{storm['id']}/constraints", "jill", "POST", {
        "requestId": rid(), "targetRowVersion": storm["rowVersion"], "category": "Decision",
        "description": "Synthetic preview: pipe sizing waits on the outfall decision.", "removalOwnerId": uid["priya"], "neededBy": iso(ahead(5)),
        "sourceUrl": f"{URL}SYN-101/decisions/outfall", "linkedType": "Decision" if outfall else None, "linkedId": outfall["id"] if outfall else None})
    made(mod, f"Decision constraint on {storm['key']} by jill")
    if attempt("SYN-101 storm task output defined by Jill", lambda: call(f"{base}/{storm['id']}", "jill", "POST", {
            "requestId": rid(), "targetRowVersion": task(p1, T_STORM)["rowVersion"],
            "intendedOutput": "Synthetic preview: storm sewer design sheets with the HGL check.",
            "completionCriteria": "Synthetic preview: every run sized, HGL below the rims, sheets checked."})):
        made(mod, f"output defined for {storm['key']} by jill")
    profiles = task(p1, T_PROFILES)
    capacity = call(f"{base}/{profiles['id']}/constraints", "priya", "POST", {
        "requestId": rid(), "targetRowVersion": profiles["rowVersion"], "category": "Capacity",
        "description": "Synthetic preview: Alex is over capacity next week; confirm cover before starting.", "removalOwnerId": uid["marc"],
        "neededBy": iso(ahead(2)), "sourceUrl": f"{URL}SYN-101/workload"})
    made(mod, f"Capacity constraint on {profiles['key']} by priya")
    if attempt("SYN-101 capacity constraint resolution proposed by Marc", lambda: call(
            f"{base}/{profiles['id']}/constraints/{capacity['id']}/transition", "marc", "POST", {
                "requestId": rid(), "rowVersion": capacity["rowVersion"], "toState": "Resolution Proposed",
                "reason": "Synthetic preview: Jill covers the sanitary profiles next week.", "evidenceUrl": f"{URL}SYN-101/allocations"})):
        made(mod, "resolution proposed by marc")


def weekly_commitments():
    mod = "Weekly commitments"
    if any(c["intendedOutput"].startswith("Synthetic preview") for c in call(f"projects/{p1}/weekly-commitments", "jordan")["commitments"]):
        return skip(mod, "Synthetic preview promise")

    def propose(who, name, target, output, criteria):
        t = task(p1, name)
        call(f"projects/{p1}/weekly-commitments/Task/{t['id']}", who, "POST", {
            "requestId": rid(), "targetRowVersion": t["rowVersion"], "weekStart": iso(day(1)), "targetDate": iso(target),
            "intendedOutput": f"Synthetic preview: {output}", "completionCriteria": f"Synthetic preview: {criteria}"})
        made(mod, f"{t['key']} for the week of {day(1)} proposed by {who}")

    propose("priya", T_PROFILES, day(1, 3), "storm sewer profiles for sheets C-201 to C-204.", "profiles drawn and checked against the HGL table.")
    propose("jill", T_STORM, day(1, 4), "HGL check summary for the 60 % review.", "summary table issued to the design team.")


def template():
    mod = "Template"
    if any(t["name"] == TEMPLATE for t in call("templates", "jordan")["templates"]):
        return skip(mod, TEMPLATE)
    t = call("templates", "jordan", "POST", {"name": TEMPLATE, "description": "Synthetic preview template for local UI review; not a real standard."})
    made(mod, TEMPLATE.removeprefix(P) + " (Draft)")
    call(f"templates/{t['id']}/structure", "jordan", "PUT", {
        "rowVersion": t["rowVersion"],
        "disciplines": [{"disciplineId": disc["CIV"], "isDefaultIncluded": True}, {"disciplineId": disc["GEO"], "isDefaultIncluded": False}],
        "milestones": [{"ref": "m1", "name": P + "Kickoff", "milestoneType": "Kickoff", "anchor": "ProjectStart", "offset": 0, "isClientFacing": False},
                       {"ref": "m2", "name": P + "60 % submission", "milestoneType": "Design Submission", "anchor": "PreviousMilestone",
                        "offset": 42, "isClientFacing": True}],
        "deliverables": [{"ref": "d1", "disciplineId": disc["CIV"], "name": P + "Site servicing drawings", "deliverableTypeId": dtype["Drawing Package"],
                          "milestoneRef": "m2", "offset": -3, "requiresReview": True},
                         {"ref": "d2", "disciplineId": disc["GEO"], "name": P + "Geotechnical memo", "deliverableTypeId": dtype["Memo"],
                          "milestoneRef": "m2", "offset": -14, "requiresReview": False}],
        "tasks": [{"ref": "t1", "disciplineId": disc["CIV"], "deliverableRef": "d1", "name": P + "Grading and servicing layout", "requiresReview": True,
                   "estimatedHours": 24, "offset": -10, "assignTo": "DisciplineLead"},
                  {"ref": "t2", "disciplineId": disc["CIV"], "deliverableRef": "d1", "name": P + "Drawing QA check", "requiresReview": False,
                   "estimatedHours": 6, "offset": -4, "assignTo": "Unassigned"},
                  {"ref": "t3", "disciplineId": disc["GEO"], "deliverableRef": "d2", "name": P + "Borehole log summary", "requiresReview": False,
                   "estimatedHours": 12, "offset": -5, "assignTo": "DisciplineLead"}],
        "dependencies": [{"predecessor": "t1", "successor": "t2"}]})
    made(mod, "structure: 2 disciplines, 2 milestones, 2 deliverables, 3 tasks, 1 dependency")


for module in (milestones_and_deliverables, decisions, risks_and_issues, meetings, comments, calendar_events, links, time_entries,
               handoffs, review_package, change_notice, submission, design_basis, readiness, weekly_commitments, template):
    attempt(module.__name__, module)

if created:
    time.sleep(2)  # the evaluation worker recomputes milestone status a moment after each save
print(f"Synthetic preview module data at {BASE} (organisation today {today}):")
for module, labels in created.items():
    print(f"  {module}: {len(labels)} created — {'; '.join(labels)}")
if not created:
    print("  Nothing created.")
for pid, number in zip((p1, p2, p3), NUMBERS):
    rows = call(f"projects/{pid}/milestones?showCompleted=true", "jordan")
    print(f"  {number} milestones: " + ("; ".join(f"{m['key']} {m['status']}" + (f" (slipped {m['slipDays']} d)" if m["slipDays"] else "")
                                                  for m in rows) or "none"))
print("  Unread notifications: " + ", ".join(f"{u} {call('me/notifications/unread-count', u)['notifications']}" for u in DEV_USERS))
for s in skipped:
    print(f"  SKIPPED {s}")
for r in refusals:
    print(f"  REFUSED {r}")
