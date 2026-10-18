namespace Dcs.Ipia.Application
{
    /// <summary>Focuses the live hardware display on applicable module and UI Layer assignments.</summary>
    public static class KneeboardLiveProjection
    {
        #region BEGIN Public
        /// <summary>Medium blue live aircraft-Base outline; the presentation supplies contrasting edges.</summary>
        public const string BaseHighlightColor = KneeboardStateColors.Base;

        /// <summary>Muted lettering for inapplicable variants in the live Input Test image.</summary>
        public const string InactiveLabelColor = "#78818e";

        /// <summary>Emphasizes applicable variants of a single callout; retains legacy single-control behavior.</summary>
        public static KneeboardComposite Focus(KneeboardComposite page,
            IReadOnlyDictionary<AssignmentLayer, string> activeModifiers)
        {
            ArgumentNullException.ThrowIfNull(page);
            ArgumentNullException.ThrowIfNull(activeModifiers);
            var labels = page.Labels.ToDictionary(x => x.Key,
                x => (IReadOnlyList<KneeboardLabelVariant>)x.Value.Select(variant =>
                    KneeboardLegendActivity.Matches(variant.Scope, variant.ModifierChord, activeModifiers)
                        ? variant : variant with { Color = InactiveLabelColor }).ToArray(),
                StringComparer.Ordinal);
            return page with { Labels = labels };
        }

        /// <summary>Fades competing Base/SHIFT positions of one physical button, across both scopes.</summary>
        public static KneeboardComposite Focus(KneeboardComposite page,
            IReadOnlyList<HardwareGraphicControl> controls,
            IReadOnlyDictionary<AssignmentLayer, string> activeModifiers)
        {
            ArgumentNullException.ThrowIfNull(page);
            ArgumentNullException.ThrowIfNull(controls);
            ArgumentNullException.ThrowIfNull(activeModifiers);
            var labels = page.Labels.ToDictionary(x => x.Key, x =>
            {
                if (!controls.Any(control => control.Control == x.Key))
                    return (IReadOnlyList<KneeboardLabelVariant>)x.Value;
                var chosen = Preferred(page, x.Key, controls, activeModifiers);
                return (IReadOnlyList<KneeboardLabelVariant>)x.Value.Select(variant =>
                    chosen is { } selected && selected.Control == x.Key &&
                    variant == selected.Variant ? variant : variant with { Color = InactiveLabelColor }).ToArray();
            }, StringComparer.Ordinal);
            return page with { Labels = labels };
        }

        /// <summary>Resolves the highest-priority applicable layer within one callout.</summary>
        public static string? ActiveColor(KneeboardComposite page, string control,
            IReadOnlyDictionary<AssignmentLayer, string> activeModifiers)
        {
            ArgumentNullException.ThrowIfNull(page);
            ArgumentException.ThrowIfNullOrWhiteSpace(control);
            ArgumentNullException.ThrowIfNull(activeModifiers);
            if (!page.Labels.TryGetValue(control, out var variants) || variants.Count == 0)
                return BaseHighlightColor;
            var chosen = variants.Where(x => KneeboardLegendActivity.Matches(x.Scope, x.ModifierChord, activeModifiers))
                .OrderByDescending(Priority).FirstOrDefault();
            return chosen is null ? null : Color(chosen);
        }

        /// <summary>Resolves exactly one applicable physical callout, preferring UI SHIFT over Module Base.</summary>
        public static string? ActiveColor(KneeboardComposite page, string control,
            IReadOnlyList<HardwareGraphicControl> controls,
            IReadOnlyDictionary<AssignmentLayer, string> activeModifiers)
        {
            ArgumentNullException.ThrowIfNull(page);
            ArgumentException.ThrowIfNullOrWhiteSpace(control);
            ArgumentNullException.ThrowIfNull(controls);
            ArgumentNullException.ThrowIfNull(activeModifiers);
            var selected = Preferred(page, control, controls, activeModifiers);
            if (selected is { } match)
                return match.Control == control ? Color(match.Variant) : null;
            var physical = controls.FirstOrDefault(x => x.Control == control);
            return physical is null || controls.Any(x => DcsInputKey.SamePhysicalInput(
                x.InputKey, physical.InputKey) && page.Labels.ContainsKey(x.Control))
                ? null : BaseHighlightColor;
        }
        #endregion END Public

        #region BEGIN private helpers
        private static (string Control, KneeboardLabelVariant Variant)? Preferred(KneeboardComposite page,
            string control, IReadOnlyList<HardwareGraphicControl> controls,
            IReadOnlyDictionary<AssignmentLayer, string> activeModifiers)
        {
            var physical = controls.FirstOrDefault(x => x.Control == control);
            if (physical is null) return null;
            var matches = controls.Where(x => DcsInputKey.SamePhysicalInput(x.InputKey, physical.InputKey))
                .SelectMany(x => (page.Labels.GetValueOrDefault(x.Control) ?? [])
                    .Select(variant => (Control: x.Control, Variant: variant)))
                .Where(x => KneeboardLegendActivity.Matches(x.Variant.Scope, x.Variant.ModifierChord, activeModifiers))
                .OrderByDescending(x => Priority(x.Variant)).ToArray();
            return matches.Length == 0 ? null : matches[0];
        }

        private static int Priority(KneeboardLabelVariant variant) => variant.ModifierChord.Length > 0
            ? variant.Scope == AssignmentLayer.UiLayer ? 4 : 3
            : variant.Scope == AssignmentLayer.Module ? 2 : 1;

        private static string Color(KneeboardLabelVariant variant) =>
            KneeboardStateColors.Resolve(variant.Scope, variant.ModifierChord, variant.Color);
        #endregion END private helpers
    }
}
