#!/usr/bin/env python3
"""Validate reviewed source/test evidence, never permissions. Strict mode refuses every pending operation/policy."""
from __future__ import annotations
import argparse
import hashlib
import json
from pathlib import Path
import re
import xml.etree.ElementTree as ET

SPEC = 'spec-parts/02-roles-ia-datamodel.md'
PERMISSIONS = 'src/Hub.Domain/Permissions.cs'
CATALOG = 'src/Hub.Domain/PermissionExplanationCatalog.cs'
MANIFEST = 'tests/Hub.Tests/Snapshots/permission-explainer-bindings.json'
SECTIONS = ('8.5.1', '8.5.2', '8.10', '8.11')
ROW_IDS = {f'{prefix}{number:02}' for prefix, count in (('S', 22), ('W', 33), ('C', 18), ('P', 10)) for number in range(1, count + 1)}
TRX_NS = {'t': 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
REQUIRED_GUARDS = (
    'src/Hub.Api/Infrastructure/Auth.cs', 'src/Hub.Api/Infrastructure/Access.cs', 'src/Hub.Api/Infrastructure/PeopleScope.cs',
    'src/Hub.Api/Features/Admin.cs', 'src/Hub.Api/Features/Planning.cs', 'src/Hub.Api/Features/Availability.cs',
    'src/Hub.Api/Features/Extras.cs', 'src/Hub.Api/Features/Handoffs.cs', 'src/Hub.Api/Features/Reviews.cs',
    'src/Hub.Api/Features/Changes.cs', 'src/Hub.Api/Features/Submissions.cs', 'src/Hub.Api/Features/DesignBasis.cs',
    'src/Hub.Api/Features/Readiness.cs', 'src/Hub.Api/Features/WeeklyCommitments.cs', 'src/Hub.Api/Features/Registers.cs',
    'src/Hub.Api/Features/Calendar.cs', 'src/Hub.Api/Features/Time.cs', 'src/Hub.Api/Features/Reports.cs',
    'src/Hub.Api/Features/Views.cs', 'src/Hub.Api/Features/Me.cs',
)

def normalize(text: str) -> str:
    return ' '.join(text.split())

def digest(text: str) -> str:
    return hashlib.sha256(normalize(text).encode()).hexdigest()

def witness_fingerprint(record: dict) -> str:
    """Bind the entire reviewed record, preserving string values and list order."""
    if not isinstance(record, dict): raise ValueError('Expected witness record object')
    canonical = json.dumps(record, sort_keys=True, separators=(',', ':'), ensure_ascii=False, allow_nan=False)
    return hashlib.sha256(canonical.encode('utf-8')).hexdigest()

def spec_sections(text: str) -> dict:
    result, current, depth = {}, None, 0
    for line in text.splitlines():
        heading = re.match(r'^(#{2,4})\s+(\d+(?:\.\d+)*)\b', line)
        if heading:
            level, number = len(heading[1]), heading[2]
            if current and level <= depth:
                current = None
            if number in SECTIONS:
                current, depth = number, level
                result[current] = []
        if current:
            result[current].append(line)
    sections = {}
    for number, lines in result.items():
        rows = []
        for line in lines:
            if not line.startswith('|'):
                continue
            action = line.split('|')[1].strip()
            if action != 'Action' and set(action) - set('-: '):
                rows.append(number + ':' + normalize(action))
        sections[number] = {'fingerprint': digest('\n'.join(lines)), 'rows': rows}
    return sections

def method_signatures(text: str) -> dict[str, str]:
    # This maintained source has ordinary public method declarations. Unknown syntax is a coverage failure, never a grant.
    text = text.split('public static class Permissions', 1)[1]
    result = {}
    for match in re.finditer(r'^\s*public static\s+(.+?)\s+(\w+)\s*\(', text, re.M):
        returned, name = normalize(match[1]), match[2]
        if returned not in ('Allow', 'bool') and 'Allow' not in returned:
            continue
        start, end, depth = match.end(), match.end(), 1
        while end < len(text) and depth:
            depth += (text[end] == '(') - (text[end] == ')')
            end += 1
        if depth:
            raise ValueError('Unclosed method signature: ' + name)
        signature = normalize(returned + ' ' + name + '(' + text[start:end - 1] + ')')
        if name in result:
            raise ValueError('Overload requires an explicit catalog binding: ' + name)
        result[name] = signature
    if not result:
        raise ValueError('No canonical methods found')
    return result

def source_tokens(text: str) -> list[str]:
    """Small lexical reader for the maintained literal Rows initializer; unsupported syntax refuses coverage."""
    tokens, i = [], 0
    while i < len(text):
        if text[i].isspace(): i += 1; continue
        if text.startswith('//', i):
            end = text.find('\n', i); i = len(text) if end < 0 else end; continue
        if text.startswith('/*', i):
            end = text.find('*/', i + 2)
            if end < 0: raise ValueError('Unclosed source comment')
            i = end + 2; continue
        start, char = i, text[i]
        if char in ('"', "'"):
            i += 1
            while i < len(text) and text[i] != char:
                i += 2 if text[i] == '\\' else 1
            if i >= len(text): raise ValueError('Unclosed source literal')
            i += 1; tokens.append(text[start:i]); continue
        match = re.match(r'[A-Za-z_][A-Za-z_0-9]*', text[i:])
        if match: i += len(match[0]); tokens.append(match[0]); continue
        tokens.append(char); i += 1
    return tokens

def group(tokens: list[str], start: int) -> tuple[list[str], int]:
    pairs = {'(': ')', '[': ']', '{': '}'}
    if start >= len(tokens) or tokens[start] not in pairs: raise ValueError('Expected source group')
    stack, i = [pairs[tokens[start]]], start + 1
    while i < len(tokens) and stack:
        token = tokens[i]
        if token in pairs: stack.append(pairs[token])
        elif token in pairs.values():
            if token != stack.pop(): raise ValueError('Mismatched source delimiter')
        i += 1
    if stack: raise ValueError('Unclosed source group')
    return tokens[start + 1:i - 1], i

def arguments(tokens: list[str]) -> list[list[str]]:
    result, start, i = [], 0, 0
    while i < len(tokens):
        if tokens[i] in ('(', '[', '{'): _, i = group(tokens, i); continue
        if tokens[i] == ',': result.append(tokens[start:i]); start = i + 1
        i += 1
    if start < len(tokens): result.append(tokens[start:])
    if any(not item for item in result): raise ValueError('Empty source argument')
    return result

def literal(tokens: list[str]) -> str:
    if len(tokens) != 1 or not tokens[0].startswith('"'): raise ValueError('Expected closed literal metadata')
    value = json.loads(tokens[0])
    if not isinstance(value, str): raise ValueError('Expected literal string')
    return value

def constructor(tokens: list[str]) -> list[list[str]]:
    if tokens[:2] != ['new', '(']: raise ValueError('Unknown Rows constructor syntax')
    body, end = group(tokens, 1)
    if end != len(tokens): raise ValueError('Unexpected Rows constructor suffix')
    return arguments(body)

def array(tokens: list[str]) -> list[list[str]]:
    if not tokens or tokens[0] != '[': raise ValueError('Expected literal source array')
    body, end = group(tokens, 0)
    if end != len(tokens): raise ValueError('Unexpected source array suffix')
    return arguments(body)

def catalog_rows(text: str) -> dict:
    tokens = source_tokens(text)
    marker = ['static', 'readonly', 'RowDefinition', '[', ']', 'Rows', '=']
    starts = [i + len(marker) for i in range(len(tokens)) if tokens[i:i + len(marker)] == marker]
    if len(starts) != 1: raise ValueError('Expected one closed Rows initializer')
    body, end = group(tokens, starts[0])
    if tokens[starts[0]] != '[' or tokens[end:end + 1] != [';']: raise ValueError('Rows must be a closed literal array')
    rows, operation_keys = {}, set()
    for item in arguments(body):
        args = constructor(item)
        if len(args) < 4: raise ValueError('Incomplete row metadata')
        row_id, key = literal(args[0]), literal(args[1])
        if row_id in rows: raise ValueError('Duplicate catalog row: ' + row_id)
        policy = False
        for arg in args[4:]:
            if arg[:2] == ['PolicyPending', ':']:
                if arg[2:] not in (['true'], ['false']): raise ValueError('Unknown policy expression')
                policy = arg[2:] == ['true']
        operations = {}
        for operation in array(args[3]):
            op = constructor(operation)
            if len(op) < 3: raise ValueError('Incomplete operation metadata')
            op_key = literal(op[0])
            if not op_key.startswith(row_id + '.') or op_key in operation_keys: raise ValueError('Duplicate/orphan operation: ' + op_key)
            if literal(op[1]) != 'adminRole.operation.' + op_key: raise ValueError('Operation label mismatch: ' + op_key)
            factors = []
            for factor in array(op[2]):
                if len(factor) != 6 or factor[:4] != ['nameof', '(', 'Permissions', '.'] or factor[-1] != ')':
                    raise ValueError('Unknown operation factor syntax: ' + op_key)
                factors.append(factor[4])
            operations[op_key] = {'methods': factors, 'sourceFingerprint': digest(' '.join(operation))}
            operation_keys.add(op_key)
        if not operations: raise ValueError('Row has no operations: ' + row_id)
        rows[row_id] = {'matrixRowKey': key, 'policyPending': policy, 'operations': operations}
    return rows

def local_file(root: Path, name: str) -> Path:
    if not isinstance(name, str) or not name or Path(name).is_absolute() or '..' in Path(name).parts:
        raise ValueError('Evidence/source path must be repository relative')
    path = (root / name).resolve()
    if not path.is_relative_to(root.resolve()) or not path.is_file(): raise ValueError('Missing/escaping file: ' + name)
    return path

def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()

def pinned(root: Path, reference: dict) -> Path:
    path = local_file(root, reference['path'])
    if sha256(path) != reference['sha256']: raise ValueError('Evidence hash mismatch: ' + reference['path'])
    return path

def string_list(value, label: str, nonempty: bool = False) -> list[str]:
    if not isinstance(value, list) or any(not isinstance(x, str) or not x for x in value) or len(value) != len(set(value)) or (nonempty and not value):
        raise ValueError('Expected unique string list: ' + label)
    return value

def witness(root: Path, record: dict, kind: str, operation: str, required_sources: set[str]) -> set[str]:
    if record.get('kind') != kind or operation not in string_list(record.get('operations'), 'witness operations', True):
        raise ValueError('Unrelated/wrong-kind witness for ' + operation)
    guards = set(string_list(record.get('guards'), 'witness guards'))
    if any(not path.startswith('src/') or not path.endswith('.cs') for path in guards): raise ValueError('Unknown witness guard source')
    source = record['testSource']
    if not isinstance(source, str) or not source.startswith('tests/Hub.Tests/') or not source.endswith('.cs'):
        raise ValueError('Expected maintained C# test source')
    if kind == 'enforcement' and (not source.startswith('tests/Hub.Tests/Api/') or 'Explanation' in Path(source).stem):
        raise ValueError('Explanation/domain evidence cannot replace actual command enforcement evidence')
    snapshot = json.loads(pinned(root, record['snapshot']).read_text())
    files = snapshot['files']
    if not isinstance(files, dict): raise ValueError('Invalid source snapshot files')
    for name in required_sources | guards | {source, SPEC, PERMISSIONS, CATALOG}:
        if name not in files or sha256(local_file(root, name)) != files[name]: raise ValueError('Missing/stale run source: ' + name)
    run = ET.parse(pinned(root, record['trx'])).getroot()
    if run.tag != '{' + TRX_NS['t'] + '}TestRun' or not run.get('id') or snapshot.get('testRunId') != run.get('id'):
        raise ValueError('Source snapshot is not tied to this TRX run')
    test_id, execution = record['testId'], record['executionId']
    if not test_id or not execution: raise ValueError('Missing exact TRX case identity')
    results = [result for result in run.findall('t:Results/t:UnitTestResult', TRX_NS)
               if result.get('testId') == test_id and result.get('executionId') == execution]
    definitions = [test for test in run.findall('t:TestDefinitions/t:UnitTest', TRX_NS) if test.get('id') == test_id]
    if len(results) != 1 or len(definitions) != 1 or results[0].get('outcome') != 'Passed':
        raise ValueError('Missing/failed/ambiguous Passed TRX case')
    method, identity = definitions[0].find('t:TestMethod', TRX_NS), definitions[0].find('t:Execution', TRX_NS)
    if method is None or identity is None or identity.get('id') != execution: raise ValueError('TRX definition/execution mismatch')
    class_name, method_name = method.get('className', '').split(',')[0].strip(), method.get('name', '')
    if not class_name or not method_name or record.get('test') != class_name + '.' + method_name:
        raise ValueError('TRX method does not match witness FQN')
    namespace, _, class_short = class_name.rpartition('.')
    # Use declarations, not filenames: partial test classes intentionally span differently named files.
    tokens = source_tokens(local_file(root, source).read_text())
    namespace_tokens = ['namespace'] + [part for component in namespace.split('.') for part in (component, '.')][:-1]
    if not any(tokens[i:i + len(namespace_tokens)] == namespace_tokens and tokens[i + len(namespace_tokens):i + len(namespace_tokens) + 1] in ([';'], ['{']) for i in range(len(tokens))):
        raise ValueError('Wrong test source namespace/class')
    declared = False
    for i in range(len(tokens)):
        if tokens[i:i + 2] != ['class', class_short]: continue
        start = i + 2
        while start < len(tokens) and tokens[start] != '{': start += 1
        body, _ = group(tokens, start)
        j = 0
        while j < len(body):
            if body[j] == 'public':
                end = j + 1
                while end < len(body) and body[end] not in ('(', '{', ';', '='): end += 1
                if end < len(body) and body[end] == '(' and body[end - 1] == method_name: declared = True
            if body[j] in ('(', '[', '{'): _, j = group(body, j)
            else: j += 1
    if not declared: raise ValueError('Witness method declaration is absent from test source')
    return guards

def _check(root: Path, manifest: dict, complete: bool) -> dict:
    errors, pending = [], list(manifest.get('completionGaps', []))
    version = manifest.get('format', 1)
    if version not in (1, 2): errors.append('Unknown evidence manifest format')
    current = spec_sections((root / SPEC).read_text())
    if set(current) != set(SECTIONS): errors.append('Missing required specification section')
    if current != manifest.get('sections'): errors.append('Changed section row/header/cell/condition/notes fingerprint')
    methods = method_signatures((root / PERMISSIONS).read_text())
    if methods != manifest.get('signatures'): errors.append('Changed/new/unmapped canonical Allow/bool/tuple signature')
    catalog = (root / CATALOG).read_text()
    tokens = source_tokens(catalog)
    names = [tokens[i + 6] for i in range(len(tokens) - 8) if tokens[i:i + 6] == ['new', '(', 'nameof', '(', 'Permissions', '.'] and tokens[i + 7:i + 9] == [')', ',']]
    if len(names) != len(set(names)) or set(names) != set(methods): errors.append('Catalog does not bind every canonical method exactly once')
    expected_rows = {row for section in current.values() for row in section['rows']}
    rows = catalog_rows(catalog)
    operations = {key: (row_id, operation) for row_id, row in rows.items() for key, operation in row['operations'].items()}
    if set(rows) != ROW_IDS or len(expected_rows) != len(ROW_IDS): errors.append('Expected all 83 closed matrix rows')
    if {row['matrixRowKey'] for row in rows.values()} != expected_rows: errors.append('Catalog rows do not match specification action rows')
    bindings = manifest.get('rowBindings', {})
    if set(bindings) != expected_rows: errors.append('Missing/new/unmapped matrix action row')
    guards = manifest.get('guards', {})
    if set(guards) != set(REQUIRED_GUARDS): errors.append('Missing/unmapped compound/transport/context/feature guard source')
    for path, guard in guards.items():
        source = root / path
        if not source.exists() or hashlib.sha256(source.read_bytes()).hexdigest() != guard.get('sha256'):
            errors.append('Guard source changed; binding review required: ' + path)
        if guard.get('coverage') not in ('Pending', 'Evaluated'): errors.append('Unknown guard coverage: ' + path)
    operation_bindings = manifest.get('operationBindings', {})
    if version == 2 and set(operation_bindings) != set(operations): errors.append('Missing/new/unmapped child operation')
    if version == 1 and operation_bindings: errors.append('Operation evidence requires format 2')
    evaluated, verified_guards = set(), set()
    witnesses = manifest.get('witnesses', {})
    for key, (row_id, operation) in operations.items():
        binding = operation_bindings.get(key, {}) if version == 2 else {}
        state = binding.get('coverage', 'Pending')
        if state not in ('Pending', 'PolicyPending', 'Evaluated'): errors.append('Unknown operation coverage: ' + key)
        binding_valid = True
        if any(name not in methods for name in operation['methods']):
            errors.append('Unknown canonical operation factor: ' + key)
            binding_valid = False
        if version == 2 and key in operation_bindings:
            if binding.get('rowId') != row_id or binding.get('methods') != operation['methods'] or binding.get('sourceFingerprint') != operation['sourceFingerprint']:
                errors.append('Changed operation/parent/factor binding: ' + key)
                binding_valid = False
            string_list(binding.get('guards'), 'operation guards', True)
            for path in binding['guards']:
                if not path.startswith('src/') or not path.endswith('.cs'): raise ValueError('Unknown operation guard: ' + path)
                local_file(root, path)
        if rows[row_id]['policyPending']:
            pending.append('policy:' + key)
            if state == 'Evaluated': errors.append('PolicyPending operation cannot be Evaluated: ' + key)
        if state != 'Evaluated' or rows[row_id]['policyPending'] or not binding_valid:
            pending.append('operation:' + key); continue
        try:
            review = binding.get('review', {})
            evidence = binding.get('evidence', {})
            if review.get('status') != 'Reviewed' or not review.get('by', '').strip() or not review.get('note', '').strip() or review.get('evidence') != evidence or review.get('guards') != binding['guards'] or review.get('operationFingerprint') != operation['sourceFingerprint']:
                raise ValueError('Missing explicit reviewed operation-to-witness mapping')
            if set(evidence) != {'explanation', 'enforcement'}:
                raise ValueError('Expected separate explanation/enforcement witness mappings')
            references = {kind: string_list(evidence[kind], kind + ' witness IDs', True) for kind in ('explanation', 'enforcement')}
            fingerprints = {reference: witness_fingerprint(witnesses[reference]) for values in references.values() for reference in values}
            if review.get('witnessFingerprints') != fingerprints:
                raise ValueError('Reviewed witness content fingerprint mismatch')
            covered = set()
            for kind, values in references.items():
                for reference in values:
                    covered_now = witness(root, witnesses[reference], kind, key, set(binding['guards']))
                    if kind == 'enforcement': covered.update(covered_now)
            if not set(binding['guards']) <= covered: raise ValueError('Underlying enforcement guard evidence is incomplete')
            evaluated.add(key); verified_guards.update(covered & set(binding['guards']))
        except (OSError, ValueError, KeyError, TypeError, AttributeError, ET.ParseError) as error:
            errors.append('Unproven Evaluated operation ' + key + ': ' + str(error)); pending.append('operation:' + key)
    for row, binding in bindings.items():
        if not binding.get('methods') and not binding.get('guards'): errors.append('Unbound action: ' + row)
        if any(name not in methods for name in binding.get('methods', [])): errors.append('Unknown method binding: ' + row)
        if any(path not in guards for path in binding.get('guards', [])): errors.append('Unknown guard binding: ' + row)
        if binding.get('coverage') not in ('Pending', 'Evaluated', 'PolicyPending'): errors.append('Unknown row coverage: ' + row)
        children = {key for key, (row_id, _) in operations.items() if rows[row_id]['matrixRowKey'] == row}
        if binding.get('coverage') == 'Evaluated' and (version != 2 or not children or not children <= evaluated):
            errors.append('Row Evaluated without all child-operation evidence: ' + row)
        if binding.get('coverage') != 'Evaluated' or not children <= evaluated: pending.append('row:' + row)
    for path, guard in guards.items():
        children = {key for key, binding in operation_bindings.items() if path in binding.get('guards', [])}
        proven = version == 2 and bool(children) and children <= evaluated and path in verified_guards
        if guard.get('coverage') == 'Evaluated' and not proven: errors.append('Guard Evaluated without child-operation enforcement evidence: ' + path)
        if guard.get('coverage') != 'Evaluated' or not proven: pending.append('guard:' + path)
    if complete and pending: errors.append('Complete FR-SCR-83 gate refused: pending policy/compound coverage')
    return {'structuralPass': not errors, 'coverageComplete': not errors and not pending, 'errors': errors, 'pending': sorted(set(pending)),
            'sections': len(current), 'rows': len(expected_rows), 'canonicalMethods': len(methods),
            'operations': len(operations), 'evaluatedOperations': len(evaluated), 'manifestFormat': version}

def check(root: Path, manifest: dict, complete: bool = False) -> dict:
    try:
        return _check(root, manifest, complete)
    except (OSError, ValueError, KeyError, IndexError, TypeError, AttributeError, ET.ParseError) as error:
        return {'structuralPass': False, 'coverageComplete': False, 'errors': [str(error)], 'pending': []}

def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root', type=Path, default=Path(__file__).resolve().parents[1])
    parser.add_argument('--require-complete', action='store_true')
    args = parser.parse_args()
    try: result = check(args.root, json.loads((args.root / MANIFEST).read_text()), args.require_complete)
    except (OSError, ValueError, KeyError, IndexError) as error: result = {'structuralPass': False, 'coverageComplete': False, 'errors': [str(error)]}
    print(json.dumps(result, indent=2))
    return 0 if result['structuralPass'] else 1

if __name__ == '__main__':
    raise SystemExit(main())
