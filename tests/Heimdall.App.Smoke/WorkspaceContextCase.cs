using Heimdall.Application.Interfaces;

namespace Heimdall.App.Smoke
{
    /// <summary>Typed native setup actions supplied to one reusable four-adapter context conformance runner.</summary>
    internal sealed record WorkspaceContextCase(string Name, IGameWorkspaceContext Context, Action<string> Edit,
        Func<IWorkspaceOperationLease, Task> Save, Action<IWorkspaceOperationLease> Discard, Action Replace, Action MakeReadOnly);
}
