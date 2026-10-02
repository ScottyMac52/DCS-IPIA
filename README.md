# DCS-IPIA

DCS Input Profile Importer Application is the future data-driven authoring and build boundary for DCS profiles, hardware, UI Layer assignments, documentation, kneeboards, and OvGME packages. Existing DCS-Common and consumer pipelines remain authoritative until each module passes equivalence and receives explicit approval. The current published installer predates the database-backed assignment editor and simplified navigation; use current `main` to review those changes.

## Visual Studio

Open `DCS-IPIA.sln` in Visual Studio 2026 with the .NET desktop development workload and .NET 10 SDK. The solution includes the WPF application, supporting libraries, and tests. Build the solution and run tests from Test Explorer.

## Module workspace

On Windows, run `dotnet run --project src/Dcs.Ipia.App/Dcs.Ipia.App.csproj -c Release`.

- Choose **New module** to author a module using internal hardware and a pinned shared-default revision, or **Import module sources** to read a committed consumer checkout or Saved Games profiles. Shared assets ship with IPIA; the normal workflow does not require a DCS-Common checkout. Imported repositories remain read-only.
- Select a saved module and **Open module** to reopen SQLite state. Devices, Modifiers, Assignments, Commands and Pages edit one module, including its isolated UI Layer scope. Shared defaults have their own versioned workspace.
- Load command catalogs from IPI JSON or DCS HTML, edit assignments, labels, axis tuning and summary cards, then **Save to database** or **Discard draft**. The Kneeboard tab generates pages from saved state; draft preview shows current staged edits.
- Enter a package version and **Validate saved module**, **Export module snapshot…** or **Build OvGME package…**. **Restore module snapshot…** inserts verified exported state without replacing an existing module identity. See [module authoring](docs/module-authoring.md) and [new-module creation](docs/new-module-creation.md).
- Diagnostics, Sources and read-only Import details expose errors and source evidence.

## Migration and OvGME status

The app validates and generates native aircraft/UI Layer Lua, device and summary PNGs, and a deterministic consumer-layout OvGME ZIP from frozen saved inputs. Portable snapshots preserve editor state and pinned assets across empty-database restore. Every package is independently verified before writing its ZIP and separate checksum.

The [pinned Step 4 capability inventory](docs/migration/dcs-common-capabilities.md) records all remaining Common authored tooling, documentation, consumer scaffolding and Complete-release gaps. Snapshot rebuild success does not establish full published-consumer parity or manual DCS success. [#83](https://github.com/ScottyMac52/DCS-IPIA/issues/83) and [roadmap #9](https://github.com/ScottyMac52/DCS-IPIA/issues/9) retain those acceptance gates. Existing consumers remain authoritative until their measured parity and rollback decisions are approved.

## Build

```powershell
dotnet restore DCS-IPIA.sln
dotnet build DCS-IPIA.sln --configuration Release --no-restore
dotnet test DCS-IPIA.sln --configuration Release --no-build
```

Read [CONTRIBUTING.md](CONTRIBUTING.md), [the architecture](docs/architecture/README.md), [the glossary](docs/architecture/terminology.md), and [ADR-001](docs/adr/0001-local-sqlite-and-immutable-snapshots.md).

CI requires restore, formatting, build, and tests. Every measured production assembly must independently retain at least 80% line and branch coverage.
