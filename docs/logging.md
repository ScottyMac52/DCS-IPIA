# Physical application logs

HEIMDALL starts Serilog in `src/Heimdall.App/Program.cs` before selecting a user-data profile or opening SQLite. The executable's `appsettings.json` is loaded from `AppContext.BaseDirectory`, independently of the working directory, and is copied into build and publish output.

The `Serilog` section controls levels, enrichment and file output. Initially the default, every production assembly, Microsoft and System are set to `Error`. Errors and fatal failures are retained; warnings and routine activity are suppressed.

Default log files are `%LOCALAPPDATA%/HEIMDALL/Logs/heimdall-YYYYMMDD.log`. The directory is created by the file sink. Logs roll daily and at 10 MiB, with a maximum of 14 files retained. Concurrent instances share the sink safely. Change `Serilog:WriteTo:0:Args:path` to choose another writable location, such as `D:/HEIMDALL-Logs/heimdall-.log`. Sink/path/retention changes require restarting HEIMDALL.

For troubleshooting module creation, change `Serilog:MinimumLevel:Override:Heimdall.App` from `Error` to `Debug`, reproduce the failure, then restore `Error`. Existing level overrides reload while HEIMDALL is running. New override keys require restarting. Keep the JSON valid when editing; logging settings are independent of the UI preference settings in SQLite and the user-data folder.

| Override key | Assembly |
| --- | --- |
| `Heimdall.App` | WPF presentation and startup |
| `Heimdall.Application` | Application services |
| `Heimdall.Dcs` | DCS import and authoring |
| `Heimdall.Domain` | Domain |
| `Heimdall.Infrastructure` | Storage and local services |
| `Heimdall.Packaging` | Package generation |
| `Heimdall.Rendering` | Rendering |

Application code receives `Microsoft.Extensions.Logging.ILogger<T>` through DI. Its category is prefixed with `typeof(T).Assembly.GetName().Name`, so assembly overrides work even when a type uses a different namespace (the WPF assembly uses `Heimdall.App`). Both providers use the same logger factory; Serilog is disposed after the WPF application and database services, flushing files at exit. New services should inject `ILogger<T>` and log failures with the exception object and structured context. Register the assembly-qualified typed logger when composing another service provider. Setting a level does not itself add logging statements to a service.

Startup/profile/lock/migration failures, unhandled dispatcher exceptions and new-module inventory/discovery/add/create failures are instrumented. Failed module creation records the module identity, baseline revision, reviewed device filenames and GUIDs, hardware IDs, selected baseline names and staged profile identities. Debug adds discovery counts and individual device-application steps. Full exception text and properties are included in each retained event.

The Windows smoke suite verifies an isolated real file sink, initial filtering, independent assembly overrides, live configuration reload, exception/scope output and provider-disposal flushing.

Saved-module output failures (Validate, snapshot/documentation/Complete export, OvGME build and Fly Now) log an Error event named `ModuleOutputFailure` (1015), including the operation, module identity, package version where available and full exception. Validate/export/build also retain their latest failure in Diagnostics. No Debug override is needed to capture these failures.

Imported bindings are reconciled with saved mapped assignments before output: a saved command replaces conflicting import evidence, a cleared mapped slot stays cleared, and obsolete axes receive explicit removals. Identical binding copies are collapsed; distinct unmapped button commands are preserved. Conflicting saved assignments report the device/profile, input, modifier chord and both command identities so the slot can be corrected.


Log categories follow the HEIMDALL type namespaces (for example `Heimdall.App.App` and `Heimdall.Application.AppSettings`). Legacy profile discovery is independent of the new log directory, so writing logs cannot hide an existing database.
