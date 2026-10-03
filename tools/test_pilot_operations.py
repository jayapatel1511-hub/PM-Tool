import json
import contextlib
import importlib.util
import io
import os
import pathlib
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import Mock, patch

ROOT = pathlib.Path(__file__).resolve().parents[1]
SHELLS = [ROOT / "scripts" / name for name in ("activate-homedev-pilot.sh", "backup-homedev-pilot.sh", "restore-homedev-pilot-drill.sh")]

class PilotOperationsTests(unittest.TestCase):
    def test_runtime_grant_step_keeps_password_on_stdin_and_sanitizes_failure(self):
        script = (ROOT / 'scripts/activate-homedev-pilot.sh').read_text()
        source = script.split("<<'GRANTS_PY'\n", 1)[1].split('\nGRANTS_PY', 1)[0]
        password = 'synthetic-app-password-never-in-argv-' + 'a' * 32
        with tempfile.TemporaryDirectory() as temp:
            app = pathlib.Path(temp) / 'pilot-app.env'
            app.write_text('ConnectionStrings__Hub=Host=db;Port=5432;Database=hub_pilot;Username=hub_pilot_app;Password=' + password + '\n')
            for code in (0, 1):
                with patch.object(sys, 'argv', ['grants', str(app), str(ROOT / 'hosting/pilot-runtime-role.sql')]), \
                     patch('subprocess.run', return_value=subprocess.CompletedProcess([], code, stderr=password.encode())) as run:
                    if code:
                        with self.assertRaises(SystemExit) as refusal:
                            exec(compile(source, 'grants', 'exec'), {})
                        self.assertNotIn(password, str(refusal.exception))
                    else:
                        exec(compile(source, 'grants', 'exec'), {})
                    args, kwargs = run.call_args
                    self.assertNotIn(password, ' '.join(args[0]))
                    self.assertIn(password.encode(), kwargs['input'])
                    self.assertEqual(kwargs['stdout'], subprocess.DEVNULL)
                    self.assertEqual(kwargs['stderr'], subprocess.PIPE)

    def test_preflight_refuses_bootstrap_or_extra_environment_in_app_file(self):
        script = (ROOT / 'scripts/activate-homedev-pilot.sh').read_text()
        source = script.split("<<'PREFLIGHT_PY'\n", 1)[1].split('\nPREFLIGHT_PY', 1)[0]
        with tempfile.TemporaryDirectory() as temp:
            app, admin = pathlib.Path(temp) / 'app', pathlib.Path(temp) / 'admin'
            password = 'a' * 64
            admin.write_text('PILOT_DB_PASSWORD=' + password + '\nConnectionStrings__Hub=Host=db;Port=5432;Database=hub_pilot;Username=hub_pilot;Password=' + password + '\n')
            valid = 'ConnectionStrings__Hub=Host=db;Port=5432;Database=hub_pilot;Username=hub_pilot_app;Password=' + 'b' * 64 + '\n'
            for value in (valid.replace('hub_pilot_app', 'hub_pilot'), valid + 'Auth__Local__BootstrapAdmins=x\n', valid.replace('b' * 64, password)):
                app.write_text(value)
                with patch.object(sys, 'argv', ['preflight', str(app), str(admin)]), self.assertRaises(SystemExit):
                    exec(compile(source, 'preflight', 'exec'), {})
            app.write_text(valid)
            with patch.object(sys, 'argv', ['preflight', str(app), str(admin)]):
                exec(compile(source, 'preflight', 'exec'), {})

    def test_activation_verifier_failure_stops_candidate(self):
        script = (ROOT / 'scripts/activate-homedev-pilot.sh').read_text()
        body = script[script.index('api_started=0;'):]
        stubs = r'''set -Eeuo pipefail
exec 3>&1
release_sha=aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
expected=/unused
app_file=/unused/app
role_sql=/unused/role
verify_file=""
install_timer=0
compose_cmd=(mock_compose)
fail() { exit 1; }
mock_compose() {
  echo "$*" >&3
  # Compose run supports --pull never; --no-build belongs to Compose up.
  if [[ "$1" == run && "$*" == *--no-build* ]]; then return 125; fi
  if [[ "$1" == exec ]]; then echo 0:0; fi
}
inspect() {
  case "$1" in
    *Health.Status*) echo healthy;;
    *) echo pm-tool-pilot-db;;
  esac
}
curl() { echo '{"status":"Healthy"}'; }
sudo() {
  case "$*" in *inspect*) echo "pm-tool-pilot:$release_sha";; esac
}
timeout() { shift; "$@"; }
python3() {
  case "$1" in
    -c) echo synthetic;;
    -) return 0;;
    *) echo VERIFIER_REJECTED >&3; return 42;;
  esac
}
ln() { return 98; }
mv() { return 98; }
'''
        result = subprocess.run(['bash'], input=stubs + body, text=True, capture_output=True, timeout=5)
        self.assertEqual(result.returncode, 42, result.stderr)
        calls = result.stdout.splitlines()
        self.assertIn('up -d --no-build --no-deps --force-recreate api', calls)
        self.assertEqual(calls[-2:], ['VERIFIER_REJECTED', 'stop api'])
        self.assertEqual(calls.count('stop api'), 2)

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
            docker.write_text("#!/usr/bin/env python3\nimport os,sys\np=sys.stdin.buffer.read(); open(os.environ['FAKE_IO'],'ab').write(b'---\\n'+p+b'\\n'); a=sys.argv[1:]\nif 'pg_restore' in a and a[-1:] == ['-']: sys.exit(2)\nif a[:1]==['inspect']:\n f=a[a.index('--format')+1] if '--format' in a else ''\n print('healthy' if 'Health.Status' in f else 'pm-tool-pilot' if 'compose.project' in f else 'db' if 'compose.service' in f else 'pm-tool-pilot-db')\nelif 'compose' in a and 'exec' in a:\n sys.stdout.buffer.write(b'synthetic-dump' if 'pg_dump' in a else b'2\\n' if 'psql' in a else b'')\n")
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

    def test_http_transport_preserves_cookie_attributes_and_fails_closed(self):
        spec = importlib.util.spec_from_file_location("pilot_transport", ROOT / "scripts/verify-homedev-pilot.py")
        module = importlib.util.module_from_spec(spec); spec.loader.exec_module(module)
        header = "__Host-hub-review=synthetic; Path=/; Secure; HttpOnly; SameSite=Strict"
        connection = Mock()
        response = connection.getresponse.return_value
        response.status = 204
        response.read.return_value = b""
        response.getheaders.return_value = [("Set-Cookie", header)]
        with patch.object(module.http.client, "HTTPConnection", return_value=connection):
            status, _, cookie = module.call(module.LOOPBACK, "GET", "/api/v1/me")
            self.assertEqual(status, 204)
            self.assertEqual(cookie, header)
            self.assertTrue(module.secure_cookie(cookie))
            response.read.side_effect = module.http.client.IncompleteRead(b"")
            self.assertEqual(module.call(module.LOOPBACK, "GET", "/api/v1/me"), (0, b"", ""))
            self.assertEqual(connection.close.call_count, 2)

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
