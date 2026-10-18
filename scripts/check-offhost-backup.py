#!/usr/bin/env python3
"""FR-OPS-04 / AC-OPS-03, AC-OPS-05: inspect a retrieved encrypted platform archive, never its contents.

Pipe `restic dump <verified-snapshot-id> personal-ubuntu.tar` to this script, or pass --archive.
Pilot data remains excluded until Jay's recorded approval; finding any pilot member fails closed.
"""
import argparse
import datetime as dt
import re
import sys
import tarfile

DUMP = re.compile(r'(?:^|/)pm-tool/data/backups/hub-review-(\d{8}T\d{6}\d{0,9}Z)\.dump$')


def check(members, now=None, hours=26):
    now = now or dt.datetime.now(dt.timezone.utc)
    newest = None
    pilot = False
    for member in members:
        name = member.name.replace('\\', '/')
        pilot |= bool(re.search(r'(?:^|/)(?:pm-tool-pilot|hub-pilot-[^/]*\.dump)(?:/|$)', name))
        match = DUMP.search(name)
        if match and member.isfile() and member.size > 0:
            stamp = match.group(1)
            try:
                when = dt.datetime.strptime(stamp[:15], '%Y%m%dT%H%M%S').replace(tzinfo=dt.timezone.utc)
            except ValueError:
                continue
            newest = max(newest, when) if newest else when
    messages = []
    if pilot:
        messages.append('FAIL pilot: unapproved pilot data present; no pilot content inspected')
    if newest is None:
        messages.append('FAIL review: newest nonempty dump missing')
    else:
        age = (now - newest).total_seconds() / 3600
        valid = 0 <= age <= hours
        messages.append(f'{"PASS" if valid else "FAIL"} review: newest={newest.isoformat()} age={age:.2f}h limit={hours}h')
    return int(pilot or newest is None or not (0 <= (now-newest).total_seconds() <= hours*3600)), messages


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--archive', default='-')
    p.add_argument('--hours', type=float, default=26)
    a = p.parse_args()
    try:
        with tarfile.open(fileobj=sys.stdin.buffer if a.archive == '-' else None,
                          name=None if a.archive == '-' else a.archive, mode='r|*') as archive:
            code, messages = check(archive, hours=a.hours)
        print('\n'.join(messages))
        return code
    except (OSError, tarfile.TarError):
        print('FAIL review: off-host archive unavailable or unreadable')
        return 1


if __name__ == '__main__':
    sys.exit(main())
