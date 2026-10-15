"""Fail closed before preview writes: only the named local Tuesday database is permitted."""
import json
import re
import subprocess
from urllib.parse import urlsplit


def verify_preview_target(base):
    url = urlsplit(base)
    if url.scheme != 'http' or url.hostname not in ('localhost', '127.0.0.1') or url.port != 5080 or url.path not in ('', '/'):
        raise SystemExit('Refusing preview writes: use http://localhost:5080 backed by pm-tuesday-preview-db on 55433.')
    try:
        ports = json.loads(subprocess.check_output(['docker', 'inspect', '--format', '{{json .NetworkSettings.Ports}}', 'pm-tuesday-preview-db'], text=True))
        if not any(x['HostPort'] == '55433' and x['HostIp'] == '127.0.0.1' for x in ports.get('5432/tcp', [])):
            raise ValueError('named container port mismatch')
        pids = subprocess.check_output(['lsof', '-tiTCP:5080', '-sTCP:LISTEN'], text=True).split()
        args = [subprocess.check_output(['ps', '-p', pid, '-o', 'args='], text=True) for pid in pids]
        if not any('Hub.Api' in a and re.search(r'Host=(localhost|127\.0\.0\.1);Port=55433;Database=hub(?:;|\s|$)', a) for a in args):
            raise ValueError('API target cannot be established')
    except (subprocess.CalledProcessError, OSError, ValueError, KeyError):
        raise SystemExit('Refusing preview writes: could not verify API 5080 uses pm-tuesday-preview-db on 127.0.0.1:55433.') from None
