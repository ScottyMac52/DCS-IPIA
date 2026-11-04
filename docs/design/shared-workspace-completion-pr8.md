# Shared workspace completion — PR8 / #323

## Decision and scope

Base: `main` at `5bde439dc4ccb08cd605c4b762f08e38e13045ff`, after the
maintainer's passing #347/#348 validation. Those results establish the baseline;
they do not validate this new change. All commits use `[skip ci]`. GitHub Actions
are not invoked. Current Windows build, 80/80/50 and WPF smoke remain merge gates.

The workspace is composed from shared controls and application policies, with
game-owned native operations. It is not a universal window or a requirement that
every simulator implement every feature. DCS's existing renderer and draft inspector
remain native consumers of the shared host, selection/editor and execution boundary.

## Final gaps found and implemented

1. Squadrons retained separate mouse-drag threshold/selection handling. It now
   uses `GameCommandDragSource`, as Elite and War Thunder do, including disposal
   and the current scanning/identification/live-input gate. DCS retains its native
   copy/move and modifier-layer drag semantics.
2. Squadrons normal Apply/drop chose pair/rebind/create branches in WPF. The new
   adapter-owned `SquadronsCommandEdit` chooses and validates the native operation.
   Its explicit record edit also owns direction preservation and verifies that
   the record belongs to the selected command. `SquadronsAxisPair` defines saved
   pair completeness and ownership for both button and context-menu availability.
   Apply availability now uses that same native review, disabling unchanged
   assignments consistently with Elite and War Thunder instead of merely checking
   that a plan could be constructed.
3. Squadrons toolbar reversal and advanced single-record edits directly mutated
   the session. Both now run through `WorkspaceAssignmentWorkflow`, as do normal
   Apply/drop and the existing reviewed clear. Feedback reflects actual change,
   invalid requests remain blocked, and completion is emitted once per action.
4. Elite, Squadrons and War Thunder repeated the same void-action wrapper around
   the workflow. The application workflow now supplies that overload, including
   War Thunder's combined tuning operation. DCS retains its native `DraftResult`
   overload so conflict/blocked outcomes are preserved rather than converted to
   a claimed success.
5. Adapter-owned physical edits need the same hardware checks as the original
   session record APIs. `IVerifiedSimNativeEdit` declares the logical device and
   physical input. The session checks linked instance/product and enumerated
   support during review and again during Apply, before publishing the isolated,
   identity-validated native result. Existing nonphysical native edits retain
   their current adapter validation; this is an explicit capability, not guessed
   from a device name, record index or diagram caption.

## #323 acceptance reconciliation

| Requirement | Current ownership and evidence |
| --- | --- |
| Composed shell, feature navigation, stateful actions | `GameWorkspaceShell`, `GameWorkspaceFeatureHost`, `FeatureActionBar`, chrome/leave guard; #325/#331/#332/#335/#342; shared-host and feature-navigation smoke |
| Shared command browser, editor and input selection | `GameCommandBrowserView`, `GameAssignmentEditorView`, `AssignmentInputPicker`, `GameAssignmentSelection`; #330/#331/#344; selection/presentation and DCS/Elite/Squadrons/War Thunder smoke |
| Common Devices layout and diagnostics | `DeviceDetailsLayout`, `DeviceSetupSection`, `WorkspaceDrawingPreview`, `WorkspaceDiagnosticsPanel`; #345; style inventory and workspace smoke |
| Common diagram interaction and real context actions | `HardwareDiagramPresenter`, typed `HardwareDiagramInteractions`, `DiagramViewport`; #326/#329/#332/#335; native DCS renderer intentionally retained; drawing/clear/tune/zoom smoke |
| Adapter-owned encoding, paired axes, identity, atomic writes | Native backends and `GameProfileSession`; #327/#328/#335 plus PR8 physical-edit contract and Squadrons command operation |
| Advanced authoring follows the same guarded execution | `WorkspaceAssignmentWorkflow`, typed native edits, session previews; PR8 removes the remaining Squadrons record/reverse bypasses |
| Save/discard and controller/command/input isolation | Shared leave guard, session snapshots, selection state and feedback; existing lifecycle/selection/workflow tests and four-game smoke |
| Squadrons feature decomposition | Separate Devices and Assignments views, shared browser/editor/drawing/input feedback, adapter-owned command operations; the window coordinates views and services rather than choosing native record encodings |
| Table/text fallback and unsupported capabilities | Shared drawing/input feedback with native tables; system records remain read-only; unsupported features are not advertised as implemented |
| War Thunder foundation migration and preserved old branches | #328/#335/#346 and the pinned source/parity audit in #323; no legacy branch merge or destructive workspace migration |

Simulator-specific views and callbacks remain intentional: DCS module/UI layers,
modifier reservations, sparse filters, catalog authoring, kneeboards and deployment;
Elite slots, combinations and vehicle sections; Squadrons native capture/template
review and directional records; War Thunder occurrence-addressed fields, ranges
and native parameter units. They are adapter features inside the common host,
not unfinished requests to make four identical feature sets.

## Regression and merge evidence

- New native command tests exercise isolated review, whole axes, digital creation
  and replacement, POV alias verification, single-record direction preservation,
  reversal, no-op detection, discard, stale hardware, read-only access and invalid
  command/record/backend/target rejection.
- Pure saved-pair tests cover incomplete, duplicated, conflicting, mixed-device,
  mixed-axis, read-only and complete directional records.
- Shared workflow tests exercise the new void-action overload's cancellation,
  changed scope, actual/no-op mutation, completion count and callback validation.
- Squadrons drawing smoke now checks that normal Apply, advanced record Apply and
  both reversals traverse the workflow; reversing twice restores native output.
- Existing DCS/Elite/War Thunder workflow, advanced tuning, save/discard/export,
  controller switching, read-only system input and no-drawing tests remain required.

Authoring environment: no .NET SDK or Windows WPF runtime. Source/layout checks
are supplementary; no passing build, unit, coverage/CRAP or WPF run is claimed.

Run from the PR branch on Windows:

```powershell
dotnet build -c Release
.\tests\coverage-test-gates.ps1
.\tests\crap-tests.ps1
.\tests\generate-test-reports.ps1 -NoOpen
.\tests\smoke-tests.ps1
```

Before merge, record the tested head SHA and results. Briefly exercise all four
workspaces: command/input selection; Apply versus drag; pointed clear/tune;
controller switches; save/discard/reopen/export; drawing navigation and fallback.
For Squadrons also check reverse, advanced single-record editing and rejection
after disconnect/rescan. Keep 80% line/80% branch per production assembly and
CRAP ceiling 50 unchanged. Resolve actual failures before merging.

This closes the #323 implementation scope only after candidate acceptance. It
does not close release-specific hardware/upgrade/DPI observations, notice/artwork
review, optional future native capabilities or commercial work. Those remain in
#245, #189–#194, #264, #282, #286 and #252 with their own acceptance requirements.
