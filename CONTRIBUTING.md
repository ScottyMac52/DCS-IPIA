# Contributing

Install the .NET 10 SDK selected by `global.json` and Git. WPF development and UI smoke require Windows with the .NET desktop development workload; structural checks and core tests also run on Linux. See [local tests](tests/README.md) for PowerShell 7, Python, Node and installer prerequisites. Use focused branches and pull requests. Write a failing test first, make the smallest change that passes it, and refactor without weakening behavior. Keep code DRY and follow SOLID boundaries: presentation calls application use cases, application depends on domain abstractions, and infrastructure implements those abstractions.

SQLite integration tests use temporary real databases; SQL behavior is not mocked. Never commit local databases, profiles, credentials, or generated consumer output.

```powershell
dotnet restore tests/Heimdall.Tests/Heimdall.Tests.csproj
dotnet format style tests/Heimdall.Tests/Heimdall.Tests.csproj --verify-no-changes --no-restore --severity warn
dotnet build tests/Heimdall.Tests/Heimdall.Tests.csproj -c Release --no-restore
dotnet test tests/Heimdall.Tests/Heimdall.Tests.csproj -c Release --no-build
```
