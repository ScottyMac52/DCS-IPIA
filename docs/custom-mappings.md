# Custom draw.io mappings and portable revisions

Use **Hardware → Import draw.io mapping… / User mappings** in installed IPIA. Select the discovered target definition by its physical labels and native definition identity. Repeated physical units can share a definition. Browse an editable file, review the actual generated image and mapped/total input counts, then **Save new revision**. Preview and publication change no modules or bundled files.

In an existing module, open **Devices**, select the physical profile, choose a saved revision, and click **Stage mapping on selected profile**. A compatible definition must contain exactly the same actual inputs. The normal **Save to database** and **Discard draft** controls govern adoption. Physical GUID/profile IDs, native filenames, page identities, assignments in both scopes, displayed labels, physical control names, modifier activators and sparse axis tuning remain unchanged. Other physical units and modules retain their prior revisions.

## Supported editable source

Version 1 supports one native or raw-deflate/URI-encoded draw.io diagram in an `mxfile` envelope. Use a flat graph with vertices parented to cell `1`, finite absolute geometry, a `canvas`, and a `footer`. Canvas dimensions are 100–4096 wide and 100–2048 high. Developer notes may be retained as `developer-notes` but are not rendered.

- Physical label cells have unique stable `label-<marker>` IDs. Use the actual DCS key as the marker, e.g. `label-JOY_BTN1`, `label-JOY_X`, or `label-JOY_BTN_POV1_UR`. For another marker, set an explicit `inputKey` attribute on the cell/UserObject or `inputKey=JOY_BTN1` in its style. Existing `buttonNumber=1` metadata can explicitly identify a button. The selected definition determines whether a key is valid and whether it is an axis.
- Plain text labels, hex RGB fill/font colors and 8–72 pixel fonts are supported. Native UserObject ID/label wrappers are normalized without changing original source.
- Optional `anchor-<marker>` cells and `connector-<marker>` edges connect the matching label to the anchor. Absolute waypoints and the tested label-edge/anchor-center endpoint calculation are retained. No connectors are required for box overlays.
- Optional `hardware-image-*` cells contain embedded PNG/JPEG data or safe relative local PNG/JPEG filenames. Import captures referenced local bytes; it never downloads images. `mask-*` rectangles and `presentation-*` text regions are also supported.

This is a documented subset, not an arbitrary draw.io renderer. Groups, transformed/custom shapes, rich HTML labels, external links, script/event attributes, external images, DTDs and unknown vertices/constructs produce errors before publication. Existing bundled sources that depend on a separate Common control manifest must acquire explicit input metadata before being imported as user mappings. Duplicate IDs, multiple labels for the same native key and unknown/ambiguous inputs are rejected rather than inferred.

## Partial mappings

Each native input remains represented once. Omitted inputs receive deterministic generic boxes below the authored drawing in the editor; the resulting canvas must fit within 4096×4096. The kneeboard includes the authored graphic followed by readable, paginated generic continuation pages for omitted controls. Assignments, chords and modifier descriptions remain visible. Package and documentation workflows include all continuation pages. Custom physical labels remain module-owned; editing an immutable drawing means importing a new revision.

## Integrity, persistence and portability

The profile's `UserMappings/<sha256>.json` library is separate from bundled assets. Each format-1/exporter-`ipia-drawio-1` revision contains the original editable source, importing app version, source filename, reviewed native capability evidence, captured local raster bytes, source/render hashes, complete control geometry and derived SVG. Its identity covers the complete revision. Loading, saving, adopting, snapshotting and rendering reproduce the SVG from source and reject mismatches.

Publication creates a new file atomically and never overwrites a different revision. Existing identical imports are idempotent. Management lists, previews and exports saved revisions and read-only pins carried by saved modules, including portable restores with no library. Native definitions from saved generic/custom modules remain import targets when inventory is absent or hardware is disconnected; destructive deletion and bulk revision updates are outside Feature 6.

**Export editable bundle…** writes a deterministic ZIP containing `mapping.drawio`, captured local images and `revision.json`. Extract it, edit the draw.io file and import again to create a new revision. Export chooses a new filename and does not replace an existing archive.

Original source is capped at 2 MB, bounded inflated graphs/generated SVGs at 4 MB, and graphs at 2000 cells. Images are capped at 32 files, 4 MB each, 8 MB aggregate local bytes, 4096 pixels per side and 8 million aggregate decoded pixels. Safe relative paths exclude traversal, absolute paths, Windows device names and links/junctions. User library reads are capped at 20 MB per revision. Checksums detect corruption, not authorship.

The complete adopted revision is an optional member of the existing diagram payload. Old bundled/generic diagrams omit it and retain their existing representation. No SQL migration or snapshot-version change is needed: old data remains readable and library files are included by full-profile backup from the first release. Portable module snapshots carry their pinned editable source/images/SVG without requiring a separate library, original paths, connected controllers or Node.

## Acceptance exercise

1. Discover/adopt an unbundled controller; create a generic module and assign buttons, an axis and a POV in both aircraft and UI scopes. Add a modifier, tune an axis and name a control.
2. Author a supported partial draw.io diagram with an explicit mapped key and embedded/local image. Preview and save it. Confirm the module is unchanged.
3. Stage adoption on one physical profile. Discard once; stage again and save. Confirm filenames, page IDs, labels, assignments, modifiers and filters survived and another unit/module stayed unchanged.
4. Reopen, render and build. Review both the artwork and generic continuation pages. Try unknown/duplicate mappings, oversized files and external image URLs; they must report errors without mutation.
5. Export/restore a module snapshot in a separate profile without the original files or library and rebuild identical output. Full-profile backup/restore must restore the separate library too.
6. Export the editable bundle, extract and edit it, then import another revision. The old revision/module pin must remain unchanged.

Automated fixtures cover these boundaries; CI publishes a custom controller drawing as PNG/SVG for visual review. Physical-controller and in-game acceptance remains a maintainer check.
