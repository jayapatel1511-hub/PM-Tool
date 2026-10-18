"""AC-OPS-01, AC-OPS-02: the public build contains assets, never construction metadata."""
import tempfile
from pathlib import Path
import unittest
from tools.check_served_assets import violations


class ServedAssetsTests(unittest.TestCase):
    def test_clean_assets_pass_and_vite_manifest_is_skipped(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            (root / '.vite').mkdir()
            (root / '.vite/manifest.json').write_text('{}')
            for name in ['index.html', 'app.js', 'font.woff2', 'font-OFL.txt', 'board.jpg']:
                (root / name).write_text('synthetic')
            self.assertEqual(violations(root), [])

    def test_every_forbidden_path_and_source_map_is_named(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            for name in ['capture.json', 'README.md', 'app.map']:
                (root / name).write_text('{}')
            (root / 'app.js').write_text('//# sourceMappingURL=app.map')
            errors = '\n'.join(violations(root))
            for name in ['capture.json', 'README.md', 'app.map', 'app.js']:
                self.assertIn(name, errors)

    def test_missing_build_fails(self):
        self.assertIn('missing', violations('/nonexistent/tuesday-build')[0])


if __name__ == '__main__':
    unittest.main()
