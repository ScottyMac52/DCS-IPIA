# Issue #323 — PR 2: common hardware diagram presenter

## Architectural ownership

`HardwareDiagramPresenter` replaces `EliteDiagramPresenter` as the shared drawing/hit-region/gesture surface. It reuses the canonical renderer and the existing `DiagramViewport` instance created by the feature host; the viewport retains PR #326 scrolling and pointer-anchored zoom.

The host supplies `HardwareDiagramInteractions` with **typed callbacks** for Select, Drop, Clear and Tune plus target-specific predicates. Missing callbacks and false predicates remove the corresponding menu item. A renderer must never infer native tune or clear support from `HardwareGraphicControl.IsAxis`.

`HardwareDiagramActionAvailability` is a WPF-independent policy used to enforce **both handler and native-target availability** and unit-tested separately.

## Elite first consumer

- Elite assigns and selects with unchanged existing native session actions.
- Left-click selects the input directly; the right-click menu does not repeat this selection action. The former `Tune / assign axis` phantom has been removed (it only selected).
- `Clear assignment` appears only if Elite reports that the currently selected command/slot is backed by a matching input on the displayed device and the existing action state permits clear. Its availability is evaluated when opening the menu, not frozen when the artwork was rendered.
- No `Tune axis` action is supplied by Elite yet because its diagram had no tune action; the existing axis editor remains accessible through the assignment editor. Empty context menus are suppressed. Menu popups use mouse-point placement to avoid blocking wide hit regions. DCS retains its real Change label / Clear assignment / Tune axis context menu.
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
- [ ] Elite right click has **no** redundant Select or misleading Tune/assign axis entry; Clear only for a genuinely matching clearable current assignment, and no menu when no action exists
- [ ] Existing axis editor still provides axis value/inversion/deadzone controls; no faux tuning from diagram
- [ ] Ctrl wheel zoom under pointer, Shift wheel horizontal, wheel vertical; DCS assignments unaffected
- [ ] No drawing / text / table fallback unchanged; Squadrons native import/export and War Thunder parser round trip unchanged

No Windows build or WPF smoke has been run in the connector authoring environment. Keep the PR draft until the host's coverage and GUI gates pass.
