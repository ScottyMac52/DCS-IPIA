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

## Attributing shared refresh work

Profile builds record `editor.refresh-core` and `editor.availability` at the inherited assignment-view boundary. The shared diagram presenter records `diagram.compose`, `diagram.rasterize` (cache misses only), and `diagram.hit-regions`, which includes tooltips and pointed-control menu construction. Compare these scopes with the enclosing scenario and dispatcher delays before choosing another optimization.

For Elite, Squadrons and War Thunder, the shared profile session records `assignment.review`, `assignment.review-candidate` and `assignment.review-baseline`. The unchanged baseline is validated once per session/content stamp and retained as private immutable file/string pairs. Candidate validation and live access/input checks still run on each review; actual mutation and export remain independently validated. Save, discard and content changes invalidate the comparison baseline. DCS retains its native module validation contract rather than being forced into this profile format.

These scopes overlap and do not cover every layout or dispatcher operation. Their durations must not be added together or treated as exclusive CPU/allocation measurements. Tags contain no controller identifiers or native configuration values.

## DCS OVGME round-trip

A plain OVGME ZIP contains generated Lua and PNGs, not editable command catalogs, drawing controls or page definitions. The runner therefore requires either a **HEIMDALL Complete release ZIP** containing the OVGME package and authoring snapshot, or both separately exported artifacts:

```powershell
.\tests\performance-smoke.ps1 -Game dcs -ConfigurationFile 'C:\captures\DCS-F-14-Components-1.0.0-Complete.zip'

.\tests\performance-smoke.ps1 -Game dcs -ConfigurationFile 'C:\captures\DCS-F-14-Components-1.0.0-OVGME.zip' `
    -DcsSnapshotFile 'C:\captures\F-14.heimdall-module.json'
```

Use the module's **Export Complete release** action, or **Build OVGME package** together with **Export module snapshot** from the same saved state and version. The test rejects a missing or mismatched snapshot before creating any module; it does not fabricate editable data from PNGs.

DCS validation uses the production Complete/OVGME verifiers without extracting archives to disk. It restores the snapshot into an isolated SQLite database, reopens the real module window, exercises controllers, commands, assignments, reference sheets and pages, builds OVGME output and exports a module snapshot through the real output buttons, then verifies and restores those new artifacts into a second database. The same UI workload runs again. Snapshot comparison covers all persisted authoring data; package comparison covers entry paths and Lua/PNG/metadata file bytes while ignoring ZIP timestamps and compression metadata. Existing module identities are preserved by the production restore workflow, so they do not need normalization. No DCS deployment or input testing is performed.

The `scenario.ovgme-export` total contains three child timings that are already included in the total. Use `scenario.ovgme-build` for the production package button, `scenario.snapshot-export` for the editable snapshot button, and `scenario.ovgme-verification` for the independent rebuild and payload comparison. Do not add the child timings to the total.

DCS Assignments also records `diagram.dcs-projection` for the native caption and modifier projection. The shared bitmap cache records `diagram.rasterize` on misses. The DCS editor retains at most 12 bitmaps and 64 MiB of estimated decoded pixels. Scope and chord changes refresh interactive callouts while reusing unchanged prepared artwork.

Controller selection reports separate `scenario.controller-selection.first`, `scenario.controller-selection.warm`, and `scenario.controller-selection.same` timings. A warm sample is a switch back to a controller already visited in that pass; selecting the current row is counted separately. Visits reset after re-import. For the 100 ms warm p95 target use the warm row. To compare with older all-switch captures, pool the three raw sample groups before computing the percentile.

Elite's assignment availability checks share the last exact draft review for the current workspace content stamp. The cache compares ordered modifier values, checks access on each request, and retains one result. Apply still performs a fresh review and validates the write. In a repeated refresh of an unchanged draft, only the first availability check should produce an `assignment.review-candidate` sample.

The first assignment review in a newly opened native profile reuses the payload already produced during opening validation. After reopen or re-import, the first valid draft should record a candidate review with no `assignment.review-baseline` sample until the workspace content stamp changes. A synthetic Elite regression checks this extra-validation count and compares managed bytes allocated on the review thread with an otherwise identical draft that rebuilds its baseline. Candidate and write validation remain live.
