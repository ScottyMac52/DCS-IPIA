# Issue #323 — PR 1 shared shell and guarded lifecycle extraction

This is a **behavior-preserving first extraction** from `main` (after #324), not a full migration of every control to shared view models.

## Implemented

- `Heimdall.App.Games.GameWorkspaceShell`: a reusable four-region layout for game/profile header, optional configuration/feature header, primary content and status. Both Elite and Squadrons now use the same root host. Simulator-specific controls remain composed within these regions.
- `Heimdall.Application.Games.GameWorkspaceChromeState`: deterministic pending/dirty, Save, Discard, Export and saved-status calculation, including editor-name text and busy handling.
- `Heimdall.Application.Games.GameWorkspaceLeaveGuard`: a WPF-independent Yes/No/Cancel contract. Clean sessions do not prompt; Save delegates to existing transactional persistence; explicit Discard restores the session; cancellation and failed saves keep the current draft.
- Both Elite and Squadrons call these shared services, retaining their game-specific confirmation text and live-input cleanup.
- `GameWorkspaceLifecycleTests`: existing-session status, pending text, busy gates, cancel, failed save, explicit discard and successful save.

## Responsibilities and contracts

| Component | Owns | Does **not** own |
| --- | --- | --- |
| `GameWorkspaceShell` | Four-pane composition and layout row sizing | Game-specific feature trees, native binding, persistence |
| `GameWorkspaceChromeState` | Derived toolbar state | Mutating sessions, dialogs, saving |
| `GameWorkspaceLeaveGuard` | Decision sequencing and cancellation | Prompt UI, file storage, stop/start live input |
| Existing game windows | Native-specific feature and selection UI | Generic leave/dirty logic (migrated) |
| `GameProfileSession` | Draft mutations, saved snapshot, native-adapter validation | WPF, dialogs, view layout |

## Parity and tests

- No native profile schema changes, no migration, no branch reset.
- Do not merge `fix/squadrons-controller-associations` or `feature/war-thunder-adapter-foundation` into this PR.
- Existing DCS module workflow stays unchanged; DCS does not presently use the same saved-game `GameProfileSession` host. Migrating DCS navigation and feature registration is a subsequent, individually tested PR under #323.
- Elite and Squadrons header controls still differ where simulator-specific settings require them; the shared root shell and lifecycle remove the duplicated layout/decision implementations. A later feature registration PR will align the remaining chrome without forcing a giant common XAML form.
- Verify `dotnet build -c Release`, `dotnet test -c Release --no-build`, 80/80/50 gates and Windows smoke for: save/discard, cancel close/profile/game switch, export enabled only when saved, busy-state disabled actions, drawing/feature layout not clipped.
- **Source checks are not substitutes for Windows execution.** No claim of passing the Windows smoke is made by this PR.

## Deferred for PR 2 and PR 3

Typed diagram actions, capability-aware menus, presenter extraction, common command/edit surface, simulator feature registration, controller-service consolidation and per-feature state models. All require characterization and review before removing existing game views.
