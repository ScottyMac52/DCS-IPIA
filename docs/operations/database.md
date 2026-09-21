# Database operations baseline

SQLite is local working data, not a release input. Schema changes are versioned, transactional, and recorded in `schema_migrations`. Before an upgrade changes user data, create an online backup and verify it with `PRAGMA integrity_check`.

Release and CI workflows operate from immutable snapshots. A later phase adds user-facing restore, interrupted-migration recovery, corruption recovery, and rollback exercises before any consumer pipeline is retired.
