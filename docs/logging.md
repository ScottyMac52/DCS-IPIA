# Physical application logs

IPIA starts Serilog in `src/Dcs.Ipia.App/Program.cs` before selecting a user-data profile or opening SQLite. The executable's `appsettings.json` is loaded from `AppContext.BaseDirectory`, independently of the working directory, and is copied into build and publish output.

The `Serilog` section controls levels, enrichment and file output. Initially the default, every production assembly, Microsoft and System are set to `Error`. Errors and fatal failures are retained; warnings and routine activity are suppressed.

Default log files are `%LOCALAPPDATA%/DCS-IPIA/Logs/ipia-YYYYMMDD.log`. The directory is created by the file sink. Logs roll daily and at 10 MiB, with a maximum of 14 files retained. Concurrent instances share the sink safely. Change `Serilog:WriteTo:0:Args:path` to choose another writable location, such as `D:/IPIA-Logs/ipia-.log`. Sink/path/retention changes require restarting IPIA.

For troubleshooting module creation, change `Serilog:MinimumLevel:Override:Dcs.Ipia.App` from `Error` to `Debug`, reproduce the failure, then restore `Error`. Existing level overrides reload while IPIA is running. New override keys require restarting. Keep the JSON valid when editing; logging settings are independent of the UI preference settings in SQLite and the user-data folder.

| Override key | Assembly |
| --- | --- |
| `Dcs.Ipia.App` | WPF presentation and startup |
| `Dcs.Ipia.Application` | Application services |
| `Dcs.Ipia.Dcs` | DCS import and authoring |
| `Dcs.Ipia.Domain` | Domain |
| `Dcs.Ipia.Infrastructure` | Storage and local services |
| `Dcs.Ipia.Packaging` | Package generation |
| `Dcs.Ipia.Rendering` | Rendering |

Application code receives `Microsoft.Extensions.Logging.ILogger<T>` through DI. Its category is prefixed with `typeof(T).Assembly.GetName().Name`, so assembly overrides work even when a type uses a different namespace (the WPF assembly uses `DcsIpia.Presentation`). Both providers use the same logger factory; Serilog is disposed after the WPF application and database services, flushing files at exit. New services should inject `ILogger<T>` and log failures with the exception object and structured context. Register the assembly-qualified typed logger when composing another service provider. Setting a level does not itself add logging statements to a service.

Startup/profile/lock/migration failures, unhandled dispatcher exceptions and new-module inventory/discovery/add/create failures are instrumented. Failed module creation records the module identity, baseline revision, reviewed device filenames and GUIDs, hardware IDs, selected baseline names and staged profile identities. Debug adds discovery counts and individual device-application steps. Full exception text and properties are included in each retained event.

The Windows smoke suite verifies an isolated real file sink, initial filtering, independent assembly overrides, live configuration reload, exception/scope output and provider-disposal flushing.
