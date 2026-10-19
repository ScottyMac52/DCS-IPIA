# Local tests

These entry points package the CI commands for a Windows development checkout.
Use **PowerShell 7 (`pwsh`)**, not Windows PowerShell 5.1 (`powershell.exe`), and
run one suite at a time. The scripts select the repository root themselves and
restore the caller's directory afterward. A failed command stops its suite;
fix the first error before rerunning. There is no change to the production
assembly or asset-exporter 80% line / 80% branch gates.

## Prerequisites

- Windows 11 for the WPF, installer and release suites.
- PowerShell 7, .NET 10 SDK, Python 3.12 accessible through `py -3.12`, and Node.js 24.
- Git for the full release script's commit evidence.
- Inno Setup 6 for the full release suite. If it is absent, that underlying script
  attempts `choco install innosetup`; installing it beforehand avoids this dependency.
- ReportGenerator 5.5.11, installed or updated by `crap-tests.ps1`.

For PowerShell and Python on Windows:

```powershell
winget install --id Microsoft.PowerShell --exact --scope machine
winget install --id Python.Python.3.12 --exact --scope user
```

Open a new **PowerShell 7** terminal, or enter `pwsh -NoProfile`, then verify:

```powershell
$PSVersionTable.PSVersion
dotnet --version
py -3.12 --version
node --version
```

All eight entry points declare `#Requires -Version 7.0`. Installing PowerShell 7
does not change an already-open Windows PowerShell terminal. The installer helper
suite deliberately launches `powershell.exe` only for the Saved Games settings
compatibility test; its other helpers and the release suite require `pwsh`.

## What to run, and in what order

Commands below start from the repository root. The first five suites are
independent, but this order gets fast structural feedback before expensive builds.
Reporting depends on coverage collection; the full installer cycle is last.

| Order | Script | Purpose and dependencies | Outputs |
| --- | --- | --- | --- |
| 1 | [csharp-test.ps1](csharp-test.ps1) | C# type layout, guide links/release-note version, WPF theme keys, pinned Common inventory, migration tests and CI Python helper tests. Requires Python 3.12. | Console diagnostics. |
| 2 | [asset-export-tests.ps1](asset-export-tests.ps1) | Node exporter tests and exact parity for all twelve pinned SVGs, followed by independent exporter-module 80/80 gates. Requires Node 24 and Python 3.12. | `asset-authoring-coverage.info`. |
| 3 | [coverage-test-gates.ps1](coverage-test-gates.ps1) | Restore, verify formatting, build and run .NET tests with independent 80/80 gates for every included production assembly. Requires .NET 10. | TRX, Cobertura and OpenCover under `TestResults/raw/dotnet`. |
| 4 | [installer-helper-tests.ps1](installer-helper-tests.ps1) | Uninstaller completion/file-lock cleanup, runtime-pack notices and Saved Games settings compatibility tests. Requires Windows. | Console diagnostics; helper fixtures use temporary directories. |
| 5 | [smoke-tests.ps1](smoke-tests.ps1) | Build the Windows solution, run the WPF UI smoke application and run .NET tests on Windows. Requires an interactive Windows desktop. This repeats the unit tests, as CI does. | TRX under `TestResults/raw/windows`, pictures under `TestResults/custom-mapping-preview`, parity evidence under `TestResults/huey-parity`. |
| 6 | [crap-tests.ps1](crap-tests.ps1) | Prepare the pinned ReportGenerator tool. The uploaded filename is retained; this installs a tool rather than running CRAP tests. Safe to repeat; an existing 5.5.11 installation is retained. | `.tools/reportgenerator`. |
| 7 | [generate-test-reports.ps1](generate-test-reports.ps1) | Read the collected OpenCover/TRX files, generate coverage and CRAP/test summaries and open the HTML report. Requires steps 3 and 6; step 5 adds Windows results. Pass `-NoOpen` to suppress opening the browser. | HTML, merged Cobertura, JSON/CSV, CRAP scores, `QualityGateSummary.md`, test results and `LOCAL-SUMMARY.md` under `TestResults/report`. |
| 8 | [release-candidate-tests.ps1](release-candidate-tests.ps1) | Publish the self-contained app; build and test portable, installed and archived payloads; exercise upgrade/uninstall/reinstall and verify profiles/settings/payload hashes. Requires Windows, Git and Inno Setup. | A unique `release-assets-local-<guid>` folder with installer, ZIP, checksums and test/distribution evidence; failed drawing comparisons under `release-acceptance-diagnostics`. |

```powershell
./tests/csharp-test.ps1
./tests/asset-export-tests.ps1
./tests/coverage-test-gates.ps1
./tests/installer-helper-tests.ps1
./tests/smoke-tests.ps1
./tests/crap-tests.ps1
./tests/generate-test-reports.ps1
```

Run the complete release cycle separately in a disposable Windows development VM:

```powershell
./tests/release-candidate-tests.ps1
```

