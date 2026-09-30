using Dcs.Ipia.Application;

namespace Dcs.Ipia.Dcs
{
    /// <summary>Recovers missing UI Layer links from physical profiles already pinned in a workspace.</summary>
    public static class PinnedUiLayerProfileRecovery
    {
        private const string SharedRoot = "assets/shared/ui-layer/input/UiLayer/joystick/";
        #region BEGIN Public
        /// <summary>Restores uniquely matching filenames without changing assignments or reading repositories.</summary>
        /// <param name="workspace">Saved workspace with its original shared source text.</param>
        /// <returns>The workspace with recovered profile links, or the original when no link can be recovered.</returns>
        public static EditorWorkspaceSnapshot Restore(EditorWorkspaceSnapshot workspace)
        {
            ArgumentNullException.ThrowIfNull(workspace);
            if (workspace.Sources is null)
                return workspace;
            var changed = false;
            var diagrams = workspace.Diagrams.Select(diagram =>
            {
                if (!diagram.SupportsUiLayer || diagram.CanonicalUiLayerSourcePath is not null)
                    return diagram;
                var profile = workspace.Configuration.Profiles.SingleOrDefault(x => x.Id == diagram.ProfileId);
                if (profile is null)
                    return diagram;
                var candidates = workspace.Sources.Keys.Where(path =>
                    path.StartsWith(SharedRoot, StringComparison.Ordinal) &&
                    !path.Contains("..", StringComparison.Ordinal) && !path.Contains('\\') &&
                    path[SharedRoot.Length..] == profile.SourceName &&
                    !profile.SourceName.Contains('/') &&
                    profile.SourceName.EndsWith(".diff.lua", StringComparison.Ordinal)).ToArray();
                if (candidates.Length != 1)
                    return diagram;
                changed = true;
                return diagram with { CanonicalUiLayerSourcePath = candidates[0] };
            }).ToArray();
            return changed ? workspace with { Diagrams = diagrams } : workspace;
        }
        #endregion END Public
    }
}
