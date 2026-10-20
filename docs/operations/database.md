# Database operations baseline

SQLite is local working data, not a release input. Schema changes are versioned, transactional, and recorded in `schema_migrations`. Before an upgrade changes user data, create an online backup and verify it with `PRAGMA integrity_check`.

Schema 9 adds transactional profile setup completion and destination alongside reviewed hardware inventory. Schema 10 records the setup offer independently, so dismissing the first offer does not mark setup complete and reruns preserve completion. See [hardware onboarding](../hardware-onboarding.md) for first-run, upgrade, rerun and full-backup behavior.

Release and CI workflows operate from immutable snapshots. The main **Data & recovery** menu now provides verified full-user-data backup, restore, deployment-record review and the user-data location. Module snapshots remain separate: they restore one module without replacing the user profile.

## Backup format and saved-state boundary

Close module/shared-default editors after saving or discarding drafts. Their modal close guards remain in force; the main recovery tools operate on committed state. The app owns an exclusive session lock beside its user-data directory, preventing another HEIMDALL instance from changing that profile while backup or restore runs.

Choose a new `.ipia-backup.zip` filename outside the active data directory. Existing archives are never overwritten. The archive contains an online SQLite backup named `ipia.db`, per-user settings, Fly Now recovery assets and other user files beneath the data directory. Database WAL/SHM sidecars and deployment locks are excluded; the copied database is normalized to a standalone journal mode. Installed binaries, arbitrary files outside the profile, and DCS Saved Games files are not included. Owner activation is stored outside profiles and is not backed up. The Windows-user-wide workspace layout file lies inside Production but outside named test profiles, so only a Production profile backup includes it.

`ipia-backup.json` records format version 1, creating application version, database schema revision, and the length/SHA-256 of every payload file. The archive is re-read and verified before publication. Archives are limited to 10,000 payload files, 2 GiB total uncompressed data and a 2 MiB manifest. Linked paths, traversal, duplicate case-insensitive paths, unmanifested entries and incompatible schema versions are rejected. Checksums detect corruption; they do not establish an archive author's identity.

## Restore and interruption recovery

Restore verifies every entry and SQLite integrity before asking for confirmation. Live services and pooled connections close before replacement; the application must then close and restart. The archive is staged beside the active directory, older supported schemas are migrated in staging, and the active directory is renamed to a unique `.before-restore-<id>` sibling. That previous directory is retained, not deleted. A verified `.ipia-backup.zip` safety archive is also created beside it when the previous database is readable. If the previous database is corrupt/incompatible, its original files are still retained without attempting to repair or migrate them.

A durable `.restore.json` journal records the same-volume directory swap. Startup checks it before opening SQLite: an incomplete swap restores the previous directory; a completed replacement is verified before the journal is removed. Ambiguous or invalid journals block startup with recovery guidance instead of guessing which directory to delete. Failed synchronous swaps roll back to the previous directory; failed replacement evidence can remain in a `.failed` sibling. Keep safety archives and retained directories until the restored profile has been reviewed, then remove unwanted copies manually.

An unreadable database opens the same recovery window without database-backed repositories or settings. It can restore a verified archive over the corrupt profile. Folder access/another-instance lock errors must be resolved before HEIMDALL can own or restore the profile.

## Restored deployment records

Restoring a profile never restores/deletes DCS files. A restored Fly Now journal creates `deployment-review.json`; Fly Now, Clear and automatic journal rollback remain blocked until review. **Review restored deployment…** shows the old record and permits explicitly forgetting it. This only clears local bookkeeping: existing DCS files remain and are no longer managed by Clear. Inspect/clean those files manually before deploying again. The archive and retained previous directory keep the original deployment evidence.

## Manual acceptance exercise

1. Save a module and shared defaults, change appearance and Saved Games destination, then create a full backup outside user data.
2. Change saved data, restore the archive, close HEIMDALL and restart. Confirm modules, defaults, preferences and user files match the backup; confirm retained original data remains available.
3. Try a truncated archive and a modified payload: live data must remain unchanged.
4. With HEIMDALL closed, test a corrupt database in a disposable Windows user profile. Startup must offer restore and reopen the restored module after restart.
5. Restore an archive containing an active deployment record. Fly Now/Clear must remain blocked; reviewing/forgetting the record must not change any Saved Games files.
