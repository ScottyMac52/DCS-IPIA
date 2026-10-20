# Multi-sim/game profile foundation

PR #285 introduces experimental intermediate profile DTOs in
`Dcs.Ipia.Domain.MultiSim` and backend/catalog contracts in
`Dcs.Ipia.Application.Interfaces`. These types do not add another game to the
application, replace saved DCS workspaces, migrate SQLite, or deploy native files.
Implementation and acceptance are tracked in [issue #286](https://github.com/ScottyMac52/DCS-IPIA/issues/286).

The useful shared boundary is physical controls and artwork plus explicit
bindings. Command catalogs, contexts, file formats, axis conventions and supported
binding modes belong to each versioned backend. Reusing a joystick diagram does
not make two games' command names equivalent or provide an input-remapping engine.

## Decisions required before backend adoption

- Define a versioned document with explicit backend/game, profile and context
  identities; do not rely on an unvalidated `Metadata["sim"]` convention.
- Separate catalog definitions, actual connected instances, logical roles and
  game-specific identifiers. `PhysicalDeviceIdentity.InstanceId` names the native
  instance string; it is not a product GUID or a catalog ID. Existing
  `Domain.PhysicalDevice` describes a catalog definition, so the proposed
  `ISimProfileBackend.ResolveDevice` input needs review before implementation.
- Declare backend capabilities and return diagnostics for unsupported chords,
  button modes and filters. Specify axis units/ranges, finite-value validation
  and custom-curve parameters before promising cross-game equivalence.
- Preserve unknown native settings and source evidence during same-game round
  trips. Object-valued `SimSpecific` properties deserialize JSON primitives,
  arrays and objects as `JsonElement`, not their original boxed CLR types.
  The tests document that behavior; adapters must use a deliberate extension-data
  contract rather than assume casts to `int`, `bool` or an arbitrary class work.
- Treat these mutable DTOs as experimental transfer data. Validation, ownership,
  null handling, migrations and immutable saved-state boundaries still need
  explicit contracts before production persistence.
- Keep native parsing/writing and path discovery in adapters. Plan cancellable
  imports/exports and separate generating reviewed output from deploying it.
  Preserve existing edition, manufacturer, native identity and rollback checks.

## Delivery approach

First map existing DCS semantics into the boundary and prove compatibility with
the real fixture suite. Then deliver one complete non-DCS import/edit/save/export
slice, including preserved native data, device resolution and recorded manual
verification, before adding more games. Validate each target's actual current
format and supported versions; the candidate list is not a support announcement.

Cross-game conversion should offer explicit semantic command mappings and report
unmapped or unsupported inputs. Hardware drawings remain reusable independently
of a game's command catalog and profile revisions.

The foundation tests cover actual nested JSON round trips, command contexts,
extension values and independent reconstructed collections. They do not claim
native format fidelity or in-game behavior. Every concrete adapter requires its
own fixtures, error-path tests, Windows smoke/manual acceptance and the existing
independent 80% line / 80% branch / method CRAP <= 50 gates.

## DCS compatibility baseline

`DcsSimProfileBackend` now implements the shared `ISimProfileBackend` contract.
The prototype's implicit file-location `Load`/`Save` and catalog-device
`ResolveDevice` signatures have been replaced before adoption: import accepts an
explicit context and captured relative native files; export returns a complete
in-memory plan or blocking diagnostics. Neither operation reads active game
folders, writes files, saves to SQLite, or deploys a package.

The portable document has schema/backend versions, stable profile identity,
backend and context IDs, and a JSON-valued `NativeData` envelope. The supported
DCS envelope is `dcs.v1`, bounded to 128 MiB on export. Unknown envelope members
are rejected. The earlier object-valued per-binding extension API remains for
compatibility, but DCS permits only the documented `dcs.filter` axis extension;
it is deliberately decoded through JSON, including after save/reopen.

| Concern | DCS version-one behavior |
| --- | --- |
| Command identity | Backend, module/UI context, key/axis section and native ID; display names never select commands |
| Physical identity | Exact native filename and optional DirectInput instance GUID; catalog IDs and USB product IDs are never instance GUIDs |
| Resolution | Explicit `resolved`, `disconnected`, `unresolved` or `ambiguous`; no automatic product/name fallback or entitlement claim |
| Binding scope | Separate logical device contexts for module and `UiLayer`, including the same physical controller in both |
| Chords | Explicit named modifier chords on both buttons and axes; hold/toggle modifier definitions retained in native source |
| Button mode | Native command selection on press; portable hold/toggle/double-tap/release runtime modes block export |
| Axis tuning | Deadzone in percentage points (0..100), invert, and exact sparse native filter JSON; no float conversion of untouched native doubles |
| Curves | Preserve native points, saturation, slider and detents; reject generic sensitivity/curve approximations and non-finite values |
| Native data | Untouched native files remain byte-for-byte identical, including unknown fields and ambiguous imported bindings |
| Native edits | Retain removals and changed history; retire cleared/replaced axes explicitly; block edited ambiguous inputs or unknown fields, mistyped native values, sparse array indices or nonstandard Lua syntax |
| Paths | Context-owned `Config/Input/...` paths; traversal, cross-context paths, duplicate case-equivalent names and invalid portable filename characters are rejected |

Native import needs the module's joystick/keyboard profiles and adjacent
`modifiers.lua`, with paths relative to the captured input payload. Sources can
come from the existing folder importer; add the explicit
`Config/Input/<context>/` prefix to its captured keys. The caller owns selection
and review of actual filesystem sources. Every active reformer must resolve to a
modifier in the same scope. Existing commands form the pinned catalog; editing
cannot invent a command ID or silently move it between module and UI scopes.
An untouched ambiguous native profile is archival evidence, not permission to
silently resolve a controller or edit an ambiguous binding.

For complete editor state, `Capture(workspace)` pins the existing verified
`EditorSnapshotSerializer` snapshot alongside the portable assignment projection.
`DcsSimProfileBackend.Restore(document)` validates edits through the existing
assignment and native-output services. It retains original assignment display
names, removal history, labels, artwork, shared revisions, provenance and
recovery sources. Keyboard files without an authored editor device remain
source-only; editing those requires a supported authoring flow. Device membership,
physical identity and scope changes require explicit re-import rather than an
implicit rebind. Modifier definitions and hardware layout authoring continue
through the existing DCS workflows.

This is an opt-in compatibility boundary, not a replacement database/snapshot
format or a new backend-selection screen. Existing Free/Owner policy,
manufacturer evidence, UI authoring, deployment review and rollback remain in
their existing services. Native metadata and instance-resolution results do not
grant capabilities or hardware entitlement. There is no automatic migration from
unversioned experimental DTOs and no non-DCS support announcement.

### Acceptance evidence

`DcsSimProfileBackendTests` exercises real F4U native fixtures through the shared
interface and a JSON save/reopen boundary, plus focused native edits, both
contexts, modifier modes, sparse curves, unknown fields, invalid versions/paths,
unsupported conversions, cancellation and repeated-device resolution.
`HueyReleaseAcceptanceTests` now passes its real released workspace through
capture, JSON save/reopen and restore before its SQLite, native-output,
kneeboard, deterministic-package, portable restore and rollback checks. It also
edits a module axis and clears a UI assignment through the portable projection,
checking scope isolation and unchanged original saved state. Snapshot digests and
package bytes are compared against the pre-adapter baseline.

Run the core suite and existing independent 80/80/50 gates, Windows solution and
training smoke, and release-candidate acceptance in CI. Manual in-game validation
is still required before claiming new simulator support; automated DCS fixture
and package parity is the compatibility evidence here. The first additional
game must validate its own native semantics and may require another reviewed
backend version.

## Elite Dangerous adapter (contract version 1)

`Dcs.Ipia.EliteDangerous.EliteDangerousProfileBackend` is the first non-DCS
implementation of `ISimProfileBackend`. It is a separate project in the solution,
with no dependency on the DCS parser, renderer or database. The existing shared
interface and portable domain types did not need changes for this pilot.

Import accepts one captured Elite 4.x `.binds` file, with optional `.start`
selector evidence, as a dictionary of filenames and original text. The owner's `Custom.4.2.binds` was characterized locally and is not
distributed in this repository. Public tests use an independently authored
`Synthetic.4.2.binds` fixture with invented device references; other 4.x minor
versions use the same structural reader but have not been characterized with
real fixtures. Other major versions are rejected. There are at most eight files,
each limited to 4 MiB; paths must be basenames directly inside `Bindings`, with
no traversal or case-equivalent duplicates. XML external entities and DTDs are
prohibited. The adapter never scans active folders or deploys files.

| Concern | Elite version-one behavior |
| --- | --- |
| Document | Portable schema 1, backend `elite-dangerous`, backend contract 1, native evidence `ed.v1`; original source text is pinned and revalidated on export |
| Context | Caller-selected preset context such as `controls`; a native preset includes ship, SRV, on-foot and UI commands rather than one DCS aircraft |
| Catalog | Commands come from native XML element names, including unbound slots; IDs contain backend, context and native command name; no guessed localization, missing-command inventory or cross-game equivalence |
| Slots | Each button has explicit `SimSpecific["ed.slot"]` equal to `Primary` or `Secondary`; axes use `Binding`; slot identity survives JSON save/reopen |
| Devices | Exact `Device` token and optional `DeviceIndex` are preserved in `Identity.SimSpecificIds["ed.device"]` and `["ed.deviceIndex"]`; named and numeric product references are not Windows instance IDs |
| Resolution | Imported devices remain unresolved without explicit instance evidence; repeated matching instances are ambiguous; product names, numeric tokens and ordinals never silently select hardware or grant entitlement |
| Buttons | Per-binding press/hold modes; button modifier chords reference a captured logical device and native key; unverified axis chording is blocked |
| Command toggles | Native `ToggleOn` is independent of per-binding `Hold`; editable through `profile.Metadata["ed.toggle:" + commandId]` as `"0"` or `"1"`; catalog metadata describes the captured value |
| Axes | Native deadzone fraction maps to portable percentage points (0..100); inversion supported; untouched native double precision retained; generic curve/sensitivity conversion rejected |
| Preservation | No edits returns every original string exactly; edits change selected slots/flags, retaining unrelated settings, unknown root data, duplicate scalar settings and selector text |
| Unsupported edits | Unknown data inside a rewritten slot blocks that edit; duplicate slots, removed/changed captured devices, unsafe new identities/indices, malformed values, unsupported modes/extensions and unknown command targets return blocking diagnostics with no output files |

Assignments can move between captured devices and newly added canonical numeric
HID references through `ISimDeviceEditor`. Captured logical identities remain
immutable and present even after clearing their bindings. New devices do not
rewrite original evidence, aliases or selector files; empty added controllers
persist in HEIMDALL JSON and enter native output only when referenced by a binding.
Duplicate products and invented device indices are rejected. Repeated identical
controllers must use captured indexed references with explicit unit choices.
Renaming presets, discovering a full localized command catalog and selecting
different presets for each game section remain follow-up flows. The coarse `ModifierChords` capability advertises
button chords; axis chords remain blocked by backend validation until verified. Selector files are archival evidence and
remain unchanged; they are not interpreted as a license or an active preset
selection. The adapter phase itself did not add WPF authoring. The
[game workspace shell](game-workspaces.md) now provides explicit game selection,
ED profile persistence, shared drawings and reviewed output through this API.
ED workspaces remain separate from the DCS SQLite module tables, and automatic
game deployment remains a follow-up in issue #286.

### API usage and manual acceptance

```csharp
ISimProfileBackend backend = new EliteDangerousProfileBackend();
var profile = backend.Import("controls", capturedFiles);
var saved = JsonSerializer.Serialize(profile);
var reopened = JsonSerializer.Deserialize<ProfileDocument>(saved)!;
var catalog = backend.GetCatalog(reopened);
// Edit a captured device's button/axis while retaining ed.slot identity.
var plan = backend.Export(reopened);
// Review plan.Diagnostics and plan.Files; only a successful complete plan may be written.
```

`EliteDangerousProfileBackendTests` verifies the synthetic 4.2 fixture through the
shared interface, JSON persistence and exact unchanged export. Focused tests
exercise primary/secondary isolation, clear/move/new assignments, holds,
command toggles, modifiers, exact axis precision, malformed XML, bounded sources,
unknown-field preservation/blocking, unsupported portable edits, cancellation,
null collections and conservative device resolution. The separate assembly is
included in both local and CI independent 80/80 coverage and method CRAP <= 50
gates; existing Windows solution/training smoke and release checks remain active.

In-game verification is still manual and must be recorded before announcing
end-user Elite support:

1. Close Elite and back up the entire original `Bindings` folder, including all
   `.binds` and `.start` files.
2. Import the captured file and persist/reopen the portable profile. Verify an
   unchanged export matches the original bytes before making edits.
3. Change one primary button, a modifier chord, a hold, a command toggle and an
   axis inversion/deadzone. Export into a separate review folder; inspect the
   changed native elements and confirm unrelated settings/selectors are retained.
4. With the game closed, manually copy the reviewed `.binds` file into the backed-up
   configuration's original filename. Open Elite, select the intended Custom
   preset in the relevant controls section, and verify each edited behavior and
   an unchanged secondary assignment with the actual connected hardware.
5. Close Elite, inspect its saved XML and restore the complete backup if needed.
   Do not interpret inherited Warthog references as evidence those devices are
   currently connected. No in-game result is claimed by automated fixture tests.
