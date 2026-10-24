# Game selection and Elite Dangerous workspace

The opening screen offers **DCS World** and **Elite Dangerous** cards, plus
**Continue** for the last explicitly selected game/profile. The same theme and
per-surface background settings apply throughout. No game or controller is
selected by guessing an installed folder or a device name.

## DCS World

Choose **Select aircraft** to reach the existing module workflow. SQLite modules,
UI Layer defaults, training, hardware management, Fly Now, packages, kneeboards
and recovery continue through their existing services. Hardware setup is offered
when entering DCS, rather than forcing DCS destination setup before choosing a
game. The game and aircraft dropdowns inside the DCS workspace provide a compact
way to switch. Pending edits prompt **save / discard / cancel** first; save
failure or cancellation retains the current workspace.

## Elite Dangerous

1. Choose **Select controls profile**, then select a saved profile or **Import
   bindings**. Select an existing `.binds` file from your Windows Bindings folder.
   Import reads that file only and retains the native preset/version/source text.
2. To identify an unknown controller ID such as `06A30BAC`, open **Devices**,
   select that reference under **Controllers in profile**, and click
   **Identify Controller**. Press a button, move a hat, or move an axis noticeably
   on your hardware. No prior association, controller selection or drawing is
   required. HEIMDALL listens to all eligible connected controllers and shows the
   device name, exact instance GUID, input key and whether the native product
   matches the selected ED reference. A mismatching product cannot be confirmed.
   **Confirm controller link** stages the explicit association; identification
   alone never changes the profile or assignments. **Save** persists the link.

   Identification is an Owner live-input feature. Initial held switches and
   neutral axis positions establish baselines; new presses and axis changes
   exceeding ten percent count as deliberate activity. Simultaneous activity
   asks you to operate only one controller. Stop, focus loss, device/profile/view
   changes, rescan, disconnect and window close invalidate the pending result
   and release input handles. Start identification again to confirm afterward.

   Identical products can share an ED token. Input proves which physical unit
   you operated, but does not prove its native `DeviceIndex`; verify the
   explicitly selected indexed reference in Elite before confirming. Existing
   exact-instance and occupied-reference checks still apply.

   To add hardware directly, choose the connected Windows controller and **Add controller…**.
   HEIMDALL reuses an unambiguous compatible ED reference, or adds a new numeric
   reference for a supported HID product not already represented in the preset.
   You do not need to create a binding in Elite first for a single unique product.
   **Remove selected…** previews every affected button/axis and cross-device chord before staging deletion. A chord requiring a removed controller is cleared in full; its modifier is never silently dropped. Keyboard and mouse system references are protected. Cancel leaves the workspace unchanged.

   **Replace selected…** uses the controller selected on the left as the source and the connected controller as the replacement. The review lists every retained/transferred and cleared binding. Native capability probing checks the replacement's available buttons, hats and axes; unsupported required keys clear the entire binding. Native key names are retained, not guessed equivalents. Compatible cross-device modifier references move too. Replacement probing uses the existing Owner live-input boundary. Drawings are optional and do not establish compatibility.

   If identical products have several captured references, explicitly choose **Replacement ED reference and index**. Windows connection order is never used as an ED index. A reference already linked to another connected unit cannot be taken over. No new index is invented.

   Preview releases native handles before opening the review. Applying rechecks the workspace revision, physical identity, connection, capabilities, edition access and native output; a stale review, failed probe, invalid extension or disconnect leaves authoring unchanged. Save/Discard and workspace reopen retain the usual transactional behavior.

   Newly added empty controllers remain in the HEIMDALL workspace; they appear in a
   native `.binds` export once a command or modifier actually references them.
   Re-adding the same physical unit selects its existing entry.

   An exact unique catalog drawing is selected automatically. Unknown devices or
   alternate drawings remain without artwork; **Change drawing…** lets you choose
   a shared illustration or **No drawing** independently. Owner authoring and
   native export work without artwork, using native input keys and the assignment
   table. Illustrated callouts/reference PNGs require a selected drawing. Artwork
   names never prove manufacturer eligibility or an attached grip.

   If multiple ED references match, choose the captured reference explicitly.
   Native `DeviceIndex` values are preserved; Windows discovery order is never
   converted into an ED index. For identical products without captured indices,
   bind an input on each in Elite and reimport the indexed preset first. HEIMDALL
   cannot safely invent that ordering. A reference already linked to another
   connected unit cannot be overwritten. Offline replacement requires an explicit
   reference choice. Existing native aliases and targets are never renamed by
   linking a controller or changing its artwork.
