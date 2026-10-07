# Author commands and pages

Open a saved module and choose **Commands** to add aircraft commands. Enter the exact DCS command code, a display name, category and key/button or axis type. **Stage command** updates the current draft and assignment palette. Command codes stay fixed after creation. Assigned commands cannot be removed or switched between key and axis until their assignments are cleared; unmapped imported bindings also protect their command.

Name and category edits retain labels, axis tuning, modifier chords, removed history and the module-owned UI Layer. The shared default catalog is edited separately.

Choose **Pages** to author kneeboards. **Add missing device pages** supplies one combined page for every pinned device, including inherited UI Layer devices. Select a device page to change its output name (without .png), heading, subtitle and order. Its hardware geometry remains pinned; MFD category captions remain editable in Devices. **Remove page** deletes any selected page, including device pages and old orphans, without deleting the device or its assignments. Use **Add missing device pages** to recreate a missing device page when needed.

For an overview, choose **Summary**, enter a safe output name, heading, subtitle and order, then add one to fourteen cards. Each card has a heading, description and palette accent. Cards render in listed order, in the existing two-column layout. Drag a card row onto the upper or lower half of another row to insert before or after it, or drop below the rows to move it to the end. A successful move renumbers card headings `01`, `02`, … in list order, keeps the moved card selected, and preserves descriptions, accents and per-card metadata. A normal click still selects for editing; a cancelled, same-position or foreign-list drag makes no change. Reordering changes the form: use **Preview draft**, then **Stage page** and **Save to database** to keep the result. Edit or remove summary pages as needed.

Choose **Generate Summary** beside **Add missing device pages** to append a new overview of all current device pages, including pages without a device mapping. Each card shows the device page's title and its current page number. Existing pages and summaries remain unchanged; output names are chosen without collisions. More than fourteen device pages produce additional summary pages so every device page is included. Generated summaries can be edited, saved or discarded like any other page; generate another summary after changing the device pages to refresh the overview.

Removing a device's final Module/UI Layer membership removes its combined pages, profile mappings and cached images. A page remains while the device still belongs to the other scope. Removing a page also clears its manifest entries and cached images. Remaining page orders are compacted without changing their stable output names. Save or discard these changes with the rest of the module.

**Preview draft** renders the page from current form values and pending assignments without saving. **Stage page** validates and stages the page. Existing unedited manifest fields, callout/category labels and per-card metadata remain preserved.

Use **Save to database** to persist all staged command, page, device, modifier, assignment and label changes together. **Discard draft** restores the last saved workspace. Reopen a module to load these definitions from SQLite; source repositories are not required.

This is the second Step 3 slice under #83. The final creation-to-validation/preview/immutable-snapshot/restore/OvGME workflow and user acceptance remain separate requirements.


## Load aircraft commands from IPI JSON or DCS HTML

In the module workspace's **Commands** tab, choose **Load commands from JSON…** for an IPI/DCS-Common schemaVersion 1 command catalog, or **Load commands from DCS HTML…** to select one or more DCS Controls **Generate HTML** exports for that aircraft. JSON must declare the same moduleId as the module's DCS input folder. HTML device headings must match their filenames; its exports do not identify the aircraft, so confirm the destination aircraft and selected files in the review.

Review additions, matching-code updates, old/new names, category paths, and key/axis types, then **Stage import**. Commands absent from the selected files stay in the catalog. Matching codes update their names/categories while retaining assignments, custom labels, axis tuning, modifier chords, removal history, UI Layer ownership and other modules. An assigned key/axis type conflict blocks the entire batch. Duplicate/conflicting HTML hashes, malformed catalogs, wrong JSON module IDs, and stale reviews also fail without partially changing the draft.

The assignment palette and categories refresh immediately after staging. **Save to database** persists the merge in SQLite; **Discard draft** restores the saved catalog. Cancelling review makes no change. Command imports load definitions only: the currentAssignments JSON metadata and the HTML button/axis assignments are retained in source evidence and are not applied to physical devices.

The module snapshot retains selected file basenames and SHA-256 addressed original source text, including IPI metadata not projected into the editable palette. Local machine paths are not recorded by the import flow. Loading the same source again reports unchanged entries and preserves catalog/source identity.


## Validate, export, restore and build

Save pending changes, enter a package version, and use **Validate saved module**. Validation checks the frozen editor data, physical binding review, aircraft/UI command and modifier references for newly created modules, pinned assets, complete device and summary pages, and the generated OvGME archive. Failures appear beside the output actions. The Kneeboard tab previews the saved device/summary pages; Commands and Pages also retain their draft authoring previews.

