# Game presentation ownership

Game-specific WPF types live beside their native presentation collaborators inside
`Heimdall.App`. Folder ownership and CLR namespace ownership follow the same rule.

| Area | Folder | Namespace |
| --- | --- | --- |
| Shared presentation | `src/Heimdall.App/Games/` | `Heimdall.App.Games` |
| Elite Dangerous presentation | `src/Heimdall.App/Games/Elite/` | `Heimdall.App.Games.Elite` |
| Squadrons presentation | `src/Heimdall.App/Games/Squadrons/` | `Heimdall.App.Games.Squadrons` |
| War Thunder presentation | `src/Heimdall.App/Games/WarThunder/` | `Heimdall.App.Games.WarThunder` |

ED's workspace, Devices, Assignments, artwork, editor, reference sheets, axis tuning,
export review and modifier-choice types now live in its game area. Squadrons'
workspace, Devices and Assignments types now live in its game area. Their existing
controller, browser, editing, drawing, profile and context components were already
in those namespaces.

`GameExportResolution` was consumed only by ED. It is now
`EliteExportResolution` in the ED area, retaining the same constructor and fields.

Shared workspace and assignment bases, navigation, generic dialogs, controller
services and hardware drawing presenters remain in the shared area. The existing
`GameWorkspaceServices` remains there because all native consumers use it; its
legacy ED backend/reference-matcher properties are preserved by this organization
change. Native backend projects continue to own file formats and rules rather than
WPF controls. War Thunder now follows the same ownership rule; see its
[workspace migration](war-thunder-workspace-inheritance.md).

## Compatibility

Each XAML view moves with its code-behind. Its `x:Class` and native element namespace
references identify the new CLR namespace. Application activation, smoke-test
imports and source-path tooling reference the new locations.

Class names, control names, existing appearance target identifiers, saved layout
keys, theme resource keys and native game/backend identifiers are preserved. No
saved profile, database or preference migration is required. The internal export
resolution type is not a serialized profile contract.

## Validation

Architecture checks require ED and Squadrons presentation types to occupy their
game folders/namespaces and check each moved XAML class identity. Existing
inheritance/ownership checks now read the new paths; theme checks also use the new
paths. The application and Windows smoke projects must cross-build together.

Run the full Windows smoke suite to validate generated XAML resource loading,
activation, native editing, appearance and layout behavior after the moves. A Linux
cross-build does not execute WPF. Native backend assemblies, measured coverage
scope and the existing 80% line / 80% branch / CRAP <= 50 gates are unchanged.
