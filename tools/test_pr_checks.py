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


def migration(up, name="Change"):
    return f"""using Microsoft.EntityFrameworkCore.Migrations;

namespace Hub.Api.Data.Migrations
{{
    public partial class {name} : Migration
    {{
        protected override void Up(MigrationBuilder migrationBuilder)
        {{
{up}
        }}

        protected override void Down(MigrationBuilder migrationBuilder)
        {{
            migrationBuilder.DropColumn(name: "x", schema: "hub", table: "t");
            migrationBuilder.Sql("DROP TABLE hub.t;");
        }}
    }}
}}
"""


ADDITIVE = """            migrationBuilder.AddColumn<string>(name: "x", schema: "hub", table: "t", type: "text", nullable: true);
            migrationBuilder.CreateTable(name: "u", schema: "hub", columns: table => new { id = table.Column<Guid>(type: "uuid", nullable: false),
                deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true) },
                constraints: table => table.ForeignKey(name: "fk", column: x => x.id, principalTable: "t", onDelete: ReferentialAction.Cascade));
            migrationBuilder.CreateIndex(name: "ix", schema: "hub", table: "t", column: "x");
            // Idempotent backfill: safe to re-run.
            migrationBuilder.Sql(\"\"\"
                UPDATE hub.t SET x = 'drop me' WHERE x IS NULL; -- DELETE nothing
                ALTER TABLE hub.u ADD CONSTRAINT fk2 FOREIGN KEY (id) REFERENCES hub.t (id) ON DELETE CASCADE;
                \"\"\");"""


