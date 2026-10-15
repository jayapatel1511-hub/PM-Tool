#!/usr/bin/env python3
"""Measures the §22 response targets (packet 011 FR-013, US5) against an API loaded with tools/scale/seed.sql.

Usage: python3 tools/scale/measure.py [base-url] [concurrent-users] [rounds] [think-seconds]
Signs in with the development header, so it only works against a development instance; never aim it at a real one.
Prints p50/p95/max per request type for N concurrent users each running the scenario R times without pauses, then the
time for a change on the 2,000-task project to reach its derived state. With think-seconds, each person pauses a random
0.5x to 1.5x that long between requests, as people do between screens; 0 is a stress test."""
import concurrent.futures as cf
import json
import random
import statistics
import sys
import time
import urllib.request

# Packet 034 is read-only measurement against the small synthetic preview fixture.
if len(sys.argv) > 1 and sys.argv[1] == '--planning':
    import platform
    from pathlib import Path
    sys.path.insert(0, str(Path(__file__).resolve().parents[1] / 'preview'))
    from preview_target import verify_preview_target
    base = sys.argv[2] if len(sys.argv) > 2 else 'http://localhost:5080'
    verify_preview_target(base)
    rounds = int(sys.argv[3]) if len(sys.argv) > 3 else 30
    if rounds < 20:
        sys.exit('Use at least 20 measured samples for p95 evidence.')
    def request(route, user):
        req = urllib.request.Request(f'{base}/api/v1/{route}', headers={'X-Dev-User': user})
        before = time.perf_counter()
        with urllib.request.urlopen(req, timeout=30) as response:
            result = json.load(response)
        return (time.perf_counter()-before)*1000, result
    _, person = request('me', 'planning-scale-1@hub.test')
    paths = [('Planner grid', 'planning/grid?weeks=12', 'planning-scale-supervisor@hub.test', 1500),
             ('My Week', f"planning/grid?personId={person['id']}&weeks=6&includeMyDrafts=false", 'planning-scale-1@hub.test', 500)]
    output = {'synthetic': True, 'base': base, 'platform': platform.platform(), 'machine': platform.machine(), 'samples': rounds, 'results': []}
    for name, route, user, limit in paths:
        _, warm = request(route,user)
        if len(warm.get('people',[])) != (12 if name=='Planner grid' else 1):
            sys.exit(f'{name}: fixture scope differs from required 12 people / one self row')
        samples = sorted(request(route,user)[0] for _ in range(rounds))
        p95 = samples[min(len(samples)-1, int(len(samples)*0.95))]
        output['results'].append({'name':name,'people':len(warm['people']),'weeks':len(warm['weeks']),'p50_ms':statistics.median(samples),'p95_ms':p95,'max_ms':samples[-1],'limit_ms':limit,'pass':p95<=limit})
    print(json.dumps(output,indent=2))
    sys.exit(0 if all(x['pass'] for x in output['results']) else 1)

BASE = sys.argv[1] if len(sys.argv) > 1 else "http://localhost:5081"
USERS = int(sys.argv[2]) if len(sys.argv) > 2 else 100
ROUNDS = int(sys.argv[3]) if len(sys.argv) > 3 else 5
THINK = float(sys.argv[4]) if len(sys.argv) > 4 else 0


def call(path, user, method="GET", body=None, headers=None, parse=True):
    req = urllib.request.Request(f"{BASE}/api/v1/{path}", method=method, data=None if body is None else json.dumps(body).encode(),
                                 headers={"X-Dev-User": user, "Content-Type": "application/json", **(headers or {})})
    t = time.perf_counter()
    with urllib.request.urlopen(req, timeout=120) as r:
        data = r.read()
    ms = (time.perf_counter() - t) * 1000
    return ms, (json.loads(data) if data and parse else None)


def timed(path, user):
    """A request as one person makes it: the response is read in full but not parsed (parsing would load the client)."""
    if THINK:
        time.sleep(THINK * random.uniform(0.5, 1.5))
    return call(path, user, parse=False)[0]


