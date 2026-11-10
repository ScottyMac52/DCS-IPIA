using Heimdall.Application.Interfaces;

namespace Heimdall.App.Smoke
{
    /// <summary>Typed native setup actions supplied to one reusable four-adapter context conformance runner.</summary>
    internal sealed record WorkspaceContextCase(string Name, IGameWorkspaceContext Context, Action<string> Edit,
        IGameProfileOperations Profiles, Action Replace, Action MakeReadOnly,
        Func<string> NativeSnapshot, Func<int> Writes, Action<Exception?> FailWrites, Func<string?> PersistedName);
}
