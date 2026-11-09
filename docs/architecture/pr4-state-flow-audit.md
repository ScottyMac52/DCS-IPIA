# PR4 state-flow audit

Audited source: `7c3c5796520ff69df6cb1b9611f7175727a9c944` and its Windows smoke logs.
The build passed. Startup passed; the remaining groups reached new failures in controller addition, failed hardware discovery, and reopening a saved configuration.

## Findings and corrections

| Boundary | Finding | Correction |
| --- | --- | --- |
| Browser to native editor | Three WPF browser adapters maintained independent editor keys. A guarded window refresh could publish a projection without completing its editor render. | `GameAssignmentsView` owns one `WorkspaceEditorObserver`. Only a successful native render accepts a revision; every native window flushes after releasing its refresh guard. |
| Hardware evidence | Hardware projection changes and scan completion do not necessarily change the native document. Content-only observation could retain obsolete input controls. | Editor revisions include browser projection builds and scan state independently of document content. Existing failed/obsolete scan assertions remain unchanged. |
| Selection authority | War Thunder's editor reconstructed command/record selection from controls during rendering. ED's renderer chose a navigation slot. | Renderers consume context command/record/slot addresses. Explicit interactions go through the browser; the ED native adapter supplies axis/button slot policy without WPF. Native pending-input fields remain adapter-specific. |
| Physical target lifetime | War Thunder relied on editor rendering to clear a physical target when navigation changed. | The transient physical target is scoped to workspace identity and navigation address, excluding pending input text. An old target is unavailable immediately after navigation changes. |
| Inactive cached view | The ED controller-addition smoke inspected an assignment editor before mounting it. | Keep the drawing/reference-choice checks on Devices, then mount Assignments, select the command, and retain the enabled-editor and actual assignment/export assertions. |
| Reopened session | The War Thunder smoke assumed the previously selected throttle remained selected after creating a new editing identity. | Assert a fresh identity and unchanged saved native text, explicitly select its throttle record, then retain Clear/Discard/export assertions. No implicit restoration of an old mutation target. |
| Shared interface placement | The earlier lifecycle interface was added under App despite the repository's shared-interface rule. | Move the same game-neutral contract to `Heimdall.Application.Interfaces`. |

## Ownership contract

1. The context owns workspace identity, navigation addresses and operation/activity state.
2. The browser validates command and record identities, projects native data, and publishes complete rows before notifying the shared view.
3. The shared view observes the last **completed editor render**, separately from the browser's cache and publication counters.
4. Native window refresh guards defer editor rendering without accepting its revision. Their final synchronization flushes it after controls are ready.
5. Pending input, pending names and action-gate changes do not reload a baseline over typed fields. Scan start/completion and evidence rebuilds do invalidate editor capabilities.
6. A presentation exception reports its diagnostic and leaves the render pending; it must not be classified as a failed native browser projection or clear valid navigation.
7. DCS selection-only editor updates do not reconstruct controller artwork. Native document changes still refresh the full assignment presentation.

## Regression coverage and verification

- `WorkspaceEditorObserverTests`: deferred selection/evidence revisions, pending-field preservation, scan completion, repeated dirty-content edits and session replacement.
- `AssignmentBrowserNativePolicyTests`: ED binding-slot policy without editor writeback.
- `BrowserEditorConformanceSmoke`: real WPF shared views over all four production context/browser adapters; guarded content changes, no-op refresh, scan completion, failed-render retry, no persistence writes, and War Thunder physical-target scope.
- Architecture checks reject control-to-navigation writeback and duplicate native editor keys.
- Existing failed/obsolete hardware-discovery tests remain strict. Existing assignment/export/clear/preservation assertions remain; their feature mount and record selection are now explicit.

Executed here: three focused Python architecture checks passed; `git diff --check` passed. The available source subset's layout comparison added zero findings and removed the misplaced-interface finding. This is not a whole-repository layout or behavior pass.

The correction environment has no .NET SDK or Windows runtime. Compilation, the added unit tests and WPF conformance have **not** been executed here. No smoke or coverage pass is claimed for these changes.

Run the focused Windows entry point first:

```powershell
.\tests\browser-state-smoke.ps1 *>&1 | Tee-Object .\pr4-browser-state.txt
```

It builds the solution, runs the editor/browser policy unit regressions, and invokes the dedicated `browser-state` WPF group. That group is also exercised by the full games conformance sequence. Once focused checks pass, run the full smoke and existing 80/80/50 acceptance gates. Keep PR #364 draft until those results and native UI review are recorded.
