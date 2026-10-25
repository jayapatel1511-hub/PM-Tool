import pathlib
import sys
import tempfile
import unittest

sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
import text_keys  # noqa: E402


class TextKeysTests(unittest.TestCase):
    def tree(self, text_cs, feature):
        root = pathlib.Path(tempfile.mkdtemp())
        (root / "src/Hub.Api/Features").mkdir(parents=True)
        (root / "src/Hub.Api/Text.cs").write_text(text_cs)
        (root / "src/Hub.Api/Features/F.cs").write_text(feature)
        return root

    def test_repository_has_no_missing_keys(self):
        self.assertEqual({}, text_keys.missing())

    def test_missing_key_in_a_permission_refusal_is_reported(self):
        root = self.tree('["perm.known"] = "Known",', 'Access.Demand(ok ? Allow.Yes : Allow.No("perm.unknown"));')
        self.assertEqual(["perm.unknown"], list(text_keys.missing(root)))

    def test_defined_keys_in_every_call_shape_pass(self):
        root = self.tree('["a.one"] = "1", ["a.two"] = "2", ["a.three"] = "3", ["a.four"] = "4",',
                         'Check.That(x, "field", "a.one"); Text.Get("a.two", 1); ApiException.Invalid("f", "a.three"); ApiException.Rule("c", "a.four");')
        self.assertEqual({}, text_keys.missing(root))


if __name__ == "__main__":
    unittest.main()
