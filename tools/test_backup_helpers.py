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

    @contextlib.contextmanager
    def review_sandbox(self):
        """A fake review release whose sudo and docker run locally; docker logs each call in FAKE_IO."""
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
open(os.path.join(root, 'calls.log'), 'a').write(' '.join(sys.argv[1:]) + '\\n')
if sys.argv[1] == 'inspect':
    fmt = sys.argv[3]
    print(os.environ.get('FAKE_PROJECT', 'pm-tool-review') if 'project' in fmt else 'db' if 'service' in fmt else 'pm-tool-review-db')
    sys.exit(0)
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
            yield sandbox, env, io_dir

    def test_batch_backup_and_restore_do_not_forward_terminal_stdin(self):
        with self.review_sandbox() as (sandbox, env, io_dir):
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

    def test_review_backup_refuses_an_unexpected_database(self):
        with self.review_sandbox() as (sandbox, env, io_dir):
            result = subprocess.run(['bash', str(ROOT / 'scripts/backup-homedev-review.sh')], cwd=sandbox,
                                    env={**env, 'FAKE_PROJECT': 'pm-tool-pilot'}, capture_output=True, text=True)
            self.assertNotEqual(result.returncode, 0)
            self.assertIn('unexpected database', result.stderr)
            self.assertNotIn('pg_dump', (io_dir / 'calls.log').read_text())
            self.assertFalse((sandbox / 'data/backups').exists())

    def test_review_restore_refuses_unsafe_dumps_before_creating_a_database(self):
        with self.review_sandbox() as (sandbox, env, io_dir):
            name = 'hub-review-20261025T010203123456789Z.dump'
            (sandbox / 'data/backups').mkdir(parents=True)
            good = sandbox / 'data/backups' / name
            good.write_bytes(b'synthetic-dump')
            good.chmod(0o600)
            link = sandbox / 'link' / name
            link.parent.mkdir()
            link.symlink_to(good)
            renamed = sandbox / 'data/backups/other.dump'
            renamed.write_bytes(b'synthetic-dump')
            renamed.chmod(0o600)
            pilot = sandbox / 'pilot' / name
            pilot.parent.mkdir()
            pilot.write_bytes(b'synthetic-dump')
            pilot.chmod(0o600)
            loose = sandbox / 'loose' / name
            loose.parent.mkdir()
            loose.write_bytes(b'synthetic-dump')
            loose.chmod(0o644)
            cases = [(link, env, 'symlink'), (renamed, env, 'not a review dump'), (pilot, env, 'pilot dump path'),
                     (loose, env, 'owner-only'), (good, {**env, 'FAKE_PROJECT': 'pm-tool-pilot'}, 'identity')]
            for dump, case_env, reason in cases:
                with self.subTest(reason):
                    result = subprocess.run(['bash', str(ROOT / 'scripts/restore-homedev-review-drill.sh'), str(dump)],
                                            cwd=sandbox, env=case_env, capture_output=True, text=True)
                    self.assertNotEqual(result.returncode, 0)
                    self.assertIn('refused', result.stderr)
                    self.assertIn(reason, result.stderr)
            calls = io_dir / 'calls.log'
            self.assertNotIn('createdb', calls.read_text() if calls.exists() else '')


class OffhostDrillTests(unittest.TestCase):
    """AC-OPS-04/05: wrapper stays on the temporary review restore path and cleans scratch."""
    def test_review_delegation_cleanup_on_success_and_failure(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            (root/'scripts').mkdir()
            source = root/'hub-review-20261018T220000123456Z.dump'
            source.write_bytes(b'synthetic')
            helper = root/'scripts/restore-homedev-review-drill.sh'
            script = ROOT/'scripts/offhost-restore-drill.sh'
            for status in (0,42):
                helper.write_text('echo "$1" > scratch-path\nexit '+str(status)+'\n')
                result=subprocess.run(['bash',str(script),'review',str(source)],cwd=root,capture_output=True,text=True)
                self.assertEqual(result.returncode,status,result.stderr)
                scratch=Path((root/'scratch-path').read_text().strip())
                self.assertFalse(scratch.exists())
                self.assertTrue(source.exists())
                self.assertIn('restore-homedev-review-drill.sh',script.read_text())
                self.assertNotIn('docker volume',script.read_text())

    def test_cross_stack_and_unapproved_pilot_fail_before_helper(self):
        with tempfile.TemporaryDirectory() as folder:
            root=Path(folder);(root/'scripts').mkdir()
            helper=root/'scripts/restore-homedev-review-drill.sh'
            helper.write_text('touch called\n')
            dump=root/'hub-pilot-20261018T220000123456Z.dump';dump.write_text('synthetic')
            for stack in ['review','pilot']:
                result=subprocess.run(['bash',str(ROOT/'scripts/offhost-restore-drill.sh'),stack,str(dump)],cwd=root,capture_output=True)
                self.assertNotEqual(result.returncode,0)
                self.assertFalse((root/'called').exists())


if __name__ == '__main__':
    unittest.main()
