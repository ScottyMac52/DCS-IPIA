# War Thunder native adapter contract v1 — #333

## Scope and ownership

`WarThunderProfileBackend` implements `ISimProfileBackend` in `Heimdall.WarThunder`. It imports one supplied exported `.blk` file with one controls-v5 envelope, builds a context-scoped native catalog and logical saved devices, returns independent edited profiles, and plans exact text export under the original safe leaf filename. It never discovers active game folders, acquires controllers, writes files or deploys settings.

The supported export shape was reviewed against a game-generated capture. Repository regression examples are authored generic data. In-game acceptance of newly generated native records remains separate from structural validation. The catalog is explicitly **captured**, not a complete inventory of every game command: `basePresetPaths` can refer to additional inherited game definitions. Such references are preserved and produce a conversion warning; inherited commands/defaults are not fabricated.

## Native source and occurrence addresses

Accepted text preserves comments, whitespace, line endings, record order, repeated fields, unrecognized commands and unrelated configuration. Supported scalar forms are bounded 32-bit `i`, finite `r`, quoted `s`/`t`, and `yes`/`no` or `0`/`1` booleans. Opaque accepted syntax remains inspectable and exportable without native writes; unsupported structure/version is rejected. No-op export returns the captured text exactly.

The parser retains each block's complete header/body span and each scalar's value span. Offsets index .NET UTF-16 strings, not encoded file bytes. Addresses are kind-prefixed, escaped, zero-based sibling occurrences:

```text
b:controls[0]/hotkeys[0]/ID_FIRE[1]
s:controls[0]/hotkeys[0]/ID_FIRE[1]/joyButton[0]
```

Legacy paths remain for inspection, and ambiguous path writes are still rejected. Occurrence-addressed writes select an exact repeated block and an exact repeated field. Value replacements validate all tokens before producing output, apply spans from right to left and reparse the resulting text. Clear retains an empty command header, so later alternatives keep their addresses. Appending alternatives does not renumber earlier records. A later imported file is a new snapshot; consumers must not apply old selection state without the shared session revision/leave guards.

## Device and input identity

Each `deviceMapping/joystick` occurrence becomes a logical `war-thunder.joystick.N` reference. N is the zero-based captured mapping occurrence, not a Windows enumeration position or a claim of connection. Captured VID/PID-like products and labels are retained, but `ResolveDevice` resolves only an explicitly supplied exact instance identity. Repeated identical products and labels never auto-link hardware.

Native axis and button offsets/counts define separate half-open ranges. A supplied zero-based native local index is encoded as `offset + index`, checked with widened arithmetic and required to have exactly one saved range owner. Missing, invalid, overflowing and overlapping ranges cannot silently select another unit. Original unresolved inputs still export unchanged with warnings.

These indices are **native**. The adapter does not infer a `JOY_X`, `JOY_BTN1`, POV direction or portable physical axis from count/order. Windows instance linking and verified native-to-physical translation remain explicit #334 consumer work. Portable `LogicalDevice` envelopes therefore contain identities, while the authoritative assignments remain occurrence-addressed native records returned by `ReadBindings`.

## Command and parameter dictionary

`GetCatalog` groups hotkey/axis blocks by exact section/name, including empty and unknown commands. Command IDs include the caller's context and section. Existing alternatives remain separate records, with every captured direct field/type/value exposed by `ReadBindings`. Metadata includes native command name, section, captured coverage and the observed parameter types. A parameter such as `nonlinearity` can retain different captured native numeric types; no command-specific encoder or universal coercion is used.

The original catalog text remains pinned independently of edited text. Clearing bindings does not erase their command identity. Inherited presets are not expanded automatically. Native activation codes and repeated chord members remain literal evidence; they are not relabelled as tap, hold or portable modifier semantics.

## Implemented native operations

| Operation | Validation and behavior |
| --- | --- |
| Rebind one input | Exact record and scalar occurrence; `joyButton:i` in hotkeys or a unique `axisId:i` in an unambiguous axis block; bounded unique target range; preserve all other chord/system fields and parameters |
| Add hotkey binding | Captured command only; fill an empty record or append a basic `joyButton:i` alternative; preserve existing activation-bearing alternatives; do not invent an activation code |
| Fill an unassigned axis block | One captured axis block without `axisId`; add one bounded native `axisId` and retain captured parameters |
| Clear joystick alternative | Joystick-only hotkey fields plus optional captured activation code; retain an explicit empty block; preserve other alternatives; mixed keyboard/mouse and axis clear are unavailable |
| Invert axis | Set native `inverse:b` while preserving captured boolean spelling, or append a known inverse flag; retain axis ownership and parameters |
| Edit captured axis literal | Existing unique `inverse`, `kMul`, `innerDeadzone`, `nonlinearity` or `relative`; preserve captured type; no unit/curve conversion or parameter-range invention; input ownership, mouse mappings and unknown parameters remain protected |
| Inspect | Full native scalar/block inventory, device ranges, binding occurrences and diagnostics in a typed inspection model |

`GetBindingCapabilities` reports the operations supported by each current record shape. Nested command data and repeated ambiguous axis blocks remain preserved and noneditable. Unsupported operations are not advertised through physical editor interfaces. Portable `SimBackendCapabilities` declares no interpreted button modes, modifier translation, portable filters or deployment. New blank-profile generation, device roster creation/reorder/removal and new arbitrary command definitions are not implemented.

## Portable envelope and export validation

The versioned `war-thunder.v1` evidence pins context, filename, current native text and original catalog text. Rebuilding the canonical portable projection, including text/catalog hashes and all saved identities, detects portable changes that would otherwise disappear during export. Use adapter methods for native writes. Generic mutation of axes/buttons, hardware identities, backend/schema/context, metadata or partial evidence produces a blocking export diagnostic and no files. A profile JSON save/reopen retains the exact native text and original filename.

The inspection model is described by [native-inspection.v1.schema.json](../../data/schemas/war-thunder/native-inspection.v1.schema.json). This schema documents output from `Inspect`; JSON is not fed to a generative BLK writer. Native text validation, evidence/projection validation and range validation remain authoritative. Schema validity does not prove game recognition.

## Acceptance

C# regressions cover both newline styles, JSON persistence, unknown data and empty blocks, exact/ambiguous instance resolution, native range edges/overlaps/overflow, duplicate products, occurrence identity, isolated chord/axis edits, immutable failure, creation, clear, inversion, parameter types, cancellation, unsafe filenames, malformed envelopes and out-of-band mutation rejection. Existing parser tests remain.

.NET build/tests and measured 80/80/50 gates require execution on the maintainer's .NET 10 environment; Windows smoke remains required before desktop integration. The authoring environment lacks that runtime. #334 retains the UI/game-selection work; this change does not register a War Thunder game tile.
