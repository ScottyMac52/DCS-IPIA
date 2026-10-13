namespace Dcs.Ipia.Application
{
    /// <summary>Pins an explicitly selected mapping in a module draft without changing its physical identities or bindings.</summary>
    public static class UserMappingAdoption
    {
        #region BEGIN Public
        /// <summary>Stages one compatible revision on one physical module profile.</summary>
        /// <param name="workspace">Existing module draft.</param>
        /// <param name="profileId">Explicitly selected physical profile.</param>
        /// <param name="revision">Verified immutable library revision.</param>
        /// <returns>Assignment-preserving draft; caller owns normal save/discard.</returns>
        public static EditorWorkspaceSnapshot Apply(EditorWorkspaceSnapshot workspace, Guid profileId, UserMappingRevision revision)
        {
            DrawioMappingImporter.Validate(revision);
            var current = workspace.Diagrams.Single(x => x.ProfileId == profileId);
            var review = MappingRevisionReview.Create(current, revision);
            if (review.Conflicts.Count > 0) throw new InvalidOperationException(string.Join("\n", review.Conflicts));
            var labels = current.Controls.GroupBy(x => revision.Definition.Capabilities.DcsKeys.FirstOrDefault(key => DcsInputKey.SamePhysicalInput(key, x.InputKey)) ?? x.InputKey, StringComparer.Ordinal)
                    .ToDictionary(x => x.Key, x => x.OrderBy(y => y.Control.EndsWith("-shifted", StringComparison.Ordinal)).First().Label, StringComparer.Ordinal);
            var replacement = current with
            {
                Path = "user-mappings/" + revision.Id + ".svg", Width = revision.Width, Height = revision.Height,
                Background = [], Svg = revision.Svg, CustomMapping = revision,
                MappingHistory = History(current, revision),
                OriginalDrawing = current.OriginalDrawing ?? (current.CustomMapping is null && current.GenericCapabilities is null
                    ? new(current.Path, current.Width, current.Height, current.Background.ToArray(), current.Svg, current.Controls.ToArray()) : null),
                Controls = revision.Controls.Select(x => x with { Label = current.Controls.FirstOrDefault(y => y.Control == x.Control)?.Label ?? labels.GetValueOrDefault(x.InputKey) ?? x.Label }).ToArray()
            };
            Validate(replacement);
            return workspace with { Diagrams = workspace.Diagrams.Select(x => x.ProfileId == profileId ? replacement : x).ToArray() };
        }
        /// <summary>Stages original built-in artwork without changing assignments, identities or user labels.</summary>
        /// <param name="workspace">Existing saved or draft module.</param>
        /// <param name="profileId">Explicit physical profile to restore.</param>
        /// <returns>Draft retaining custom revisions for later re-adoption.</returns>
        public static EditorWorkspaceSnapshot RestoreOriginal(EditorWorkspaceSnapshot workspace, Guid profileId)
        {
            var current = workspace.Diagrams.Single(x => x.ProfileId == profileId);
            Validate(current);
            var original = current.OriginalDrawing ?? throw new InvalidOperationException("This device has no retained original built-in drawing.");
            var labels = current.Controls.ToDictionary(x => x.Control, x => x.Label, StringComparer.Ordinal);
            var restored = current with
            {
                Path = original.Path, Width = original.Width, Height = original.Height,
                Background = original.Background.ToArray(), Svg = original.Svg, CustomMapping = null, OriginalDrawing = null,
                MappingHistory = (current.MappingHistory ?? []).Append(current.CustomMapping!).DistinctBy(x => x.Id).ToArray(),
                Controls = original.Controls.Select(x => x with { Label = labels[x.Control] }).ToArray()
            };
            GenericHardwareLayout.Validate(restored);
            return workspace with { Diagrams = workspace.Diagrams.Select(x => x.ProfileId == profileId ? restored : x).ToArray() };
        }
        /// <summary>Rejects missing, forged or incompatible pinned source/rendering before persistence and export.</summary>
        /// <param name="diagram">Module-owned diagram.</param>
        public static void Validate(UiLayerDiagram diagram)
        {
            var revision = diagram.CustomMapping ?? throw new InvalidDataException("Missing custom mapping evidence.");
            DrawioMappingImporter.Validate(revision);
            if (diagram.Path != "user-mappings/" + revision.Id + ".svg" || diagram.Svg != revision.Svg ||
                diagram.Width != revision.Width || diagram.Height != revision.Height || diagram.Background.Length != 0 ||
                diagram.GenericCapabilities is not null && ControllerCapabilityMapper.Fingerprint(diagram.GenericCapabilities) != revision.Definition.Id)
                throw new InvalidDataException("Custom layout differs from its immutable revision.");
            var history = diagram.MappingHistory ?? [];
            if (history.Select(x => x.Id).Distinct(StringComparer.Ordinal).Count() != history.Count || history.Any(x => x.Id == revision.Id))
                throw new InvalidDataException("Drawing history contains duplicate/current revision identities.");
            foreach (var previous in history) DrawioMappingImporter.Validate(previous);
            if (diagram.OriginalDrawing is { } original)
            {
                if (diagram.GenericCapabilities is not null || original.Path.StartsWith("user-mappings/", StringComparison.Ordinal) ||
                    original.Path.StartsWith("generic/", StringComparison.Ordinal) || string.IsNullOrWhiteSpace(original.Svg) ||
                    !double.IsFinite(original.Width) || !double.IsFinite(original.Height) || original.Width <= 0 || original.Height <= 0 ||
                    original.Controls.Select(x => x.Control).Distinct(StringComparer.Ordinal).Count() != original.Controls.Count)
                    throw new InvalidDataException("Invalid original built-in drawing evidence.");
                var replacementControls = revision.Controls.ToDictionary(x => x.Control, StringComparer.Ordinal);
                if (original.Controls.Any(x => !replacementControls.TryGetValue(x.Control, out var after) || x.IsAxis != after.IsAxis ||
                    !DcsInputKey.SamePhysicalInput(x.InputKey, after.InputKey)))
                    throw new InvalidDataException("Original built-in controls differ from the pinned drawing.");
            }
            var actual = diagram.Controls.OrderBy(x => x.Control, StringComparer.Ordinal).ToArray();
            var expected = revision.Controls.OrderBy(x => x.Control, StringComparer.Ordinal).ToArray();
            if (actual.Length != expected.Length || actual.Where((x, i) => (x with { Label = expected[i].Label }) != expected[i]).Any() ||
                actual.Any(x => string.IsNullOrWhiteSpace(x.Label) || x.Label.Length > 200 || x.Label.Any(char.IsControl)))
                throw new InvalidDataException("Custom control geometry/identity differs from its verified mapping.");
        }
        #endregion END Public
        #region BEGIN private helpers
        private static UserMappingRevision[]? History(UiLayerDiagram current, UserMappingRevision revision)
        {
            var history = (current.MappingHistory ?? []).Concat(current.CustomMapping is { } previous ? [previous] : [])
                .Where(x => x.Id != revision.Id).DistinctBy(x => x.Id).ToArray();
            return history.Length == 0 ? null : history;
        }
        #endregion END private helpers
    }
}
