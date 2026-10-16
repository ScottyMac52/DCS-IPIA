using System.Text.Json;
using Dcs.Ipia.Application;
using Dcs.Ipia.Infrastructure;
using Xunit;

namespace Dcs.Ipia.Tests
{
    /// <summary>Verifies default precedence, user overrides, persistence and legacy-theme migration.</summary>
    public sealed class AppSettingsServiceTests : IDisposable
    {
        private readonly string _folder =
            Path.Combine(Path.GetTempPath(), "dcs-ipia-settings-" + Guid.NewGuid().ToString("N"));
        private string Defaults => Path.Combine(_folder, "appsettings.json");
        private string User => Path.Combine(_folder, "user-settings.json");
        private string Legacy => Path.Combine(_folder, "theme.txt");

        #region BEGIN Public
        /// <summary>Packaged defaults apply when no user preference exists.</summary>
        [Fact]
        public void Loads_versioned_defaults()
        {
            WriteDefaults("Dark", "Reject");
            var service = Open();
            Assert.Equal(AppearanceTheme.Dark, service.Current.Theme);
            Assert.Equal(OccupiedDestinationBehavior.Reject, service.Current.OccupiedDestinationBehavior);
            Assert.Equal(1, service.Current.SchemaVersion);
            Assert.Null(service.LastError);
        }

        /// <summary>Missing defaults still allow the application to start safely.</summary>
        [Fact]
        public void Missing_defaults_fall_back_to_safe_values()
        {
            var service = Open();
            Assert.Equal(AppearanceTheme.Light, service.Current.Theme);
            Assert.Equal(OccupiedDestinationBehavior.Ask, service.Current.OccupiedDestinationBehavior);
        }

        /// <summary>Valid user overrides win over packaged defaults and survive a new service instance.</summary>
        [Fact]
        public void Persists_independent_user_overrides()
        {
            WriteDefaults("Light", "Ask");
            var service = Open();
            Assert.True(service.TryUpdateTheme(AppearanceTheme.Dark));
            Assert.True(service.TryUpdateOccupiedDestinationBehavior(OccupiedDestinationBehavior.ReplaceInDraft));
            var reloaded = Open();
            Assert.Equal(AppearanceTheme.Dark, reloaded.Current.Theme);
            Assert.Equal(OccupiedDestinationBehavior.ReplaceInDraft, reloaded.Current.OccupiedDestinationBehavior);
            Assert.Equal(
                "1", JsonDocument.Parse(File.ReadAllText(User)).RootElement.GetProperty("SchemaVersion").ToString());
        }

        /// <summary>Every built-in skin survives JSON restart and changing it preserves other preferences.</summary>
        [Theory]
        [InlineData(AppearanceTheme.Light)]
        [InlineData(AppearanceTheme.Dark)]
        [InlineData(AppearanceTheme.NavyAmber)]
        [InlineData(AppearanceTheme.CockpitGreen)]
        public void Built_in_skins_round_trip(AppearanceTheme theme)
        {
            WriteDefaults("Light", "Reject");
            var settings = Open();
            Assert.True(settings.TryUpdateTheme(theme));
            Assert.Equal(theme, Open().Current.Theme);
            Assert.Equal(OccupiedDestinationBehavior.Reject, Open().Current.OccupiedDestinationBehavior);
            Assert.True(settings.TryUpdateTheme(AppearanceTheme.Light));
            Assert.Equal(AppearanceTheme.Light, Open().Current.Theme);
        }

        /// <summary>Each bad override falls back independently without losing another valid value.</summary>
        [Fact]
        public void Invalid_theme_keeps_valid_occupied_target_policy()
        {
            WriteDefaults("Light", "Ask");
            File.WriteAllText(User,
                              "{\"SchemaVersion\":1,\"Theme\":\"Blue\",\"OccupiedDestinationBehavior\":\"Reject\"}");
            var service = Open();
            Assert.Equal(AppearanceTheme.Light, service.Current.Theme);
            Assert.Equal(OccupiedDestinationBehavior.Reject, service.Current.OccupiedDestinationBehavior);
            Assert.Contains("Theme", service.LastError);
        }

        /// <summary>An invalid destination value falls back to the packaged default.</summary>
        [Fact]
        public void Invalid_behavior_preserves_valid_theme()
        {
            WriteDefaults("Light", "Ask");
            File.WriteAllText(User,
                              "{\"SchemaVersion\":1,\"Theme\":\"dark\",\"OccupiedDestinationBehavior\":\"Erase\"}");
            var service = Open();
            Assert.Equal(AppearanceTheme.Dark, service.Current.Theme);
            Assert.Equal(OccupiedDestinationBehavior.Ask, service.Current.OccupiedDestinationBehavior);
            Assert.Contains("OccupiedDestinationBehavior", service.LastError);
        }

        /// <summary>Malformed user JSON does not prevent a workspace from opening.</summary>
        [Fact]
        public void Malformed_json_does_not_crash()
        {
            WriteDefaults("Dark", "Reject");
            File.WriteAllText(User, "{broken");
            var service = Open();
            Assert.Equal(AppearanceTheme.Dark, service.Current.Theme);
            Assert.Equal(OccupiedDestinationBehavior.Reject, service.Current.OccupiedDestinationBehavior);
            Assert.Contains("Malformed", service.LastError);
        }

