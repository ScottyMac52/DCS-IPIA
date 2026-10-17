using Dcs.Ipia.Application;
using Dcs.Ipia.Infrastructure;
using Xunit;

namespace Dcs.Ipia.Tests
{
    /// <summary>Verifies atomic appearance commits, managed image integrity and per-profile isolation.</summary>
    public sealed class FileAppearanceStoreTests : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "ipia-appearance-" + Guid.NewGuid().ToString("N"));
        private static readonly byte[] Png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAIAAAACCAYAAABytg0kAAAAFElEQVR4nGPUCKj4z8DAwMDEAAUAHNgB8zfAN28AAAAASUVORK5CYII=");
        #region BEGIN Public
        /// <summary>Accepted images are content-addressed, unlocked, portable and independent of another profile.</summary>
        [Fact]
        public void Save_and_reload_are_isolated_and_portable()
        {
            var first = new FileAppearanceStore(Path.Combine(_root, "one"));
            Assert.Null(first.ReadImage()); Assert.Null(first.LastError);
            Assert.True(first.TrySave(new AppearanceSettings(Enabled: true, Accent: "#123456"), Png));
            var imageName = first.Current.Image;
            Assert.NotNull(imageName); Assert.Null(first.LastError);
            Assert.Equal(Png, first.ReadImage());
            var reopened = new FileAppearanceStore(Path.Combine(_root, "one"));
            Assert.Equal(first.Current, reopened.Current); Assert.Equal(Png, reopened.ReadImage());
            var second = new FileAppearanceStore(Path.Combine(_root, "two"));
            Assert.Equal(new AppearanceSettings(), second.Current);
            Assert.True(reopened.TrySave(reopened.Current with { Opacity = 0.2 }));
            Assert.Equal(imageName, reopened.Current.Image);
            Assert.True(reopened.TrySave(new AppearanceSettings())); Assert.Null(reopened.ReadImage());
            Assert.Equal(new AppearanceSettings(), new FileAppearanceStore(Path.Combine(_root, "one")).Current);
            Assert.False(Directory.Exists(Path.Combine(_root, "two")));
        }
        /// <summary>Invalid data and failed atomic replacement preserve accepted settings and the prior JSON.</summary>
        [Fact]
        public void Failed_save_preserves_the_last_committed_snapshot()
        {
            var store = new FileAppearanceStore(_root);
            Assert.True(store.TrySave(new AppearanceSettings(Opacity: 0.1)));
            var original = store.Current; var path = Path.Combine(_root, "appearance", "settings.json"); var json = File.ReadAllText(path);
            foreach (var invalid in new[] { new AppearanceSettings(Opacity: 1), new AppearanceSettings(Image: new string('a', 64) + ".png") })
            {
                Assert.False(store.TrySave(invalid)); Assert.Equal(original, store.Current); Assert.Equal(json, File.ReadAllText(path));
            }
            Assert.False(store.TrySave(new(), []));
            Assert.False(store.TrySave(new(), new byte[FileAppearanceStore.MaximumImageBytes + 1]));
            Assert.Equal(json, File.ReadAllText(path));
            File.Delete(path); Directory.CreateDirectory(path);
            Assert.False(store.TrySave(new AppearanceSettings(Enabled: true), Png));
            Assert.Equal(original, store.Current); Assert.NotNull(store.LastError);
            Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(path)!, "*.png"));
            Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(path)!, "*.tmp"));
        }
        /// <summary>Unsupported or corrupt saved settings fall back without modifying the original evidence.</summary>
        [Theory]
        [InlineData("{")][InlineData("{\"SchemaVersion\":2}")][InlineData("{\"Image\":\"../outside.png\"}")][InlineData("{\"Opacity\":9}")]
        public void Invalid_settings_fall_back_with_a_diagnostic(string json)
        {
            var folder = Path.Combine(_root, "appearance"); Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "settings.json"), json);
            var store = new FileAppearanceStore(_root);
            Assert.Equal(new AppearanceSettings(), store.Current); Assert.NotNull(store.LastError);
            Assert.Equal(json, File.ReadAllText(Path.Combine(folder, "settings.json")));
        }
        /// <summary>Missing, corrupted and oversized managed images cannot crash startup or escape the profile.</summary>
        [Theory]
        [InlineData("missing")][InlineData("format")][InlineData("checksum")][InlineData("oversized")]
        public void Invalid_image_uses_skin_fallback(string failure)
        {
            var store = new FileAppearanceStore(_root); Assert.True(store.TrySave(new AppearanceSettings(Enabled: true), Png));
            var path = Path.Combine(_root, "appearance", store.Current.Image!);
            if (failure == "missing") File.Delete(path);
            if (failure == "format") File.WriteAllBytes(path, new byte[30]);
            if (failure == "checksum") { var changed = Png.ToArray(); changed[^1] ^= 1; File.WriteAllBytes(path, changed); }
            if (failure == "oversized") { using var file = File.OpenWrite(path); file.SetLength(FileAppearanceStore.MaximumImageBytes + 1L); }
            Assert.Null(store.ReadImage()); Assert.Contains("unavailable", store.LastError);
            if (failure == "checksum") Assert.False(store.TrySave(store.Current, Png));
        }
        /// <summary>Oversized JSON and a blocked profile directory fail predictably without session mutation.</summary>
        [Fact]
        public void Bounded_json_and_blocked_storage_are_diagnosed()
        {
            var folder = Path.Combine(_root, "appearance"); Directory.CreateDirectory(folder);
            var path = Path.Combine(folder, "settings.json"); File.WriteAllText(path, new string(' ', 65537));
            Assert.NotNull(new FileAppearanceStore(_root).LastError);
            File.Delete(path); Directory.Delete(folder); File.WriteAllText(folder, "blocked");
            var blocked = new FileAppearanceStore(_root);
            Assert.False(blocked.TrySave(new AppearanceSettings(Accent: "#123456")));
            Assert.Equal(new AppearanceSettings(), blocked.Current);
            Assert.Throws<ArgumentException>(() => new FileAppearanceStore(" "));
        }
        /// <summary>Removes isolated profile files after each test.</summary>
        public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
        #endregion END Public
    }
}
