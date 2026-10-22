# Shared DCS and Elite Dangerous workspace

Implements the approved direction in #312. The workspace uses three vertical regions:

| Region | Devices | Assignments & Testing | Reference Sheets |
|---|---|---|---|
| Controllers | Persistent profile controller selection | Same selection across assignment and test tasks | Same controller context |
| Tools | Native controller link / drawing controls | Searchable command catalog, or DCS test events | Native reference options / page list |
| Work area | Optional canonical drawing preview | Controller artwork with selected-input inspector below | Reference preview |

Each boundary uses `WorkspaceDivider` from `Themes/Controls.xaml`. Drag either boundary,
or focus it and press Left/Right. Nested three-column grids retain the existing
`WorkspaceLayout` persistence and bounded proportion restore. Each game/task has its own
preference key; training continues to disable persistence. Minimum widths keep controls
usable and controller names wrap. Secondary, longer forms scroll inside their own pane.

## Presentation ownership

- `ModuleWorkspaceWindow.Controllers.cs` connects the DCS rail to existing view selectors;
  it coordinates presentation selection without editing native identities or profile order.
  The stable feature keys remain available to training and automation. Assign/Test modes
  reuse cached native views, including the existing frozen input-test snapshot behavior.
- `EliteAssignmentsView` composes `EliteCommandBrowserView`,
  `EliteAssignmentArtworkView` and `EliteAssignmentEditorView`. Its forwarding properties
  preserve the existing presenter and automation contracts. Only the selected-command
  readout crosses child namescopes through an explicit binding.
- `WorkspaceDrawingPreview` is shared by both games and uses the canonical SVG pipeline.
  It clears stale artwork when a controller has no drawing; a drawing is never required
  to author native inputs.
- Global Save/Discard remain separate from applying an assignment. Native exporters,
  backup/review behavior, file names, validation and edition enforcement are unchanged.
- DCS-specific Modifiers, Commands, Pages and Documentation remain available alongside
  the common tasks. DCS Reference Sheets continue to use native kneeboard pages.

Both games use the shared fixed-size toolbar resources for feature actions. Theme colors
remain DynamicResource references; no palette-specific colors are introduced. ED's
configuration CRUD, import, profile name and destination settings are grouped into a
scrollable disclosure rather than consuming permanent working height.

## Verification

`UnifiedWorkspaceSmoke` exercises both divider boundaries through WPF keyboard events,
checks selected-controller continuity across DCS Devices/Assign/Test, checks ED command
and inspector geometry, smaller window size and 125% presentation scaling, and compares
native export/snapshot state before and after presentation changes. CI captures actual
DCS/ED assignment, ED device/reference and enlarged UI screenshots. Existing Windows
assignment, controller lifecycle, reference export, edition, appearance and interactive
training scenarios continue to run.

The independent 80% line / 80% branch / CRAP <= 50 checks remain unchanged for each
production assembly. WPF presentation behavior is exercised by the Windows smoke suite.
