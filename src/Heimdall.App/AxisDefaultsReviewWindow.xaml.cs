using System.Windows;
using Heimdall.Application;
using Heimdall.Domain;

namespace Heimdall.App
{
    /// <summary>Reviews command-specific DCS axis removal evidence before staging it.</summary>
    public partial class AxisDefaultsReviewWindow : Window
    {
        /// <summary>Displays the destination and every captured default combination.</summary>
        /// <param name="workspace">Destination module.</param>
        /// <param name="device">Selected device and scope.</param>
        /// <param name="removals">Validated cleared-axis evidence.</param>
        public AxisDefaultsReviewWindow(EditorWorkspaceSnapshot workspace, WorkspaceDevice device, IReadOnlyList<DcsBinding> removals)
        {
            InitializeComponent();
            AxisDefaultsHeading.Text = workspace.Configuration.Module.DisplayName + " (" + workspace.Configuration.Module.DcsId + ") · " +
                device.Layer + " · " + device.FileName;
            AxisDefaultsChanges.Text = string.Join("\n", removals.OrderBy(x => x.CommandId, StringComparer.Ordinal)
                .ThenBy(x => x.InputKey, StringComparer.Ordinal).Select(x => x.CommandName + " [" + x.CommandId + "] · " + x.InputKey +
                    " · " + (x.Reformers.Count == 0 ? "Base" : string.Join("+", x.Reformers))));
        }
        #region BEGIN Public
        #endregion END Public
        #region BEGIN private helpers
        private void StageClicked(object sender, RoutedEventArgs e) => DialogResult = true;
        #endregion END private helpers
    }
}
