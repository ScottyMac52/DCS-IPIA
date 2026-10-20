# HEIMDALL branding

HEIMDALL — Simulation Control Manager is the public product name replacing DCS-IPIA. The bridge H and square tactical display form the product identity. The large logo includes the name and descriptor; the color and monochrome square assets are application icons.

The executable is `Heimdall.exe`. Window titles, help, training, generated document footers, installer shortcuts, release archives, and distribution notices use HEIMDALL. The installer keeps its existing AppId and previous install directory so upgrades retain their identity, and removes obsolete Dcs.Ipia.App executable and product shortcuts.

Existing profile directories, license product identifiers, environment variables, workspace formats, extensions, project names, namespaces, and `DCS-IPIA.sln` retain their compatibility identifiers. No profile migration or license reissue is required. Historical release and migration records retain their original names. This branding change does not implement edition entitlement changes tracked in issue #295.

Artwork masters and asset provenance are in [Branding](../src/Dcs.Ipia.App/Branding/PROVENANCE.md). Regenerate derivatives with `python scripts/generate_heimdall.py` using Pillow, then run `python scripts/check_branding_assets.py`. Build and Windows smoke checks remain necessary before shipping an installer.
