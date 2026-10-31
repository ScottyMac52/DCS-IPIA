# PR6 — War Thunder native backend and shared desktop workspace (#333, #334)

PR6 now includes the native contract and desktop implementation, following the shared-workspace audit in #323. The user confirmed the backend Release build before the desktop changes. The desktop build, unit/smoke runs, measured 80/80/50 gates and in-game acceptance still need local Windows validation; no synthetic test proves game recognition.

## Ownership and reuse audit

| Concern | Existing owner to reuse | War Thunder requirement |
| --- | --- | --- |
| Profile lifecycle and persistence | `GameWorkspaceShell`, `GameWorkspaceChromeState`, `GameWorkspaceLeaveGuard`, `GameWorkspaceServices`, `GameProfileSession` | Implement `ISimProfileBackend` before opening a portable profile session |
| Optional features and action bar | `GameWorkspaceFeatureHost`, `FeatureActionBar` | Register only implemented features and actions |
| Command browsing and selected command | `GameCommandBrowserView`, `GameAssignmentSelection` | Native catalog/context/parameter projection in the adapter |
| Assignment validation and apply | `GameAssignmentEditorView`, `GameProfileSession`, native editor interfaces | A verified adapter operation must handle both Apply and diagram drops |
| Artwork and gestures | `HardwareDiagramPresenter`, `HardwareDiagramInteractions`, `DiagramViewport` | Adapter maps control keys and supplies actual select/drop/clear/tune callbacks |
| Device ownership | `ControllerLinkState`, `ControllerLinkPresentation`, session preview/apply operations | Native instance/slot identity needs evidence; display names and numeric offsets are insufficient |
| Physical live feedback | `HardwareInputFeedback` and controller discovery/capability services | Physical feedback cannot establish native game ownership by itself |
| Native output | `ISimProfileBackend.Export`, `GameReviewExport` | Lossless adapter export followed by a separate review copy; no implicit deployment |
| Native parsing | `Heimdall.WarThunder` | Keep BLK grammar, typed values, record addressing and command encoding out of WPF |

The generic `SimBackendCapabilities` record does not grant every operation. Existing native editor interfaces and registered UI callbacks remain the capability boundary. War Thunder must implement only interfaces whose native contracts it can validate.

## Implemented desktop behavior

- War Thunder appears in game cards, recent selection and all four workspace game pickers. Premium entitlement is enforced both at entry and at edit/export boundaries.
- Explicit controls-v5 BLK import creates an unsaved workspace. Save, discard, duplicate, reopen, optimistic store revisions and pending-change navigation use the shared session/store/chrome/leave guard. No blank native document is invented.
- Devices displays captured mapping occurrences with their offset/count ranges. Physical links require matching native VID/PID and explicit instance selection. Names and enumeration order never choose a physical unit. Saved physical/drawing links stay visible and locked, including offline devices, until explicitly unlinked.
- Assignments composes the common command browser and assignment editor. Captured commands have category, type, text and ownership filters. The occurrence table preserves chords, activation literals, system alternatives, unknown fields and empty blocks.
- Supported native operations stage through `ISimNativeEdit` and `GameProfileSession`: rebind a selected input occurrence, add a basic joystick alternative/input, clear a joystick-only alternative, inverse and captured typed axis parameters. Row context menus expose only implemented clear/inverse actions. Invalid operations leave the draft unchanged.
- Native device/local indices are visibly zero-based. A selected physical drawing supports inspection, zoom/pan and live raw feedback, but has no native assignment drag/drop/clear/tune callbacks until physical-index translation is verified. Table/text editing is the supported fallback. Keyboard/mouse/gamepad artwork is not invented from unsupported mappings.
- Cached Devices / Assignments & Testing / Reference sheet views use the shared feature host and action bar. Raw input handles stop on device/profile/feature switches, focus loss, save/export and close. Testing works without a drawing and does not execute game commands.
- Reference output is the complete native occurrence table, including system fields, with a tab-separated copy action. It does not claim diagram labels for unverified native axes.
- Export produces one exact BLK under a new review folder, preserving the source filename. It cannot write in the imported source directory; that protection survives saving/reopening. No automatic installation or active-game overwrite is implemented.

Native parsing, command metadata, range resolution, scalar addressing and mutation remain in `Heimdall.WarThunder`. WPF collects native indices/literals and submits adapter-owned edit requests. See the [native contract](war-thunder-native-contract.md) and [inspection schema](../../data/schemas/war-thunder/native-inspection.v1.schema.json).

## Remaining capability boundaries

The catalog covers captured/retained commands, not inherited preset contents or a complete game-wide dictionary. Physical native-index translation, fresh blank envelopes, native device roster editing, activation/modifier interpretation and unsupported tuning remain unclaimed. These limits are reflected by actual UI actions rather than fake enabled controls.

## Local validation

`WarThunderWorkspaceTests` adds native-session/persistence/link/filter/export regressions. `WarThunderWorkspaceSmoke` exercises actual WPF controls with an authored generic capture and deterministic physical inputs: import, inverse/rebind, system read-only behavior, save/discard/reopen, pending navigation, explicit physical/drawing ownership, drawing zoom, no-drawing feedback, focus/navigation disposal and export preservation. Existing DCS selection smoke now expects four games. Fixtures contain no personal configuration.

