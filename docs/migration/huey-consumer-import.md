# Pinned consumer source import

`ConsumerSourceImporter` reads supplied, committed module source texts without writing to either repository. The pinned Huey fixture at `77d924cce220355860e8f026cff3524bac531e29` characterizes six `config/*.json` files, five joystick `diff.lua` profiles, and `modifiers.lua`. DCS-Common remains the source for shared hardware and canonical UI Layer data. The importer does not follow machine-specific paths embedded in scaffold settings.

The module loader also reads committed `README.md`, `docs/**/*.md`, `packaging/ovgme/README.TXT`, the OvGME/build/test/version scripts, and `.github/workflows/release.yml` when present. It preserves their original text and repository/commit/path/SHA-256 provenance alongside the configuration and Lua. These references survive the SQLite editor workspace and immutable editor snapshot. Uncommitted checkout edits are ignored. The import does **not** interpret these files as editable assignments or copy their old output into a generated package.

The parsed result includes aircraft profiles and instances, bindings, modifiers, kneeboard page identities, canonical UI Layer usage, and cloned JSON roots. Authored labels, overlays, callouts, and original formatting remain in the pinned texts. It rejects contradictory device identities and active chords with undefined module modifiers atomically; removed binding history can retain old chords. Missing or invalid graphics receive explicit diagnostics.

This is a read-only source preservation slice under roadmap #9. It does not prove complete page persistence, documentation generation, OvGME package parity, or a source-of-truth transition. Generated output must be rebuilt and compared independently before the consumer repository can be replaced.
