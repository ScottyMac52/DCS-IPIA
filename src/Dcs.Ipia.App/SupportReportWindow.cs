using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Dcs.Ipia.Infrastructure;

namespace DcsIpia.Presentation
{
    /// <summary>Reviews the complete support scope before an explicit local-only export.</summary>
    public sealed class SupportReportWindow : Window
    {
        private readonly string _preview;
        private readonly Func<string?>? _picker;
        private readonly TextBlock _status;
        /// <summary>Shows an immutable capture; saving exports precisely this preview.</summary>
        /// <param name="preview">Allow-listed report text.</param>
        /// <param name="picker">Optional destination adapter for smoke testing.</param>
        public SupportReportWindow(string preview, Func<string?>? picker = null)
        {
            _preview = preview;
            _picker = picker;
            NameScope.SetNameScope(this, new NameScope());
            Title = "HEIMDALL — Review support report · " + ApplicationVersion.Label;
            Width = 780; Height = 620; MinWidth = 500; MinHeight = 380;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            SetResourceReference(BackgroundProperty, "WindowBackgroundBrush");
            SetResourceReference(ForegroundProperty, "PrimaryTextBrush");
            var panel = new DockPanel { Margin = new Thickness(20) };
            var scope = new TextBlock
            {
                Text = "Saved state only: app/runtime/schema versions, integrity, numbered device capability and mapping counts, module counts and read failures. Devices are historical inventory; this does not test connectivity or DCS behavior.\n\nExcluded: names, paths, GUIDs, identifiers, assignments, settings, artwork, database bytes, logs, exception messages and stacks. Nothing is uploaded. Review the complete text below before choosing Save.",
                TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 12)
            };
            DockPanel.SetDock(scope, Dock.Top); panel.Children.Add(scope);
            var actions = new StackPanel { Margin = new Thickness(0, 12, 0, 0) };
            var save = new Button { Name = "SaveSupportReportButton", Content = "_Save report…", HorizontalAlignment = HorizontalAlignment.Left, Padding = new Thickness(14, 4, 14, 4) };
            RegisterName(save.Name, save); save.Click += SaveClicked; actions.Children.Add(save);
            _status = new TextBlock { Name = "SupportReportStatus", Text = "Choose a new filename; existing reports are never replaced.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0) };
            RegisterName(_status.Name, _status); actions.Children.Add(_status);
            DockPanel.SetDock(actions, Dock.Bottom); panel.Children.Add(actions);
            var text = new TextBox { Name = "SupportReportPreview", Text = preview, IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.NoWrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto };
            System.Windows.Automation.AutomationProperties.SetName(text, "Complete support report preview");
            RegisterName(text.Name, text); panel.Children.Add(text);
            Content = panel;
            Loaded += (_, _) => text.Focus();
            PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) { Close(); e.Handled = true; } };
        }
        #region BEGIN Public
        #endregion END Public
        #region BEGIN private helpers
        private void SaveClicked(object sender, RoutedEventArgs e)
        {
            var picker = new ThemeBrowseDialog { Save = true, Title = "Save reviewed support report locally", Filter = "Support report|*.json", FileName = "HEIMDALL-support.json", OverwritePrompt = false };
            var path = _picker is not null ? _picker() : picker.ShowDialog(this) == true ? picker.FileName : null;
            if (path is null) { _status.Text = "Export cancelled."; return; }
            try { SupportReportFile.WriteNew(path, _preview); _status.Text = "Report saved locally. Nothing was uploaded."; }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            { _status.Text = "Report was not saved. Choose a new writable filename outside the user-data folder."; }
        }
        #endregion END private helpers
    }
}

