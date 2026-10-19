# Hardware setup

**Edition availability:** Free setup accepts verified Thrustmaster units; previously verified units remain usable offline. The generic/unbundled-controller exercises below require Owner access. See [the edition guide](design/free-edition.md).

A fresh profile opens hardware setup after the main window is ready. Setup automatically scans attached controllers using the same capability discovery and inventory review used by **Hardware → Discover devices… / Rescan**. No module is required. New units are unselected until you choose them.

1. Review the Saved Games DCS destination. Browse to an existing folder, or leave it empty and explicitly choose offline setup. The wizard never creates destination folders or deploys DCS files.
2. Select controllers and edit distinct instance labels and roles. Inspect actual buttons, axes and POV controls and any enumeration errors. Unbundled units default to generic support. Compatible bundled mappings are optional preferences; adding a unit to a module still requires an explicit artwork choice and uses its own physical GUID.
3. Confirm neutral/empty defaults for new modules. Existing modules, artwork, assignments and shared-default revisions are preserved. Deliberately copying a shared revision remains available in New Module.
4. Confirm setup. Inventory adoption, destination and completion commit together. A failed or cancelled confirmation rolls back the whole transaction.
5. A successful confirmation offers the existing verified full-profile backup tools. Continue with New Module, Import Module or Open Module from the main window.

Setup is offered automatically once per profile. The offer is remembered independently of completion, so **Skip for now** or closing the wizard suppresses later automatic popups; setup remains available on demand. Skipping, closing, cancelling a scan or a discovery failure does not adopt devices or mark setup complete. The main window remains usable. Finish without controllers or a destination only by explicitly selecting offline setup. Per-device enumeration failures are shown; units without supported control evidence cannot be newly adopted.

Use **Settings → Resume / rerun hardware setup** to review the current profile again. **Data & recovery → Test profiles and reset… → Rerun setup** opens the same wizard. Rerun preserves completion and saved data and begins a fresh scan. Disconnected selected units retain their labels and definitions. Changed capabilities remain pinned unless **Adopt changed controls** is explicitly checked. Rescanning prompts before discarding edited inventory rows. Cancelling or closing a rerun preserves the previous confirmed setup and does not make it reopen automatically.

## Persistence and compatibility

SQLite schema 9 adds one `profile_setup` row containing completion and the confirmed destination. `SetupManager` stages adoption with `HardwareInventoryManager.Prepare`; `SqliteSetupRepository` checks the expected inventory and writes inventory and setup state in one SQLite transaction. Scanning does not adopt hardware. Schema 10 adds a separate `offered` flag to `profile_setup`; opening the wizard records the offer without changing completion, destination or inventory. Completed schema 9 profiles migrate with the offer already recorded. Completion has no dependency on application version, so ordinary restarts and upgrades do not restart finished setup.

Schema 8 profiles with existing modules or adopted hardware are treated as established profiles during migration and are not forced through new-user setup. Legacy `onboarding-state.json` completion remains honored. Clearing or explicitly setting completion moves it to the database and removes that legacy marker. Profiles with neither saved modules nor inventory are offered setup if they have no prior completion marker.

The confirmed SQLite destination takes precedence over legacy JSON settings. Before confirmation, a null destination preserves the user's current JSON preference. Ordinary destination browsing updates both the existing preference file and the SQLite destination; theme and assignment replacement preferences are unchanged. After wizard confirmation, the settings service reloads the committed destination without another settings write.

Full-profile backups include the database and any legacy marker, so completion, destination and inventory restore together. Full backups also preserve whether setup was offered. Restoring an unfinished profile only offers setup automatically if it has never been offered; a previously dismissed or completed profile stays quiet. Module snapshots do not replace profile setup. Resetting a profile creates a fresh migrated database and offers setup on the next launch.

## Validation

Automated tests cover read-only scans, cancellation, injected failure after inventory writes, stale review, explicit offline completion, unsupported units, disconnected reruns, capability adoption, profile isolation, upgrades and verified backup/restore. Windows WPF smoke exercises automatic fresh-profile launch, skip followed by a quiet restart, explicit resume and generic confirmation, completed startup, both rerun entry points, cancelling/closing reruns, another quiet restart and offline completion. Access-key labels, standard WPF tab navigation and the editable grid support keyboard use.

Manual acceptance on Windows: start `Dcs.Ipia.App.exe --test-profile onboarding-check` with an unbundled controller; tab through destination, selection, label and mapping; confirm; reopen the same profile; rerun and skip; rescan after unplugging/reconnecting; create and restore a verified full-profile backup. Inspect Saved Games to confirm no deployment occurred. This physical-controller acceptance requires the user's Windows machine.
