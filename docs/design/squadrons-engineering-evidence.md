# Squadrons native export evidence

The general in-game export failure remains unresolved. This audit replaces the padding and automatic-name hypotheses with traceable observations. It does not call a structural pass a working game export. The requirement remains a reliable blank-slate exporter supplied by the adapter, without personal template imports or prebinding commands in the game.

## Audited corpus

The [machine-readable audit](../../data/schemas/squadrons/native-evidence-audit.v1.json) contains all eight parameter values for each observed command/index, with capture labels attached to every value. Its 125 command concepts match the current catalog. This is a second inventory, independent of the [bundled descriptor inventory](../../data/schemas/squadrons/command-inventory.v1.json); observations are not automatically generation recipes.

All six audited files use InputDataVersion 4. Counts describe file contents, not successful hardware acquisition.

| Label | Source role | Records | Concepts | Concepts with index gaps | Named declarations |
| --- | --- | ---: | ---: | ---: | ---: |
| V4-A | Earlier supplied native reference | 411 | 125 | 0 | 10 |
| V4-B | Earlier supplied Pitch reference | 391 | 125 | 0 | 9 |
| REDUCED | Supplied failed post-run export | 26 | 22 | 20 | 9 |
| PADDED | Supplied failed padding candidate | 63 | 22 | 0 | 9 |
| GAME | Supplied game-written file after MFD assignment | 400 | 125 | 0 | 9 |
| PROBE | GAME with one button-field edit | 400 | 125 | 0 | 9 |

Each label has a byte count and SHA-256 of the bytes actually audited. V4-A and V4-B were reconstructed from the supplied text; their hashes identify those audited copies, not independently verified original-download bytes. The recent four files were read locally as bytes. Raw user profiles, device names, source paths and unrelated scalar settings are excluded from the committed audit.

## Findings and limits

| Rule or hypothesis | Evidence | Conclusion and scope |
| --- | --- | --- |
| All eight fields must be tracked | Native records across the three reference captures | The dictionary records every field independently. Observed values do not establish all legal values or parameter interactions. |
| MFD 1 uses declaration 4 and `deviceid=3` | GAME target-select record after direct assignment | Confirmed association in this capture; does not explain acquisition of other controllers. |
| Button 1 is 22; Button 2 is 23 | GAME has button 22; PROBE changes only that field to 23; user reported the requested Button 2 action | One successful external edit for this command and controller. No post-probe capture was supplied to independently verify runtime persistence. |
| A native identifier can survive a button edit | The same PROBE retains identifier -123319776 | No requirement to zero or regenerate this identifier for this edit. General identifier construction remains unresolved. |
| Contiguous arrays alone fix the exporter | REDUCED → PADDED adds 37 disabled records and preserves all 26 originals; in-game failure remains | Rejected as a sufficient fix. Automatic padding is removed from production review export. Contiguity remains an observation, not a proven game requirement. |
| The game retained the padded assignments | PADDED → GAME adds 337 records and changes 51 common records; 14 changed records were originally active joystick records | False. All 37 fillers changed, too. A field-preserving pre-run export did not establish native acceptance. |
| A complete native envelope permits this edit | GAME → PROBE changes one field, retaining 399 records and all declarations | Supported for this single edit. It does not prove that 400 records are necessary or sufficient for a blank-slate file. |
| Adding occurrence suffixes fixes recognition | Earlier review export inferred names; no isolated successful recognition case establishes the transformation | Automatic name repair is removed. Explicit reviewed Add/Rename operations remain separate user actions. |
| Writing a TQS declaration establishes recognition | TQS appears in V4-A but is already missing from V4-B and recent post-run captures | Unsupported. Game acquisition and persistence of the intended TQS identity remain unresolved. |
| Every bundled command is game accepted | 125 concepts / 149 descriptors exist in adapter metadata | Unsupported. Catalog coverage and parser acceptance are not runtime acceptance. |

The exact successful native record is `IncomStarshipInputConcepts.ConceptTargetSelect.1`:

| Parameter | GAME | PROBE |
| --- | ---: | ---: |
| altbutton | 0 | 0 |
| axis | 26 | 26 |
| button | 22 | 23 |
| deviceid | 3 | 3 |
| identifier | -123319776 | -123319776 |
| modifier | 0 | 0 |
| negate | 0 | 0 |
| type | 3 | 3 |

The C# regression reproduces these record values and verifies that both native export and review export change only the requested button field, including LF/CRLF and an unrelated setting. Its small synthetic envelope tests writer preservation; it does not pretend to reproduce the complete game acceptance case. Python tests verify the audit comparison, observation provenance, privacy boundaries and published case counts.

## Contract still to establish

A production blank-slate contract needs a versioned native envelope, command-specific construction rules, identifier rules, record ownership rules and game-recognized device identity/slot rules. The current captures do not establish which missing fields or records caused the reduced file to be replaced, the meaning of every type -1 record, or the cause of TQS acquisition failure. Copying the 400-record file into a template would conceal those gaps rather than resolve them.

Implementation acceptance must distinguish structural validity, exact requested semantic changes, preservation of unrelated data, and actual game behavior. Each supported construction rule needs an evidence scope; unknown combinations must not silently become supported because a descriptor can be serialized. Blank-slate output must be constructed from adapter-owned verified rules without requiring a user's prior assignments. These are adapter engineering obligations, not additional user setup steps.

## Current exporter behavior

Review export groups existing controller declarations in numeric order. It preserves every native name, binding address, binding field and unrelated setting. It neither fills missing indices nor derives occurrence names. Source-preserving Export remains exact. This removes unsupported mutations; it is not a claim that the remaining exporter now satisfies the unresolved native contract.

The read-only inspector still reports sparse arrays. `--strict-arrays` is an optional investigation policy, not a proven Squadrons requirement.

The audit tool can be reproduced against privately held sources without publishing them:

```sh
python scripts/audit_squadrons_evidence.py --capture A=path/to/before --capture B=path/to/after --dictionary A B --compare A:B --output audit.json
python -m unittest discover -s tests/ci -p 'test_*.py'
```

## Later working-controls capture and targeted correction

The later supplied capture contains 376 records across the same 125 concepts. The user reports working MFD bindings, MFD 1 Button 3 selecting the target, TPR yaw, SOL-R roll, and SOL-R pitch with reversed desired direction. Its target-select record uses `deviceid=3, button=24`. Pitch uses SOL-R device 0 and axis codes 9/11 at indices 6/7. Both throttle records use `deviceid=1`, whose declaration now names the ICP. TQS has no declaration; slot 10 is empty. These observations establish a wrong destination for the intended TQS throttle, not the cause of the game's roster changes.

A separate review candidate retains all 376 records and changes exactly five native lines: fill empty declaration 10 with the exact TQS name present in V4-A; set both throttle device IDs to 9; swap pitch axis codes 9/11 between their existing records. All identifiers, negate fields, other bindings and scalar settings remain exact. The user's original upload is untouched. This candidate repairs the intended references and reverses the encoded pitch pair; TQS runtime acquisition and the resulting pitch direction remain unverified until observed in the game. It does not add missing TQS button assignments.

The application now provides atomic **Reverse axis** and blocks review export when a saved physical link conflicts with its native name. The check cannot infer an unlinked controller's intended identity. These changes address explicit editor/validation gaps; they do not claim to solve the still-unknown game device-acquisition rules.
