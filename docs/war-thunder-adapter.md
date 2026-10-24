# War Thunder adapter — Phase 1 native BLK capture

This branch provides an isolated, build-registered `Heimdall.WarThunder` project with regression tests for captured BLK inventory and conservative editing. It does **not** advertise a working game selection or device editor.

- Parse typed BLK scalars and nested blocks; preserve unchanged text exactly.
- Inventory captured hotkey, axis and joystick fields without inferring hardware identity.
- Expose numeric activation codes without assigning undocumented tap/hold meanings.
- Replace a single uniquely addressed captured scalar, rejecting unknown, ambiguous or type-invalid edits.
- Include the project in `HEIMDALL.sln` and the unit-test project; tests cover round trips, inventory, edits and malformed input.

## Remaining work

The parser is intentionally a conservative Phase 1 subset; validation against real War Thunder BLK fixtures and uncommon native encodings is still required. Simulator backend integration, device linking, gesture/axis tuning, game selection, and safe reviewed deployment remain follow-up phases. Do not deploy output into live game settings without a backup. Builds and tests have not been executed in this connector-only environment.
