# ADR-001: Local SQLite and immutable snapshots

- Status: Accepted
- Date: 2026-09-20

## Context

HEIMDALL needs transactional offline authoring without weakening Git review, deterministic releases, or recovery. A hosted database adds authentication, availability, cost, and operations before a confirmed multi-user requirement exists.

## Decision

SQLite is the authoritative local working datastore. Infrastructure exclusively owns SQL, transactions, migrations, verified backup, restore, and recovery. The application exposes transport-neutral use cases and repository contracts. Presentation never issues SQL; domain rules remain independent of persistence and presentation.

CI and releases consume deterministic, human-reviewable immutable snapshots, never mutable live database state. Integration tests exercise temporary real SQLite databases.

## Consequences

- Offline work has a low operational footprint.
- Schema upgrades require transactional migrations and verified pre-upgrade backups.
- Canonical serialization, stable identifiers, hashing, and round trips are release contracts.
- Each production layer independently maintains 80% line and branch coverage.

## Alternatives and revisit triggers

Git remains the snapshot/review mechanism but is not the interactive datastore. Hosted and hybrid databases are deferred. Supersede this ADR only with evidence that SQLite cannot meet a confirmed requirement such as concurrent authoring or centralized policy. A future hosted API must preserve offline operation, migrations, deterministic snapshots, and application contracts.
