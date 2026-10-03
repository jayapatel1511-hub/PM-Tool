import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest

SCRIPT = Path(__file__).resolve().parents[1] / 'scripts/init-homedev-pilot.py'

class PilotInitializerTests(unittest.TestCase):
    def run_init(self, path, name='Synthetic Operator'):
        return subprocess.run([sys.executable, str(SCRIPT), '--runtime', str(path), '--admin-email', 'operator@example.test', '--admin-name', name], capture_output=True, text=True)

    def test_private_separate_settings_and_no_overwrite(self):
        with tempfile.TemporaryDirectory() as d:
            path = Path(d) / '.runtime'
            result = self.run_init(path)
            self.assertEqual(result.returncode, 0)
            env = (path / 'pilot.env').read_text()
            self.assertIn('Database=hub_pilot;Username=hub_pilot;', env)
            self.assertIn('Auth__Local__BootstrapAdmins=operator@example.test|Synthetic Operator', env)
            app = (path / 'pilot-app.env').read_text()
            self.assertRegex(app, r'^ConnectionStrings__Hub=Host=db;Port=5432;Database=hub_pilot;Username=hub_pilot_app;Password=[A-Za-z0-9_-]+\n$')
            self.assertNotIn(env.split('PILOT_DB_PASSWORD=')[1].splitlines()[0], app)
            self.assertNotIn(app.split('Password=')[1].strip(), result.stdout + result.stderr)
            self.assertEqual(os.stat(path / 'pilot-app.env').st_mode & 0o777, 0o600)
            self.assertNotIn('hub_review', env)
            self.assertNotIn(env.split('PILOT_DB_PASSWORD=')[1].splitlines()[0], result.stdout + result.stderr)
            self.assertEqual(os.stat(path / 'pilot.env').st_mode & 0o777, 0o600)
            self.assertEqual(os.stat(path / 'keys').st_mode & 0o777, 0o700)
            self.assertEqual(json.loads((path / 'pilot-users.json').read_text()), {'users': []})
            self.assertNotEqual(self.run_init(path).returncode, 0)
            self.assertEqual((path / 'pilot.env').read_text(), env)
            self.assertEqual((path / 'pilot-app.env').read_text(), app)

    def test_explicit_legacy_upgrade_only_adds_new_credential_and_refuses_overwrite(self):
        with tempfile.TemporaryDirectory() as d:
            path = Path(d) / '.runtime'
            self.assertEqual(self.run_init(path).returncode, 0)
            (path / 'pilot-app.env').unlink()  # synthetic legacy runtime
            before = {p.name: (p.read_bytes(), p.stat().st_ino) for p in path.iterdir() if p.is_file()}
            command = [sys.executable, str(SCRIPT), '--runtime', str(path), '--add-app-credential']
            result = subprocess.run(command, capture_output=True, text=True)
            self.assertEqual(result.returncode, 0, result.stderr)
            app = (path / 'pilot-app.env').read_bytes()
            for name, (content, inode) in before.items():
                self.assertEqual((path / name).read_bytes(), content)
                self.assertEqual((path / name).stat().st_ino, inode)
            self.assertNotEqual(subprocess.run(command, capture_output=True).returncode, 0)
            self.assertEqual((path / 'pilot-app.env').read_bytes(), app)

    def test_app_credential_symlink_is_never_replaced_or_followed(self):
        with tempfile.TemporaryDirectory() as d:
            path = Path(d) / '.runtime'
            self.assertEqual(self.run_init(path).returncode, 0)
            (path / 'pilot-app.env').unlink()
            protected = Path(d) / 'protected'
            protected.write_text('preserved')
            (path / 'pilot-app.env').symlink_to(protected)
            result = subprocess.run([sys.executable, str(SCRIPT), '--runtime', str(path), '--add-app-credential'], capture_output=True)
            self.assertNotEqual(result.returncode, 0)
            self.assertTrue((path / 'pilot-app.env').is_symlink())
            self.assertEqual(protected.read_text(), 'preserved')

    def test_refuses_review_runtime_without_touching_it(self):
        with tempfile.TemporaryDirectory() as d:
            path = Path(d)
            (path / 'review.env').write_text('preserved')
            self.assertNotEqual(self.run_init(path).returncode, 0)
            self.assertEqual((path / 'review.env').read_text(), 'preserved')
            self.assertFalse((path / 'pilot.env').exists())

    def test_refuses_env_injection_and_nonprivate_directory(self):
        with tempfile.TemporaryDirectory() as d:
            path = Path(d)
            self.assertNotEqual(self.run_init(path, 'Unsafe\nAuth__Mode=Development').returncode, 0)
            path.chmod(0o755)
            self.assertNotEqual(self.run_init(path).returncode, 0)
            self.assertFalse((path / 'pilot.env').exists())

if __name__ == '__main__': unittest.main()
