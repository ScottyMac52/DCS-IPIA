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
2. On **Devices**, select a native reference, a shared hardware drawing and the
   exact connected controller, then choose **Associate**. This explicitly links
   native reference, catalog drawing and physical instance without replacing any
   of those identities. The adapter must verify native product compatibility
   against the Windows HID product GUID before association. Numeric VID/PID
   references and the reviewed built-in Warthog joystick/throttle aliases are
   supported; unknown aliases remain unresolved. A different native target must
   be rebound in Elite first. The original reference stays visible in IPIA. The same bundled drawings used in DCS are reusable here.
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
   These are conservative display hints, not new native files or cross-context
   reassignment. **All** always exposes every captured command; unfamiliar names
   remain under General. Command identities and source values remain unchanged.
5. **Save** stores an IPIA game workspace. **Discard** restores the last saved
   document. Changing game/profile or closing prompts save/discard/cancel, and a
   failed save retains the pending draft. Native preset names are retained;
   changing the IPIA profile name does not rename the game's preset.
6. **Export for review** requires saved state and creates a new child folder in
   the selected destination. Previous exports are not overwritten. The known
   active Bindings folder and symbolic-link destinations are rejected. There is
   no Elite automatic deployment/Fly Now path.
7. **Reference Sheets** exports a PNG using the same shared artwork and current
   assignment projection. **Input Testing** is an Owner feature, requires the
   explicitly associated connected instance, and stops on focus loss, navigation
   or edits. It highlights raw controller feedback; native Elite command
   execution, hold timing and in-game behavior still require manual verification.

Unknown data inside an edited native assignment, unsupported curves/modes,
unverified axis chords or malformed values block the operation. Failed edits
are staged on an independent copy and never replace the current or saved state.
Imported devices remain unresolved until explicitly associated; a named Warthog
reference or numeric Device/DeviceIndex token is not evidence of a connected unit.

## Edition and persistence boundaries

Free Edition retains its reviewed hardware allowlist and native manufacturer
checks. An imported ED preset cannot grant hardware eligibility, Owner access,
training scope or live-input permission. Until every physical reference has a
reviewed eligible association matching its native product target, the imported
data is retained read-only in Free. An unrelated eligible controller or a forged
association cannot authorize exporting references to another product.
Keyboard/mouse tokens are system inputs. Previously verified associations remain
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

## Acceptance

`GameWorkspaceTests` covers JSON save/reopen, exact native evidence, immutable
snapshots, rejected edits, stale saves, manufacturer/edition checks, isolated
review output and full-profile backup/restore. `EliteWorkspaceProjectionTests`
covers physical input translation, section hints and reference labels.
`GameWorkspaceSmoke` operates the actual WPF cards and ED window on Windows:
import, explicit association, button/axis edits, save, reopen, export,
save/discard/cancel transitions, input acquisition/release and blocked Free
commands. CI attaches game-selection and ED-workspace screenshots to the
`custom-mapping-preview` artifact. Existing DCS/training and release smoke remain
active, with independent production 80% line / 80% branch / CRAP <= 50 gates.

Before announcing verified in-game ED support, back up the entire real Bindings
folder, import a personal preset, export into a review folder and manually verify
primary/secondary assignments, a chord, hold/toggle behavior and axis tuning in
Elite. Record the game version and results; restore the backup if needed. CI does
not launch Elite or claim that manual acceptance has been completed.
