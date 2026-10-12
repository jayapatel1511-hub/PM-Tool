"""Run a tools/preview seed script against the temporary acceptance API (not the shared preview).
Usage: python3 run_seed.py <tools/preview dir> <script.py> <base-url>"""
import runpy, sys
tools, script, base = sys.argv[1:4]
assert base.startswith("http://localhost:509"), "temporary acceptance API only"
sys.path.insert(0, tools)
import preview_target, fixture_api
preview_target.verify_preview_target = fixture_api.verify_preview_target = lambda b: None
sys.argv = [script, base]
runpy.run_path(f"{tools}/{script}", run_name="__main__")
