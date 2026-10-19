import unittest
from shapes import differences, merge, operations, recorded_files
import tempfile, json
from pathlib import Path


class ShapeTests(unittest.TestCase):
    def test_removed_and_added_paths_and_changed_kinds_fail(self):
        old = {'GET /x 200': {'$': ['object'], '$.name': ['string']}}
        self.assertTrue(differences(old, {'GET /x 200': {'$': ['object']}}))
        self.assertTrue(differences(old, {'GET /x 200': {'$': ['object'], '$.name': ['number']}}))
        self.assertTrue(differences(old, {'GET /x 200': {'$': ['object'], '$.name': ['string'], '$.new': ['boolean']}}))
        self.assertEqual([], differences(old, old))

    def test_empty_arrays_and_null_are_unknown_not_removed(self):
        old = {'GET /x 200': {'$': ['array'], '$[]': ['object'], '$[].name': ['string']}}
        self.assertEqual([], differences(old, {'GET /x 200': {'$': ['array']}}))
        self.assertEqual([], differences({'x': {'$': ['object'], '$.a': ['object'], '$.a.name': ['string']}}, {'x': {'$': ['object'], '$.a': ['null']}}))
        self.assertEqual([], differences({'x': {'$': ['null', 'string']}}, {'x': {'$': ['string']}}))

    def test_merge_is_union_and_typed_operations_require_shapes(self):
        self.assertEqual({'x': {'$': ['null', 'string']}}, merge([{'x': {'$': ['null']}}, {'x': {'$': ['string']}}]))
        typed = list(operations({'paths': {'/x': {'get': {'responses': {'200': {'content': {'application/json': {'schema': {'type': 'object'}}}}}}}}}))
        self.assertEqual(['GET /x 200'], typed)
        self.assertNotIn(typed[0], merge([{}]))

    def test_group_root_slash_matches_openapi_without_hiding_shape_drift(self):
        before = {'GET /api/v1/organisation 200': {'$': ['object'], '$.name': ['string']}}
        recorded = {'GET /api/v1/organisation/ 200': {'$': ['object'], '$.name': ['string']}}
        self.assertEqual(before, merge([recorded]))
        self.assertTrue(differences(before, merge([{'GET /api/v1/organisation/ 200': {'$': ['object']}}])))
        self.assertTrue(differences(before, merge([{'GET /api/v1/organisation/ 200': {'$': ['object'], '$.name': ['number']}}])))
        self.assertIn('GET / 200', merge([{'GET / 200': {'$': ['object']}}]))
        self.assertIn('POST /api/v1/organisation 403', merge([{'POST /api/v1/organisation/ 403': {'$': ['object']}}]))

    def test_previous_run_cannot_mask_a_removed_field(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            for run in ['old', 'new']:
                (root / 'shapes' / run).mkdir(parents=True)
            old = {'x': {'$': ['object'], '$.name': ['string']}}
            new = {'x': {'$': ['object']}}
            (root / 'shapes/old/a.json').write_text(json.dumps(old))
            (root / 'shapes/new/a.json').write_text(json.dumps(new))
            (root / 'shapes/latest.json').write_text('{"run":"new"}')
            actual = merge(json.loads(p.read_text()) for p in recorded_files(root))
            self.assertTrue(differences(old, actual))

    def test_dictionary_ids_vary_without_hiding_value_shape_changes(self):
        def sample(key, field='name', kind='string'):
            return {'GET /x 200': {'$': ['object'], '$.people': ['object'], f'$.people.{key}': ['object'], f'$.people.{key}.{field}': [kind]}}
        old = merge([sample('00000000-0000-0000-0000-000000000001')])
        other = merge([sample('00000000-0000-0000-0000-000000000002')])
        self.assertEqual([], differences(old, other))
        self.assertTrue(differences(old, merge([sample('00000000-0000-0000-0000-000000000002', 'name', 'number')])))
        self.assertTrue(differences(old, merge([sample('00000000-0000-0000-0000-000000000002', 'different')])))


if __name__ == '__main__':
    unittest.main()
