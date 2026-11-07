# Workspace role acceptance design

Applies to [ADR-0002](../adr/0002-enforced-game-workspace-role-contracts.md), the [exact proposed APIs](workspace-role-contracts.md) and [native migration map](workspace-role-migration.md). These are test specifications, not passing production tests. PR 1 must be reviewed as architecture; PRs 2–10 implement and execute the cases below. All four games participate in every migrated role before that PR merges.

## Reusable fixture contract

Place UI-free reusable fixtures in `tests/Heimdall.Tests` alongside existing Application tests; place dispatcher/render/input/layout fixtures in `tests/Heimdall.App.Smoke`. A native fixture builder supplies real role instances behind interfaces and typed native test ports. Do not implement a fake subclass that bypasses the production native inheritor and count it as game conformance. Pure base tests with injected failure ports supplement, rather than replace, the four native fixtures.

Each fixture must supply: writable dirty and clean sessions; readonly/edition-restricted and empty/new sessions; a stable selected controller/command/slot/alternative/input; a second session with the same key but a new generation; canonical native snapshot/source bytes before and after; saved database/store state and revision where applicable; real event recorder; controlled review callback; failure-injectable native store/discovery/input ports; resource/subscription counters; asynchronous completion control. Use existing native import fixtures, temporary isolated data roots and actual persistence readback. Native input may be synthetic evidence, but must traverse the real presenter/base path. Do not depend on physical hardware to exercise failure/reentry/cleanup.

Expose fixture operations only through role interfaces or explicitly typed native extensions. Record every test's native fixture, baseline/PR commit, expected result and relevant event/resource/native-data assertions. Generic reusable assertions run for DCS, Elite, Squadrons and War Thunder; unsupported native features are asserted disabled with reasons, not silently skipped. A native-only scenario is marked native-only with its required feature, not presented as four-game proof.

## Common context and lifecycle cases (PR 2)

| ID | Arrange / action | Required observations |
| --- | --- | --- |
| C01 | Open, change selection, edit native draft/name, then refresh | Stable native key, monotonic version, one authoritative selection; selection alone clean; native/name edits dirty; no view queried for authoritative state |
| C02 | Replace/reopen same native key, discard/rebuild, attempt old target | Generation changes; old target rejected before apply; no selection carried across unrelated scope |
| C03 | Closed, readonly, testing, operation busy and unsupported actions | Correct blocked precedence and reason; dirty retained under restriction; window/feature/editor/menu see same gates |
| C04 | Admit operation; reenter via feature/window/callback; throw/cancel | One delegate/write; nested request blocked; finally releases gate; retry works; no leaked input lease |
| C05 | Nested DeferRefresh; native + selection changes; callback refresh | One final distinct state notification; no lost update or recursive publish; busy admission/release visible across await |
| C06 | Observe StateChanged/StatusChanged; repeat same text/state; listener throws | No duplicate unchanged events, status does not dirty, subscriber failure isolated; state cleanup and subsequent listeners still run |
| C07 | Scan/Identify/Test conflicts, stop/start, old callback after replacement | Single activity authority; stale completion ignored; startup failure releases resources and lease; old disposal cannot clear new activity |
| C08 | Dispose twice while async work completes; unload/remount cached view | Resources detach exactly once, no post-dispose UI publication, remount reads current state, no duplicate subscriptions |
| C09 | Native fingerprint canonical equivalence vs real change | Equivalent native snapshot does not change result; assignment/label/structural/native parameter changes do; version alone is not the fingerprint |

## Profile and persistence cases (PR 3)

