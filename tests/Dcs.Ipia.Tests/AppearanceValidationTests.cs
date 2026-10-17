using System.Text.Json;
using Dcs.Ipia.Application;
using Xunit;

namespace Dcs.Ipia.Tests
{
    /// <summary>Checks bounded data-only appearance formats, semantic contrast and adjustment validation.</summary>
    public sealed class AppearanceValidationTests
    {
        #region BEGIN Public
        /// <summary>All settings survive serialization with optional complete palette and a portable image reference.</summary>
        [Fact]
        public void Settings_round_trip_preserves_adjustments()
        {
            var value = new AppearanceSettings(Accent: "#123456", Palette: Palette(), Image: new string('a', 64) + ".png",
                Enabled: true, Opacity: 0.3, Horizontal: 0, Vertical: 1, Tint: 1, PanelOpacity: 0.9, Fit: "Fit");
            var parsed = AppearanceValidation.ParseSettings(JsonSerializer.Serialize(value));
            Assert.Equal(value with { Palette = null }, parsed with { Palette = null });
            Assert.Equal(Palette().OrderBy(p => p.Key), parsed.Palette!.OrderBy(p => p.Key));
            Assert.Equal(19, AppearanceValidation.ColorKeys.Count);
            Assert.Equal(new AppearanceSettings(), AppearanceValidation.ParseSettings("{}"));
        }
        /// <summary>Strict RGB parsing rejects every non-data color spelling.</summary>
        [Theory]
        [InlineData("# 12345")][InlineData("red")][InlineData("#fff")][InlineData("#FF000000")][InlineData("#GGHHII")][InlineData("1234567")][InlineData("")][InlineData(null)]
        public void Color_rejects_unsupported_syntax(string? color) => Assert.Throws<ArgumentException>(() => AppearanceColors.Parse(color!));
        /// <summary>Contrast and composition use relative luminance and opacity endpoints.</summary>
        [Fact]
        public void Contrast_and_blend_have_known_results()
        {
            Assert.Equal(0x123456u, AppearanceColors.Parse("#123456"));
            Assert.Equal(21, AppearanceColors.Contrast(0, 0xFFFFFF), 6);
            Assert.Equal(21, AppearanceColors.Contrast(0xFFFFFF, 0), 6);
            Assert.Equal(1, AppearanceColors.Contrast(0x123456, 0x123456), 6);
            Assert.Equal(0xFFFFFFu, AppearanceColors.Blend(0, 0xFFFFFF, 0));
            Assert.Equal(0u, AppearanceColors.Blend(0, 0xFFFFFF, 1));
            Assert.Equal(0x808080u, AppearanceColors.Blend(0xFFFFFF, 0, 0.5));
            AppearanceValidation.RequireContrast("#000000", "#FFFFFF", "sample");
            Assert.Throws<ArgumentException>(() => AppearanceValidation.RequireContrast("#FFFFFF", "#FFFFFF", "sample"));
        }
        /// <summary>Unsafe paths, non-finite numbers, unsupported versions and out-of-range controls are rejected.</summary>
        [Fact]
        public void Settings_reject_invalid_values()
        {
            var invalid = new[] { new AppearanceSettings(SchemaVersion: 2), new AppearanceSettings(Accent: "url(test)"),
                new AppearanceSettings(Image: "../outside.png"), new AppearanceSettings(Image: "/tmp/test.png"),
                new AppearanceSettings(Image: "https://example.org/image.png"), new AppearanceSettings(Image: "a.png"),
                new AppearanceSettings(Opacity: -0.01), new AppearanceSettings(Opacity: 0.31), new AppearanceSettings(Opacity: double.NaN),
                new AppearanceSettings(Horizontal: -1), new AppearanceSettings(Horizontal: 2), new AppearanceSettings(Vertical: -1),
                new AppearanceSettings(Vertical: 2), new AppearanceSettings(Tint: -1), new AppearanceSettings(Tint: double.PositiveInfinity),
                new AppearanceSettings(PanelOpacity: -0.01), new AppearanceSettings(PanelOpacity: 1.1), new AppearanceSettings(Fit: "Stretch"),
                new AppearanceSettings(Palette: new Dictionary<string, string>()) };
            foreach (var item in invalid) Assert.Throws<ArgumentException>(() => AppearanceValidation.Validate(item));
            Assert.Throws<ArgumentNullException>(() => AppearanceValidation.Validate(null!));
            AppearanceValidation.Validate(new AppearanceSettings(Image: new string('0', 64) + ".png", Opacity: 0));
            AppearanceValidation.Validate(new AppearanceSettings(PanelOpacity: 0));
            AppearanceValidation.Validate(new AppearanceSettings(PanelOpacity: 1));
        }
        /// <summary>Palettes require exact keys and readable text; no external resource interpretation occurs.</summary>
        [Fact]
        public void Palette_contract_enforces_complete_readable_colors()
        {
            var valid = Palette();
            Assert.Equal(valid.OrderBy(p => p.Key), AppearanceValidation.ParsePalette(PaletteJson(valid)).OrderBy(p => p.Key));
            foreach (var key in new[] { "PrimaryTextBrush", "SecondaryTextBrush", "ControlForegroundBrush", "DisabledTextBrush",
                "ErrorTextBrush", "StatusTextBrush", "AccentTextBrush" })
            {
                var invalid = Palette(); invalid[key] = "#FFFFFF";
                Assert.Throws<ArgumentException>(() => AppearanceValidation.ValidatePalette(invalid));
            }
            var badKey = Palette(); badKey.Remove("BorderBrush"); badKey["ExternalResource"] = "#000000";
            Assert.Throws<ArgumentException>(() => AppearanceValidation.ValidatePalette(badKey));
            var invalidColor = Palette(); invalidColor["BorderBrush"] = "{DynamicResource Whatever}";
            Assert.Throws<ArgumentException>(() => AppearanceValidation.ValidatePalette(invalidColor));
        }
        /// <summary>Unknown, duplicate, partial, oversized and non-versioned import formats fail before preview changes.</summary>
        [Theory]
        [InlineData("null")][InlineData("[]")][InlineData("{}")][InlineData("{\"SchemaVersion\":2,\"Colors\":{}}")]
        [InlineData("{\"SchemaVersion\":\"1\",\"Colors\":{}}")][InlineData("{\"SchemaVersion\":1.5,\"Colors\":{}}")]
        [InlineData("{\"SchemaVersion\":1,\"Other\":{}}")][InlineData("{\"Other\":1,\"Colors\":{}}")]
        [InlineData("{\"SchemaVersion\":1,\"Colors\":[]}")][InlineData("{\"SchemaVersion\":1,\"Colors\":{\"WindowBackgroundBrush\":12}}")]
        [InlineData("{\"SchemaVersion\":1,\"SchemaVersion\":1}")][InlineData("{\"SchemaVersion\":1,\"Colors\":{\"a\":1,\"a\":2}}")]
        [InlineData("{\"SchemaVersion\":1,\"Colors\":{},\"Script\":\"test\"}")]
        public void Palette_rejects_invalid_documents(string json) => Assert.ThrowsAny<ArgumentException>(() => AppearanceValidation.ParsePalette(json));
        /// <summary>Malformed JSON, duplicate settings, unrecognized fields and size limits cannot silently change settings.</summary>
        [Fact]
        public void Settings_json_rejects_malformed_duplicate_and_oversized_input()
        {
            Assert.ThrowsAny<JsonException>(() => AppearanceValidation.ParseSettings("not json"));
            Assert.ThrowsAny<JsonException>(() => AppearanceValidation.ParseSettings("{\"ExternalFile\":\"test\"}"));
            Assert.Throws<ArgumentException>(() => AppearanceValidation.ParseSettings("{\"Opacity\":0.1,\"Opacity\":0.2}"));
            Assert.Throws<ArgumentException>(() => AppearanceValidation.ParseSettings("null"));
            Assert.Throws<ArgumentException>(() => AppearanceValidation.ParsePalette(new string(' ', 65537)));
            Assert.Throws<ArgumentException>(() => AppearanceValidation.ParseSettings(new string(' ', 65537)));
            Assert.ThrowsAny<JsonException>(() => AppearanceValidation.ParseSettings(new string('[', 20) + new string(']', 20)));
        }
        #endregion END Public
        #region BEGIN private helpers
        private static Dictionary<string, string> Palette()
        {
            var colors = AppearanceValidation.ColorKeys.ToDictionary(k => k, _ => "#FFFFFF");
            foreach (var key in new[] { "PrimaryTextBrush", "SecondaryTextBrush", "ControlForegroundBrush", "DisabledTextBrush", "ErrorTextBrush", "StatusTextBrush", "AccentTextBrush" }) colors[key] = "#000000";
            return colors;
        }
        private static string PaletteJson(Dictionary<string, string> colors) => JsonSerializer.Serialize(new { SchemaVersion = 1, Colors = colors });
        #endregion END private helpers
    }
}
