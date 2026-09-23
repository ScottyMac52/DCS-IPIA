# DCS-IPIA module workspace design preview

Review issue: [#20](https://github.com/ScottyMac52/DCS-IPIA/issues/20). This PR changes the WPF Phase 0 placeholder into a navigable design preview. The sample Huey context is clearly labeled and all data-changing controls are disabled. No import, edit, snapshot, or build behavior is implied.

## Navigation

| Area | User decision | Later application contract |
| --- | --- | --- |
| Overview | See the import → assign → validate path | Module discovery, validation summary |
| Assignments | Select device instance, layer, control and command in one workspace | Per-module binding CRUD, device geometry, chord validation |
| UI Layer | Inspect shared template separately from module overlay | Canonical shared read model and isolated module overlay editor |
| Kneeboard | Review summary and device pages in context | Deterministic renderer from immutable snapshot |
| Outputs | Review diagnostics, change list, snapshot and package contents | Validation, snapshot diff and snapshot-only package builder |

The future controller canvas uses the shared hardware source geometry. The MFD rectangle and OSB labels in this preview illustrate layout only; they are not hardware data. A searchable control list will mirror every selectable canvas control for keyboard and screen-reader access, including all OSBs and rocker positions. Repeated MFD instances remain explicit, with no cross-instance editing.

## Interaction rules

- Select a module first; keep its DCS input ID, source commit and snapshot status visible.
- Import is read-only against source repositories and reports ambiguous identity at the point of resolution.
- Separate shared DCS-Common UI Layer data from editable module-only overlay data at the point of action.
- Stage changes and review their scope, before/after values and diagnostics before saving through application services.
- Bindings expose default/removed dispositions, axis tuning and multiple hold/toggle modifier chords.
- Build packages only after validating a snapshot and showing its hash and resulting files.
- Text and iconography convey status without relying on color alone; support keyboard focus and Windows scaling.

## Review path

1. Open the Windows app and click each navigation section. Review hierarchy, terminology, empty states and action placement.
2. Review the Huey task flow in #20. Confirm landing page, graphic versus list emphasis and UI Layer navigation.
3. Implement live data only after the corresponding Workstream 3/4 application contracts and Phase 1 Huey parity tests exist. Maintain the independent per-assembly 80/80 gates and keep WPF presentation-only.
