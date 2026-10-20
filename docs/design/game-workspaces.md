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
2. On **Devices**, choose the connected Windows controller and **Add controller…**.
   IPIA reuses an unambiguous compatible ED reference, or adds a new numeric
   reference for a supported HID product not already represented in the preset.
   You do not need to create a binding in Elite first for a single unique product.
   Newly added empty controllers remain in the IPIA workspace; they appear in a
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
   bind an input on each in Elite and reimport the indexed preset first. IPIA
   cannot safely invent that ordering. A reference already linked to another
   connected unit cannot be overwritten. Offline replacement requires an explicit
   reference choice. Existing native aliases and targets are never renamed by
   linking a controller or changing its artwork.
3. Use **Assignments** to choose a command and click an illustrated control, or
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
5. **Save** stores an IPIA game workspace. **Discard** restores the last saved
   document. Changing game/profile or closing prompts save/discard/cancel, and a
   failed save retains the pending draft. Native preset names are retained;
   changing the IPIA profile name does not rename the game's preset.
6. **Export for review** requires saved state and creates a new child folder in
   the selected destination. Previous exports are not overwritten. The known
   active Bindings folder and symbolic-link destinations are rejected. There is
   no Elite automatic deployment/Fly Now path.
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

8. **Input Testing** is an Owner feature, requires the explicitly associated
   connected instance, and stops on focus loss, navigation or edits. It highlights
   raw controller feedback; native Elite command execution, hold timing and
   in-game behavior still require manual verification.

### Viewing ED references while playing

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
IPIA does not install a viewer, alter the VR runtime or claim a non-VR ED overlay.
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
in the selected IPIA user-data profile; `selection.json` remembers the latest
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

In Elite **Input Testing**, a controller without a displayed drawing (including
an explicit **No drawing** choice) shows a read-only text area in the drawing
space. **Start input test** shows the linked controller's Windows name and exact
instance GUID, followed by the latest twenty raw button/hat presses and releases
and axis movements. Axis values range from zero to one; changes of 1.5 percent
or less are filtered against the last reported value, so slow movement still
accumulates. Neutral axes establish their baseline without flooding the display.

This uses the same Owner-only, exact-controller input acquisition as illustrated
testing. Stop, focus loss, controller/profile/view changes and disconnect errors
release the source and clear its text. The area is hidden outside Input Testing
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

