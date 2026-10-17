using System.Collections.Frozen;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Dcs.Ipia.Application
{
    /// <summary>Validates bounded data-only appearance settings and complete imported palettes.</summary>
    public static partial class AppearanceValidation
    {
        private static readonly JsonSerializerOptions JsonOptions = new() { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, MaxDepth = 8 };
        private static readonly string[] Names = ["WindowBackgroundBrush", "PrimaryTextBrush", "SurfaceBrush", "BorderBrush",
            "SecondaryTextBrush", "SampleControlBrush", "BadgeBackgroundBrush", "AccentTextBrush", "CanvasBorderBrush",
            "CanvasBackgroundBrush", "StatusBackgroundBrush", "StatusTextBrush", "ControlBackgroundBrush", "ControlForegroundBrush",
            "ControlBorderBrush", "SelectedBackgroundBrush", "DisabledTextBrush", "DisabledBackgroundBrush", "ErrorTextBrush"];
        #region BEGIN Public
        /// <summary>Maximum encoded settings/palette file size.</summary>
        public const int MaximumJsonBytes = 65536;
        /// <summary>Gets the allowed semantic color keys without exposing mutable state.</summary>
        public static IReadOnlyList<string> ColorKeys { get; } = Array.AsReadOnly(Names);
        /// <summary>Reads a settings document, rejecting unknown fields and duplicate properties.</summary>
        public static AppearanceSettings ParseSettings(string json)
        {
            CheckJson(json);
            var value = JsonSerializer.Deserialize<AppearanceSettings>(json, JsonOptions) ?? throw new ArgumentException("Appearance settings are empty.");
            Validate(value);
            return value with { Palette = value.Palette?.ToFrozenDictionary(StringComparer.Ordinal) };
        }
        /// <summary>Reads the versioned palette format: SchemaVersion plus a complete Colors object.</summary>
        public static IReadOnlyDictionary<string, string> ParsePalette(string json)
        {
            CheckJson(json);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object || root.EnumerateObject().Count() != 2 ||
                !root.TryGetProperty("SchemaVersion", out var version) || version.ValueKind != JsonValueKind.Number || !version.TryGetInt32(out var number) || number != 1 ||
                !root.TryGetProperty("Colors", out var colors) || colors.ValueKind != JsonValueKind.Object)
                throw new ArgumentException("Palette requires SchemaVersion 1 and a complete Colors object.");
            var palette = colors.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.ValueKind == JsonValueKind.String
                ? p.Value.GetString()! : throw new ArgumentException("Palette colors must be #RRGGBB strings."), StringComparer.Ordinal);
            ValidatePalette(palette);
            return palette.ToFrozenDictionary(StringComparer.Ordinal);
        }
        /// <summary>Validates finite adjustment ranges, a safe image reference and palette contents.</summary>
        public static void Validate(AppearanceSettings value)
        {
            ArgumentNullException.ThrowIfNull(value);
            if (value.SchemaVersion != 1) throw new ArgumentException("Unsupported appearance schema version.");
            if (value.Accent is not null) _ = AppearanceColors.Parse(value.Accent);
            if (value.Palette is not null) ValidatePalette(value.Palette);
            if (value.Image is not null && !ImageName().IsMatch(value.Image)) throw new ArgumentException("Invalid managed image reference.");
            if (!Range(value.Opacity, 0, 0.3) || !Range(value.Horizontal, 0, 1) || !Range(value.Vertical, 0, 1) ||
                !Range(value.Tint, 0, 1) || !Range(value.PanelOpacity, 0, 1) || !Range(value.ControlOpacity, 0, 1) || value.Fit is not ("Fill" or "Fit"))
                throw new ArgumentException("Appearance adjustments are outside the supported readability limits.");
        }
        /// <summary>Validates keys and 4.5:1 text/background pairs for an imported palette.</summary>
        public static void ValidatePalette(IReadOnlyDictionary<string, string> colors)
        {
            if (colors.Count != Names.Length || Names.Any(name => !colors.ContainsKey(name)))
                throw new ArgumentException("A palette must contain exactly the 19 documented semantic color keys.");
            foreach (var color in colors.Values) _ = AppearanceColors.Parse(color);
            foreach (var (foreground, background) in new[] { ("PrimaryTextBrush", "WindowBackgroundBrush"),
                ("SecondaryTextBrush", "WindowBackgroundBrush"), ("PrimaryTextBrush", "SurfaceBrush"), ("SecondaryTextBrush", "SurfaceBrush"), ("ErrorTextBrush", "SurfaceBrush"),
                ("ControlForegroundBrush", "ControlBackgroundBrush"), ("PrimaryTextBrush", "SelectedBackgroundBrush"),
                ("DisabledTextBrush", "DisabledBackgroundBrush"), ("ErrorTextBrush", "WindowBackgroundBrush"),
                ("StatusTextBrush", "StatusBackgroundBrush"), ("AccentTextBrush", "WindowBackgroundBrush"), ("AccentTextBrush", "SurfaceBrush") })
                RequireContrast(colors[foreground], colors[background], foreground + " / " + background);
        }
        /// <summary>Rejects an accent or text color that cannot remain readable on the supplied background.</summary>
        public static void RequireContrast(string foreground, string background, string description)
        {
            if (AppearanceColors.Contrast(AppearanceColors.Parse(foreground), AppearanceColors.Parse(background)) < 4.5)
                throw new ArgumentException(description + " needs at least 4.5:1 contrast. Choose a darker or lighter color.");
        }
        #endregion END Public
        #region BEGIN private helpers
        private static bool Range(double value, double minimum, double maximum) => double.IsFinite(value) && value >= minimum && value <= maximum;
        private static void CheckJson(string json)
        {
            if (System.Text.Encoding.UTF8.GetByteCount(json) > MaximumJsonBytes) throw new ArgumentException("Appearance JSON exceeds 64 KiB.");
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 8 });
            CheckDuplicates(document.RootElement);
        }
        private static void CheckDuplicates(JsonElement element)
        {
            if (element.ValueKind != JsonValueKind.Object) return;
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                if (!keys.Add(property.Name)) throw new ArgumentException("Duplicate appearance property: " + property.Name);
                CheckDuplicates(property.Value);
            }
        }
        [GeneratedRegex("\\A[0-9a-f]{64}\\.png\\z", RegexOptions.CultureInvariant)]
        private static partial Regex ImageName();
        #endregion END private helpers
    }
}
