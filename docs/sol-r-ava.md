# Sol-R grip on AVA

HEIMDALL ships four shared Sol-R grip layouts in its source-controlled asset bundle.
All four layouts are available in Free Edition, subject to verified native
Thrustmaster identity and the existing capability checks. Other licensed
features keep their edition requirements.

They are reusable built-in hardware references, not imported user drawing
revisions. No personal database or draw.io import is required to distribute them.

| Native Windows name | Button-focused layout | POV-focused layout |
| --- | --- | --- |
| `AVA [R] Sol-R` | `tm-sol-r-ava-r-buttons` | `tm-sol-r-ava-r-pov` |
| `AVA [L] Sol-R` | `tm-sol-r-ava-l-buttons` | `tm-sol-r-ava-l-pov` |

These layouts cover the grip on AVA only. They contain no Sol-R base buttons or
throttle and do not establish Warthog or MOZA AB9 support. See [AVA grip setup](ava-grip-setup.md)
for Hornet recognition, current limitations and the physical acceptance record for #282.

## Configure and adopt

1. Connect the Sol-R grip to AVA following Thrustmaster's installation
   instructions, with USB disconnected during grip installation. Use the AVA
   driver/control panel and firmware compatible with the grip; the reported
   working setup uses AVA firmware 2.07.
2. Open `joy.cpl`, select the AVA, then **Properties → Test Input**. Choose
   **RIGHT-HANDED** or **LEFT-HANDED**, and configure **Point of View hat-switch**
   as desired. Confirm the native name and test buttons, hats and axes. See the
   [official manual](https://ts.thrustmaster.com/download/accessories/Manuals/SOL-R/Sol-R_3_AVA_Add-On_Grip_User_Manual.pdf),
   printed pages 15–16, 21–23 and 28. HEIMDALL does not change these driver settings.
3. In HEIMDALL, use **Hardware → Discover devices… / Rescan**. Review and adopt the
   actual AVA instance. Both layouts for the recognized handedness are offered
   when its enumerated controls match the catalog.
4. Refresh an existing workspace's catalog using **Load built-in hardware**, then
   save the staged catalog update. Choose the intended Sol-R shared hardware
   reference when adding that physical device to a module. Existing modules,
   assignments and imported drawing revisions are not silently replaced.
5. Choose the presentation matching your POV setting. Verify all inputs in the
   input tester and preview/export the kneeboard before using it in DCS.

The layouts retain all 21 numbered buttons, X/Y/RZ, and eight POV directions.
The right-hand layouts retain all eight axes reported by the verified AVA [R]
Sol-R enumeration: X/Y/Z, RX/RY/RZ and SLIDER1/SLIDER2. RX/RY remain available
alongside the mini-stick's independent Button 6 press. Z, Dial (SLIDER1) and
Slider (SLIDER2) appear in a separate **Driver-exposed axes** area; their native
keys are verified, but a physical location or virtual function is not inferred.
This gives 37 native keys after expanding the POV directions. Left-hand layouts
retain their existing 32-key definition; right-hand evidence does not establish
left-hand controls. Controls replaced by the active POV/button setting are listed
in an **Alternate mode inputs** area. They are retained as alternative native
inputs, not a claim that the driver emits both representations simultaneously.
The Windows name distinguishes handedness but does not identify the POV setting;
HEIMDALL therefore offers both compatible layouts for explicit selection.

Automatic inventory artwork recommendations still require an exact native key
set and no unsupported objects. The reported right-hand AVA also enumerates a
nonassignable collection and an unidentified `Axis 8 [2050]`. Both remain explicit
unsupported diagnostics; no ninth axis or third slider is invented. Their
presence keeps automatic recommendations unavailable. You may explicitly choose
the right-hand bundled layout in New module when its known controls exactly
match the adopted inventory. Unsupported objects are not assignable.

The right-hand definitions were corrected from five to eight known axes using
this recorded enumeration. If your driver exposes a different supported set,
use the generic inventory layout and retain the exact details for review; do
not force an incompatible template. Scanning does not update previously adopted
capabilities until you explicitly review and save changed controls. Existing
module drawings remain pinned until you review and apply a built-in catalog
update; rebuilding alone does not rewrite saved module data.

## Source and validation

The hardware manifest, recognition rules, per-layout Lua input definitions,
editable draw.io sources, derived SVGs, original vector facsimiles and bundle
provenance are committed under `data/shared-assets/`. Build/publish copies them
to `SharedAssets/`. Follow the [draw.io authoring workflow](drawio-hardware-workflow.md)
and update provenance whenever changing these assets.

Automated checks cover packaged loading, exact capability matching, unrelated
device exclusion, native filename preservation, import validation, module
save/reopen, portable snapshots and rendering. Controller-free tests use
representative capability evidence. Real AVA enumeration, physical input
numbering, handedness, driver modes and in-game acceptance still require the
physical hardware check above.
