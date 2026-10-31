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
throttle and do not establish Warthog or MOZA AB9 support. Hornet catalog work
and the full remaining acceptance checklist are tracked separately in #282.

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
The right-hand layouts also include RX/RY for the mini-stick horizontal/vertical
axes alongside its independent Button 6 press (34 native keys; left-hand layouts
retain their existing 32-key definition). Controls replaced by the active POV/button setting are listed
in an **Alternate mode inputs** area. They are retained as alternative native
inputs, not a claim that the driver emits both representations simultaneously.
The Windows name distinguishes handedness but does not identify the POV setting;
HEIMDALL therefore offers both compatible layouts for explicit selection.

Inventory matching still requires an exact native key set and no unsupported
objects. If the driver exposes a different capability set, record its inventory
details before extending the shared definition. In particular, the optional
scroll-wheel virtual axis is documented but not mapped until its actual native
key is verified; enabling extra axes can prevent an exact catalog match.

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
