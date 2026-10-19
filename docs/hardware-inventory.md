# User hardware inventory (Feature 3)

**Edition availability:** Free adoption requires verified Thrustmaster HID evidence and a reviewed catalog template for authoring. Previously verified units can remain selected offline. Other brands and generic definitions require licensed access; see [the edition guide](design/free-edition.md).

Use **Hardware → Discover devices… / Rescan** from the main window, without opening a module. Review each unit's native name, physical instance GUID, product GUID, connection status, enumerated controls, unsupported-object explanations and artwork candidates. Select the units you want to adopt, give identical units distinct labels/roles, then choose **Save reviewed inventory**. Newly scanned units are never preselected. **Manage devices…** reopens saved units offline until a successful rescan.

Scanning creates observations only. Cancellation/global failure retains the previous review rows and saved inventory. A device that disconnects during its own enumeration appears with an error and cannot be newly adopted until supported controls are verified. Previously saved units retain their pinned definition and selection. No device is silently removed, renamed by role, rebound to another physical GUID or added to a module. Module device selections and assignments remain independent; adopting inventory into modules and generic assignment/output are implemented in the [generic hardware workflow](generic-hardware.md).

A changed control fingerprint displays **controls changed (review required)**. Saving labels/selections keeps the old definition unless you explicitly check **Adopt changed controls**. Old definitions remain available. A reconnect with unchanged evidence restores the connection observation; identical names never merge physical units. A replacement PC/device with a different DirectInput GUID requires explicit adoption/rebinding in the existing module workflow.

## Capability interpretation

Identity/object enumeration opens DirectInput8 device interfaces without acquiring input, setting a data format, configuring force feedback or writing DCS files. It runs off the UI thread and checks cancellation inside enumeration callbacks and between devices. Native calls cannot be forcibly interrupted while a driver itself is blocked; cancellation is honored at the next callback/return.

The same `ControllerCapabilityMapper` supplies inventory and live testing:

- Actual native button instances map to JOY_BTN1–JOY_BTN128; gaps stay gaps.
- Supported absolute X/Y/Z/RX/RY/RZ axes and up to two sliders map to the existing 176-byte live-test format. Sliders are ordered by native instance, independently of callback order.
- Up to four actual POV objects map to JOY_BTN_POV1–4, with eight directional keys per hat. Hat counts count objects, not directions.
- Relative/unknown/excess objects and duplicate native/DCS identities remain explained diagnostics. Ambiguous controls are excluded, rather than inventing a mapping.

DirectInput game controllers are the backend scope. DirectInput cannot reliably attest physical versus virtual provenance, so supported virtual controllers use the same explicit-adoption and control-validation policy. Other USB/HID device classes are not scanned by this feature. Existing keyboard authoring/testing support remains available in module editing; controllers are not inferred from keyboard sources.

Bundled artwork recognition combines native-name recognition rules with an exact match against enumerated DCS controls and no unsupported objects. All compatible candidates remain visible; ambiguous candidates do not select artwork. These are hints for later reviewed artwork adoption, not evidence of an attached grip. Personal template GUIDs, modifier buttons and assignments are never imported into inventory. Licensed users can inventory supported unbundled units and use [image-free authoring/rendering](generic-hardware.md).

## Persistence, migration and recovery

SQLite migration **8** adds three tables transactionally, leaving all existing module tables/assignments unchanged:

| Table | Contract |
| --- | --- |
| hardware_definitions | Reusable content-addressed capability definitions; schema version 1, supported objects plus unsupported evidence |
| hardware_inventory | Exact physical GUID, reviewed definition, native name, user label/role, selection, per-unit compatible template hints |
| hardware_observations | Last scan evidence, including product identity and per-device failures; historical connection state is never trusted after reopening |

Fingerprints include stable native type/GUID and interpreted keys/offsets, excluding localized labels and callback order. Definitions can be shared across repeated units; name-specific artwork hints belong to each physical instance. Current-machine observations, reusable definitions, physical-unit selection and existing module profiles have separate identities. Saves validate versioned contracts and relationships before an atomic transaction. Stale edits are rejected; omitted saved units are retained.

The public `HardwareInventoryContract.Export/Import` contract provides version-1 portable inventory JSON. Import validates control interpretation and relationships and clears historical connectivity. A reviewed caller can save the validated snapshot through `IHardwareInventoryRepository`; this feature does not add another snapshot UI. Existing per-module snapshots remain per-module and do not overwrite user inventory.

Full profile backups include all three inventory tables through SQLite online backup. Recovery requires these tables for schema 8, rejects newer schemas, and migrates older profiles/backups to empty inventory without adopting controllers or personal defaults. Restored inventory remains disconnected until rescanned. Production and named test profiles use their own inventory databases; no inventory operation accesses Saved Games or deployment files.

## Validation and remaining manual acceptance

Deterministic tests cover supported/unsupported/duplicate objects, live-state decoding, identical units, no-device and failure/cancellation cases, explicit adoption, stale edits, disconnected reopening, reviewed capability changes, portable snapshot validation, migration from schema 7, profile isolation and full backup/restore while preserving existing modules. Windows smoke performs real DirectInput enumeration without acquiring controllers and exercises the main-window commands plus a fake repeated-device discovery/save/reopen UI flow.

Manual acceptance on Windows: use an isolated profile; rescan two MFDs/quadrants with identical names and an unbundled controller; compare native GUIDs and button/axis/hat counts with live testing; select and label units; save/reopen; unplug/reconnect/rescan; confirm selections and existing module assignments remain intact. Record capability errors and artwork ambiguity where present. This hardware-specific evidence requires the user's physical setup and is not replaced by a controller-free CI runner.
