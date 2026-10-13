namespace Dcs.Ipia.Application
{
    /// <summary>Separates visual revision differences from stable-marker and native-input reconciliation.</summary>
    public sealed class MappingRevisionReview
    {
        private MappingRevisionReview(IReadOnlyList<string> changes, IReadOnlyList<string> conflicts)
        {
            Changes = changes; Conflicts = conflicts;
        }
        #region BEGIN Public
        /// <summary>Gets reviewable artwork differences without mutating the target.</summary>
        public IReadOnlyList<string> Changes { get; }
        /// <summary>Gets identity changes that require external reconciliation before adoption.</summary>
        public IReadOnlyList<string> Conflicts { get; }
        /// <summary>Compares one physical profile's current drawing with an explicitly selected revision.</summary>
        /// <param name="current">Saved or draft target diagram.</param>
        /// <param name="revision">Verified candidate, including older revisions for rollback.</param>
        /// <returns>Visual differences and actionable blocking identity conflicts.</returns>
        public static MappingRevisionReview Create(UiLayerDiagram current, UserMappingRevision revision)
        {
            DrawioMappingImporter.Validate(revision);
            GenericHardwareLayout.Validate(current);
            var changes = new List<string>();
            var conflicts = new List<string>();
            var keys = current.Controls.Select(x => revision.Definition.Capabilities.DcsKeys
                .FirstOrDefault(key => DcsInputKey.SamePhysicalInput(key, x.InputKey)) ?? x.InputKey).ToHashSet(StringComparer.Ordinal);
            var definitionKeys = revision.Definition.Capabilities.DcsKeys.ToHashSet(StringComparer.Ordinal);
            var firstBuiltInReplacement = current.CustomMapping is null && current.GenericCapabilities is null;
            if (!(firstBuiltInReplacement ? keys.IsSubsetOf(definitionKeys) : keys.SetEquals(definitionKeys)) ||
                current.GenericCapabilities is not null && ControllerCapabilityMapper.Fingerprint(current.GenericCapabilities) != revision.Definition.Id)
            {
                var onlyInCurrent = keys.Except(definitionKeys, StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToArray();
                var onlyInDefinition = definitionKeys.Except(keys, StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToArray();
                var detail = new List<string>();
                if (onlyInCurrent.Length > 0) detail.Add("profile-only: " + string.Join(", ", onlyInCurrent));
                if (onlyInDefinition.Length > 0) detail.Add("definition-only: " + string.Join(", ", onlyInDefinition));
                conflicts.Add("Native controls differ" + (detail.Count == 0 ? " (capability fingerprint changed)" : ": " + string.Join("; ", detail)) +
                    ". The profile's existing controls are compared with the selected hardware definition; reconcile the definition, assignments and modifiers before importing.");
            }
            if (current.CustomMapping is { } previous)
            {
                var before = previous.Controls.ToDictionary(x => x.Control, StringComparer.Ordinal);
                var after = revision.Controls.ToDictionary(x => x.Control, StringComparer.Ordinal);
                CheckMarkers(previous.Controls, revision.Controls, conflicts, allowAdded: false);
                var moved = before.Values.Count(x => after.TryGetValue(x.Control, out var next) &&
                    (x.X != next.X || x.Y != next.Y || x.Width != next.Width || x.Height != next.Height));
                changes.Add(moved + " control boxes moved or resized.");
                changes.Add(previous.Width == revision.Width && previous.Height == revision.Height
                    ? "Canvas size unchanged." : "Canvas changed from " + previous.Width + " × " + previous.Height + " to " + revision.Width + " × " + revision.Height + ".");
                changes.Add(previous.SvgSha256 == revision.SvgSha256 ? "Rendered artwork unchanged." : "Rendered artwork changed (images, text, colors or layout).");
                changes.Add(previous.SourceSha256 == revision.SourceSha256 ? "Editable source unchanged." : "Editable source changed.");
                changes.Add("Pinned " + previous.Title + " → " + revision.Title + ".");
            }
            else
            {
                if (current.GenericCapabilities is null)
                    CheckMarkers(current.Controls, revision.Controls, conflicts, allowAdded: true);
                changes.Add("Replace the current layout with " + revision.Title + ".");
            }
            changes.Add("Assignments, tuning, modifiers, physical profile identities and module-owned labels are preserved.");
            return new(changes, conflicts);
        }
        #endregion END Public
        #region BEGIN private helpers
        private static void CheckMarkers(IReadOnlyList<HardwareGraphicControl> before,
            IReadOnlyList<HardwareGraphicControl> after, List<string> conflicts, bool allowAdded)
        {
            var replacements = after.ToDictionary(x => x.Control, StringComparer.Ordinal);
            foreach (var control in before)
            {
                if (!replacements.TryGetValue(control.Control, out var replacement))
                    conflicts.Add("Removed or renamed marker " + control.Control + ". Restore its stable label ID and inputKey in draw.io; control changes require reconciliation.");
                else if (!DcsInputKey.SamePhysicalInput(control.InputKey, replacement.InputKey) || control.IsAxis != replacement.IsAxis)
                    conflicts.Add("Marker " + control.Control + " moved from " + control.InputKey + " to " + replacement.InputKey + ". Restore the original inputKey or reconcile assignments explicitly.");
            }
            var markers = before.Select(x => x.Control).ToHashSet(StringComparer.Ordinal);
            foreach (var control in after.Where(x => !allowAdded && !markers.Contains(x.Control)))
                conflicts.Add("Added or renamed marker " + control.Control + ". Preserve existing marker IDs or reconcile control identities before adoption.");
        }
        #endregion END private helpers
    }
}
