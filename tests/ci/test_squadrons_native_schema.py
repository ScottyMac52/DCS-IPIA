"""Keep the Squadrons evidence inventory complete and reject damaged native records."""
import csv
import hashlib
import importlib.util
import json
from pathlib import Path
import re
import unittest

ROOT = Path(__file__).resolve().parents[2]
SPEC = importlib.util.spec_from_file_location('squadrons_schema', ROOT / 'scripts/squadrons_native_schema.py')
SCHEMA = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(SCHEMA)


def record(device=0, extra=''):
    values = dict(altbutton=0, axis=26, button=22, deviceid=device, identifier=-123, modifier=0, negate=0, type=3)
    return 'GstKeyBinding.InputDataVersion 4\n' + ''.join(f'GstKeyBinding.Example.ConceptFire.1.{key} {value}\n' for key, value in values.items()) + extra


class SquadronsNativeSchemaTests(unittest.TestCase):
    def test_inventory_covers_current_catalog_and_exact_bundled_parameters(self):
        folder = ROOT / 'data/schemas/squadrons'
        inventory = json.loads((folder / 'command-inventory.v1.json').read_text())
        concepts = set()
        source = (ROOT / 'src/Heimdall.Squadrons/SquadronsKnownConcepts.cs').read_text()
        for scope, names in re.findall(r'Add\(result, "([^"]+)", """(.*?)"""\);', source, re.S):
            concepts.update(scope + '.Concept' + name for name in names.split())
        self.assertEqual(concepts, {c['concept'] for c in inventory['commands']})
        self.assertEqual(len(concepts), inventory['commandCount'])
        files = [('sourceSha256', inventory['source']), ('catalogSha256', 'src/Heimdall.Squadrons/SquadronsKnownConcepts.cs'),
                 ('codecSha256', 'src/Heimdall.Squadrons/SquadronsInputCodec.cs')]
        for field, path in files:
            self.assertEqual(hashlib.sha256((ROOT / path).read_bytes()).hexdigest(), inventory[field], 'Regenerate Squadrons inventory after source changes.')
        model, _ = SCHEMA.inspect_native((ROOT / inventory['source']).read_text())
        expected = {(r['concept'], r['index']): r['fields'] for r in model['bindings']}
        actual = {(c['concept'], r['index']): r['fields'] for c in inventory['commands'] for r in c['records']}
        self.assertEqual(expected, actual)
        with (folder / 'command-parameters.v1.csv').open() as stream:
            rows = list(csv.DictReader(stream))
        self.assertEqual(len(expected), len(rows))
        for row in rows:
            self.assertEqual(expected[(row['concept'], int(row['index']))], {k: int(row[k]) for k in SCHEMA.FIELDS})
            self.assertEqual('unverified', row['gameAcceptance'])
        schema = json.loads((folder / 'native-export-inspection.v1.schema.json').read_text())
        fields = schema['properties']['bindings']['items']['properties']['fields']
        self.assertEqual(set(SCHEMA.FIELDS), set(fields['required']))

    def test_scattered_declarations_unknown_fields_and_negative_identifiers_survive_inspection(self):
        text = 'Future.Setting  keep spaces\r\n' + record(extra='GstInput.JoystickDevice1 Throttle 1\nGstKeyBinding.Example.ConceptFire.1.future 7\n')
        model, unresolved = SCHEMA.inspect_native(text)
        self.assertEqual([], unresolved)
        self.assertEqual(' keep spaces', model['settings']['Future.Setting'])
        self.assertEqual(-123, model['bindings'][0]['fields']['identifier'])
        self.assertEqual(7, model['bindings'][0]['fields']['future'])

    def test_damage_is_rejected_instead_of_becoming_a_partial_binding(self):
        bad = [record().replace('.axis 26\n', '.axis nope\n'), record().replace('identifier -123', 'identifier 2147483648'),
               record().replace('InputDataVersion 4', 'InputDataVersion 2'), record().replace('GstKeyBinding.Example.ConceptFire.1.type 3\n', ''),
               record(extra='GstKeyBinding.Example.ConceptFire.1.type 3\n'), record(extra='GstKeyBinding.Example.ConceptFire.01.type 3\n'),
               record(extra='GstInput.JoystickDevice1 A\nGstInput.JoystickDevice01 B\n'), 'bad', '\0']
        for text in bad:
            with self.subTest(text=text), self.assertRaises(ValueError):
                SCHEMA.inspect_native(text)

    def test_missing_and_empty_slots_are_unresolved_without_inventing_hardware(self):
        for declaration in ['', 'GstInput.JoystickDevice2 \n']:
            model, unresolved = SCHEMA.inspect_native(record(1, declaration))
            self.assertEqual(1, len(unresolved))
            self.assertEqual(1, model['bindings'][0]['fields']['deviceid'])
        # A name is a declaration, not proof of the correct physical device or game acceptance.
        _, unresolved = SCHEMA.inspect_native(record(1, 'GstInput.JoystickDevice2 Unrelated Panel 1\n'))
        self.assertEqual([], unresolved)

    def test_fresh_controller_only_document_needs_no_prior_assignment(self):
        model, unresolved = SCHEMA.inspect_native('GstInput.JoystickDevice1 Stick 1\n')
        self.assertIsNone(model['inputDataVersion'])
        self.assertEqual([], model['bindings'])
        self.assertEqual([], unresolved)


if __name__ == '__main__':
    unittest.main()
