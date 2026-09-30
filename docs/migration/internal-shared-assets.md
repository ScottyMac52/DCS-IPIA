# Internal shared assets

Migration Step 1 of #83; broader roadmap #9.

The application distributes all 73 shared hardware, component, and canonical UI Layer assets plus the DCS-Common MIT license. `data/shared-assets/provenance.json` records the source repository/commit, original paths, sizes, and Git blob IDs. Original bytes, draw.io format/footer, SVG geometry, binary images, and GUID-qualified Lua filenames are preserved.

MSBuild copies the bundle to `SharedAssets/` in build and publish output, including project-reference consumers. Desktop module import uses this bundle automatically. **Load built-in hardware** stages its catalog without a Common folder picker; Save persists the update. Existing module UI Layer source texts and assignments remain pinned during catalog refresh. Imported snapshots carry the bundle provenance manifest.

Before import, every file is checked against its original Git blob ID. SHA-1 is used solely for Git identity interoperability; release checksums continue to use SHA-256. Missing or changed sources fail explicitly. The bundle is versioned with IPIA and never downloaded or silently refreshed at runtime. Consumer module import still uses Git to read committed module sources. Explicit legacy Common-checkout loader calls remain for compatibility.

Tests cover bundle distribution and all source identities, the full hardware graphic catalog and AVA alias, real module import without a Common checkout, unsafe/duplicate/missing/changed entries, and presentation output. New bundle revisions require reviewed source and provenance updates.

Shared-default UI Layer editing, new-module creation, scaffolding/export/documentation tools, and consumer pipeline migration remain Steps 2–4. Existing DCS-Common consumers continue operating until their output parity and rollback gates pass. This PR does not retire DCS-Common or change consumer repositories.
