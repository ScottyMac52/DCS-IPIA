# Application settings (schema 1)

`src/Dcs.Ipia.App/appsettings.json` supplies read-only packaged defaults. Theme and assignment-drop preferences are persisted in SQLite, with one-time migration from the earlier user JSON and `theme.txt`. The **Saved Games DCS Folder** is persisted in writable `%LOCALAPPDATA%\DCS-IPIA\appsettings.json` by atomic replacement and read on every startup; its per-user value overrides the packaged default. Existing `user-settings.json` preferences are migrated beneath the current file without replacing the current folder choice. Updating the folder preserves unrelated JSON properties. Updating appearance or drop preferences preserves the folder choice.

| Field | Values | Packaged default |
| --- | --- | --- |
| `SchemaVersion` | `1` | `1` |
| `Theme` | `Light`, `Dark` | `Light` |
| `OccupiedDestinationBehavior` | `Ask`, `ReplaceInDraft`, `Reject` | `Ask` |
| `SavedGamesDcsFolder` | Absolute DCS Saved Games instance folder, or empty during setup | Empty |

The Windows **setup wizard** presents a **Saved Games DCS Folder** page with a folder field and **Browse…** button after the application installation directory page, including on upgrades and reruns. It prefills the assigned path from per-user `appsettings.json`, then an earlier `user-settings.json` or installed packaged default when available. With no assigned path, it uses the current user's Windows Saved Games location (including redirection) plus `DCS`. The browser starts at the displayed value. Select the DCS instance folder, for example `C:\Users\pilot\Saved Games\DCS`, rather than its aircraft or joystick subfolder.

Setup persists the confirmed selection to per-user `appsettings.json` before offering to launch IPIA. It reports a persistence failure instead of claiming success. Silent installations use the same current/default value; `/SAVEDGAMES="C:\path\to\DCS-instance"` supplies an explicit choice. Setup does not deploy files to DCS or create the destination folder.

The Modules screen and Settings also provide **Browse…** to change the choice after installation; both start with the current selection and save it to the same per-user `appsettings.json`. An invalid JSON folder value falls back to a valid preference/default and produces a visible diagnostic in the application. Failed application persistence keeps the session choice and reports the error. Portable users can choose the folder in the application without running setup.

See [Fly Now and Clear](../fly-now.md) for deployment behavior. Changing this preference does not relocate or clear an active deployment: its original destination remains recorded in SQLite. Clear that deployment before using another destination. Destination choice is separate from payload generation so future module/type-specific folder selection can use the same deployment service.
