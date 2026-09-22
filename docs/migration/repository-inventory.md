# Phase 1 Workstream 1: repository inventory and ownership map

Audit date: 2026-09-22  
Roadmap issue: [#10](https://github.com/ScottyMac52/DCS-IPIA/issues/10)  
Decision: inventory only. Existing repositories, tags, releases, issues, and workflows remain operational and authoritative until their individual parity gates are approved.

## Classification key

| Code | Classification | Meaning |
| --- | --- | --- |
| **A** | Authored source | Human-maintained data, code, geometry, metadata, templates, or documentation source. |
| **S** | Immutable snapshot | Canonically ordered, schema-versioned, provenance-bearing data used for review, rebuild, and rollback. This is a DCS-IPIA target; the audited repositories do not currently provide it. |
| **G** | Generated artifact | Reproducible Lua, Markdown, SVG/PNG, manifest, package, checksum, or installer output. It does not become authoritative merely because it is committed. |
| **C** | Compatibility adapter | Proven parser, renderer, exporter, projection, or packaging behavior retained behind a DCS-IPIA boundary until equivalence is accepted. |
| **R** | Release-only output | Tag, GitHub Release, uploaded ZIP/checksum/installer, and its immutable public history. |

## Audit method and scope

The inventory was made from each repository's recursive `main` tree at the exact commit below, plus its GitHub Release collection. Counts are evidence for scope, not acceptance criteria. A future change after these commits requires an inventory refresh before that repository is imported.

| Repository | Audited `main` commit | Files | Releases | Latest release |
| --- | --- | ---: | ---: | --- |
| `DCS-Common` | `dd3d9cff8997c57ebfad36066ec4caf06bdbe52e` | 211 | 84 | `v5.5.6.0` |
| `DCS-UI-Layer` | `844ea08e05d649a339e9424a2025decd290e5463` | 49 | 10 | `v2.5.0` |
| `DCS-UH-1H-Huey` | `77d924cce220355860e8f026cff3524bac531e29` | 51 | 5 | `v3.1.1` |
| `DCS-F4U-1D-Components` | `278751c9555b48efa7b4e6ce18852e295e70c518` | 68 | 16 | `v3.2.2` |
| `DCS-F-14B-U-Components` | `72eb711dc4257422190fea0e0ec43dac0058816d` | 88 | 36 | `v3.5.1` |
| `DCS-F-16C-Components` | `e15ffae260316333bc454837e5260cb6e80bb9cd` | 80 | 26 | `v3.1.5` |
| `DCS-FA-18-Components` | `1e3d2a7d870a784f2f88806ac7e93dfb508d09c7` | 56 | 11 | `v3.6.0` |
| `DCS-T-45-Goshawk` | `8208dc81dab83f106c8f78245072b9afff2e4eec` | 67 | 6 | `v3.2.1` |
| `DCS-F-100D-Super-Sabre-aka-Hun` | `4129a239830184bea333cf35e565319a0ebe99f0` | 64 | 7 | `v3.0.2` |

The active consumer set is the seven module repositories above plus the UI Layer projection repository. `DCS-F-16C` contains only two files and no current component pipeline. `DCSMissionInspector`, `DCS-Missions`, `DCS-KneeboardPreview`, `dcs-lua-datamine`, and `VAICOMPRO-Community` are adjacent inputs or utilities, not current DCS-Common consumers. They must not be silently imported; a future executable or data dependency must first be added to this inventory with provenance and an owner.

## DCS-Common inventory

### Source, data, tools, and tests

| Current item | Evidence at audited commit | Class | Target DCS-IPIA owner | Preservation requirement |
| --- | --- | --- | --- | --- |
| Shared-hardware catalog | `assets/shared/hardware/manifest.json`, `scaffold-device-map.json`, 12 Lua device definitions | **A** | `data/shared-hardware/` and Domain identities | Preserve stable device/control IDs, aliases, instance semantics, and the source path/commit. Reject ambiguous IDs. |
| Hardware visual sources | 12 uncompressed `drawio/*.drawio`, 20 image inputs under `source/`, `SOURCES.md` | **A** | `data/shared-hardware/` assets and provenance | Preserve source bytes, licenses/attribution, draw.io connector IDs, and case-sensitive paths. Binary inputs are copied byte-for-byte. |
| Hardware visual projections | 12 `svg/*.svg` files paired with the 12 draw.io sources | **G** | Rendering golden fixtures, then `Dcs.Ipia.Rendering` | Retain current SVGs as parity evidence; regenerate from the preserved source and compare deterministically before promotion. |
| Canonical UI Layer | `functions.json`, `hardware-overlays.json`, five joystick profiles, keyboard profile, and `modifiers.lua` | **A** | `data/ui-layer/`, Domain, and Application | Preserve command IDs, device identities, modifier modes/chords, removed/default bindings, GUID-bearing source filenames, and module-isolation rules. |
| Node behavior | 23 `scripts/` files covering import/scaffold, Lua parsing, shared hardware, UI Layer, draw.io export, rendering, documentation, and packaging | **C** (code is **A**) | `Dcs.Ipia.Dcs`, `Dcs.Ipia.Rendering`, `Dcs.Ipia.Packaging` | Characterize existing behavior before replacement. Keep adapters read-only against source repositories and retire only after measured parity. |
| Consumer scaffolding templates | 13 files under `templates/consumer/` | **A/C** | Application orchestration and Packaging templates | Preserve token substitution, path/case behavior, generated workflow contract, and consumer-specific escape hatches. |
| Automated tests | 20 Node tests under `test/` plus 20 C# scaffold tests | **C** (test source is **A**) | Corresponding DCS-IPIA layer tests and golden fixtures | Port behaviors, not merely filenames. Keep determinism, embedded-image, draw.io/SVG, UI Layer, package, and workflow contract cases. |
| WPF importer/scaffolder | 30 source files under `tools/DcsConsumerScaffold/src/` | **C** (code is **A**) | Application services and `Dcs.Ipia.App` | Preserve observable import/edit/preview behavior while moving domain and persistence rules out of WPF. |
| Documentation and generation rules | 10 `docs/` sources plus README workflow contracts; Markdown generators in `scripts/` | **A/C/G** | `docs/`, Application metadata, and Rendering | Preserve authored guidance separately from generated module/device mapping documents; compare generated Markdown byte-for-byte or record intentional differences. |
| Schemas | `schemaVersion` fields and validation tests embedded in JSON/Lua contracts; no standalone schema files exist | **A** | Domain schema plus versioned files under `data/schemas/` | First capture the implicit v1 constraints as explicit versioned schemas. Do not infer missing values during import without a diagnostic. |
| Build/package workflows | `.github/workflows/build.yml` and `release.yml`; PowerShell builders; Node versioning scripts | **C/G** | Packaging and CI adapters | Preserve package layout, clean replacement semantics, validation, generated-commit provenance, and current environment inputs. |
| DCS-Common app workflow | `.github/workflows/scaffold-app.yml` delegates to `shared-github-workflows`; four-part app tags | **C/R** | DCS-IPIA CI/release workflow | Preserve signed-off tag/version behavior and `setup.exe` plus portable ZIP assets independently from module release semantics. |
| Published history | 84 releases; latest `v5.5.6.0` includes `DcsConsumerScaffold_v5.5.6.0.zip` and `setup.exe` | **R** | GitHub remains system of record | Never rewrite, move, or delete tags/releases/assets. Record source release/tag in imported provenance. |

### Current release behavior

The reusable consumer build checks out `DCS-Common` into `.dcs-common`, exports `DCS_COMMON_ROOT`, generates and validates kneeboards, optionally commits generated pages, builds and validates OvGME/complete bundles, and uploads artifacts. The reusable release workflow:

1. is manually dispatched with a semantic-version bump;
2. selects the latest three-part `vX.Y.Z` tag;
3. regenerates and may commit kneeboard output to `main`;
4. builds and validates both package forms;
5. publishes ZIP assets and `SHA256SUMS.txt` to a GitHub Release.

This behavior is a compatibility contract, not the DCS-IPIA end state. Snapshot-only builds must eventually remove the mutable checkout and generated-commit side effect without changing the accepted package contents.

## Consumer inventory

All seven module consumers currently call `DCS-Common/.github/workflows/{build,release}.yml@main`. Their scripts resolve `DCS_COMMON_ROOT`, falling back to `.dcs-common`, and import DCS-Common renderer, shared-hardware, UI Layer, documentation, and packaging behavior. No consumer pins a DCS-Common commit. This mutable dependency is a migration risk and is preserved until a snapshot-only build proves parity.

Common ownership boundary:

- **A:** `config/*.dcs-scaffold.json`, command catalogs, `config/kneeboard.json`, UI Layer overlay/override decisions, `src/Config/Input/**` Lua profiles/modifiers, local image/source assets, exception scripts, release notes, and authored documentation.
- **G:** generated kneeboard SVG/PNG, generated control-mapping/device Markdown, package staging, ZIPs, checksums, and manifests.
- **C:** checked-in build/test/version scripts and calls into `.dcs-common`; these remain adapters until DCS-IPIA equivalence is accepted.
- **R:** three-part semantic-version tags, GitHub Releases, complete and OvGME ZIPs, and `SHA256SUMS.txt`.
- **S:** a future per-module DCS-IPIA snapshot containing normalized authored data and provenance; generated files and release assets are not copied into it as authoritative data.

| Consumer / target owner | Authored input and generated-output evidence | Exceptions and release behavior | Effort / risk |
| --- | --- | --- | --- |
| `DCS-UI-Layer` → `data/ui-layer/` plus Packaging projection | 5 profiles/pages; 6 Lua inputs; 5 source and 6 projected kneeboard files; collision fixtures; reserved inputs and semantic-modifier overrides | No `.github/workflows/` files at the audited commit despite 10 historical releases. Latest `v2.5.0` has Complete/OvGME ZIPs and checksums. Treat restoration or replacement of release automation as an explicit decision. | **M / high** — global runtime namespace, collision behavior, modifier semantics, and undocumented current release path. |
| `DCS-UH-1H-Huey` → `data/modules/uh-1h/` | 5 profiles/pages; 6 Lua inputs; overlay, device override, instance-role, and scaffold decisions; generated module/device docs and 13 kneeboard source/projection files | Reference vertical slice. DCS HTML command source; authored MOZA Hornet-grip device decision; nine deliberately removed profiles. Latest `v3.1.1`. | **L / high** — first full parity/rollback proof sets the pattern for every later import. |
| `DCS-F4U-1D-Components` → `data/modules/f4u-1d/` | 10 profiles/pages; 11 Lua inputs; local command catalog; generated docs and 23 kneeboard files | Two physical Logitech quadrant instances; a scaffold JSON file is present under `src/Config/Input` and must be classified during import, not treated as a Lua profile. Latest `v3.2.2`. | **M / medium** — repeated devices and catalog import, otherwise follows the common pattern. |
| `DCS-F-14B-U-Components` → `data/modules/f-14bu/` | 10 profiles/pages; 12 Lua inputs; 11 local kneeboard assets; PDCP, MFD, Warthog, VAICOM, modifiers, and generated docs | AutoHotKey bridge and the richest local asset/license set. DCS HTML command source. Latest `v3.5.1`. | **XL / high** — broadest device/modifier/asset surface and strongest regression exposure. |
| `DCS-F-16C-Components` → `data/modules/f-16c-50/` | 8 profiles/pages; 9 Lua inputs; local command catalog; 15 local kneeboard assets; generated docs | AutoHotKey bridge plus five device-specific PowerShell validators. Latest `v3.1.5`. | **L / high** — custom validation and automation must remain testable; significant binary/source asset provenance. |
| `DCS-FA-18-Components` → `data/modules/fa-18c-hornet/` | 9 profiles/pages; 10 Lua inputs; raw and cohesive command catalogs; 24 kneeboard source/projection files | AutoHotKey bridge. No `docs/` files at the audited commit, so documentation absence must be confirmed as intentional or regenerated. Latest `v3.6.0`. | **L / high** — dual catalogs and missing committed documentation baseline create ambiguity. |
| `DCS-T-45-Goshawk` → `data/modules/t-45/` | 9 profiles/pages; 10 Lua inputs; local command catalog; generated docs and 24 kneeboard files | Authored-device decisions for MFD instance 3, MOZA AB9, and WINCTRL ICP; one removed profile. Latest `v3.2.1`. | **M / medium** — explicit authored-device exceptions need lossless modeling. |
| `DCS-F-100D-Super-Sabre-aka-Hun` → `data/modules/f-100d/` | 11 profiles, 9 pages, 10 Lua inputs; local command catalog; generated docs and 21 kneeboard files | Profile/page count is intentionally non-1:1 and Warthog throttle is removed. Latest `v3.0.2`. | **M / medium** — must preserve non-rendered profiles and explicit removal without synthesizing pages. |

Each module release workflow is manually dispatched with a major/minor/patch choice and delegates version calculation, generation, validation, packaging, checksums, tagging, and release creation to mutable `DCS-Common@main`. The parity baseline for a module is its latest release named above, not an uncommitted local build.

## Target ownership map

| Concern | DCS-IPIA owner | Boundary rule |
| --- | --- | --- |
| Stable module, device, instance, control, command, binding, modifier, page, and release identities | `Dcs.Ipia.Domain` | No WPF, SQLite, filesystem, GitHub, or DCS parser dependencies. |
| Import/edit/export use cases and diagnostics | `Dcs.Ipia.Application` | Transport-neutral interfaces; import is read-only against source repositories. |
| SQLite authored working state | `Dcs.Ipia.Infrastructure` | Local implementation detail; never a release-build input. |
| DCS Lua/HTML/catalog and legacy DCS-Common behavior | `Dcs.Ipia.Dcs` | Compatibility adapters with characterization and golden-file tests. |
| Canonical shared hardware and UI Layer authored data | `data/shared-hardware/`, `data/ui-layer/` | Versioned identities and provenance; visual source assets stay distinct from generated SVG. |
| Per-module authored data | `data/modules/<module-id>/` | Module-owned decisions remain isolated; no cross-module overlay leakage. |
| Review/rebuild/rollback inputs | `snapshots/` | Immutable, schema-versioned, canonical, human-reviewable, SHA-256 identified. |
| Markdown, SVG, and PNG projections | `Dcs.Ipia.Rendering` | Deterministic generated output compared with current golden artifacts. |
| OvGME, complete ZIP, checksum, and installer projections | `Dcs.Ipia.Packaging` | Consumes an immutable snapshot plus repository assets, never live SQLite. |
| Desktop interaction | `Dcs.Ipia.App` | UI orchestration only; no domain or persistence rules. |
| Published tags/releases/assets | Existing GitHub repositories | Remain in place and immutable until an explicit, separately approved retirement plan. |

## Preservation and promotion gates

1. Record repository, commit SHA, original path, schema/version, and content hash for every imported authored item.
2. Preserve original bytes for Lua, draw.io, images, licenses, and other inputs; normalized representations are additional data, not replacements for provenance.
3. Keep authored sources, immutable snapshots, and generated artifacts in separate roots and manifests.
4. Preserve case, DCS module IDs, device aliases, GUID-independent identities, device-instance roles, removed/default bindings, axis tuning, and modifier chords.
5. Retain latest-release artifacts as golden parity inputs and retain all historical tags, releases, issues, and URLs in their current repositories.
6. Report unsupported, missing, or ambiguous data. Do not silently default it during import.
7. Require byte-identical deterministic output or an owner-approved intentional-difference record for each generated projection.
8. Require snapshot-only rebuild and demonstrated restore before any existing pipeline ceases to be authoritative.
9. Keep DCS-Common and each consumer operational until its own gate passes; migration is per consumer, not a fleet-wide cutover.
10. Preserve the independent 80% line and 80% branch coverage gates for every production assembly. Inventory work does not relax or aggregate them.

## Workstream 1 disposition

This document supplies the inventory, classification, preservation rules, target owners, and per-consumer effort/risk estimate required by Workstream 1. Merge of the implementing pull request is the point at which the five Workstream 1 checklist items in issue #10 may be marked complete. It does not authorize Phase 2, repository retirement, workflow disablement, or destructive migration.
