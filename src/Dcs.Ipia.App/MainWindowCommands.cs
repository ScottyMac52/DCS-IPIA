using System.Windows.Input;

namespace DcsIpia.Presentation
{
    /// <summary>Shared menu and quick-action commands for the main application window.</summary>
    public static class MainWindowCommands
    {
        #region BEGIN Public
        /// <summary>Creates a module.</summary>
        public static RoutedUICommand NewModule { get; } = Create("NewModule");
        /// <summary>Opens the selected module workspace.</summary>
        public static RoutedUICommand OpenModule { get; } = Create("OpenModule");
        /// <summary>Imports module sources.</summary>
        public static RoutedUICommand ImportModule { get; } = Create("ImportModule");
        /// <summary>Restores a portable module snapshot.</summary>
        public static RoutedUICommand RestoreModule { get; } = Create("RestoreModule");
        /// <summary>Deletes a selected saved module.</summary>
        public static RoutedUICommand DeleteModule { get; } = Create("DeleteModule");
        /// <summary>Opens independent shared defaults.</summary>
        public static RoutedUICommand SharedDefaults { get; } = Create("SharedDefaults");
        /// <summary>Opens preferences.</summary>
        public static RoutedUICommand Settings { get; } = Create("Settings");
        /// <summary>Chooses the DCS Saved Games destination.</summary>
        public static RoutedUICommand Destination { get; } = Create("Destination");
        /// <summary>Deploys a saved module.</summary>
        public static RoutedUICommand FlyNow { get; } = Create("FlyNow");
        /// <summary>Clears recorded deployment files.</summary>
        public static RoutedUICommand ClearDeployment { get; } = Create("ClearDeployment");
        /// <summary>Creates a verified user-data backup.</summary>
        public static RoutedUICommand Backup { get; } = Create("Backup");
        /// <summary>Restores a verified user-data backup.</summary>
        public static RoutedUICommand RestoreData { get; } = Create("RestoreData");
        /// <summary>Shows recovery tools and a restored deployment review.</summary>
        public static RoutedUICommand Recovery { get; } = Create("Recovery");
        /// <summary>Shows the current user-data location.</summary>
        public static RoutedUICommand DataLocation { get; } = Create("DataLocation");
        /// <summary>Manages isolated profiles, setup completion and verified reset.</summary>
        public static RoutedUICommand Profiles { get; } = Create("Profiles");
        /// <summary>Discovers native controller capabilities independently of a module.</summary>
        public static RoutedUICommand DiscoverHardware { get; } = Create("DiscoverHardware");
        /// <summary>Reviews saved user-owned physical units.</summary>
        public static RoutedUICommand ManageHardware { get; } = Create("ManageHardware");
        /// <summary>Shows version and workflow help.</summary>
        public static RoutedUICommand About { get; } = Create("About");
        /// <summary>Closes the application.</summary>
        public static RoutedUICommand Exit { get; } = Create("Exit");
        #endregion END Public
        #region BEGIN private helpers
        private static RoutedUICommand Create(string name)
        {
            var gestures = new InputGestureCollection();
            if (name == "NewModule") gestures.Add(new KeyGesture(Key.N, ModifierKeys.Control));
            if (name == "OpenModule") gestures.Add(new KeyGesture(Key.O, ModifierKeys.Control));
            if (name == "Backup") gestures.Add(new KeyGesture(Key.B, ModifierKeys.Control | ModifierKeys.Shift));
            return new(name, name, typeof(MainWindowCommands), gestures);
        }
        #endregion END private helpers
    }
}
