# Application settings (schema 1)

`src/Dcs.Ipia.App/appsettings.json` supplies read-only packaged defaults. Theme and assignment-drop preferences are persisted in SQLite, with one-time migration from the earlier user JSON and `theme.txt`. The **Saved Games DCS Folder** is persisted in `%LOCALAPPDATA%\DCS-IPIA\user-settings.json` by atomic replacement and read on every startup; its per-user value overrides the packaged default. Updating appearance or drop preferences preserves the folder choice.

| Field | Values | Packaged default |
| --- | --- | --- |
| `SchemaVersion` | `1` | `1` |
| `Theme` | `Light`, `Dark` | `Light` |
| `OccupiedDestinationBehavior` | `Ask`, `ReplaceInDraft`, `Reject` | `Ask` |
| `SavedGamesDcsFolder` | Absolute DCS Saved Games instance folder, or empty during setup | Empty |

On the Modules screen during setup, use **Browse…** under **Saved Games DCS Folder**. Select the DCS instance folder, for example `C:\Users\pilot\Saved Games\DCS`, rather than its aircraft or joystick subfolder. Settings provides the same browser. Both browsers start with the current selection, which remains the default across restarts. An invalid JSON folder value falls back to the packaged choice and produces a visible diagnostic. Failed persistence keeps the session choice and reports the error.

See [Fly Now and Clear](../fly-now.md) for deployment behavior. Changing this preference does not relocate or clear an active deployment: its original destination remains recorded in SQLite. Clear that deployment before using another destination. Destination choice is separate from payload generation so future module/type-specific folder selection can use the same deployment service.
