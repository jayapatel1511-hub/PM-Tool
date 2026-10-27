#!/usr/bin/env python3
"""Local rehearsal of the application half of the restore drill (AC-OPS-04; docs/runbooks/restore.md).

Run from the repository root with the local PostgreSQL container up (pm-tool-db-1 on localhost:55432) and `dotnet` installed:

    python3 tools/local_restore_drill.py [--source-port 7224 --drill-ports 7225 7226]

It builds a synthetic source database with the review demo data, writes marker task 1, takes a custom-format dump the way
scripts/backup-homedev-review.sh does, writes marker task 2, then restores that dump twice into new drill databases and starts
a loopback API on each: (A) with the off-host runbook's flags (no seed, no jobs, no worker, no migration), (B) with the
evaluation worker on and the ordinary no-op migration. On each it checks Healthy, Admin > Operations (no problem except the
two that jobs being off can cause: DigestLate after 08:00 and NightlyMissed), marker 1 present and
marker 2 absent, a task found by key, the newest activity entry at or before the dump, and a task export. It prints the
measured loss window and restore-to-Healthy time as JSON, proves the source database is unchanged, and drops everything it made.

It rehearses the application side only. The off-host copy and the drill on homedev stay operator work (AC-OPS-03/04/05); the
timings are this Mac's, not a recovery-time claim. Not part of CI: it starts three API processes (about three minutes).
"""
import argparse
import json
import os
import signal
import subprocess
import sys
import tempfile
import time
import urllib.error
import urllib.request
from datetime import datetime, timedelta, timezone
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
CONTAINER = 'pm-tool-db-1'
JOBS_OFF_PROBLEMS = {'DigestLate', 'NightlyMissed'}  # the digest and nightly jobs are off in a drill, so these two can be true


def docker(*args, stdin=None, binary=False):
    done = subprocess.run(['docker', 'exec', *(['-i'] if stdin else []), CONTAINER, *args], stdin=stdin, capture_output=True, check=False)
    if done.returncode:
        raise RuntimeError(f'docker exec {args[0]} failed: {done.stderr.decode(errors="replace")[:400]}')
    return done.stdout if binary else done.stdout.decode().strip()


def sql(database, statement):
    return docker('psql', '-U', 'hub', '-d', database, '-Atc', statement)


class Api:
    """One API process on a loopback port, signed in by the development header."""

    def __init__(self, label, database, port, log_dir, **env):
        self.label, self.port, self.base = label, port, f'http://127.0.0.1:{port}'
        self.log = open(Path(log_dir) / f'{label}.log', 'wb')
        merged = {**os.environ, 'ASPNETCORE_ENVIRONMENT': 'Development', 'Auth__Mode': 'Development', 'Jobs__Enabled': 'false',
                  'ConnectionStrings__Hub': f'Host=localhost;Port=55432;Database={database};Username=hub', 'RateLimit__PerMinute': '100000',
                  **{k: str(v) for k, v in env.items()}}
        self.process = subprocess.Popen(['dotnet', 'run', '--project', 'src/Hub.Api', '--no-launch-profile', '--urls', self.base],
                                        cwd=ROOT, env=merged, stdout=self.log, stderr=subprocess.STDOUT, start_new_session=True)

    def call(self, path, who='jordan', method='GET', body=None, raw=False):
        headers = {'X-Dev-User': f'{who}@hub.test', 'Content-Type': 'application/json'}
        request = urllib.request.Request(f'{self.base}{path}', method=method, headers=headers, data=None if body is None else json.dumps(body).encode())
        try:
            with urllib.request.urlopen(request, timeout=60) as response:
                data = response.read()
                status = response.status
        except urllib.error.HTTPError as error:
            data, status = error.read(), error.code
        if raw:
            return status, data.decode(errors='replace')
        return status, (json.loads(data) if data else None)

    def ok(self, path, who='jordan', method='GET', body=None):
        status, data = self.call(path, who, method, body)
        if status >= 300:
            raise RuntimeError(f'{method} {path} as {who}: HTTP {status} {str(data)[:300]}')
        return data

    def wait_healthy(self, timeout=240):
        deadline = time.monotonic() + timeout
        while time.monotonic() < deadline:
            if self.process.poll() is not None:
                raise RuntimeError(f'{self.label} exited early ({self.process.returncode}); see its log')
            try:
                status, data = self.call('/health', raw=False)
                if status == 200 and data and data.get('status') == 'Healthy':
                    return data
            except (urllib.error.URLError, ConnectionError, TimeoutError):
                pass
            time.sleep(0.5)
        raise RuntimeError(f'{self.label} did not answer Healthy within {timeout}s')

    def stop(self):
        if self.process.poll() is None:
            os.killpg(self.process.pid, signal.SIGTERM)
            try:
                self.process.wait(30)
            except subprocess.TimeoutExpired:
                os.killpg(self.process.pid, signal.SIGKILL)
        self.log.close()


