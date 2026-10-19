# DCS-IPIA module workspace navigation

Select a saved module in the Modules window and choose **Open module**. The workspace opens directly on Assignments. The implemented compact shell uses horizontal feature navigation, cached views and shared Save/Discard and feature actions. See [compact layout and display acceptance](compact-workspace.md).

| Feature | User action |
| --- | --- |
| Devices | Add/remove or rebind physical units, edit device labels/headings, review axis defaults and apply a drawing. |
| Modifiers | Maintain aircraft and module UI Layer Hold/Toggle activators. |
| Assignments | Select device, scope and modifier layer; search commands on the left; assign by button or drag/drop to the right-hand drawing. Right-click callouts to change labels, clear assignments or tune axes. Modifier activators are reserved. |
| Input Test | Freeze current edits or saved state and test the selected device; inspect events and the modifier legend. Licensed access is required. |
| Commands | Load JSON/DCS HTML or maintain the aircraft command catalog. |
| Pages | Author device and summary pages, reorder cards and preview staged page values. Summary/card fields appear only when applicable. |
| Kneeboards | Select and fit a generated saved-state device or summary page. |
| Documentation | Review/import/remove module integration instructions. |

Use **Build & export** for Validate, snapshot, documentation, Complete release and OvGME actions. Save or discard pending edits first; output status remains visible in the footer. Sources and read-only import details remain available on demand; import diagnostics remain visible when supplied. They are not additional feature tabs.

Fit includes the complete drawing and callouts. Manual zoom keeps its scale when resizing; return to Fit for automatic fitting. Splitter/disclosure and zoom/Fit preferences are stored in the Windows-user-wide layout file. Training does not read or write that file. See the compact layout guide for reset instructions and the pending VM/DPI matrix.

Module controls and module-owned UI Layer assignments remain separate. Repeated physical devices retain distinct identities. Only a scope change resets the command Category filter to All; other valid selections and draft changes preserve it. **Save to database** commits the draft and **Discard draft** restores saved state. Source repositories remain read-only.

Free limits edits and outputs to supported workspaces; existing reserved/mixed modules remain read-only. [Edition policy](free-edition.md) applies regardless of navigation layout. Historical navigation issue #48 is closed; remaining keyboard/high-contrast/display acceptance is tracked in #191, with compact-workspace acceptance also recorded in #264.