3. Use **Assignments & Testing** to choose a command and click an illustrated control, or
   drag the command onto a compatible callout. Primary and secondary slots are
   separate. The editor supports native input keys, press/hold, cross-device
   button modifiers, inversion and percentage deadzone. A command's native
   **Toggle** is separate from per-binding **Hold**. Right-click a callout to
   select/tune its input or clear its selected assignment. The assignment table
   remains available for captured inputs without a matching drawing. Use Fit or
   the shared Zoom control to read dense or multi-view drawings; manual zoom and
   scroll do not change assignment geometry.
4. Choose **All**, **Ship**, **SRV**, **On Foot** or **General** to filter commands.
   Within a section, use **Category** for groups such as Flight Rotation,
   Targeting, Weapons, Power Management, Driving or On Foot Movement, and
   **Input type** for **All types**, **Buttons** or **Axes**. Categories, type and
   search intersect. Search matches display names, native IDs and categories;
   the list shows category headings and a matching-command count. Categories are
   display hints based on known native command families. Unfamiliar commands
   remain under **Other**, with unknown activities under General. Input type
   comes from native assignment structure, including unbound commands.
   **Clear filters** resets category, type and search within the current section.
   Changing section resets a category only if it is unavailable there. Palette
   filters do not hide the section's existing assignment rows or diagram labels;
   selecting an assignment reveals its command and clears conflicting palette
   filters. No filter changes command identities, assignments or native files.
5. **Save** stores an HEIMDALL game workspace. **Discard** restores the last saved
   document. Changing game/profile or closing prompts save/discard/cancel, and a
   failed save retains the pending draft. Imported native sources are retained;
   the saved HEIMDALL profile name becomes the exported preset name and basename.
6. **Export** requires saved state and writes the named preset into the detected
   or configured Bindings directory. Existing same-named presets require confirmation
   and receive an exact backup. **Export to a separate review folder** creates a new
   child folder outside active Bindings for manual installation instead. See export
   destinations below. Export does not start Elite or alter preset selectors.
7. **Reference Sheets** opens a dedicated paginated print preview. Select one
   controller on the left and **All**, **Ship**, **SRV**, **On Foot** or **General**
   above. Choose **Artwork + assignment table** or **Assignment table**, A4 or
   Letter, and portrait or landscape. **Only assigned commands** is enabled by
   default; disabling it includes captured commands unassigned on this controller.
   Table entries retain primary/secondary slots, press/hold, toggles, native
   modifier device/index references, axis inversion and deadzone.

   Artwork uses the shared drawing and includes a complete assignment-table
   appendix, including inputs without matching illustrated callouts. A controller
   without visible artwork automatically uses the table layout. Page navigation
   shows the exact PNG pages backing the complete PDF. Long rows continue onto
   additional pages instead of being truncated. Profile, controller, section and
   page number identify every page. **Export PDF…** saves all pages for this
   controller; **Export page PNG…** saves only the currently previewed page at
   144 DPI. Paper/table output is opaque white for printing; artwork retains its
   existing colors and opacity. Palette text/category/type filters do not filter
   reference content. Preview choices do not change native assignments or save
   state, and existing edition checks apply before preview/export.

8. **Assignments & Testing** is an Owner feature, requires the explicitly associated
   connected instance, and stops on focus loss, navigation or edits. It highlights
   raw controller feedback; native Elite command execution, hold timing and
   in-game behavior still require manual verification.

#### Native ED compatibility when changing controllers

ED device references are carried by binding slots and modifier nodes, not a separately exported controller table. A stored reference need not belong to a currently connected device. Changing hardware is valid when the exported slots use real device identities, captured indices where required, supported native keys and consistent modifier references. Empty added controllers appear in a `.binds` file only once they own or modify a binding. XML validation/export-reimport tests do not replace testing the result in ED.

HEIMDALL retains the preset name/version, selector bytes, unknown settings and unrelated bindings. An unmodeled native reference to a removed controller blocks export rather than leaving an obsolete reference behind. The adapter's original native evidence is retained for reproducibility; the editable profile and exported XML carry the reviewed change.

