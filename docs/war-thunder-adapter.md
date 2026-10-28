# War Thunder adapter — Phase 1 native BLK capture

PR 1B reconciles the protected `feature/war-thunder-adapter-foundation` source into current `main` as an isolated, build-registered `Heimdall.WarThunder` project with regression tests for captured BLK inventory and conservative editing. It does **not** advertise a working game selection or device editor.

- Parse typed BLK scalars and nested blocks; preserve unchanged text exactly.
- Inventory captured hotkey, axis and joystick fields without inferring hardware identity.
- Expose numeric activation codes without assigning undocumented tap/hold meanings.
- Replace a single uniquely addressed captured scalar, rejecting unknown, ambiguous or type-invalid edits.
- Include the project in `HEIMDALL.sln` and the unit-test project; tests cover round trips, inventory, edits and malformed input.

## PR 1B integration boundaries

- Targets the solution's shared .NET 10 configuration through `Directory.Build.props`, rather than retaining the old branch's explicit net8.0 target.
- Includes `Heimdall.WarThunder` in `HEIMDALL.sln` and references the project from `Heimdall.Tests` without dropping the Squadrons test reference.
- The existing BLK parser and control inventory remain native-adapter concerns. PR 2's shared presenter must consume adapter projections, never parse BLK text in WPF.
- No WPF window, game tile, simulator selection registration, native deployment workflow, or speculative joystick mapping is exposed by this PR.
- The original War Thunder feature branch is left unchanged. DCS, Elite and Squadrons remain as on main.

## Verification (required before merge)

- [ ] `dotnet build -c Release`
- [ ] `dotnet test -c Release --no-build`
- [ ] 80% line / 80% branch / CRAP ≤ 50 quality gates
- [ ] `tests/smoke-tests.ps1` on Windows, ensuring all existing simulator scenarios still pass
- [ ] Validate BLK fixture import and unchanged export; reject ambiguous/invalid native edits, including comments, numeric and string scalar types

## Remaining work

The parser is intentionally a conservative Phase 1 subset; validation against real War Thunder BLK fixtures and uncommon native encodings is still required. Simulator backend integration, device linking, gesture/axis tuning, game selection, and safe reviewed deployment remain follow-up phases. Do not deploy output into live game settings without a backup. Builds and tests have not been executed in the GitHub authoring environment.
