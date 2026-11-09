# Performance profiling

Use the optimized **Profile** build to investigate responsiveness across DCS,
Elite Dangerous, Squadrons and War Thunder. Debug and Release exclude the
instrumentation hooks and do not start the capture session or heartbeat timer.
Profile outputs are separate under `bin/Profile`; this configuration is for
measurement rather than distribution.

## Capture a scenario

Requires Windows, PowerShell 7, .NET 10 SDK and Python 3.12. From the repository root:

```powershell
git status --short
git rev-parse HEAD
./scripts/Run-PerformanceProfile.ps1 -ProfileName performance
```

The launcher builds Profile, opens an isolated persistent HEIMDALL test profile,
waits for a normal exit, then generates JSON, Markdown and CSV in the printed
unique `TestResults/performance/<session>` directory. The normal Production
profile is not opened. Import representative saved snapshots into the test
profile before comparing workloads. Keep hardware connections, data and edition
consistent. User settings and imported test data persist between launches.

For direct use without the launcher:

```powershell
dotnet build HEIMDALL.sln -c Profile
$env:HEIMDALL_PERFORMANCE_DIRECTORY = Join-Path $PWD 'TestResults/performance/manual'
dotnet run --project src/Heimdall.App/Heimdall.App.csproj -c Profile --no-build -- --test-profile performance
Get-ChildItem $env:HEIMDALL_PERFORMANCE_DIRECTORY -Filter 'capture-*.json' |
    ForEach-Object { py -3.12 scripts/performance_report.py $_.FullName --output $_.DirectoryName }
Remove-Item Env:HEIMDALL_PERFORMANCE_DIRECTORY
```

Without the environment override, captures go to `%LOCALAPPDATA%/HEIMDALL/Performance`.
Captures are written once on normal application exit. A crash or forced termination
can leave no capture; absence of a report is not a pass. An IO failure emits a
trace warning and leaves shutdown operational; the launcher treats a missing
capture as a failure. Each session creates a new file; archive or remove old
capture folders when no longer needed. There is no automatic disk retention policy.

## Compare all four simulators

Use a separate capture for each simulator and repeat each case three times on
the same machine, first recording its commit, dataset size, connected controllers
and exact steps in your comparison notes. Do not commit personal snapshots or captures.

1. Start the test profile and open the same saved workspace. Record cold open separately.
2. After loading settles, search the same command text, clear it, change filters
   ten times and select ten commands and alternatives.
3. Switch controllers ten times, select physical inputs, and open/redraw the diagram.
4. Stage one binding, save, discard a second edit, and repeat the navigation sequence.
5. Close normally. Label the folder with the simulator/scenario in your notes.
6. Repeat for DCS, Elite, Squadrons and War Thunder using comparable data sizes.

Compare mean/P50/P95/max and counts for matching operation/component rows.
Use captured counts and dataset notes to distinguish more refresh work from a
slower individual refresh. A rebuilt projection is `browser.rebuild`; reuse of
the cached projection is `browser.filter`. `browser.refresh-request` also counts
requests that defer/coalesce. `editor.render` measures native assignment-view
refresh, including availability and observer work. These do not separately time
each importer, exporter, device scan, diagram renderer or save implementation;
add a targeted hook after the shared timings identify the next area to investigate.

## Read the evidence correctly

- All operation durations are inclusive wall time, including nested work, awaits
  and exceptional exits. They do not establish success and must not be added
  across nested groups. Startup measures WPF `OnStartup`, not the entire process launch.
- Dispatcher queue delay measures scheduler posting to callback start. The 100 ms
  background heartbeat measures excess interval, a responsiveness proxy that can
  be delayed by higher-priority dispatcher work. It does not measure frame rate.
- Process CPU and managed allocations cover process lifetime; private bytes are
  sampled at exit. GC counts cover the capture lifetime. They are not per-operation
  allocations, working-set peaks or evidence of a memory leak.
- At most 100,000 samples are retained. Later samples increment `Dropped`.
  A truncated report describes only the first retained operations. Repeat a
  shorter scenario; do not compare it as if it represented the complete session.
- Profiling adds timers, allocation, locking and a 10 Hz dispatcher heartbeat.
  Reports use exact nearest-rank percentiles over retained samples. Threshold
  counts above 16/50 ms are diagnostic columns, not new acceptance gates.
- Captures contain fixed operation/simulator tags, timings, build/runtime/OS and
  process statistics. They contain no search text, assignments, device names,
  workspace paths or profile contents. Nothing is uploaded automatically.

## Validation

```powershell
./tests/csharp-test.ps1
py -3.12 -m unittest discover -s tests/ci -p test_performance_report.py
dotnet test tests/Heimdall.Tests/Heimdall.Tests.csproj -c Release --filter FullyQualifiedName~PerformanceCaptureTests
dotnet test tests/Heimdall.Tests/Heimdall.Tests.csproj -c Profile --filter FullyQualifiedName~PerformanceCaptureTests
./tests/coverage-test-gates.ps1
./tests/smoke-tests.ps1
```

For manual acceptance, capture at least one non-empty session for each simulator,
verify its build identity and report totals, then launch Release and confirm it
creates no capture. Timing numbers are observations until compared against an
agreed baseline. This feature makes no claim that performance has improved.
