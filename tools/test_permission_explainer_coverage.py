"""Defect-seeded evidence fixtures, not executable C# authorization or production coverage proof."""
import copy
import hashlib
import importlib.util
import json
import tempfile
import unittest
from pathlib import Path

spec = importlib.util.spec_from_file_location('permission_coverage', Path(__file__).with_name('check_permission_explainer_coverage.py'))
coverage = importlib.util.module_from_spec(spec)
spec.loader.exec_module(coverage)


class CoverageTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.row_ids = [f'{prefix}{i:02}' for prefix, count in (('S', 22), ('W', 33), ('C', 18), ('P', 10)) for i in range(1, count + 1)]
        self.op_keys = {row: [row + f'.operation-{i}' for i in range(1, 5 if row == 'S01' else 4)] for row in self.row_ids}
        sections = dict(zip(('S', 'W', 'C', 'P'), coverage.SECTIONS))
        self.spec_text = '\n'.join(f'### {section} Rules\n\n| Action | Role |\n|---|---|\n' +
            '\n'.join(f'| Action {row} | Y |' for row in self.row_ids if sections[row[0]] == section) +
            '\n\nCondition retained.' for section in coverage.SECTIONS)
        self.permissions = 'public static class Permissions\n{\n public static bool CanView(Actor a) => true;\n public static Allow Edit(Actor a, bool own = false) => Allow.Yes;\n}'
        self.catalog = 'new(nameof(Permissions.CanView), f); new(nameof(Permissions.Edit), f);\nstatic readonly RowDefinition[] Rows =\n[\n' + '\n'.join(
            f'new("{row}", "{sections[row[0]]}:Action {row}", ["identity"],\n[' +
            ',\n'.join(f'new("{key}", "adminRole.operation.{key}", [nameof(Permissions.CanView)])' for key in self.op_keys[row]) +
            '], PolicyPending: ' + ('true' if row == 'S21' else 'false') + '),' for row in self.row_ids) + '\n];'
        self.write(coverage.SPEC, self.spec_text)
        self.write(coverage.PERMISSIONS, self.permissions)
        self.write(coverage.CATALOG, self.catalog)
        guards = {}
        for name in coverage.REQUIRED_GUARDS:
            self.write(name, 'guard source ' + name)
            guards[name] = {'sha256': self.hash(name), 'coverage': 'Pending'}
        sections = coverage.spec_sections(self.spec_text)
        self.manifest = {'format': 1, 'sections': sections, 'signatures': coverage.method_signatures(self.permissions),
            'guards': guards, 'completionGaps': ['policy-unresolved'], 'rowBindings': {
                row: {'methods': ['CanView'], 'guards': [coverage.REQUIRED_GUARDS[0]], 'coverage': 'Pending'}
                for section in sections.values() for row in section['rows']}}
        self.example_source = 'tests/Hub.Tests/Domain/PermissionOperationExplanationTests.cs'
        # Filename differs from the declared partial class, as in real ExternalReview test files.
        self.command_source = 'tests/Hub.Tests/Api/OtherPartialFile.cs'
        self.write(self.example_source, 'namespace Hub.Tests.Domain; public sealed class PermissionOperationExplanationTests { [Fact] public void Example() {} }')
        self.write(self.command_source, 'namespace Hub.Tests.Api; public sealed partial class CommandTests { [Fact] public async Task Command() {} }')

    def write(self, name, text):
        path = self.root / name
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(text)

    def hash(self, name):
        return hashlib.sha256((self.root / name).read_bytes()).hexdigest()

    def pin(self, name):
        return {'path': name, 'sha256': self.hash(name)}

    def version_two(self):
        self.manifest['format'] = 2
        self.manifest['operationBindings'] = {}
        for row_id, row in coverage.catalog_rows((self.root / coverage.CATALOG).read_text()).items():
            for key, operation in row['operations'].items():
                self.manifest['operationBindings'][key] = {'rowId': row_id, **operation,
                    'guards': list(coverage.REQUIRED_GUARDS), 'coverage': 'Pending'}
        self.manifest['witnesses'] = {}

    def proofs(self, keys=None):
        keys = keys or ['C06.operation-1']
        trx = 'evidence/run.trx'
        self.trx_text = '''<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010" id="seed-run">
<Results><UnitTestResult testId="example" executionId="example-execution" outcome="Passed" />
<UnitTestResult testId="command" executionId="command-execution" outcome="Passed" /></Results>
<TestDefinitions><UnitTest id="example"><Execution id="example-execution" />
<TestMethod className="Hub.Tests.Domain.PermissionOperationExplanationTests" name="Example" /></UnitTest>
<UnitTest id="command"><Execution id="command-execution" />
<TestMethod className="Hub.Tests.Api.CommandTests" name="Command" /></UnitTest></TestDefinitions></TestRun>'''
        self.write(trx, self.trx_text)
        snapshot = 'evidence/sources.json'
        sources = {coverage.SPEC, coverage.PERMISSIONS, coverage.CATALOG, self.example_source, self.command_source, *coverage.REQUIRED_GUARDS}
        self.write(snapshot, json.dumps({'format': 1, 'testRunId': 'seed-run', 'files': {name: self.hash(name) for name in sources}}))
        for kind, test_id, source, test in (
            ('explanation', 'example', self.example_source, 'Hub.Tests.Domain.PermissionOperationExplanationTests.Example'),
            ('enforcement', 'command', self.command_source, 'Hub.Tests.Api.CommandTests.Command')):
            self.manifest['witnesses'][kind] = {'kind': kind, 'operations': keys, 'guards': list(coverage.REQUIRED_GUARDS),
                'testSource': source, 'test': test, 'testId': test_id, 'executionId': test_id + '-execution',
                'trx': self.pin(trx), 'snapshot': self.pin(snapshot)}
        for key in keys:
            binding = self.manifest['operationBindings'][key]
            binding['coverage'] = 'Evaluated'
            binding['evidence'] = {'explanation': ['explanation'], 'enforcement': ['enforcement']}
            binding['review'] = {'status': 'Reviewed', 'by': 'synthetic fixture reviewer', 'note': 'Synthetic parser case, not product evidence',
                'evidence': copy.deepcopy(binding['evidence']), 'guards': list(binding['guards']), 'operationFingerprint': binding['sourceFingerprint']}
        self.refresh_review_fingerprints()

    def refresh_review_fingerprints(self):
        # These defect fixtures deliberately renew record review only when testing a deeper validation gate.
        for binding in self.manifest['operationBindings'].values():
            if 'review' not in binding: continue
            references = {reference for values in binding['evidence'].values() for reference in values}
            binding['review']['witnessFingerprints'] = {
                reference: coverage.witness_fingerprint(self.manifest['witnesses'][reference]) for reference in references}

    def edit_trx(self, text):
        self.write('evidence/run.trx', text)
        for record in self.manifest['witnesses'].values(): record['trx'] = self.pin('evidence/run.trx')
        self.refresh_review_fingerprints()

    def assert_refused(self, message=None):
        result = coverage.check(self.root, self.manifest)
        self.assertFalse(result['structuralPass'], result)
        self.assertFalse(result['coverageComplete'], result)
        if message: self.assertTrue(any(message in error for error in result['errors']), result)

    def test_legacy_partial_is_honest_and_closed_catalog_is_83_rows_250_operations(self):
        result = coverage.check(self.root, self.manifest)
        self.assertTrue(result['structuralPass'], result)
        self.assertEqual((83, 250, 0), (result['rows'], result['operations'], result['evaluatedOperations']))
        self.assertFalse(result['coverageComplete'])
        self.assertFalse(coverage.check(self.root, self.manifest, True)['structuralPass'])

    def test_literal_evaluated_cannot_complete_legacy_manifest_even_with_current_hashes(self):
        self.manifest['completionGaps'] = []
        for binding in list(self.manifest['guards'].values()) + list(self.manifest['rowBindings'].values()): binding['coverage'] = 'Evaluated'
        self.assert_refused('without')

    def test_version_two_pending_is_structural_only_and_missing_child_fails(self):
        self.version_two()
        result = coverage.check(self.root, self.manifest)
        self.assertTrue(result['structuralPass'], result)
        self.assertFalse(result['coverageComplete'])
        self.manifest['operationBindings'].pop('C06.operation-2')
        self.assert_refused('child operation')

    def test_operation_enumeration_tracks_source_children_without_a_fixed_count(self):
        self.version_two()
        added = ',\n'.join(f'new("S01.added-{i}", "adminRole.operation.S01.added-{i}", [nameof(Permissions.CanView)])' for i in range(7))
        changed = self.catalog.replace('new("S01.operation-1",', added + ',\nnew("S01.operation-1",', 1)
        self.write(coverage.CATALOG, changed)
        self.assert_refused('child operation')
        self.version_two()
        result = coverage.check(self.root, self.manifest)
        self.assertTrue(result['structuralPass'], result)
        self.assertEqual(257, result['operations'])
        self.assertFalse(result['coverageComplete'])

    def test_fabricated_evaluated_and_unknown_coverage_fail(self):
        self.version_two()
        binding = self.manifest['operationBindings']['C06.operation-1']
        binding['coverage'] = 'Evaluated'
        self.assert_refused('reviewed operation-to-witness')
        binding['coverage'] = 'LooksGood'
        self.assert_refused('Unknown operation coverage')

    def test_real_passed_case_references_can_prove_one_operation_without_promoting_others(self):
        self.version_two(); self.proofs()
        result = coverage.check(self.root, self.manifest)
        self.assertTrue(result['structuralPass'], result)
        self.assertEqual(1, result['evaluatedOperations'])
        self.assertFalse(result['coverageComplete'])

    def test_same_witness_id_cannot_substitute_another_valid_passed_case_without_new_review(self):
        self.version_two()
        self.write(self.command_source, 'namespace Hub.Tests.Api; public sealed partial class CommandTests { '
            '[Fact] public async Task Command() {} [Fact] public async Task AnotherCommand() {} }')
        self.proofs()
        second_case = self.trx_text.replace('</Results>',
            '<UnitTestResult testId="another-command" executionId="another-execution" outcome="Passed" /></Results>')
        second_case = second_case.replace('</TestDefinitions>', '<UnitTest id="another-command">'
            '<Execution id="another-execution" /><TestMethod className="Hub.Tests.Api.CommandTests" '
            'name="AnotherCommand" /></UnitTest></TestDefinitions>')
        self.edit_trx(second_case)
        baseline = coverage.check(self.root, self.manifest)
        self.assertTrue(baseline['structuralPass'], baseline)
        review = self.manifest['operationBindings']['C06.operation-1']['review']
        reviewed_fingerprints = copy.deepcopy(review['witnessFingerprints'])
        for invalid in ({}, {**reviewed_fingerprints, 'unreferenced': '0' * 64}):
            with self.subTest(fingerprints=invalid):
                review['witnessFingerprints'] = invalid
                self.assert_refused('Reviewed witness content fingerprint mismatch')
        review['witnessFingerprints'] = copy.deepcopy(reviewed_fingerprints)
        record = self.manifest['witnesses']['enforcement']
        record.update(test='Hub.Tests.Api.CommandTests.AnotherCommand', testId='another-command', executionId='another-execution')
        # Both cases are genuinely Passed in the pinned synthetic TRX, declared in the same pinned source.
        # Keep the old review: the ID is unchanged, while the reviewed semantic witness has changed.
        self.assert_refused('Reviewed witness content fingerprint mismatch')
        self.assertEqual(reviewed_fingerprints, review['witnessFingerprints'])
        self.refresh_review_fingerprints()
        renewed = coverage.check(self.root, self.manifest)
        self.assertTrue(renewed['structuralPass'], renewed)
        self.assertEqual(1, renewed['evaluatedOperations'])

    def test_failed_missing_wrong_and_tampered_trx_cases_are_refused(self):
        self.version_two(); self.proofs()
        for changed in (self.trx_text.replace('outcome="Passed"', 'outcome="Failed"'),
                        self.trx_text.replace('testId="command"', 'testId="different"'),
                        self.trx_text.replace('name="Command"', 'name="DifferentCommand"'),
                        self.trx_text.replace('executionId="command-execution"', 'executionId="other"')):
            with self.subTest(changed=changed):
                self.edit_trx(changed); self.assert_refused('Unproven Evaluated')
        self.edit_trx(self.trx_text)
        self.write('evidence/run.trx', self.trx_text + '\n')
        self.assert_refused('Evidence hash mismatch')

    def test_source_snapshot_must_belong_to_the_selected_run(self):
        self.version_two(); self.proofs()
        self.edit_trx(self.trx_text.replace('id="seed-run"', 'id="other-run"'))
        self.assert_refused('tied to this TRX run')

    def test_same_signature_permission_body_guard_catalog_and_test_changes_make_proof_stale(self):
        self.version_two(); self.proofs()
        for name in (coverage.PERMISSIONS, coverage.CATALOG, self.command_source, coverage.REQUIRED_GUARDS[0]):
            with self.subTest(source=name):
                original = (self.root / name).read_text()
                self.write(name, original + '\n// source body changed after the run\n')
                self.assert_refused('source' if name in coverage.REQUIRED_GUARDS else 'Missing/stale run source')
                self.write(name, original)

    def test_unrelated_case_and_missing_enforcement_cannot_be_relabelled_as_proof(self):
        self.version_two(); self.proofs()
        self.manifest['witnesses']['enforcement']['operations'] = ['C06.operation-2']
        self.refresh_review_fingerprints()
        self.assert_refused('Unrelated/wrong-kind')
        self.manifest['witnesses']['enforcement']['operations'] = ['C06.operation-1']
        self.manifest['witnesses']['enforcement']['kind'] = 'explanation'
        self.refresh_review_fingerprints()
        self.assert_refused('Unrelated/wrong-kind')
        self.manifest['witnesses'].pop('enforcement')
        self.assert_refused('Unproven Evaluated')

    def test_known_explanation_api_test_is_not_command_enforcement_evidence(self):
        self.version_two(); self.proofs()
        self.manifest['witnesses']['enforcement']['testSource'] = 'tests/Hub.Tests/Api/AdminRoleOperationExplanationTests.cs'
        self.refresh_review_fingerprints()
        self.assert_refused('cannot replace actual command enforcement')

    def test_missing_guard_witness_and_changed_review_mapping_fail_closed(self):
        self.version_two(); self.proofs()
        self.manifest['witnesses']['enforcement']['guards'].pop()
        self.refresh_review_fingerprints()
        self.assert_refused('guard evidence is incomplete')
        self.manifest['witnesses']['enforcement']['guards'] = list(coverage.REQUIRED_GUARDS)
        self.refresh_review_fingerprints()
        self.manifest['operationBindings']['C06.operation-1']['review']['evidence']['enforcement'] = ['other']
        self.assert_refused('reviewed operation-to-witness')

    def test_row_and_guard_cannot_outrun_pending_child_evidence(self):
        self.version_two(); self.proofs()
        row = next(key for key in self.manifest['rowBindings'] if key.endswith('Action C06'))
        self.manifest['rowBindings'][row]['coverage'] = 'Evaluated'
        self.manifest['guards'][coverage.REQUIRED_GUARDS[0]]['coverage'] = 'Evaluated'
        self.assert_refused('without')

    def test_source_policy_pending_survives_removed_prose_gap_and_fake_evaluated(self):
        self.version_two(); self.proofs(['S21.operation-1'])
        self.manifest['completionGaps'] = []
        self.assert_refused('PolicyPending operation')

    def test_strict_can_pass_only_a_fully_reviewed_synthetic_nonpolicy_catalog(self):
        self.write(coverage.CATALOG, self.catalog.replace('PolicyPending: true', 'PolicyPending: false'))
        self.version_two(); self.proofs([key for keys in self.op_keys.values() for key in keys])
        self.manifest['completionGaps'] = []
        for binding in list(self.manifest['guards'].values()) + list(self.manifest['rowBindings'].values()): binding['coverage'] = 'Evaluated'
        result = coverage.check(self.root, self.manifest, True)
        self.assertTrue(result['structuralPass'], result)
        self.assertTrue(result['coverageComplete'], result)
        self.assertEqual(250, result['evaluatedOperations'])

    def test_cell_condition_signature_new_method_missing_row_and_guard_still_fail(self):
        for name, changed in (
            (coverage.SPEC, self.spec_text.replace('| Y |', '| — |', 1)),
            (coverage.SPEC, self.spec_text.replace('Condition retained.', 'Different condition.', 1)),
            (coverage.PERMISSIONS, self.permissions.replace('bool own = false', 'bool own = false, bool correction = false')),
            (coverage.PERMISSIONS, self.permissions.replace('}', ' public static bool NewRead(Actor a) => true;\n}'))):
            with self.subTest(source=name):
                original = (self.root / name).read_text()
                self.write(name, changed); self.assert_refused(); self.write(name, original)
        for key in ('rowBindings', 'guards'):
            changed = copy.deepcopy(self.manifest)
            changed[key].pop(next(iter(changed[key])))
            self.assertFalse(coverage.check(self.root, changed)['structuralPass'])

    def test_catalog_ignores_comment_string_decoys_but_refuses_duplicate_or_renamed_child(self):
        self.version_two()
        self.write(coverage.CATALOG, self.catalog + '\n// new("C99.fake", "ignored", []),\n' +
            'static string decoy = "new(\\"C99.fake\\", \\"ignored\\", [])";')
        self.assertTrue(coverage.check(self.root, self.manifest)['structuralPass'])
        self.write(coverage.CATALOG, self.catalog.replace('C06.operation-2', 'C06.changed'))
        self.assert_refused('child operation')
        self.write(coverage.CATALOG, self.catalog.replace('C06.operation-2', 'C06.operation-1'))
        self.assert_refused('Duplicate/orphan')

    def test_paths_snapshot_sources_wrong_partial_class_and_method_call_decoy_fail(self):
        self.version_two(); self.proofs()
        record = self.manifest['witnesses']['enforcement']
        original = copy.deepcopy(record)
        record['trx']['path'] = '../outside.trx'
        self.refresh_review_fingerprints()
        self.assert_refused('repository relative')
        self.manifest['witnesses']['enforcement'] = copy.deepcopy(original)
        self.manifest['witnesses']['enforcement']['test'] = 'Hub.Tests.Api.OtherPartialFile.Command'
        self.refresh_review_fingerprints()
        self.assert_refused('FQN')
        self.manifest['witnesses']['enforcement'] = copy.deepcopy(original)
        snapshot = json.loads((self.root / 'evidence/sources.json').read_text())
        snapshot['files'].pop(coverage.PERMISSIONS)
        self.write('evidence/sources.json', json.dumps(snapshot))
        for item in self.manifest['witnesses'].values(): item['snapshot'] = self.pin('evidence/sources.json')
        self.refresh_review_fingerprints()
        self.assert_refused('Missing/stale run source')
        self.write(self.command_source, 'namespace Hub.Tests.Api; public sealed partial class CommandTests { public void Other() { Command(); } }')
        self.proofs()
        self.assert_refused('method declaration')


if __name__ == '__main__':
    unittest.main()
