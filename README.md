# DCS-IPIA

DCS Input Profile Importer Application is the future data-driven authoring and build boundary for DCS profiles, hardware, UI Layer assignments, documentation, kneeboards, and OvGME packages. Existing DCS-Common and consumer pipelines remain authoritative until each module passes equivalence and receives explicit approval.

Phase 0 establishes independently testable layers, transport-neutral application contracts, a local SQLite persistence foundation, deterministic output primitives, and automated quality gates. It does not migrate DCS-Common behavior or consumer data.

## Visual Studio

Open `DCS-IPIA.sln` in Visual Studio 2026 with the .NET desktop development workload and .NET 10 SDK. The solution includes the WPF application, all six supporting libraries, and the test project. Build the solution and run tests from Test Explorer. Visual Studio 2022 cannot target .NET 10; the project remains on .NET 10.

## Read-only Huey preview

On Windows, run `dotnet run --project src/Dcs.Ipia.App/Dcs.Ipia.App.csproj -c Release`. Choose **Open Huey (read-only)**, select the root of your local `DCS-UH-1H-Huey` Git checkout, and then select the root of your local `DCS-Common` Git checkout. Git must be installed. The app reads committed `HEAD` contents from both repositories, so uncommitted edits are not included. It opens a results window with real profiles, aircraft bindings, kneeboard page identities, UI Layer functions, diagnostics, and source hashes. You can reopen the last result with **View imported**.

The original main workspace tabs still illustrate the proposed editing layout with sample data. Import is read-only; the edit, validation, and package build actions remain unavailable while their domain workflows are completed.

## Build

```powershell
dotnet restore DCS-IPIA.sln
dotnet build DCS-IPIA.sln --configuration Release --no-restore
dotnet test DCS-IPIA.sln --configuration Release --no-build
```

Read [CONTRIBUTING.md](CONTRIBUTING.md), [the architecture](docs/architecture/README.md), [the glossary](docs/architecture/terminology.md), and [ADR-001](docs/adr/0001-local-sqlite-and-immutable-snapshots.md).

CI requires restore, formatting, build, and tests. Every measured production assembly must independently retain at least 80% line and branch coverage.
