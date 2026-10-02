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
        with tempfile.TemporaryDirectory(dir=Path(tempfile.gettempdir()).resolve()) as name:
            folder = Path(name) / 'backups'
            folder.mkdir(mode=0o700)
            self.run_helper(folder)

    def test_replaced_partial_cannot_redirect_validation_or_chown(self):
        with tempfile.TemporaryDirectory(dir=Path(tempfile.gettempdir()).resolve()) as name:
            folder = Path(name) / 'backups'
            folder.mkdir(mode=0o700)
            self.run_helper(folder, attack=True)
            self.assertFalse(list(folder.glob('*.dump')))

    def test_parent_symlink_is_refused(self):
        source = (ROOT / 'hosting/pm-tool-review-backup-root.sh').read_text().split("<<'BACKUP_PY'\n", 1)[1].rsplit('BACKUP_PY', 1)[0]
        with tempfile.TemporaryDirectory(dir=Path(tempfile.gettempdir()).resolve()) as name:
            actual = Path(name) / 'actual'
            actual.mkdir(mode=0o700)
            link = Path(name) / 'alias'
            link.symlink_to(actual, target_is_directory=True)
            with patch.object(sys, 'argv', ['backup', str(link), 'container', 'database', 'prefix']), patch('subprocess.run') as docker:
                with self.assertRaises(OSError):
                    exec(compile(source, 'backup-helper', 'exec'), {})
                docker.assert_not_called()

    def test_batch_backup_and_restore_do_not_forward_terminal_stdin(self):
        with tempfile.TemporaryDirectory(dir=Path(tempfile.gettempdir()).resolve()) as name:
            sandbox = Path(name)
            (sandbox / 'hosting').mkdir()
            (sandbox / '.runtime').mkdir()
            (sandbox / 'hosting' / 'homedev.compose.yml').write_text('services: {}\n')
            (sandbox / '.runtime' / 'review.env').write_text('DB=synthetic\n')
            fake_bin = sandbox / 'bin'
            fake_bin.mkdir()
            (fake_bin / 'sudo').write_text('#!/usr/bin/env python3\nimport os, sys\nos.execvp(sys.argv[1], sys.argv[1:])\n')
            (fake_bin / 'docker').write_text("""#!/usr/bin/env python3
import os, sys
root = os.environ['FAKE_IO']
name = ' '.join(sys.argv[1:])
safe = str(len(os.listdir(root))) + '.stdin'
payload = sys.stdin.buffer.read()
open(os.path.join(root, safe), 'wb').write(payload)
if 'pg_dump' in sys.argv:
    sys.stdout.buffer.write(b'synthetic-dump')
elif 'psql' in sys.argv:
    sys.stdout.write('2\\n')
""")
            (fake_bin / 'sudo').chmod(0o755)
            (fake_bin / 'docker').chmod(0o755)
            io_dir = sandbox / 'io'
            io_dir.mkdir()
            env = {**os.environ, 'PATH': str(fake_bin) + os.pathsep + os.environ['PATH'], 'FAKE_IO': str(io_dir)}
            backup = ROOT / 'scripts/backup-homedev-review.sh'
            restore = ROOT / 'scripts/restore-homedev-review-drill.sh'
            terminal = b'terminal-input-must-not-reach-batch-container'
            backed_up = subprocess.run(['bash', str(backup)], cwd=sandbox, env=env, input=terminal, capture_output=True)
            self.assertEqual(backed_up.returncode, 0, backed_up.stderr.decode())
            dump = next((sandbox / 'data/backups').glob('*.dump'))
            self.assertEqual(dump.read_bytes(), b'synthetic-dump')
            restored = subprocess.run(['bash', str(restore), str(dump)], cwd=sandbox, env=env, input=terminal, capture_output=True)
            self.assertEqual(restored.returncode, 0, restored.stderr.decode())
            payloads = [p.read_bytes() for p in io_dir.glob('*.stdin')]
            self.assertIn(b'synthetic-dump', payloads)  # pg_restore receives the explicit dump file.
            self.assertNotIn(terminal, payloads)
            self.assertEqual(payloads.count(b''), 4)  # pg_dump, createdb, psql and cleanup receive no terminal input.


if __name__ == '__main__':
    unittest.main()
