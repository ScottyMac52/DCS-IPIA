using System.Windows;
using System.Windows.Controls;
using Heimdall.Application;

namespace Heimdall.App.Smoke
{
    /// <summary>Captures real feature surfaces for the user guide without modifying application state.</summary>
    internal static class UserGuideCapture
    {
        #region BEGIN Public
        /// <summary>Reuses the opt-in smoke artifact destination for an actual rendered window.</summary>
        internal static void Save(Window window, string name)
        {
            if (!Enabled) return;
            ThemeControlSmoke.Pump(window);
            ThemeControlSmoke.SaveImage(window, "guide-" + name);
        }

        /// <summary>Documents every DCS feature, restores selection and verifies that capture leaves authoring unchanged.</summary>
        internal static void DcsFeatures(ModuleWorkspaceWindow window)
        {
            if (!Enabled) return;
            var before = EditorSnapshotSerializer.Serialize(window._working!);
            var navigation = (ListBox)window.FindName("FeatureNav");
            var selected = (navigation.SelectedItem as ListBoxItem)?.Tag as string ?? "Assignments";
            try
            {
                foreach (var feature in new[] { "Devices", "Modifiers", "Assignments", "InputTest", "Commands", "Pages", "Kneeboards", "Documentation" })
                {
                    WorkspaceUi.Navigate(window, feature);
                    Save(window, "dcs-" + feature.ToLowerInvariant());
                }
            }
            finally { WorkspaceUi.Navigate(window, selected); }
            if (EditorSnapshotSerializer.Serialize(window._working!) != before)
                throw new InvalidOperationException("Guide capture changed the DCS authoring snapshot.");
        }
        #endregion END Public
        #region BEGIN private helpers
        private static bool Enabled => !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("HEIMDALL_MAPPING_PREVIEW")
            ?? Environment.GetEnvironmentVariable("IPIA_MAPPING_PREVIEW"));
        #endregion END private helpers
    }
}