It installs and uninstalls the application with its normal installer identity;
the temporary payload directory does not make the installer registration isolated.
The wrapper supplies `RUNNER_TEMP` and `GITHUB_SHA` normally provided by Actions,
chooses a fresh output directory, and restores those environment variables afterward.
It does not create a GitHub release or write to DCS Saved Games profiles.

The shared [LocalTestSupport.ps1](LocalTestSupport.ps1) implements root selection,
native exit-code checks and Windows preconditions. It is dot-sourced by the wrappers;
do not run it as a test suite. Its focused regression test is:

```powershell
pwsh -NoProfile -File ./tests/ci/Test-LocalTestSupport.ps1
```

CI runs this regression check in its Windows solution job. Actual WPF and installer
verification remains in the existing Windows jobs. These wrappers are Windows entry
points (notably `py -3.12`); CI's Ubuntu commands continue to use `python3` directly.

## Reading the results

A normal .NET test run proves tests pass but does not collect coverage; use
`coverage-test-gates.ps1` for the gates and OpenCover needed by reporting. Coverage
failures happen after tests complete, so `failed: 0` alone is not a gate pass.

`crap-tests.ps1` only prepares ReportGenerator. `generate-test-reports.ps1` runs the
reporting tools. CRAP scores measure complexity and coverage. Reporting enforces a maximum of 50
for every measured method in the six production assemblies, alongside independent
80/80 assembly coverage. Gate failures are written to `QualityGateSummary.md`
and `LOCAL-SUMMARY.md` before the script stops. The local publisher records results found in raw TRX files;
it cannot infer a failed build from a missing TRX file. Keep the console error too.

The reporter includes files currently present under `TestResults/raw`. Repeated
runs can leave older TRX files and previews there; inspect timestamps and archive
or remove old results before a fresh comparison. The wrappers do not delete failure
evidence automatically.

## Troubleshooting the reported local failures

The supplied transcript shows two successful runs of all 841 .NET tests, successful
WPF smoke checks and all 17 asset tests passing their module coverage gates. The
installer helper and full release failures occurred before those suites could
complete. No execution of `csharp-test.ps1` is shown in that transcript.

| Message or symptom | Where to look | What to do |
| --- | --- | --- |
| `These regression tests require Windows file-sharing semantics` on Windows | [installer/Test-ReleaseCleanup.ps1](installer/Test-ReleaseCleanup.ps1), `$IsWindows` guard. Windows PowerShell 5.1 has no `$IsWindows` automatic variable, so the old guard falsely reported a non-Windows host. | Check `$PSVersionTable`; run the wrapper using `pwsh`. The explicit version requirement now rejects 5.1 before that misleading guard. |
| `A parameter cannot be found ... AsHashtable` | [../scripts/Write-DistributionNotices.ps1](../scripts/Write-DistributionNotices.ps1), `ConvertFrom-Json -AsHashtable`, called by [../scripts/Test-ApplicationRelease.ps1](../scripts/Test-ApplicationRelease.ps1). | Use PowerShell 7. Keep the hashtable conversion: the notice code uses dictionary keys and expects that representation. The release script now checks its required shell before publishing. |
| `Release test cleanup failed; preserving the original failure` and a missing `.dat` path | [../scripts/ReleaseTestCleanup.ps1](../scripts/ReleaseTestCleanup.ps1), `Remove-ReleaseTestDirectory`. This warning came from cleanup after the earlier notice-generation error. | Fix the shell failure first and rerun. If cleanup still fails in PowerShell 7, inspect that helper and the temporary path/locks; the supplied transcript does not establish a separate cleanup defect. |
| Native restore/build/test/export reports failure but later commands still run | [LocalTestSupport.ps1](LocalTestSupport.ps1), `Invoke-LocalTestCommand`. `$ErrorActionPreference` alone does not consistently stop native command failures. | The committed wrappers now check exit codes explicitly and stop before running dependent steps. |
| No OpenCover data / missing ReportGenerator | [generate-test-reports.ps1](generate-test-reports.ps1). | Run `coverage-test-gates.ps1` and `crap-tests.ps1` before generating reports. |
| C# layout, theme or Common inventory errors | `scripts/check_csharp_layout.py`, `scripts/check_theme_resources.py`, `scripts/check_common_migration.py`. | Follow the specific file/type/resource/evidence path reported by the checker. |
| Unit test, coverage or WPF smoke failure | `tests/Dcs.Ipia.Tests`, `tests/Dcs.Ipia.App.Smoke`, and the generated TRX/preview files. | Start with the named failing test and its stack trace; for coverage inspect the implicated assembly and OpenCover report. |

The transcript's reporting command successfully wrote its coverage HTML/CSV/JSON;
no reporting exception is shown. Confirm `crap-scores.csv`, `test-results.json` and
`LOCAL-SUMMARY.md` exist to verify the later Python publisher completed as well.

See the [project README](../README.md) for CI artifact downloads and the application workflow.