**Export module snapshot…** writes a new `*.ipia-module.json` file containing the package version, canonical complete editor state and SHA-256 digest. Assignments, sparse axis tuning, modifier chords, custom labels, source evidence, graphics, pages and the pinned shared default are retained. No existing snapshot file is overwritten. Keep this file to reproduce that exact output even after subsequent edits.

On the Modules screen, use **Restore module snapshot…** to validate and insert its module into an empty database, or a database in which both its module GUID and DCS folder are unused. Existing identities are rejected atomically, including case-insensitive DCS folder collisions; this action does not replace a saved module or update independent shared defaults. The restored module appears in the selector. Open it and use the package version reported by restore to reproduce the original archive and checksum. No consumer checkout or original database is needed; the snapshot contains the required assets and baseline evidence.

**Build OvGME package…** freezes the saved state, generates and independently verifies the archive from that snapshot, then writes the ZIP and separate checksum into the selected folder. Output actions are disabled while another output operation runs or edits are pending. Save or Discard first. Package and snapshot files are separate: the snapshot is portable authoring/build input; the ZIP contains only the deployable DCS files and package metadata. To recover an earlier output, build from the earlier snapshot after restoring it in an empty database; it is not an in-place rollback of the current module.

Automated acceptance uses a newly authored module with real bundled MFD/MOZA hardware, JSON command import, base/shift aircraft bindings, a module-owned UI assignment, a toggle modifier, tuned axis, custom label and device/summary pages. Real SQLite save/reopen and empty-database restore retain canonical snapshot bytes, and repeated snapshot-only builds reproduce identical ZIP/checksum bytes. Corruption, incomplete state and duplicate identities fail before writes. Windows smoke exercises validation, snapshot export, package build, pending-edit guards, restore into the selector and duplicate restore rejection. Manual Windows/DCS acceptance remains tracked in #83 and #111.

## Export module documentation

With a clean saved module and a package version, choose **Export module documentation…** and select a parent folder. IPIA validates and freezes the inputs, then creates `DCS-<module>-Components-<version>-Documentation`. Existing files or folders are never replaced. The export contains README, installation/removal, control mappings and third-party asset guides, all stamped with the version and workspace SHA-256. Keep the matching exported module snapshot and OvGME ZIP/checksum alongside it.

Mappings are parsed from the actual generated Lua, including aircraft and effective UI Layer profiles, exact native filenames, modifier activators/modes, added/changed/removed entries, custom labels and sparse axis filters. The separately captured shared UI revision is identified as a baseline. Authored controls with no active packaged diff are listed explicitly; this does not assert that the installed DCS game has no default binding. Captured source/image/SVG hashes, license/attribution texts and the rendering font license are retained.

Optional integrations appear only when the snapshot explicitly contains `config/integrations.json`, with `schemaVersion: 1` and an `integrations` array of objects containing nonempty `name` and `instructions` strings. The guide preserves these instructions as captured text; it does not enable or package an integration. The Documentation tab now supports reviewed JSON and legacy Markdown imports; legacy per-device document parity and integration asset/configuration adapters remain review gates. No VAICOM/OpenKneeboard/AutoHotkey configuration is invented for an aircraft.

Documentation remains separate from the deployable OvGME archive. Complete release distribution is available below. Automated checks cover source-free restore/reproduction, version/digest and mapping semantics, escaping, explicit integrations, existing-destination protection and the Windows export button/pending-edit guard. Consumer golden comparisons and manual in-game acceptance remain open under #83/#9/#111.

## Export a Complete module release

Choose **Export Complete release…** with clean saved inputs and a package version. Select a parent folder; IPIA creates a new versioned Complete artifact folder containing the distribution ZIP and SHA256SUMS.txt. The ZIP contains separate `OVGME`, `Documentation` and `Snapshots` directories, version metadata, release notes and checksums for every included file. Extract it, verify checksums and install only the nested OvGME ZIP. The Complete distribution ZIP is not itself an OvGME mod.

Verification independently reads paths and checksums, verifies the nested deployable ZIP, validates/restores the portable snapshot in memory, and regenerates the archive and guides for exact comparison. It rejects stale guides, version mismatches, corrupt/duplicate/unsafe entries, missing snapshots and unexpected local database files. Rebuilding a restored snapshot reproduces the Complete ZIP; existing output folders are protected.

## Import an editable module from Saved Games

Use **Import module sources** and select the module input folder or its `joystick` subfolder. Review the destination DCS input folder and display name: copied or renamed folders do not prove the aircraft identity. Every joystick profile appears in the hardware review, including empty profiles and profiles containing only removals. Hardware suggestions use the bundled recognition rules; confirm each choice and select an appropriate template for unrecognized devices. Repeated controller names retain distinct physical GUIDs.