| ID | Arrange / action | Required observations |
| --- | --- | --- |
| P01 | Dirty Save from window, feature and devices/native API | Same inherited profile sequence; one admission and durable write per action; actual readback matches candidate; accepted baseline/revision/name only after write |
| P02 | Clean Save; blocked/readonly/closed Save | Clean writable returns NoChange/no write; blocked returns reason/no success wording; no success attributed to rejected feature save |
| P03 | Candidate invalid, write fails/conflicts, cancellation before commit | Baseline/revision/name/draft/source unchanged; no success event/status; busy released; retry succeeds from current candidate |
| P04 | Commit succeeds then refresh throws; cancellation arrives after commit | Committed result remains Applied, baseline reconciled once, warning explains refresh; no duplicate write or false unsaved state |
| P05 | Dirty Discard, new/never-saved Discard, failed restore | No database/game-file write; atomic restore exact native baseline/name; generation invalidated; restoration failure retains usable old draft |
| P06 | Save/Discard/Cancel leave decision for profile/game switch and close | Cancel/blocked/failure stays current; successful save/discard may navigate; no nested operation lock; cleanup only after successful leave |
| P07 | Import/new/duplicate/export typed commands: cancel, stale review, fail, succeed | Common admission/prepare/review/recheck/accept/finally used; cancellation/rejection preserves source and current session; successful replacement installs once; outputs retain native atomic/backup rules |
| P08 | Preference update fails after successful persistence | Native save still committed; best-effort recent-choice failure explained; no rewrite or lost edits |

## Browser cases (PR 4)

| ID | Arrange / action | Required observations |
| --- | --- | --- |
| B01 | Search/filter/clear with selected command and record | Common refresh preserves surviving stable IDs; removes invalid dependent selection; no row-index identity and no dirty/write |
| B02 | Native duplicate labels, repeated records and ordered declarations | All distinct native addresses retained; source order preserved; exact native filter predicates honored |
| B03 | Reentrant state/projection update and projection failure | One final refresh; last good immutable rows retained, unsafe target cleared, status/retry works; one subscription through remount |

## Editing cases (PR 5)

| ID | Arrange / action | Required observations |
| --- | --- | --- |
| E01 | Assign/clear/native operation by toolbar, editor, drag and callout | Same existing workflow reached through editing base; review/apply once; canonical changed result; native draft changes only, no persistence |
| E02 | Review pumps messages: change scope/draft/access/activity | Post-review scope/fingerprint/gates block apply; native data unchanged; invalid selection cleared |
| E03 | Native Apply says Applied but snapshot unchanged; no-change review | NoChange/no false dirty; exactly one normal completion; native feedback displays real result |
| E04 | Apply/review throws or reenters | Existing workflow semantics preserved: exception and early reentry do not emit Completed; guard released, retry works; no doubled forwarded completion |
| E05 | Action requested after menu opened but before new busy/access state | Per-action supported/ready/gate recalculated at dispatch; unsupported hidden or explained per common policy; no stale enabled bypass |

## Drawing cases (PR 6)

| ID | Arrange / action | Required observations |
| --- | --- | --- |
| D01 | Refresh artwork then change common operation/activity without redrawing | Actions update through base on release; exact native callout/slot/alternative identity preserved |
| D02 | Stale projection, missing drawing, render error, retry | Obsolete installation rejected; last good drawing/safe disabled actions, explanatory text, no guessed link or dirty state |
| D03 | Mount twice, unmount, switch feature, remount, dispose | One event/interaction set; input stopped at surface loss; zoom/focus remain UI-local; no resource or callback leak |

## Controller cases (PR 7)

| ID | Arrange / action | Required observations |
| --- | --- | --- |
| H01 | Scan succeeds/fails/cancels; stale result arrives after replace | Stable IDs/evidence; previous evidence marked stale on failure; no late overwrite; single activity and final release |
| H02 | Select/identify/test/startup failure/stop/close | Native input acquired once, disposed exactly once; no input after stop; shared gates restore; selected dependent targets invalidated |
| H03 | Link/rebind review accepted/cancelled/stale, ambiguous hardware | Correct native link policy; cancelled/stale no mutation; no product/index/name inference; common operation admission/cleanup |
| H04 | Unsupported Test/Identify/Link, including absent physical evidence | Explicit availability reason, no acquisition or mutation; normal browsing/inspection still works |

## Devices and composition cases (PRs 8–9)

| ID | Arrange / action | Required observations |
| --- | --- | --- |
| V01 | Four devices views mount/refresh/selection/change availability | All derive GameDevicesView and consume interfaces; common lifecycle/population/availability sequence; native panels preserved |
| V02 | Resize narrow/normal, scaling, keyboard/focus, cached feature remount | Shared bounds/wrapping preserve usable controls and controller space; no horizontal mangling or view-owned business logic |
| W01 | Four roots compose all roles and route Save/Discard/leave | Contract-typed dependencies and one context/workflow/operation authority; no concrete sibling factories, old direct SaveCore route or orphan shims |
| W02 | Feature unavailable/available again, close cancelled then close accepted | Existing feature cache/edition reevaluation retained; cancelled close keeps subscriptions/input usable; accepted close cleans reverse order |
| W03 | Primary DCS surfaces moved with namespaces/XAML/test locators | Builds/resources/namescopes/automation resolve; persisted IDs, file formats, command keys and package names unchanged |

