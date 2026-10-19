#!/usr/bin/env python3
import json
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

import ratchet


class RatchetTests(unittest.TestCase):
    def run_main(self, baseline, current, *args):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "ratchet.json"
            path.write_text(json.dumps(baseline), encoding="utf-8")
            with patch.object(ratchet, "BASELINE", path), patch.object(ratchet, "count_sources", return_value=current), patch.object(ratchet, "hard_checks", return_value=[]):
                return ratchet.main(list(args)), json.loads(path.read_text())

    def test_report_only_never_fails(self):
        code, _ = self.run_main({"counters": {"x": {"value": 1, "ratchet": False}}}, {"x": 2})
        self.assertEqual(code, 0)

    def test_ratchet_rise_fails(self):
        code, _ = self.run_main({"counters": {"x": {"value": 1, "ratchet": True}}}, {"x": 2})
        self.assertEqual(code, 1)

    def test_update_lowers_but_refuses_raise(self):
        code, data = self.run_main({"counters": {"x": {"value": 2, "ratchet": True}}}, {"x": 1}, "--update")
        self.assertEqual(code, 0)
        self.assertEqual(data["counters"]["x"]["value"], 1)
        code, _ = self.run_main({"counters": {"x": {"value": 1, "ratchet": True}}}, {"x": 2}, "--update")
        self.assertEqual(code, 1)

class SourceChecks(unittest.TestCase):
    def test_real_text_syntax_is_parsed_and_one_sided_changes_fail(self):
        self.assertEqual({'itemType.Action': 'Action'}, ratchet._parse_text('["itemType.Action"] = "Action",', True))
        self.assertEqual({'itemType.Action': 'Meeting action'}, ratchet._parse_text("'itemType.Action': 'Meeting action',", False))
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            (root / 'src/Hub.Api').mkdir(parents=True)
            (root / 'web/src/i18n').mkdir(parents=True)
            (root / 'web/src/pages').mkdir(parents=True)
            (root / 'src/Hub.Api/Text.cs').write_text('["demo"] = "same",')
            client = root / 'web/src/i18n/en.ts'
            client.write_text("'demo': 'same',")
            baseline = root / 'baseline.json'; baseline.write_text('{"text_differences": {}}')
            with patch.object(ratchet, 'ROOT', root), patch.object(ratchet, 'BASELINE', baseline):
                self.assertEqual([], ratchet.hard_checks())
                client.write_text("'demo': 'changed',")
                self.assertIn('text mismatch: demo', ratchet.hard_checks())
                (root / 'web/src/pages/Demo.tsx').write_text("<ViewMenu />; sp.get('view')")
                self.assertTrue(any('reserved view parameter' in x for x in ratchet.hard_checks()))

    def test_figure_counters_find_seeded_regressions_and_fail(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            page = root / 'web/src/pages/Demo.tsx'
            page.parent.mkdir(parents=True)
            page.write_text('cell.pct > 110; 90 < cell.pct; permissions.isPm; permissions.leadOf; options.manageDisciplineIds.length; options.manageDisciplineIds.includes(id)')
            with patch.object(ratchet, 'ROOT', root): values = ratchet.count_sources()
            self.assertEqual(2, values['load_percentage_literals'])
            self.assertEqual(4, values['page_permission_derivations'])
            baseline = {'counters': {name: {'value': 0, 'ratchet': True} for name in ['load_percentage_literals', 'page_permission_derivations']}}
            code, _ = RatchetTests().run_main(baseline, {name: values[name] for name in baseline['counters']})
            self.assertEqual(1, code)


if __name__ == "__main__":
    unittest.main()
