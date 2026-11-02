# PR6 systemic refactor contract and migration gates

Status: in progress. PR6 remains open; it is not ready to merge.

Recovery point: `e372843df2bb32b93b0f984d81f6238d9eb8c708` on
`checkpoint/pr6-before-systemic-refactor-20261102`. The connector can create a
checkpoint branch, not a tag; this branch preserves exactly the approved starting
commit. Do not advance it. Continue implementation on
`refactor/323-pr6-war-thunder-readiness`. Every implementation commit uses
`[skip ci]`. GitHub Actions are not part of this validation process.

## Ownership and dependency direction

| Boundary | Owns | Must not infer |
|---|---|---|
| Windows discovery | Exact physical instance/product, original object order, GUID/type, native offset, flags and available HID metadata | Game device slots, native game indices, drawing identity, command compatibility |
| Neutral input catalog | Stable local object identities, native categories, ambiguity and missing-evidence diagnostics | DCS keys as universal identities, sorted order as native order, discarded unknown controls |
| Drawing alias bridge | Authored input key to a particular physical object or direction | Game index from the label, device name or visual position |
| Game adapter | Physical-object to native-game translation, virtual inputs, command/alternative identity, supported operations, native serialization | WPF controls, dialogs, arbitrary UI indices as physical evidence |
| Shared assignment workflow | Selection, compatibility, routing Apply/drop/context operations through the same transaction | Per-game native parsing or hidden edits to a different selected controller |
| Shared workspace and dialogs | Standard layout, actions, reasons for unavailable actions, parameter-driven Tune Axis form | Native field names or one simulator's parameter units as universal defaults |
| Draft/session facade | Atomic changes, notifications, dirty state, Save, Discard and export preflight | Different Save semantics for different entry points |

Existing DCS drawing aliases and the legacy live-test buffer are compatibility
projections during migration. They are not removed in the discovery stage. The
neutral catalog is built from every native object, independently of whether the
legacy mapper supports it. Per-device discovery errors must remain distinguishable
from an empty or disconnected device.

## Native evidence contract

`ControllerObject` retains its existing constructor and serialized identity fields.
Optional `Details` adds the native offset, object flags, force metadata, collection,
designator, HID usage page/usage, dimension, exponent and report ID when present in
the supplied native structure. Missing metadata means unknown, not zero.

`ControllerInputCatalog` exposes every object in original enumeration order with a
stable local ID derived from its native GUID and object type/instance. IDs do not
contain the localized name, enumeration position, raw offset, DCS key, game device
number or a guessed axis role. The owning physical device identity remains on the
observation. Duplicate IDs are retained and explicitly marked ambiguous.

Microsoft documents that `DIDEVICEOBJECTINSTANCE.dwOfs` is a **raw native device
offset regardless of SetDataFormat**. It is neither the offset in HEIMDALL's
176-byte custom test buffer nor a canonical DIJOYSTATE2 offset. Discovery must not
change formats to try to manufacture canonical offsets. Source:
https://learn.microsoft.com/en-us/previous-versions/windows/desktop/ee416612(v=vs.85)

The War Thunder layout translator must follow the published game-driver behavior,
including its GUID-first lookup, native-offset fallback, slider allocation and
POV expansion, and retain an explanation for each included or excluded object.
Source: https://github.com/GaijinEntertainment/DagorEngine/blob/main/prog/engine/drv/hid_joystick/joy_enumdev.cpp
and https://github.com/GaijinEntertainment/DagorEngine/blob/main/prog/engine/drv/hid_joystick/joy_device.cpp

The reported 7-versus-6 TQS discrepancy is not diagnosed solely by these counts.
The preserved native roster and saved mapping must explain the extra entry before
changing its assignment translation. Counts are consistency checks after
translation, not an algorithm for guessing identities or reordering inputs.

## Shared assignment and tuning contract

The shared workflow will hold a selection containing controller identity, command,
physical input identity/direction and the specific assignment alternative(s).
Changing controller or input invalidates an incompatible target. Toolbar Apply,
command drop and context actions request the same operation on that selection.
Clear works on a reviewed alternative; several alternatives require a choice.
Input testing blocks mutations consistently. A no-op does not become a dirty
assignment. Every successful edit refreshes labels, current alternatives, action
availability and Save state from the resulting draft.

Each supported action supplies availability and a reason when unavailable. The
shared UI must render those descriptors rather than reconstruct adapter rules.
The adapter returns an operation or independent candidate; the common session
validates and stages it atomically. Existing native advanced controls must not
remain a second, silently different normal authoring workflow.

Tune Axis will render the adapter's supported parameter descriptors in one dialog.
Each descriptor needs an ID, label, value type, unit, bounds/allowed values, current
value and whether unset/clear is supported. Booleans preserve unset separately
from false. Curves remain typed lists or a supported curve model, not an assumed
percentage. Native extension parameters use the same descriptor protocol. The
adapter owns conversions and writes only the requested supported values.

DCS, Elite, Squadrons and War Thunder must all consume the workflow and dialog.
Their adapter differences must remain explicit: DCS sparse filter values, Elite
inversion/deadzone and slot semantics, Squadrons paired directional records, and
War Thunder captured native axis parameters/ranges. Existing verified behavior is
an acceptance requirement, not a reason to keep independent UI implementations.

## Ordered implementation and local gates

| Stage | Work | Completion evidence |
|---|---|---|
| 0 | Recovery checkpoint and this contract | Checkpoint branch and exact SHA recorded; no merge |
| 1 | Preserve native metadata; neutral catalog; read-only evidence export | Reader/catalog/compatibility tests, headless export smoke, local 80/80/50 and capture from actual devices |
| 2 | Complete War Thunder layout translation and drawing alias bridge | Native roster trace explains TQS and Sol-R counts, correct object/offset/POV tests, actual assignment/export check |
| 3 | Shared selection/action workflow and draft facade; migrate all four consumers | Same Apply/drop/Clear/tuning availability and transaction behavior for every consumer; exact Save/Discard checks |
| 4 | Shared parameter descriptors and one Tune Axis dialog; migrate all four adapters | Each adapter renders only supported parameters and preserves native units/unset behavior; export checks |
| 5 | Remove superseded handlers and duplicated forms; unify workspace composition | Consumer/architecture checks, existing Windows smoke, full local 80/80/50, real-game acceptance |

Stage 1 is the first implementation checkpoint. It deliberately does not claim to
fix War Thunder translation or complete the shared UI migration. The subsequent
translation stage depends on the actual raw roster that was previously discarded;
passing synthetic count examples alone is insufficient evidence.

Run `tests/validate-pr6.ps1` locally after pulling a checkpoint. This retains the
existing coverage, per-method CRAP and Windows smoke gates without exclusions or
lowered thresholds. Then run `tests/capture-controller-evidence.ps1` to create a
new JSON snapshot. That command does not load a profile, initialize the database,
acquire controls, configure force feedback, change data formats or write game files.
It creates a separate file and refuses to overwrite an existing capture. Reports
contain local device identities and must not be committed as public fixtures.

Stage 1 tests cover the documented byte layout and partial/legacy sizes, unknown
and relative axes, sparse inputs, duplicate identities, stable IDs across name and
order changes, unchanged DCS capability fingerprints and per-device failures. The
Windows smoke invokes the real command with injected discovery and verifies that
ordinary startup arguments are untouched and existing captures are not overwritten.
The authoring environment has no .NET SDK or Windows desktop; local execution is
required before progressing past this first validation gate.
