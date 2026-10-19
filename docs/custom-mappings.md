# Custom draw.io mappings and portable revisions

**Edition availability:** imported artwork does not unlock a reserved device type. Free authoring/output still requires a reviewed Thrustmaster hardware identity; a compatible custom drawing can retain that identity. The unbundled/generic hardware examples below require Owner access. Premium customer availability/pricing remains TBA. See [the edition guide](design/free-edition.md).

Use **Hardware → Hardware drawings… / Import draw.io** in installed IPIA. Select the discovered target definition by its physical labels and native definition identity. Repeated physical units can share a definition. Choose **Open draw.io file…**, review the actual generated image and mapped/total input counts, then **Save drawing**. This is upload only: preview and publication change no modules or bundled files. Select the saved revision to preview, export or review replacements later.

In an existing module, open **Devices**, select the physical profile, choose a saved revision, and click **Apply drawing to selected device**. The normal **Save to database** and **Discard draft** controls govern adoption. Physical GUID/profile IDs, native filenames, page identities, assignments in both scopes, displayed labels, physical control names, modifier activators and sparse axis tuning remain unchanged. Other physical units and modules retain their prior revisions.

## Replace selected devices or all compatible devices

Close module editors, select a saved drawing in Hardware drawings, and choose **Review replacements / rollback…**. Review includes built-in, generic and custom profiles across saved modules. Each row identifies its module, device and native DCS filename, reports compatibility, and offers **Compare pictures**. No devices are selected automatically.

- Check individual devices to replace only those units in those modules.
- Choose **Select all compatible** to select every eligible device across saved modules; already-pinned and incompatible profiles remain unselected.
- **Clear selection** removes every selection.
- Choose **Stage selected**, then **Save staged changes** or **Discard staged changes**. Uploading, reviewing and staging alone never write modules.

Existing stable control markers must retain their native input and axis/button kind. The first replacement of a built-in drawing can add validated native inputs missing from its old artwork (for example diagonal POV directions); existing controls cannot disappear or be remapped. Generic/custom profiles keep their existing native definition and revision identity checks. Incompatible rows explain what must be reconciled. Replacement rules and persistence orchestration live in the application layer, independent of WPF.

On the first built-in replacement, the module retains its original SVG, raster, canvas and control geometry in optional `originalDrawing` evidence. In the central review, choose **Stage original built-in drawing** beside a changed device, then Save or Discard. Restoration keeps current personal labels, assignments and physical identities and retains custom revisions for re-adoption. Original artwork travels with SQLite, portable module snapshots and full-profile backups; it does not require the installed bundle to remain at the same version. Historical custom revisions remain available even after restoring a built-in drawing.

A bulk save commits each module separately with stale-state checks. If a save fails, the screen reports how many modules committed and retains remaining drafts. Discard remaining drafts and reopen review before retrying. After a successful save, reopen review for another operation. Bundled assets and default artwork for newly created devices are unchanged; this is explicit replacement of reviewed saved profiles, not an automatic default override.

## Supported editable source

Version 1 supports one native or raw-deflate/URI-encoded draw.io diagram in an `mxfile` envelope. Use a flat graph with vertices parented to cell `1`, finite absolute geometry, a `canvas`, and a `footer`. Canvas dimensions are 100–4096 wide and 100–2048 high. Developer notes may be retained as `developer-notes` but are not rendered.

- Physical label cells have unique stable `label-<marker>` IDs. Use the actual DCS key as the marker, e.g. `label-JOY_BTN1`, `label-JOY_X`, or `label-JOY_BTN_POV1_UR`. For another marker, set an explicit `inputKey` attribute on the cell/UserObject or `inputKey=JOY_BTN1` in its style. Existing `buttonNumber=1` metadata can explicitly identify a button. The selected definition determines whether a key is valid and whether it is an axis.
- Plain text labels, hex RGB fill/font colors and 8–72 pixel fonts are supported. Native UserObject ID/label wrappers are normalized without changing original source.
- Optional `anchor-<marker>` cells and `connector-<marker>` edges connect the matching label to the anchor. Absolute waypoints and the tested label-edge/anchor-center endpoint calculation are retained. No connectors are required for box overlays.
- Optional `hardware-image-*` cells contain embedded PNG/JPEG data or safe relative local PNG/JPEG filenames. Import captures referenced local bytes; it never downloads images. `mask-*` rectangles and `presentation-*` text regions are also supported.

This is a documented subset, not an arbitrary draw.io renderer. Groups, transformed shapes, unsupported custom stencils, rich HTML labels, external links, script/event attributes, external images, DTDs and unknown vertices/constructs produce errors before publication. Bundled drawing copies, including renamed copies, can acquire native keys from installed catalog mappings only when their complete stable-marker set matches an unambiguous catalog entry for the selected native definition. Keep the original label IDs; otherwise supply explicit input metadata. Duplicate IDs, multiple labels for the same native key and unknown/ambiguous inputs are rejected rather than inferred.

## Ordinary draw.io artwork edits

