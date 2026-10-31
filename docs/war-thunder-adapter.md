# War Thunder adapter — native foundation and readiness

War Thunder now has an `ISimProfileBackend` implementation with native-backed portable profiles, a captured command/parameter catalog, saved device ranges, occurrence-addressed edits and exact native text export. It is not yet registered as a desktop game. Physical input translation and desktop workflows remain #334.

PR1B integrated the protected `feature/war-thunder-adapter-foundation` parser into the shared .NET 10 solution. PR6 of #323 audits it against the shared contracts accepted through Squadrons PR5 and strengthens the scalar-edit boundary. See the [PR6 readiness report](design/shared-war-thunder-pr6.md) for the component reuse map, findings, capability matrix and local acceptance commands.

## Implemented subset

- Capture typed scalars and nested blocks while preserving accepted source text exactly.
- Inventory root-level or wrapped controls, including repeated hotkeys, axes and `deviceMapping/joystick` fields, without inferring hardware identity.
- Report numeric activation codes without assigning undocumented tap/hold meanings.
- Replace one uniquely addressed supported scalar, preserving comments and all other source spans.
- Validate quoted `s`/`t`, 32-bit `i`, `yes`/`no` or `0`/`1` booleans, and finite `r` replacements as single tokens.
- Keep successfully captured opaque/unsupported syntax available for unchanged export and inspection, with scalar writes blocked. Unsupported structural forms can still fail capture; this is not a complete BLK grammar.
- Retain duplicate paths as evidence and reject ambiguous writes; expose immutable scalar/block inventories.

`WarThunderControls.EditableSettings` lists supported unique literal scalar edits. It does not grant command authoring, physical input mapping or deployment. The native document is immutable. `WarThunderProfileBackend` stages independent native-backed profiles after validation; failed edits leave the original source available. See the [native backend contract](design/war-thunder-native-contract.md) for methods, capability boundaries and the inspection schema.

## Integration boundaries

The project is already referenced by `HEIMDALL.sln` and `Heimdall.Tests`. War Thunder joins the expected production assemblies for the independent 80/80 coverage and CRAP <=50 gates in PR6. A desktop consumer must reuse the shared lifecycle, command browser/editor, drawing interactions, link policy and input feedback; native interpretation remains in `Heimdall.WarThunder`.

A privately reviewed game export establishes the basic controls/device-mapping shape and native boolean spelling. Repository tests use generic authored examples. Occurrence-aware addresses, captured command/parameter metadata, native range validation and guarded source edits are implemented in #333. Physical index translation, complete inherited command coverage and in-game acceptance of newly generated bindings remain explicit integration requirements.

- [#333: verify the native binding contract and implement the profile backend](https://github.com/ScottyMac52/DCS-IPIA/issues/333).
- [#334: connect the verified adapter to the shared game workspace](https://github.com/ScottyMac52/DCS-IPIA/issues/334).

The old feature branch is retained. Existing DCS, Elite and Squadrons workflows remain registered. Any future export uses a separate reviewed copy; this foundation does not write active game settings.
