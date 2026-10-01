using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Dcs.Ipia.Application
{
    /// <summary>Freezes a verified shared default revision as portable module baseline evidence.</summary>
    public static class SharedUiLayerRevisionReference
    {
        #region BEGIN Public
        /// <summary>Copies saved default sources and verifies their original revision identity.</summary>
        /// <param name="saved">Saved shared revision with its content digest.</param>
        /// <returns>Independent, ordinal baseline sources with verified provenance.</returns>
        public static SharedUiLayerWorkspace Freeze(SharedUiLayerWorkspace saved)
        {
            ArgumentNullException.ThrowIfNull(saved);
            if (saved.Revision <= 0 || saved.SourceCommit.Length != 40 || !saved.SourceCommit.All(Uri.IsHexDigit) ||
                saved.Sources.Count == 0 || saved.Sources.Any(x =>
                    !x.Key.StartsWith("assets/shared/ui-layer/", StringComparison.Ordinal) || x.Key.Contains('\\') ||
                    x.Key.Split('/').Any(part => part is "" or "." or "..") || string.IsNullOrWhiteSpace(x.Value)))
                throw new InvalidDataException("A saved shared default revision with valid source provenance is required.");
            var sources = new SortedDictionary<string, string>(
                saved.Sources.ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal), StringComparer.Ordinal);
            var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(
                saved.SourceCommit + "\n" + JsonSerializer.Serialize(sources))));
            if (hash != saved.ContentSha256)
                throw new InvalidDataException("The shared default baseline failed its content check.");
            return saved with { Sources = sources };
        }
        /// <summary>Records a shared baseline without changing module sources, assignments, labels or overlays.</summary>
        /// <param name="module">Module snapshot to reference the saved baseline.</param>
        /// <param name="saved">Verified saved default revision.</param>
        /// <returns>Module snapshot with portable default revision evidence.</returns>
        public static EditorWorkspaceSnapshot Attach(EditorWorkspaceSnapshot module, SharedUiLayerWorkspace saved)
        {
            ArgumentNullException.ThrowIfNull(module);
            return module with { SharedDefault = Freeze(saved) };
        }
        #endregion END Public
    }
}