def project(number):
    return call(f"projects?q={number}&mine=false&pageSize=5", "priya@hub.test")[1]["items"][0]


big, two = project("S0001"), project("S0002")
some = [project(f"S{n:04d}") for n in (3, 50, 120, 250, 400)]
task_ids = [t["id"] for t in call(f"projects/{big['id']}/tasks?pageSize=200", "priya@hub.test")[1]["items"][:50]]
people = [f"scale{n}@hub.test" for n in range(61, 481)] + ["alex@hub.test", "marc@hub.test"]


def scenario(user):
    """One person's burst: My Work, Home, a dashboard, the 500-task list (three pages), lists, an item and a search."""
    out = []
    p = random.choice(some)
    out.append(("My Work", timed("me/work", user)))
    out.append(("Home (my projects)", timed("home?projects=mine", user)))
    out.append(("Project dashboard (230 tasks)", timed(f"projects/{p['id']}/dashboard", user)))
    out.append(("Project dashboard (5,000 tasks)", timed(f"projects/{big['id']}/dashboard", user)))
    t = sum(call(f"projects/{big['id']}/tasks?pageSize=200&page={page}", user, parse=False)[0] for page in (1, 2, 3))
    out.append(("500-task list (3 pages)", t))
    out.append(("List read: projects", timed("projects?pageSize=50", user)))
    out.append(("List read: tasks page", timed(f"projects/{p['id']}/tasks?pageSize=50", user)))
    out.append(("Item read: task", timed(f"tasks/{random.choice(task_ids)}", user)))
    out.append(("Search", timed(f"search?q=Scale+task+{random.randint(1, 230)}", user)))
    return out


def worker(i):
    user = people[i % len(people)]
    rows = []
    if THINK:
        time.sleep(random.uniform(0, THINK * 3))  # people arrive over a few seconds, not in the same instant
    for _ in range(ROUNDS):
        rows += scenario(user)
    return rows


start = time.perf_counter()
with cf.ThreadPoolExecutor(max_workers=USERS) as pool:
    results = [r for rows in pool.map(worker, range(USERS)) for r in rows]
elapsed = time.perf_counter() - start

by = {}
for name, ms in results:
    by.setdefault(name, []).append(ms)
print(f"{USERS} concurrent users x {ROUNDS} rounds, think time {THINK:g} s, {len(results)} measurements in {elapsed:.0f} s")
print(f"{'request':34} {'n':>5} {'p50 ms':>8} {'p95 ms':>8} {'max ms':>8}")
for name, xs in by.items():
    xs.sort()
    p95 = xs[min(len(xs) - 1, int(len(xs) * 0.95))]
    print(f"{name:34} {len(xs):5} {statistics.median(xs):8.0f} {p95:8.0f} {xs[-1]:8.0f}")

# A change on the 2,000-task project reflected in its derived state (target 2 s).
row = call(f"projects/{two['id']}/tasks?pageSize=1", "priya@hub.test")[1]["items"][0]
before = row["state"]["evaluatedAt"] if row.get("state") else ""
due = "2027-01-15" if row.get("dueDate") != "2027-01-15" else "2027-01-22"
t0 = time.perf_counter()
call(f"tasks/{row['id']}", "priya@hub.test", "PATCH", {"dueDate": due, "reason": "Scale test date change"}, {"If-Match": f"\"{row['rowVersion']}\""})
while True:
    now = call(f"projects/{two['id']}/tasks?ids={row['id']}", "priya@hub.test")[1]["items"][0]
    if now.get("state") and now["state"]["evaluatedAt"] != before:
        break
    if time.perf_counter() - t0 > 60:
        print("derived state not updated within 60 s")
        sys.exit(1)
    time.sleep(0.1)
print(f"Change on the 2,000-task project reflected in derived state after {(time.perf_counter() - t0) * 1000:.0f} ms")