Choose **Import module** to create and reopen a new SQLite workspace. Existing modules with the same DCS input identity are protected from replacement. Cancel or correct a failed review without changing your source files. Imports with malformed profiles or unresolved modifier references are blocked rather than partially saved.

To start with inherited axes suppressed, back up your existing controls, reset the device's axes to defaults in DCS, clear all axes and save its profile. Leave keys alone, then import the module folder. Captured axis removal entries are retained automatically; they need no separate default import. Assign the desired axes in IPIA. Unchanged DCS default key bindings are absent from diff files and are not reconstructed. No aircraft-specific default commands or physical axes are inferred.

Explicit mapped key and axis assignments become editable bindings, including modifiers and axis tuning. Unmapped imported keys remain in native exports; select the hardware that represents those keys to edit them. An active imported axis missing from the chosen hardware blocks import so it cannot be silently removed. The command palette initially contains only commands found in the profiles; load the module command JSON or DCS Controls HTML for its complete catalog.

Module keyboard profiles are captured with the folder and included unchanged in package exports and portable snapshots. They are not joystick device rows. The saved shared UI Layer revision is copied independently, using the existing module creation behavior; review/rebind its copied physical units before packaging. Reopening, rendering and exporting use SQLite and the frozen sources, without rereading Saved Games.

## Suppress inherited DCS axis defaults

An empty axis diff does not clear the game's default axes. On **Devices**, select the device and scope, then choose **Import DCS axis defaults…**. First back up the device's current DCS controls. In DCS Controls for this aircraft (or UI Layer), reset that device's axes to defaults, clear all its axes, and save its profile as a `.diff.lua`. Restore your backed-up controls afterward. Import the cleared profile into IPIA and review the destination and each command/axis/modifier combination before staging. DCS diff files do not encode the aircraft identity; verify the selected aircraft and scope yourself.

The import replaces the baseline for that device and scope. **Save to database** retains it; **Discard draft** undoes it. Aircraft and module-owned UI Layer baselines are independent and do not change the shared UI default. Keys in the capture are ignored. Files containing no axis removals, any active axes or duplicate removal combinations are rejected.

Exports suppress the captured defaults except exact command/axis/chord combinations still assigned in IPIA. Reusing `JOY_X` for Pitch still removes a captured Roll default on `JOY_X`; a shifted assignment does not retain an unshifted default. Imported axes missing authored hardware controls, cleared axes and replaced commands also emit explicit removals. Intended filters and modifier chords remain. Device/layer clears retain axis removals, so defaults do not return after clearing assignments.

The original capture is stored with the module and portable snapshot. Complete/OVGME builds and source-free restores use the saved capture. No capture can suppress defaults absent from that file; recapture after changing DCS default bindings or aircraft versions. An axis-less device still needs its DCS baseline to remove inherited axes. Verify the installed package in DCS before considering in-game acceptance complete.

## Maintain integration instructions

Open **Documentation → Load integration instructions…**. Choose one explicit JSON instruction file or an existing consumer Markdown guide such as `docs/OPENKNEEBOARD-VAICOM.md`. Review the destination module and full previous/proposed text, then **Stage import**. Groups with matching names (ignoring case) are updated; other groups remain. Original basenames, decoded UTF-8 source texts and source hashes are retained in portable snapshots. A selected Markdown guide is captured verbatim under its basename, including historical paths; those paths are not automatically rewritten or asserted to match this release. Check them against the generated installation and control mappings.

Use **Save to database** to keep the draft, or **Discard draft** to undo it. **Remove selected instruction** removes the effective group; the original import evidence remains. Integration instructions share the module's Save/Discard transaction with commands, assignments, modifiers and pages. Saved groups reopen in SQLite and appear in `INTEGRATIONS.md` in both documentation and Complete exports. They do not change the nested deployable OvGME package or another module/shared default.

JSON schema:

```json
{
  "schemaVersion": 1,
  "moduleDcsId": "P47-D-30",
  "integrations": [
    { "name": "Custom operator guidance", "instructions": "Describe this module's reviewed configuration here." }
  ]
}
```

`moduleDcsId` is optional on imported JSON; when supplied, it must match the destination exactly. Staged metadata always records the destination. Names must be unique ignoring case. Imports reject malformed/unsupported fields, wrong module identities, unsafe basenames and oversized text before staging; a changed module requires a fresh review. Up to 64 groups are supported, with printable names up to 200 characters and instructions up to 64 KiB. Instruction JSON is limited to 1 MiB of text and selected files to 4 MiB. Only UTF-8 JSON/Markdown instructions are loaded. Integration executables, scripts, credentials and configuration assets are not imported or deployed by this action. Consumer integration-asset adapters and full parity remain pending.

