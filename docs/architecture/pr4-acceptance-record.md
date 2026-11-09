# PR4 acceptance record — shared assignment browsers (#357)

Status: draft. Implementation starts at main `cc3e67e60d7930b6fdb183d1ea900b7ed17769cc` after merged PR3 (#361). Windows UI execution and local formatter acceptance are pending. This PR completes the implementation of the browser role, not the remaining editing/drawing/controller/Devices migrations or the overall issue.

## Actual ownership

| Role | Shared owner | Native hooks / presentation edge |
| --- | --- | --- |
| Interface and typed data | `IGameAssignmentBrowser`, `IGameAssignmentBrowser<TCommand,TRecord,TFilter>` in Application.Interfaces | Native command, record and filter types; no `object`/`dynamic` payload |
| Query and content invalidation | `GameAssignmentBrowserBase.Query`, `RefreshProjection` | `ReadCommands`, `ReadRecords`, `NativeVersion`, `PrepareQuery`, `IncludeCommand`, `IncludeRecord` |
| Stable selection and error handling | `SelectVisibleCommand`, `CommandSelection`, `ReconcileRecordSelection`; context owns selection | `ScopeSelection`, `FilterForSelection`, typed command/record address hooks |
| Publication | `PublishProjection`, `ProjectionChanged` | WPF bindings replace collections only when their projection instance changes; native editor reloads follow selection/content changes |
| Lifetime | `SetActive`, `Dispose`; `GameAssignmentsView.BindBrowser` and window cleanup | Cached views suspend browser selection ownership while unmounted; the owning workspace releases subscriptions on close |
| DCS | `Heimdall.Dcs.DcsAssignmentBrowser` inherits the shared base | `DcsCommandsManagementView` consumes it; native module/UI-layer palettes, live draft read port, every-device status and catalog structural operations remain native |
| ED | `Heimdall.EliteDangerous.EliteAssignmentBrowser` inherits the shared base | App's `Games.Elite.EliteAssignmentBrowser` inherits the native adapter and binds grouped WPF command/slot controls |
| Squadrons | `Heimdall.Squadrons.SquadronsAssignmentBrowser` inherits the shared base | App's native browser inherits it; editing consumes `ISquadronsAssignmentBrowser<SquadronsWorkspaceRow>`; raw review and assignable decoding are separately cached |
| War Thunder | `Heimdall.WarThunder.WarThunderAssignmentBrowser` inherits the shared base | App's native browser inherits it and binds occurrence controls; indexed assignment presence includes system-only native assignments |

The production native adapters are UI-free, so the Linux conformance suite executes the same native implementations used by the WPF inheritors. WPF adapters contain control binding/event translation, not a second filter/cache/selection algorithm. Browser presentation receives typed actions for editor/controller/drawing interactions instead of concrete sibling factories.

## Removed duplicate call paths

Before: each App browser and DCS command manager rebuilt projections, filtered collections, chose fallback targets, managed selection and replaced ItemsSource independently. The three native windows also rebuilt catalog/record projections.

After: TextChanged/filter changes → typed native query → `GameAssignmentBrowserBase.Query` → cache invalidation by workspace identity/content version/native evidence → native predicates over cached typed projections → shared stable-ID selection → shared publication → WPF binding. Window refreshes consume `AllCommands`/`AllRecords`; they do not rebuild browser catalogs or row sets independently. DCS's independent `GameAssignmentSelection` and dynamic anonymous-row selection were removed.

Input/slot-only notifications do not refilter the catalog. Queries entered during an operation are deferred until release; selection during persistence is refused. Unknown/stale command or record IDs clear unsafe targets. Native initial-open selection policies never restore a fallback after an invalid ID. Identical result collections retain their instances. `SetActive` prevents cached native views from competing for the same context selection.

Browser projection errors retain the last published rows, clear unsafe targets, report status and permit retry. The browser performs no assignment mutations or persistence. Scope/controller transitions retain their native meanings through required hooks.

## Native behavior retained

- DCS: module/UI-layer separation, draft ownership, layer/controller/every-device status, native modifier scope, typed palette commands; catalog import/edit transactions stay in the management panel.
- ED: grouped display categories, section/type/ownership filters, Primary/Secondary/Binding slot addresses and native combinations.
- Squadrons: aggregate/system/joystick sources, unknown/unbound types, raw record review versus assignable records, paired-axis/native template semantics and hardware verification. Cached hints use the same planner policy as before.
- War Thunder: source occurrence order, system/keyboard assignment presence, controller ownership, unsupported/empty record shapes and native name/ID search.

The shared command ListBox explicitly enables recycling and virtualization while grouped. Browser filtering itself does not rebuild artwork; drawing-scope changes remain explicit native actions.

## Executable evidence

- Red contract build: `AssignmentBrowserContractTests` could not compile before the browser implementation/probe existed (`/tmp/pr4-red.log` in the execution workspace).
- Runtime red cases retained their assertions: invalid War Thunder command selection expected `null` but restored `controls/hotkeys/ID_FIRE`; native War Thunder search parity exposed unwanted category matches; cached Squadrons hints expected one editable record but reported zero because raw-review decoding differs from assignable decoding. Native record selection also expected `JOY_Z` but produced `null` after the context cleared a previous address. The shared base now establishes the native record address and applies its fresh input evidence within one deferred notification. The implementation was corrected for each case.
- `AssignmentBrowserContractTests`: warm invalidation/publication, invalid IDs, failure/retry with retained rows, reentrant refresh, input-only notifications, busy selection and deferred query behavior.
- `AssignmentBrowserConformanceTests`: the same production-adapter cases for all four games; no native data mutation, warm document-read counts, controller/scope changes, already-dirty content changes, disposal, cached activation, close/reopen and retained identities.
- `AssignmentBrowserNativePolicyTests`: native status/type/controller parity, DCS every-device/layer behavior, Squadrons raw-versus-assignable capability hints and War Thunder system-only assignment presence.
- Stress fixture: 2,000 native DCS commands and 10,000 native assignment records; warm queries must not read the document again.
- `BrowserPerformanceSmoke`: actual mounted WPF windows for all four games. Reports UI-thread p50/p95, allocations, defensive copies, native panel refreshes, collection publications and browser build counts for search, command selection and controller/scope switching. Warm p95 target is 100ms; an over-budget run fails for investigation. It also checks native data preservation, cached view reuse and zero publications/native panel refreshes for repeated identical refreshes. Runs through the existing full smoke group scenario.

## Measurement interpretation

The unit benchmark compares the previous call pattern through retained native helpers with inherited cached queries over the same synthetic capture. It is a controlled backend comparison, not a claim about measured old/new Windows UI responsiveness. Fixture sizes and allocations are reported with the tests. Windows timings, realistic user-profile review and the tested commit must be recorded before merge; cross-compilation is not that evidence.

Remaining editing/drawing/controller refresh work belongs to PR5–PR7. UI measurements include those native downstream costs so they remain visible, rather than treating a fast browser kernel as proof that every interaction is fast.

## Controlled backend measurements

One executed Linux run, 20 warm queries per row; small synthetic captures, no WPF rendering:

| Adapter | Visible commands in fixture | Previous call pattern | Inherited cached queries | Allocations before / after |
| --- | ---: | ---: | ---: | ---: |
| DCS | 3 | 0.31 ms | 0.36 ms | 90,280 B / 15,240 B |
| ED | 5 | 0.16 ms | 0.25 ms | 75,400 B / 18,760 B |
| Squadrons | 126 | 248.87 ms | 2.07 ms | 111,330,120 B / 283,400 B |
| War Thunder | 7 | 72.76 ms | 0.13 ms | 16,768,360 B / 24,360 B |

All four recorded zero warm document copies and retained one content build. Tiny ED/DCS timings vary at sub-millisecond scale; this evidence does not claim they became faster. The separate 2,000-command / 10,000-record DCS stress case measured 30.20 ms and 1,222,680 B across 20 queries, with no additional document reads. The principal demonstrated gains are eliminating repeated catalog/source work in Squadrons/War Thunder and reducing allocations; real UI latency remains the Windows acceptance gate.

## Validation

Thresholds, exclusions and existing native assertions are unchanged.

| Check | Observed result |
| --- | --- |
| Release solution rebuild, Windows cross-targeting and EnforceCodeStyleInBuild | PASS: zero warnings/errors after the fresh-input selection fix |
| Focused production browser tests | PASS: 21/21 after the fresh-input selection fix, including the original failing JOY_Z assertion |
| Earlier full unit/coverage run | PASS: 2,391 tests; all nine assemblies independently above 80% line / 80% branch; CRAP <= 50 (2,460 measured methods) |
| Final full unit/coverage rerun after fresh-input selection fix | UNCONFIRMED: the execution environment disconnected during this run; its final output could not be recovered |
| Python CI checks | PASS: 68 tests |
| Theme resources and documentation checks | PASS before the final acceptance-note update |
| C# layout checker | 89 baseline violations, 89 current, zero added before the final UI diagnostics |
| dotnet format style | NOT CONFIRMED: Roslyn build-host IPC failed in this environment; run the repository formatter gate on Windows |
| Windows WPF smoke, native UI review and p50/p95 evidence | PENDING |
| Last two App/smoke diagnostic additions | PENDING compilation/runtime verification: native-refresh/publication counters and identical-refresh assertions were added after the successful rebuild, before environment loss |

The earlier full-run aggregate was 96.97% line / 87.65% branch. This is evidence for that tested snapshot, not confirmation of the final source. Per-assembly results were Domain 92.54/87.80, Application 97.45/88.45, Infrastructure 95.01/86.46, Dcs 96.80/86.92, EliteDangerous 98.00/90.09, Squadrons 97.32/85.88, WarThunder 98.89/91.00, Rendering 97.80/85.71 and Packaging 98.14/87.23 (line/branch percentages).

Before merge, run the full Windows smoke and coverage scripts on the published head, confirm formatter exit zero, run the explicit CRAP gate, and record the commit, complete outputs, machine/fixture sizes, timings and native UI review. Existing layout findings remain visible; this PR does not waive or weaken them.

```powershell
.\tests\smoke-tests.ps1 *>&1 | Tee-Object .\pr4-357-smoke.txt
.\tests\coverage-test-gates.ps1
python scripts/assert_quality_gates.py --coverage TestResults/raw/dotnet/coverage.cobertura.xml --raw TestResults/raw/dotnet --output TestResults/quality --summary TestResults/quality-summary.md --crap-ceiling 50
```
