# Compact workspace implementation and acceptance

Related enhancement: [#264](https://github.com/ScottyMac52/DCS-IPIA/issues/264).
The [interactive mockup](workspace-layout-concepts/workspace-interactive.html)
illustrates the direction; its extra sample editors/content are not requirements.

## Presentation behavior

The module shell retains its existing ListBox navigation, feature keys, cached
views, action commands and context. Navigation is arranged as horizontal tabs.
Import diagnostics remain visible when present. Source/import details are
available on demand. The compact Build & export popup retains the original
buttons and handlers; output progress/results remain visible in the footer.
Save/Discard and feature-specific actions remain in the
shared horizontally scrollable action strip.

Assignments places the existing command manager left of the controller. Search
remains visible; filters and embedded catalog authoring expand on demand.
Selected-input feedback remains visible; label/generic-name fields expand below
the drawing. Input Test places events and modifier legend left of the controller.
Other feature editors keep existing functionality with flexible split panels.

Fit includes the complete existing drawing dimensions and callouts. It follows
viewport resizing while Fit mode is active. Manual zoom turns Fit mode off and
uses the existing scrollbars for panning; resizing does not reset manual zoom.
Kneeboard previews also offer Fit. This does not alter controller assets, SVG
composition, generated kneeboards or output formats.

## Preferences and training

`%LOCALAPPDATA%\DCS-IPIA\workspace-layout.json` stores splitter proportions,
disclosure state, and zoom/Fit mode. It contains presentation numbers, not module
content. Missing/corrupt/unwritable preferences fall back without blocking edits.
Restored split proportions are clamped and constrained by minimum pane sizes.
To reset the workspace layout, close the application and delete this file.
Existing theme/background/control-opacity settings and edition restrictions are
unchanged. Absolute window coordinates are not restored from another monitor.

Training disables reading/writing these preferences for its mounted workspace.
It uses Fit instead of a fixed assignment scale, reveals disclosed training
targets, and highlights visible output status. It still invokes the real original output actions.
Both complete training scenarios must pass rendered smoke acceptance.

## Changes outside view files

- `WorkspaceLayoutStore.cs`: isolated local JSON preferences, safe defaults and
  atomic replacement; no settings database migration or domain contract changes.
- `WorkspaceLayout.cs`: presentation-only attached behavior for persisted
  splitters/disclosures and training preference isolation.
- `DiagramViewport.cs`: viewport scale/Fit behavior shared by existing drawings.
- `Training/TrainingActions.cs`: Fit and disclosed-target visibility; no training
  scenario content or business operations are replaced.
- `Training/TrainingWindow.xaml.cs`: fits the mounted compact workspace to the
  training viewport so both drag endpoints remain visible; retains the creation
  form's existing scroll extent.
- Unit/smoke tests and test project source linkage: verify storage cross-platform
  and real WPF layout on Windows. Smoke runs use an isolated preference path.

## Windows visual acceptance

Start with a logical client area around 1024 × 640 DIP. The window minimum is
900 × 540 DIP; this lower limit is safe overflow, not a claim that every expanded
editor fits without scrolling. Record actual client area, monitor working area,
and DPI for each review. Physical resolution alone does not determine usable
logical space at higher scaling.

Review 1280 × 720, 1366 × 768 and 1920 × 1080 displays at 100%, 125% and 150% Windows
scaling. At 200%, check scrolling, focus and reachability without requiring every
pane to fit simultaneously. Check all four skins, custom backgrounds/control
opacity, high contrast, Free and Owner access.

1. Open a real Viper TQS Mission Pack module; choose Assignments and Fit. Confirm
   complete artwork/callouts, left/top alignment, and readable command selection.
2. Resize both window and divider; use manual zoom and pan, then resize again.
   Confirm manual scale remains. Re-enter Fit and confirm the full bounds fit.
3. Search/filter and drag commands; select inputs, rename labels, clear via
   context menu, tune an axis, and test reserved modifiers. Switch module/UI
   layer, Save/Discard, change features and reopen the module.
4. Expand the inspector, adjust divider/zoom, close and reopen the application.
   Confirm preferences persist. Reopen on a smaller monitor; both panes must be
   reachable. Try corrupt preference JSON and reset by deleting the file.
5. Navigate all eight views by mouse and keyboard. Check focus, scrollbars,
   action enablement, independent pane scrolling and output-menu keyboard access.
   Validate, snapshot/documentation/complete-release export and OvGME build must
   retain existing semantics and diagnostics.
6. Complete both training scenarios, including drag endpoints, context menus,
   axis editor, live test highlights and real package outputs. Confirm training
   did not change the user's preference file or module data.

Automated Windows smoke exercises horizontal navigation, right-hand controller
space, complete drawing fit, manual zoom/panning, cache retention, preference
restoration/clamping, menu reachability and live Input Test divider behavior.
The existing training, context-menu, modifier, output and theme smoke checks
continue to run. Manual VM/DPI acceptance remains necessary before release.
