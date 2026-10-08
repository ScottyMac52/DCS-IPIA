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
            GenericHardwareLayout.Validate(current);
            if (!current.Controls.Select(x => x.InputKey).ToHashSet(StringComparer.Ordinal)
                .SetEquals(revision.Definition.Capabilities.DcsKeys) ||
                current.GenericCapabilities is not null &&
                ControllerCapabilityMapper.Fingerprint(current.GenericCapabilities) != revision.Definition.Id)
                throw new InvalidOperationException("The mapping definition differs from this profile's verified controls.");
            var labels = current.Controls.ToDictionary(x => x.InputKey, x => x.Label, StringComparer.Ordinal);
            var replacement = current with
            {
                Path = "user-mappings/" + revision.Id + ".svg", Width = revision.Width, Height = revision.Height,
                Background = [], Svg = revision.Svg, CustomMapping = revision,
                Controls = revision.Controls.Select(x => x with { Label = labels[x.InputKey] }).ToArray()
            };
            Validate(replacement);
            return workspace with { Diagrams = workspace.Diagrams.Select(x => x.ProfileId == profileId ? replacement : x).ToArray() };
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
            var actual = diagram.Controls.OrderBy(x => x.Control, StringComparer.Ordinal).ToArray();
            var expected = revision.Controls.OrderBy(x => x.Control, StringComparer.Ordinal).ToArray();
            if (actual.Length != expected.Length || actual.Where((x, i) => (x with { Label = expected[i].Label }) != expected[i]).Any() ||
                actual.Any(x => string.IsNullOrWhiteSpace(x.Label) || x.Label.Length > 200 || x.Label.Any(char.IsControl)))
                throw new InvalidDataException("Custom control geometry/identity differs from its verified mapping.");
        }
        #endregion END Public
    }
}
