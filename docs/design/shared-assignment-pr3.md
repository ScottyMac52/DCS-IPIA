# Issue #323 — PR 3: shared assignment presentation and selection

## Scope and first consumer

Builds on main after PR 2 (`41a89ded533a08623c458153b9f84fa0747df2c2`). Elite is the first production consumer of the shared browser, editor frame, and UI-independent selection/feedback state. Full DCS/Elite host migration is PR 4; selective Squadrons parity migration remains PR 5. Neither protected feature branch is rewritten or deleted.

## Ownership

- `GameCommandEntry` contains adapter-supplied section, display category, supported native input types, and actual assignment owners. A native format can have mixed or unknown types; the browser never guesses types from labels.
- `GameCommandBrowser` intersects section/category/type/text/ownership filters and orders by category, label, then native identity. It reads projections without mutating catalogs, profiles, or native records.
- `GameCommandBrowserView` receives typed input/status options from the adapter. It has no hard-coded simulator types or ownership option ordering.
- `GameAssignmentSelection` owns selected controller/command/native slot/input. WPF lists mirror that state; native identifiers stay opaque.
- `GameAssignmentEditorState` combines the adapter's native review with current busy/live-test state to derive action availability and adjacent feedback.
- `GameAssignmentEditorView` composes the selected command and feedback with an injected native editor. Elite inversion, deadzone, slots, combinations, and command toggles remain in its native field view.
- `EliteCommandBrowser` is now a compatibility adapter over the shared browser. Existing category, assignment ownership, and native format rules are retained.

## Selection transitions

| Transition | Selected command | Pending input | Native profile |
| --- | --- | --- | --- |
| Same controller, catalog refreshed/reordered | Preserve visible identity | Preserve until native editor reload | Unchanged |
| Change controller | Preserve command if visible | Clear, then reload that controller's binding | Unchanged |
| Filter hides selection | First visible command, or none | Clear, then load matching native data | Unchanged |
| Change command or binding slot | New identity/slot | Clear, then load matching native data | Unchanged |
| Open/replace/delete workspace | Reset selection before refresh | Clear | Existing session lifecycle rules |
| Select input on drawing or in picker | Current command/input | Native key supplied by adapter | Unchanged until Apply |
| Drop command on drawing | Select command and native key | Same validated Apply path as picker | Staged session edit |

Apply/Clear availability and live-test feedback are derived rather than retained as stale success flags. Changing controllers clears stale workspace success text. Saves, discards, and exports retain their existing session and adapter boundaries.

## Validation

Unit tests cover all shared ownership filters, scope/category/type intersections, unknown and mixed input types, text search, stable ordering, controller/command/slot/workspace transitions, native adapter projections, and editor feedback/action states. Existing Elite tests characterize native ownership and lossless source behavior.

Windows smoke checks retain actual drawing selection, drag/drop, Clear/Tune, save/discard, filters and no-drawing fallback. Additional assertions require typed adapter descriptors and correct command/input refresh after controller switches.

Acceptance before merge:

- Release build including WPF and smoke project, with warnings treated as errors.
- Unit tests, independent 80% line/branch assembly gates, and method CRAP <= 50.
- `tests/smoke-tests.ps1` on Windows, including resizing/scaling and shared editor reachability.
- Manual Elite command filter, controller switch, picker/callout/drag assignment, Clear/Tune, save/reopen/export checks.
- Existing DCS/Squadrons workflows unchanged.

Cross-compilation verifies C#/XAML compilation; it does not establish Windows GUI or in-game acceptance.

## Authoring verification

- .NET SDK 10.0.403 on Linux: 1,607 unit tests passed, none failed or skipped.
- Coverlet independent 80% line / 80% branch gates: all eight measured assemblies passed. Application: 96.62% line / 86.78% branch; Elite: 99.40% / 91.38%.
- Repository CRAP gate: all 1,815 measured methods passed the ceiling of 50.
- WPF app and smoke project cross-compiled with `EnableWindowsTargeting=true`; Windows execution/manual GUI acceptance remains required.
- `git diff --check` passed.
- `dotnet format style` could not open its MSBuild workspace because this environment denies the build host's named-pipe connection. No formatter pass is claimed; run the standard Windows coverage script (which includes formatting verification) before merge.

## Additional repository checks and CI limitation

Documentation and theme-resource checks passed. The C# layout checker reports no diagnostics in this PR's new/modified files; it still reports 71 diagnostics in unchanged baseline files. Existing baseline layout failures are not claimed fixed by this scoped extraction.

After nonsemantic XML/region/file-layout cleanup, 58 focused shared-presentation and Elite browser tests passed again. The full 1,607-test/coverage results above precede only that cleanup, not a behavior change.

GitHub Actions run 40717308139 did not start its jobs. The check annotation states: "The job was not started because your account is locked due to a billing issue." This is separate from local build/test results. No successful GitHub CI run is claimed.
