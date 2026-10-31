"""Inspect Squadrons native text without claiming that the game accepts its bindings."""
import argparse
import json
import re
from pathlib import Path

FIELDS = ('altbutton', 'axis', 'button', 'deviceid', 'identifier', 'modifier', 'negate', 'type')
PREFIX = 'GstKeyBinding.'
DEVICE = 'GstInput.JoystickDevice'
INT_MIN, INT_MAX = -(2**31), 2**31 - 1


def inspect_native(text):
    """Return the schema inspection model and unresolved joystick references."""
    if not text.strip() or '\0' in text or len(text) > 4 * 1024 * 1024:
        raise ValueError('Expected nonempty native text below 4 MiB, without NUL.')
    values, records, devices, settings = {}, {}, [], {}
    for number, line in enumerate(text.split('\n'), 1):
        line = line.removesuffix('\r')
        if not line.strip():
            continue
        key, sep, value = line.partition(' ')
        if not sep or not key or key in values:
            raise ValueError(f'Line {number}: malformed or duplicate setting {key!r}.')
        values[key] = value
        if key.startswith(DEVICE):
            suffix = key[len(DEVICE):]
            if not suffix.isascii() or not suffix.isdigit() or not 1 <= int(suffix) <= 10:
                raise ValueError(f'Line {number}: slot outside the current adapter range 1..10.')
            if any(d['slot'] == int(suffix) for d in devices):
                raise ValueError(f'Line {number}: duplicate numeric controller slot.')
            devices.append({'slot': int(suffix), 'name': value})
        elif key.startswith(PREFIX) and key != PREFIX + 'InputDataVersion':
            match = re.fullmatch(r'GstKeyBinding\.(\S+)\.([0-9]+)\.([^\s.]+)', key)
            if not match or not re.fullmatch(r'[+-]?[0-9]+', value) or not INT_MIN <= int(value) <= INT_MAX:
                raise ValueError(f'Line {number}: malformed indexed binding or signed 32-bit field.')
            concept, index, field = match.groups()
            if int(index) > INT_MAX:
                raise ValueError(f'Line {number}: record index exceeds signed 32-bit range.')
            fields = records.setdefault((concept, int(index)), {})
            if field in fields:
                raise ValueError(f'Line {number}: duplicate normalized binding field.')
            fields[field] = int(value)
        else:
            settings[key] = value
    version = settings.pop(PREFIX + 'InputDataVersion', None)
    if version != '4' and (version is not None or records or not any(k.startswith('GstInput.') for k in values)):
        raise ValueError('Expected input data version 4, or a fresh GstInput document without bindings.')
    bindings = []
    for (concept, index), fields in sorted(records.items()):
        missing = set(FIELDS) - fields.keys()
        if missing:
            raise ValueError(f'{concept}.{index}: missing fields {sorted(missing)}.')
        bindings.append({'concept': concept, 'index': index, 'fields': dict(sorted(fields.items()))})
    named = {d['slot'] for d in devices if d['name'].strip()}
    unresolved = [f"{b['concept']}.{b['index']}: joystick deviceid {b['fields']['deviceid']} has no named slot {b['fields']['deviceid'] + 1}."
                  for b in bindings if b['fields']['type'] == 3 and b['fields']['deviceid'] + 1 not in named]
    return {'format': 'squadrons-native-inspection-v1', 'inputDataVersion': int(version) if version else None,
            'settings': settings, 'devices': sorted(devices, key=lambda d: d['slot']), 'bindings': bindings}, unresolved


def sparse_arrays(model):
    """Report absent lower indices without guessing how the game will fill them."""
    groups = {}
    for row in model['bindings']:
        groups.setdefault(row['concept'], set()).add(row['index'])
    return {concept: sorted(set(range(max(indices) + 1)) - indices)
            for concept, indices in groups.items() if max(indices) < 128 and len(indices) != max(indices) + 1}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('file', type=Path)
    parser.add_argument('--json', action='store_true', help='Print the parsed inspection model, not a game export.')
    parser.add_argument('--strict-links', action='store_true', help='Fail for joystick references without named declarations.')
    parser.add_argument('--strict-arrays', action='store_true', help='Fail when native record arrays have missing lower indices.')
    args = parser.parse_args()
    try:
        model, warnings = inspect_native(args.file.read_text(encoding='utf-8-sig'))
    except (ValueError, OSError, UnicodeError) as error:
        parser.exit(1, str(error) + '\n')
    if args.json:
        print(json.dumps(model, indent=2))
    else:
        print(f"Structure valid: {len(model['devices'])} declarations, {len(model['bindings'])} records. Game acceptance UNVERIFIED.")
    for warning in warnings:
        print('Unresolved reference: ' + warning)
    gaps = sparse_arrays(model)
    for concept, indices in gaps.items():
        print(f'Sparse native array: {concept}; missing indices {indices}')
    if (args.strict_links and warnings) or (args.strict_arrays and gaps):
        parser.exit(1)


if __name__ == '__main__':
    main()
