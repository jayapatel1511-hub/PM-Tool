#!/usr/bin/env python3
"""Merge test-host JSON shapes and detect contract drift (§44.4)."""
import argparse
import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def merge(documents):
    merged = {}
    for document in documents:
        for operation, paths in document.items():
            # Route-group roots retain a slash in RawText; OpenAPI omits it.
            operation = re.sub(r"^([A-Z]+ /\S*?)/ (\d{3})$", r"\1 \2", operation)
            dest = merged.setdefault(operation, {})
            for path, kinds in paths.items():
                # Dictionary IDs vary per fixture; their values still carry the complete shape.
                path = re.sub(r'\.[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}(?=\.|\[|$)', '.{id}', path)
                dest.setdefault(path, set()).update(kinds)
    return {op: {p: sorted(k) for p, k in sorted(paths.items())} for op, paths in sorted(merged.items())}


def differences(before, after):
    errors = []
    for operation in sorted(before.keys() | after.keys()):
        old, new = before.get(operation, {}), after.get(operation, {})
        # Empty arrays and null parents cannot reveal their children's shape in this run.
        unknown = [p for p, kinds in new.items() if kinds == ['null'] or ('array' in kinds and p + '[]' not in new)]
        for path in sorted(old.keys() | new.keys()):
            if path not in new:
                if not any(path.startswith(p + '.') or path.startswith(p + '[]') for p in unknown):
                    errors.append(f'{operation}: removed {path}')
            elif path not in old:
                errors.append(f'{operation}: added {path}')
            else:
                was, now = set(old[path]) - {'null'}, set(new[path]) - {'null'}
                if was and now and was != now:
                    errors.append(f'{operation}: {path} kind changed {sorted(was)} -> {sorted(now)}')
    return errors


def operations(document):
    for path, methods in document.get('paths', {}).items():
        for method, operation in methods.items():
            if method not in {'get', 'post', 'put', 'patch', 'delete', 'head', 'options'}:
                continue
            for status, response in operation.get('responses', {}).items():
                content = response.get('content', {}).get('application/json', {})
                if content.get('schema'):
                    yield f'{method.upper()} {path} {status}'


def recorded_files(root):
    manifests = sorted(root.rglob('shapes/latest.json'))
    return [file for manifest in manifests for file in sorted((manifest.parent / json.loads(manifest.read_text())['run']).glob('*.json'))]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('mode', choices=['check', 'update'])
    parser.add_argument('--input', type=Path, default=ROOT / 'ci-artifacts')
    parser.add_argument('--snapshot', type=Path, default=ROOT / 'tests/Hub.Tests/Snapshots/shapes.json')
    parser.add_argument('--contract', type=Path, default=ROOT / 'web/src/gen/openapi.json')
    args = parser.parse_args()
    files = recorded_files(args.input)
    if not files:
        raise SystemExit('No recorded shapes; run the API suite first')
    actual = merge(json.loads(file.read_text()) for file in files)
    contract = json.loads(args.contract.read_text())
    typed = set(operations(contract))
    missing = sorted(typed - actual.keys())
    mapped = {f'{method.upper()} {path}' for path, methods in contract.get('paths', {}).items() for method in methods
              if method in {'get', 'post', 'patch', 'put', 'delete'}}
    called = {op.rsplit(' ', 1)[0] for op in actual}
    print(f'Endpoint coverage: {len(mapped & called)}/{len(mapped)} ({100 * len(mapped & called) / max(1, len(mapped)):.1f}%)')
    print('Uncalled endpoints:\n' + '\n'.join(sorted(mapped - called)))
    if missing:
        raise SystemExit('Typed responses without recorded shapes:\n' + '\n'.join(missing))
    if args.mode == 'update':
        args.snapshot.parent.mkdir(parents=True, exist_ok=True)
        args.snapshot.write_text(json.dumps(actual, indent=2, sort_keys=True) + '\n')
        print(f'Updated {args.snapshot}')
    else:
        errors = differences(json.loads(args.snapshot.read_text()), actual)
        if errors:
            raise SystemExit('\n'.join(errors))
        print(f'PASS: {len(actual)} response shapes')


if __name__ == '__main__':
    main()
