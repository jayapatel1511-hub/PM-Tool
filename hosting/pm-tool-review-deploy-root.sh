#!/usr/bin/env bash
set -euo pipefail
set +x
umask 077
(( EUID == 0 )) || { echo 'Review deploy helper requires root.' >&2; exit 1; }
# Root executes only its installed copies; never a script from a writable release.
cd /etc/pm-tool-review-deploy
exec /usr/bin/python3 - <<'DEPLOY_PY'
import datetime
import fcntl
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess

ROOT = Path('/etc/pm-tool-review-deploy')
os.environ['DOCKER_CONFIG'] = str(ROOT/'docker')
STATE = Path('/var/lib/pm-tool-review-deploy')
REPOSITORY = 'jayapatel1511-hub/PM-Tool'
IMAGE = 'ghcr.io/jayapatel1511-hub/pm-tool'
REVISION = 'org.opencontainers.image.revision'
KEEP_IMAGES = 3  # the running image, its rollback image and one older
BACKUP = '/usr/local/libexec/pm-tool-review-backup'


def prune(run, previous_id):
    # Newest first. Inspect every candidate's complete tag set: image ls is filtered to this repository and would hide
    # aliases such as local/manual-rollback:keep. Never remove an image with an external alias or an unmanaged review tag.
    rows = [line.split() for line in run(['docker', 'image', 'ls', '--no-trunc', '--format', '{{.ID}} {{.Tag}}', 'pm-tool-review']).splitlines()]
    for image_id in list(dict.fromkeys(image_id for image_id, _ in rows))[KEEP_IMAGES:]:
        if image_id == previous_id:
            continue
        tags = json.loads(run(['docker', 'image', 'inspect', '--format', '{{json .RepoTags}}', image_id])) or []
        managed = [tag for tag in tags if re.fullmatch(r'pm-tool-review:(?:[0-9a-f]{40}|rollback-[0-9a-f]{64})', tag)]
        if tags and len(managed) == len(tags):
            run(['docker', 'image', 'rm', '--force', image_id])


def command(argv, *, capture=True, env=None):
    result = subprocess.run(argv, cwd=ROOT, stdin=subprocess.DEVNULL,
                            stdout=subprocess.PIPE if capture else None, stderr=subprocess.PIPE,
                            text=True, env=env)
    if result.returncode:
        # Never echo subprocess output: it may contain credentials or private application logs.
        raise RuntimeError(f'{argv[0]} {argv[1] if len(argv)>1 else ""} failed')
    return result.stdout.strip() if capture else ''


def write_state(path, state):
    temporary = path.with_suffix('.tmp')
    temporary.write_text(json.dumps(state, indent=2) + '\n')
    temporary.chmod(0o644)
    temporary.replace(path)


