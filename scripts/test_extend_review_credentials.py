#!/usr/bin/env python3
import importlib.util
import json
import os
import stat
import tempfile
import unittest
import uuid
from pathlib import Path
from unittest.mock import patch


MODULE_PATH = Path(__file__).with_name("extend-review-credentials.py")
SPEC = importlib.util.spec_from_file_location("extend_review_credentials", MODULE_PATH)
assert SPEC and SPEC.loader
module = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(module)


class ExtendReviewCredentialsTests(unittest.TestCase):
    def setUp(self) -> None:
        self.tmp = tempfile.TemporaryDirectory(dir=Path(tempfile.gettempdir()).resolve())
        self.root = Path(self.tmp.name)
        os.chmod(self.root, 0o700)
        self.verifier = self.root / "review-users.json"
        self.handoff = self.root / "new-handoff.json"
        self.verifier.write_text(json.dumps({"users": []}) + "\n")
        os.chmod(self.verifier, 0o600)

    def tearDown(self) -> None:
        self.tmp.cleanup()

    def test_append_preserves_existing_and_creates_strong_private_handoff(self) -> None:
        old_id = str(uuid.uuid4())
        self.verifier.write_text(json.dumps({"users": [{
            "userId": old_id, "userName": "jordan", "salt": "00" * 16, "hash": "11" * 32,
        }]}) + "\n")
        with patch.object(module.secrets, "token_urlsafe", return_value="new-password-value"), patch.object(module.os, "urandom", return_value=b"\x01" * 16):
            added = module.extend(self.verifier, self.handoff, ["jordan:" + old_id, "rita:" + str(uuid.uuid4())])
        self.assertEqual(added, 1)
        stored = json.loads(self.verifier.read_text())
        self.assertEqual(stored["users"][0]["userId"], old_id)
        self.assertEqual(stored["users"][0]["hash"], "11" * 32)
        self.assertEqual(json.loads(self.handoff.read_text())["credentials"][0]["password"], "new-password-value")
        self.assertEqual(stat.S_IMODE(self.verifier.stat().st_mode), 0o600)
        self.assertEqual(stat.S_IMODE(self.handoff.stat().st_mode), 0o600)

    def test_exact_rerun_is_noop_without_overwriting_handoff(self) -> None:
        user_id = str(uuid.uuid4())
        with patch.object(module.secrets, "token_urlsafe", return_value="password"):
            module.extend(self.verifier, self.handoff, ["alex:" + user_id])
        before = self.verifier.read_bytes()
        self.handoff.unlink()
        self.assertEqual(module.extend(self.verifier, self.handoff, ["alex:" + user_id]), 0)
        self.assertEqual(self.verifier.read_bytes(), before)
        self.assertFalse(self.handoff.exists())

    def test_refuses_collisions_duplicates_and_nonwhitelisted_logins(self) -> None:
        user_id = str(uuid.uuid4())
        with self.assertRaises(ValueError):
            module.extend(self.verifier, self.handoff, ["alex:" + user_id, "alex:" + str(uuid.uuid4())])
        with self.assertRaises(ValueError):
            module.extend(self.verifier, self.handoff, ["not-on-list:" + str(uuid.uuid4())])
        with self.assertRaises(ValueError):
            module.extend(self.verifier, self.handoff, ["alex:" + user_id, "rita:" + user_id])

    def test_refuses_existing_handoff_and_nonprivate_parent(self) -> None:
        self.handoff.write_text("do not replace")
        os.chmod(self.handoff, 0o600)
        with self.assertRaises(ValueError):
            module.extend(self.verifier, self.handoff, ["alex:" + str(uuid.uuid4())])
        public = self.root / "public"
        public.mkdir(mode=0o755)
        os.chmod(public, 0o755)
        with self.assertRaises(ValueError):
            module.extend(public / "users.json", public / "handoff.json", ["alex:" + str(uuid.uuid4())])

    def test_verifier_failure_leaves_existing_verifier_and_recoverable_handoff(self) -> None:
        before = self.verifier.read_bytes()
        real_write = module.atomic_write
        calls = 0

        def fail_verifier_on_second_write(path, value):
            nonlocal calls
            calls += 1
            if calls == 2:
                raise OSError("injected verifier write failure")
            return real_write(path, value)

        with patch.object(module, "atomic_write", side_effect=fail_verifier_on_second_write):
            with self.assertRaises(OSError):
                module.extend(self.verifier, self.handoff, ["rita:" + str(uuid.uuid4())])
        self.assertEqual(self.verifier.read_bytes(), before)
        self.assertTrue(self.handoff.exists())
        self.assertEqual(stat.S_IMODE(self.handoff.stat().st_mode), 0o600)
        handoff = json.loads(self.handoff.read_text())
        self.assertEqual(len(handoff["credentials"]), 1)
        self.assertIn("password", handoff["credentials"][0])

    def test_handoff_failure_leaves_existing_verifier_unchanged(self) -> None:
        before = self.verifier.read_bytes()
        with patch.object(module, "atomic_write", side_effect=OSError("injected handoff write failure")):
            with self.assertRaises(OSError):
                module.extend(self.verifier, self.handoff, ["omar:" + str(uuid.uuid4())])
        self.assertEqual(self.verifier.read_bytes(), before)
        self.assertFalse(self.handoff.exists())

    def test_refuses_malformed_existing_credential_material(self) -> None:
        self.verifier.write_text(json.dumps({"users": [{
            "userId": str(uuid.uuid4()), "userName": "jordan", "salt": "00", "hash": "11" * 32,
        }]}) + "\n")
        with self.assertRaises(ValueError):
            module.extend(self.verifier, self.handoff, ["alex:" + str(uuid.uuid4())])

    def test_refuses_symlink_ancestor(self) -> None:
        target = self.root / "target"
        target.mkdir(mode=0o700)
        os.chmod(target, 0o700)
        link = self.root / "link"
        link.symlink_to(target, target_is_directory=True)
        with self.assertRaises(ValueError):
            module.extend(link / "users.json", link / "handoff.json", ["alex:" + str(uuid.uuid4())])


if __name__ == "__main__":
    unittest.main()
