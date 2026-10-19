using System.Windows;
using System.Windows.Controls;
using Dcs.Ipia.Domain;

namespace DcsIpia.Presentation
{
    /// <summary>Requires an explicit choice before removing unsupported hardware from an imported snapshot copy.</summary>
    internal static class SnapshotImportReview
    {
        #region BEGIN Public
        /// <summary>Shows named import/cancel actions, defaulting safely to cancellation including Escape and window close.</summary>
        internal static bool Show(Window owner, IReadOnlyList<DeviceProfile> unsupported, bool hasSupported)
        {
            var dialog = new AppearanceDialog("MessageDialog") { Name = "SnapshotImportReviewDialog", Title = "Review Free Edition snapshot import",
                Owner = owner, Width = 660, MinWidth = 460, SizeToContent = SizeToContent.Height,
                MaxHeight = SystemParameters.WorkArea.Height * 0.85, WindowStartupLocation = WindowStartupLocation.CenterOwner };
            var panel = new DockPanel { Margin = new Thickness(20) };
            var actions = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
            DockPanel.SetDock(actions, Dock.Bottom); panel.Children.Add(actions);
            var cancel = new Button { Name = "CancelSnapshotImportButton", Content = "Cancel import", IsCancel = true, IsDefault = true, Padding = new Thickness(12, 4, 12, 4) };
            cancel.Click += (_, _) => dialog.DialogResult = false;
            var accept = new Button { Name = "ImportSupportedDevicesButton", Content = "Remove unsupported devices and import", IsEnabled = hasSupported,
                Margin = new Thickness(8, 0, 0, 0), Padding = new Thickness(12, 4, 12, 4) };
            accept.Click += (_, _) => dialog.DialogResult = true;
            actions.Children.Add(cancel); actions.Children.Add(accept);
            var content = new StackPanel();
            content.Children.Add(new TextBlock { Text = "This snapshot contains devices unavailable in Free Edition:", TextWrapping = TextWrapping.Wrap, FontWeight = FontWeights.SemiBold });
            content.Children.Add(new ListBox { Name = "UnsupportedSnapshotDevices", ItemsSource = unsupported.Select(p => p.SourceName + " — " + p.DeviceId).ToArray(),
                Margin = new Thickness(0, 12, 0, 12), MaxHeight = 240 });
            content.Children.Add(new TextBlock { TextWrapping = TextWrapping.Wrap, Text = hasSupported
                ? "Import only the supported devices? Unsupported devices and their assignments, modifiers and device kneeboards will be removed from the imported copy. Assignments on supported devices that depend on removed modifiers will also be removed; they will not become Base assignments. Other supported assignments and command catalogs are retained."
                : "No supported controllers would remain, so this snapshot cannot be imported into Free Edition. Cancel and choose a snapshot containing supported Thrustmaster controllers." });
            content.Children.Add(new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 12, 0, 0),
                Text = "The original snapshot file, existing modules and shared defaults remain unchanged. Cancel import saves nothing." });
            panel.Children.Add(new ScrollViewer { Content = content, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
            dialog.Content = panel; dialog.Loaded += (_, _) => cancel.Focus();
            return dialog.ShowDialog() == true;
        }
        #endregion END Public
    }
}
