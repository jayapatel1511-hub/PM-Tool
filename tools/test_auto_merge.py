import sys
import unittest
from unittest.mock import patch

sys.path.insert(0, "tools")
import auto_merge


class AutoMergeTests(unittest.TestCase):
    def test_tier_b_paths_are_blocked_but_docs_are_tier_a(self):
        self.assertEqual(auto_merge.tier_b_paths(["docs/runbooks/delivery.md"]), set())
        self.assertIn(".github/workflows/ci.yml", auto_merge.tier_b_paths([".github/workflows/ci.yml"]))
        self.assertEqual(auto_merge.tier_b_paths(["specs/039-delivery-process/verification.md"]), set())
        self.assertNotIn("src/Hub.Api/Data/Migrations/1_Add.cs",
                         auto_merge.tier_b_paths(["src/Hub.Api/Data/Migrations/1_Add.cs"]))
        self.assertIn("src/Hub.Api/Data/Migrations/1_Drop.cs", auto_merge.tier_b_paths(
            ["src/Hub.Api/Data/Migrations/1_Drop.cs"], {"src/Hub.Api/Data/Migrations/1_Drop.cs"}))

    def test_all_required_statuses_must_be_green_at_head(self):
        runs = [{"name": name, "conclusion": "success"} for name in auto_merge.REQUIRED]
        self.assertTrue(auto_merge.checks_green(runs))
        runs[-1]["conclusion"] = "failure"
        self.assertFalse(auto_merge.checks_green(runs))
        jay = {"login": "synthetic"}
        self.assertTrue(auto_merge.review_green([{"context": "review", "state": "success", "creator": jay}], {"synthetic"}))
        self.assertFalse(auto_merge.review_green([{"context": "review", "state": "failure", "creator": jay}], {"synthetic"}))

    def test_review_status_counts_only_from_a_trusted_reviewer(self):
        stranger = {"context": "review", "state": "success", "creator": {"login": "someone-else"}}
        self.assertFalse(auto_merge.review_green([stranger], {"synthetic"}))
        self.assertFalse(auto_merge.review_green([{"context": "review", "state": "success", "creator": None}], {"synthetic"}))
        # Newest first: a later trusted failure wins over an earlier trusted success; strangers are ignored.
        trusted = lambda state: {"context": "review", "state": state, "creator": {"login": "synthetic"}}
        self.assertFalse(auto_merge.review_green([stranger, trusted("failure"), trusted("success")], {"synthetic"}))
        self.assertTrue(auto_merge.review_green([stranger, trusted("success"), trusted("failure")], {"synthetic"}))

    def test_latest_check_wins_and_hidden_second_page_blocks_merge(self):
        runs = [{"id": 2, "name": name, "conclusion": "success"} for name in auto_merge.REQUIRED]
        runs += [{"id": 3, "name": "tier", "conclusion": "failure"}, {"id": 1, "name": "tier", "conclusion": "success"}]
        self.assertFalse(auto_merge.checks_green(runs))
        def api(repo, path):
            if path.endswith('/pulls'):
                return [{"number": 1}]
            if path == 'pulls/1':
                return {"head": {"sha": "a"*40}, "base": {"ref": "main"}, "changed_files": 101}
            if 'files?' in path:
                return [{"filename": "docs/synthetic.md", "patch": "+safe"}]*100
            raise AssertionError(path)
        with patch.object(auto_merge, 'gh', side_effect=api), patch.object(auto_merge.subprocess, 'run') as merge:
            self.assertEqual(auto_merge.main(['--repo','synthetic/repo','--sha','a'*40]), 0)
            merge.assert_not_called()

    @staticmethod
    def api_for(sha, reviewer):
        def api(repo, path):
            if path.endswith('/pulls'): return [{"number": 1}]
            if path == 'pulls/1': return {"head": {"sha": sha}, "base": {"ref": "main"}, "changed_files": 1}
            if 'files?' in path: return [{"filename": "tools/ci_steps.txt", "patch": "+checks: synthetic test"}]
            if 'check-runs?' in path: return {"check_runs": [{"id": 1, "name": n, "conclusion": "success"} for n in auto_merge.REQUIRED]}
            if '/statuses?' in path: return [{"context": "review", "state": "success", "creator": {"login": reviewer}}]
            raise AssertionError(path)
        return api

    def test_untrusted_review_status_does_not_merge(self):
        sha = 'a'*40
        with patch.dict(auto_merge.os.environ, {"REVIEWER_LOGINS": ""}), \
                patch.object(auto_merge, 'gh', side_effect=self.api_for(sha, 'someone-else')), \
                patch.object(auto_merge.subprocess, 'run') as merge:
            self.assertEqual(auto_merge.main(['--repo','synthetic/repo','--sha',sha]), 0)
            merge.assert_not_called()
        with patch.dict(auto_merge.os.environ, {"REVIEWER_LOGINS": "reviewer-bot,other"}), \
                patch.object(auto_merge, 'gh', side_effect=self.api_for(sha, 'reviewer-bot')), \
                patch.object(auto_merge.subprocess, 'run') as merge:
            auto_merge.main(['--repo','synthetic/repo','--sha',sha])
            merge.assert_called_once()

    def test_reviewer_after_ci_enables_exact_head_docs_merge(self):
        sha = 'a'*40
        with patch.dict(auto_merge.os.environ, {"REVIEWER_LOGINS": ""}), \
                patch.object(auto_merge, 'gh', side_effect=self.api_for(sha, 'synthetic')), \
                patch.object(auto_merge.subprocess, 'run') as merge:
            self.assertEqual(auto_merge.main(['--repo','synthetic/repo','--sha',sha]), 0)
            command = merge.call_args.args[0]
            self.assertIn('--match-head-commit', command)
            self.assertEqual(command[-1], sha)
            self.assertIn('-R', command)


if __name__ == "__main__":
    unittest.main()
