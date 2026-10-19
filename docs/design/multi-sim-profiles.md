# Multi-sim/game profile foundation

PR #285 introduces experimental intermediate profile DTOs in
`Dcs.Ipia.Domain.MultiSim` and backend/catalog contracts in
`Dcs.Ipia.Application.Interfaces`. These types do not add another game to the
application, replace saved DCS workspaces, migrate SQLite, or deploy native files.
Implementation and acceptance are tracked in [issue #286](https://github.com/ScottyMac52/DCS-IPIA/issues/286).

The useful shared boundary is physical controls and artwork plus explicit
bindings. Command catalogs, contexts, file formats, axis conventions and supported
binding modes belong to each versioned backend. Reusing a joystick diagram does
not make two games' command names equivalent or provide an input-remapping engine.

## Decisions required before backend adoption

- Define a versioned document with explicit backend/game, profile and context
  identities; do not rely on an unvalidated `Metadata["sim"]` convention.
- Separate catalog definitions, actual connected instances, logical roles and
  game-specific identifiers. `PhysicalDeviceIdentity.InstanceId` names the native
  instance string; it is not a product GUID or a catalog ID. Existing
  `Domain.PhysicalDevice` describes a catalog definition, so the proposed
  `ISimProfileBackend.ResolveDevice` input needs review before implementation.
- Declare backend capabilities and return diagnostics for unsupported chords,
  button modes and filters. Specify axis units/ranges, finite-value validation
  and custom-curve parameters before promising cross-game equivalence.
- Preserve unknown native settings and source evidence during same-game round
  trips. Object-valued `SimSpecific` properties deserialize JSON primitives,
  arrays and objects as `JsonElement`, not their original boxed CLR types.
  The tests document that behavior; adapters must use a deliberate extension-data
  contract rather than assume casts to `int`, `bool` or an arbitrary class work.
- Treat these mutable DTOs as experimental transfer data. Validation, ownership,
  null handling, migrations and immutable saved-state boundaries still need
  explicit contracts before production persistence.
- Keep native parsing/writing and path discovery in adapters. Plan cancellable
  imports/exports and separate generating reviewed output from deploying it.
  Preserve existing edition, manufacturer, native identity and rollback checks.

## Delivery approach

First map existing DCS semantics into the boundary and prove compatibility with
the real fixture suite. Then deliver one complete non-DCS import/edit/save/export
slice, including preserved native data, device resolution and recorded manual
verification, before adding more games. Validate each target's actual current
format and supported versions; the candidate list is not a support announcement.

Cross-game conversion should offer explicit semantic command mappings and report
unmapped or unsupported inputs. Hardware drawings remain reusable independently
of a game's command catalog and profile revisions.

The foundation tests cover actual nested JSON round trips, command contexts,
extension values and independent reconstructed collections. They do not claim
native format fidelity or in-game behavior. Every concrete adapter requires its
own fixtures, error-path tests, Windows smoke/manual acceptance and the existing
independent 80% line / 80% branch / method CRAP <= 50 gates.
