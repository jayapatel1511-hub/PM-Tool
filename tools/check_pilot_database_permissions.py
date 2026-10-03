#!/usr/bin/env python3
"""Opt-in PG17/image rehearsal; creates/removes only its own disposable Docker resources.

Build hosting/Dockerfile.review first, then run with --image <local-reviewed-image>.
No host release, review database, persistent application volume or real accounts are used.
"""
import argparse
import http.client as http_client
import io
import json
import os
from pathlib import Path
import secrets
import subprocess
import sys
import tarfile
import tempfile
import time
import traceback
import uuid

ROOT = Path(__file__).resolve().parents[1]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--image', required=True, help='already-built local API image; never pulled')
    args = parser.parse_args()
    label = 'pm-pilot-permissions-' + uuid.uuid4().hex[:10]
    network, database, api, runtime = [label + s for s in ('-net', '-db', '-api', '-runtime')]
    owned_containers, owned_volumes, owned_networks = [], [], []
    stage = 'resource creation'
    report = {'scope': 'Disposable local PG17, Staging LocalPassword API; not homedev/company acceptance'}

    def command(argv, data=None, timeout=180, allow_failure=False):
        result = subprocess.run(argv, input=data, stdout=subprocess.PIPE, stderr=subprocess.PIPE, timeout=timeout)
        if not allow_failure and result.returncode:
            print('Failed command prefix: ' + ' '.join(argv[:4]) + '; exit=' + str(result.returncode), file=sys.stderr)
            raise RuntimeError('Command failed during ' + stage)  # Never expose environment/password/input errors.
        return result

    def sql(statement, app=False, allow_failure=False):
        text = ('SET ROLE hub_pilot_app;\n' if app else '') + statement
        return command(['docker', 'exec', '-i', database, 'psql', '-X', '-q', '-t', '-A',
                        '-v', 'ON_ERROR_STOP=1', '-U', 'hub_pilot', '-d', 'hub_pilot'],
                       text.encode(), allow_failure=allow_failure)

    def mount_verifier(value):
        bundle = io.BytesIO()
        with tarfile.open(fileobj=bundle, mode='w') as tar:
            for name, data, mode in [('keys', None, 0o700), ('pilot-users.json', value, 0o600)]:
                entry = tarfile.TarInfo(name); entry.uid = entry.gid = 1000; entry.mode = mode
                if data is None:
                    entry.type = tarfile.DIRTYPE; tar.addfile(entry)
                else:
                    entry.size = len(data); tar.addfile(entry, io.BytesIO(data))
        command(['docker', 'run', '-i', '--rm', '--pull', 'never', '--network', 'none', '--user', '0:0',
                 '--mount', 'type=volume,src=' + runtime + ',dst=/run/pilot', '--entrypoint', 'sh',
                 'postgres:17-alpine', '-c', 'tar -x -C /run/pilot && chmod 700 /run/pilot && chown 1000:1000 /run/pilot'], bundle.getvalue())

    try:
        with tempfile.TemporaryDirectory(prefix=label) as temp:
            private = Path(temp); private.chmod(0o700)
            admin_password, app_password = secrets.token_urlsafe(48), secrets.token_urlsafe(48)
            def envfile(name, values):
                path = private / name
                with os.fdopen(os.open(path, os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o600), 'w') as target:
                    target.write('\n'.join(k + '=' + v for k, v in values.items()) + '\n')
                return str(path)
            command(['docker', 'network', 'create', network]); owned_networks.append(network)
            command(['docker', 'volume', 'create', runtime]); owned_volumes.append(runtime)
            mount_verifier(b'{"users": []}\n')
            command(['docker', 'run', '-d', '--pull', 'never', '--name', database, '--network', network,
                     '--env-file', envfile('db.env', {'POSTGRES_DB': 'hub_pilot', 'POSTGRES_USER': 'hub_pilot',
                                                    'POSTGRES_PASSWORD': admin_password}), 'postgres:17-alpine'])
            owned_containers.append(database)
            for _ in range(60):
                if command(['docker', 'exec', database, 'pg_isready', '-U', 'hub_pilot'], allow_failure=True).returncode == 0:
                    break
                time.sleep(1)
            else: raise RuntimeError('Database did not become ready')
            gateway = json.loads(command(['docker', 'network', 'inspect', network]).stdout)[0]['IPAM']['Config'][0]['Gateway']
            common = {'ASPNETCORE_ENVIRONMENT': 'Staging', 'Auth__Mode': 'LocalPassword',
                      'Auth__Local__UsersFile': '/run/pilot/pilot-users.json', 'Auth__Local__KeyDirectory': '/run/pilot/keys',
                      'Seed__ReviewDemo': 'false', 'Seed__DevUsers': 'false', 'Graph__DirectorySync': 'false',
                      'Graph__Mail': 'false', 'Email__Mode': 'Log', 'AllowedHosts': 'pm.engcalchub.com',
                      'Hosting__LocalTunnelProxy': 'true', 'Hosting__LocalTunnelProxyAddress': gateway,
                      'ASPNETCORE_HTTPS_PORT': '443'}
            stage = 'migration-only bootstrap'
            migration_container = label + '-migrate'
            owned_containers.append(migration_container)
            migration_env = envfile('migration.env', {**common, 'Db__MigrateOnly': 'true', 'Db__Migrate': 'true',
                'Auth__Local__BootstrapAdmins': 'permission-check@example.test|Synthetic Permission Check Admin',
                'ConnectionStrings__Hub': f'Host={database};Database=hub_pilot;Username=hub_pilot;Password={admin_password}'})
            migrate = command(['docker', 'run', '--rm', '--name', migration_container, '--pull', 'never', '--network', network,
                               '--mount', 'type=volume,src=' + runtime + ',dst=/run/pilot', '--env-file', migration_env, args.image])
            assert b'Now listening' not in migrate.stdout
            assert sql('SELECT count(*) FROM hub.__ef_migrations').stdout.strip() == b'29'
            assert sql('SELECT count(*) FROM hub.project').stdout.strip() == b'0'
            admin_id = sql("SELECT id FROM hub.app_user WHERE email='permission-check@example.test'").stdout.decode().strip()
            assert str(uuid.UUID(admin_id)) == admin_id
            report['migrationOnly'] = {'exit': 0, 'httpHostStarted': False, 'migrations': 29, 'fixtureProjects': 0}
            stage = 'runtime role grants'
            command(['docker', 'exec', '-i', database, 'psql', '-X', '-q', '-v', 'ON_ERROR_STOP=1', '-U', 'hub_pilot', '-d', 'hub_pilot'],
                    ("\\set app_password '" + app_password + "'\n" + (ROOT / 'hosting/pilot-runtime-role.sql').read_text()).encode())
            denied = ['CREATE TABLE hub.permission_escape(id integer)', 'CREATE TABLE public.permission_escape(id integer)',
                      'ALTER TABLE hub.activity_log DISABLE TRIGGER ALL', 'UPDATE hub.activity_log SET reason=reason',
                      'DELETE FROM hub.activity_log', 'TRUNCATE hub.activity_log', 'DROP FUNCTION hub.activity_log_immutable()',
                      'CREATE ROLE permission_escape']
            original_log_count = sql('SELECT count(*) FROM hub.activity_log').stdout.strip()
            for statement in denied:
                result = sql(statement, app=True, allow_failure=True)
                assert result.returncode != 0 and (b'permission denied' in result.stderr or b'must be owner' in result.stderr)
            assert sql('SELECT count(*) FROM hub.activity_log').stdout.strip() == original_log_count
            report['forbiddenDatabaseOperationsRefused'] = len(denied)
            verifiers, handoff = private / 'pilot-users.json', private / 'handoff.json'
            verifiers.write_text('{"users": []}\n'); verifiers.chmod(0o600)
            command([sys.executable, str(ROOT / 'scripts/bootstrap-review-credentials.py'), str(verifiers), str(handoff), 'permission-check:' + admin_id])
            login = json.loads(handoff.read_text())['credentials'][0]
            mount_verifier(verifiers.read_bytes())
            stage = 'ordinary runtime startup/auth/audited writes'
            runtime_env = envfile('api.env', {**common, 'Db__Migrate': 'false', 'Db__MigrateOnly': 'false',
                'ConnectionStrings__Hub': f'Host={database};Database=hub_pilot;Username=hub_pilot_app;Password={app_password}'})
            command(['docker', 'run', '-d', '--pull', 'never', '--name', api, '--network', network,
                     '-p', '127.0.0.1::8080', '--mount', 'type=volume,src=' + runtime + ',dst=/run/pilot',
                     '--env-file', runtime_env, args.image]); owned_containers.append(api)
            port = int(json.loads(command(['docker', 'inspect', '--format', '{{json .NetworkSettings.Ports}}', api]).stdout)['8080/tcp'][0]['HostPort'])
            cookie = None
            def http(method, path, body=None):
                con = http_client.HTTPConnection('127.0.0.1', port, timeout=10)
                headers = {'Host': 'pm.engcalchub.com', 'X-Forwarded-Proto': 'https', 'Origin': 'https://pm.engcalchub.com'}
                if cookie: headers['Cookie'] = cookie
                if body is not None: headers['Content-Type'] = 'application/json'
                con.request(method, path, json.dumps(body) if body is not None else None, headers)
                response = con.getresponse(); data = response.read(); status = response.status; headers = dict(response.getheaders()); con.close()
                return status, (json.loads(data) if data and path != '/health' else data), headers
            for _ in range(60):
                try:
                    last_health = http('GET', '/health')[0]
                    if last_health == 200: break
                except (OSError, http_client.HTTPException): pass
                time.sleep(1)
            else: raise RuntimeError('Restricted API not healthy')
            status, _, headers = http('POST', '/api/v1/auth/local/sign-in', {'userName': login['login'], 'password': login['password']})
            assert status == 204; cookie = headers['Set-Cookie'].split(';', 1)[0]
            assert http('GET', '/api/v1/me')[1]['capabilities']['admin']
            status, created, _ = http('POST', '/api/v1/admin/users', {'email': 'permission-person@example.test', 'displayName': 'Synthetic Permission Person'})
            assert status == 201
            assert http('PATCH', '/api/v1/admin/users/' + created['id'], {'rowVersion': created['rowVersion'], 'isActive': False})[0] == 200
            assert int(sql('SELECT count(*) FROM hub.activity_log').stdout.strip()) >= int(original_log_count) + 3
            sessions = sql("SELECT DISTINCT usename FROM pg_stat_activity WHERE datname='hub_pilot' AND client_addr IS NOT NULL").stdout.decode().splitlines()
            assert sessions == ['hub_pilot_app']
            env = json.loads(command(['docker', 'inspect', '--format', '{{json .Config.Env}}', api]).stdout)
            assert not any(v.startswith(('PILOT_', 'Auth__Local__Bootstrap')) or 'Username=hub_pilot;' in v for v in env)
            report['runtime'] = {'healthy': True, 'individualSignIn': True, 'userInsertUpdateAndAuditInsert': True,
                                 'observedDatabaseSessionRole': 'hub_pilot_app', 'administratorSecretAbsent': True}
            stage = 'runtime cookie/key persistence after restart'
            command(['docker', 'restart', api])
            # Docker may assign a new ephemeral published port on restart.
            port = int(json.loads(command(['docker', 'inspect', '--format', '{{json .NetworkSettings.Ports}}', api]).stdout)['8080/tcp'][0]['HostPort'])
            for _ in range(60):
                try:
                    if http('GET', '/api/v1/me')[0] == 200: break
                except (OSError, http_client.HTTPException): pass
                time.sleep(1)
            else: raise RuntimeError('Cookie/key did not survive restart')
            report['runtime']['cookieSurvivesRestart'] = True
            report['verdict'] = 'PASS'
            print(json.dumps(report, indent=2))
    except Exception as exc:
        last = traceback.extract_tb(exc.__traceback__)[-1]
        if api in owned_containers:
            logs = command(['docker', 'logs', '--tail', '40', api], allow_failure=True)
            diagnostic = (logs.stdout + logs.stderr).decode(errors='replace')
            for secret in (locals().get('admin_password', ''), locals().get('app_password', ''), locals().get('login', {}).get('password', '')):
                if secret: diagnostic = diagnostic.replace(secret, '[REDACTED]')
            print('Last health status: ' + str(locals().get('last_health', 'transport unavailable')), file=sys.stderr)
            print(diagnostic, file=sys.stderr)
        print(f'FAIL: {stage}; {type(exc).__name__} at {Path(last.filename).name}:{last.lineno}. No private input printed.', file=sys.stderr)
        return 1
    finally:
        for name in reversed(owned_containers): command(['docker', 'rm', '-f', '-v', name], allow_failure=True)
        for name in owned_volumes: command(['docker', 'volume', 'rm', name], allow_failure=True)
        for name in owned_networks: command(['docker', 'network', 'rm', name], allow_failure=True)
    return 0


if __name__ == '__main__':
    sys.exit(main())
