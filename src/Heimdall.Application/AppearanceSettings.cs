namespace Heimdall.Application
{
    /// <summary>Versioned profile appearance overrides; image names refer only to managed profile content.</summary>
    /// <param name="SchemaVersion">Supported appearance format version.</param>
    /// <param name="Accent">Optional opaque RGB accent.</param>
    /// <param name="Palette">Optional complete semantic palette.</param>
    /// <param name="Image">Content-addressed managed PNG filename.</param>
    /// <param name="Enabled">Whether the decorative background is enabled.</param>
    /// <param name="Opacity">Requested photograph opacity, subject to readability protection.</param>
    /// <param name="Horizontal">Horizontal alignment from left to right.</param>
    /// <param name="Vertical">Vertical alignment from top to bottom.</param>
    /// <param name="Tint">Amount of theme surface blending.</param>
    /// <param name="PanelOpacity">Minimum protected surface opacity.</param>
    /// <param name="Fit">Fill crops proportionally; Fit shows the entire image.</param>
    /// <param name="ControlOpacity">Requested normal control-fill opacity, independent of panel opacity.</param>
    /// <param name="ArtworkOpacity">Preview-only controller artwork opacity; labels and highlights stay opaque.</param>
    /// <param name="Backgrounds">Optional window and view overrides keyed by stable appearance identity.</param>
    public sealed record AppearanceSettings(int SchemaVersion = 1, string? Accent = null,
        IReadOnlyDictionary<string, string>? Palette = null, string? Image = null, bool Enabled = false,
        double Opacity = 0.15, double Horizontal = 0.5, double Vertical = 0.5, double Tint = 0.2,
        double PanelOpacity = 0.35, string Fit = "Fill", double ControlOpacity = 1, double ArtworkOpacity = 1, IReadOnlyDictionary<string, AppearanceBackground>? Backgrounds = null);
}
