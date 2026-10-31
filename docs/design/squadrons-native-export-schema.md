# Squadrons native export contract — draft 1

**Status: structural inspection contract and evidence inventory, not a validated game exporter specification.** The real-game failure reported on 2026-10-30 invalidates any assumption that parser round trips or populated declarations establish working bindings. This document deliberately separates native values, HEIMDALL interpretations and unresolved game semantics.

## Deliverables and scope

- [Complete command/parameter reference](../reference/squadrons-command-parameters.md): all 125 currently known commands, with 149 bundled joystick record variants and all eight fields per variant.
- [Machine-readable inventory](../../data/schemas/squadrons/command-inventory.v1.json): full concept names, contexts, record indices, parameter objects, current adapter input labels and evidence status.
- [Flat CSV](../../data/schemas/squadrons/command-parameters.v1.csv): one row per variant; no axis direction or alternative record is collapsed.
- [JSON Schema](../../data/schemas/squadrons/native-export-inspection.v1.schema.json): schema for the parsed inspection model. **Squadrons consumes native text, not this JSON.**
- [Independent native evidence audit](squadrons-engineering-evidence.md): observed parameter dictionary, failed hypotheses and the isolated successful MFD edit.
- [Native inspector](../../scripts/squadrons_native_schema.py): checks text grammar, completeness, duplicate addresses, numeric fields and unresolved joystick references. It does not deploy or modify files.

This list covers the current adapter catalog, not every command implemented by every game version. The bundled variants cover joystick authoring. Complete keyboard, mouse and gamepad defaults have not been established; captured records in those families must remain intact. Unknown concepts and additional numeric record fields are preserved for inspection, rather than relabeled as verified support.

## Native text grammar

```text
GstInput.JoystickDevice<slot> <native device name or empty value>
GstKeyBinding.InputDataVersion 4
GstKeyBinding.<concept>.<index>.<field> <signed 32-bit integer>
<other setting key> <raw setting value>
```

The first ASCII space separates key and value. Device names and other values can contain spaces; an empty value is distinct from a missing declaration. Blank lines and LF/CRLF endings are retained by the production source document. Duplicate setting keys and incomplete indexed records are invalid. Review export preserves record indices and never fills missing entries; sparse-array findings are diagnostic observations. Native record indices are nonnegative signed 32-bit values; the adapter currently limits newly appended indices to below 128, which is **not a verified native format limit**.

`InputDataVersion 4` is required when binding records exist. The adapter also accepts fresh `GstInput` settings without a version or binding table. This exception supports blank-slate authoring; it does not establish that a game export without the version will be accepted.

## Every binding parameter

| Parameter | Structural representation | Current interpretation / evidence limit |
| --- | --- | --- |
| `altbutton` | Signed 32-bit integer | Alternate input metadata; the meaning of nonzero values is unresolved. Zero is used by the bundled joystick descriptors. |
| `axis` | Signed 32-bit integer | Native input code, not a DirectInput axis ordinal. Its relationship to `button` is device-family dependent. |
| `button` | Signed 32-bit integer | Native code, not always a button number: extended axes and hats also use this field in the adapter. |
| `deviceid` | Signed 32-bit integer | Adapter maps joystick value `n` to declaration `JoystickDevice(n+1)`. This identifies a native slot, never a Windows GUID. Runtime retention of the slot/name association requires game evidence. |
| `identifier` | Signed 32-bit integer | Preserve exact captured value, including negative and zero values. Its generation/lookup rule is unresolved. A bundled identifier is not proof that changing other fields produces a valid game record. |
| `modifier` | Signed 32-bit integer | Native modifier metadata; complete cross-device/type semantics are unresolved. Do not substitute HEIMDALL modifier IDs. |
| `negate` | Signed 32-bit integer | Native polarity metadata. Preserve independently of the adapter's decoded positive/negative input direction; universal boolean/direction semantics are unproven. |
| `type` | Signed 32-bit integer | Adapter currently interprets 0=mouse, 1=gamepad, 2=keyboard, 3=joystick. Unknown values remain preserved, without editable-input claims. |

A record address is `(concept, index)`. The index is not a command ID, device slot or Primary/Secondary binding selector. Records for the same command may have different identifiers, inputs and direction metadata. Preserve additional captured integer fields until their semantics are established.

## Devices and file export

