# Shared default UI Layer workspace foundation

First Step 2 slice of #83, related to #9.

The Modules screen opens **Shared default UI Layer**. On first use, IPIA validates its built-in catalogs/Lua and saves a separate shared default revision. Subsequent opens read the saved revision from SQLite without reseeding from the bundle. The window displays its revision, a key-binding editor for existing device profiles, a command catalog editor, a modifier editor, and a read-only source view.

Schema migration 5 adds an independent append-only revision table. It does not create an aircraft module or alter module workspaces. Each revision carries its pinned source commit, exact source texts, and verified SHA-256 content identity. Saving requires the expected latest revision; stale saves fail atomically and old revisions remain addressable. A bundled asset update does not silently replace saved defaults or module overrides.

Key-binding editing is available for existing shared profiles. Select a profile and binding, choose a command, enter a DCS input key and optional declared modifier chord, then Add, Update, or Remove. These actions stage changes. Save new default revision validates modifier references and occupied input chords, and appends through the existing revision concurrency check. Discard restores the baseline. Only the edited profile is rewritten; untouched sources retain exact bytes. Unmapped bindings, axis filters, and removal history are retained. Axis rows and removal history remain read-only in this slice.

Commands are offered from the shared function catalog plus existing imported key bindings. Module revision references, affected-module reporting and controlled adoption/propagation remain open. Existing module import continues using its pinned built-in sources until controlled adoption is implemented. Editing shared defaults does not update any module implicitly.

Tests use real SQLite to cover initialization, reopen, immutable history, stale-save rejection, source-content integrity, migration idempotence, invalid input and isolation from saved module overrides. Windows smoke checks the read-only source view and the binding editor: update, discard, save, reopen, and preserved revision history. Regression tests cover key conflicts, modifier references, invalid/stale selection, axis/filter/removal preservation, untouched source bytes, and module isolation.


## Shared modifier maintenance

The **Modifiers** tab adds, updates, and removes device modifiers and switches in the shared draft. Choose an existing native device profile, enter a modifier name and DCS input, and select **Hold** (modifier) or **Toggle** (switch). Native names and GUID case remain exact. Built-in keyboard defaults are protected. Duplicate names and physical activators, including equivalent POV spellings, are rejected.

Updating a bound modifier's input or mode retains its name and all assignments. Clear active key and axis assignments across shared profiles before renaming or deleting a modifier. Removal history remains intact. These changes rewrite only the modifier source; other source files, filters, and provenance stay unchanged. Save appends a revision; Discard restores the baseline and refreshes binding-layer choices. Existing modules retain their pinned sources and overrides.

The command catalog editor, binding editor, and modifier editor share the same revision save/discard workflow. Unit tests cover source preservation, bound-name protection, exact native names, invalid/stale edits, POV conflicts, protected defaults, SQLite history and module isolation. Windows smoke exercises modifier add/update/remove, keyboard protection, layer-picker refresh, discard, save, and reopen.


## Shared device/profile management

The **Devices** tab lists native profiles and their active key/axis assignments and modifier references. Create a joystick profile with the exact native DCS filename including its physical GUID, or copy an existing joystick profile. Copying retains the original Lua bytes, filters, removal history and existing shared modifier chords; it does not create duplicate activator definitions. Leave the copy selector blank for an empty profile. The default keyboard profile is protected.

Rename keeps the same physical GUID, preserves profile Lua exactly, and updates modifiers activated by that device to the new exact native name. Clear all active assignments and move or remove device modifier definitions before deleting a profile. Deleting removes that profile from the draft; older saved revisions retain its source and history. Duplicate filenames or GUIDs, unsafe paths, missing/stale templates and attempts to move an existing profile to another physical device are rejected.

Device edits use the same **Save new default revision** and **Discard draft** workflow. Device, binding-profile, source-file and modifier-device selectors refresh together. Existing modules and their overrides remain unchanged. Tests cover exact clone/rename preservation, keyboard and reference guards, GUID identity, invalid/stale selections, persistence, immutable history and module isolation. Windows smoke exercises profile clone, rename, deletion guards, empty-profile removal, discard and save/reopen.
