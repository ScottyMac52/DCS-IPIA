using System.IO;
using System.Windows;
using System.Windows.Controls;
using Dcs.Ipia.Application;
using Dcs.Ipia.Application.Interfaces;

namespace DcsIpia.Presentation
{
    /// <summary>Offers verified backup and restore even when the working database cannot be opened.</summary>
    public sealed class UserDataRecoveryWindow : Window
    {
        private readonly IUserDataRecoveryService _recovery;
        private readonly Action _closeDatabase;
        private readonly Func<string?>? _backupPicker;
        private readonly Func<string?>? _restorePicker;
        private readonly Func<bool>? _restoreConfirmation;
        private readonly TextBlock _status;
        private readonly Button _backup;
        private readonly Button _restore;
        private readonly Button _review;
        private readonly Button _support;
        private readonly SupportReportService? _supportReports;
        private bool _busy;
        /// <summary>Creates recovery tools without requiring database-backed UI services.</summary>
        /// <param name="recovery">User-data archive operations.</param>
        /// <param name="closeDatabase">Closes live services before restore.</param>
        /// <param name="initialStatus">Startup failure or saved-state guidance.</param>
        /// <param name="backupPicker">Optional test adapter for destination selection.</param>
        /// <param name="restorePicker">Optional test adapter for source selection.</param>
        /// <param name="restoreConfirmation">Optional test adapter for explicit restore consent.</param>
        /// <param name="supportReports">Optional read-only support capture, including failed startup.</param>
        public UserDataRecoveryWindow(IUserDataRecoveryService recovery, Action? closeDatabase = null,
            string? initialStatus = null, Func<string?>? backupPicker = null, Func<string?>? restorePicker = null,
            Func<bool>? restoreConfirmation = null, SupportReportService? supportReports = null)
        {
            _recovery = recovery;
            _supportReports = supportReports;
            _closeDatabase = closeDatabase ?? (() => { });
            _backupPicker = backupPicker;
            _restorePicker = restorePicker;
            _restoreConfirmation = restoreConfirmation;
            NameScope.SetNameScope(this, new NameScope());
            SetResourceReference(BackgroundProperty, "WindowBackgroundBrush");
            SetResourceReference(ForegroundProperty, "PrimaryTextBrush");
            Title = "HEIMDALL — Data & recovery";
            Width = 680; Height = 480; MinWidth = 500; MinHeight = 380;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            var panel = new StackPanel { Margin = new Thickness(24) };
            panel.Children.Add(new TextBlock { Text = "Saved user data", FontSize = 24, FontWeight = FontWeights.SemiBold });
            panel.Children.Add(new TextBlock { Text = recovery.DataDirectory, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 10, 0, 10) });
            panel.Children.Add(new TextBlock { Text = "Full backups include saved modules, shared defaults, preferences and user files. Save or discard editor drafts and close their windows first. Module snapshots are separate. Restore replaces user data and requires restarting HEIMDALL; DCS files are never restored or removed.", TextWrapping = TextWrapping.Wrap });
            _backup = Button("Back up user data…", "BackupUserDataButton", BackupClicked);
            _restore = Button("Restore backup…", "RestoreUserDataButton", RestoreClicked);
            _review = Button("Review restored deployment…", "ReviewDeploymentButton", ReviewClicked);
            _review.IsEnabled = recovery.DeploymentReview is not null;
            panel.Children.Add(_backup); panel.Children.Add(_restore); panel.Children.Add(_review);
            _support = Button("_Export support report…", "RecoverySupportButton", SupportClicked);
            _support.IsEnabled = supportReports is not null;
            panel.Children.Add(_support);
            _status = new TextBlock { Name = "RecoveryStatus", Text = initialStatus ?? "Choose a backup location outside the user-data directory.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 16, 0, 0) };
            RegisterName(_status.Name, _status);
            panel.Children.Add(_status);
            Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            Closing += (_, e) => { if (_busy) e.Cancel = true; };
        }
        #region BEGIN Public
        /// <summary>Gets whether live services were closed and the application must restart.</summary>
        public bool RequiresRestart { get; private set; }
        #endregion END Public
        #region BEGIN private helpers
        private Button Button(string label, string name, RoutedEventHandler handler)
        {
            var button = new Button { Content = label, Name = name, Padding = new Thickness(14, 4, 14, 4), Margin = new Thickness(0, 10, 0, 0), HorizontalAlignment = HorizontalAlignment.Left };
            RegisterName(name, button);
            button.Click += handler;
            return button;
        }
        private async void BackupClicked(object sender, RoutedEventArgs e)
        {
            if (_busy || RequiresRestart) return;
            var picker = new ThemeBrowseDialog { Save = true, Title = "Back up saved HEIMDALL user data", Filter = "HEIMDALL user-data backup|*.ipia-backup.zip", FileName = "HEIMDALL-" + DateTime.Now.ToString("yyyyMMdd-HHmmss", System.Globalization.CultureInfo.InvariantCulture) + ".ipia-backup.zip", OverwritePrompt = false };
            var path = _backupPicker is not null ? _backupPicker() : picker.ShowDialog(this) == true ? picker.FileName : null;
            if (path is null) return;
            Busy(true);
            try
            {
                _status.Text = "Creating and verifying a saved-state backup…";
                await Task.Run(() => _recovery.Backup(path));
                _status.Text = "Verified full backup saved: " + path;
            }
            catch (Exception ex) { _status.Text = "Backup was not created: " + ex.Message; }
            finally { Busy(false); }
        }
        private async void RestoreClicked(object sender, RoutedEventArgs e)
        {
            if (_busy) return;
            var picker = new ThemeBrowseDialog { Title = "Restore HEIMDALL user data", Filter = "HEIMDALL user-data backup|*.ipia-backup.zip" };
            var path = _restorePicker is not null ? _restorePicker() : picker.ShowDialog(this) == true ? picker.FileName : null;
            if (path is null) return;
            Busy(true);
            try
            {
                _status.Text = "Verifying archive checksums, database integrity and schema compatibility…";
                var info = await Task.Run(() => _recovery.Inspect(path));
                var confirmed = _restoreConfirmation?.Invoke() ?? ThemeMessageBox.Show(this,
                    $"Replace saved user data with this verified backup ({info.FileCount} files; app {info.ApplicationVersion}; database schema {info.DatabaseSchemaVersion})?\n\nThe previous directory will be retained beside the active data. A verified safety archive is also created when the current database is readable. HEIMDALL must close and restart. DCS files remain untouched. Restored deployment records require review.",
                    "Confirm user-data restore", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) == MessageBoxResult.Yes;
                if (!confirmed) { _status.Text = "Restore cancelled; current data is unchanged."; return; }
                RequiresRestart = true;
                _closeDatabase();
                var previous = await Task.Run(() => _recovery.Restore(path));
                _status.Text = "Restore verified. Previous data retained at: " + previous + ". Close this window and restart HEIMDALL.";
            }
            catch (Exception ex) { _status.Text = "Restore did not complete: " + ex.Message + (RequiresRestart ? " Close this window and restart HEIMDALL to reopen recovered data." : " Current data was not replaced."); }
            finally { Busy(false); }
        }
        private void ReviewClicked(object sender, RoutedEventArgs e)
        {
            if (_busy || RequiresRestart || _recovery.DeploymentReview is not { } record) return;
            if (ThemeMessageBox.Show(this, "This record may describe another computer or an earlier state:\n\n" + record +
                "\n\nForget this restored record without deleting or restoring any DCS files? Existing deployed files remain on disk and are no longer managed by Clear. Inspect and clean them manually before deploying again. The backup and retained original directory keep the old evidence.",
                "Review restored deployment", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes) return;
            try { _recovery.ForgetReviewedDeployment(); _review.IsEnabled = false; _status.Text = "Restored record forgotten. No DCS files were changed. Inspect existing files before deploying again."; }
            catch (Exception ex) { _status.Text = "Deployment review did not complete: " + ex.Message; }
        }
        private async void SupportClicked(object sender, RoutedEventArgs e)
        {
            if (_busy || RequiresRestart || _supportReports is null) return;
            Busy(true);
            try { new SupportReportWindow(await _supportReports.CaptureAsync()) { Owner = this }.ShowDialog(); }
            finally { Busy(false); }
        }
        private void Busy(bool busy)
        {
            _busy = busy;
            _support.IsEnabled = !busy && !RequiresRestart && _supportReports is not null;
            _backup.IsEnabled = !busy && !RequiresRestart;
            _restore.IsEnabled = !busy;
            _review.IsEnabled = !busy && !RequiresRestart && _recovery.DeploymentReview is not null;
        }
        #endregion END private helpers
    }
}

