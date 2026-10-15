"""Capture /workload and evaluator output from the temporary acceptance API and database.
Usage: python3 capture.py <base-url> <out-dir>
Forces a fresh evaluation of every project first (marks project_state as evaluated two days ago, then reads /state)."""
import json, os, subprocess, sys, urllib.request
base, out = sys.argv[1], sys.argv[2]
assert base.startswith("http://localhost:509")
os.makedirs(out, exist_ok=True)

def sql(q):
    return subprocess.check_output(["docker", "exec", "pm-acc036-temp", "psql", "-U", "hub", "-d", "hub", "-Atc", q], text=True)

def get(path, who):
    r = urllib.request.Request(f"{base}/api/v1/{path}", headers={"X-Dev-User": f"{who}@hub.test"})
    with urllib.request.urlopen(r) as resp:
        return json.loads(resp.read() or "null")

def save(name, value):
    with open(f"{out}/{name}.json", "w") as f:
        json.dump(value, f, indent=1, sort_keys=True)

projects = [l.split("|") for l in sql("select id, project_number from hub.project order by project_number").split()]
sql("update hub.project_state set evaluated_at = evaluated_at - interval '2 days'")
for pid, num in projects:
    get(f"projects/{pid}/state", "jordan")
stale = sql("select count(*) from hub.project_state where evaluated_at < now() - interval '1 day'").strip()
print("projects", len(projects), "still stale after re-evaluation:", stale)

TABLES = {  # table -> (key column, columns left out)
    "task_state": ("task_id", ["evaluated_at"]),
    "deliverable_state": ("deliverable_id", ["evaluated_at"]),
    "milestone_state": ("milestone_id", ["evaluated_at"]),
    "decision_state": ("decision_id", ["evaluated_at"]),
    "project_state": ("project_id", ["evaluated_at"]),
    "attention_item": ("rule_id, item_type, item_id", ["id", "first_detected_at", "last_evaluated_at"]),
}
for t, (key, drop) in TABLES.items():
    minus = "".join(f" - '{c}'" for c in drop)
    rows = sql(f"select coalesce(jsonb_agg(to_jsonb(x){minus} order by {key}), '[]') from hub.{t} x")
    save(f"db-{t}", json.loads(rows))
for pid, num in projects:
    save(f"api-attention-{num}", get(f"projects/{pid}/attention?includeSnoozed=true", "jordan"))
    save(f"api-state-{num}", get(f"projects/{pid}/state", "jordan"))
for who in ("jordan", "lena", "sam", "priya", "marc"):
    save(f"workload-{who}", get("workload", who))
print("saved to", out)
