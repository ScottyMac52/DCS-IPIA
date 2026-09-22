# Immutable configuration snapshot v1

Snapshot v1 is the review, CI, rollback, and interoperability boundary for Option B. SQLite remains authoritative only for local interactive work.

Canonical rules:

- UTF-8 JSON without a byte-order mark;
- two-space indentation and exactly one trailing LF;
- camel-case property and enum names;
- absent optional values are omitted rather than emitted as `null`;
- modules sort by ordinal DCS module ID and stable module UUID;
- profiles sort by ordinal device identity and instance;
- modifiers sort by ordinal native name;
- bindings sort by profile UUID, section, command, disposition, and input key;
- reformer chords are de-duplicated and ordinally sorted;
- numeric values use `System.Text.Json`'s invariant shortest round-trippable form;
- snapshot identity is lower-case SHA-256 of the exact canonical UTF-8 bytes.

Every module includes provenance naming its source repository and immutable commit. Re-importing a snapshot into an empty migrated database and exporting it again must produce identical bytes and hash. A release build must work with the snapshot and repository assets while the interactive database and source repositories are unavailable.

Schema v1 covers modules, GUID-independent device identities and instances, roles, source profile names, native modifiers, added/changed/removed bindings, modifier chords, and sparse axis filters. UI Layer utilization, page/document/package metadata, and catalog geometry will extend the schema through versioned migrations; fields are not inferred from WPF controls.
