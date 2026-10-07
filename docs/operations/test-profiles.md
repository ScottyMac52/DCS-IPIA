# Isolated profiles and clean-slate testing

Start IPIA normally to use **Production** at `%LOCALAPPDATA%\DCS-IPIA`. Existing production data and migrations keep their original paths. Normal upgrade, uninstall and reinstall preserve this directory; uninstall removes program files, not user data. Setup preserves the selected Saved Games folder unless you explicitly choose another one.

Use **Data & recovery → Test profiles and reset… → Open named test profile**, or:

```powershell
.\Dcs.Ipia.App.exe --test-profile fresh-install
# Development:
dotnet run --project src/Dcs.Ipia.App -- --test-profile fresh-install
```

Names contain 1–40 ASCII letters, digits, underscores or hyphens, starting with a letter/digit; Windows device names are reserved. Names are normalized to lowercase. Arbitrary directories, roots and traversal are rejected. Named data lives at `%LOCALAPPDATA%\DCS-IPIA-TestProfiles\fresh-install`. A new name creates a fresh database. Reusing a name reopens that profile. The main window displays the name, data location and test indicator. Return with **Open Production**, or close IPIA and launch without arguments. Close other instances of the same profile first; each profile has an exclusive session lease.

Each profile owns its SQLite database, settings and legacy settings import, user hardware/assets, onboarding marker, temporary directory, Fly Now bookkeeping and backup working directories. Immutable packaged defaults/assets can be read by all profiles; production user files are never imported into a new test profile. Process-local TMP/TEMP/TMPDIR point into the selected profile. No database schema change is required.

**Fly Now and Clear are disabled for test profiles**, including at the service boundary. A restored production archive in a test profile cannot recover, clear or deploy production DCS files. Importing Saved Games remains read-only; exporting a package is still available. Restored deployment evidence can be reviewed separately. Test profiles cannot be used for live deployment; return to Production.

**Rerun setup** only clears `onboarding-state.json`; it preserves modules, assignments, settings and artwork. Feature #160 will connect the wizard to `IUserDataProfileService`. Successful setup writes the version-1 completion marker atomically; skip/cancel/failure must not call completion. Full backups include this state and restore it; reset creates an incomplete profile.

**Reset this profile's user data…** is destructive. Save/discard editor drafts and close editor windows first. Choose a new backup outside *all* production/test profile directories, then explicitly confirm the displayed profile name/path. Reset closes database services, creates and verifies a full archive (checksums, schema and integrity), builds a fresh migrated database, and uses the existing journaled replacement/rollback contract. Cancellation, unsafe paths, an existing archive or failed backup leave data unchanged. If reset fails after services close, exit and restart the same profile. The verified archive and original directory remain available for recovery; do not delete them until satisfied.

Reset never accesses Saved Games. A production reset forgets deployment bookkeeping while leaving deployed DCS files in place. Inspect those files manually before another deployment; recover the old profile from the mandatory archive if you need its deployment record. Production and test resets use the same safeguards. There is no implicit reset on uninstall/reinstall.

## Validation

Automated tests cover independent profile selection/reopening, production checksums, assets/temp/setup isolation, verified reset/restore, cancellation, failed backup, unsafe names/paths, linked paths and blocked deployment. Windows smoke checks cover the visible test indicator and disabled deployment commands. The release installer test seeds a real SQLite database, assets and setup state in Production and two test profiles and compares their checksums across upgrade, uninstall and reinstall, while separately verifying settings persistence.

Manual Windows acceptance: back up Production; record database/settings/asset hashes; open two distinct named test profiles; change settings/import modules in one; close and reopen both; confirm independence and visible locations. Rerun setup and confirm saved data remains. Cancel reset, try an unwritable backup, then perform a confirmed reset to a valid backup and restore it. Confirm Production hashes and Saved Games files are unchanged. Run normal upgrade/uninstall/reinstall and reopen Production. Record this desktop evidence before marking the feature's manual acceptance complete.