def newest_activity(database):
    return datetime.fromisoformat(sql(database, "SELECT to_char(max(occurred_at) AT TIME ZONE 'UTC', 'YYYY-MM-DD\"T\"HH24:MI:SS.US\"+00:00\"') FROM hub.activity_log"))


def fingerprint(database):
    return sql(database, "SELECT (SELECT count(*) FROM hub.project)||'/'||(SELECT count(*) FROM hub.task)||'/'||(SELECT count(*) FROM hub.activity_log)"
                         "||'/'||(SELECT count(*) FROM hub.outbox_event)||'/'||(SELECT count(*) FROM hub.outbox_event WHERE processed_at IS NULL)"
                         "||'/'||(SELECT coalesce(max(occurred_at)::text,'') FROM hub.activity_log)")


def settle(api, timeout=120):
    deadline = time.monotonic() + timeout
    while time.monotonic() < deadline:
        operations = api.ok('/api/v1/admin/operations')
        if operations['pendingChanges'] == 0:
            return operations
        time.sleep(1)
    raise RuntimeError(f'{api.label}: evaluation did not settle within {timeout}s')


def make_markers(source):
    reference = source.ok('/api/v1/reference', 'priya')
    civil = next(d for d in reference['disciplines'] if d['name'] == 'Civil')
    people = {who: source.ok('/api/v1/me', who) for who in ('alex', 'priya', 'marc')}
    suffix = datetime.now(timezone.utc).strftime('%H%M%S')
    created = source.ok('/api/v1/projects', 'priya', 'POST', {
        'projectNumber': f'RD{suffix}', 'name': f'Restore drill {suffix}', 'clientId': reference['clients'][0]['id'], 'officeId': reference['offices'][0]['id'],
        'disciplines': [{'disciplineId': civil['id'], 'leadUserId': people['marc']['id']}],
        'members': [{'userId': people['alex']['id'], 'roles': ['TeamMember'], 'disciplineId': civil['id']}]})
    draft = source.ok(f'/api/v1/projects/{created["id"]}', 'priya')
    source.ok(f'/api/v1/projects/{created["id"]}/transition', 'priya', 'POST', {'toStatus': 'Active', 'rowVersion': draft['rowVersion']})
    detail = source.ok(f'/api/v1/projects/{created["id"]}', 'priya')
    today = datetime.now(timezone.utc).date()

    def marker(number):
        task = source.ok(f'/api/v1/projects/{created["id"]}/tasks', 'priya', 'POST', {
            'name': f'Restore drill {suffix} marker {number}', 'projectDisciplineId': (detail.get('disciplines') or detail['projectDisciplines'])[0]['id'],
            'assigneeId': people['alex']['id'], 'startDate': str(today), 'dueDate': str(today + timedelta(days=7))})
        full = source.ok(f'/api/v1/tasks/{task["id"]}', 'priya')['task']
        return {'id': full['id'], 'key': full['key'], 'name': full['name']}
    return created['id'], marker


