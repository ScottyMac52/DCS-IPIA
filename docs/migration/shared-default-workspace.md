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

Module snapshots carry a verified shared default baseline independently of their effective sources and authored overrides. The reference contains the revision number, source commit, original content hash and a frozen ordinal copy of all default source texts. SQLite reopen and immutable snapshot export/restore verify the digest. Baseline sources travel with the snapshot, so empty-database restore requires neither shared revision history nor a checkout. Existing snapshots without optional reference evidence retain their canonical format. Later default saves leave module references unchanged.

## Reviewed source adoption and rollback

In **Module impact**, refresh and select a module. Save or discard any shared draft first. **Record this baseline** declares the opened saved default revision as its baseline without changing effective module sources, assignments, labels or overlays. It does not claim that older imports already inherit that revision.

For a module with a recorded baseline, the source decision grid compares that baseline with the opened saved revision. Safe source changes start checked; local source differences and missing previously pinned sources start unchecked. An added default path that already has a different module source is also a conflict. Check **Take default** only for files you choose to replace or remove. **Apply selected defaults** updates those exact paths and records the target baseline; all unchecked source files remain module overrides. Existing assignments, labels, catalogs, graphics and overlays remain module-owned. This is source adoption, not automatic binding or catalog propagation. Future semantic propagation needs its own overlay reconciliation and review.

The prospective Lua payload must validate before adoption is saved. Reviewed writes compare both the saved editor payload and relational module configuration under a SQLite transaction. Concurrent edits or module deletion reject the stale review without changing data. Each successful reviewed write retains the previous full module snapshot and its hash in migration 6 history. Ordinary saves reject removal or replacement of a recorded baseline from stale editors.

**Restore prior module state** restores the latest verified prior state through the same stale-review check. That operation also records its pre-restore state, allowing the last change to be reversed again. This is exact module rollback, so review after subsequent module edits before choosing it. Shared revision history and other modules remain unchanged. Deleting a module cascades its update history while preserving shared defaults.

Tests cover conflict decisions, explicit replacement, removals, kept source differences, invalid choices, missing baselines, backwards/same-identity revision checks, real SQLite atomicity, configuration-only and editor-payload races, verified rollback, module deletion and corrupt history rejection. Windows smoke records a baseline, adopts saved source changes, reopens them, restores prior sources/reference, and verifies cross-module isolation. Default binding/catalog propagation, independent shared-default kneeboard output and broader Steps 3–4 remain open under #83.



## Reviewed inherited assignments and catalogs

After choosing saved source changes, select **Review assignment/catalog changes**. The staged proposal compares the module's previous default, effective module bindings and new default by physical input and modifier chord. Matching inherited assignments update, including axes and filters; module overrides and explicit clears remain. Custom labels and removed-modifier overlays remain protected. Catalog labels and modifier Hold/Toggle changes use the same inheritance rule. Unmapped imported bindings and removal history remain in the emitted profiles. Ambiguous device matches stop the review.

The decision grid shows every changed binding, catalog entry and modifier and whether it will propagate or preserve a module edit. Changing checked sources requires another review. Apply validates prospective Lua, then uses the existing atomic stale-review save and prior-state rollback. Other modules and saved default history are untouched.
