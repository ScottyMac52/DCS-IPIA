# Shared default UI Layer workspace foundation

First Step 2 slice of #83, related to #9.

The Modules screen opens **Shared default UI Layer**. On first use, IPIA validates its built-in catalogs/Lua and saves a separate shared default revision. Subsequent opens read the saved revision from SQLite without reseeding from the bundle. The window displays its revision, a key-binding editor for existing device profiles, and a read-only catalog/profile/modifier source view.

Schema migration 5 adds an independent append-only revision table. It does not create an aircraft module or alter module workspaces. Each revision carries its pinned source commit, exact source texts, and verified SHA-256 content identity. Saving requires the expected latest revision; stale saves fail atomically and old revisions remain addressable. A bundled asset update does not silently replace saved defaults or module overrides.

The next Step 2 slice adds key-binding editing for existing shared profiles. Select a profile and binding, choose a command, enter a DCS input key and optional declared modifier chord, then Add, Update, or Remove. These actions stage changes. Save new default revision validates modifier references and occupied input chords, and appends through the existing revision concurrency check. Discard restores the baseline. Only the edited profile is rewritten; untouched sources retain exact bytes. Unmapped bindings, axis filters, and removal history are retained. Axis rows and removal history remain read-only in this slice.

Commands are offered from the shared function catalog plus existing imported key bindings. Command catalog maintenance, shared device/modifier management, module revision references, affected-module reporting and controlled adoption/propagation remain open. Existing module import continues using its pinned built-in sources until controlled adoption is implemented. Editing shared defaults does not update any module implicitly.

Tests use real SQLite to cover initialization, reopen, immutable history, stale-save rejection, source-content integrity, migration idempotence, invalid input and isolation from saved module overrides. Windows smoke checks the read-only source view and the binding editor: update, discard, save, reopen, and preserved revision history. Regression tests cover key conflicts, modifier references, invalid/stale selection, axis/filter/removal preservation, untouched source bytes, and module isolation.