## Native regression matrix

These supplement all shared cases, not replace them. Assert serialized native bytes/structures and persistence, not only mock call counts or class names.

| Native acceptance ID | Game | Required scenarios |
| --- | --- | --- |
| N-DCS | DCS | Module/UI-layer scope; reserved modifiers; labels included in dirty/fingerprint/save/discard; structural edit atomically rebuilds subscribed draft; edition restriction and repository failure retain edits; pinned UI-layer recovery and validation; Saved Games/integration import; output/recovery; both training lessons remain isolated, restartable and do not touch owner data |
| N-ED | Elite | Primary/secondary/axis slots; combinations and modifiers; invert/deadzone/tuning; exact native product reference vs physical instance; new/duplicate/rename/import; save/store conflict/readback; reviewed preset replacement and backup; reference-sheet invalidation; no game-file overwrite on rejected action |
| N-SQ | Squadrons | Logical slot ID/order/name independent physical link; paired axes/reversal; templates/new unassigned profile; normal view remains readable with unverified-hardware explanation rather than raw-record dump; system sources readonly; source/declaration order preserved in reviewed output |
| N-WT | War Thunder | Repeated/nested/unsupported BLK records retain occurrence IDs and parameters; native input ranges/physical evidence; joystick chord plus alternatives on one input; clear choices enabled after save and disabled during save; compound tuning; only separate review copy exported; rejected edit leaves original source unchanged |
| N-UI | All four | Compact widths and scaling used by existing smoke fixtures, plus normal size; bounded drawing/inspector, usable right-side controller area, wrapping shared action bar/header and game/profile selectors; keyboard dropdowns, visible focus, themes/accessibility; preserve exact existing native smoke assertions |

## Prove “change shared behavior once” for every role

Each experiment changes one production base/policy only (and its expected assertions), then runs all four real native conformance fixtures. It must require no per-game sequence edits. Keep the scenario as a reusable contract assertion; the experiment's temporary policy change need not be merged. Source review must identify the single owning method and verify no native override replaces the complete algorithm.

| Role | Representative experiment / all-four result |
| --- | --- |
| Context | Coalesce nested refresh to one final distinct StateChanged: all four contexts publish once while preserving native facts |
| Profiles | Add shared post-write refresh failure handling: all four report committed success with refresh warning and perform one write |
| Browser | Preserve selected stable command across search whitespace normalization: all four use the same trim/preserve sequence without losing native filters |
| Editing | Recheck scope after native review: all four reject a switched target through the existing workflow, with zero Apply calls |
| Drawing | Refresh action menus on busy release without full artwork redraw: all four recover supported actions after save |
| Controllers | Ignore discovery results from an obsolete generation: all four retain current controller/evidence and release old resources |
| Devices view | Detach/remount subscriptions once and preserve keyboard focus during population: all four use the inherited view lifecycle |
| Existing window / assignment view | Route feature/window save through the one profile operation and base feedback: all four reject blocked saves without success wording or a second lock |

## Compile-time, dependency and independent review checks

Architecture tests must assert closed generic inheritance/implemented roles for every native game, using compiled symbols/reflection where appropriate. Source-string checks may supplement namespace/XAML placement but cannot prove inherited behavior.

Design negative compile fixtures that intentionally fail for: omitted required native context/profile/browser hook; DCS snapshot passed to an ED profile/context port; incompatible native row/filter types; shared orchestration depending on concrete sibling instead of interface; interface-only native collaborator that skips its behavior-owning base. The last two require architectural dependency tests in addition to compiler diagnostics: C# allows such dependencies unless the test enforces the boundary. Do not claim the compiler alone prohibits all bypasses. Assert production native bases are the tested base types, and source/dependency audit confirms public orchestration is nonvirtual with real shared sequencing. A namespace/type-name assertion is insufficient.

