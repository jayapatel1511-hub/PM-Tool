import json
import contextlib
import importlib.util
import io
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
            (root / "hosting").mkdir(); (root / ".runtime/keys").mkdir(parents=True); (root / "data/backups").mkdir(parents=True)
            for private_dir in (root / ".runtime", root / ".runtime/keys", root / "data", root / "data/backups"):
                private_dir.chmod(0o700)
            (root / "hosting/homedev-pilot.compose.yml").write_text("name: pm-tool-pilot\n")
            (root / ".runtime/pilot.env").write_text("PILOT_HOSTNAME=pm.engcalchub.com\n")
            (root / ".runtime/pilot-users.json").write_text('{"users": []}\n')
            (root / ".runtime/pilot.env").chmod(0o600); (root / ".runtime/pilot-users.json").chmod(0o600)
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
            timer_dump = root / "data/backups/hub-pilot-20261002T001356123456Z.dump"
            dump.rename(timer_dump)
            dump = timer_dump
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
            handoff.write_text(json.dumps({"login": "a", "password": "unique-private-secret"})); handoff.chmod(0o600)
            result = subprocess.run(["python3", str(ROOT / "scripts/verify-homedev-pilot.py"), "http://127.0.0.1:1", "--credentials-file", str(handoff)], capture_output=True)
            self.assertNotEqual(result.returncode, 0)
            self.assertNotIn(b"unique-private-secret", result.stdout + result.stderr)

    def test_verifier_refuses_remote_or_review_credentials_before_network(self):
        spec = importlib.util.spec_from_file_location("pilot_verify", ROOT / "scripts/verify-homedev-pilot.py")
        module = importlib.util.module_from_spec(spec); spec.loader.exec_module(module)
        def network_must_not_run(*args, **kwargs):
            raise AssertionError("network was called before preflight refusal")
        with tempfile.TemporaryDirectory() as temp:
            review = pathlib.Path(temp) / "review-login-handoff.json"
            review.write_text(json.dumps({"login": "a", "password": "unique-private-secret"})); review.chmod(0o600)
            output = io.StringIO()
            with contextlib.redirect_stdout(output):
                self.assertEqual(module.run(["--credentials-file", str(review)], network_must_not_run), 2)
                self.assertEqual(module.run(["https://evil.example", "--credentials-file", str(review)], network_must_not_run), 2)
            self.assertNotIn("unique-private-secret", output.getvalue())

    def test_verifier_replays_secure_cookie_from_mocked_sign_in(self):
        spec = importlib.util.spec_from_file_location("pilot_verify", ROOT / "scripts/verify-homedev-pilot.py")
        module = importlib.util.module_from_spec(spec); spec.loader.exec_module(module)
        calls = []
        def fake_call(base, method, path, body=None, host=module.HOST, origin=None, cookie=None):
            calls.append((method, path, body, host, origin, cookie))
            if path == "/health" and host == module.HOST: return 200, b"Healthy", ""
            if path == "/health": return 400, b"", ""
            if path == "/api/v1/me": return (200, b"{}", "") if cookie == "__Host-hub-review=secure-token" else (401, b"", "")
            if path == "/api/v1/auth/local/sign-in" and origin == "https://evil.example": return 403, b"", ""
            if path == "/api/v1/auth/local/sign-in":
                if body["password"] == "wrong": return 401, b"", ""
                return 204, b"", "__Host-hub-review=secure-token; Path=/; Secure; HttpOnly; SameSite=Strict"
            if path == "/api/v1/projects": return (200, b"[]", "") if cookie == "__Host-hub-review=secure-token" else (401, b"", "")
            if path == "/api/v1/auth/local/sign-out": return 204, b"", "__Host-hub-review=; Path=/; Secure; HttpOnly; SameSite=Strict; Max-Age=0"
            return 403, b"", ""
        with tempfile.TemporaryDirectory() as temp:
            credentials = pathlib.Path(temp) / "pilot-verify.json"
            credentials.write_text(json.dumps({"login": "pilot@example.com", "password": "unique-private-secret"})); credentials.chmod(0o600)
            output = io.StringIO()
            with contextlib.redirect_stdout(output):
                self.assertEqual(module.run(["--credentials-file", str(credentials)], fake_call), 0, output.getvalue())
            self.assertTrue(any(path == "/api/v1/me" and cookie == "__Host-hub-review=secure-token" for _, path, _, _, _, cookie in calls))
            self.assertIn("RESULT PASS", output.getvalue())

if __name__ == "__main__":
    unittest.main()
