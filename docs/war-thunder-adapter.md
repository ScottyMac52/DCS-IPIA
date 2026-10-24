# War Thunder adapter — native capture support

The isolated `Heimdall.WarThunder` project now inventories captured BLK scalars and blocks, preserves untouched input text, and supports guarded replacement of an existing scalar by unique full path. It does **not** yet implement HEIMDALL game selection, controller assignment UI, profile persistence, or a simulator backend.

## Native evidence and safety

- Read `machine.blk` with `WarThunderControls`; original source is retained exactly for no-op output.
- `Hotkeys`, `Axes`, and `Joysticks` expose captured scalar fields without inventing bindings.
- `ActivationCodes()` returns numeric codes only. Fast/long/double-tap meanings are not established by this code.
- Joystick global offsets are **not** stable hardware identifiers. Never map a global axis/button to TPR, TQS or other devices without verified native joystick association.
- Scalar replacement requires an existing unique path and type-valid value; unrelated fields remain unchanged.
- Never write directly into live game settings without explicit review and backup.

## Not yet delivered

Native nested-binding interpretation, complete export safety checks, device identification, gesture editing, axis-tuning UI, simulator registration, workspace integration, automated tests, and DCS/ED/Squadrons regression validation. This branch remains a draft and must not be merged as a completed integration.
