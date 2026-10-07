namespace Dcs.Ipia.Application
{
    /// <summary>Validated effective application preferences for one user.</summary>
    public sealed class AppSettings
    {
        /// <summary>Creates a versioned immutable settings snapshot.</summary>
        /// <param name="schemaVersion">The settings schema version.</param>
        /// <param name="theme">The appearance palette.</param>
        /// <param name="occupiedDestinationBehavior">The response to an occupied target.</param>
        /// <param name="savedGamesDcsFolder">Current DCS Saved Games destination, or empty during setup.</param>
        public AppSettings(int schemaVersion, AppearanceTheme theme,
                           OccupiedDestinationBehavior occupiedDestinationBehavior, string savedGamesDcsFolder = "")
        {
            SchemaVersion = schemaVersion;
            Theme = theme;
            OccupiedDestinationBehavior = occupiedDestinationBehavior;
            SavedGamesDcsFolder = savedGamesDcsFolder;
        }
        #region BEGIN Public
        /// <summary>Gets the on-disk schema version.</summary>
        public int SchemaVersion { get; }
        /// <summary>Gets the effective appearance palette.</summary>
        public AppearanceTheme Theme { get; }
        /// <summary>Gets the effective occupied-destination behavior.</summary>
        public OccupiedDestinationBehavior OccupiedDestinationBehavior { get; }
        /// <summary>Gets the current default DCS Saved Games instance folder.</summary>
        public string SavedGamesDcsFolder { get; }
        #endregion END Public
    }
}
