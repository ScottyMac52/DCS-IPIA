using System.Text.Json;
using Heimdall.Application;
using Heimdall.Application.Interfaces;
using Microsoft.Data.Sqlite;

namespace Heimdall.Infrastructure
{
    /// <summary>Stores effective application preferences in the local SQLite database.</summary>
    public sealed class SqliteAppSettingsService : IAppSettingsService
    {
        private const int SchemaVersion = 1;
        private readonly string _connectionString;
        private readonly FileAppSettingsService _folderSettings;
        /// <summary>Loads preferences from SQLite, migrating existing per-user files once.</summary>
        /// <param name="connectionString">Migrated local SQLite database.</param>
        /// <param name="defaultsPath">Packaged appsettings.json defaults.</param>
        /// <param name="legacyUserPath">Previous per-user JSON settings path.</param>
        /// <param name="legacyThemePath">Previous theme.txt path.</param>
        /// <param name="userSettingsPath">Writable per-user appsettings.json, with migration from legacyUserPath.</param>
        public SqliteAppSettingsService(string connectionString, string defaultsPath, string legacyUserPath,
                                        string legacyThemePath, string? userSettingsPath = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
            _connectionString = connectionString;
            _folderSettings = new FileAppSettingsService(defaultsPath, userSettingsPath ?? legacyUserPath,
                legacyThemePath, false, userSettingsPath is null ? null : legacyUserPath);
            var fallback = new AppSettings(SchemaVersion, AppearanceTheme.Light, OccupiedDestinationBehavior.Ask);
            var defaults = ReadJson(defaultsPath, fallback, out _);
            defaults = new AppSettings(SchemaVersion, defaults.Theme, defaults.OccupiedDestinationBehavior, ReadDestination());
            try
            {
                using var db = new SqliteConnection(_connectionString);
                db.Open();
                using var cmd = db.CreateCommand();
                cmd.CommandText =
                    "SELECT schema_version,theme,occupied_destination_behavior FROM app_preferences WHERE id=1";
                using var reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    if (reader.GetInt32(0) == SchemaVersion &&
                        Enum.TryParse<AppearanceTheme>(reader.GetString(1), true, out var theme) &&
                        Enum.IsDefined(theme) &&
                        Enum.TryParse<OccupiedDestinationBehavior>(reader.GetString(2), true, out var behavior) &&
                        Enum.IsDefined(behavior))
                    {
                        Current = new AppSettings(SchemaVersion, theme, behavior, ReadDestination());
                        LastError = _folderSettings.LastError;
                        return;
                    }
                    LastError = "Invalid stored settings; using packaged defaults.";
                    Current = defaults;
                    return;
                }
            }
            catch (SqliteException ex)
            {
                LastError = "Unable to read local settings: " + ex.Message;
                Current = defaults;
                return;
            }
            var imported = ReadJson(legacyUserPath, defaults, out var hasTheme);
            if (!hasTheme && File.Exists(legacyThemePath))
            {
                try
                {
                    if (Enum.TryParse<AppearanceTheme>(File.ReadAllText(legacyThemePath).Trim(), true, out var theme) &&
                        Enum.IsDefined(theme))
                        imported = new AppSettings(SchemaVersion, theme, imported.OccupiedDestinationBehavior);
                }
                catch (IOException)
                {
                    LastError = "Unable to read legacy theme; using available settings.";
                }
                catch (UnauthorizedAccessException)
                {
                    LastError = "Unable to read legacy theme; using available settings.";
                }
            }
            Current = new AppSettings(SchemaVersion, imported.Theme, imported.OccupiedDestinationBehavior, ReadDestination());
            TryPersist();
            LastError ??= _folderSettings.LastError;
        }
        #region BEGIN Public
        /// <inheritdoc/>
        public AppSettings Current { get; private set; }
        /// <inheritdoc/>
        public string? LastError { get; private set; }
        /// <inheritdoc/>
        public bool TryUpdateTheme(AppearanceTheme theme)
        {
            if (!Enum.IsDefined(theme))
                throw new ArgumentOutOfRangeException(nameof(theme));
            Current = new AppSettings(SchemaVersion, theme, Current.OccupiedDestinationBehavior, Current.SavedGamesDcsFolder);
            return TryPersist();
        }
        /// <inheritdoc/>
        public bool TryUpdateOccupiedDestinationBehavior(OccupiedDestinationBehavior behavior)
        {
            if (!Enum.IsDefined(behavior))
                throw new ArgumentOutOfRangeException(nameof(behavior));
            Current = new AppSettings(SchemaVersion, Current.Theme, behavior, Current.SavedGamesDcsFolder);
            return TryPersist();
        }
        /// <inheritdoc/>
        public bool TryUpdateSavedGamesDcsFolder(string folder)
        {
            var persisted = _folderSettings.TryUpdateSavedGamesDcsFolder(folder);
            if (persisted)
            {
                try
                {
                    using var db = new SqliteConnection(_connectionString);
                    db.Open();
                    using var command = db.CreateCommand();
                    command.CommandText = "UPDATE profile_setup SET destination=$folder WHERE id=1";
                    command.Parameters.AddWithValue("$folder", _folderSettings.Current.SavedGamesDcsFolder);
                    command.ExecuteNonQuery();
                }
                catch (SqliteException ex) { LastError = "Unable to save destination: " + ex.Message; return false; }
            }
            Current = new AppSettings(SchemaVersion, Current.Theme, Current.OccupiedDestinationBehavior,
                ReadDestination());
            LastError = _folderSettings.LastError;
            return persisted;
        }
        /// <summary>Reloads the destination after the atomic setup transaction without rewriting user preferences.</summary>
        public void ReloadDestination() => Current = new AppSettings(SchemaVersion, Current.Theme,
            Current.OccupiedDestinationBehavior, ReadDestination());
        #endregion END Public
        #region BEGIN private helpers
        private string ReadDestination()
        {
            try
            {
                using var db = new SqliteConnection(_connectionString);
                db.Open();
                using var command = db.CreateCommand();
                command.CommandText = "SELECT destination FROM profile_setup WHERE id=1";
                return command.ExecuteScalar() as string ?? _folderSettings.Current.SavedGamesDcsFolder;
            }
            catch (SqliteException) { return _folderSettings.Current.SavedGamesDcsFolder; }
        }
        private bool TryPersist()
        {
            try
            {
                using var db = new SqliteConnection(_connectionString);
                db.Open();
                using var cmd = db.CreateCommand();
                cmd.CommandText =
                    "INSERT INTO app_preferences(id,schema_version,theme,occupied_destination_behavior) VALUES(1,$version,$theme,$behavior) ON CONFLICT(id) DO UPDATE SET schema_version=excluded.schema_version,theme=excluded.theme,occupied_destination_behavior=excluded.occupied_destination_behavior";
                cmd.Parameters.AddWithValue("$version", SchemaVersion);
                cmd.Parameters.AddWithValue("$theme", Current.Theme.ToString());
                cmd.Parameters.AddWithValue("$behavior", Current.OccupiedDestinationBehavior.ToString());
                cmd.ExecuteNonQuery();
                LastError = null;
                return true;
            }
            catch (SqliteException)
            {
                LastError = "Settings changed for this session but could not be saved to SQLite.";
                return false;
            }
        }
        private static AppSettings ReadJson(string path, AppSettings fallback, out bool hasTheme)
        {
            hasTheme = false;
            if (!File.Exists(path))
                return fallback;
            try
            {
                using var document = JsonDocument.Parse(File.ReadAllText(path));
                var root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("SchemaVersion", out var version) ||
                    version.ValueKind != JsonValueKind.Number || !version.TryGetInt32(out var schema) ||
                    schema != SchemaVersion)
                    return fallback;
                var theme = fallback.Theme;
                var behavior = fallback.OccupiedDestinationBehavior;
                if (root.TryGetProperty("Theme", out var t) && t.ValueKind == JsonValueKind.String &&
                    Enum.TryParse<AppearanceTheme>(t.GetString(), true, out var parsedTheme) &&
                    Enum.IsDefined(parsedTheme))
                {
                    theme = parsedTheme;
                    hasTheme = true;
                }
                if (root.TryGetProperty("OccupiedDestinationBehavior", out var b) &&
                    b.ValueKind == JsonValueKind.String &&
                    Enum.TryParse<OccupiedDestinationBehavior>(b.GetString(), true, out var parsedBehavior) &&
                    Enum.IsDefined(parsedBehavior))
                    behavior = parsedBehavior;
                return new AppSettings(SchemaVersion, theme, behavior);
            }
            catch (JsonException)
            {
                return fallback;
            }
            catch (IOException)
            {
                return fallback;
            }
            catch (UnauthorizedAccessException)
            {
                return fallback;
            }
        }
        #endregion END private helpers
    }
}
