import datetime as dt
import importlib.util
from pathlib import Path
import tarfile
import unittest

spec = importlib.util.spec_from_file_location('freshness', Path(__file__).with_name('check-offhost-backup.py'))
freshness = importlib.util.module_from_spec(spec)
spec.loader.exec_module(freshness)


class FreshnessTests(unittest.TestCase):
    def member(self, name, size=42):
        value = tarfile.TarInfo(name)
        value.size = size
        return value

    def test_fresh_stale_missing_and_future(self):
        now = dt.datetime(2026, 10, 18, 22, tzinfo=dt.timezone.utc)
        for stamp, expected in [('20261018T220000123456Z',0), ('20261017T210000Z',0), ('20261017T190000Z',1), ('20261019T220000Z',1)]:
            member = self.member('home/jay/Workspace/Projects/pm-tool/data/backups/hub-review-'+stamp+'.dump')
            code, lines = freshness.check([member], now)
            self.assertEqual(code, expected, lines)
            self.assertIn('review:', lines[0])
        self.assertEqual(freshness.check([], now)[0], 1)

    def test_pilot_absent_without_approval_and_no_secret_printed(self):
        now = dt.datetime(2026,10,18,22,tzinfo=dt.timezone.utc)
        members = [self.member('pm-tool/data/backups/hub-review-20261018T220000Z.dump'), self.member('pm-tool/.runtime/SECRET-do-not-print')]
        self.assertEqual(freshness.check(members, now)[0], 0)
        members.append(self.member('pm-tool-pilot/.runtime/private.env'))
        code, lines = freshness.check(members, now)
        self.assertEqual(code, 1)
        self.assertIn('pilot', lines[0])
        self.assertNotIn('SECRET', '\n'.join(lines))
        self.assertNotIn('private.env', '\n'.join(lines))


if __name__ == '__main__':
    unittest.main()
