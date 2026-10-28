# Issue #323 — PR 4: DCS and Elite consumer migration

Baseline: main after merged PR 3 (`6d8d60558b564d9b8f5f509314f3236eaef6558d`). This is an incremental consumer migration, with native editing/rendering surfaces composed inside shared hosts.

## Production migration

- DCS now uses `GameWorkspaceShell`, retaining its five-row arrangement through the explicit optional action row. Elite retains the existing compact shell arrangement.
- Both windows use `GameWorkspaceFeatureHost` to mount cached native Devices, Assignments, Reference Sheets and Input Test surfaces and expose only their real feature actions. Stale action commands are disposed on transitions. Elite stops live input/identification before leaving a feature; DCS retains its tested native unload cleanup.
- DCS's asynchronous profile/game switch now uses the same `GameWorkspaceLeaveGuard` protocol as Elite. It waits for native persistence and never discards after cancellation, a failed save, or a save exception. Native repository/save/revision and close semantics remain with the consumer.
- DCS command filtering delegates to `GameCommandBrowser` through `DcsCommandBrowser`. The adapter retains module/UI scope isolation and the all-controller intersection. The assignment palette's "this device/layer" ownership filters now use the actual selected controller rather than counting any device in the scope.
- DCS command identity is owned by `GameAssignmentSelection`; the native editable catalog grid remains a projection. The optional explicit-empty selection policy preserves DCS's initial no-command behavior. A display cache prevents refreshes from erasing unsubmitted native catalog field edits.
- DCS now injects its native inspector fields into `GameAssignmentEditorView`, alongside Elite's existing native fields. Selected command and adjacent feedback share a presentation frame; native DCS labels, generic names, modifiers and sparse axis tuning are retained.
- Controller switches clear stale DCS callout/inspector state. Redraws re-project the complete current saved/pending binding, label, reservation and tuning availability rather than partially restoring old details.

## Preserved native surfaces

DCS's editable command catalog, module/UI layer and modifier scopes, reserved modifier behavior, callout menus, source-to-target/copy drag operations, undo, labels, sparse axis tuning, authored mappings, reference sheets/kneeboards, input feedback, save/discard and package/export workflows remain native. Elite slot/combinations, axis inversion/deadzone, identification, controller ordering, reference sheets and native preset export remain native.

The DCS callout renderer deliberately remains native: it renders editable labels, modifier reservations, sparse filters and drag affordances absent from the generic transparent-region presenter. This PR composes that surface; it does not replace it and claim rendering parity from source inspection. Native codecs/record allocation remain outside shared WPF hosts.

No Squadrons or War Thunder native implementation or protected feature branch is rewritten. Squadrons drawing/assignment parity remains PR 5; War Thunder readiness remains the later audit and implementation work.

## Validation and Windows acceptance

Unit tests cover DCS controller/scope ownership, every-device intersection, shared category/type/text filtering, native evidence invariance, explicit-empty selection, and asynchronous leave/persistence failure behavior.

New Windows smoke coverage checks shared shell row contracts, cached feature state, real native action routing, stale action disposal, unsupported-feature fallback, idempotent cleanup, native editor injection, and switching controllers with the same physical input key without retaining old saved/pending inspection details. Existing DCS/Elite native training, assignment, reference, input testing, menu and export smokes remain required.

Before merge on Windows:

- `tests/coverage-test-gates.ps1`
- `tests/generate-test-reports.ps1`
- `tests/smoke-tests.ps1`
- Manually inspect DCS module/UI scope and modifier layers, catalog editing, callout label/clear/tune, drag/copy, controller filtering and inspector refresh, input test/navigation, reference sheets and save/discard/export.
- Recheck Elite filters, controller/feature switching, Clear/Tune, identification focus cleanup, reference sheets and preset export.

Linux cross-compilation establishes C#/XAML/analyzer compatibility, not GUI/in-game acceptance. Keep draft until Windows interaction validation is complete.

## Authoring verification

- .NET SDK 10.0.403 on Linux: all 1,623 unit tests passed, none failed/skipped.
- Independent 80% line / 80% branch gates passed for all eight measured assemblies. DCS: 96.92% line / 86.81% branch; Application: 96.62% / 86.81%.
- Repository method CRAP gate passed for all 1,819 measured methods at ceiling 50. Thresholds are unchanged.
- App and WPF smoke project cross-build passed with `EnableWindowsTargeting=true`, zero warnings/errors. Actual Windows smoke execution remains pending.
- Documentation, theme resources, pinned Common inventory, and `git diff --check` passed.
- C# layout checks add no diagnostics in new/modified files. The global checker still reports 70 diagnostics in unchanged baseline files; this scoped migration does not claim to fix that existing gate.
- The known named-pipe restriction prevents the MSBuild workspace used by `dotnet format style` from opening here. The standard Windows coverage script includes formatting verification and must be run before merge.
