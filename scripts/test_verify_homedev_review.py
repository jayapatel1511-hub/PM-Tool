import pathlib
import unittest


ROOT = pathlib.Path(__file__).resolve().parents[1]


class ReviewBuildProbeTests(unittest.TestCase):
    def test_probe_accepts_an_explicit_expected_build_and_reports_missing_values(self):
        source = (ROOT / "scripts/verify-homedev-review.py").read_text()
        self.assertIn('parser.add_argument("--expect-build"', source)
        self.assertIn('health.get("build") == args.expect_build', source)
        self.assertIn("<missing>", source)


if __name__ == "__main__":
    unittest.main()
