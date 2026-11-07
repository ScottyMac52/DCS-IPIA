# PR 2 context integration review

Scope: issue #357, PR #359, following the Windows smoke failure at the War Thunder native index edit. This reviews context consumers and completes neither the later role migrations nor real-game acceptance.

## Ownership and findings

| Boundary | Required behavior | Finding and correction |
| --- | --- | --- |
| Shared assignment view | Reload native fields when native content or mounted identity changes; update gates for selection, name and activity notifications | The observation snapshot included the pending profile name. Use the native assignment snapshot and seed observation on attachment. A required availability hook now separates gate updates from native reloads for all four inheritors. |
| Operation notifications | Preserve pending fields on admission, failure and release when native data is unchanged | Busy notifications update availability; native reloads wait until the operation releases. Failed saves retain the pending editor and context name. |
| Shared window release | Notify availability after every outcome; reload native panels only after native data or mounted identity actually changes | The window unconditionally reloaded panels on release, undoing the assignment view's pending-field preservation after failures. Observe the operation's starting identity/fingerprint and reload only on an actual change. This observation is not another operation lock. |
| War Thunder editor | Changing a pending index must not rebuild that index from the saved BLK | The window called `RefreshEditor` for every context notification. Window and input-state callbacks now use availability-only refresh. Native content/browser selection still explicitly loads records. |
| War Thunder source and destination | The sidebar owns the browser controller; the editor's destination is part of its immutable native edit | Native editor target changes were rewriting the context's browser controller. Remove those writes, invalidate the old pending input when choosing a destination, and batch alternative/input selection publication. |
| DCS selection publication | Observers see one complete scope/controller/command/input selection | Batch the native selection synchronization. Availability updates use the action presentation path, avoiding a native editor reload. |
| Controller selection | Aggregate/system/missing browser entries cannot become physical controller slots | Squadrons now requires an existing native joystick slot. ED resolves its selected controller against native devices; War Thunder's native editor no longer rewrites the browser source. DCS derives its profile selection from the native diagram. |
| Saved names and discard | Context, native baseline and visible name agree after release; failed saves keep pending names | Squadrons release refresh reads `State.EditedName`. Elite and War Thunder explicitly restore the native name on discard. All three existing native inheritance smoke scenarios retain failure/retry/discard checks. |
| Overlapping discovery | New supported scans supersede obsolete scans; input testing/identification and writes stay exclusive | Replacement is owned by the shared context lease. Native callbacks in ED, Squadrons and War Thunder reject cancelled/stale results. DCS uses the same activity contract. |

Shared bases contain no simulator-ID branches or native edit models. Native persistence and review actions remain behind the current typed adapters; their later role-base migrations remain separate PRs under #357.

## Behavioral checks

`WorkspacePresentationConformanceSmoke` mounts the real `GameWorkspaceWindow` and `GameAssignmentsView` bases together against **each production context adapter** (DCS, Elite, Squadrons and War Thunder), with native sessions and snapshots supplied by `WorkspaceContextCases`. The same assertions cover:

- First pending-input notification after attachment, name overlays and activity gates preserve editor fields while updating availability.
- New scans replace obsolete scans without editing native data.
- Failed save admission/release preserves the pending editor, native data and edited name.
- Real native edits reload projections; discard restores the baseline.
- Same-content mount replacement invalidates pending fields.
- Unmount detaches notifications; remount loads authoritative data.

This common consumer supplements the native window smoke cases; it does not substitute for them. In particular, `WarThunderWorkspaceSmoke` checks that index 3 survives context/name notifications and dispatcher pumping, leaves browser ownership and native data unchanged before Apply, and then stages exactly one native edit through the shared workflow. Its original exported `joyButton:i=3` assertion remains enforced.

`WorkspaceStateChangeTests` verifies native fingerprints versus presentation overlays, operation gates, readonly changes, fresh identity and real content changes. `WorkspaceScanReplacementTests` protects late-result/failure cancellation and lease lifetime. Existing all-adapter context, native save/discard, busy/reentrant mutation, training-isolation and selection tests remain enabled.

## Validation boundary

The ordinary .NET coverage/CRAP gates measure the nine inner production assemblies, **excluding `Heimdall.App`**. Passing those gates is evidence for the measured code only. A Windows-targeted build establishes compilation, not WPF runtime behavior.

The new presentation conformance cases and native window smoke cases require execution on Windows. Keep PR #359 in draft until the complete Windows smoke suite passes on its final head and the changed UI behaviors have been reviewed. No native assertion or quality threshold is relaxed by this review.
