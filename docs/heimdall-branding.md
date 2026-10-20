# HEIMDALL branding and compatibility

HEIMDALL — Simulation Control Manager replaces the public DCS-IPIA name. The large bridge H/square tactical display lockup includes the product name and descriptor; separate color and monochrome square images are application icons.

Open `HEIMDALL.sln`. All ten projects and their namespaces use `Heimdall.*`; the WPF namespace is `Heimdall.App`, while its executable/assembly remains `Heimdall.exe`/`Heimdall`. Scripts, CI paths, tests, coverage filters, XAML classes, resources, and generated reference material follow these names. The repository URL remains unchanged.

## Startup icon repair

The first branding release used PNG-compressed ICO frames that failed Windows image decoding during static branding initialization (`0x88982F60`). ICO generation now writes explicit 32-bit BGRA bitmap frames, one color plane and aligned AND masks at all nine sizes. A regression test validates every frame and payload. WPF loads the PNG window icon directly. Compiled Windows smoke tests still decode both ICOs and initialize the window assets before release.

## Storage and compatibility

Fresh installations use `%LOCALAPPDATA%\HEIMDALL` and `%LOCALAPPDATA%\HEIMDALL-TestProfiles`. Existing `DCS-IPIA` production/test-profile directories are resolved in place. An existing primary database takes precedence; a newly created HEIMDALL log directory cannot hide a legacy database. No database files are moved, copied, renamed or discarded by startup.

New logs are `%LOCALAPPDATA%\HEIMDALL\Logs\heimdall-YYYYMMDD.log`. Categories use HEIMDALL namespaces. The installer configuration helper uses the same production-root selection. Existing owner-license and layout files remain discoverable under their old paths; fresh files use `HEIMDALL-Licensing` and `HEIMDALL` respectively. New signed owner licenses use the `HEIMDALL` product claim; the verifier still accepts valid signatures with the exact legacy `DCS-IPIA` claim. Reset backup destinations exclude both generations of profile directories.

New module snapshots, full backups and editable mapping exports use `.heimdall-module.json`, `.heimdall-backup.zip` and `.heimdall-mapping.zip`. Import dialogs/readers continue to accept existing files. Preview/parity scripts use `HEIMDALL_MAPPING_PREVIEW` and `HEIMDALL_PARITY_OUTPUT`; smoke tooling retains their legacy environment aliases.

The remaining `ipia.db`, backup manifest names, saved game-workspace extensions, embedded workspace source keys (`config/ipia-*.json`), draw.io/SVG metadata and exporter-version tokens are persistence/provenance contracts. They remain readable without a data-format migration. Historical release/audit records and upstream source assets retain their original names. These compatibility identifiers do not name the application or its source projects.

The installer retains its AppId and previous installation directory, and removes the obsolete `Dcs.Ipia.App.exe` and old product shortcuts. Free/Premium game-entitlement changes remain tracked separately in issue #295.

## Asset maintenance

See [artwork provenance](../src/Heimdall.App/Branding/PROVENANCE.md). Regenerate with `python scripts/generate_heimdall.py` (Pillow), then run `python scripts/check_branding_assets.py` and `python -m unittest discover -s tests/ci`. Build `HEIMDALL.sln` and run the Windows smoke suite before shipping. Production builds use committed assets.
