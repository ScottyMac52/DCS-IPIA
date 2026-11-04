# PR6 systemic refactor contract and migration gates

Status: historical PR6 implementation contract. PR6 merged in #335, followed by
shared input selection (#344), Devices standardization (#345), War Thunder
controller creation (#346), and the input-label quality fix (#347). The final
#323 implementation reconciliation and current merge gates are documented in
[PR8 completion](shared-workspace-completion-pr8.md). The recovery branches and
ordered tasks below describe the original migration, not active branch instructions.

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
| Neutral input catalog | Driver-declared object identities, categories, ambiguity and missing-evidence diagnostics | DCS keys as universal identities, sorted order as native order, discarded unknown controls |
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

Stage 1 established the evidence boundary. Stage 2 now implements the native
layout translation against the supplied raw roster, pending local validation and
in-game acceptance. The shared workflow and UI migrations remain subsequent
stages. Passing synthetic count examples alone is insufficient evidence.

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

## Stage 2 checkpoint: declarations, authored controls and live evidence

The received capture establishes what DirectInput reported, not how many physical actuators exist. The model now uses `ReportedControllerInput`, `ReportedInputKind` and `ControllerCapabilities.ReportedInputs`. The evidence report advances to schema 2, replacing `physicalInputs` with `reportedInputs`, adding declared counts, and explicitly leaving the physical control count unconfirmed and live verification unperformed. Existing saved capability payloads remain compatible: the renamed catalog is a derived, JSON-ignored projection; the retained native objects and legacy supported-input fields are unchanged.

The VKB example exposes why this distinction is mandatory: native discovery declares 128 button slots and the saved game mapping reserves 128, while the shared drawing defines sixteen button aliases. The owner's joy.cpl observation of 32 is a separate UI observation; this discovery report does not measure that UI or infer the physical count from it. Raw slots must remain available for game-index translation without automatically creating physical controls or claiming reachability.

Stage 2 implements the ordinary War Thunder driver layout in separate adapter-owned classes. All native slots are linked to neutral reported IDs and receive a trace. A separate compatibility bridge maps authored drawing aliases to those IDs. Native storage tags are not physical aliases: a slider's authored key can refer to a different native game slot than its driver storage name suggests. The bridge cannot shrink, reorder or renumber the saved game ranges.

The private evidence plus published driver rules reconciled the saved axis/button counts of all ten connected devices. TQS's seventh native axis comes from an unknown-GUID object at raw offset 28; Sol-R's offset-36 extra object is excluded and its POV contributes two native axis slots. The authored regression fixtures retain only generic protocol shapes, not personal instance IDs or native configuration content.

This checkpoint still requires `tests/validate-pr6.ps1` locally and actual main-throttle assignment/export validation. The shared workflow, four-consumer migration and common Tune Axis dialog remain stages 3–5, not completed work. Existing captures remain useful and do not need regeneration merely because the next evidence export has clearer schema-2 terminology.



## Stage 3A: common assignment execution checkpoint

The four existing workspaces now consume `WorkspaceAssignmentWorkflow`. The shared application boundary owns fresh open/read-only/busy/testing checks, immutable target scope, review followed by a scope/draft recheck, non-reentrant execution, actual snapshot change detection and one completion result. Native translation and atomic mutation remain in each adapter or the DCS draft service.

Entry points migrated in this checkpoint:

- War Thunder: native editor requests, callout Apply, command drop and callout/native Clear share the same operation. The native editor no longer mutates the session itself.
- Elite: Apply and command drop, plus toolbar/callout Clear, use the common boundary and portable adapter review.
- Squadrons: Apply/drop retains paired-axis handling inside its native operation. Reviewed record clears and controller changes execute after the shared scope/draft recheck; keyboard/mouse/gamepad remain read-only.
- DCS: command Apply/drop and single-assignment Clear use the common boundary around the existing conflict-aware draft service. An occupied-control confirmation rechecks its scope and original draft before committing.

Native session review is non-mutating, including export/schema validation. A repeated native or portable mutation that leaves its profile identical does not emit a false session Changed notification. The workflow also reports an unchanged result when a native action claims success without changing the snapshot.

This is an execution checkpoint, not completion of the shared presentation refactor. Existing selection widgets, capability-dependent availability rendering, DCS callout copy/move and bulk/advanced operations, and the game-specific tuning dialogs still need migration. No native range renumbering, hardware capacity changes or export-schema changes belong to this checkpoint. Completion events describe results; the existing draft facades still own undo, persistence and atomicity.

Local acceptance for this checkpoint:

```powershell
git pull
.\tests\validate-pr6.ps1 *>&1 | Tee-Object -FilePath .\pr6-refactor-stage3-results.txt
```

For each game: assign through Apply, repeat the same assignment, assign through drag/drop, clear a single assignment, Discard and verify the original, then repeat and Save/reopen. Confirm drawings and Save state follow the draft, and export the saved profile. War Thunder acceptance includes a TQS throttle axis and SOL-R axes. Existing 80% line/80% branch per assembly and maximum method CRAP 50 gates remain unchanged. GitHub Actions must not be run.

The development environment has no .NET SDK or Windows runtime. C# tests and WPF smoke behavior are pending the local run; source checks are not substitutes for those gates.



## Stage 3B: common selection and live action availability

`GameAssignmentSelection` now represents workspace/context scope, controller, command, native alternative, slot, pending input and failed-translation feedback. All four workspaces consume it. Scope changes reset the target; controller/alternative changes invalidate old input; adapters explicitly repopulate native fields from the newly selected record. War Thunder's extra blocked-selection boolean has been removed in favor of the shared selection state.

`WorkspaceAssignmentActionSupport` is adapter evidence, separate from runtime gates. Native compatibility, range checks, paired-axis metadata, reserved DCS modifiers and record-shape capabilities still come from their existing adapter/draft boundaries. `WorkspaceAssignmentActionState` combines that evidence with the common open/read-only/busy/testing policy for Apply, Add Alternative, Clear, Tune Axis, Reverse Axis, Inspect, Start, Stop and Fit. Unsupported actions are hidden; supported unavailable actions remain disabled with an explanation. Start requires live hardware evidence; Stop remains available during acquisition even if the workspace is busy or closing. Read-only native system inputs remain protected.

The common presenter renders live action state on buttons and pointed-control context menus. Drag-over/drop applies current workspace gates plus the adapter's compatibility check. FeatureActionBar now binds the source button's live tooltip and visibility; previously its tooltip was a navigation-time snapshot, so changing availability could leave the visible toolbar explanation stale. Clear and Tune state is refreshed when opening a context menu, independently of the last left-click selection.

Windows smoke checks cover actual rendered toolbar updates, common menu/button agreement, and War Thunder live-input action gates. Unit tests cover mutation gates, supported/unavailable distinctions, recovery Stop, read-only Inspect/Fit, input readiness and complete selection transitions. Existing native editing, preservation and draft smoke checks remain in the full PR6 validation script.

This checkpoint does not finish common screen composition or replace the native tuning dialogs. The remaining steps are the shared capability-driven Tune Axis form, remaining bulk/advanced-operation migration, and consolidation of workspace presentation/handlers. Production coverage thresholds and CRAP measurement remain unchanged.

Run locally:

```powershell
git pull
.\tests\validate-pr6.ps1 *>&1 | Tee-Object -FilePath .\pr6-refactor-stage3b-results.txt
```

Then check a button and an axis in each game: change controller/command/context/alternative, Apply, drag/drop, Clear, start/stop testing, Save/reopen, and Discard. Compare toolbar and context-menu availability for the same target. Supported unavailable actions should explain why; untranslatable callouts must not reuse the preceding input. No controller-evidence recapture is needed. .NET/WPF and 80/80/50 acceptance require the user's Windows run; this environment has no .NET SDK or Windows runtime. All commits use `[skip ci]`; do not run GitHub Actions.

