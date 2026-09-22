# Versioned domain schema v1.0

Workstream 2 extends the executable configuration contract introduced in PR #11. The domain remains independent of WPF, SQLite, HTTP, filesystem, and GitHub types.

## Version contract

| Concern | Current version | Rule |
| --- | ---: | --- |
| Domain schema | 1.0 | Major changes break readers; minor changes are backward-compatible additions. |
| SQLite migration | 2 | Persistence adapters advance transactionally and record applied migrations. |
| Immutable snapshot | 1 | Snapshot bytes and SHA-256 identity remain governed by the snapshot v1 canonicalization contract. |
| Release | four-part capable | Module releases remain three-part; application releases may include revision. |

## Stable identities and relationships

| Area | Stable identity and relationship |
| --- | --- |
| Modules | UUID plus unique DCS input module ID. |
| Devices | GUID-independent UUID derived from the stable device key; aliases are descriptive, never identity. |
| Instances and roles | UUID scoped to module and stable instance key; references one physical device and one module. |
| Controls and geometry | UUID scoped to device and stable control key; typed button/axis/hat/rocker/switch/encoder/keyboard control with optional named coordinate space. |
| Commands and catalogs | Command UUID references a versioned source catalog and retains the native DCS command ID. Shared catalogs remain aircraft-neutral. |
| Bindings | UUID references profile, physical control, and command; preserves disposition, input key, modifier chord, and sparse axis tuning. |
| Canonical UI Layer | DCS-Common is the sole authored source. Module utilization, device/control overlays, and tombstones reference stable module/catalog identities. Historical DCS-UI-Layer releases are parity evidence only. |
| Kneeboards | Pages reference module, optional device instance, ordered categories, stable page key, kind, and output order. |
| Documentation and packaging | Module-scoped output paths, package roots, include patterns, and titles remain metadata rather than UI behavior. |
| Provenance and publication | Repository, immutable commit, source path, content hash, snapshot version/hash, migration version, and release version are explicit. |

## Validation invariants

- IDs and stable keys are unique in their scope.
- Every foreign identity resolves inside the same schema graph.
- Axis tuning may reference only an axis command.
- UI Layer overlays cannot silently create devices, controls, commands, or modules.
- Kneeboard category and device-instance references must resolve.
- Collections are copied and canonically ordered at the domain boundary.
- No schema decision depends on WPF controls or transport/persistence representations.

This contract defines Workstream 2. Importers, future SQLite migrations, snapshot evolution, and projections consume it in later workstreams; they do not redefine it.
