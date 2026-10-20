using System.Windows;
using System.Windows.Controls;
using Heimdall.Application;
using Heimdall.Application.Games;

namespace Heimdall.App.Games
{
    /// <summary>Shows every unresolved native reference and lets the user choose a reviewed repair or cancel export.</summary>
    internal sealed class EliteExportReviewWindow : Window
    {
        internal EliteExportReviewWindow(Window owner, IReadOnlyList<GameExportIssue> issues, IReadOnlyList<DetectedDcsDevice> connected,
            Func<Guid, IReadOnlyList<GameDeviceChoice>> references, bool canReplace)
        {
            Owner = owner; Title = "Resolve ED export references"; Width = 820; Height = 650; MinWidth = 650; MinHeight = 450;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            SetResourceReference(BackgroundProperty, "WindowBackgroundBrush"); SetResourceReference(ForegroundProperty, "PrimaryTextBrush");
            var layout = new DockPanel { Margin = new Thickness(18) };
            var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
            var cancel = new Button { Content = "Cancel export", IsCancel = true };
            var clear = new Button { Content = "Clear affected assignments…", Margin = new Thickness(8, 0, 0, 0) };
            var replace = new Button { Content = "Replace…", Margin = new Thickness(8, 0, 0, 0) };
            actions.Children.Add(cancel); actions.Children.Add(clear); actions.Children.Add(replace); DockPanel.SetDock(actions, Dock.Bottom); layout.Children.Add(actions);
            var selectors = new StackPanel(); DockPanel.SetDock(selectors, Dock.Top); layout.Children.Add(selectors);
            selectors.Children.Add(new TextBlock { Text = "ED cannot resolve these references. Choose one to repair, then review its binding impact. No files have been exported.", TextWrapping = TextWrapping.Wrap });
            var source = new ComboBox { ItemsSource = issues, DisplayMemberPath = "Reference", Margin = new Thickness(0, 10, 0, 8) };
            System.Windows.Automation.AutomationProperties.SetName(source, "Unresolved ED controller"); selectors.Children.Add(source);
            selectors.Children.Add(new TextBlock { Text = "Connected replacement (identical devices require a captured ED index and an explicit link):", TextWrapping = TextWrapping.Wrap });
            var target = new ComboBox { ItemsSource = connected, DisplayMemberPath = "Title", Margin = new Thickness(0, 4, 0, 4) };
            System.Windows.Automation.AutomationProperties.SetName(target, "Replacement physical controller"); selectors.Children.Add(target);
            var reference = new ComboBox { DisplayMemberPath = "Name", Margin = new Thickness(0, 0, 0, 8) };
            System.Windows.Automation.AutomationProperties.SetName(reference, "Captured replacement ED reference"); selectors.Children.Add(reference);
            var details = new TextBox { IsReadOnly = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            System.Windows.Automation.AutomationProperties.SetName(details, "Affected native assignments"); layout.Children.Add(details);
            void Availability()
            {
                var editable = source.SelectedItem is GameExportIssue { CanRepair: true };
                clear.IsEnabled = editable;
                replace.IsEnabled = editable && canReplace && target.SelectedItem is DetectedDcsDevice
                    && (reference.Items.Count < 2 || reference.SelectedItem is GameDeviceChoice);
            }
            source.SelectionChanged += (_, _) =>
            {
                var issue = (GameExportIssue)source.SelectedItem;
                details.Text = issue.Reason + "\n\n" + string.Join("\n", issue.Assignments) +
                    (issue.CanRepair ? "\n\nClear removes entire dependent chords. Replacement requires Owner live-input access and a second impact review. Save reviewed repairs before exporting again."
                        : "\n\nThis reference is inside an unsupported native field. Repair it in ED or the source preset and reimport; it cannot be silently removed.");
                Availability();
            };
            target.SelectionChanged += (_, _) =>
            {
                reference.ItemsSource = target.SelectedItem is DetectedDcsDevice device ? references(device.PhysicalGuid) : [];
                reference.SelectedIndex = reference.Items.Count == 1 ? 0 : -1; Availability();
            };
            reference.SelectionChanged += (_, _) => Availability();
            clear.Click += (_, _) => { Resolution = new(((GameExportIssue)source.SelectedItem).DeviceId, true); DialogResult = true; };
            replace.Click += (_, _) =>
            {
                Resolution = new(((GameExportIssue)source.SelectedItem).DeviceId, false, ((DetectedDcsDevice)target.SelectedItem).PhysicalGuid,
                    (reference.SelectedItem as GameDeviceChoice)?.Id); DialogResult = true;
            };
            Content = layout; source.SelectedIndex = 0; Availability();
        }
        #region BEGIN Public
        /// <summary>Gets the explicit repair choice, or null when export was cancelled.</summary>
        internal GameExportResolution? Resolution { get; private set; }
        #endregion END Public
    }
}
