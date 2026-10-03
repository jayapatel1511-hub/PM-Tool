#!/usr/bin/env python3
"""Syntax checks; --docker also rehearses maintained helpers on disposable local PG17."""
import argparse
import json
import os
from pathlib import Path
import subprocess
import tempfile
import time
import uuid

ROOT = Path(__file__).resolve().parents[1]

def command(args, **kwargs):
    result = subprocess.run(args, stdout=subprocess.PIPE, stderr=subprocess.PIPE, timeout=240, **kwargs)
    if result.returncode:
        raise RuntimeError('Command failed: ' + ' '.join(args[:3]) + '\n' + result.stderr.decode(errors='replace')[-3000:])
    return result

def main():
    parser=argparse.ArgumentParser(description=__doc__); parser.add_argument('--docker', action='store_true'); args=parser.parse_args()
    for name in ('scripts/pilot-base-backup.sh','scripts/pilot-pitr-drill.sh'):
        command(['bash','-n',str(ROOT/name)])
    archive=ROOT/'hosting/pm-tool-pilot-wal-archive.sh'
    command(['sh','-n',str(archive)])
    drill=(ROOT/'scripts/pilot-pitr-drill.sh').read_text()
    command(['sh','-n'],input=drill.split("<<'RESTORE_SH'\n",1)[1].split('\nRESTORE_SH',1)[0].encode())
    if not args.docker:
        print('PASS shell syntax, including actual embedded recovery command; PG17 replay requires --docker')
        return
    label='pm-pitr-test-'+uuid.uuid4().hex[:12]
    with tempfile.TemporaryDirectory(prefix='.'+label,dir=ROOT.parents[1]) as temp:
        root=Path(temp); root.chmod(0o700)
        for folder in ('hosting','scripts','.runtime','bin','data'): (root/folder).mkdir(mode=0o700)
        (root/'.runtime/keys').mkdir(mode=0o700); (root/'data/backups').mkdir(mode=0o700)
        (root/'.runtime/pilot-users.json').write_text('{"users": []}\n'); (root/'.runtime/pilot-users.json').chmod(0o600)
        for name in ('pilot-base-backup.sh','pilot-pitr-drill.sh','backup-homedev-pilot.sh','restore-homedev-pilot-drill.sh'):
            (root/'scripts'/name).write_text((ROOT/'scripts'/name).read_text().replace('pm-tool-pilot',label))
        (root/'hosting/archive.sh').write_text(archive.read_text())
        config={'name':label,'services':{
            'init':{'image':'postgres:17-alpine','network_mode':'none','volumes':['wal:/var/lib/postgresql/wal-archive','base:/var/lib/postgresql/base-backups'],
                    'entrypoint':['sh','-ceu','chown postgres:postgres /var/lib/postgresql/wal-archive /var/lib/postgresql/base-backups; chmod 700 /var/lib/postgresql/wal-archive /var/lib/postgresql/base-backups']},
            'db':{'image':'postgres:17-alpine','container_name':label+'-db-1','network_mode':'none',
                  'environment':{'POSTGRES_USER':'hub_pilot','POSTGRES_DB':'hub_pilot','POSTGRES_HOST_AUTH_METHOD':'trust'},
                  'depends_on':{'init':{'condition':'service_completed_successfully'}},
                  'command':['postgres','-c','wal_level=replica','-c','archive_mode=on','-c','archive_timeout=5min','-c','archive_command=test -f /archive.sh && /bin/sh /archive.sh "%p" "%f"'],
                  'volumes':['data:/var/lib/postgresql/data','wal:/var/lib/postgresql/wal-archive','base:/var/lib/postgresql/base-backups',{'type':'bind','source':'./archive.sh','target':'/archive.sh','read_only':True,'bind':{'create_host_path':False}}]}},
            'volumes':{name:{'name':label+'-'+target} for name,target in [('data','db'),('wal','wal-archive'),('base','base-backups')]}}
        compose=root/'hosting/homedev-pilot.compose.yml'; compose.write_text(json.dumps(config))
        (root/'.runtime/pilot.env').write_text('PILOT_HOSTNAME=pm.engcalchub.com\n'); (root/'.runtime/pilot.env').chmod(0o600)
        sudo=root/'bin/sudo'; sudo.write_text('#!/usr/bin/env python3\nimport os,sys\na=sys.argv[1:]\nif a==["-v"]:sys.exit(0)\nos.execvpe(a[0],a,os.environ)\n');sudo.chmod(0o700)
        # macOS lacks GNU timeout; keep the same bound via stdlib in this local rehearsal.
        timeout=root/'bin/timeout';timeout.write_text('#!/usr/bin/env python3\nimport subprocess,sys\nsys.exit(subprocess.run(sys.argv[2:],timeout=int(sys.argv[1])).returncode)\n');timeout.chmod(0o700)
        env={**os.environ,'PATH':str(root/'bin')+':'+os.environ['PATH']}
        cli=['docker','compose','-f',str(compose)]
        started=time.monotonic()
        try:
            command(cli+['up','-d','db'])
            mounted=command(['docker','exec',label+'-db-1','cat','/archive.sh']).stdout
            assert mounted==archive.read_bytes(), 'Archive helper bind mount did not match source'

            for _ in range(60):
                ready=subprocess.run(['docker','exec',label+'-db-1','pg_isready','-U','hub_pilot'],capture_output=True)
                if ready.returncode==0:break
                time.sleep(1)
            else:raise RuntimeError('Disposable PG17 did not become ready')
            command(['docker','exec',label+'-db-1','psql','-U','hub_pilot','-d','hub_pilot','-v','ON_ERROR_STOP=1','-c','CREATE SCHEMA hub; CREATE TABLE hub.project(id integer); INSERT INTO hub.project VALUES (1)'])
            command(['bash',str(root/'scripts/backup-homedev-pilot.sh')],cwd=root,env=env,input=b'UNRELATED_TERMINAL_INPUT')
            dump=next((root/'data/backups').glob('hub-pilot-*.dump'))
            logical=command(['bash',str(root/'scripts/restore-homedev-pilot-drill.sh'),str(dump)],cwd=root,env=env,input=b'UNRELATED_TERMINAL_INPUT')
            assert b'projects=1' in logical.stdout
            print('PASS actual PG17 logical backup, stdin restore, source count preserved',flush=True)
            result=command(['bash',str(root/'scripts/pilot-pitr-drill.sh')],cwd=root,env=env,input=b'UNRELATED_TERMINAL_INPUT_MUST_NOT_REACH_DOCKER')
            assert b'paused=true before=1 after=0' in result.stdout
            daily=(ROOT/'hosting/pm-tool-pilot-backup-root.sh').read_text().split("<<'BASE_BACKUP_SH'\n",1)[1].split('\nBASE_BACKUP_SH',1)[0]
            import datetime
            stamp=datetime.datetime.now(datetime.timezone.utc).strftime('%Y%m%dT%H%M%S%fZ')
            daily_result=command(['docker','exec','-i','--user','postgres',label+'-db-1','sh','-s','--',stamp],input=daily.encode())
            assert b'Verified physical pilot base:' in daily_result.stdout
            # Real Alpine archive helper: history files, idempotence, collision refusal, traversal refusal.
            test=r'''set -eu
mkdir /tmp/archive-source; mkdir /tmp/archive-source/pg_wal
export PGDATA=/tmp/archive-source
for name in 00000002.history 000000010000000000000001.00000001.backup; do
 printf synthetic > "$PGDATA/pg_wal/$name"
 sh /archive.sh "pg_wal/$name" "$name"
 sh /archive.sh "pg_wal/$name" "$name"
 printf different > "$PGDATA/pg_wal/$name"
 if sh /archive.sh "pg_wal/$name" "$name"; then exit 1; fi
 test "$(cat /var/lib/postgresql/wal-archive/$name)" = synthetic
done
if sh /archive.sh pg_wal/../../escape ../../escape; then exit 1; fi
'''
            command(['docker','exec',label+'-db-1','sh','-ceu',test])
            print(json.dumps({'verdict':'PASS','scope':'Disposable local PG17; not homedev/off-host acceptance','baseBackupVerified':True,'dailyPhysicalBackupCommandVerified':True,
                              'replayPausedAtTarget':True,'postBackupBeforeMarker':1,'postBackupAfterMarker':0,
                              'archiveHistoryAndCollisionChecks':True,'elapsedSeconds':round(time.monotonic()-started,1)},indent=2))
        except Exception:
            logs=subprocess.run(['docker','logs','--tail','25',label+'-db-1'],capture_output=True)
            print((logs.stdout+logs.stderr).decode(errors='replace')[-5000:],flush=True)
            raise
        finally:
            command(cli+['down','--volumes','--remove-orphans'])

if __name__=='__main__': main()
