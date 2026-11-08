using Heimdall.Application.Interfaces;

namespace Heimdall.App.Smoke
{
    /// <summary>One game's production export command wired for command conformance.</summary>
    /// <param name="Name">The game name.</param>
    /// <param name="Context">The production workspace context.</param>
    /// <param name="Export">Runs the export into a folder.</param>
    /// <param name="Available">Reads the command availability.</param>
    /// <param name="Dirty">Stages an unsaved edit.</param>
    /// <param name="Clean">Discards staged edits.</param>
    /// <param name="Cancelled">Runs a cancelled review, when the game has one.</param>
    internal sealed record ProfileCommandExportCase(string Name, IGameWorkspaceContext Context, Func<string, Task<bool>> Export, Func<bool> Available, Action Dirty, Action Clean, Func<string, Task<bool>>? Cancelled);
}
