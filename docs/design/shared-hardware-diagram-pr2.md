# Issue #323 — PR 2: common hardware diagram presenter

## Architectural ownership

`HardwareDiagramPresenter` replaces `EliteDiagramPresenter` as the shared drawing/hit-region/gesture surface. It reuses the canonical renderer and the existing `DiagramViewport` instance created by the feature host; the viewport retains PR #326 scrolling and pointer-anchored zoom.

The host supplies `HardwareDiagramInteractions` with **typed callbacks** for Select, Drop, Clear and Tune plus target-specific predicates. Missing callbacks and false predicates remove the corresponding menu item. A renderer must never infer native tune or clear support from `HardwareGraphicControl.IsAxis`.

`HardwareDiagramActionAvailability` is a WPF-independent policy used to enforce **both handler and native-target availability** and unit-tested separately.

## Elite first consumer

- Elite assigns and selects with unchanged existing native session actions.
- Left-click selects the input directly; the right-click menu does not repeat this selection action. The former `Tune / assign axis` phantom has been removed (it only selected).
- Right-click resolves the native assignment under the pointer without requiring a prior left-click. Clear availability follows that target and the host's edit/test/busy state. If several assignments share an input, the matching selected command/slot is preferred; otherwise the first visible assignment is used, consistently with callout selection.
- Assigned native axes offer `Tune axis…`, opening an Elite-specific form for inversion and a finite 0–100 percent deadzone. Apply stages the targeted native axis through the existing session; Cancel leaves bindings unchanged. Empty context menus are suppressed. Menu popups use mouse-point placement. DCS retains its existing context actions.
- Existing tooltip projection, drawing regions, activity outlines, drag payload and `DiagramViewport` are unchanged.
- The shared presenter does not know Elite/Squadrons/War Thunder native record IDs, pairing formats, or unsupported BLK semantics.

## Next consumer migrations

- PR 3: migrate shared assignment/command browser presenters and expose capability-aware action descriptors rather than adding simulator-specific WPF handlers.
- DCS uses real native tuning and clearing in its current assignments view; preserve functionality. When moving DCS to this shared presenter, supply real typed Tune/Clear delegates.
- Squadrons must use adapter-supported `INativeAxisPairEditor` and `INativeRecordCreator` for plus/minus axis/native creation, and never attach phantom clear/tune callbacks. Keep the protected drawing branch unchanged until feature parity is verified.
- War Thunder is a foundation-only native BLK inventory today: no native handler means no menu action. A table/text fallback is valid and requires no diagram.
- `DiagramViewport` remains the single owner for vertical wheel, Shift horizontal wheel and Ctrl pointer zoom.

## Windows acceptance

- [ ] `dotnet build -c Release`
- [ ] `dotnet test -c Release --no-build`
- [ ] 80% line, 80% branch, CRAP <= 50 gates
- [ ] `tests/smoke-tests.ps1`
- [ ] Elite drawing select, drag/drop and tooltip outline unchanged
- [ ] Elite right click has **no** redundant Select or misleading Tune/assign axis entry; Clear targets the pointed assignment without a prior left-click, and no menu appears when no action exists
- [ ] Right-click an assigned axis with another command selected; Tune axis opens native inversion/deadzone editing, Cancel preserves bindings, and Apply survives export
- [ ] Ctrl wheel zoom under pointer, Shift wheel horizontal, wheel vertical; DCS assignments unaffected
- [ ] No drawing / text / table fallback unchanged; Squadrons native import/export and War Thunder parser round trip unchanged

No Windows build or WPF smoke has been run in the connector authoring environment. Keep the PR draft until the host's coverage and GUI gates pass.