| Item | Contract |
| --- | --- |
| Filename | `ProfileOptions_profile_synced`; no extension is appended. |
| Encoding | Current exporter writes UTF-8 without BOM. The inspector accepts an optional UTF-8 BOM. Game acceptance of every encoding variant is not established. |
| Controller declarations | Preserve every captured declaration, including empty slots. The adapter range is 1–10; this is not a verified game capacity. |
| Presentation order | The current review exporter groups declarations before binding records and sorts slot numbers numerically. That is an output convention, not evidence that native line order caused the in-game failure. |
| Native identities | Use the identity the game actually recognizes. A matching Windows display name, guessed suffix or successful HEIMDALL link is insufficient evidence of game recognition. |
| Occurrence suffix | Review export preserves captured names. Explicit Add/Rename actions offer reviewed suggestions; no suffix transformation is established as a universal native identity rule. |
| Record ownership | When slots are intentionally changed, every affected joystick `deviceid` must move atomically with its intended controller. Other input families retain their native values. |
| Non-binding settings | Preserve unknown scalar settings, native versions and unrelated values. They must not disappear because they are absent from the command catalog. |
| Blank slate | No active assignments may be materialized merely by browsing the catalog. A user's explicit assignment must produce the correct native record(s) from proven adapter metadata, without requiring prior game-side bindings. |
| Deployment | Export remains a separate review copy. This schema and inspector never write active game settings. |

The supplied post-run capture retained all 26 binding records while changing the controller roster and losing the intended throttle declaration. It also retained MFD declarations without working in-game bindings. Therefore referential consistency and field preservation are necessary but insufficient acceptance checks.

## Structural constraints beyond JSON Schema

Controller slot numbers and `(concept, index)` addresses must be unique. Every native key must be unique before parsing. A joystick reference should resolve to a nonempty declaration; the inspector reports unresolved references, and `--strict-links` makes them errors. JSON Schema alone cannot enforce these relational rules or prove that a name refers to the intended connected hardware. Matching an unrelated panel at the same slot is structurally valid and semantically wrong.

Desktop review export also checks saved physical-link names against native declarations. A mismatch blocks output without changing either name; a resolved integer slot alone is insufficient. This comparison is available only for saved links and cannot prove game recognition.

The inspection model separates scalar `settings`, `devices`, and `bindings`. It is an analysis representation, not a lossless serialization envelope: use the original native source to preserve whitespace, ordering and unknown lines when editing/exporting. Do not generate a replacement game file by simply serializing this JSON.

## Evidence and acceptance work remaining

| Level | Meaning |
| --- | --- |
| Catalogued | Command string exists in the current adapter catalog. |
| Bundled | Descriptor fields exist in the shipped metadata. |
| Observed | Exact fields were captured; source/version and whether the game generated or merely retained them must be recorded. |
| Structurally valid | Grammar, eight required fields, integer bounds and references pass inspection. |
| Game accepted | The game retains the intended device association and the requested control invokes the intended command. **Unverified for the inventory as a whole; one isolated MFD edit has a user-reported success.** |

The 95 bundled nonzero-identifier records and 54 zero-identifier records are listed without claiming per-record acceptance. The zero-identifier set mixes observed and generalized metadata; a per-record independent provenance map is still required. See [existing provenance notes](squadrons-custom-record-provenance.md).

Controlled game-generated cases must establish joystick input encoding, identifier behavior, record-index rules, native device-name resolution, occurrence suffixes, slots retained after restart, and effective mappings for an axis, button and hat. Before/after files must identify the game version, changed physical control, selected command and result. Capturing these cases is adapter development evidence; it must not become a user prerequisite for blank-slate authoring.

## Commands

Run from the repository root:

```sh
python scripts/generate_squadrons_schema.py
python scripts/squadrons_native_schema.py path/to/ProfileOptions_profile_synced
python scripts/squadrons_native_schema.py path/to/ProfileOptions_profile_synced --strict-links
python scripts/squadrons_native_schema.py path/to/ProfileOptions_profile_synced --json
python -m unittest discover -s tests/ci -p 'test_*.py'
```

The CLI's success message explicitly states that game acceptance is unverified. No structural check should be reported as an in-game pass.


## Retired sparse-array hypothesis

Native references have contiguous arrays. Filling 37 lower-index gaps with inactive records did not fix the reported in-game failure. The subsequent game-written capture changed all 37 fillers and 14 originally active joystick records. Automatic completion is removed from review export. The inspector still reports gaps, and `--strict-arrays` can enforce that optional investigation policy; it is not a verified native acceptance constraint.

The [engineering evidence matrix](squadrons-engineering-evidence.md) records this failed experiment, the independent observation dictionary and the one-field MFD edit that the user reported working. It separates those results from unresolved identifier, envelope and device-acquisition rules. No raw user profiles are committed.
