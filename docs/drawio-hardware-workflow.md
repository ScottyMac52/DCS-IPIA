# Hardware SVG authoring

IPIA owns the deterministic draw.io exporter under `scripts/asset-authoring/`. Node 24 is required for this developer tool; the installed .NET application does not require Node or a Common checkout. There are no npm dependencies. Original source identities and MIT attribution are retained in [PROVENANCE.json](../scripts/asset-authoring/PROVENANCE.json).

From the repository root, verify all published bundled hardware SVGs without changing files:

```sh
node scripts/asset-authoring/export-hardware.mjs --check
node --test tests/asset-authoring/*.test.mjs
```

With no arguments the exporter also verifies only. Compare the exact baseline and intentional behavior changes in the [all-device parity report](migration/drawio-export-parity.md).

To edit, work on a copy of `data/shared-assets/assets/shared/hardware/`. Open a device's draw.io file in diagrams.net, retain the canvas dimensions and stable IDs, and move the existing label, anchor, connector or image geometry. Native and compressed single diagrams are supported. `UserObject` wrappers can hold stable IDs and labels. The supported vocabulary is the existing canvas, embedded PNG/JPEG artwork, labels, anchors, connectors with optional waypoints, masks, presentation fields and footer; arbitrary draw.io shapes, grouped transforms and HTML formatting are not a general-purpose export contract.

Generate into a new staging folder:

```sh
node scripts/asset-authoring/export-hardware.mjs --hardware-root /path/to/hardware-copy --output /path/to/new-staging-folder
```

The tool validates and renders all devices before writing. Existing output folders are rejected; the source folder and published bundled SVGs are never overwritten. Manifest paths must stay relative and cannot contain traversal, Windows separators/drive prefixes or duplicate output names. Invalid visible geometry, ambiguous diagrams, duplicate cells and incomplete raster image payloads are rejected with source context. Image signature/end-marker and base64 checks detect incomplete payloads; they are not a general image decoder.

Review staged SVGs visually before promoting any edited assets. Commit the approved source and derived SVG together with updated bundle provenance. New source creation/bootstrap and hardware-authoring UI are separate migration work; do not recreate an edited source from its SVG.

Watermarks remain optional: add `buttonNumber=N;` to a label style and pass `--watermarks` with `--output`, or set `includeButtonNumberWatermarks` in the copied `export-config.json`. Default published graphics remain clean. Preserve image copyrights and source attribution in `SOURCES.md`.

CI tests Linux and Windows and independently requires 80% line and 80% branch coverage for every exporter module. The .NET layers retain their existing independent gates. Exact SVG parity does not claim full consumer PNG/documentation/package parity or in-game acceptance.
