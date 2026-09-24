# DCS-IPIA

DCS Input Profile Importer Application is the future data-driven authoring and build boundary for DCS profiles, hardware, UI Layer assignments, documentation, kneeboards, and OvGME packages. Existing DCS-Common and consumer pipelines remain authoritative until each module passes equivalence and receives explicit approval.

Phase 0 establishes independently testable layers, transport-neutral application contracts, a local SQLite persistence foundation, deterministic output primitives, and automated quality gates. It does not migrate DCS-Common behavior or consumer data.

## Visual Studio

Open `DCS-IPIA.sln` in Visual Studio 2026 with the .NET desktop development workload and .NET 10 SDK. The solution includes the WPF application, all six supporting libraries, and the test project. Build the solution and run tests from Test Explorer. Visual Studio 2022 cannot target .NET 10; the project remains on .NET 10.

## Read-only source preview

On Windows, run `dotnet run --project src/Dcs.Ipia.App/Dcs.Ipia.App.csproj -c Release` and choose **Open source (read-only)**.

- For live DCS exports, select `Saved Games/DCS.../Config/Input/UH-1H/joystick` or its containing `UH-1H` directory. The app reads current `diff.lua` profiles and adjacent `modifiers.lua` directly, displays bindings and diagnostics, and leaves device identities unresolved where no shared catalog is supplied. A Saved Games joystick folder has no kneeboard PNG assets.
- For the committed Huey sample, select the root of a local `DCS-UH-1H-Huey` Git checkout and then a local `DCS-Common` Git checkout. Git must be installed. The app reads their committed `HEAD` contents, excluding uncommitted changes. Its Kneeboard tab displays the **existing rendered PNGs** committed in the Huey repository. It does not regenerate them from the imported configuration.

**View imported** reopens the last read-only result. The main workspace layout tabs still show sample design data. Edit, validation, and package building remain unavailable.

## Build

```powershell
dotnet restore DCS-IPIA.sln
dotnet build DCS-IPIA.sln --configuration Release --no-restore
dotnet test DCS-IPIA.sln --configuration Release --no-build
```

Read [CONTRIBUTING.md](CONTRIBUTING.md), [the architecture](docs/architecture/README.md), [the glossary](docs/architecture/terminology.md), and [ADR-001](docs/adr/0001-local-sqlite-and-immutable-snapshots.md).

CI requires restore, formatting, build, and tests. Every measured production assembly must independently retain at least 80% line and branch coverage.
