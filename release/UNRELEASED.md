# HEIMDALL — Unreleased

DCS-IPIA is now HEIMDALL — Simulation Control Manager. This update replaces the application name, executable, product artwork, application icons, installer branding, help, and generated-document branding.

Existing profiles, workspaces, and licenses keep their compatibility identifiers. The installer retains its upgrade identity and previous installation directory. See the [branding guide](../docs/heimdall-branding.md) for asset sources and compatibility details.

This does not change Free/Premium game availability. Published v1.1.1.0 release notes and version remain historical; update them together when preparing the next release.

## Startup repair and source rename

Fixes the Windows image-decoder failure while opening the main window. WPF window icons now load PNG directly; executable/installer ICOs use validated 32-bit bitmap frames and transparency masks.

The solution is now `HEIMDALL.sln`; all projects and namespaces use `Heimdall.*`. Training text, log filenames/categories, distribution notices, new exports, scripts and documentation follow the new name. Existing profiles, signed licenses, backups and workspace formats remain accessible through explicit compatibility paths. See the branding guide for the identifiers retained for data compatibility.
