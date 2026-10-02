#!/usr/bin/env python3
"""Create one review login verifier for an existing AppUser; never prints the password."""
import argparse
import getpass
import hashlib
import json
import os
import re
import tempfile
import uuid
from pathlib import Path

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('file', type=Path, help='secret JSON file outside the repository')
parser.add_argument('user_id', type=uuid.UUID, help='existing active AppUser ID')
parser.add_argument('user_name', help='individual login ID')
args = parser.parse_args()
if not re.fullmatch(r'[A-Za-z0-9._@-]{1,64}', args.user_name):
    parser.error('user_name must be 1-64 letters, numbers, dots, underscores, @ or hyphens')
password = getpass.getpass('New password: ')
confirm = getpass.getpass('Confirm password: ')
if password != confirm or len(password) < 12 or len(password.encode()) > 1024 or any(c in password for c in '\r\n\0'):
    parser.error('passwords must match, be at least 12 characters, and contain no line breaks')
path = args.file.expanduser().resolve()
if path.exists():
    if path.stat().st_mode & 0o077:
        parser.error('credential file must be private to its owner (chmod 600)')
    value = json.loads(path.read_text())
    users = value['users']
else:
    users = []
if len(users) >= 64 or any(u['userName'].casefold() == args.user_name.casefold() or u['userId'] == str(args.user_id) for u in users):
    parser.error('account already mapped or 64-account limit reached')
salt = os.urandom(16)
users.append({'userId': str(args.user_id), 'userName': args.user_name,
              'salt': salt.hex(), 'hash': hashlib.pbkdf2_hmac('sha256', password.encode(), salt, 600_000).hex()})
path.parent.mkdir(mode=0o700, parents=True, exist_ok=True)
fd, temp = tempfile.mkstemp(prefix='.review-users-', dir=path.parent)
try:
    os.fchmod(fd, 0o600)
    with os.fdopen(fd, 'w') as out:
        json.dump({'users': users}, out, indent=2)
        out.write('\n')
        out.flush()
        os.fsync(out.fileno())
    os.replace(temp, path)
finally:
    if os.path.exists(temp):
        os.unlink(temp)
print(f'Added {args.user_name}; restart the review API to load the new verifier.')
