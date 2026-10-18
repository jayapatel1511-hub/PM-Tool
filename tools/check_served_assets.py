#!/usr/bin/env python3
"""FR-OPS-02: reject build metadata and source maps from the public web output."""
from pathlib import Path
import sys

ALLOWED = {'.html', '.js', '.css', '.svg', '.jpg', '.jpeg', '.png', '.webp', '.ico', '.woff2', '.txt'}
TEXT = {'.html', '.js', '.css', '.svg', '.txt'}


def violations(root):
    root = Path(root)
    if not root.is_dir():
        return [f'{root}: build output missing']
    errors = []
    for path in sorted(root.rglob('*')):
        relative = path.relative_to(root)
        if relative.parts[0] == '.vite' or not path.is_file():
            continue
        if path.suffix.lower() not in ALLOWED:
            errors.append(f'{relative}: extension outside served allowlist')
        if path.suffix.lower() in TEXT and 'sourceMappingURL' in path.read_text(errors='replace'):
            errors.append(f'{relative}: sourceMappingURL')
    return errors


if __name__ == '__main__':
    errors = violations(sys.argv[1] if len(sys.argv) == 2 else 'src/Hub.Api/wwwroot')
    print('\n'.join(errors) if errors else 'PASS served asset allowlist')
    sys.exit(bool(errors))
