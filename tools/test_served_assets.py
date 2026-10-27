"""AC-OPS-01, AC-OPS-02: the public build contains assets, never construction metadata."""
import tempfile
from pathlib import Path
import unittest
from tools.check_served_assets import violations

ROOT = Path(__file__).resolve().parents[1]
METADATA = ['capture.json', 'README.md', 'fonts/sources.json']
KEPT = ['board.svg', 'tuesday-board.jpg', 'THIRD-PARTY-NOTICES.txt', 'fonts/dmsans-OFL.txt', 'fonts/dm-sans-normal-1.woff2']


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

    def test_landing_assets_stay_served_and_the_metadata_moved_to_docs(self):
        """AC-OPS-01: images, fonts and licence notices stay in the source folder and in a built output when one exists; the three files do not."""
        roots = [ROOT / 'web/public/landing'] + ([ROOT / 'src/Hub.Api/wwwroot/landing'] if (ROOT / 'src/Hub.Api/wwwroot/landing').is_dir() else [])
        for root in roots:
            for name in KEPT:
                self.assertTrue((root / name).is_file(), f'{root}/{name} must stay served')
            for name in METADATA:
                self.assertFalse((root / name).exists(), f'{root}/{name} must not be served')
        for name in METADATA:
            self.assertTrue((ROOT / 'docs/landing-assets' / name).is_file(), f'docs/landing-assets/{name} missing')


if __name__ == '__main__':
    unittest.main()
