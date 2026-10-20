using Heimdall.Application;

namespace Heimdall.Dcs
{
    /// <summary>An immutable command-import proposal tied to the reviewed module editor state.</summary>
    /// <param name="BaselineSha256">Reviewed editor state fingerprint.</param>
    /// <param name="Sources">Selected original texts.</param>
    /// <param name="Commands">Parsed aircraft commands.</param>
    /// <param name="Changes">Additions and updates for review.</param>
    /// <param name="Error">Blocking validation failure, or null.</param>
    public sealed record ModuleCommandImportReview(string BaselineSha256, IReadOnlyList<CommandCatalogImportSource> Sources,
        IReadOnlyList<CommandPaletteEntry> Commands, IReadOnlyList<CommandCatalogImportChange> Changes, string? Error);
}
