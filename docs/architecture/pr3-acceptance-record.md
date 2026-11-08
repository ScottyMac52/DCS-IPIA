# PR 3 acceptance record (issue #357, profile operations)

Status: **draft — Windows acceptance pending.** Linux evidence below was executed; Windows smoke cases are compiled
on Linux (`EnableWindowsTargeting`) but run only on Windows. Earlier Windows passes were recorded at `cf804e1`, before the
save-boundary/owner-thread changes, and must be re-run at the final SHA.

## Cross-adapter transaction semantics (one shared sequence, native hooks only)

All four adapters inherit `GameProfileOperationsBase` (DCS via `DcsProfileOperations`; ED/SQ/WT via `GameStoreProfileOperationsBase`)
and all typed commands inherit `GameProfileCommandBase`. Ordering, admission, cancellation, owner-thread resumption, post-commit outcome and
notifications are implemented once; the table lists only the native differences.

| Concern | Shared owner | DCS | Elite Dangerous | Squadrons | War Thunder |
| --- | --- | --- | --- | --- | --- |
| Order | precheck → admission → stop input → prepare → persist once → resume on owner → accept → refresh → status | same | same | same | same |
| Fallible prep before write | `PrepareSave` | edition, pinned UI layer, Lua/integration validation, accepted draft baseline | name + `GameWorkspaceContract` validation | name validation | name validation |
| Durable write | `PersistAsync` once | SQLite repository (no revision; editing lock held until write) | file store, optimistic revision | file store, optimistic revision | file store, optimistic revision |
| Accept | `AcceptCommit`; failure → Committed + RecoveryRequired; context blocks stale work until a fresh session is installed | installs prepared draft | reopens a fresh native session | accepts in place | accepts in place |
| Discard | `RestoreSaved`, no write | rebuild draft via `DcsSavedWorkspaceDraft` | session discard | session discard; never-saved import closes | session discard |
| Cancellation | before persist → Cancelled, nothing written; after persist → Committed | lease token not passed into the repository write | store write is synchronous | same as ED | same as ED |
| Notifications/status | context `Publish`/`ReportStatus` on owner | native editor surfaces (empty shared message) | native saved message | shared messages | shared messages |
| Export readiness (unchanged policy) | `GameProfileCommandBase.Availability` | disables for module errors | offered when saved; refuses unresolved references on run | offered when saved; refuses duplicate declarations on run | offered when saved |

## P01–P08 map

| ID | Evidence (Linux, executed) | Windows (real adapters) |
| --- | --- | --- |
| P01 | `GameProfileOperationsTests.Dirty_save_writes_once…`, `ProfileLifecycleConformanceTests.Open_edit_save_reopen…` (ED/SQ/WT real backend+store), `DcsLifecycleNativeConformanceTests` (DCS SQLite) | `ProfileOperationsConformanceSmoke` all 4 — pending re-run |
| P02 | `GameProfileOperationsTests.Clean_readonly_closed_and_unsupported…`, lifecycle clean-save NoChange | `ProfileOperationsConformanceSmoke`, `EditionSmoke` — pending |
| P03 | `Failed_save_retains_pending_state…`, `Cancellation_before_commit…`, lifecycle conflict + pre-commit cancellation, DCS invalid candidate before persistence | pending |
| P04 | `Refresh_failure_after_commit…`, lifecycle refresh failure (any type) and refresh cancellation; accept failure → RecoveryRequired with stale work blocked until reopen (`Recovery_required_blocks_stale_work…`) | pending |
| P05 | `Discard_restores_without_writing…`, `Discard_after_save_restores…`, lifecycle discard, `DcsSavedWorkspaceDraftTests` | command smoke DCS discard — pending (failed at `66194dd`, fixed in `cf804e1`) |
| P06 | `AsyncWorkspaceLeaveTests`, `GameWorkspaceLifecycleTests` | window leave/close smokes — pending |
| P07 | `GameProfileCommandTests`, `WorkspaceOwnerThreadTests.Native_accept_after_real_suspension…`, SQ duplicate refusal + reviewed slot removal (lifecycle) | `ProfileCommandConformanceSmoke` export/import all 4 — DCS/ED export passed at `cf804e1`; SQ refusal/removal, WT, imports never executed |
| P08 | lifecycle refresh failure is a committed warning; preference failures surface through refresh | pending |