        /// <summary>Unknown user schemas do not override recognized defaults.</summary>
        [Fact]
        public void Future_schema_is_rejected()
        {
            WriteDefaults("Light", "Ask");
            File.WriteAllText(User, "{\"SchemaVersion\":2,\"Theme\":\"Dark\"}");
            var service = Open();
            Assert.Equal(AppearanceTheme.Light, service.Current.Theme);
            Assert.Contains("Unsupported settings schema", service.LastError);
        }

        /// <summary>Missing or invalid packaged settings use safe defaults.</summary>
        [Fact]
        public void Malformed_packaged_defaults_use_safe_values()
        {
            Directory.CreateDirectory(_folder);
            File.WriteAllText(Defaults, "[]");
            var service = Open();
            Assert.Equal(AppearanceTheme.Light, service.Current.Theme);
            Assert.Equal(OccupiedDestinationBehavior.Ask, service.Current.OccupiedDestinationBehavior);
            Assert.NotNull(service.LastError);
        }

        /// <summary>Existing Dark users retain that preference through one-time migration.</summary>
        [Fact]
        public void Migrates_legacy_dark_once()
        {
            WriteDefaults("Light", "Ask");
            File.WriteAllText(Legacy, "dark\n");
            var migrated = Open();
            Assert.Equal(AppearanceTheme.Dark, migrated.Current.Theme);
            Assert.True(File.Exists(User));
            File.WriteAllText(Legacy, "Light");
            Assert.Equal(AppearanceTheme.Dark, Open().Current.Theme);
        }

        /// <summary>An explicit user preference takes precedence over legacy theme.txt.</summary>
        [Fact]
        public void User_theme_wins_over_legacy_theme()
        {
            WriteDefaults("Dark", "Ask");
            File.WriteAllText(User, "{\"SchemaVersion\":1,\"Theme\":\"Light\"}");
            File.WriteAllText(Legacy, "Dark");
            Assert.Equal(AppearanceTheme.Light, Open().Current.Theme);
        }

        /// <summary>Invalid user theme can still recover a valid earlier Dark selection.</summary>
        [Fact]
        public void Invalid_theme_falls_back_to_legacy_dark()
        {
            WriteDefaults("Light", "Ask");
            File.WriteAllText(User,
                              "{\"SchemaVersion\":1,\"Theme\":\"Blue\",\"OccupiedDestinationBehavior\":\"Reject\"}");
            File.WriteAllText(Legacy, "Dark");
            var service = Open();
            Assert.Equal(AppearanceTheme.Dark, service.Current.Theme);
            Assert.Equal(OccupiedDestinationBehavior.Reject, service.Current.OccupiedDestinationBehavior);
        }

        /// <summary>Invalid legacy values are ignored.</summary>
        [Fact]
        public void Invalid_legacy_theme_is_ignored()
        {
            WriteDefaults("Light", "Ask");
            File.WriteAllText(Legacy, "Neon");
            Assert.Equal(AppearanceTheme.Light, Open().Current.Theme);
            Assert.False(File.Exists(User));
        }

        /// <summary>Write failure leaves the in-memory choice usable and explains the failure.</summary>
        [Fact]
        public void Unwritable_user_path_keeps_session_setting()
        {
            WriteDefaults("Light", "Ask");
            var blocked = Path.Combine(_folder, "blocked");
            File.WriteAllText(blocked, "file not directory");
            var service = new FileAppSettingsService(Defaults, Path.Combine(blocked, "settings.json"), Legacy);
            Assert.False(service.TryUpdateTheme(AppearanceTheme.Dark));
            Assert.Equal(AppearanceTheme.Dark, service.Current.Theme);
            Assert.NotNull(service.LastError);
        }

        /// <summary>Unknown enum values cannot enter the persisted settings model.</summary>
        [Fact]
        public void Unknown_policy_and_theme_are_rejected()
        {
            var service = Open();
            Assert.Throws<ArgumentOutOfRangeException>(() => service.TryUpdateTheme((AppearanceTheme)99));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => service.TryUpdateOccupiedDestinationBehavior((OccupiedDestinationBehavior)99));
        }

        /// <summary>Deletes test-only preference files.</summary>
        public void Dispose()
        {
            if (Directory.Exists(_folder))
                Directory.Delete(_folder, true);
            GC.SuppressFinalize(this);
        }
        #endregion END Public

        #region BEGIN private helpers
        private FileAppSettingsService Open()
        {
            Directory.CreateDirectory(_folder);
            return new FileAppSettingsService(Defaults, User, Legacy);
        }
        private void WriteDefaults(string theme, string behavior)
        {
            Directory.CreateDirectory(_folder);
            File.WriteAllText(
                Defaults,
                $"{{\"SchemaVersion\":1,\"Theme\":\"{theme}\",\"OccupiedDestinationBehavior\":\"{behavior}\"}}");
        }
        #endregion END private helpers
    }
}
