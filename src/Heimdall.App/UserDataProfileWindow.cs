using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Heimdall.Infrastructure;
using Heimdall.Application.Interfaces;

namespace Heimdall.App
{
    /// <summary>Exposes explicit profile launch, nondestructive setup rerun and backed-up reset.</summary>
    public sealed class UserDataProfileWindow : Window
    {
        private readonly UserDataProfile _profile;
        private readonly IUserDataProfileService _service;
        private readonly Action _closeDatabase;
        private readonly TextBox _name = new() { Text = "fresh-install", Margin = new Thickness(0, 8, 0, 8) };
        private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap };
        private readonly StackPanel _actions = new();
        private bool _busy;
        /// <summary>Creates lifecycle controls for the current exclusively leased profile.</summary>
        /// <param name="profile">Selected profile.</param>
        /// <param name="service">Profile lifecycle operations.</param>
        /// <param name="closeDatabase">Quiesces services before reset.</param>
        public UserDataProfileWindow(UserDataProfile profile, IUserDataProfileService service, Action closeDatabase)
        {
            _profile = profile;
            _service = service;
            _closeDatabase = closeDatabase;
            Title = "Profiles and reset — " + profile.Name;
            Width = 650; Height = 510;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            SetResourceReference(BackgroundProperty, "WindowBackgroundBrush");
            SetResourceReference(ForegroundProperty, "PrimaryTextBrush");
            var panel = new StackPanel { Margin = new Thickness(24) };
            panel.Children.Add(new TextBlock { Text = profile.Name + "\n" + profile.DataDirectory, TextWrapping = TextWrapping.Wrap });
            panel.Children.Add(new TextBlock { Text = "Named test profiles persist independently. Fly Now and Clear are disabled. Close this instance after opening another profile. Starting normally returns to Production.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 10, 0, 0) });
            panel.Children.Add(_name);
            Add("Open named test profile", (_, _) => Launch(_name.Text));
            Add("Open Production", (_, _) => Launch(null));
            Add("Rerun setup (preserve saved data)", (_, _) =>
            {
                try { ResumeSetupRequested = true; Close(); }
                catch (Exception ex) { _status.Text = ex.Message; }
            });
            Add("Reset this profile's user data…", ResetClicked);
            panel.Children.Add(_actions);
            panel.Children.Add(_status);
            Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            Closing += (_, e) => { if (_busy) e.Cancel = true; };
        }
        #region BEGIN Public
        /// <summary>Gets whether the caller should open the real setup wizard after this dialog closes.</summary>
        public bool ResumeSetupRequested { get; private set; }
        /// <summary>Gets whether services were closed and the application must exit.</summary>
        public bool RequiresRestart { get; private set; }
        #endregion END Public
        #region BEGIN private helpers
        private void Add(string label, RoutedEventHandler handler)
        {
            var button = new Button { Content = label, Margin = new Thickness(0, 6, 0, 6), Padding = new Thickness(10, 4, 10, 4) };
            button.Click += handler;
            _actions.Children.Add(button);
        }
        private void Launch(string? name)
        {
            try
            {
                UserDataProfile.Select(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), name);
                var start = new ProcessStartInfo(Path.Combine(AppContext.BaseDirectory, "Heimdall.exe")) { UseShellExecute = false };
                if (name is not null) { start.ArgumentList.Add("--test-profile"); start.ArgumentList.Add(name); }
                using var process = Process.Start(start);
                _status.Text = "Profile launched. Close this instance when ready.";
            }
            catch (Exception ex) { _status.Text = "Unable to open profile: " + ex.Message; }
        }
        private async void ResetClicked(object sender, RoutedEventArgs e)
        {
            var picker = new ThemeBrowseDialog { Save = true, Title = "Choose mandatory full backup", Filter = "HEIMDALL backup|*.heimdall-backup.zip",
                FileName = "HEIMDALL-before-reset-" + DateTime.Now.ToString("yyyyMMdd-HHmmss", System.Globalization.CultureInfo.InvariantCulture) + ".heimdall-backup.zip",
                OverwritePrompt = false };
            if (picker.ShowDialog(this) != true) return;
            if (ThemeMessageBox.Show(this, "Reset " + _profile.Name + " at\n" + _profile.DataDirectory +
                "?\n\nThis removes saved modules, settings, artwork and setup completion from this profile. A full verified backup is mandatory and the original directory is retained. DCS files stay untouched; existing deployments must be inspected manually before deploying again. HEIMDALL will close; restart using the same profile arguments.",
                "Confirm destructive profile reset", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes) return;
            _busy = true;
            _actions.IsEnabled = false;
            try
            {
                RequiresRestart = true;
                _closeDatabase();
                var retained = await Task.Run(() => _service.Reset(true, picker.FileName));
                _status.Text = "Reset complete. Verified backup: " + picker.FileName + "\nOriginal directory: " + retained + "\nClose this window and restart.";
            }
            catch (Exception ex) { _status.Text = "Reset failed: " + ex.Message + "\nClose this window and restart to reopen retained data."; }
            finally { _busy = false; }
        }
        #endregion END private helpers
    }
}
