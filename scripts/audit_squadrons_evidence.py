"""Build an observation dictionary and compare native captures without exposing device names."""
import argparse
import hashlib
import json
from pathlib import Path
import sys

sys.path.insert(0, str(Path(__file__).resolve().parent))
from squadrons_native_schema import FIELDS, inspect_native, sparse_arrays


def capture(label, path):
    data = Path(path).read_bytes()
    model, unresolved = inspect_native(data.decode('utf-8-sig'))
    return label, model, {'sha256AuditedBytes': hashlib.sha256(data).hexdigest(), 'byteCount': len(data),
                          'inputDataVersion': model['inputDataVersion'], 'recordCount': len(model['bindings']),
                          'conceptCount': len({r['concept'] for r in model['bindings']}),
                          'declaredSlotCount': len(model['devices']),
                          'namedSlotCount': sum(bool(r['name'].strip()) for r in model['devices']),
                          'typeCounts': {str(t): sum(r['fields']['type'] == t for r in model['bindings'])
                                         for t in sorted({r['fields']['type'] for r in model['bindings']})},
                          'sparseConceptCount': len(sparse_arrays(model)), 'unresolvedReferenceCount': len(unresolved)}


def compare(before, after):
    old = {(r['concept'], r['index']): r['fields'] for r in before['bindings']}
    new = {(r['concept'], r['index']): r['fields'] for r in after['bindings']}
    changed = []
    for concept, index in sorted(old.keys() & new.keys()):
        fields = {k: {'before': old[(concept, index)].get(k), 'after': new[(concept, index)].get(k)}
                  for k in sorted(old[(concept, index)].keys() | new[(concept, index)].keys())
                  if old[(concept, index)].get(k) != new[(concept, index)].get(k)}
        if fields:
            changed.append({'concept': concept, 'index': index, 'fields': fields})
    old_devices = {r['slot']: r['name'] for r in before['devices']}
    new_devices = {r['slot']: r['name'] for r in after['devices']}
    return {'addedRecordCount': len(new.keys() - old.keys()), 'removedRecordCount': len(old.keys() - new.keys()),
            'changedRecordCount': len(changed), 'unchangedRecordCount': sum(old[k] == new[k] for k in old.keys() & new.keys()),
            'changedOriginallyActiveJoystickRecords': sum(old[(r['concept'], r['index'])]['type'] == 3 for r in changed),
            'changedDeclarationSlots': [slot for slot in sorted(old_devices.keys() | new_devices.keys())
                                        if old_devices.get(slot) != new_devices.get(slot)],
            'changedRecords': changed}


def dictionary(models):
    commands = {}
    for label, model in models.items():
        for row in model['bindings']:
            command = commands.setdefault(row['concept'], {})
            record = command.setdefault(row['index'], {})
            for field, value in row['fields'].items():
                record.setdefault(field, {}).setdefault(value, []).append(label)
    return [{'concept': concept, 'acceptance': 'observed-values-only',
             'records': [{'index': index, 'parameters': {
                 field: [{'value': value, 'captures': sorted(set(labels))} for value, labels in sorted(values.items())]
                 for field, values in sorted(fields.items())}}
                         for index, fields in sorted(records.items())]}
            for concept, records in sorted(commands.items())]


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--capture', action='append', required=True, metavar='LABEL=PATH')
    p.add_argument('--dictionary', nargs='+', required=True, help='Capture labels used for the observation dictionary.')
    p.add_argument('--compare', action='append', default=[], metavar='BEFORE:AFTER')
    p.add_argument('--output', required=True, type=Path)
    args = p.parse_args()
    models, summaries = {}, {}
    for spec in args.capture:
        label, path = spec.split('=', 1)
        if label in models:
            p.error('Duplicate capture label: ' + label)
        label, model, summary = capture(label, path)
        models[label], summaries[label] = model, summary
    chosen = {label: models[label] for label in args.dictionary}
    comparisons = {}
    for pair in args.compare:
        before, after = pair.split(':', 1)
        comparisons[pair] = compare(models[before], models[after])
    report = {'schemaVersion': 1, 'status': 'observations-not-a-normative-game-specification',
              'privacy': 'Device names, file paths and unrelated scalar settings are excluded.',
              'captures': summaries, 'comparisons': comparisons, 'commands': dictionary(chosen)}
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(report, separators=(',', ':')) + '\n')
    print(f"Audited {len(models)} captures; {len(report['commands'])} command concepts; {len(comparisons)} comparisons.")


if __name__ == '__main__':
    main()
