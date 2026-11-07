# ADR-0002: Enforced workspace roles and inherited behavior for all four games

- Status: Accepted — owner approved and merged #358; PR 2 API clarifications are documented for review
- Date: 2026-11-07
- Baseline: `0fa0e28b8e50c23d57a3d14b4d47d35b7c1d3db0` (main after PR #356)
- Tracking: [issue #357](https://github.com/ScottyMac52/DCS-IPIA/issues/357), PR 1

## Context

The existing `GameWorkspaceWindow` and `GameAssignmentsView` provide real shared behavior. Their use does not establish contracts for the contexts, browsers, editing, drawing, controller presenters, devices views or profile operations that they compose. ED, Squadrons and War Thunder have independent concrete collaborators with similar members, mutable state and deferred concrete sibling factories. DCS distributes those responsibilities among its session, window facade, feature views and helpers. Folder symmetry is not the missing requirement: enforceable substitutability and inherited common functions are.

This decision is grounded in the pinned [source inventory and call paths](../architecture/workspace-role-migration.md). Earlier inheritance documents describe the existing window/view extraction; they are not evidence that this decision has been implemented. Passing current smoke tests establishes neither these contracts nor the correctness of this architectural choice.

## Decision

Adopt the exact [role specification](../architecture/workspace-role-contracts.md) and [acceptance design](../architecture/workspace-role-acceptance.md). The seven role interfaces have behavior-owning bases, and all four games inherit each applicable base. Existing window and assignment-view bases remain and consume these interfaces. Public orchestration methods are nonvirtual; protected native hooks provide data, review, mutation, persistence and presentation. Generic type arguments and narrow typed extension interfaces retain native snapshot, row, slot, occurrence and request types.

`GameWorkspaceContextBase<TSnapshot>` owns authoritative identity/generation, selection, edit gates, operation/activity admission, refresh batching, state/status notifications and disposal. It implements the existing assignment-host boundary. It owns one instance of the existing assignment workflow and selection; views no longer create competing workspace instances. Native session state stays native: DCS retains its draft/repository/training transaction model; the other games retain `GameProfileSession` and store revisions. One profile base owns save/discard orchestration and calls typed persistence hooks. No common base knows DCS snapshots, Elite slot names, Squadrons indices or War Thunder BLK addresses.

Application owns UI-free contracts and algorithms. App owns WPF bases, view composition, native adapter implementations and dispatcher integration. Backend adapters remain outward dependencies. Shared code depends on role contracts; concrete siblings are constructed only at composition roots. `GameWorkspaceServices.Backend` and `.DeviceReferences` move to a typed Elite service port; they do not become a generic service locator.

Migrate one role across all four games per PR, in issue order. The [migration map](../architecture/workspace-role-migration.md) identifies DCS adapters and exact source transitions. Required native-only panels and features remain; identical file counts are not a requirement. A wrapper that forwards the entire old duplicated sequence, marker inheritance, a second assignment engine or a shared game-ID switch fails acceptance.

PR 1 adds documentation and a source index only. The APIs in the specification are normative proposed signatures, not production declarations. The owner accepted the PR 1 design by merging #358 and requesting PR 2. PR 2 implementation details and narrow API clarifications are recorded in the linked context implementation document. Any later signature/ownership change must update this ADR's linked specification and acceptance mapping in that same PR.

## Consequences

- A common behavior change has one implementation and a reusable all-game conformance fixture; native differences are explicit overrides/typed ports.
- Operation migration replaces existing locks/flags rather than layering another busy flag over them. Persistence success, refresh failure, cancellation and stale review have distinct outcomes.
- DCS needs adapters for every role even where no matching filename exists today. Its larger native editor is not flattened into the other games' session model.
- Transitional facades are allowed only with a removal PR and one underlying authority. Compile-time negative fixtures and a dependency audit supplement runtime behavior tests.
- Runtime, layout and persisted-identity compatibility must be proved in later implementation PRs. This proposed ADR does not certify their completion.

## Alternatives considered

1. Continue extracting concrete helpers: rejected because matching shapes and deferred concrete factories do not enforce contracts or inherited behavior.
2. Interfaces with unchanged duplicated implementations: rejected because the user requires functions in the base with native steps overridden/injected.
3. Force all games into `GameProfileSession`: rejected because DCS owns different native drafts, structural edits, labels and repository transactions.
4. A universal class with per-game flags, `object` payloads or switches: rejected because it erases native types and recreates a monolith.
5. Replace the assignment engine: rejected; extend the existing host, operation, selection and workflow boundaries.

## Evidence and follow-up

- [Contracts, algorithms and failure/event rules](../architecture/workspace-role-contracts.md)
- [Pinned inventory, native mappings and PR transitions](../architecture/workspace-role-migration.md)
- [Reusable contract tests, native acceptance and review gate](../architecture/workspace-role-acceptance.md)
- Implementation sequence: issue #357 PRs 2–10, each with all-four-game acceptance. PR 10 independently audits ownership, dependencies and behavior before claiming architectural completion.