```powershell
dotnet build -c Release
dotnet test tests/Heimdall.Tests/Heimdall.Tests.csproj -c Release --filter "FullyQualifiedName~WarThunder"
.\tests\smoke-tests.ps1
.\tests\coverage-test-gates.ps1
.\tests\crap-tests.ps1
.\tests\generate-test-reports.ps1 -NoOpen
```

GitHub Actions must not run for these updates; commits use `[skip ci]`. The authoring environment has no .NET SDK or Windows runtime. Python/source checks do not substitute for the commands above. After they pass, import a real game export, edit a supported native binding, save/export a review copy, load that copy explicitly in War Thunder and verify the intended command and unrelated settings in-game.

## Shared presentation correction

The first desktop implementation reused primitives but introduced a divergent War Thunder layout. The correction centralizes the actual presentation:

- `GameWorkspaceSplit` owns the assignment-pane geometry and accessible divider for DCS, Elite, Squadrons and War Thunder. `GameControllerWorkspace` owns the game controller sidebar geometry.
- `GameFeatureNavigation` supplies the same selectable feature tabs to all three game windows; War Thunder no longer implements navigation as independent buttons.
- `GameDrawingView` supplies the same zoom controls, direct canvas scroll surface, drawing hint and read-only raw-input fallback to Elite, Squadrons and War Thunder. Existing simulator drawing interactions and lifecycle ownership remain in their consumers.
- `GameControllerSetup` supplies the common setup disclosure used by Squadrons and War Thunder. Native setup fields remain capability-specific.
- War Thunder now follows the existing profile toolbar/settings disclosure, drawing-first assignment view, adjacent shared selection editor and collapsed captured diagnostics. Native assignment actions route through the existing feature action bar, including stable ordering for composed action groups.
- Native input ownership, offset arithmetic and inverse-flag interpretation live in `Heimdall.WarThunder`, rather than the WPF editor.

`SharedWorkspacePresentationSmoke` checks actual component reuse across the four assignment consumers, shared drawing viewport contracts, native editor slots, collapsed War Thunder diagnostics and feature-action routing/order. The existing Windows simulator smoke remains required; this environment cannot run WPF or .NET checks.

### Drawing wheel regression

The shared drawing view now bounds its scroll host to the arranged drawing pane, including when hosted in an Expander. Without this bound, excess image height could be clipped without becoming vertical scroll extent. This replaces the Squadrons-window-only bound and gives War Thunder the same behavior without a separate wheel implementation. The existing Ctrl zoom / Shift horizontal / plain vertical gesture handler is unchanged. War Thunder smoke uses an oversized drawing and checks actual routed wheel movement down/up at normal and minimum window sizes, plus reachable horizontal extent. Local Windows execution remains required.

### Physical binding import and assignment completion

The initial renderer deliberately supplied empty labels and inspection-only handlers; native records were retained but not connected to physical callouts. This is replaced by `WarThunderPhysicalMap` and `WarThunderDrawingProjection`. Native range reordering remains disabled: no axis/button offsets or unrelated references are renumbered.

The adapter validates an explicitly linked product's saved counts against fresh DirectInput objects. Axes follow original enumeration order; physical buttons use native button instances; each POV contributes four cardinal buttons and two virtual axes to the saved counts. Cardinal POV controls support their native button entries or the horizontal/vertical virtual axis according to the selected command kind; diagonals retain native-table fallback. Incomplete/changed count evidence blocks physical assignment with an explanation. Original enumeration is retained separately from the common sorted capability/key list, so alphabetical or device order never becomes a native index.

Imported joystick records now label supported callouts. Command drag/drop and pick-control/Apply use one adapter-owned assignment factory and the shared session boundary. Simple joystick records rebind in place, while mixed/chord records and keyboard alternatives are preserved by adding a separate joystick alternative. Native-index Apply also supports an unassigned captured command. Saved assignments have a visible count and an explicit table action. Controller rescans refresh mapping evidence without replacing a draft.

Native evidence: Gaijin's published [DirectInput enumeration](https://github.com/GaijinEntertainment/DagorEngine/blob/main/prog/engine/drv/hid_joystick/joy_enumdev.cpp) and [joystick decoding/counts](https://github.com/GaijinEntertainment/DagorEngine/blob/main/prog/engine/drv/hid_joystick/joy_device.cpp), observed blob IDs `60c3820c882ebae0b8c195b59635cfaab61fd5fa` and `cd455edfc197cff055a9caac4090a3b493fbb47c`. This implements the published ordinary DirectInput layout with saved-count validation; remapped gamepad layouts are not claimed. In-game acceptance remains local.

`WarThunderPhysicalMappingTests` covers raw axis order, nonzero saved offsets, POV expansion, missing/changed evidence, imported labels, common assignment planning and chord/system preservation. Windows smoke exercises a physical callout drop and public Apply action. The command drag source is shared with Elite. No personal source capture is published. These .NET/Windows checks remain unrun in the authoring environment.
