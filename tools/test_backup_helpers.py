"""Exercise root backup pathname defenses without Docker or root privileges."""
import contextlib
import io
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[1]


class BackupHelperTests(unittest.TestCase):
    def run_helper(self, folder, attack=False):
        source = (ROOT / 'hosting/pm-tool-review-backup-root.sh').read_text().split("<<'BACKUP_PY'\n", 1)[1].rsplit('BACKUP_PY', 1)[0]
        protected = folder.parent / 'protected'
        protected.write_bytes(b'untouched')
        validated = []

        def docker(argv, **kw):
            if 'pg_dump' in argv:
                os.write(kw['stdout'], b'synthetic dump')
                if attack:
                    partial = next(folder.glob('*.partial.*'))
                    partial.unlink()
                    partial.symlink_to(protected)
            else:
                validated.append(os.read(kw['stdin'], 100))
            return subprocess.CompletedProcess(argv, 0)

        with patch.object(sys, 'argv', ['backup', str(folder), 'container', 'database', 'hub-review-']), \
             patch('subprocess.run', side_effect=docker), patch('os.fchown') as ownership, \
             contextlib.redirect_stdout(io.StringIO()):
            if attack:
                with self.assertRaisesRegex(RuntimeError, 'changed during creation'):
                    exec(compile(source, 'backup-helper', 'exec'), {})
                ownership.assert_not_called()
            else:
                exec(compile(source, 'backup-helper', 'exec'), {})
                ownership.assert_called_once()
                self.assertEqual(next(folder.glob('*.dump')).read_bytes(), b'synthetic dump')
        self.assertEqual(validated, [b'synthetic dump'])
        self.assertEqual(protected.read_bytes(), b'untouched')

    def test_verified_dump_is_published(self):
        with tempfile.TemporaryDirectory(dir='/private/tmp') as name:
            folder = Path(name) / 'backups'
            folder.mkdir(mode=0o700)
            self.run_helper(folder)

    def test_replaced_partial_cannot_redirect_validation_or_chown(self):
        with tempfile.TemporaryDirectory(dir='/private/tmp') as name:
            folder = Path(name) / 'backups'
            folder.mkdir(mode=0o700)
            self.run_helper(folder, attack=True)
            self.assertFalse(list(folder.glob('*.dump')))

    def test_parent_symlink_is_refused(self):
        source = (ROOT / 'hosting/pm-tool-review-backup-root.sh').read_text().split("<<'BACKUP_PY'\n", 1)[1].rsplit('BACKUP_PY', 1)[0]
        with tempfile.TemporaryDirectory(dir='/private/tmp') as name:
            actual = Path(name) / 'actual'
            actual.mkdir(mode=0o700)
            link = Path(name) / 'alias'
            link.symlink_to(actual, target_is_directory=True)
            with patch.object(sys, 'argv', ['backup', str(link), 'container', 'database', 'prefix']), patch('subprocess.run') as docker:
                with self.assertRaises(OSError):
                    exec(compile(source, 'backup-helper', 'exec'), {})
                docker.assert_not_called()


if __name__ == '__main__':
    unittest.main()
