using System.Text.Json;

namespace Dcs.Ipia.Application
{
    /// <summary>Resolves a stored rendered kneeboard page through the module's authored profile mapping.</summary>
    public static class KneeboardPageLocator
    {
        #region BEGIN Public
        /// <summary>Finds the rendered device page for one saved profile, if its PNG exists.</summary>
        /// <param name="snapshot">Saved module sources and kneeboard PNGs.</param>
        /// <param name="profileId">Selected editor device profile.</param>
        /// <returns>The stored PNG for the selected device, or null.</returns>
        public static KneeboardImageAsset? ForProfile(EditorWorkspaceSnapshot snapshot, Guid profileId)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            var profile = snapshot.Configuration.Profiles.FirstOrDefault(x => x.Id == profileId);
            if (profile is null || snapshot.Sources is null ||
                !snapshot.Sources.TryGetValue("config/kneeboard.json", out var source))
                return null;
            using var document = JsonDocument.Parse(source);
            var root = document.RootElement;
            if (!root.TryGetProperty("profiles", out var profiles) || profiles.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("pages", out var pages) || pages.ValueKind != JsonValueKind.Array)
                return null;
            var key = profiles.EnumerateObject()
                          .FirstOrDefault(x => string.Equals(Path.GetFileName(x.Value.GetString()), profile.SourceName,
                                                             StringComparison.Ordinal))
                          .Name;
            if (key is null)
                return null;
            foreach (var page in pages.EnumerateArray())
            {
                if (!page.TryGetProperty("profile", out var selected) || selected.GetString() != key ||
                    !page.TryGetProperty("file", out var file))
                    continue;
                var pageKey = file.GetString();
                return snapshot.Images.FirstOrDefault(x => x.PageKey == pageKey);
            }
            return null;
        }
        #endregion END Public
    }
}