def checks(api, database, project_id, marker1, marker2, dump_start, label):
    """The runbook's verification checklist; every line is an assertion on the restored copy."""
    result = {}
    health = api.wait_healthy()
    assert health['status'] == 'Healthy', health
    result['health'] = health['status']
    operations = api.ok('/api/v1/admin/operations')
    # A drill runs with jobs off (it must not send email), so the two job-lateness checks can say so; nothing else may appear.
    unexpected = [p for p in operations['problems'] if p['kind'] not in JOBS_OFF_PROBLEMS]
    assert not unexpected and operations['deadLetterCount'] == 0, operations
    result['operations'] = {'problems': operations['problems'], 'deadLetters': 0, 'pendingChanges': operations['pendingChanges']}
    newest = newest_activity(database)
    assert newest <= dump_start, f'newest restored activity {newest} is after the dump start {dump_start}'
    result['newestRestoredActivity'] = newest.isoformat()
    first, second = api.call(f'/api/v1/tasks/{marker1["id"]}', 'priya'), api.call(f'/api/v1/tasks/{marker2["id"]}', 'priya')
    assert first[0] == 200 and first[1]['task']['name'] == marker1['name'], first
    assert second[0] == 404, f'marker 2 must not exist in the restored copy, got {second[0]}'
    result['markers'] = {'first': 'present', 'second': 'absent'}
    found = api.ok(f'/api/v1/search?q={marker1["key"]}', 'priya')['groups']['tasks']
    assert [t['key'] for t in found] == [marker1['key']], found
    assert api.ok(f'/api/v1/search?q={marker2["key"]}', 'priya')['groups']['tasks'] == []
    result['searchByKey'] = marker1['key']
    status, exported = api.call(f'/api/v1/projects/{project_id}/tasks/export?format=csv', 'priya', raw=True)
    assert status == 200 and marker1['name'] in exported and marker2['name'] not in exported, (status, exported[:200])
    result['export'] = f'csv {len(exported.splitlines())} lines, marker 1 only'
    result[label] = 'PASS'
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument('--source-port', type=int, default=7224)
    parser.add_argument('--drill-ports', type=int, nargs=2, default=[7225, 7226])
    parser.add_argument('--keep', action='store_true', help='leave the drill databases and logs in place')
    args = parser.parse_args()
    stamp = datetime.now(timezone.utc).strftime('%Y%m%d%H%M%S')
    names = {'source': f'hub_drill_src_{stamp}', 'a': f'hub_drill_a_{stamp}', 'b': f'hub_drill_b_{stamp}'}
    work = tempfile.mkdtemp(prefix='restore-drill-')
    os.chmod(work, 0o700)
    apis, created, evidence = [], [], {}
    try:
        for name in names.values():
            sql('hub', f'CREATE DATABASE {name}')
            created.append(name)

        source = Api('source', names['source'], args.source_port, work, Seed__DevUsers='true', Seed__ReviewDemo='true', Evaluation__Worker='true',
                     Evaluation__DebounceMs='60000')  # a long debounce keeps marker 1's events pending, so the dump carries unsettled work
        apis.append(source)
        source.wait_healthy()
        project_id, marker = make_markers(source)
        marker1 = marker(1)

        dump = Path(work) / f'hub-review-{datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%S%f")}Z.dump'
        dump_start = datetime.now(timezone.utc)
        dump.write_bytes(docker('pg_dump', '-U', 'hub', '-d', names['source'], '-Fc', '--no-owner', '--no-privileges', binary=True))
        os.chmod(dump, 0o600)
        assert dump.stat().st_size > 0, 'empty dump'
        with dump.open('rb') as handle:
            assert docker('pg_restore', '--list', stdin=handle, binary=True), 'dump is unreadable'
        time.sleep(2)
        marker2 = marker(2)
        loss_window = newest_activity(names['source']) - dump_start
        source.stop()
        apis.remove(source)
        before = fingerprint(names['source'])
        evidence['dump'] = {'bytes': dump.stat().st_size, 'startedAt': dump_start.isoformat(), 'newestSourceWriteAfterDump': marker2['key'],
                            'lossWindowSeconds': round(loss_window.total_seconds(), 1)}

        for label, database, port, env in (('a', names['a'], args.drill_ports[0], dict(Seed__ReviewDemo='false', Seed__DevUsers='false', Evaluation__Worker='false', Db__Migrate='false')),
                                           ('b', names['b'], args.drill_ports[1], dict(Seed__ReviewDemo='false', Seed__DevUsers='false', Evaluation__Worker='true'))):
            started = time.monotonic()
            with dump.open('rb') as handle:
                docker('pg_restore', '-U', 'hub', '--exit-on-error', '--no-owner', '--no-privileges', '-d', database, stdin=handle)
            restored = time.monotonic() - started
            pending = int(sql(database, 'SELECT count(*) FROM hub.outbox_event WHERE processed_at IS NULL'))
            drill = Api(f'drill-{label}', database, port, work, **env)
            apis.append(drill)
            drill.wait_healthy()
            recovered = time.monotonic() - started
            if label == 'b':
                settle(drill)
            run = checks(drill, database, project_id, marker1, marker2, dump_start, f'drill_{label}')
            run.update(restoreSeconds=round(restored, 1), restoreToHealthySeconds=round(recovered, 1), pendingEventsInDump=pending,
                       flags={k: v for k, v in env.items()})
            evidence[f'drill_{label}'] = run
            drill.stop()
            apis.remove(drill)

        assert fingerprint(names['source']) == before, 'the source database changed during the drills'
        evidence['sourceUnchanged'] = before
        print(json.dumps(evidence, indent=2))
    finally:
        for api in apis:
            api.stop()
        if args.keep:
            print(f'kept: {created}, logs in {work}', file=sys.stderr)
        else:
            for name in created:
                sql('hub', f'DROP DATABASE IF EXISTS {name} WITH (FORCE)')
            for path in Path(work).glob('*'):
                path.unlink()
            Path(work).rmdir()


if __name__ == '__main__':
    main()