The importer selects exporter `ipia-drawio-4` for ordinary blank cover rectangles, draw.io freehand stencils made from move/line/close paths, straight decorative lines and embedded images with arbitrary cell IDs. Artwork retains draw.io painter order. White strokes and cover shapes can hide headings or lines without changing control IDs. Empty presentation headings remain empty. `light-dark(...)` artwork colors use the first visible color, including white when its alternative is transparent.

Images and decorative artwork may extend beyond the page; rendering clips them to the original page instead of rejecting a slight overhang or expanding the page. Native control labels and anchors must still fit inside the canvas. Artwork coordinates and dimensions remain bounded, stencil payloads are limited to 1 MB before and after inflation, and an aggregate 100,000-command budget applies to stencil paths and decorative lines. Curves, arrowed decorative edges, arbitrary stencil instructions, active content and transformed artwork remain unsupported.

Earlier immutable revisions retain exporters 1–3 and reproduce their original SVG and hashes. Saving an edited drawing creates a new revision; it does not rewrite existing saved drawings.

## Partial mappings

Each native input remains represented once. Omitted inputs receive deterministic generic boxes below the authored drawing in the editor; the resulting canvas must fit within 4096×4096. The kneeboard includes the authored graphic followed by readable, paginated generic continuation pages for omitted controls. Assignments, chords and modifier descriptions remain visible. Package and documentation workflows include all continuation pages. Custom physical labels remain module-owned; editing an immutable drawing means importing a new revision.

## Integrity, persistence and portability

The profile's `UserMappings/<sha256>.json` library is separate from bundled assets. Each format-1 revision records its immutable exporter semantics (`ipia-drawio-1` through `ipia-drawio-4`) and contains the original editable source, importing app version, source filename, reviewed native capability evidence, captured local raster bytes, source/render hashes, complete control geometry and derived SVG. Its identity covers the complete revision. Loading, saving, adopting, snapshotting and rendering reproduce the SVG from source and reject mismatches.

Publication creates a new file atomically and never overwrites a different revision. Existing identical imports are idempotent. Management lists, previews and exports saved revisions and read-only pins carried by saved modules, including portable restores with no library. Native definitions from saved generic/custom modules remain import targets when inventory is absent or hardware is disconnected; reviewed bulk replacement and deletion of library copies are available as described below.

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

## Starter drawings and reviewed revisions (Feature 7)

Choose **Hardware drawings → Export starter draw.io…** after selecting a verified controller. The image-free starter uses one explicit `label-<DCS key>` and `inputKey` for every native button, axis and POV direction, with deterministic paginated columns inside the supported canvas. Edit its layout and add supported images in external draw.io. Retain IDs and native keys; import the result through the normal preview/save flow. Existing filenames are never overwritten.

Saved drawing contents are immutable: an edit creates another revision. Library copies and restored module pins remain selectable for preview and **Export editable bundle…**, which carries exact source, captured images and integrity evidence. **Delete saved drawing** permanently removes the selected library copy after confirmation. It does not change bundled drawings, existing module pins/history or exported snapshots; a revision retained only by a module is read-only and cannot be deleted through the library command. Keep an editable bundle or full backup if you need to recover a deleted library copy.

Select a revision and **Review replacements / rollback…** to inspect every saved device profile. The review reports old/new pins, moved/resized controls, canvas size, rendered artwork and source changes. Use **Compare pictures** beside any target to see its current and selected artwork side by side, including blocked targets. Native input differences and removed, renamed or remapped stable markers block adoption with instructions to restore IDs/keys or reconcile the definition and assignments separately. New artwork never silently moves a command to another input. Selecting an older compatible version uses the same review path for rollback.

Choose individual module/profile checkboxes, **Stage selected**, then **Save staged changes** or **Discard staged changes**. Merely reviewing/staging does not write modules. Save checks that each reviewed module still matches its saved state before atomically updating it and retaining repository rollback evidence. A multi-module save is a sequence of module transactions: a failure explicitly reports how many saved, leaves remaining drafts staged, and requires discarding/reopening review. It does not overwrite concurrent changes. Close module editors before opening the central review; the main window already prevents opening this tool while an editor is open.

Inside a module, applying a drawing now presents the same difference/conflict review before staging. Normal editor Save/Discard preserves unrelated draft work. Older pinned drawings are included in the module's drawing choices after portable restore even when the separate library is empty.

Each diagram optionally carries `mappingHistory`, a list of complete immutable prior custom pins, including after original built-in artwork is restored. `originalDrawing` is omitted until the first built-in replacement and cleared upon restoring that artwork. Adoption retains the previous pin; rollback retains the version rolled back from. Assignments, axis filters, modifier state, physical GUID/profile IDs, labels, filenames, pages and unrelated devices remain module-owned. Database payloads and portable snapshots carry the history alongside the active pin; full-profile backups also carry the complete library. The optional null field is omitted, preserving old payloads and canonical snapshot bytes. Existing revision hashes and exporter semantics are unchanged. No SQL schema migration is required.

Acceptance: export a starter, edit its geometry/images, import two revisions, compare them, stage/discard once and save only selected targets. Restore a module snapshot into an empty profile and select its older pin for rollback/export. Change a stable marker or native key and confirm actionable blocking; verify another module and unselected physical unit keep their original output.
