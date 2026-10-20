# UH-1H control mappings

This reference is generated from the effective DCS `.diff.lua` profiles used by IPI. Those profiles remain the executable source of truth.

## Device index

| Device | Profile file | Layers | Assignments |
| --- | --- | --- | ---: |
| [F16 MFD 1 — mfd1](devices/TM-MFD-1-MAPPINGS.md) | `F16 MFD 1 {51FA60C0-CB32-11ed-800B-444553540000}.diff.lua` | Base | 18 |
| [F16 MFD 2 — mfd2](devices/TM-MFD-2-MAPPINGS.md) | `F16 MFD 2 {51FA39B0-CB32-11ed-8008-444553540000}.diff.lua` | Base, MOZA_F16_F18_BTN3 | 23 |
| [F16 MFD 3 — mfd3](devices/TM-MFD-3-MAPPINGS.md) | `F16 MFD 3 {C5BE49A0-2342-11ee-8001-444553540000}.diff.lua` | Base, UI: MOZA_MODIFIER_BTN3 | 29 |
| [MOZA AB9 FFB Base](devices/MOZA-AB9-HORNET-GRIP-MAPPINGS.md) | `MOZA AB9 FFB Base {71DA6210-432E-11f1-8001-444553540000}.diff.lua` | Base, MOZA_F16_F18_BTN3, UI: MOZA_MODIFIER_BTN3 | 40 |
| [Throttle - HOTAS Warthog](devices/TM-WARTHOG-THROTTLE-MAPPINGS.md) | `Throttle - HOTAS Warthog {5200C960-CB32-11ed-8020-444553540000}.diff.lua` | Base | 7 |

## Reading the tables