Export to a review folder, close ED before installing the reviewed preset, back up the existing Bindings directory, then load and test the preset in ED. Check `BindingLoadingErrors.log` if ED refuses it. Frontier documents [backing up game settings and keybinds](https://customersupport.frontier.co.uk/hc/en-us/articles/25580471182620-Back-up-your-game-settings-and-keybinds); community-maintained [EDCD input documentation](https://github.com/EDCD/EliteCustomButtonNames) describes native device naming. Save in HEIMDALL does not modify the live ED preset.

## Organizing and navigating the ED workspace

The feature navigation has **Devices**, **Assignments & Testing** and **Reference Sheets**.
Each owns a separate view and supplies the shared action bar, matching the DCS shell.
Assignments and raw input feedback share the same drawing/table surface. **Apply assignment**,
**Clear slot**, **Start input test**, **Stop** and **Fit** are available from its action bar.
Leaving that view stops input acquisition. A missing drawing uses the raw activity text fallback;
testing reports physical inputs and does not emulate Elite commands.

Drag a controller above or below another under **Controllers in profile**. **Move up** / **Move down**
and **Ctrl+Up** / **Ctrl+Down** provide keyboard alternatives. Selection stays on the same native reference.
Save persists this organization; Discard restores saved order. Older workspaces use native reference order.
New references append, removed references disappear and a replacement inherits the old entry's position.
`ControllerOrder` belongs to the HEIMDALL workspace envelope only: native device order, identities,
`DeviceIndex`, assignments and `.binds` / selector output are unaffected. Ordering does not grant editing,
export, controller probing or edition access.

Elite button combinations are native per-binding `<Modifier>` requirements, including keys/buttons from
another controller. They are not DCS global modifier/switch layers. **Button combination (optional)**
expands in the assignment editor and opens automatically for an existing combination. Existing combinations
survive edits, Save/Discard and export. The assignment table retains combination details for review, while
drawing and reference artwork show the resulting command names without a raw chord suffix or separate
modifier-layer appearance. Slot and Hold hints remain visible. Reference tables retain full input details.

`GameControllerOrderTests` checks persistence, legacy envelopes, no-op/invalid moves, lifecycle changes and
byte-identical raw/named exports. `EliteWorkspaceEnhancementsSmoke` checks real feature/action switching,
window-scoped drop validation, selected-reference retention, move controls and combination editing on Windows.

## Viewing ED references while playing

These are portable documents, not deployment into a native Elite kneeboard
folder. Print a PDF, use another screen, or load exported pages into a compatible
third-party viewer. OpenKneeboard's
[game compatibility table](https://openkneeboard.com/compatibility/games/)
lists Elite Dangerous support through **SteamVR/OpenVR**, but lists the ED
**non-VR** and legacy Oculus overlays as unsupported; ED has no native OpenXR
mode. OpenComposite is reported to work, but OpenKneeboard does not provide
support for that combination. Check those runtime requirements before choosing a
VR viewer. The [OpenKneeboard FAQ](https://openkneeboard.com/faq/) recommends PDF
for document text and keeping existing images in their original image format.
HEIMDALL does not install a viewer, alter the VR runtime or claim a non-VR ED overlay.
Multiple-controller reference packs remain follow-up work.


Unknown data inside an edited native assignment, unsupported curves/modes,
unverified axis chords or malformed values block the operation. Failed edits
are staged on an independent copy and never replace the current or saved state.
Imported devices remain unresolved until explicitly linked; a named Warthog
reference or numeric Device/DeviceIndex token is not evidence of a connected unit.

## Edition and persistence boundaries

Free Edition retains its reviewed hardware allowlist and native manufacturer
checks. An imported ED preset cannot grant hardware eligibility, Owner access,
training scope or live-input permission. Until every physical reference has a
reviewed eligible association matching its native product target, the imported
data is retained read-only in Free. An unrelated eligible controller or a forged
association cannot authorize exporting references to another product.
A controller with no reviewed catalog choice remains read-only in Free; omitting
artwork does not expand the Free hardware allowlist. Once reviewed, **No drawing**
hides the illustration while retaining the catalog identity and eligibility.
Owner can author supported
HID controllers without a drawing. Keyboard/mouse tokens are system inputs.
Previously verified associations remain
usable offline; conflicting current native observations block authoring/export.

Game workspaces are stored under `GameWorkspaces/<workspace-id>.ipia-game.json`
in the selected HEIMDALL user-data profile; `selection.json` remembers the latest
explicit game/profile. The envelope is versioned and bounded, writes are atomic,
and a stale revision fails before replacement. Game state does not enter the DCS
module tables. Existing full-user-data backup/restore includes this directory;
the regression suite verifies an actual SQLite/profile backup and restore.
Malformed workspace records are retained for recovery, and preference failures
do not turn a successful profile save into a failed save.

This first UI integration uses the bundled shared hardware catalog. Custom drawing
adoption/revision management continues through the existing DCS workflows and is
not yet an ED authoring flow. Active-game deployment, multiple section-specific
presets in one ED workspace and a localized complete command catalog remain
follow-up work in [issue #286](https://github.com/ScottyMac52/DCS-IPIA/issues/286).
The adapter's format/capability limits are documented in
[multi-sim profiles](multi-sim-profiles.md).

## Input testing without a drawing

In Elite **Assignments & Testing**, a controller without a displayed drawing (including
an explicit **No drawing** choice) shows a read-only text area in the drawing
space. **Start input test** shows the linked controller's Windows name and exact
instance GUID, followed by the latest twenty raw button/hat presses and releases
and axis movements. Axis values range from zero to one; changes of 1.5 percent
or less are filtered against the last reported value, so slow movement still
accumulates. Neutral axes establish their baseline without flooding the display.

This uses the same Owner-only, exact-controller input acquisition as illustrated
testing. Stop, focus loss, controller/profile/view changes and disconnect errors
release the source and clear its text. The area is hidden outside Assignments & Testing
and whenever a drawing is displayed. No additional association is required;
activity does not assign controls or emulate Elite command execution.

## Acceptance

`GameWorkspaceTests` covers JSON save/reopen, exact native evidence, immutable
snapshots, rejected edits, stale saves, manufacturer/edition checks, isolated
review output and full-profile backup/restore. `EliteWorkspaceProjectionTests`
covers physical input translation, section hints and reference labels.
`EliteControllerAdditionTests` covers optional artwork, unique reference creation,
new button/axis/chord native round trips, persisted empty-capable references,
immutable captured identities, explicit indexed units, rejected ambiguous or
unsupported additions, atomic failures and Free eligibility.
`GameWorkspaceSmoke` operates the actual WPF cards and ED window on Windows:
import, single-choice controller addition, optional/automatic drawings, indexed
reference choices, button/axis edits, save, reopen, export,
dedicated PDF/PNG reference previews, table fallback, page navigation and paper\norientation, combined category/type/search filtering, empty results, filtered assignment
selection, save/discard/cancel transitions, drawing-independent input activity, exact-device filtering, focus/stop cleanup,
input acquisition/release and blocked Free
commands. CI attaches game-selection and ED-workspace screenshots to the
`custom-mapping-preview` artifact. Existing DCS/training and release smoke remain
active, with independent production 80% line / 80% branch / CRAP <= 50 gates.

Before announcing verified in-game ED support, back up the entire real Bindings
folder, import a personal preset, export into a review folder and manually verify
primary/secondary assignments, a chord, hold/toggle behavior and axis tuning in
Elite. Record the game version and results; restore the backup if needed. CI does
not launch Elite or claim that manual acceptance has been completed.



## ED export preflight and preset identity

Workspace saving retains editable native evidence, even with disconnected hardware. Reference sheets and format-only validation remain available offline where edition access permits. **Export** additionally checks every native `Device` field against fresh connected-device observations, including modifier-only dependencies and opaque native extensions. An unresolved reference lists the affected commands, slots and keys before any export folder is created. Choose a connected replacement, clear all affected assignments, or cancel. Each repair uses the existing binding-impact review; missing modifier inputs clear an entire chord. Save explicitly reviewed repairs and export again. No files are written while references remain unresolved.

A unique native HID product can resolve without artwork. Repeated products require captured ED `DeviceIndex` values and explicit physical-instance links. Disconnected/changed pins, duplicate instance observations and conflicting links block export. Windows discovery order never supplies ED indices. Unsupported aliases or opaque native references are reported without inventing matches; uneditable fields must be repaired in ED or in the source and reimported. Replacement input probing retains its Owner-only boundary.

The saved HEIMDALL workspace name becomes both the native `PresetName` and `<name>.4.<minor>.binds` basename. Names must be valid Windows basenames (1–100 characters); invalid names are reported rather than silently sanitized. Matching lines in captured `.start` selectors change to the new preset name; unrelated section selections and line endings remain intact. Original imported sources remain unchanged. Game export collapses exact duplicate scalar root settings such as identical `MouseGUI` entries and reports the normalization. Conflicting duplicate values block import/export; duplicate command structures remain unsupported.

The status area distinguishes saving in HEIMDALL, direct preset export and a separate review copy. Close ED before direct export, then restart ED and select the exported name. Separate review copies require manual installation. Review any captured selector separately before copying it so unrelated section choices are preserved. Preflight does not launch the game or certify actual in-game behavior.

## ED action states, assignment status and export destinations

ED shell and feature actions use the shared `ToolbarActionButton` resource: 176 pixels wide,
30 pixels high, with consistent alignment and spacing. DCS keeps its existing toolbar style.

Apply requires an editable controller/command, a valid draft and a native output change.
Clear requires a binding in the selected controller's selected slot, even when the draft key or
axis settings are invalid. Review validates a copy through the same backend used for Apply;
assignment and command-toggle changes publish atomically. Unchanged drafts stay disabled.
Start requires a linked connected exact instance/product and existing Owner live-input access;
no drawing or selected command is required. Start/Stop are mutually exclusive. During testing,
fields remain editable, while Apply/Clear wait for Stop. Stop remains available while testing,
including during a busy operation. Busy Save/Export operations disable Apply/Clear/Start.
Focus loss, navigation, disconnect/read failure and Stop release handles and update availability.
Idle controller connection readiness is refreshed once per second while the window is active.

The command **Assignment status** filter offers:

| Choice | Ownership scope |
| --- | --- |
| All | Every matching command |
| Assigned Only | A primary, secondary or axis binding on the selected controller |
| Not Assigned | No binding on the selected controller, including commands assigned elsewhere |
| Not Assigned by Any | No binding anywhere in the complete profile |
| Assigned by Any | A binding anywhere, including keyboard/mouse |

Combination-only references do not own a command. Whole-profile ownership is evaluated before
intersecting section, category, input type and text filters. Clear filters resets status to All.
Selecting an assignment hidden by command filters clears those filters to reveal it. Filtering
never changes native data or output.

**Export** defaults to `%LOCALAPPDATA%\Frontier Developments\Elite Dangerous\Options\Bindings`.
Expand **Export destination** to see the resolved folder, **Browse…** for an optional remembered
per-user override, or **Reset to default**. The same folder is the import dialog's starting point.
This preference is independent of profile Save/Discard and older selections need no migration.

Direct export writes only the named `.binds` file after fresh device/native validation. Close ED
before exporting. An existing same-named preset requires confirmation; replacement atomically
preserves its exact original bytes as `<preset>.binds.backup-<unique-id>`, which does not end in
`.binds`. Changes to destination bytes after confirmation invalidate the prepared write. Unrelated
presets and `.start` selectors are retained; restart ED and select the exported preset manually.
Temporary output is cleaned on failure. Linked/symlinked destinations are rejected.

Enable **Export to a separate review folder** for the previous isolated review-copy workflow,
including captured selector output for manual review. That mode still cannot write inside either
the default or configured active bindings directory. Both modes require saved profile edits and
fresh controller preflight before writing. Selecting the default directory does not auto-export.

`GameAssignmentReviewTests`, `EliteAssignmentStatusTests` and `GameBindingsExportTests` cover
state rules, draft semantics/atomicity, ownership scope/intersections, exact backup bytes,
stale destination detection, preferences and failure paths. `EliteWorkspacePolishSmoke` operates
the actual WPF buttons, status filter, physical test states, uniform toolbar and direct export
against temporary fixture directories.


## Keyboard and mouse drawings

Native `Keyboard` and `Mouse` entries automatically have built-in vector drawings in
Assignments & Testing and Reference Sheets. They do not require hardware discovery,
a drawing selection, an image file, or a physical controller pin. Other hardware retains
its existing shared/custom drawing choice. System artwork does not enable DirectInput
acquisition or make system references removable.

The keyboard uses a generic US QWERTY layout with function keys, left/right modifiers,
navigation, arrows and keypad. It is a reference layout, not automatic detection of the
user's physical keyboard or regional layout. Mouse artwork exposes the left, right and
middle buttons and native positive/negative wheel inputs. Captured extra keys, side
buttons and mouse axes appear as additional exact-name controls; no joystick aliases or
native input names are invented for imported assignments.

Click a key/button to select its native assignment or prepare a draft; command drag/drop
and the existing context menu use the same path as controller diagrams. Tooltips show
complete current command names and combinations. Assigned command names wrap directly inside each key/button, below its input caption.
Every applicable command on the same input is shown, including Secondary/Hold labels;
empty inputs retain their caption without invented assignments. Keyboard cells provide
additional vertical space, and the complete cell remains clickable. The full reference
table retains Primary/Secondary, combinations, hold/toggle and axis details. Section
filters apply to displayed commands; unknown inputs remain visible in the additional
input area and table. Assignment and reference previews share the same geometry.

The Assignment editor also displays the currently selected command name directly above
the native input key. It follows diagram clicks, assignment-table selection and catalog
selection on every device, wraps long names and shows "No command selected." when the
selection is cleared. Viewing a binding leaves the native profile unchanged.

Button combination input choices follow the device selected inside the combination
editor, independently of the controller receiving the command. The editable dropdown
shows authored default captions, explicit button numbers and exact native keys; these
are hardware captions rather than currently assigned command names. It includes
unassigned drawing buttons and hats, keyboard/mouse keys, and captured combination
inputs. Axes and other devices' inputs are excluded. Without a known drawing, only
captured native inputs are listed; exact keys can still be entered manually. Hiding
artwork retains the reviewed hardware choices. Changing the combination source clears
the pending key, and export stores only the native device/key, never display captions.

The main Native input key editor uses the same editable device-input choices and
default captions. Its source is the selected controller in the profile; its input kind
follows the selected command (buttons/hats/keys or axes). Diagram/table selection loads
the corresponding choice, and Apply writes its exact native key. Captured inputs remain
available without a drawing, and manual entry still accepts exact keys. Device or
command changes rebuild the list and load the applicable binding rather than retaining
another controller's pending choice.

Manual-entry review: the ED workspace's remaining editable text fields are command
search, configuration names and numeric deadzone values. The export directory already
has a folder picker. Further dropdown candidates outside this workspace are the advanced
DCS/UI Layer editor's binding input, modifier input and modifier chord, plus editable
category suggestions in its command catalog and the module command catalog. Names,
labels, command-code authoring, paths and numeric values still need free-form entry.

The same command-label projection serves physical sticks, throttles, rudders and MFDs:
assigned buttons, hats and axes display their commands in authored callouts. Multiple
commands share the callout, section filters apply, and unassigned controls have no
assignment label. Physical artwork retains its authored geometry and full hit areas.

Both table-only and Artwork + assignment table layouts remain available, with the
existing PNG/PDF export, pagination, section filters and background/opacity behavior.
All drawing and reference operations leave native evidence and hardware links intact.
`EliteSystemInputDrawingTests` covers native names, geometry, escaping, complete command labels,
caption separation, rendering and isolation; `EliteSystemInputSmoke` exercises keyboard/mouse
selection, actual diagram clicks, assignment application and illustrated reference exports.
`EliteWorkspaceProjectionTests` also covers physical controller callouts for buttons,
hats, axes, multiple commands, Secondary/Hold labels and section filtering.

Native naming evidence: captured ED keyboard/mouse defaults archived at
https://github.com/richardbuckle/EDRefCard/blob/dev/bindings/Defaults%20ODY%20patch%208/KeyboardMouseOnly.binds
and its key-name catalog. The artwork and implementation are authored here; no third-party
artwork or code is bundled.

## Named ED configurations

The profile selector holds independent saved configurations with their own identities,
assignments, controller links, drawing choices and controller display order. Names are
case-insensitively unique within the game and use the same Windows preset-name contract
as native export: 1–100 characters, spaces allowed, no reserved names or path characters.
Existing documents still load; saving asks users to correct an invalid or conflicting name.

- **Add / Import…** captures an existing `.binds` preset into a new unsaved configuration.
  Reimporting a saved name suggests a numbered name rather than replacing its saved identity.
- **New configuration…** creates and saves a named variant with all authored button/axis
  assignments and their combinations cleared. It retains command definitions, controller
  references, associations, display order and game settings. With no open configuration,
  choose an existing ED preset as a command template first; HEIMDALL does not invent a
  game-version-specific catalog. Pending edits use the existing Save/Discard/Cancel flow.
- **Duplicate…** saves an independent named copy, including current pending assignment
  edits. The original saved configuration is retained. Cancel or a failed save retains the
  current editing session. The copy's new name replaces any pending rename of the original.
- **Rename…** stages a validated unique name, just like editing the profile-name field.
  **Save** persists it under the same workspace identity; **Discard** restores the saved name.
- **Delete…** confirms removal of a saved HEIMDALL configuration and its pending edits.
  The exact persisted revision must still match. Cancellation, stale revisions and read
  failures retain the session. Another saved configuration opens afterwards, or the workspace
  returns to an empty state and clears the remembered selection.

Changing configurations resolves pending edits and stops live input. New and duplicate
copies are deep snapshots, including retained native evidence. Export uses the saved name
for both `PresetName` and `<name>.4.<minor>.binds`; differently named presets coexist.
Renaming/deleting in HEIMDALL leaves previously exported ED presets in place. Exported
preset replacement continues to require explicit confirmation and an exact backup.

All configuration buttons use `ToolbarActionButton`; the name dialog uses the shared
text, label and button styles. `GameConfigurationTests` covers isolated copies, cleared
native round trips, catalogs/settings, naming conflicts, independent reopen/rename and
stale deletion. `EliteConfigurationsSmoke` exercises real commands, cancellation, saved
selection, rename/Save and deleting the final configuration on Windows.


## ED device action states

Controller management uses one `GameControllerActions` availability policy and rechecks
fresh observations inside the handlers. Source buttons retain shared styles, and the
feature action bar mirrors their enabled states.

| Action | Prerequisite while Devices is idle and not busy |
| --- | --- |
| Start Identification | Selected physical native reference, Owner live-input access, eligible connected hardware |
| Stop Identification | Identification is running; available even while busy |
| Confirm Controller Link | Identification is running, one fresh matching result, legal available link/index, Owner access, not busy |
| Add Controller | Selected eligible connected instance, open profile, instance not already linked |
| Replace Selected | Selected physical native reference, different eligible connected replacement, explicit indexed choice when needed, Owner access |
| Remove Selected | Selected physical native reference; keyboard and mouse are retained |
| Rescan | No identification, input test or busy operation; available without a profile |
| Change Drawing | Selected linked physical controller; drawing remains optional |

Start, Add, Replace, Remove, Rescan and drawing changes are disabled while identifying
or testing input. A missing, mismatching or ambiguous result cannot be confirmed.
Fresh discovery is rechecked before actions and while idle on Devices; focus loss,
Stop, view/device changes, errors and disconnect invalidate the result and restore
idle states. Review dialogs use the busy state. Existing detection thresholds and
baseline behavior remain unchanged; the aborted settling-slider work is not included.

`GameControllerActionsTests` verifies the truth table, and Windows identification smoke
checks all eight source buttons, visible action commands, connected/unlinked/linked
transitions, conflicts raised directly against disabled handlers and lifecycle cleanup.

## Elite command binding slots

Button commands have **Primary** and **Secondary** slots shared across the entire
profile. Either input activates the command. For example, UI Select can use a
joystick button in Primary and a throttle button in Secondary. Choose the other
slot to add a second input; assigning an occupied slot replaces its current input,
even when it belongs to another device. The inspector lists both slot owners and
inputs across all devices and explains replacement before Apply. Clearing a slot
on its owning device preserves the other slot. Drag-and-drop uses the selected slot.

Axis commands have one native **Binding** slot. Their inspector shows that slot
and axis tuning rather than offering Primary/Secondary or button press/hold mode.
Keyboard bindings count toward the same two button slots; there is no third slot
per controller. Import, save/reopen, and export retain each slot's device identity,
button combinations and supported mode independently.
