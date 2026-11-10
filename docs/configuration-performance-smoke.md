# Configuration performance smoke

Run a supplied **Elite Dangerous `.binds`**, **Squadrons `ProfileOptions_profile_synced`**, or **War Thunder `.blk`** through a fresh Windows WPF process. This is an opt-in Profile-build scenario, independent of full smoke acceptance. For DCS, use a HEIMDALL Complete release ZIP, or an OVGME ZIP plus its matching exported module snapshot (see below).

```powershell
.\tests\performance-smoke.ps1 -Game elite -ConfigurationFile 'C:\captures\MyControls.4.2.binds'
.\tests\performance-smoke.ps1 -Game squadrons -ConfigurationFile 'C:\captures\ProfileOptions_profile_synced'
.\tests\performance-smoke.ps1 -Game war-thunder -ConfigurationFile 'C:\captures\controls.blk'
```

The runner copies the file, creates a saved configuration through the actual game workspace import path, closes/reopens it from an isolated store, exercises assignment navigation, search, controller selection and command selection, writes a native review export, imports the exported file into a second independent store and repeats the interactions. It compares export/re-import/export payloads exactly, including unknown native fields, and checks the provided file's SHA-256 is unchanged. It does not populate or alter your normal HEIMDALL database.

This tests **identity-preserving native review export** through the production session exporter and review-file writer. It does not run live-controller association, artwork linking, ED's connected-device installation preflight, or write active game files. Missing or invalid native data fails the scenario; it is not silently repaired. No physical controllers or paid license are required: the smoke process uses the same ephemeral signed test entitlement as existing smoke tests and empty device discovery.

Artifacts are retained under a unique folder in `TestResults/performance-smoke`:

- `scenario.json`: outcome, failure details, per-phase wall time and process-wide allocation deltas.
- `capture-*.json`, `.csv`, `.md`: the existing Profile telemetry, including dispatcher heartbeat delays and nested browser/editor spans.
- `input/`, `store/`, `export/`, `reimport-store/`: isolated evidence for troubleshooting.

These artifacts contain the supplied bindings and should stay local unless deliberately shared. Failed runs retain artifacts too. A hung process is killed after ten minutes and is reported as a failure with a partial runner log; an abruptly killed process may not have a finalized telemetry capture.

The default budget is **1,000 ms per measured interactive action**, with 20 iterations before and after re-import. Creation/import/export phases are measured separately without a total-duration limit; dispatcher heartbeat stalls anywhere in the workflow are also checked against the UI budget. All interactive samples, including cold navigation, must meet the budget. Thresholds are configurable, not proof of frame-rate or CPU performance:

```powershell
.\tests\performance-smoke.ps1 -Game war-thunder -ConfigurationFile 'C:\captures\controls.blk' `
    -Iterations 40 -UiBudgetMilliseconds 250 -TimeoutMinutes 15
```

Compare identical files, iteration counts and builds on the same machine. Wall times include dispatcher pumping and nested spans overlap. Allocation deltas are process-wide, not exclusive to a method. A native round-trip success does not claim full game installation or hardware acceptance.

## DCS OVGME round-trip

A plain OVGME ZIP contains generated Lua and PNGs, not editable command catalogs, drawing controls or page definitions. The runner therefore requires either a **HEIMDALL Complete release ZIP** containing the OVGME package and authoring snapshot, or both separately exported artifacts:

```powershell
.\tests\performance-smoke.ps1 -Game dcs -ConfigurationFile 'C:\captures\DCS-F-14-Components-1.0.0-Complete.zip'

.\tests\performance-smoke.ps1 -Game dcs -ConfigurationFile 'C:\captures\DCS-F-14-Components-1.0.0-OVGME.zip' `
    -DcsSnapshotFile 'C:\captures\F-14.heimdall-module.json'
```

Use the module's **Export Complete release** action, or **Build OVGME package** together with **Export module snapshot** from the same saved state and version. The test rejects a missing or mismatched snapshot before creating any module; it does not fabricate editable data from PNGs.

DCS validation uses the production Complete/OVGME verifiers without extracting archives to disk. It restores the snapshot into an isolated SQLite database, reopens the real module window, exercises controllers, commands, assignments, reference sheets and pages, builds OVGME output and exports a module snapshot through the real output buttons, then verifies and restores those new artifacts into a second database. The same UI workload runs again. Snapshot comparison covers all persisted authoring data; package comparison covers entry paths and Lua/PNG/metadata file bytes while ignoring ZIP timestamps and compression metadata. Existing module identities are preserved by the production restore workflow, so they do not need normalization. No DCS deployment or input testing is performed.
