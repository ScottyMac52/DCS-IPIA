# Shared controller drawing mouse-wheel behavior (issue #323)

All hardware drawing surfaces using `DiagramViewport` implement **one** mouse-wheel contract:

| Gesture | Behavior |
| --- | --- |
| Mouse wheel | Vertical scroll, without zoom |
| Shift + mouse wheel | Horizontal scroll, without zoom |
| Ctrl + mouse wheel | Zoom in/out around cursor position |
| Ctrl + Shift + mouse wheel | Zoom (Ctrl takes precedence) |

Wheel up moves toward the top or left and increases zoom; down moves toward the bottom/right and decreases zoom. Zoom respects the current slider's bounds and persists through the existing viewport settings. When the drawing fits and no scrollbar is needed, scrolling has no visible effect; Ctrl+wheel still zooms.

## Responsibility and migration

- `Heimdall.Application.DiagramWheelNavigation` contains pure modifier priority, scroll step, zoom factor, and cursor-offset calculations. Its unit tests lock that behavior independently of WPF.
- `Heimdall.App.DiagramViewport` attaches one `PreviewMouseWheel` routed event handler to each drawing `ScrollViewer`, using the shared policy; no simulator owns a separate wheel handler.
- Mainline consumers already include DCS `AssignmentsView`, Elite's assignments drawing, and the kneeboard drawing/preview surfaces.
- The active `fix/squadrons-controller-associations` branch has a newer drawing than main; **do not cherry-pick this into its old divergent code**. When Squadrons' drawing is migrated under #323, use the shared `DiagramViewport` contract and remove any one-off wheel handlers.

## Windows smoke acceptance matrix

1. Open DCS Assignments drawing; zoom enough to reveal both scrollbars. Wheel moves vertically, Shift+wheel horizontally, Ctrl+wheel zooms under the pointer. Parent panes must not scroll instead.
2. Repeat on Elite Assignments drawing. Wheel behavior must be identical to DCS.
3. Verify direction reversals, min/max zoom clamps and fit/reset behavior in both simulators.
4. Verify wheel gestures over callout regions and over the artwork, not only the scrollbar background.
5. Verify a zoomed kneeboard preview retains scrolling and pointer zoom; no regressed label/hit region alignment.
6. During Squadrons PR 5 migration, repeat the same matrix on its new shared drawing and verify its viewport-height clipping workaround is retained or made unnecessary by the new layout.

## Testing and quality

`DiagramWheelNavigationTests` exercises 4 modifier combinations, vertical/horizontal pixel deltas, symmetric zoom, clamping and cursor-offset preservation. This is a **source-level implementation**; run Windows build/unit tests, existing 80/80/50 gates, and WPF GUI smoke before merging. The new class does not change simulator native formats or saved game profiles.
