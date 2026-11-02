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

The adapter validates an explicitly linked product's saved counts against fresh DirectInput objects. Axes follow original enumeration order; native button slots follow original enumeration while drawing aliases retain the source button instances; each POV contributes four cardinal buttons and two virtual axes to the saved counts. Cardinal POV controls support their native button entries or the horizontal/vertical virtual axis according to the selected command kind; diagonals retain native-table fallback. Incomplete/changed count evidence blocks physical assignment with an explanation. Original enumeration is retained separately from the common sorted capability/key list, so alphabetical or device order never becomes a native index.

Imported joystick records now label supported callouts. Command drag/drop and pick-control/Apply use one adapter-owned assignment factory and the shared session boundary. Simple joystick records rebind in place, while mixed/chord records and keyboard alternatives are preserved by adding a separate joystick alternative. Native-index Apply also supports an unassigned captured command. Saved assignments have a visible count and an explicit table action. Controller rescans refresh mapping evidence without replacing a draft.

Native evidence: Gaijin's published [DirectInput enumeration](https://github.com/GaijinEntertainment/DagorEngine/blob/main/prog/engine/drv/hid_joystick/joy_enumdev.cpp) and [joystick decoding/counts](https://github.com/GaijinEntertainment/DagorEngine/blob/main/prog/engine/drv/hid_joystick/joy_device.cpp), observed blob IDs `60c3820c882ebae0b8c195b59635cfaab61fd5fa` and `cd455edfc197cff055a9caac4090a3b493fbb47c`. This implements the published ordinary DirectInput layout with saved-count validation; remapped gamepad layouts are not claimed. In-game acceptance remains local.

`WarThunderPhysicalMappingTests` covers raw axis order, nonzero saved offsets, POV expansion, missing/changed evidence, imported labels, common assignment planning and chord/system preservation. Windows smoke exercises a physical callout drop and public Apply action. The command drag source is shared with Elite. No personal source capture is published. These .NET/Windows checks remain unrun in the authoring environment.

### Empty startup/filter recovery and retained operation errors

The indexed fallback selection is guarded when the captured command list is empty, and refresh suppression is restored in `finally`. An empty filter clears stale record/editor selection and can be cleared without leaving the UI stuck. This retains indexed selection for nonempty lists while handling no-profile startup normally.

War Thunder input notices now have their own status line. Caught operation failures retain copyable full exception details in a separate disclosure, so focus changes cannot erase the diagnostic. Shared input feedback reports focus loss only when acquisition was actually running. Windows smoke covers empty startup, no-match search and recovery, and diagnostic retention across focus loss. Local Windows execution remains required.


### Independent physical input groups

Physical button/POV and axis/POV evidence now validate independently. An axis-count mismatch or unsupported axis no longer disables otherwise verified button assignments, drawing drops or Apply; an invalid button group likewise does not suppress verified axes. Missing identity, missing discovery evidence and ambiguous POV objects still block mapping. Saved native ranges and ownership checks remain enforced without guessing unsupported ordinals.

Devices and drawing feedback show verified target counts and the exact blocked group, including saved BLK counts versus discovered axes/buttons/POVs. Discovery errors remain visible, and physical evidence can be assessed without artwork. WarThunderPhysicalMappingTests covers partial groups and native assignment preservation; the Windows workspace smoke exercises public Apply with an unsupported relative axis present. These changes still require local .NET/WPF execution and actual-device acceptance.

Dragging commands onto callouts is distinct from dragging controllers to reorder the Devices roster. Native roster reordering and rewriting all affected global references remain unimplemented and disabled.


### Callout edit feedback and context actions

Physical callouts now supply the shared presenter with assignment inspection, clear for an unambiguous single-input joystick alternative, and axis-settings navigation when the captured record supports it. Chords/system alternatives are preserved; ambiguous alternatives remain individually reviewable in the occurrence table. Context inspection and drawing labels share the adapter's native ownership lookup. The shared presenter advertises a drop only when an adapter-supplied compatibility predicate accepts the pointed control and command.

