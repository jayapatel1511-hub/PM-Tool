import contextlib
import io
import pathlib
import shutil
import subprocess
import sys
import tempfile
import unittest

ROOT = pathlib.Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "tools"))
import pr_checks  # noqa: E402

DESIGNER = "src/Hub.Api/Data/Migrations/20990101000000_Big.Designer.cs"
HAND = "src/Hub.Api/Features/Example.cs"


def git(repo, *args):
    subprocess.run(["git", "-C", repo, *args], check=True, capture_output=True)


class SizeCheckTests(unittest.TestCase):
    """AC-DLV-02: limits of FR-DLV-06 on a real git diff, generated paths skipped."""

    def setUp(self):
        self.repo = tempfile.mkdtemp()
        self.addCleanup(shutil.rmtree, self.repo)
        git(self.repo, "init", "-q")
        git(self.repo, "config", "user.email", "t@example.invalid")
        git(self.repo, "config", "user.name", "t")
        shutil.copy(ROOT / ".gitattributes", self.repo)
        git(self.repo, "add", ".")
        git(self.repo, "commit", "-qm", "base")
        self.base = subprocess.run(["git", "-C", self.repo, "rev-parse", "HEAD"], capture_output=True, text=True, check=True).stdout.strip()

    def change(self, hand_lines, designer_lines=11000):
        for path, n in ((HAND, hand_lines), (DESIGNER, designer_lines)):
            file = pathlib.Path(self.repo, path)
            file.parent.mkdir(parents=True, exist_ok=True)
            file.write_text("".join(f"line {i}\n" for i in range(n)))
        git(self.repo, "add", ".")
        git(self.repo, "commit", "-qm", "change")

    def run_size(self, labels=""):
        out = io.StringIO()
        with contextlib.redirect_stdout(out), contextlib.chdir(self.repo):
            code = pr_checks.main(["size", "--base", self.base, "--labels", labels])
        return code, out.getvalue()

    def test_350_hand_written_lines_with_11000_line_designer_passes_without_warning(self):
        self.change(350)
        code, out = self.run_size()
        self.assertEqual(code, 0)
        self.assertNotIn("::warning::", out)
        self.assertIn("350 hand-written", out)

    def test_450_lines_warns_and_passes(self):
        self.change(450)
        code, out = self.run_size()
        self.assertEqual(code, 0)
        self.assertIn("::warning::450", out)

    def test_1050_lines_fails_and_large_label_passes(self):
        self.change(1050)
        code, out = self.run_size()
        self.assertEqual(code, 1)
        self.assertIn("::error::1050", out)
        code, out = self.run_size("tier-b, large")
        self.assertEqual(code, 0)
        self.assertIn("::warning::1050", out)

    def test_generated_path_is_skipped_even_when_diffed_as_text(self):
        # A hand-written count above the limit only in the generated file must still pass.
        pathlib.Path(self.repo, ".gitattributes").write_text(f"{DESIGNER} linguist-generated\n")
        git(self.repo, "commit", "-qam", "attributes without -diff")
        self.base = subprocess.run(["git", "-C", self.repo, "rev-parse", "HEAD"], capture_output=True, text=True, check=True).stdout.strip()
        self.change(10, 5000)
        self.assertEqual(self.run_size(), (0, "10 hand-written changed lines.\n"))

    def test_renamed_file_counts_under_new_path(self):
        self.change(10, 0)
        git(self.repo, "mv", HAND, "src/Moved.cs")
        git(self.repo, "commit", "-qm", "move")
        self.assertEqual(pr_checks.changed_lines("HEAD~1", cwd=self.repo), {"src/Moved.cs": 0})

    def test_limits_are_inclusive_at_400_and_1000(self):
        self.assertEqual(pr_checks.size_verdict(400, set())[0], 0)
        self.assertNotIn("warning", pr_checks.size_verdict(400, set())[1])
        self.assertEqual(pr_checks.size_verdict(1000, set())[0], 0)
        self.assertEqual(pr_checks.size_verdict(1001, set())[0], 1)


class GeneratedAttributeTests(unittest.TestCase):
    """AC-DLV-03: `git check-attr` on the repository's own paths."""

    def attr(self, path):
        out = subprocess.run(["git", "-C", str(ROOT), "check-attr", "linguist-generated", "diff", "--", path],
                             capture_output=True, text=True, check=True).stdout
        return out

    def test_each_generated_path_is_marked(self):
        migrations = ROOT / "src/Hub.Api/Data/Migrations"
        designer = next(migrations.glob("*.Designer.cs")).relative_to(ROOT).as_posix()
        for path in (designer, "src/Hub.Api/Data/Migrations/HubDbModelSnapshot.cs",
                     "Engineering-Project-Coordination-Hub-Specification.md",
                     "Engineering-Project-Coordination-Hub-Specification.html",
                     "specs/TRACEABILITY.md", "web/package-lock.json"):
            with self.subTest(path=path):
                self.assertTrue((ROOT / path).exists())
                self.assertEqual(self.attr(path), f"{path}: linguist-generated: set\n{path}: diff: unset\n")

    def test_no_hand_written_source_is_marked(self):
        tracked = subprocess.run(["git", "-C", str(ROOT), "ls-files"], capture_output=True, text=True, check=True).stdout.split()
        marked = pr_checks.generated(tracked, cwd=str(ROOT))
        hand = [p for p in marked if not (p.endswith(".Designer.cs") or p.endswith("HubDbModelSnapshot.cs"))]
        self.assertEqual(sorted(hand), ["Engineering-Project-Coordination-Hub-Specification.html",
                                        "Engineering-Project-Coordination-Hub-Specification.md",
                                        "specs/TRACEABILITY.md", "web/package-lock.json"])
        for path in ("src/Hub.Api/Data/Migrations/20260925015745_InitialSchema.cs", "spec-parts/18-delivery-process.md",
                     "web/package.json", "specs/README.md", "tools/pr_checks.py"):
            with self.subTest(path=path):
                self.assertNotIn(path, marked)


if __name__ == "__main__":
    unittest.main()
