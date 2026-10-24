# HEIMDALL — Unreleased

DCS-IPIA is now HEIMDALL — Simulation Control Manager. This update replaces the application name, executable, product artwork, application icons, installer branding, help, and generated-document branding.

Existing profiles, workspaces, and licenses keep their compatibility identifiers. The installer retains its upgrade identity and previous installation directory. See the [branding guide](../docs/heimdall-branding.md) for asset sources and compatibility details.

This does not change Free/Premium game availability. Published v1.1.1.0 release notes and version remain historical; update them together when preparing the next release.

## Startup repair and source rename

Fixes the Windows image-decoder failure while opening the main window. WPF window icons now load PNG directly; executable/installer ICOs use validated 32-bit bitmap frames and transparency masks.

The solution is now `HEIMDALL.sln`; all projects and namespaces use `Heimdall.*`. Training text, log filenames/categories, distribution notices, new exports, scripts and documentation follow the new name. Existing profiles, signed licenses, backups and workspace formats remain accessible through explicit compatibility paths. See the branding guide for the identifiers retained for data compatibility.

## ED controller lifecycle

Adds reviewed controller removal and replacement alongside Add controller. Removal clears dependent modifier chords in full. Replacement transfers supported native inputs and modifiers, reports cleared bindings, preserves captured device indices and rechecks live capabilities before staging. Existing keyboard/mouse references, unrelated XML, selector files and live ED presets are preserved.

- ED game export now reviews unresolved controller references and their affected commands before writing files, with reviewed replace/clear/cancel repairs, fresh hardware checks and captured-index requirements for identical units.
- ED exports use the saved profile name consistently in filenames, native preset names and matching captured selectors. Exact duplicate scalar settings are normalized with a diagnostic; conflicting duplicates are blocked. Export status explains the separate manual installation step.

## Squadrons desktop workspace

- Fix captured button/hat records being presented as usable controls on linked
  axis-only hardware such as TPR pedals. Show per-record hardware verification,
  restrict the editor to enumerated destination controls, and enforce the same
  check in the application session. Preserve unsupported native records; failed
  or missing enumeration never offers generic inputs as a fallback.
- Add reviewed physical-controller link/replace/unlink for captured slots, rescan
  status and button/hat/axis identification using the shared foreground input
  service. Duplicate names retain explicit instance GUIDs; stale reviews and changed
  hardware block staging. Links save/reopen/discard independently of native slots
  and bindings. Native slot creation/removal and full-lineup game acquisition remain
  separate follow-up work.
- Add Star Wars: Squadrons to game cards, recent-workspace continuation and guarded
  game selectors for Premium/Owner users.
- Import captured controls, filter native bindings by device/context/input type/text,
  edit supported joystick records, and save/reopen/discard named profiles.
- Export a separate native review copy while preserving other assignments and settings;
  active-game deployment, hardware association, live testing and reference sheets remain pending.
