#!/usr/bin/env python3
"""Exercise operator sequencing with real verifier/handoff material and fake host commands."""
import hashlib
import importlib.util
import json
import subprocess
import tempfile
import unittest
import uuid
from pathlib import Path
from unittest.mock import patch


def load(name, filename):
    spec = importlib.util.spec_from_file_location(name, Path(__file__).with_name(filename))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


module = load('seed_tuesday_review', 'seed-homedev-tuesday-review.py')
helper = load('extend_review', 'extend-review-credentials.py')


class SeedTuesdayReviewTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory(dir=Path(tempfile.gettempdir()).resolve())
        self.root = Path(self.tmp.name) / ('a' * 40)
        self.root.mkdir(mode=0o700)
        for name in ('.runtime', 'data'):
            (self.root / name).mkdir(mode=0o700)
        self.sha = self.root.name
        self.ids = {name: str(uuid.uuid4()) for name in module.ALL_IDENTITIES}
        self.verifier = self.root / '.runtime/review-users.json'
        self.target = self.root / '.runtime/tuesday-review-login-handoff.json'
        self.credentials = {name: 'existing-' + name for name in module.LEGACY}
        users = []
        for name, password in self.credentials.items():
            salt = b'\x02' * 16
            users.append({'userName': name, 'userId': self.ids[name], 'salt': salt.hex(),
                          'hash': hashlib.pbkdf2_hmac('sha256', password.encode(), salt, 600_000).hex()})
        self.verifier.write_text(json.dumps({'users': users}))
        self.verifier.chmod(0o600)
        self.legacy = users
        self.events, self.sleeps = [], []
        self.snapshot = {'projects': [{'id': str(uuid.uuid4()), 'number': n, 'visibility': 'Restricted' if n == 'SYN-103' else 'Open'}
                                     for n in ('SYN-101', 'SYN-102', 'SYN-103')],
                         'counts': {**module.FIXTURE_MINIMUMS, 'planning_entry': 7, 'project_template': 1}}

    def tearDown(self):
        self.tmp.cleanup()

    def runner(self, command, **kwargs):
        self.events.append(command)
        output = ''
        if 'psql' in command:
            if command[-1].startswith('SELECT email'):
                output = '\n'.join(f'{n}@hub.test|dev-{n}@hub.test|{self.ids[n]}' for n in module.ALL_IDENTITIES)
            else:
                output = json.dumps(self.snapshot)
        elif 'extend-review-credentials.py' in ' '.join(command):
            # Use the actual helper, rather than mocking away credential binding.
            mappings = command[command.index('--new-private-handoff-file') + 2:]
            helper.extend(self.verifier, self.target, mappings)
        elif any('seed_' in arg and arg.endswith('.py') for arg in command):
            output = 'All fixture steps completed; fictional data only.'
        return subprocess.CompletedProcess(command, 0, output, '')

    def run_setup(self):
        return module.run([self.sha], root=self.root, verify=lambda _: None, runner=self.runner,
                          sleeper=self.sleeps.append, hosted_verify=lambda *_: self.events.append(['hosted-verify']), now=lambda: 'now')

    def test_guard_precedes_every_command(self):
        with self.assertRaises(module.WrapperError):
            module.run([self.sha], root=self.root, verify=lambda _: (_ for _ in ()).throw(module.WrapperError('guard')), runner=self.runner)
        self.assertEqual(self.events, [])

    def test_complete_setup_preserves_three_passwords_and_verifies_actual_dataset(self):
        self.run_setup()
        users = json.loads(self.verifier.read_text())['users']
        self.assertEqual(users[:3], self.legacy)
        new = module.read_handoff(self.target, 10)
        self.assertTrue(module.credentials_match(users, new))
        self.assertTrue(module.credentials_match(users, self.credentials))
        self.assertEqual(len(users), 13)
        self.assertEqual(self.sleeps, [60, 60, 60])
        health = next(e for e in self.events if 'curl' in e)
        self.assertIn('Host: pm.engcalchub.com', health)
        self.assertIn('X-Forwarded-Proto: https', health)
        recreate = next(e for e in self.events if '--force-recreate' in e)
        self.assertIn('RELEASE_SHA=' + self.sha, recreate)
        manifest = json.loads((self.root / 'data' / f'tuesday-fixtures-{self.sha}.json').read_text())
        self.assertEqual(manifest['counts']['planning_entry'], 7)
        self.assertEqual(manifest['projects'], self.snapshot['projects'])
        text = json.dumps(manifest)
        for password in new.values():
            self.assertNotIn(password, text)
        extension = next(e for e in self.events if 'extend-review-credentials.py' in ' '.join(e))
        self.assertEqual(extension[extension.index('--verifier-file') + 1], str(self.verifier.resolve()))

    def test_completed_rerun_reads_and_validates_without_recreating_or_reseeding(self):
        self.run_setup()
        before = self.verifier.read_bytes()
        self.events.clear()
        self.run_setup()
        self.assertTrue(all('psql' in e for e in self.events))
        self.assertEqual(self.verifier.read_bytes(), before)

    def test_incomplete_dataset_is_not_a_successful_rerun(self):
        self.run_setup()
        self.snapshot['counts']['handoff'] = 0
        with self.assertRaises(module.WrapperError):
            self.run_setup()

    def test_wrong_reserved_identity_is_refused_before_credential_write(self):
        def bad(command, **kwargs):
            self.events.append(command)
            rows = '\n'.join(f'{n}@hub.test|real-directory-object|{self.ids[n]}' for n in module.ALL_IDENTITIES)
            return subprocess.CompletedProcess(command, 0, rows, '')
        with self.assertRaises(module.WrapperError):
            module.run([self.sha], root=self.root, verify=lambda _: None, runner=bad)
        self.assertFalse(self.target.exists())
        self.assertEqual(len(self.events), 1)

    def test_changed_handoff_password_refuses_rotation(self):
        self.run_setup()
        manifest = self.root / 'data' / f'tuesday-fixtures-{self.sha}.json'
        manifest.unlink()
        payload = json.loads(self.target.read_text())
        payload['credentials'][0]['password'] = 'incorrect'
        self.target.write_text(json.dumps(payload))
        before = self.verifier.read_bytes()
        with self.assertRaises(module.WrapperError):
            self.run_setup()
        self.assertEqual(self.verifier.read_bytes(), before)

    def test_failed_seed_retains_private_error_and_no_completion_manifest(self):
        actual = self.runner
        def failing(command, **kwargs):
            if any(arg.endswith('seed_modules.py') for arg in command):
                raise subprocess.CalledProcessError(1, command, output='REFUSED handoff: HTTP 403', stderr='fixture failed')
            return actual(command, **kwargs)
        with self.assertRaisesRegex(module.WrapperError, 'seed_modules.py failed'):
            module.run([self.sha], root=self.root, verify=lambda _: None, runner=failing,
                       sleeper=lambda _: None, hosted_verify=lambda *_: None)
        self.assertFalse((self.root / 'data' / f'tuesday-fixtures-{self.sha}.json').exists())
        log = self.root / '.runtime' / f'seed_modules.py-{self.sha}.log.json'
        self.assertIn('HTTP 403', log.read_text())
        self.assertEqual(log.stat().st_mode & 0o777, 0o600)


if __name__ == '__main__':
    unittest.main()