Owner thread: `WorkspaceOwnerThreadTests` covers applied/failed/cancelled completion with and without the owner context, reentrant
blocking, native accept on the owner after a real suspension, and disposal queued ahead of an off-owner completion (released once).

## Stale-session recovery

`WorkspaceState.Recovery` is set when a committed save cannot be installed (before or part-way through native acceptance). While set:
every mutation route refuses with unchanged state: the context name and receipt (`SetEditedName`, `Revision`), every
`GameProfileSession` mutator (also through a retained reference), and for DCS the sealed session (`SealStaleNative` hook: labels,
structure edits and the retained draft are suspended). Save/discard/edit/export admissions are blocked. The shared recovery-aware
`GameWorkspaceLeaveGuard.ConfirmAsync(state, …)` used by every window leave (profile/game switch, import, close) abandons the stale
session without a prompt, a stale discard or another write, because its content is already durable; the next open reads the store.
Ordinary pending-change prompts are unchanged. Evidence: `Recovery_rejects_stale_mutation_and_the_shared_leave_guard_abandons_it_without_writing`
(ED/SQ/WT × pre/partial install) and `WorkspaceOwnerThreadTests.Recovery_transition_after_real_suspension_runs_on_the_owner`
(seal, notifications and recovery clear on the owner after a real suspended write; Linux, executed).
`RecoveryConformanceSmoke` (Windows, compiled on Linux, **not yet run**) drives the production entry points with a native install
observer failing on an existing change event after the single write: the DCS seal (labels, structure edit, retained draft, save,
discard refused with native and durable state unchanged; disposal releases the seal and the detached draft cannot reach durable
state), and a real Squadrons window (profile switch, import, game switch and close leave without a prompt or write; ordinary
Cancel/Discard/Save prompts unchanged; mounted rename/save disabled and refused; mounted document equals the durable receipt).
A pre-install failure has no production trigger in the window adapters: each native accept installs first (SQ/WT
`AcceptSaved`, DCS `Saved`/`Working`, ED constructs a session from the already validated candidate), so pre-install is covered at
the shared contract level only. Detached sessions/drafts are not frozen by recovery; they are isolated (they cannot reach the mounted or durable state).

## Known gaps and exceptions

- DCS session/context lifecycle (open → edit → save → reopen → discard through `DcsWorkspaceSession`) runs only in the Windows smokes; Linux
  covers the native repository and draft composition on the same fixtures.
- Store-backed writes are synchronous, so real asynchronous suspension inside the store save is not exercised; the shared base is covered with real suspension.
- C# layout gate: one owner-approved exception (2026-11-08) for exactly `internal interface IEliteWorkspaceServices` in
  `src/Heimdall.App/Games/Elite/IEliteWorkspaceServices.cs`, namespace `Heimdall.App.Games.Elite`. It exposes native ED types that
  Heimdall.Application cannot reference. Any other path, namespace, name or a public declaration still fails, Application stays
  ED-free (`tests/ci`), and all other layout, region and documentation rules are unchanged. The layout output now matches main's 89-line baseline.

## Windows native review (to be completed by the reviewer at the final SHA)

- [ ] Full `Heimdall.App.Smoke` run: context, presentation, profile operations, command conformance (all exports, SQ refusal and slot removal, imports), Elite drawing cache, later smokes.
- [ ] Manual: save/discard/close on each game window; Squadrons duplicate-declaration export message and Remove unused slot; ED reviewed preset replacement; DCS output.
