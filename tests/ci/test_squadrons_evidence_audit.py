"""Keep native observations traceable without promoting them into generation rules."""
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[2]
SPEC = importlib.util.spec_from_file_location('squadrons_audit', ROOT / 'scripts/audit_squadrons_evidence.py')
AUDIT = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(AUDIT)


def native(button=22, name='Private hardware name'):
    fields = dict(altbutton=0, axis=26, button=button, deviceid=3, identifier=-123319776, modifier=0, negate=0, type=3)
    return ('GstInput.JoystickDevice4 ' + name + '\nFuture.Private secret\nGstKeyBinding.InputDataVersion 4\n' +
            ''.join(f'GstKeyBinding.IncomStarshipInputConcepts.ConceptTargetSelect.1.{k} {v}\n' for k, v in fields.items()))


class SquadronsEvidenceAuditTests(unittest.TestCase):
    def test_comparison_isolates_one_field_without_treating_names_as_public_data(self):
        before, _ = AUDIT.inspect_native(native())
        after, _ = AUDIT.inspect_native(native(23, 'Another private device'))
        result = AUDIT.compare(before, after)
        self.assertEqual(0, result['addedRecordCount'])
        self.assertEqual(0, result['removedRecordCount'])
        self.assertEqual(1, result['changedRecordCount'])
        self.assertEqual([4], result['changedDeclarationSlots'])
        self.assertEqual({'button': {'before': 22, 'after': 23}}, result['changedRecords'][0]['fields'])
        self.assertNotIn('private', json.dumps(result).lower())

    def test_dictionary_retains_observation_sources_and_varying_values(self):
        before, _ = AUDIT.inspect_native(native())
        after, _ = AUDIT.inspect_native(native(23))
        commands = AUDIT.dictionary({'A': before, 'B': after})
        self.assertEqual('observed-values-only', commands[0]['acceptance'])
        fields = commands[0]['records'][0]['parameters']
        self.assertEqual([{'value': 22, 'captures': ['A']}, {'value': 23, 'captures': ['B']}], fields['button'])
        self.assertEqual([{'value': -123319776, 'captures': ['A', 'B']}], fields['identifier'])
        self.assertEqual(set(AUDIT.FIELDS), set(fields))

    def test_capture_summary_excludes_source_path_names_and_private_settings(self):
        with tempfile.TemporaryDirectory() as folder:
            path = Path(folder) / 'private-profile'
            path.write_text(native(), encoding='utf-8')
            label, _, summary = AUDIT.capture('A', path)
        self.assertEqual('A', label)
        self.assertEqual(1, summary['recordCount'])
        self.assertEqual(64, len(summary['sha256AuditedBytes']))
        published = json.dumps(summary)
        for private in ['Private hardware name', 'secret', folder, 'private-profile']:
            self.assertNotIn(private, published)

    def test_published_audit_keeps_failed_padding_and_successful_probe_distinct(self):
        report = json.loads((ROOT / 'data/schemas/squadrons/native-evidence-audit.v1.json').read_text())
        self.assertEqual('observations-not-a-normative-game-specification', report['status'])
        self.assertEqual(6, len(report['captures']))
        inventory = json.loads((ROOT / 'data/schemas/squadrons/command-inventory.v1.json').read_text())
        self.assertEqual({r['concept'] for r in inventory['commands']}, {r['concept'] for r in report['commands']})
        for command in report['commands']:
            for record in command['records']:
                self.assertTrue(set(AUDIT.FIELDS) <= record['parameters'].keys())
                for values in record['parameters'].values():
                    for value in values:
                        self.assertTrue(set(value['captures']) <= {'V4-A', 'V4-B', 'GAME'})
        padding = report['comparisons']['REDUCED:PADDED']
        self.assertEqual((37, 0, 26), (padding['addedRecordCount'], padding['changedRecordCount'], padding['unchangedRecordCount']))
        self.assertEqual(14, report['comparisons']['PADDED:GAME']['changedOriginallyActiveJoystickRecords'])
        probe = report['comparisons']['GAME:PROBE']
        self.assertEqual((0, 0, 1, 399), tuple(probe[k] for k in ['addedRecordCount', 'removedRecordCount', 'changedRecordCount', 'unchangedRecordCount']))
        self.assertEqual([{'concept': 'IncomStarshipInputConcepts.ConceptTargetSelect', 'index': 1,
                          'fields': {'button': {'before': 22, 'after': 23}}}], probe['changedRecords'])


if __name__ == '__main__':
    unittest.main()
