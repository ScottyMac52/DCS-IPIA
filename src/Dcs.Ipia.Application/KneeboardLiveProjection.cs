namespace Dcs.Ipia.Application
{
    /// <summary>Emphasizes only the live modifier layer without altering the exported kneeboard artwork.</summary>
    public static class KneeboardLiveProjection
    {
        #region BEGIN Public
        /// <summary>Yellow active-Base outline, visible against a dark or light hardware image.</summary>
        public const string BaseHighlightColor = "#facc15";

        /// <summary>Muted lettering for inactive modifier variants in the live Input Test image.</summary>
        public const string InactiveLabelColor = "#78818e";

        /// <summary>Preserves all authored labels but fades only variants that do not match the active layer.</summary>
        public static KneeboardComposite Focus(KneeboardComposite page,
            IReadOnlyDictionary<AssignmentLayer, string> activeModifiers)
        {
            ArgumentNullException.ThrowIfNull(page);
            ArgumentNullException.ThrowIfNull(activeModifiers);
            var labels = page.Labels.ToDictionary(
                x => x.Key,
                x => (IReadOnlyList<KneeboardLabelVariant>)x.Value.Select(variant =>
                    KneeboardLegendActivity.Matches(variant.Scope, variant.ModifierChord, activeModifiers)
                        ? variant
                        : variant with { Color = InactiveLabelColor }).ToArray(),
                StringComparer.Ordinal);
            return page with { Labels = labels };
        }

        /// <summary>Gets the applicable binding layer's legend color, or null when this control is shifted out.</summary>
        public static string? ActiveColor(KneeboardComposite page, string control,
            IReadOnlyDictionary<AssignmentLayer, string> activeModifiers)
        {
            ArgumentNullException.ThrowIfNull(page);
            ArgumentException.ThrowIfNullOrWhiteSpace(control);
            ArgumentNullException.ThrowIfNull(activeModifiers);
            if (!page.Labels.TryGetValue(control, out var variants) || variants.Count == 0)
                return BaseHighlightColor; // Unassigned physical controls still report input.
            var active = variants.FirstOrDefault(x =>
                KneeboardLegendActivity.Matches(x.Scope, x.ModifierChord, activeModifiers));
            if (active is null) return null;
            return active.Scope == AssignmentLayer.Module && active.ModifierChord.Length == 0
                ? BaseHighlightColor : active.Color;
        }
        #endregion END Public
    }
}
