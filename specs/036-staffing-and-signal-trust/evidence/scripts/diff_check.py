"""Compare before/after captures, ignoring only the documented new fields. Exit 1 on any other difference."""
import json, os, sys
before, after = sys.argv[1], sys.argv[2]
NEW_PERSON = {"officeId", "jobTitle", "homeDisciplineId"}

def strip(name, v):
    v = json.loads(json.dumps(v))
    if name.startswith("workload-"):
        v.pop("unfilledRoles", None)
        for p in v["people"]:
            for k in NEW_PERSON:
                p.pop(k, None)
    if name.startswith("api-state-"):
        v.pop("evaluatedAt", None)
    if name == "db-attention_item.json" or name.startswith("api-attention-"):
        items = v if isinstance(v, list) else v.get("items", v)
        # new A-21 items are allowed after the release
        if isinstance(items, list):
            items[:] = [i for i in items if (i.get("rule_id") or i.get("ruleId")) != "A-21"]
    return v

bad = 0
for name in sorted(os.listdir(before)):
    a, b = (json.load(open(f"{d}/{name}")) for d in (before, after))
    added = None
    if name.startswith("workload-"):
        added = sorted({k for p in b["people"] for k in p} - {k for p in a["people"] for k in p}), sorted(set(b) - set(a))
    same = strip(name, a) == strip(name, b)
    bad += not same
    print(f"{'SAME' if same else 'DIFF'} {name}" + (f"  (new person fields {added[0]}, new top-level {added[1]})" if added else ""))
a21 = [i for i in json.load(open(f"{after}/db-attention_item.json")) if i["rule_id"] == "A-21"]
print(f"A-21 items after: {len(a21)}; files differing beyond the documented new fields: {bad}")
sys.exit(1 if bad else 0)
