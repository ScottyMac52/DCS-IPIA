# User hardware inventory (Feature 3)

For the bundled right/left Sol-R grip layouts and AVA configuration, see
[Sol-R grip on AVA](sol-r-ava.md).

**Edition availability:** Free adoption requires verified Thrustmaster HID evidence and a reviewed catalog template for authoring. Previously verified units can remain selected offline. Other brands and generic definitions require licensed access; see [the edition guide](design/free-edition.md).

Open **Devices…** on the game selection screen or in any game workspace. This opens the same profile-owned device manager. The main menu also provides **Devices → Discover devices… / Rescan** and **Manage devices…**. Review the controllers, select those you want to use, give identical units distinct labels/roles, choose **Drawing (all games)**, then **Save reviewed inventory**. New units are never preselected. Saved units remain visible offline until a rescan observes a current connection.

## Use devices across games

- DCS, Elite Dangerous, Squadrons and War Thunder use the same adopted physical inventory. New game links and live testing require a current connection; saved connection history never establishes availability.
- Choose the preferred artwork once in Devices. New links use that preference automatically, including DCS New Module. Generic controls are an explicit option. Existing game-specific drawing pins remain unchanged until an explicit replacement, so changing the shared default cannot silently rewrite an existing configuration.
- Game workspaces call their association page **Controllers**. Linking a controller to an imported native game reference remains game-specific; a Windows instance GUID cannot establish Squadrons slot numbers or ED reference indices.
- Unplugging a controller retains its profiles, assignments, labels and artwork. Its saved configuration remains visible but controller assignment editing is read-only until that adopted physical unit reconnects. System keyboard support retains its existing behavior.
- Global device names are display labels. Native names, GUID-qualified filenames and game bindings are not renamed.
- Input testing reports inherited, unreviewed DCS scopes as untested while allowing reviewed module hardware to run. Export still requires physical review of every inherited profile.

The shared drawing preference is an optional field in the existing inventory JSON. No database migration, profile recreation or assignment rewrite is required. Older inventories load without a preferred drawing; choose one in Devices to set the default for future links.

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

Automatic bundled recognition combines native-name rules with exact enumerated control evidence and no unsupported objects. The explicit drawing selector also offers layouts matching all supported keys while retaining unsupported objects as diagnostics; choosing artwork never invents controls or proves an attached grip. Personal template GUIDs, modifier buttons and assignments are never imported into inventory. Licensed users can inventory supported unbundled units and use [image-free authoring/rendering](generic-hardware.md).

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