The shared command drag source retains the command under the original pointer press instead of taking whichever command happens to be selected later. War Thunder drawing edits compare profile contents and distinguish a changed staged assignment from an already-set assignment. No-op edits do not enable Save or claim a new staged change. Changed profiles refresh through the session notification and the common Save chrome.

Regression coverage now checks old/new callout SVG labels after native reassignment. Windows smoke starts with a saved profile, applies a different binding, requires Save to enable, saves it, repeats the unchanged assignment without dirtying it, invokes callout assignment inspection, checks the clear action, and changes the binding through public Apply. These new .NET/WPF checks require local execution; source checks do not establish hardware acceptance.


### Standard Clear Assignment availability

The shared hardware presenter now keeps an adapter-provided Clear action visible on an unavailable callout, disabled with a tooltip explaining why. An adapter may provide named native alternatives; when more than one is clearable, Clear Assignment opens a choice submenu. This common presentation is used by the game workspaces rather than a separate War Thunder context-menu implementation.

War Thunder no longer restricts physical clear to exactly one single-input record. Captured joystick-only chords and multiple joystick alternatives are offered using the existing backend clear capability. Clearing removes the selected complete native alternative; other alternatives, system inputs and unrelated records remain untouched. Mixed system/joystick hotkey records and opaque/nested syntax remain unsupported and receive an explanatory disabled action. Assigned axis clearing is supported as described below. Missing physical mapping evidence is reported rather than treated as an empty binding.

Windows smoke now clears a joystick chord from several alternatives sharing a physical input, checks unrelated alternatives and keyboard bindings, requires Save to enable, and verifies exact saved-state restoration through Discard. The Squadrons smoke checks the same shared disabled-action policy for an empty callout. Local Windows execution remains required for these changes.

### Native roster translation and stale physical targets

The GUID-only roster filter has been replaced by the neutral-to-native layout translator described below. Discovery retains raw native offsets; these are not canonical DIJOYSTATE2 or application-buffer offsets. The adapter applies the published driver's GUID precedence, raw-offset fallback, slider allocation and POV expansion before checking saved counts. Native layout cardinality does not depend on compatibility-supported DCS controls.

The regression roster contains eight recognized absolute axes, one extra unrecognized raw axis object and one POV, matching ten saved axes. Tests check the native ordinal, common drawing assignment factory and preservation of tuning and system alternatives. Windows smoke exercises axis drawing assignment and public Apply, dirty/Save state and exact Discard restoration. A genuinely mismatched roster still fails closed.

Selecting an unmapped callout now clears the native target/index and disables edits. Refreshes retain that blocked selection until the user explicitly chooses a different command or native record, preventing an existing binding on another controller from remaining a misleading Apply target.

### Reviewed controller removal

The shared session accepts an optional single-purpose controller-removal contract. The adapter returns the edited profile and an explicit old-to-new logical identity map; the session validates and stages the result while moving physical pins, detached drawings and presentation order with surviving units. No native file parsing or War Thunder type is introduced into the shared session.

Devices exposes Remove controller separately from physical unlinking. Review describes the selected mapping and dependent native alternatives that will be cleared, including complete mixed/chord alternatives. Native removal deletes the mapping and empties dependent command blocks. Independent alternatives, other controller ranges, global indices and other source text remain unchanged. Surviving range offsets are preserved, including gaps; this operation does not implement native roster reordering or range compaction. Opaque syntax, nested command content, malformed input fields and overlapping or invalid ranges block removal.

Tests cover preview isolation, repeated products, surviving physical/drawing association ownership, dependent and independent records, both newline conventions, final-controller removal and exact Discard. Windows smoke covers the actual Devices action and Save availability. All new .NET/WPF tests and the 80/80/50 gates require local execution; this authoring environment has no .NET SDK. Successful source checks do not establish in-game acceptance of the exported review copy.

### Axis Clear Assignment

The adapter's record capability is now named ClearAssignment and covers both joystick-only hotkey alternatives and uniquely assigned, editable axis records. The toolbar, native-record context menu and physical callout context menu consume that same capability. Clear no longer depends on the selected record being a hotkey.

