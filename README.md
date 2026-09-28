# DCS-IPIA

DCS Input Profile Importer Application is the future data-driven authoring and build boundary for DCS profiles, hardware, UI Layer assignments, documentation, kneeboards, and OvGME packages. Existing DCS-Common and consumer pipelines remain authoritative until each module passes equivalence and receives explicit approval. The current published installer predates the database-backed assignment editor and simplified navigation; use current `main` to review those changes.

## Visual Studio

Open `DCS-IPIA.sln` in Visual Studio 2026 with the .NET desktop development workload and .NET 10 SDK. The solution includes the WPF application, supporting libraries, and tests. Build the solution and run tests from Test Explorer.

## Module workspace

On Windows, run `dotnet run --project src/Dcs.Ipia.App/Dcs.Ipia.App.csproj -c Release`.

- Choose **Import module sources** for a committed module Git checkout plus DCS-Common checkout, or a Saved Games module/joystick folder. Repository imports read committed `HEAD`; source repositories remain unchanged.
- Select an imported module and **Open module** to reopen its saved SQLite workspace. Assignments is the first section; Module controls and Module UI Layer are scopes of the same graphical editor.
- Edit assignments, authored labels, and axis tuning in a draft, then explicitly **Save to database** or discard. The Kneeboard tab shows existing imported PNGs; **Rendered kneeboard…** composites the current selected device's Module and applicable UI Layer labels into a live preview.
- Diagnostics, Sources and read-only Import details expose problems and provenance. The app does not yet validate a full immutable build or generate a release package from its saved edits.

## OvGME work

[#50](https://github.com/ScottyMac52/DCS-IPIA/issues/50) tracks the complete snapshot-to-OvGME path under Phase 1 [#10](https://github.com/ScottyMac52/DCS-IPIA/issues/10). The packaging library assembles a deterministic ZIP from an explicit frozen file set with the consumer's README/VERSION, `Config/Input`, and `KNEEBOARD` layout. This assembler does not itself generate Lua or kneeboard PNGs, export a complete editor snapshot, or publish a release. Those gates remain open until demonstrated against a pinned consumer package.

## Build

```powershell
dotnet restore DCS-IPIA.sln
dotnet build DCS-IPIA.sln --configuration Release --no-restore
dotnet test DCS-IPIA.sln --configuration Release --no-build
```

Read [CONTRIBUTING.md](CONTRIBUTING.md), [the architecture](docs/architecture/README.md), [the glossary](docs/architecture/terminology.md), and [ADR-001](docs/adr/0001-local-sqlite-and-immutable-snapshots.md).

CI requires restore, formatting, build, and tests. Every measured production assembly must independently retain at least 80% line and branch coverage.
