using System.Security.Cryptography;
using System.Text.Json;
using Dcs.Ipia.Application;
using Dcs.Ipia.Infrastructure;
using Xunit;

namespace Dcs.Ipia.Tests
{
    /// <summary>Checks background inheritance, bounded overrides and transactional multi-image persistence.</summary>
    public sealed class ScopedAppearanceTests
    {
        #region BEGIN Public
        /// <summary>View selection wins over its window; explicit disabled overrides stop inheritance.</summary>
        [Fact]
        public void Resolves_target_then_parent_then_default()
        {
            var settings = new AppearanceSettings(Enabled: true, Opacity: 0.1, ControlOpacity: 0.42, Backgrounds: new Dictionary<string, AppearanceBackground>
            {
                ["ModuleWorkspaceWindow"] = new(Enabled: true, Opacity: 0.2, ArtworkOpacity: 0.6),
                ["AssignmentsView"] = new(Enabled: false, ArtworkOpacity: 0)
            });
            Assert.Same(settings, AppearanceTargets.Resolve(settings, "SettingsWindow"));
            var inherited = AppearanceTargets.Resolve(settings, "InputTestView", "ModuleWorkspaceWindow");
            Assert.Equal(0.2, inherited.Opacity); Assert.Equal(0.6, inherited.ArtworkOpacity); Assert.Equal(0.42, inherited.ControlOpacity);
            var own = AppearanceTargets.Resolve(settings, "AssignmentsView", "ModuleWorkspaceWindow");
            Assert.False(own.Enabled); Assert.Equal(0, own.ArtworkOpacity);
            var parsed = AppearanceValidation.ParseSettings(JsonSerializer.Serialize(settings));
            Assert.Equal(settings.Backgrounds!.OrderBy(x => x.Key), parsed.Backgrounds!.OrderBy(x => x.Key));
            Assert.Equal(35, AppearanceTargets.All.Count);
        }
        /// <summary>Invalid target names, null entries, paths, nested duplicates and non-finite adjustments are rejected.</summary>
        [Fact]
        public void Rejects_invalid_overrides_and_preserves_legacy_defaults()
        {
            foreach (var background in new AppearanceBackground?[] { null, new(Image: "../outside.png"), new(ArtworkOpacity: -0.1), new(ArtworkOpacity: 1.1), new(ArtworkOpacity: double.NaN), new(Opacity: 0.4), new(Fit: "Stretch") })
                Assert.Throws<ArgumentException>(() => AppearanceValidation.Validate(new(Backgrounds: new Dictionary<string, AppearanceBackground> { ["MainWindow"] = background! })));
            Assert.Throws<ArgumentException>(() => AppearanceValidation.Validate(new(Backgrounds: new Dictionary<string, AppearanceBackground> { ["unknown"] = new() })));
            Assert.Throws<ArgumentException>(() => AppearanceValidation.ParseSettings("{\"Backgrounds\":{\"MainWindow\":{\"Enabled\":true,\"Enabled\":false}}}"));
            Assert.Throws<JsonException>(() => AppearanceValidation.ParseSettings("{\"Backgrounds\":{\"MainWindow\":{\"Script\":1}}}"));
            Assert.Equal(1, AppearanceValidation.ParseSettings("{}").ArtworkOpacity);
            Assert.Null(AppearanceValidation.ParseSettings("{}").Backgrounds);
            AppearanceValidation.Validate(new(ArtworkOpacity: 0));
        }
        /// <summary>Multiple images survive replacement/restart; failed commits remove only newly staged content.</summary>
        [Fact]
        public void Retains_all_referenced_images_and_rolls_back_failed_batches()
        {
            var root = Path.Combine(Path.GetTempPath(), "ipia-scoped-" + Guid.NewGuid().ToString("N"));
            try
            {
                var first = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAIAAAACCAYAAABytg0kAAAAFElEQVR4nGPUCKj4z8DAwMDEAAUAHNgB8zfAN28AAAAASUVORK5CYII=");
                var second = first.ToArray(); second[^1] ^= 1;
                var a = Convert.ToHexStringLower(SHA256.HashData(first)) + ".png";
                var b = Convert.ToHexStringLower(SHA256.HashData(second)) + ".png";
                var store = new FileAppearanceStore(root);
                var settings = new AppearanceSettings(Image: a, Enabled: true, Backgrounds: new Dictionary<string, AppearanceBackground> { ["MainWindow"] = new(b, true, ArtworkOpacity: 0.5) });
                Assert.True(store.TrySave(settings, images: new Dictionary<string, byte[]> { [a] = first, [b] = second }), store.LastError);
                var reopened = new FileAppearanceStore(root);
                Assert.Equal(first, reopened.ReadImage()); Assert.Equal(second, reopened.ReadImage(b));
                Assert.Equal(0.5, reopened.Current.Backgrounds!["MainWindow"].ArtworkOpacity);
                Assert.True(store.TrySave(settings with { Image = null }));
                Assert.False(File.Exists(Path.Combine(root, "appearance", a))); Assert.Equal(second, store.ReadImage(b));
                var accepted = File.ReadAllText(Path.Combine(root, "appearance", "settings.json"));
                var bad = new string('0', 64) + ".png";
                Assert.False(store.TrySave(settings with { Backgrounds = new Dictionary<string, AppearanceBackground> { ["MainWindow"] = new(bad, true) } }, images: new Dictionary<string, byte[]> { [a] = first, [bad] = second }));
                Assert.False(File.Exists(Path.Combine(root, "appearance", a)));
                Assert.Equal(accepted, File.ReadAllText(Path.Combine(root, "appearance", "settings.json")));
                Assert.Equal(second, store.ReadImage(b));
                Assert.Null(store.ReadImage("../outside.png"));
                Assert.True(store.TrySave(new())); Assert.Empty(Directory.GetFiles(Path.Combine(root, "appearance"), "*.png"));
            }
            finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
        }
        #endregion END Public
    }
}
