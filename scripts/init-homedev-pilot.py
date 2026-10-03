#!/usr/bin/env python3
"""Prepare a separate private pilot runtime. Never copies review data or prints secrets."""
import argparse
import json
import os
import re
import secrets
from pathlib import Path


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--runtime', type=Path, required=True, help='separate pilot .runtime directory outside Git')
    parser.add_argument('--admin-email', help='approved first administrator UPN')
    parser.add_argument('--admin-name')
    parser.add_argument('--add-app-credential', action='store_true',
                        help='explicit legacy upgrade: create only pilot-app.env; never replace existing private files')
    args = parser.parse_args()
    runtime = args.runtime.expanduser().resolve()
    if args.add_app_credential:
        if args.admin_email or args.admin_name:
            parser.error('legacy upgrade does not change the first administrator')
        if not runtime.is_dir() or runtime.stat().st_uid != os.getuid() or runtime.stat().st_mode & 0o077:
            parser.error('legacy runtime must already be owner-only and owned by the current user')
        legacy = runtime / 'pilot.env'
        if legacy.is_symlink() or not legacy.is_file() or legacy.stat().st_uid != os.getuid() or legacy.stat().st_mode & 0o077:
            parser.error('legacy pilot.env must be an owner-private regular file')
        value = legacy.read_text()
        if ('PILOT_HOSTNAME=pm.engcalchub.com\n' not in value
                or 'Database=hub_pilot;Username=hub_pilot;' not in value
                or any(word in value.lower() for word in ('review', 'hub_review'))):
            parser.error('legacy settings are not a separate pilot runtime')
        write_app_credential(runtime)
        print('New private pilot-app.env created; existing runtime files and database were not changed.')
        return
    if not args.admin_email or not args.admin_name:
        parser.error('initial bootstrap requires --admin-email and --admin-name')
    email, name = args.admin_email.strip().lower(), args.admin_name.strip()
    if not re.fullmatch(r'[A-Za-z0-9._+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}', email) or len(email) > 254:
        parser.error('admin-email must be a valid UPN')
    if not name or len(name) > 200 or any(c in name for c in '\r\n\0|;#='):
        parser.error('admin-name must be a single safe display name')
    runtime.mkdir(mode=0o700, parents=True, exist_ok=True)
    if runtime.stat().st_uid != os.getuid() or runtime.stat().st_mode & 0o077:
        parser.error('runtime must be owner-only (mode 700) and owned by the current user')
    # A pilot can never reuse the review runtime, key ring or credentials. Never overwrite a partial bootstrap.
    if any((runtime / p).exists() or (runtime / p).is_symlink()
           for p in ['review.env', 'review-users.json', 'pilot.env', 'pilot-app.env', 'pilot-users.json', 'keys']):
        parser.error('runtime already holds credentials or keys; preserving it unchanged')
    created = []
    try:
        keys = runtime / 'keys'
        keys.mkdir(mode=0o700)
        created.append(keys)
        password = secrets.token_urlsafe(48)
        content = {
            'pilot.env': f'PILOT_HOSTNAME=pm.engcalchub.com\nPILOT_DB_PASSWORD={password}\n'
                f'ConnectionStrings__Hub=Host=db;Port=5432;Database=hub_pilot;Username=hub_pilot;Password={password}\n'
                f'Auth__Local__BootstrapAdmins={email}|{name}\n',
            'pilot-users.json': json.dumps({'users': []}, indent=2) + '\n',
        }
        for filename, value in content.items():
            path = runtime / filename
            fd = os.open(path, os.O_WRONLY | os.O_CREAT | os.O_EXCL | os.O_NOFOLLOW, 0o600)
            created.append(path)
            with os.fdopen(fd, 'w') as out:
                out.write(value)
                out.flush()
                os.fsync(out.fileno())
        write_app_credential(runtime)
        created.append(runtime / 'pilot-app.env')
    except BaseException:
        for path in reversed(created):
            if path.is_dir(): path.rmdir()
            else: path.unlink(missing_ok=True)
        raise
    print('Private pilot settings, empty verifier list and separate key directory created. No review data copied.')


def write_app_credential(runtime):
    # A single permitted API setting: no bootstrap/admin database secret is shared with it.
    path = runtime / 'pilot-app.env'
    password = secrets.token_urlsafe(48)
    try:
        fd = os.open(path, os.O_WRONLY | os.O_CREAT | os.O_EXCL | os.O_NOFOLLOW, 0o600)
    except FileExistsError:
        raise SystemExit('pilot-app.env already exists; preserving it unchanged')
    try:
        with os.fdopen(fd, 'w') as out:
            out.write('ConnectionStrings__Hub=Host=db;Port=5432;Database=hub_pilot;'
                      f'Username=hub_pilot_app;Password={password}\n')
            out.flush()
            os.fsync(out.fileno())
    except BaseException:
        path.unlink(missing_ok=True)
        raise


if __name__ == '__main__':
    main()
