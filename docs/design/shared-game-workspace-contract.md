# HEIMDALL multi-simulator workspace — PR 0 baseline and architecture contract

Status: **baseline proposal**, issue [#323](https://github.com/ScottyMac52/DCS-IPIA/issues/323). This document defines migration acceptance, not a new UI implementation.

## Frozen GitHub reference points (2026-10-27)

| Ref | Commit SHA | Status at capture | Purpose |
| --- | --- | --- | --- |
| `main` | `8f287f34d7dd538e90d62abfb71a76f67ce1dfe5` | mainline | Source branch for PR 0 and later shared-contract PRs |
| `fix/squadrons-controller-associations` | `18babcc6134d2fb7adb362765aef3646b410e37e` | 203 commits ahead of main; 38 changed files | Preserve working native/drawing features and user fixes; **do not** merge as-is |
| `feature/war-thunder-adapter-foundation` | `568c6723430f334352d389194f70da1f8f3c2fc5` | 4 ahead / 2 behind main; 7 changed files | Separate BLK-native foundation; no workspace migration in PR 0 |

The SHAs are historical snapshots; branch tips may advance independently. Never reset, squash, force-push, rewrite, or delete either feature branch during this program. Before migrating any functionality, record its current source SHA and keep an audit trail of tests and functionality transplanted. PR 0 intentionally does **not** merge either feature branch.

## Current ownership inventory (observed, not desired)

| Concern | Existing implementation | Shared today? | Migration rule |
| --- | --- | --- | --- |
| Native profile import/export and input encoding | `Heimdall.Dcs`, `Heimdall.EliteDangerous`, `Heimdall.Squadrons` | Adapter boundary | Native codecs and precise validation remain game-specific |
| Saved workspace draft/persistence | `GameProfileSession`, `GameWorkspaceContract`, `GameWorkspaceServices` | Elite and Squadrons | Keep independent snapshot, optimistic revision and save/discard semantics |
| Hardware discovery/linking | `GameControllerIdentification`, controller observations, captured-slot APIs | Substantial reuse | Native slot IDs must not be conflated with physical device identities |
| Drawing rendering | `EliteDiagramPresenter`, shared SVG renderer and drawing definitions | Used by Elite/Squadrons feature branch | Extract shared presenter with typed callbacks; never fake implemented menu actions |
| Drawing navigation | `DiagramViewport` and local feature-window code | Partly shared | Fit, zoom, scroll, pan consistent; preserve the Squadrons branch's measured scroll fix when migrating |
| Command selection and assignment editing | DCS `AssignmentsView`, Elite feature views, Squadrons window | Mostly separate | Compose shared surfaces with adapter-owned action plans |
| Workspace toolbar, navigation, pending status | DCS, Elite, Squadrons windows | Duplicated presentation | Extract a common shell and session coordinator incrementally |
| Tuning/clearing/context menus | Working draft operations in DCS, adapter-specific in Elite, partial in Squadrons | Inconsistent | Capabilities must control menu visibility and actual command execution |
| No-drawing fallback / native diagnostics | Selected game-specific views | Partial | First-class drawing, table-only, and text-only modes |
| Reference sheets, input testing | DCS and Elite feature workflows | Partial | Optional feature surfaces, not simulator requirements |

Current app code-behind is **not** a shared contract. UI event handlers may mediate selection but must not implement native record ID parsing, index allocation, format-specific mutation or validation.

## Behavioral contract for every migrated simulator

1. **Stable identity.** Controller logical/native reference, captured slot and physical GUID are distinct. Changing the displayed controller only selects a projection. It must never mutate source native bytes or stage new associations.
2. **Independent draft.** Any successful authoring command stages an isolated result. Save marks the persisted revision only after storage succeeds. Discard restores the last persisted snapshot. Stale success notices and drawing callouts must refresh when changing the selected controller or loading a different workspace.
3. **Safe native evidence.** Imports preserve unrecognized native records and settings. A display filter must never delete, normalize, or infer data. Exports must round-trip unchanged when authoring has not explicitly modified those native fields.
4. **Whole physical input.** An analog axis is one physical control even where a simulator represents its +/- motions in separate native records. Native adapters decide whether both are required, how to stage them atomically and which negate/identifier fields must remain unchanged.
5. **Real commands only.** Selecting a menu action must call an implemented operation. Unsupported tune/clear/creation must not masquerade as working interactions. A presenter may expose typed commands only when the owning application capability is available.
6. **One interaction model.** Command selection, controller selection, input selection, action staging and feedback must be consistent across games; click and drag should use the same command path and validation.
7. **Optional surfaces.** Simulator capabilities may enable/disable Devices, Assignments, Input Testing, Reference Sheets, modifiers, tuning, or no-drawing fallback without introducing another entire workspace window.
8. **Accessibility and visual feedback.** Resize and scroll operations preserve hit-test alignment; errors are adjacent to the attempted action; pending state is derived from session state rather than stale status text.

## Characterization tests already present on `main`

- `SquadronsAssignmentViewTests`: verified/unverified input filtering and native slot ordering.
- `SquadronsDeviceLinkTests`: reviewed hardware association, save/reopen/discard, stale review protection, no unintended native changes.
- `GameControllerOrderTests`: display order independence from native device order and saves.
- Existing DCS/Elite tests validate game-specific authoring details; no claims of complete GUI parity.

## New PR 0 tests

`tests/Heimdall.Tests/SharedWorkspaceBaselineTests.cs` provides:
- Native bytes and profile object unchanged while selecting/filtering different captured controller slots.
- Positive/negative native axis record presence and independent preserved metadata.
- Session staged rename, discard, accept saved, discard after saved and native-export invariance.
- Hardware verification versus advanced review: display-only filtering that retains raw evidence.

These tests intentionally cover established **mainline** behavior. They do **not** claim that the 203-commit Squadrons feature branch has been integrated, or that its newer interactions are passing. Before Squadrons migration, add branch-specific parity tests for diagram callouts, label collisions, native dual-direction edits, controller switching and measured-viewport scroll.

## User interaction smoke matrix (Windows; to execute and record)

| Scenario | DCS | Elite | Squadrons feature branch | Acceptance |
| --- | --- | --- | --- | --- |
| Load / change / save / discard profile | Pending | Pending | Pending | Saved/unsaved state follows current profile |
| Change controller while a command is selected | Pending | Pending | Pending | Callout/rows/tooltip switch to matching device; no native mutation |
| Select control, assign via click and drag | Pending | Pending | Pending | Both paths share one validated command |
| Clear selected binding via context menu | Pending | Pending | Pending | Actual clear or action absent if unsupported |
| Tune axis via context menu | Pending | Pending | Pending | Real tuning, or no tune item |
| Drawing zoom, fit, scroll, pan and resize | Pending | Pending | Pending | No clipped unreachable controls; aligned hit regions |
| Native +/- axes and control collision | N/A per sim | N/A per sim | Pending | Correct complete/partial label and lossless source |
| No drawing / table-only / unsupported features | Pending | Pending | Pending | Functional fallback with accurate capabilities |

No Windows GUI tests were run as part of authoring this document. CI success, merge readiness and real native game acceptance remain unverified until test results are attached to the relevant PR.

## PR sequencing / gates

- **PR 0:** characterize baseline and record this ownership/capability contract; zero production behavior changes.
- **PR 1:** shared workspace chrome and guarded lifecycle coordinator.
- **PR 2:** shared diagram presenter, capability-aware context actions and common viewport.
- **PR 3:** shared command browser/editor and deterministic UI state model.
- **PR 4:** incremental DCS/Elite consumer migrations without losing native functionality.
- **PR 5:** selective Squadrons feature migration from its protected branch, with parity tests.
- **PR 6:** obsolete-code cleanup, full cross-game regression, War Thunder readiness audit.

Each PR must follow the repository's **80% line / 80% branch per measured assembly and CRAP ≤ 50** quality gates plus applicable Windows smoke. No PR may claim GUI verification from source inspection alone. War Thunder UI work remains outside this program until the common shell and capabilities are proven.
