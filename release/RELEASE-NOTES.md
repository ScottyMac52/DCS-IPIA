DCS-IPIA v0.0.0.5 adds the saved module creation, command loading, device/modifier management, kneeboard summary authoring and snapshot-only output workflow developed from PR #109 through this release.

- Load aircraft commands from IPI JSON or DCS Controls HTML, review changes, then save/discard locally.
- Author device and summary pages/cards, physical device instances, modifiers, assignments, axis tuning and labels in SQLite.
- Validate saved modules, export/restore portable snapshots without overwriting existing identities, and reproduce previous outputs from captured inputs.
- Export module documentation and deterministic Complete releases with OvGME ZIP, guides, snapshot, internal/external checksums and independent frozen-input verification.

Release gates include Linux/Windows tests, independent 80% line and 80% branch coverage for each core layer, WPF editor/export smoke, inventory/provenance checks and formatting. The exact self-contained portable ZIP and setup.exe are tested through portable/installed/archive startup, isolated SQLite, shipped hardware integrity, native PNG rendering, installed payload hashes and silent install/uninstall. TEST-EVIDENCE.json records the source commit and executed artifact checks; SHA256SUMS.txt covers every release asset.

Install with setup.exe or extract the portable win-x64 ZIP. Back up your existing local database before upgrading (normally %LOCALAPPDATA%\DCS-IPIA\ipia.db). The installer leaves local user data intact.

The application release does not declare DCS-Common/consumer parity complete. #83/#9 remain open for exporter/scaffold/version adapters, pinned consumer golden comparisons and rollback/source-of-truth decisions. #111 and full manual DCS hardware acceptance remain open. Existing consumer workflows and historical releases remain operational.
