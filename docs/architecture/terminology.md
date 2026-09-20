# Terminology

| Term | Meaning |
| --- | --- |
| Module | A DCS aircraft or input namespace with a stable application identity and DCS ID. |
| Device | A physical or virtual controller identity; instances may have machine-specific GUIDs. |
| Binding | An assignment between a control and DCS command, optionally with modifiers or axis tuning. |
| UI Layer | Canonical cross-module functions plus module-owned overlays and tombstones. |
| Snapshot | Canonically ordered, immutable, reviewable export used by Git, CI, and releases. |
| Generated output | Replaceable documentation, kneeboard, Lua, or package content derived from a snapshot. |
| Migration | A versioned transactional SQLite schema change with backup and recovery requirements. |
| Equivalence | Measured agreement with an existing authoritative consumer release. |