def deploy(root=ROOT, state_dir=STATE, run=command):
    state_path = state_dir / 'state.json'
    state = json.loads(state_path.read_text()) if state_path.exists() else {'failed': []}
    digest = json.loads(run(['docker', 'buildx', 'imagetools', 'inspect', IMAGE+':main', '--format', '{{json .Manifest}}']))['digest']
    if not re.fullmatch(r'sha256:[0-9a-f]{64}', digest):
        raise RuntimeError('registry returned an invalid digest')
    if digest == state.get('digest') or digest in state.get('failed', []):
        print('STOP digest already running or previously failed')
        return
    ref = IMAGE+'@'+digest
    token = (root/'ghcr-token').read_text().strip()
    env = {**os.environ, 'GH_TOKEN': token}
    run(['gh', 'attestation', 'verify', 'oci://'+ref, '-R', REPOSITORY,
         '--signer-workflow', REPOSITORY+'/.github/workflows/ci.yml', '--source-ref', 'refs/heads/main'], env=env)
    config = json.loads(run(['docker', 'buildx', 'imagetools', 'inspect', ref, '--format', '{{json .Image}}']))
    labels = config.get('config', {}).get('Labels', {})
    sha = labels.get(REVISION, '')
    if not re.fullmatch(r'[0-9a-f]{40}', sha):
        raise RuntimeError('image has no valid revision label')
    files = {'compose': 'hosting/homedev.compose.yml', 'probe': 'scripts/verify-homedev-review.py'}
    social = (root/'hosting/social-auth.override.yml').exists()
    if social:
        files['social'] = 'hosting/social-auth.override.yml'
    for name, path in files.items():
        expected = hashlib.sha256((root/path).read_bytes()).hexdigest()
        if labels.get('hub.review-'+name+'-sha256') != expected:
            raise RuntimeError(name+' differs from installed root-owned copy; refresh with sudo')
    contract = labels.get('hub.contract-migration')
    if contract is None:
        raise RuntimeError('image has no contract-migration label')
    if contract != 'none':
        if not re.fullmatch(r'[0-9]{14}_[A-Za-z0-9_]+', contract):
            raise RuntimeError('invalid contract-migration label')
        applied = run(['docker', 'exec', 'pm-tool-review-db-1', 'psql', '-U', 'hub_review', '-d', 'hub_review', '-Atc',
                       'SELECT "MigrationId" FROM hub.__ef_migrations'])
        if contract not in applied.splitlines():
            raise RuntimeError('contract migration not applied; manual activation required')
    volume = run(['docker', 'inspect', '--format', '{{range .Mounts}}{{if eq .Destination "/var/lib/postgresql/data"}}{{.Name}}{{end}}{{end}}', 'pm-tool-review-db-1'])
    if volume != 'pm-tool-review-db':
        raise RuntimeError('review database volume identity mismatch')
    previous = run(['docker', 'inspect', '--format', '{{.Config.Image}}', 'pm-tool-review-api-1'])
    if not re.fullmatch(r'pm-tool-review:(?:[0-9a-f]{40}|rollback-[0-9a-f]{64})', previous):
        raise RuntimeError('running image cannot be safely rolled back')
    previous_id = run(['docker', 'inspect', '--format', '{{.Image}}', 'pm-tool-review-api-1'])
    if not re.fullmatch(r'sha256:[0-9a-f]{64}', previous_id):
        raise RuntimeError('running image has no immutable image ID')
    # A missing backup helper stops the run before any tag or pull; the dump itself waits for a good pull, so a registry
    # outage does not leave a new dump every five minutes.
    run(['test', '-x', BACKUP])
    rollback_tag = 'rollback-'+previous_id.split(':')[1]
    run(['docker', 'tag', previous_id, 'pm-tool-review:'+rollback_tag])
    run(['docker', 'pull', ref])
    run(['docker', 'tag', ref, 'pm-tool-review:'+sha])
    dump = run([BACKUP])
    if not dump.startswith('Verified private dump: '):
        raise RuntimeError('backup helper did not verify a nonempty dump')
    compose = ['docker', 'compose', '--env-file', '.runtime/review.env', '-f', 'hosting/homedev.compose.yml']
    if social:
        compose += ['-f', 'hosting/social-auth.override.yml']
    probe = ['python3', 'scripts/verify-homedev-review.py'] + (['--social'] if social else [])

    def activate(revision):
        run(compose+['up', '-d', '--no-build', '--no-deps', '--force-recreate', 'api'], env={**os.environ, 'RELEASE_SHA': revision})
        # Probe performs the signed-in and copied-cookie checks as well as health.
        for attempt in range(30):
            try:
                run(probe)
                return
            except RuntimeError:
                if attempt == 29:
                    raise
                import time
                time.sleep(2)

    try:
        activate(sha)
    except RuntimeError:
        state.setdefault('failed', []).append(digest)
        write_state(state_path, state)
        try:
            activate(rollback_tag)
        except RuntimeError:
            print('FAIL candidate and rollback probes; manual operator recovery required')
            raise
        print('FAIL candidate; previous image restored and probed; digest will not be retried')
        raise
    state.update(sha=sha, digest=digest, time=datetime.datetime.now(datetime.timezone.utc).isoformat())
    write_state(state_path, state)
    try:
        prune(run, previous_id)
    except RuntimeError:
        print('WARN old review images not pruned; retried after the next deployment')
    print('PASS review deployed '+sha+' '+digest)


if __name__ == '__main__':
    STATE.mkdir(mode=0o755, exist_ok=True)
    STATE.chmod(0o755)
    with (STATE/'lock').open('w') as lock:
        fcntl.flock(lock, fcntl.LOCK_EX | fcntl.LOCK_NB)
        try:
            deploy()
        except Exception as error:
            print('STOP '+str(error))
            raise SystemExit(1)
DEPLOY_PY
