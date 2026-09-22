# Phase 1 repository inventory and ownership map

Inventory date: 2026-09-22. Existing repositories, tags, releases, issues, and workflows remain operational and authoritative until their individual parity gate is approved.

| Repository | Current ownership | Phase 1 classification | Target DCS-IPIA owner | Effort / risk |
| --- | --- | --- | --- | --- |
| `DCS-Common` | parsers, shared-hardware catalog, canonical UI Layer, draw.io/SVG, rendering, templates, documentation and package/release scripts | authored source plus compatibility implementation | Domain/Application contracts; DCS, Rendering and Packaging adapters | XL / critical: broad shared blast radius and history requirements |
| `DCS-UI-Layer` | distributable global UI Layer inputs and releases | authored projection and release output | canonical UI Layer snapshot plus Packaging projection | M / high: global runtime namespace must remain module-isolated during composition |
| `DCS-UH-1H-Huey` | UH-1H profiles, modifiers, module overlay/utilization, kneeboard layout, docs and OvGME release | Phase 1 reference authored source and generated outputs | `data/modules/uh-1h`, immutable snapshots and generated projections | L / high: first measured equivalence and rollback pilot |
| `DCS-F4U-1D-Components` | F4U-1D profiles, module overlay/utilization, pages, docs and release | authored source plus generated outputs | future module snapshot/projection | M / medium; first post-Huey candidate |
| `DCS-F-14B-U-Components` | F-14B(U), PDCP, MFD, modifier, VAICOM, page, docs and release data | authored source plus generated outputs | future module snapshot/projection | XL / high: richest device/modifier and VAICOM coverage |
| `DCS-F-16C-Components` | F-16C profiles, devices, pages, docs and release | authored source plus generated outputs | future module snapshot/projection | L / high |
| `DCS-FA-18-Components` | F/A-18 profiles, devices, pages, docs and release | authored source plus generated outputs | future module snapshot/projection | L / high |
| `DCS-T-45-Goshawk` | T-45 module configuration and release | authored source plus generated outputs | future module snapshot/projection | M / medium |
| `DCS-F-100D-Super-Sabre-aka-Hun` | F-100D module configuration and release | authored source plus generated outputs | future module snapshot/projection | M / medium |

`DCS-F-16C`, `DCSMissionInspector`, `DCS-Missions`, `DCS-KneeboardPreview`, `dcs-lua-datamine`, and `VAICOMPRO-Community` were reviewed as adjacent repositories. They are not silently treated as consumer repositories: any data or executable dependency must be recorded explicitly before migration.

## Classification contract

- **Authored source:** human-maintained profiles, modifiers, catalogs, geometry, overlay/utilization, page configuration, metadata, templates, and documentation source.
- **Immutable snapshot:** deterministic, human-reviewable export with schema version, provenance, stable identities, canonical ordering, and SHA-256 identity.
- **Generated artifact:** Lua projections, Markdown, SVG/PNG kneeboards, manifests, ZIP/OvGME payloads, checksums, and installers. These never become authoritative merely because they are committed or released.
- **Compatibility adapter:** existing proven DCS-Common parser/renderer/package behavior retained behind an adapter and characterization tests until an equivalent DCS-IPIA implementation is accepted.
- **Release-only output:** tags, GitHub Releases, packages, checksums, and installer assets retained with their existing URLs/history.

## Preservation rules

1. Importers are read-only against source repositories.
2. No workflow, repository, tag, release, or issue history is deleted or disabled in Phase 1.
3. A snapshot records the exact source repository and commit in `provenance`.
4. CI/release builds consume immutable snapshots and repository assets, never mutable SQLite state.
5. Each consumer requires a measured equivalence report and explicit owner approval before its current pipeline can be retired.
