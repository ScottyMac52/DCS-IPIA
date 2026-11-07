# AVA grip setup and acceptance

HEIMDALL keeps physical controllers, capability definitions and hardware drawings separate. A familiar name alone does not prove the attached grip or its button numbering. Review the actual enumerated controls before adopting a unit or applying a drawing. Free additionally requires verified Thrustmaster manufacturer evidence; renaming another manufacturer's controller does not grant access.

## Supported catalog combinations

| Native name / mode | Bundled layout | Expected supported inputs | Evidence and limitation |
| --- | --- | --- | --- |
| `AVA [R] Hornet` | `ava-base-f18c`, using the original `grip-f18c` artwork | 19 numbered buttons, X/Y, eight POV directions (29 keys) | Catalog and synthetic capability tests; right hardware acceptance below remains required. |
| `AVA [L] Hornet` | No automatic static layout | Use the enumerated capability definition | No verified left-mode capture or manufacturer left-mode numbering in the reviewed sources. Do not mirror the right diagram or guess numbers. Generic controls require Owner access. |
| `AVA [R] Sol-R` | `tm-sol-r-ava-r-buttons` / `tm-sol-r-ava-r-pov` | 21 buttons, X/Y/RZ/RX/RY, eight POV directions (34 keys) | Both bundled definitions covered by catalog/persistence tests; record actual mode behavior locally. |
| `AVA [L] Sol-R` | `tm-sol-r-ava-l-buttons` / `tm-sol-r-ava-l-pov` | 21 buttons, X/Y/RZ, eight POV directions (32 keys) | Both definitions follow the documented left layout; physical left-mode acceptance is not recorded. |

Sol-R buttons/POV are presentation choices that retain the alternate inputs; they do not assert that both are emitted simultaneously. See [Sol-R details](sol-r-ava.md). The optional scroll-wheel virtual axis has no verified native key in this catalog and is not guessed.

Right Hornet reuses the existing Hornet SVG and Lua definition, including trigger stages on buttons 1 and 6. No artwork is copied from Viper and no personal DCS defaults are inherited. Generic Hornet and MOZA aliases remain distinct edition identities. Artwork provenance remains in [SOURCES.md](../data/shared-assets/assets/shared/hardware/SOURCES.md); the manifest alias is included in the bundle integrity record.

Names are matched case-insensitively against the existing grip-specific rules, then the entire supported key set must match. An additional button, missing axis, unsupported object or unavailable enumeration prevents a bundled candidate. `JOY_POV1_U` and `JOY_BTN_POV1_U` (and the other directions) identify the same physical POV input for compatibility checks. This comparison does not rewrite stored native keys, assignments or filenames. Bare `AVA Base` and unknown grips receive no automatic Viper fallback.

## Windows preparation

The [official AVA support page](https://support.thrustmaster.com/en/product/ava-en/) lists driver package `2026_TFHT_3` and firmware `2.07` when checked on 2026-11-04. Open the Thrustmaster Flight Control Panel to record the actual installed versions. Follow the manufacturer's installation/update instructions and restart prompts; firmware changes can require new game mappings. HEIMDALL does not require T.A.R.G.E.T. for native discovery. Check the [Sol-R support page](https://support.thrustmaster.com/en/product/sol-r-3-ava-add-on-grip-en/) for grip-specific instructions.

The earlier issue report says right Hornet and right Sol-R worked with firmware 2.07. It does not establish the local driver version, a left Hornet layout, or acceptance of this change. Older manuals may show `AVA Base` while newer firmware exposes a grip-specific name: record what Windows actually reports.

1. Back up the HEIMDALL profile and game input files before changing hardware, firmware or modes. Save current module drafts.
2. Follow the [AVA manual](https://ts.thrustmaster.com/download/accessories/Manuals/AVA_Base/AVA_Base_User_Manual.pdf) for grip installation and the [Sol-R manual](https://ts.thrustmaster.com/download/accessories/Manuals/SOL-R/Sol-R_3_AVA_Add-On_Grip_User_Manual.pdf) for handedness and POV configuration. Use their procedure rather than an assumed button sequence. A known working rig need not be updated merely to run HEIMDALL.
3. In Windows/control panel verify X/Y, each button, POV direction and any additional axes. Record the native name, manufacturer/product evidence, mode and counts. The instance GUID identifies the physical unit; do not copy another unit's GUID or a catalog default filename.
4. In HEIMDALL use **Hardware → Discover devices / Rescan**, review controls and enumeration errors, select the physical unit, and assign a distinct label/role. For a changed unit, review **Adopt changed controls** explicitly. Rescan alone does not adopt or rewrite saved modules.
5. Choose a compatible bundled preference only when offered. In a module's Devices area, explicitly add/link the reviewed unit and select its compatible drawing. With no compatible drawing, use reviewed generic controls rather than forcing an unrelated grip. Drawing selection does not change the native controller identity.
6. Save, close and reopen. Verify the same physical unit, drawing and assignments. Check an offline reopen without rebinding. Existing modules retain their pinned inputs when a grip/mode changes; review them explicitly before export and test the exported profile in the game.

## Acceptance record

Run the repository Release build, coverage/complexity validation and Windows smoke procedure at the PR head. The installed-catalog smoke renders all four Sol-R layouts and the AVA Hornet alias without a user drawing library. Automated tests use synthetic capabilities and do not certify connected hardware.

For each available physical combination, record:

| Field | Required result |
| --- | --- |
| Build | Commit SHA; Release, 80% line / 80% branch coverage and CRAP ≤50; smoke log |
| Environment | Windows version, installed driver package, control-panel firmware version |
| Hardware | Base/grip, right/left setting, buttons/POV setting, exact Windows native name and product VID/PID |
| Controls | Enumerated button/axis/POV counts and native keys; errors/unsupported objects |
| Selection | Correct catalog candidate or explicit generic-only limitation; no Viper substitution |
| Behavior | Every button (including both Hornet trigger stages), POV direction and axis agrees with the physical control |
| Persistence | Save/reopen and offline reopen preserve drawing, native instance filename and assignments |
| Game | Reviewed export retains native identity; actual game recognizes the intended controls |

Capture capability evidence to a **new** filename; the exporter refuses overwrite:

```powershell
$evidence = Join-Path $env:TEMP ("ava-controller-" + (Get-Date -Format 'yyyyMMdd-HHmmss') + '.json')
& .\src\Heimdall.App\bin\Release\net10.0-windows\Heimdall.exe --controller-evidence $evidence
if ($LASTEXITCODE -ne 0) { throw 'Controller evidence capture failed.' }
```

Keep personal instance GUIDs and paths local unless deliberately sharing them. Record only redacted evidence in the issue. Right Hornet and right Sol-R need current-build physical acceptance. If left hardware is unavailable, report that explicitly; left Hornet remains unsupported for automatic static matching until a real capture and verified mapping are supplied. No left validation is implied by the right results.
