# Shared default UI Layer workspace foundation

First Step 2 slice of #83, related to #9.

The Modules screen opens **Shared default UI Layer**. On first use, IPIA validates its built-in catalogs/Lua and saves a separate shared default revision. Subsequent opens read the saved revision from SQLite without reseeding from the bundle. The window displays its revision and exact catalog/profile/modifier sources in a read-only preview.

Schema migration 5 adds an independent append-only revision table. It does not create an aircraft module or alter module workspaces. Each revision carries its pinned source commit, exact source texts, and verified SHA-256 content identity. Saving requires the expected latest revision; stale saves fail atomically and old revisions remain addressable. A bundled asset update does not silently replace saved defaults or module overrides.

This is the agreed save/reopen foundation. Command/binding/device/modifier editing, semantic validation of user edits, module revision references, affected-module reporting and controlled adoption/propagation are the next Step 2 slices. The current read-only window does not imply those editing features are delivered. Existing module import continues using its pinned built-in sources until controlled adoption is implemented.

Tests use real SQLite to cover initialization, reopen, immutable history, stale-save rejection, source-content integrity, migration idempotence, invalid input and isolation from saved module overrides. Windows smoke opens the shared source preview and checks its content/read-only state.
