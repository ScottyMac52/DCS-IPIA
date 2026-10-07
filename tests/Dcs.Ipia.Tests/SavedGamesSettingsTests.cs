using System.Text.Json;
using Dcs.Ipia.Application;
using Dcs.Ipia.Infrastructure;
using Xunit;

namespace Dcs.Ipia.Tests
{
    /// <summary>Checks JSON destination defaults alongside existing SQLite appearance preferences.</summary>
    public sealed class SavedGamesSettingsTests
    {
        #region BEGIN Public
        /// <summary>Folder choices survive restarts and theme/drop updates without resetting each other.</summary>
        [Fact]
        public async Task Destination_is_persisted_in_settings_file_and_remains_the_default()
        {
            var root = Path.Combine(Path.GetTempPath(), "ipia-folder-settings-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                var connection = "Data Source=" + Path.Combine(root, "ipia.db") + ";Pooling=False";
                await new SqliteMigrationRunner(connection).MigrateAsync();
                var defaults = Path.Combine(root, "defaults.json");
                var user = Path.Combine(root, "user-settings.json");
                var theme = Path.Combine(root, "theme.txt");
                var initial = Path.Combine(root, "DCS-default");
                File.WriteAllText(defaults, JsonSerializer.Serialize(new { SchemaVersion = 1, SavedGamesDcsFolder = initial }));
                var settings = new SqliteAppSettingsService(connection, defaults, user, theme);
                Assert.Equal(initial, settings.Current.SavedGamesDcsFolder);
                var selected = Path.Combine(root, "DCS-selected");
                Assert.True(settings.TryUpdateSavedGamesDcsFolder(selected));
                Assert.True(settings.TryUpdateTheme(AppearanceTheme.Dark));
                Assert.True(settings.TryUpdateOccupiedDestinationBehavior(OccupiedDestinationBehavior.Reject));
                var reopened = new SqliteAppSettingsService(connection, defaults, user, theme);
                Assert.Equal(selected, reopened.Current.SavedGamesDcsFolder);
                Assert.Equal(AppearanceTheme.Dark, reopened.Current.Theme);
                Assert.Equal(OccupiedDestinationBehavior.Reject, reopened.Current.OccupiedDestinationBehavior);
                using var json = JsonDocument.Parse(File.ReadAllText(user));
                Assert.Equal(selected, json.RootElement.GetProperty("SavedGamesDcsFolder").GetString());
                var fileSettings = new FileAppSettingsService(defaults, user, theme);
                Assert.True(fileSettings.TryUpdateTheme(AppearanceTheme.Dark));
                Assert.True(fileSettings.TryUpdateOccupiedDestinationBehavior(OccupiedDestinationBehavior.Reject));
                Assert.Equal(selected, new FileAppSettingsService(defaults, user, theme).Current.SavedGamesDcsFolder);
                Assert.Throws<ArgumentException>(() => reopened.TryUpdateSavedGamesDcsFolder("relative"));
                Assert.Throws<ArgumentNullException>(() => fileSettings.TryUpdateSavedGamesDcsFolder(null!));
                Assert.True(reopened.TryUpdateSavedGamesDcsFolder(""));
                Assert.Empty(new SqliteAppSettingsService(connection, defaults, user, theme).Current.SavedGamesDcsFolder);
            }
            finally { Directory.Delete(root, true); }
        }

        /// <summary>Invalid folder JSON produces a visible diagnostic and falls back to a valid default.</summary>
        [Fact]
        public async Task Invalid_folder_values_fall_back_with_diagnostics()
        {
            var root = Path.Combine(Path.GetTempPath(), "ipia-folder-invalid-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                var connection = "Data Source=" + Path.Combine(root, "ipia.db") + ";Pooling=False";
                await new SqliteMigrationRunner(connection).MigrateAsync();
                var user = Path.Combine(root, "user.json");
                File.WriteAllText(user, "{\"SchemaVersion\":1,\"SavedGamesDcsFolder\":123}");
                var settings = new SqliteAppSettingsService(connection, Path.Combine(root, "missing.json"), user, Path.Combine(root, "theme.txt"));
                Assert.Empty(settings.Current.SavedGamesDcsFolder);
                Assert.Contains("SavedGamesDcsFolder", settings.LastError!, StringComparison.Ordinal);
                Assert.True(settings.TryUpdateSavedGamesDcsFolder(root));
                Assert.Null(settings.LastError);
            }
            finally { Directory.Delete(root, true); }
        }
        #endregion END Public
    }
}
