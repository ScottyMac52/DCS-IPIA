# Workstream 3: pinned shared catalog import

The first read-only Workstream 3 adapter accepts the three authored DCS-Common texts at an exact commit: `assets/shared/hardware/manifest.json`, `assets/shared/ui-layer/functions.json`, and `assets/shared/ui-layer/hardware-overlays.json`. Callers supply the source paths, commit SHA, and unmodified JSON to `ISharedCatalogImporter`. No repository checkout, path traversal, database write, or network access occurs inside this importer.

The result contains stable physical device IDs and aliases, the canonical UI Layer command catalog, overlay templates with instance selectors and function-to-control assignments, and per-source SHA-256 over the supplied UTF-8 text. Parsing rejects missing and ambiguous device identities, unknown referenced functions or devices, unsupported UI Layer schema versions, malformed documents, and invalid provenance. Failed imports return diagnostics and no partial result. Repeating an import yields the same ordered values and hashes.

The fixtures are copies of the DCS-Common authored files at `dd3d9cff8997c57ebfad36066ec4caf06bdbe52e`, the pinned Workstream 1 inventory commit. The UI Layer catalog authority is DCS-Common. The historical DCS-UI-Layer repository is not an import input.

This slice preserves overlay control keys as source identities. Graphical control geometry and the ordered `scaffold-device-map.json` recognition rules require separate import and reference validation; this adapter does not infer them. Consumer Lua/HTML/catalog data, module overlays and pages, SQLite persistence for the complete domain graph, and source-byte preservation remain subsequent Workstream 3 tasks. No production consumer is switched to DCS-IPIA.
