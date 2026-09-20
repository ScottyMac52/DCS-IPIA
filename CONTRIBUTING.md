# Contributing

Install the .NET 8 SDK and Git. Use focused branches and pull requests. Write a failing test first, make the smallest change that passes it, and refactor without weakening behavior. Keep code DRY and follow SOLID boundaries: presentation calls application use cases, application depends on domain abstractions, and infrastructure implements those abstractions.

SQLite integration tests use temporary real databases; SQL behavior is not mocked. Never commit local databases, profiles, credentials, or generated consumer output.

```powershell
dotnet restore tests/Dcs.Ipia.Tests/Dcs.Ipia.Tests.csproj
dotnet format style tests/Dcs.Ipia.Tests/Dcs.Ipia.Tests.csproj --verify-no-changes --no-restore --severity warn
dotnet build tests/Dcs.Ipia.Tests/Dcs.Ipia.Tests.csproj -c Release --no-restore
dotnet test tests/Dcs.Ipia.Tests/Dcs.Ipia.Tests.csproj -c Release --no-build
```