For every role PR, review a before/after call path and list which old sequence/flags/factories were deleted. Reject forwarding shells around an unchanged duplicate body, hidden service-locator casts, game-ID switches in common bases, a second assignment engine and native data erased into object/dynamic. Review the native adapter's complete hook body to ensure it supplies a native step rather than repeats the base sequence.

## Per-PR delivery and acceptance gate

| PR | Required acceptance evidence before merge |
| --- | --- |
| 1 specification | Pinned source/declaration/XAML index; exact seven roles/bases/hooks/native maps; explicit context ownership and native session/service boundaries; call paths and duplicate sequences; all cases above mapped before implementation; owner architecture review. No production types or migration claim |
| 2 context | C01–C09 all four; existing bases delegate to same context engine/gates; delete independent authoritative state; native typed session integration; no unused interfaces |
| 3 profiles | P01–P08 + C regression all four; every save/discard route uses base; typed native command admission; ED services split; actual failure/retry and readback evidence |
| 4 browser | B01–B03 + E02 selection regression all four; shared algorithm and typed hooks; no raw/native identity loss |
| 5 editing | E01–E05 all four and native assignments in N-DCS/ED/SQ/WT; existing engine reused, no bypass paths |
| 6 drawing | D01–D03 all four; native artwork/menu/tuning regressions, #356 action refresh; bounded controller space |
| 7 controllers | H01–H04 all four; input acquisition/discovery cleanup measured; typed native link/index policies |
| 8 devices | V01–V02 all four; controller/context interfaces only; preserve native panels and user-visible reasons |
| 9 composition | W01–W03 all four; interface dependency audit; remove scheduled shims/locks/concrete factories; DCS ownership transition complete |
| 10 final independent audit | All shared/native cases and common-change experiments; no skipped game; compiled/dependency/source review; no dead transitional code; requirement review separately accepted before closing #357 |

Implementation PRs use TDD: record the failing contract/native case before implementing, then passing reusable conformance. Preserve existing assertions; do not reduce controller-space thresholds or replace a native outcome assertion with a type check. Build App and smoke projects with zero warnings/errors, run applicable unit/architecture/theme/documentation gates, then the full Windows smoke suite for all four games. Cross-compile is not WPF runtime evidence. Record commit, commands, outputs and manual/native UI review. If measured production assemblies change, enforce existing per-assembly 80% line, 80% branch and CRAP <= 50 across the nine measured assemblies. Do not weaken workflow pins/exclusions/thresholds or move logic into WPF to avoid measurement. Skipped CI is not acceptance evidence.

Existing gate entry points (run on the actual PR checkout; implementation PRs also run applicable repository build/coverage scripts):

```powershell
python -m unittest discover -s tests/ci -p "test_*.py"
python scripts/check_theme_resources.py
python scripts/check_documentation.py
.\tests\smoke-tests.ps1 *>&1 | Tee-Object .\<pr>-smoke.txt
```

## PR 1 requirement review record

Prepared against the user's requirement: shared functions live in bases, native inheritors override/inject native steps, consumers depend on explicit contracts, and DCS participates in every role without losing native functionality. The proposed design provides an owner, signatures, failure/event/capability rules, four native mappings and common-change test for each role. No runtime claim follows from this review or from documentation validation.

- [x] Baseline includes merged #356 and is pinned by commit/blob IDs.
- [x] All seven roles and existing composition bases have explicit contracts, sequence ownership, mandatory hooks and four native bindings.
- [x] Native sessions, selection/generation/fingerprint, dirty/access, operations/activities, batching/events/disposal and ED services have one stated owner.
- [x] Save/discard/assignment paths and duplicate sequence removal are mapped; DCS transitions are assigned to PRs 2–9.
- [x] Shared/native/compile-time/dependency acceptance is specified before migrations; common behavior experiments cover every role.
- [x] PR 1 introduces no production contract, marker inheritance or unused interface.
- [x] Owner accepted the PR 1 architecture independently of tests by merging #358 and requesting PR 2. Context API clarifications and remaining Windows acceptance are recorded in [context implementation](workspace-context-implementation.md).

No runtime conformance cases above have been executed by PR 1. Final architecture acceptance and issue completion remain in PR 10; this review gate is deliberately separate from smoke/build success.
