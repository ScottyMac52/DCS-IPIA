# PR6 — shared workspace closure and War Thunder readiness (#323)

## Scope and baseline

Base: `main` at `23a5dfadb27262467249a3378e794cc91f7e881a`, after merged Squadrons PR5 (#332). This is the cleanup and readiness gate defined by #323, not the complete War Thunder integration. The protected historical feature branches are retained. No active game files or saved workspaces are migrated.

The shared shell now has DCS, Elite and Squadrons consumers. The former Elite-only diagram presenter and phantom "Tune / assign axis" action are absent from current source. Existing feature-specific views remain because they own native behavior; they are not obsolete merely because their names include a simulator. This audit therefore removes no working consumer windows.

## Ownership and reuse audit

| Concern | Existing owner to reuse | War Thunder requirement |
| --- | --- | --- |
| Profile lifecycle and persistence | `GameWorkspaceShell`, `GameWorkspaceChromeState`, `GameWorkspaceLeaveGuard`, `GameWorkspaceServices`, `GameProfileSession` | Implement `ISimProfileBackend` before opening a portable profile session |
| Optional features and action bar | `GameWorkspaceFeatureHost`, `FeatureActionBar` | Register only implemented features and actions |
| Command browsing and selected command | `GameCommandBrowserView`, `GameAssignmentSelection` | Native catalog/context/parameter projection in the adapter |
| Assignment validation and apply | `GameAssignmentEditorView`, `GameProfileSession`, native editor interfaces | A verified adapter operation must handle both Apply and diagram drops |
| Artwork and gestures | `HardwareDiagramPresenter`, `HardwareDiagramInteractions`, `DiagramViewport` | Adapter maps control keys and supplies actual select/drop/clear/tune callbacks |
| Device ownership | `ControllerLinkState`, `ControllerLinkPresentation`, session preview/apply operations | Native instance/slot identity needs evidence; display names and numeric offsets are insufficient |
| Physical live feedback | `HardwareInputFeedback` and controller discovery/capability services | Physical feedback cannot establish native game ownership by itself |
| Native output | `ISimProfileBackend.Export`, `GameReviewExport` | Lossless adapter export followed by a separate review copy; no implicit deployment |
| Native parsing | `Heimdall.WarThunder` | Keep BLK grammar, typed values, record addressing and command encoding out of WPF |

The generic `SimBackendCapabilities` record does not grant every operation. Existing native editor interfaces and registered UI callbacks remain the capability boundary. War Thunder must implement only interfaces whose native contracts it can validate.

## Native foundation findings and fixes

Repository fixtures are generic authored examples. A privately reviewed game export establishes root-level `controls`, repeated hotkey blocks, `controls/deviceMapping/joystick` records and native `yes`/`no` booleans; the capture itself is not published. It is not an `ISimProfileBackend` implementation and has no verified command catalog, joystick ownership mapping or binding creation contract. A successful BLK parse is not proof that a game profile can be authored.

PR6 fixes these concrete capture/edit problems:

- Exported root-level `controls/hotkeys` and `controls/axes` now populate their inventories, alongside the older wrapped shape. Saved devices under `controls/deviceMapping/joystick` are included without flattening repeated records or treating them as physical identities. Native `yes`/`no` flags parse and edit without normalization.

- `:t` text previously bypassed string validation; `:s` accepted merely starting/ending quotes. Both now require exactly one complete quoted token, including correct escapes. Quoted punctuation stays inside that token.
- Comments immediately after `=` could be captured as the value. Trivia is now skipped, and the scalar span points to the real value. Replacements preserve the comment and all surrounding text.
- Real-number replacements now reject nonfinite values and overflow. Replacement values cannot inject extra tokens, comments, structure, newlines or NUL.
- Retained unsupported syntax or scalar values mark the document `HasOpaqueSyntax`. Such successfully captured documents support unchanged export and inspection but expose no editable settings. Unsupported structural forms can still be rejected during capture; this remains a subset parser.
- Repeated same-name blocks/scalars retain their source positions. A bare path is editable only if it identifies exactly one scalar. No occurrence address or record identity is invented.
- Scalar/block inventories now expose read-only collections, so callers cannot forge editable source spans.
- Invalid captured activation values remain available for inspection; numeric inventory skips unparseable values without assigning tap/hold meanings.

`EditableSettings` means eligibility for a reviewed literal scalar change. It does not imply command creation, physical association, modifier support or axis tuning. The current editable subset is bounded 32-bit `i`, `yes`/`no` and `0`/`1` booleans, finite `r`, and quoted `s`/`t`. Additional boolean spellings, compound/vector types and additional BLK forms require native contract work; they are not silently normalized.

## Readiness decision

| Capability | Current state | Required before activation |
| --- | --- | --- |
| Unchanged source round trip | Implemented for accepted captures | Real exported fixture acceptance |
| Unique captured scalar replacement | Implemented for the supported subset | Native context/parameter validation |
| Repeated binding occurrence identity | Ambiguity blocks edits | Stable occurrence-aware addresses |
| Command dictionary and parameter schema | No verified native contract | Catalog from evidence, retaining unknown commands and fields |
| Controller links and joystick inputs | No verified native contract | Explicit identity plus verified native button/axis translation |
| Blank profiles and binding creation | Unsupported | Valid native envelopes, identifiers and required default records |
| Axis pairs, tuning, clear, modifiers | Unsupported | Individual capability contracts and atomic native operations |
| Portable backend and saved game profile | Not implemented | `ISimProfileBackend`, diagnostics and native identity preservation |
| Desktop game selection | Unregistered | Working backend and capability-driven consumer tests |
| Active-game deployment | Unsupported | Export remains a separate review copy |

**Decision: shared UI reuse is ready; native binding authoring and desktop activation are not yet ready.** Implementation is tracked in [#333 — native contract/backend](https://github.com/ScottyMac52/DCS-IPIA/issues/333), followed by [#334 — shared desktop consumer](https://github.com/ScottyMac52/DCS-IPIA/issues/334). Neither task requires per-command editing branches or a new universal window.

## Validation and merge gate

PR6 adds `WarThunderReadinessTests` for quoted text, comments/value spans, invalid/injected replacements, finite numerics, opaque capture behavior, duplicate paths, immutable inventories, activation inventory and capture limits. Existing foundation tests remain. War Thunder is added to the per-assembly coverage include lists and the coverage/CRAP report's expected assemblies, keeping the 80% line / 80% branch / CRAP <=50 thresholds unchanged.

No new WPF feature is registered in this PR. Existing DCS/Elite/Squadrons Windows smoke remains a non-regression requirement. This authoring environment has no .NET SDK or Windows runtime; added .NET tests, measured coverage, CRAP and GUI smoke require local execution. Source review and Python/Node checks do not substitute for those results.

Run from the repository root:

```powershell
dotnet build -c Release
dotnet test tests/Heimdall.Tests/Heimdall.Tests.csproj -c Release --filter "FullyQualifiedName~WarThunder"
.\tests\coverage-test-gates.ps1
.\tests\crap-tests.ps1
.\tests\generate-test-reports.ps1 -NoOpen
.\tests\smoke-tests.ps1
```

GitHub Actions results are outside this local review. Before subsequent desktop activation, #333 must supply a representative native schema/fixture contract and #334 must exercise real shared assignment actions, save/discard/reopen, device switching, review export preservation and in-game recognition.