- `JOY_BTN#` identifies a button; `JOY_X`, `JOY_Y`, and similar names identify axes.
- A modifier before an input means both must be active.
- Empty/default profiles are documented explicitly rather than omitted.
- DCS device GUIDs in filenames are installation-specific; see [Installation](INSTALLATION.md#device-guids).

## Devices

### F16 MFD 1 — mfd1

- Profile: `F16 MFD 1 {51FA60C0-CB32-11ed-800B-444553540000}.diff.lua`
- Shared hardware: `tm-mfd`
- Physical instance: `51fa60c0-cb32-11ed-800b-444553540000`
- Kneeboard page: `01-TM-MFD-1.png`

#### Base layer

| Physical input | Assignment | Axis/filter settings |
| --- | --- | --- |
| `JOY_BTN3` | Arm/Dck | — |
| `JOY_BTN28` | Elevation- | — |
| `JOY_BTN27` | Elevation+ | — |
| `JOY_BTN22` | Intensity- | — |
| `JOY_BTN21` | Intensity+ | — |
| `JOY_BTN1` | Switch | — |
| `JOY_BTN14` | Chaff | — |
| `JOY_BTN12` | Dispense | — |
| `JOY_BTN25` | Bright+ | — |
| `JOY_BTN23` | Bright+ | — |
| `JOY_BTN26` | Bright- | — |
| `JOY_BTN24` | Bright- | — |
| `JOY_BTN20` | De-Ice | — |
| `JOY_BTN19` | Gov Auto/Emer | — |
| `JOY_BTN18` | Low RPM | — |
| `JOY_BTN17` | Main Fuel | — |
| `JOY_BTN6` | Ripple Cvr | — |
| `JOY_BTN7` | Ripple | — |

### F16 MFD 2 — mfd2

- Profile: `F16 MFD 2 {51FA39B0-CB32-11ed-8008-444553540000}.diff.lua`
- Shared hardware: `tm-mfd`
- Physical instance: `51fa39b0-cb32-11ed-8008-444553540000`
- Kneeboard page: `02-TM-MFD-2.png`

#### Base layer

| Physical input | Assignment | Axis/filter settings |
| --- | --- | --- |
| `JOY_BTN1` | Brief | — |
| `JOY_BTN2` | Debrief | — |
| `JOY_BTN4` | Info bar | — |
| `JOY_BTN3` | Jump | — |
| `JOY_BTN5` | Score | — |
| `JOY_BTN20` | KB | — |
| `JOY_BTN27` | Prev Pg | — |
| `JOY_BTN28` | Nxt Pg | — |
| `JOY_BTN19` | Glance | — |
| `JOY_BTN18` | Mark | — |
| `JOY_BTN15` | My | — |
| `JOY_BTN14` | Cover me | — |
| `JOY_BTN13` | SEAD | — |
| `JOY_BTN12` |  Attck GT | — |
| `JOY_BTN11` |  Cmpl RTB | — |
| `JOY_BTN10` | Join up | — |
| `JOY_BTN9` | Dialog | — |
| `JOY_BTN8` | Toggle | — |

#### Modifier layer: `MOZA_F16_F18_BTN3`

| Physical input | Assignment | Axis/filter settings |
| --- | --- | --- |
| `MOZA_F16_F18_BTN3 + JOY_BTN3` | SPAWN | — |
| `MOZA_F16_F18_BTN3 + JOY_BTN4` | Units | — |
| `MOZA_F16_F18_BTN3 + JOY_BTN5` | Mouse | — |
| `MOZA_F16_F18_BTN3 + JOY_BTN1` | Pilot | — |
| `MOZA_F16_F18_BTN3 + JOY_BTN2` | Sound | — |

### F16 MFD 3 — mfd3

- Profile: `F16 MFD 3 {C5BE49A0-2342-11ee-8001-444553540000}.diff.lua`
- Shared hardware: `tm-mfd`
- Physical instance: `c5be49a0-2342-11ee-8001-444553540000`
- Kneeboard page: `03-TM-MFD-3.png`

#### Base layer

| Physical input | Assignment | Axis/filter settings |
| --- | --- | --- |
| `JOY_BTN20` | Burst | — |
| `JOY_BTN19` | ROE | — |
| `JOY_BTN1` | Burst | — |
| `JOY_BTN2` | ROE | — |
| `JOY_BTN6` | Burst | — |
| `JOY_BTN7` | ROE | — |
| `JOY_BTN3` | AI Pnl | — |
| `JOY_BTN28` | Arm- | — |
| `JOY_BTN27` | Arm+ | — |
| `JOY_BTN22` | Gun- | — |
| `JOY_BTN21` | Gun+ | — |
| `JOY_BTN18` | Door | — |
| `JOY_BTN8` | Door | — |
| `JOY_BTN13` | Reset | — |
| `JOY_BTN26` | Rckt Pair- | — |
| `JOY_BTN25` | Rckt Pair+ | — |

#### UI Layer assignments

| Physical input | Assignment |
| --- | --- |
| `MOZA_MODIFIER_BTN3 + mfd-osb-t3-shifted` | Active Pause |
| `MOZA_MODIFIER_BTN3 + mfd-osb-r3-shifted` | F10 Map Coordinates Picker |
| `MOZA_MODIFIER_BTN3 + JOY_BTN28` | Make Screenshot |
| `MOZA_MODIFIER_BTN3 + mfd-osb-l3-shifted` | Pause |
| `MOZA_MODIFIER_BTN3 + JOY_BTN24` | Recenter VR |
| `MOZA_MODIFIER_BTN3 + mfd-osb-b1-shifted` | Restart Mission |
| `MOZA_MODIFIER_BTN3 + mfd-osb-t2-shifted` | Spyglass |
| `MOZA_MODIFIER_BTN3 + JOY_BTN25` | Time Fast |
| `MOZA_MODIFIER_BTN3 + mfd-osb-b3-shifted` | Time Normal |
| `MOZA_MODIFIER_BTN3 + JOY_BTN26` | Time Slow |
| `MOZA_MODIFIER_BTN3 + JOY_BTN21` | VR Left |
| `MOZA_MODIFIER_BTN3 + JOY_BTN27` | VR Right |
| `MOZA_MODIFIER_BTN3 + mfd-osb-t1-shifted` | VR Zoom |

### MOZA AB9 FFB Base

- Profile: `MOZA AB9 FFB Base {71DA6210-432E-11f1-8001-444553540000}.diff.lua`
- Shared hardware: `moza-ab9-hornet-grip`
- Physical instance: `71da6210-432e-11f1-8001-444553540000`
- Kneeboard page: `04-MOZA-AB9-HORNET-GRIP.png`

#### Base layer

| Physical input | Assignment | Axis/filter settings |
| --- | --- | --- |
| `JOY_BTN_POV1_U` | Pilot | — |
| `JOY_BTN_POV1_D` | Reset | — |
| `JOY_BTN_POV1_R` | Force | — |
| `JOY_BTN11` | Arm Up | — |
| `JOY_BTN13` | Arm Down | — |
| `JOY_BTN10` | Left Gunner | — |
| `JOY_BTN8` | Right Gunner | — |
| `JOY_BTN19` | Rocket Reset | — |
| `JOY_BTN17` | Rckt Pair- | — |
| `JOY_BTN15` | Rckt Pair+ | — |
| `JOY_BTN7` | AI Operator ROE Iterate | — |
| `JOY_BTN18` | AI Left ROE Iterate | — |
| `JOY_BTN16` | AI Right ROE Iterate | — |
| `JOY_BTN9` | AI Operator Burst Switch | — |
| `JOY_BTN6` | Shoot | — |
| `JOY_BTN12` | Arms | — |
| `JOY_BTN2` | Copilot weapon release | — |
| `JOY_BTN14` | Flare Dispense Button | — |
| `JOY_BTN5` | Chaff Mode Switch | — |
| `JOY_Y` | Pitch Axis | — |
| `JOY_X` | Roll Axis | — |

#### Modifier layer: `MOZA_F16_F18_BTN3`

| Physical input | Assignment | Axis/filter settings |
| --- | --- | --- |
| `MOZA_F16_F18_BTN3 + JOY_BTN11` | Gun Up | — |
| `MOZA_F16_F18_BTN3 + JOY_BTN13` | Gun Down | — |
| `MOZA_F16_F18_BTN3 + JOY_BTN10` | AI Left ROE Iterate | — |
| `MOZA_F16_F18_BTN3 + JOY_BTN8` | AI Right ROE Iterate | — |
| `MOZA_F16_F18_BTN3 + JOY_BTN7` | AI Left Burst Switch | — |
| `MOZA_F16_F18_BTN3 + JOY_BTN9` | AI Right Burst Switch | — |
| `MOZA_F16_F18_BTN3 + JOY_BTN5` | NVG | — |
| `MOZA_F16_F18_BTN3 + JOY_BTN_POV1_L` | NVGG- | — |
| `MOZA_F16_F18_BTN3 + JOY_BTN_POV1_R` | NVGG+ | — |
| `MOZA_F16_F18_BTN3 + JOY_BTN12` | Pilot Sight Switch | — |
| `MOZA_F16_F18_BTN3 + JOY_BTN18` | Pilot Sight Intensity Decrease | — |
| `MOZA_F16_F18_BTN3 + JOY_BTN16` | Pilot Sight Intensity Increase | — |
| `MOZA_F16_F18_BTN3 + JOY_BTN2` | Pilot Sight Armed/Docked | — |
| `MOZA_F16_F18_BTN3 + JOY_BTN_POV1_D` | Pilot Sight Elevation Decrease | — |
| `MOZA_F16_F18_BTN3 + JOY_BTN_POV1_U` | Pilot Sight Elevation Increase | — |
| `MOZA_F16_F18_BTN3 + JOY_BTN14` | AI Panel Show/Hide | — |

#### UI Layer assignments

| Physical input | Assignment |
| --- | --- |
| `MOZA_MODIFIER_BTN3 + JOY_BTN4` | Recenter VR |
| `MOZA_MODIFIER_BTN3 + JOY_BTN15` | Spyglass |
| `MOZA_MODIFIER_BTN3 + JOY_BTN19` | VR Zoom |

### Throttle - HOTAS Warthog

- Profile: `Throttle - HOTAS Warthog {5200C960-CB32-11ed-8020-444553540000}.diff.lua`
- Shared hardware: `tm-warthog-throttle`
- Physical instance: `5200c960-cb32-11ed-8020-444553540000`
- Kneeboard page: `05-TM-WARTHOG-THROTTLE.png`

#### Base layer

| Physical input | Assignment | Axis/filter settings |
| --- | --- | --- |
| `JOY_BTN24` | Arms | — |
| `JOY_BTN27` | ORBIT | — |
| `JOY_BTN28` | LEVEL FLIGHT | — |
| `JOY_BTN26` | AutoPilot | — |
| `JOY_BTN21` | Caution Reset | — |
| `JOY_Z` | Collective | invert=true |
| `JOY_RZ` | Throttle | — |
