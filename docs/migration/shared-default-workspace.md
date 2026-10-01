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

## Module impact review

The **Module impact** tab compares the opened saved default revision with the current draft and lists every added, updated or removed source in ordinal path order. **Refresh module impact** reads saved module snapshots and compares each changed source against their exact pinned texts. Select a module to see its source evidence:

| Evidence | Meaning |
| --- | --- |
| Matches baseline | The module source agrees with the opened saved default. |
| Matches proposal | The module source already agrees with the draft, including an absent removed source. |
| Different source | The module has another source text; preserve and review that version. |
| Not pinned | The module has no source evidence for this comparison; review is required. |

A module requires review when any changed source differs from the proposal or lacks pinned evidence. With no draft source changes, the summary says **No default changes**. Missing catalog pins and older imports are reported conservatively. Exact filename case and Lua whitespace count as differences. This is source-level evidence, not a semantic assignment merge or a promise that a module can adopt safely. Module-owned assignments, labels, modifier edits and overlays still require their own adoption review.

Refresh before saving; draft edits, discard and save invalidate the report. Editing is temporarily disabled while module snapshots load. A read failure clears the report and shows the error rather than presenting partial results. The report compares the opened revision and each module snapshot read during refresh; it is not a transactional cross-module snapshot. Refresh again after concurrent module changes. Shared default saves continue to leave saved module state untouched.

This slice introduces no adoption or propagation action. Persisted module references to default revision IDs and controlled adoption remain pending under #83 Step 2. Tests cover exact source classifications, additions/removals, missing evidence, stable ordering, invalid baseline comparisons and real SQLite isolation. Windows smoke verifies refresh, module selection, draft/discard/save invalidation and preserved module source versions.


## Portable default revision references

Module snapshots can now carry a verified shared default baseline, independently of their effective sources and authored overrides. The reference contains the revision number, source commit, original content hash and a frozen ordinal copy of all default source texts. Capturing the baseline does not adopt its bindings or replace module sources.

SQLite reopen and immutable snapshot export/restore verify the reference digest. Baseline sources travel with the snapshot, so restoring into an empty database does not require external default history or a source checkout. Existing snapshots without this optional evidence retain their canonical format. Later default saves do not change an existing module reference. Ordinary module saves preserve the reference and reject removal/replacement from stale editors; changing a recorded baseline requires a reviewed adoption action. Module deletion leaves shared history intact.

This foundation adds portable reference evidence; explicit baseline selection and controlled adoption UI follow in the next PR. Tests cover source isolation, invalid/unsigned references, old exports, SQLite reopen, immutable export/restore into an empty database, history preservation, stale reference guards and tamper detection.

