# DCS-IPIA

DCS Input Profile Importer Application is the future data-driven authoring and build boundary for DCS profiles, hardware, UI Layer assignments, documentation, kneeboards, and OvGME packages. Existing DCS-Common and consumer pipelines remain authoritative until each module passes equivalence and receives explicit approval.

Phase 0 establishes independently testable layers, transport-neutral application contracts, a local SQLite persistence foundation, deterministic output primitives, and automated quality gates. It does not migrate DCS-Common behavior or consumer data.

## Build

```powershell
dotnet restore tests/Dcs.Ipia.Tests/Dcs.Ipia.Tests.csproj
dotnet build tests/Dcs.Ipia.Tests/Dcs.Ipia.Tests.csproj --configuration Release --no-restore
dotnet test tests/Dcs.Ipia.Tests/Dcs.Ipia.Tests.csproj --configuration Release --no-build
```

Read [CONTRIBUTING.md](CONTRIBUTING.md), [the architecture](docs/architecture/README.md), [the glossary](docs/architecture/terminology.md), and [ADR-001](docs/adr/0001-local-sqlite-and-immutable-snapshots.md).

CI requires restore, formatting, build, and tests. Every measured production assembly must independently retain at least 80% line and branch coverage.
