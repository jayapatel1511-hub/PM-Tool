import json
import os
import pathlib
import subprocess
import tempfile
import unittest

ROOT = pathlib.Path(__file__).resolve().parents[1]
SHELLS = [ROOT / "scripts" / name for name in ("activate-homedev-pilot.sh", "backup-homedev-pilot.sh", "restore-homedev-pilot-drill.sh")]

class PilotOperationsTests(unittest.TestCase):
    def test_shell_scripts_have_valid_syntax(self):
        for script in SHELLS:
            self.assertEqual(subprocess.run(["bash", "-n", str(script)]).returncode, 0, script)

    def test_fake_docker_backup_restore_never_forwards_terminal_stdin(self):
        with tempfile.TemporaryDirectory() as temp:
            root = pathlib.Path(temp)
            (root / "hosting").mkdir(); (root / ".runtime").mkdir(); (root / "data/backups").mkdir(parents=True)
            (root / "hosting/homedev-pilot.compose.yml").write_text("name: pm-tool-pilot\n")
            (root / ".runtime/pilot.env").write_text("PILOT_HOSTNAME=pm.engcalchub.com\n")
            fakebin = root / "bin"; fakebin.mkdir(); log = root / "fake-io.log"
            sudo = fakebin / "sudo"
            sudo.write_text("#!/usr/bin/env python3\nimport os,sys\na=sys.argv[1:]\nif a and a[0]=='-v': sys.exit(0)\nwhile a and '=' in a[0] and not a[0].startswith('-'): a.pop(0)\nos.execvpe(a[0],a,os.environ)\n")
            docker = fakebin / "docker"
            docker.write_text("#!/usr/bin/env python3\nimport os,sys\np=sys.stdin.buffer.read(); open(os.environ['FAKE_IO'],'ab').write(b'---\\n'+p+b'\\n'); a=sys.argv[1:]\nif a[:1]==['inspect']:\n f=a[a.index('--format')+1] if '--format' in a else ''\n print('healthy' if 'Health.Status' in f else 'pm-tool-pilot' if 'compose.project' in f else 'db' if 'compose.service' in f else 'pm-tool-pilot-db')\nelif 'compose' in a and 'exec' in a:\n sys.stdout.buffer.write(b'synthetic-dump' if 'pg_dump' in a else b'2\\n' if 'psql' in a else b'')\n")
            for path in (sudo, docker): path.chmod(0o755)
            env = {**os.environ, "PATH": f"{fakebin}:{os.environ['PATH']}", "FAKE_IO": str(log), "RELEASE_SHA": "a" * 40}
            terminal = b"terminal-secret-must-not-reach-docker"
            backup = subprocess.run(["bash", str(ROOT / "scripts/backup-homedev-pilot.sh")], cwd=root, env=env, input=terminal, capture_output=True)
            self.assertEqual(backup.returncode, 0, backup.stderr.decode())
            dump = next((root / "data/backups").glob("hub-pilot-*.dump"))
            self.assertEqual(dump.read_bytes(), b"synthetic-dump")
            restore = subprocess.run(["bash", str(ROOT / "scripts/restore-homedev-pilot-drill.sh"), str(dump)], cwd=root, env=env, input=terminal, capture_output=True)
            self.assertEqual(restore.returncode, 0, restore.stderr.decode())
            calls = log.read_bytes()
            self.assertNotIn(terminal, calls)
            self.assertGreaterEqual(calls.count(b"synthetic-dump"), 2)

    def test_review_dump_is_refused_before_docker(self):
        with tempfile.TemporaryDirectory() as temp:
            dump = pathlib.Path(temp) / "hub-review-20261001.dump"; dump.write_bytes(b"x")
            result = subprocess.run(["bash", str(ROOT / "scripts/restore-homedev-pilot-drill.sh"), str(dump)], input=b"secret", capture_output=True)
            self.assertNotEqual(result.returncode, 0)
            self.assertIn(b"pilot restore drill refused", result.stderr)

    def test_verify_rejects_review_handoff(self):
        with tempfile.TemporaryDirectory() as temp:
            handoff = pathlib.Path(temp) / "review-login-handoff.json"
            handoff.write_text(json.dumps({"login": "a", "password": "b"})); handoff.chmod(0o600)
            result = subprocess.run(["python3", str(ROOT / "scripts/verify-homedev-pilot.py"), "http://127.0.0.1:1", "--credentials-file", str(handoff)], capture_output=True)
            self.assertNotEqual(result.returncode, 0)
            self.assertNotIn(b"b", result.stdout + result.stderr)

if __name__ == "__main__":
    unittest.main()
