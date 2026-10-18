namespace Dcs.Ipia.Application
{
    /// <summary>A complete background override; absence from the target map means inherit.</summary>
    public sealed record AppearanceBackground(string? Image = null, bool Enabled = false, double Opacity = 0.15,
        double Horizontal = 0.5, double Vertical = 0.5, double Tint = 0.2, string Fit = "Fill", double ArtworkOpacity = 1)
    {
        #region BEGIN Public
        /// <summary>Captures only background properties, leaving skin and control fills global.</summary>
        public static AppearanceBackground From(AppearanceSettings value) => new(value.Image, value.Enabled, value.Opacity,
            value.Horizontal, value.Vertical, value.Tint, value.Fit, value.ArtworkOpacity);
        /// <summary>Combines a target background with global palette and control preferences.</summary>
        public AppearanceSettings Apply(AppearanceSettings value) => value with { Image = Image, Enabled = Enabled,
            Opacity = Opacity, Horizontal = Horizontal, Vertical = Vertical, Tint = Tint, Fit = Fit, ArtworkOpacity = ArtworkOpacity };
        #endregion END Public
    }
}
