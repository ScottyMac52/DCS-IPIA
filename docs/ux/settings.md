# Application settings (schema 1)

`src/Dcs.Ipia.App/appsettings.json` is packaged with the application and read as a default at startup. IPIA never modifies this installation file. User choices are saved to `%LOCALAPPDATA%\DCS-IPIA\user-settings.json` by an atomic replace. A missing file uses the packaged defaults; malformed JSON, unsupported schema versions, and unknown values fall back safely and produce a visible settings diagnostic.

| Field | Values | Packaged default |
| --- | --- | --- |
| `SchemaVersion` | `1` | `1` |
| `Theme` | `Light`, `Dark` | `Light` |
| `OccupiedDestinationBehavior` | `Ask`, `ReplaceInDraft`, `Reject` | `Ask` |

Valid per-user values override defaults independently. An existing `%LOCALAPPDATA%\DCS-IPIA\theme.txt` Light/Dark preference is imported once if there is no valid `Theme` in the user JSON. The new JSON then takes precedence on future launches. The legacy file remains untouched. Preferences take effect for the current session even when saving fails, and the Settings window shows the error. `OccupiedDestinationBehavior` becomes active when the draft assignment editor lands in a later PR; it does not change the current read-only import previews.