class MigrationLintTests(unittest.TestCase):
    """AC-DLV-14: contract operations in added migrations fail unless `contract` is applied."""

    def setUp(self):
        self.repo = tempfile.mkdtemp()
        self.addCleanup(shutil.rmtree, self.repo)
        git(self.repo, "init", "-q")
        git(self.repo, "config", "user.email", "t@example.invalid")
        git(self.repo, "config", "user.name", "t")
        # An existing migration that drops a column is not linted.
        self.write("20200101000000_Old.cs", migration('            migrationBuilder.DropColumn(name: "y", schema: "hub", table: "t");', "Old"))
        git(self.repo, "add", ".")
        git(self.repo, "commit", "-qm", "base")
        self.base = subprocess.run(["git", "-C", self.repo, "rev-parse", "HEAD"], capture_output=True, text=True, check=True).stdout.strip()

    def write(self, name, text):
        file = pathlib.Path(self.repo, pr_checks.MIGRATIONS, name)
        file.parent.mkdir(parents=True, exist_ok=True)
        file.write_text(text)

    def add(self, up, designer=""):
        self.write("20990101000000_Change.cs", migration(up))
        self.write("20990101000000_Change.Designer.cs", designer or 'migrationBuilder.DropTable(name: "generated");')
        self.write("HubDbModelSnapshot.cs", 'b.DropColumn("generated");')
        # The existing migration is edited too: still not linted.
        self.write("20200101000000_Old.cs", migration('            migrationBuilder.RenameColumn(name: "a", table: "t", newName: "b");', "Old"))
        git(self.repo, "add", ".")
        git(self.repo, "commit", "-qm", "change")

    def run_lint(self, labels=""):
        out = io.StringIO()
        with contextlib.redirect_stdout(out), contextlib.chdir(self.repo):
            code = pr_checks.main(["migrations", "--base", self.base, "--labels", labels])
        return code, out.getvalue()

    def test_additive_migration_with_idempotent_backfill_passes(self):
        self.add(ADDITIVE)
        self.assertEqual(self.run_lint(), (0, "No contract operations in added migrations.\n"))

    def test_each_contract_operation_fails_and_names_file_and_operation(self):
        cases = {
            'migrationBuilder.DropColumn(name: "x", schema: "hub", table: "t");': "DropColumn",
            'migrationBuilder.RenameColumn(name: "x", schema: "hub", table: "t", newName: "y");': "RenameColumn",
            'migrationBuilder.RenameTable(name: "t", schema: "hub", newName: "u");': "RenameTable",
            'migrationBuilder.AlterColumn<string>(name: "x", schema: "hub", table: "t", nullable: false);': "AlterColumn",
            'migrationBuilder.DropCheckConstraint(name: "ck", schema: "hub", table: "t");': "DropCheckConstraint",
            'migrationBuilder.DropForeignKey(name: "fk", schema: "hub", table: "t");': "DropForeignKey",
            'migrationBuilder.DropTable(name: "t", schema: "hub");': "DropTable",
            'migrationBuilder.Sql("DROP TABLE hub.t;");': "Sql DROP",
            'migrationBuilder.Sql(@"truncate hub.t");': "Sql TRUNCATE",
            'migrationBuilder.Sql($"DELETE FROM hub.{table} WHERE x IS NULL");': "Sql DELETE",
            'migrationBuilder.Sql("ALTER TABLE hub.t RENAME COLUMN x TO y");': "Sql RENAME",
            'migrationBuilder.Sql("""\n    ALTER TABLE hub.t ALTER COLUMN x TYPE integer USING x::integer;\n    """);': "Sql ALTER ... TYPE",
            'migrationBuilder.Sql("ALTER TABLE hub.t " +\n                "DROP COLUMN x;");': pr_checks.UNVERIFIABLE,
            'migrationBuilder.Sql(DropSql);': pr_checks.UNVERIFIABLE,
            'migrationBuilder.Sql("SELECT 1", false);': pr_checks.UNVERIFIABLE,
            'migrationBuilder.Sql("DO $$ BEGIN EXECUTE \'DROP TABLE hub.t\'; END $$");': "Sql dynamic EXECUTE",
        }
        for up, op in cases.items():
            with self.subTest(op=op):
                self.setUp()
                self.add(ADDITIVE + "\n            " + up)
                code, out = self.run_lint()
                self.assertEqual(code, 1)
                self.assertEqual(out, f"::error file={pr_checks.MIGRATIONS}20990101000000_Change.cs::{op} is a contract operation."
                                      " Expand only, or Jay applies the label `contract`.\n")

    def test_append_only_guard_trigger_passes(self):
        # Copied from 20261001213118_TaskStartAuthorisation and 20260925015745_InitialSchema.
        self.add(ADDITIVE + """
            migrationBuilder.Sql(\"\"\"
                CREATE FUNCTION hub.guard_task_start_authorisation() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF TG_OP = 'DELETE' THEN RAISE EXCEPTION 'Task start authorisations cannot be deleted'; END IF;
                    RETURN NEW;
                END $$;
                CREATE TRIGGER guard_task_start_authorisation BEFORE UPDATE OR DELETE ON hub.task_start_authorisation
                    FOR EACH ROW EXECUTE FUNCTION hub.guard_task_start_authorisation();
                CREATE TRIGGER guard_output_commitment BEFORE INSERT OR UPDATE OF status, owner OR DELETE ON hub.output_commitment
                    FOR EACH ROW EXECUTE FUNCTION hub.guard_task_start_authorisation();
                \"\"\", suppressTransaction: false);
            migrationBuilder.Sql(@"CREATE TRIGGER activity_log_no_truncate BEFORE TRUNCATE ON hub.activity_log FOR EACH STATEMENT EXECUTE FUNCTION hub.activity_log_immutable();");""")
        self.assertEqual(self.run_lint(), (0, "No contract operations in added migrations.\n"))

    def test_repository_guard_trigger_migrations_have_no_sql_contract_operations(self):
        for name in ("20260925015745_InitialSchema", "20260927210259_SubmissionReadinessSchema", "20260927234721_DesignBasisSchema",
                     "20260928003457_ReadinessSchema", "20261001213118_TaskStartAuthorisation"):
            with self.subTest(name=name):
                source = (ROOT / pr_checks.MIGRATIONS / f"{name}.cs").read_text()
                self.assertEqual(pr_checks.contract_operations(source), [])

    def test_contract_label_passes(self):
        self.add('            migrationBuilder.DropColumn(name: "x", schema: "hub", table: "t");')
        code, out = self.run_lint("tier-b, contract")
        self.assertEqual(code, 0)
        self.assertIn("::warning file=", out)
        self.assertIn("DropColumn (label `contract` applied)", out)

    def test_down_designer_snapshot_and_existing_migrations_are_not_linted(self):
        self.add(ADDITIVE)
        self.assertEqual(self.run_lint()[0], 0)
        self.assertEqual(pr_checks.added_migrations(self.base, cwd=self.repo), [f"{pr_checks.MIGRATIONS}20990101000000_Change.cs"])

    def test_repository_migrations_are_existing_and_not_linted(self):
        # The current tree adds no migration against itself; the lint passes on an empty diff.
        self.assertEqual(pr_checks.migrations("HEAD", "HEAD", cwd=str(ROOT)), (0, "No contract operations in added migrations."))


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