Axis Clear removes only the addressed axisId scalar, using its captured typed-header span. It retains the axis block, inverse flag, tuning parameters, mouseAxisId, comments, independent alternatives and all other native source text. Removing the joystick field uses the same unassigned-axis shape already supported by axis creation; no negative sentinel or unrelated defaults are invented. Clear disables afterward and a subsequent Apply can add a new joystick axis while preserving the retained settings. Parameter-only axis records display as Unassigned when no other active input remains.

Unit tests cover exact native-text preservation for both newline styles and optional semicolons, mixed mouse input retention, reassignment, capability rejection for invalid/repeated/nested/opaque records, unassigned filters and session Discard. Windows smoke exercises both the axis callout Clear action and public toolbar Clear, checks tuning preservation, Save state, updated availability and exact Discard. The 47 Python checks pass in the authoring environment; new .NET/WPF tests, 80/80/50 gates and in-game behavior still require local execution.

### Systemic refactor stage 2: explicit native layout and drawing bridge

`WarThunderInputLayout` translates the complete driver-declared catalog into game-native local slots and an inclusion/exclusion trace. `WarThunderAxisLayout` owns GUID-first storage selection, raw-offset fallback and allocation of nonstandard-offset sliders. Native buttons retain their original enumeration positions; up to four POVs append two axis slots and four button slots each. The published ordinary driver reserves 16 of its 512 button bits for POVs, leaving a 496-button declaration limit. Remapped gamepad layouts remain outside this ordinary-layout contract.

`ControllerDrawingAliases` is an explicit downstream compatibility projection for existing artwork. `WarThunderDrawingAliases` joins those aliases to native slots by neutral object identity. This matters for sliders: the physical object's authored alias and the game's selected slider storage can differ. The alias bridge never decides how many native slots exist. An extra native entry can remain counted even when no supported authored alias exists. Native relative axes remain represented without being silently promoted to compatibility-supported drawing controls.

The captured evidence explains both reported failures. TQS has six standard axes plus an unknown-GUID object at raw offset 28, accepted by the driver's slider fallback: seven native slots. Sol-R has eight standard axes plus an unknown-GUID object at offset 36, which the driver excludes; its POV adds two virtual slots: ten native axis slots. Main TQS throttle is the ordinary Z object at local axis slot zero. Existing saved global offsets remain authoritative and are not compacted or inferred from device-list position.

A reference reconciliation against the supplied capture and previously supplied BLK matched native axis/button counts for all ten connected devices, including TQS 7/64, Sol-R 10/25, MOZA 10/132 and VKB 2/128. This is evidence analysis, not execution of the new C# implementation or an in-game acceptance result. Source captures, physical instance IDs and personal configurations are not committed.

The VKB's 128 declared button slots are not evidence of 128 physical buttons. Its existing shared drawing supplies sixteen button aliases. Native declarations, reviewed hardware layout controls and observed live activity are separate concepts; none may be silently substituted for another. Evidence report schema 2 names the roster `reportedInputs`, supplies declared-object counts, leaves physical confirmation unknown and records that live verification was not performed. The stage-1 schema-1 capture remains sufficient for this investigation; no recapture is needed solely for the terminology correction.

Devices includes a collapsed, read-only native translation trace. Normal feedback says translated native slots and available drawing aliases, rather than claiming that every driver slot is a verified physical control. Unknown, duplicate or incomplete evidence and genuine saved-count mismatches remain explicit. Native roster reordering is still not implemented.

Authored tests cover the offset-fallback and excluded-extra-axis shapes, all standard fallback offsets, GUID precedence, explicit/fallback slider storage, button enumeration versus sparse instances, driver limits, duplicate evidence and independence from the legacy supported-input list. A separate regression ensures the drawing alias is joined by reported object identity rather than guessed from native storage. Windows smoke exercises main-axis public Apply on a seven-slot roster, the trace, Save/reopen, Clear/Discard and source-preserving review export. New .NET/WPF execution, 80/80/50 and in-game acceptance remain local gates.
