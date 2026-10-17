using System.IO;
using System.IO.Enumeration;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace DcsIpia.Presentation
{
    /// <summary>Provides asynchronous directory navigation and validated Open, Save and folder selection in the app skin.</summary>
    public partial class BrowseWindow : Window
    {
        private readonly ThemeBrowseDialog _options;
        private readonly List<(string Label, string Pattern)> _filters = [];
        private string _directory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        private int _navigation;
        private bool _loading;
        #region BEGIN Public
        /// <summary>Builds a picker whose resources, chrome and decorative background follow the active appearance.</summary>
        public BrowseWindow(ThemeBrowseDialog options)
        {
            _options = options;
            InitializeComponent();
            Title = options.Title == "Browse" ? options.SelectFolder ? "Select folder" : options.Save ? "Save file" : "Open file" : options.Title;
            Entries.SelectionMode = options.Multiselect && !options.Save && !options.SelectFolder
                ? DataGridSelectionMode.Extended : DataGridSelectionMode.Single;
            NameLabel.Text = options.SelectFolder ? "Folder:" : "File name:";
            AcceptSelection.Content = options.SelectFolder ? "Select _folder" : options.Save ? "_Save" : "_Open";
            FileTypes.Visibility = options.SelectFolder ? Visibility.Collapsed : Visibility.Visible;
            NewFolder.Visibility = options.Save || options.SelectFolder ? Visibility.Visible : Visibility.Collapsed;
            var parts = options.Filter.Split('|');
            for (var i = 0; i + 1 < parts.Length; i += 2) _filters.Add((parts[i], parts[i + 1]));
            if (_filters.Count == 0) _filters.Add(("All files", "*.*"));
            FileTypes.ItemsSource = _filters.Select(x => x.Label + " (" + x.Pattern + ")").ToArray();
            FileTypes.SelectedIndex = 0;
            AddPlace("_Home", _directory);
            AddPlace("_Documents", Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
            AddPlace("_Downloads", Path.Combine(_directory, "Downloads"));
            AddPlace("Saved _Games", Path.Combine(_directory, "Saved Games"));
            foreach (var drive in DriveInfo.GetDrives()) AddPlace(drive.Name, drive.Name);
            SelectionName.Text = options.SelectFolder ? "" : Path.GetFileName(options.FileName);
            Loaded += async (_, _) =>
            {
                var initial = options.InitialDirectory;
                if (string.IsNullOrWhiteSpace(initial) && Path.IsPathFullyQualified(options.FileName))
                    initial = Path.GetDirectoryName(options.FileName) ?? "";
                if (!Directory.Exists(initial)) initial = _directory;
                await NavigateAsync(initial);
                Location.Focus();
            };
            Closed += (_, _) => _navigation++;
        }
        #endregion END Public
        #region BEGIN private helpers
        private void AddPlace(string label, string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            var button = new Button { Content = label, Margin = new Thickness(0, 0, 8, 4) };
            button.Click += async (_, _) => await NavigateAsync(path);
            Places.Children.Add(button);
        }
        private string FullPath(string path) => Path.GetFullPath(Environment.ExpandEnvironmentVariables(path.Trim().Trim('"')), _directory);
        private async Task NavigateAsync(string path)
        {
            var request = ++_navigation;
            _loading = true; AcceptSelection.IsEnabled = false; Status.Text = "Reading folder…";
            try
            {
                var folder = FullPath(path);
                var pattern = _filters[Math.Max(0, FileTypes.SelectedIndex)].Pattern;
                var entries = await Task.Run(() =>
                {
                    var enumeration = new EnumerationOptions { IgnoreInaccessible = false,
                        AttributesToSkip = FileAttributes.Hidden | FileAttributes.System };
                    return Directory.EnumerateDirectories(folder, "*", enumeration)
                        .Select(x => new BrowseEntry(Path.GetFileName(x), x, true))
                        .Concat(_options.SelectFolder ? Enumerable.Empty<BrowseEntry>() :
                            Directory.EnumerateFiles(folder, "*", enumeration)
                                .Where(x => pattern.Split(';').Any(p => p is "*" or "*.*" ||
                                    FileSystemName.MatchesSimpleExpression(p, Path.GetFileName(x), true)))
                                .Select(x => new BrowseEntry(Path.GetFileName(x), x, false)))
                        .OrderByDescending(x => x.IsDirectory).ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToArray();
                });
                if (request != _navigation) return;
                _directory = folder; Location.Text = folder;
                Entries.ItemsSource = entries;
                if (_options.SelectFolder) SelectionName.Text = folder;
                Status.Text = entries.Length + " items. " + (_options.Multiselect ? "Use Ctrl or Shift to select multiple files." : "");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
            {
                if (request == _navigation) { Location.Text = _directory; Status.Text = "Cannot open folder: " + ex.Message; }
            }
            finally
            {
                if (request == _navigation) { _loading = false; AcceptSelection.IsEnabled = true; }
            }
        }
        private async void UpClicked(object sender, RoutedEventArgs e)
        {
            var parent = Directory.GetParent(_directory);
            if (parent is not null) await NavigateAsync(parent.FullName);
        }
        private async void GoClicked(object sender, RoutedEventArgs e) => await NavigateAsync(Location.Text);
        private async void RefreshClicked(object sender, RoutedEventArgs e) => await NavigateAsync(_directory);
        private async void FilterChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsLoaded) await NavigateAsync(_directory);
        }
        private async void LocationKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;
            e.Handled = true; await NavigateAsync(Location.Text);
        }
        private async void NameKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;
            e.Handled = true; await AcceptAsync();
        }
        private void EntrySelected(object sender, SelectionChangedEventArgs e)
        {
            if (Entries.SelectedItem is not BrowseEntry entry) return;
            if (_options.SelectFolder) SelectionName.Text = entry.FullPath;
            else if (!entry.IsDirectory) SelectionName.Text = entry.Name;
            if (Entries.SelectedItems.Count > 1) SelectionName.Text = string.Join("; ", Entries.SelectedItems.Cast<BrowseEntry>().Select(x => x.Name));
        }
        private async void EntryDoubleClicked(object sender, MouseButtonEventArgs e)
        {
            // Header/empty-space double clicks must not activate a stale selection.
            if (ItemsControl.ContainerFromElement(Entries, e.OriginalSource as DependencyObject) is not DataGridRow) return;
            await ActivateEntryAsync();
        }
        private async void EntryKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;
            e.Handled = true; await ActivateEntryAsync();
        }
        private async Task ActivateEntryAsync()
        {
            if (_loading) return;
            if (Entries.SelectedItem is BrowseEntry { IsDirectory: true } folder) await NavigateAsync(folder.FullPath);
            else await AcceptAsync();
        }
        private async void AcceptClicked(object sender, RoutedEventArgs e) => await AcceptAsync();
        private async Task AcceptAsync()
        {
            if (_loading) return;
            try
            {
                var name = SelectionName.Text.Trim();
                if (_options.SelectFolder)
                {
                    var folder = FullPath(string.IsNullOrEmpty(name) ? _directory : name);
                    if (!Directory.Exists(folder)) throw new IOException("Choose an existing folder.");
                    _options.FolderName = folder; DialogResult = true; return;
                }
                if (string.IsNullOrWhiteSpace(name))
                {
                    if (Entries.SelectedItem is BrowseEntry { IsDirectory: true } selected) await NavigateAsync(selected.FullPath);
                    else Status.Text = "Enter a file name or select a file.";
                    return;
                }
                var selectedFiles = Entries.SelectedItems.Cast<BrowseEntry>().Where(x => !x.IsDirectory).ToArray();
                var useSelection = !_options.Save && _options.Multiselect && selectedFiles.Length > 1 &&
                    name == string.Join("; ", Entries.SelectedItems.Cast<BrowseEntry>().Select(x => x.Name));
                var paths = useSelection ? selectedFiles.Select(x => x.FullPath).ToArray() : [FullPath(name)];
                if (paths.Length == 1 && Directory.Exists(paths[0])) { await NavigateAsync(paths[0]); return; }
                if (paths.Length == 1 && (_options.Save || !File.Exists(paths[0])))
                {
                    var suffix = _filters[Math.Max(0, FileTypes.SelectedIndex)].Pattern.Split(';')[0];
                    if (!Path.HasExtension(paths[0]) && suffix.StartsWith("*.", StringComparison.Ordinal) && !suffix[1..].Contains('*') && !suffix.Contains('?'))
                        paths[0] += suffix[1..];
                }
                if (_options.Save)
                {
                    if (!Directory.Exists(Path.GetDirectoryName(paths[0]))) throw new IOException("The destination folder does not exist.");
                    if (File.Exists(paths[0]) && _options.OverwritePrompt && ThemeMessageBox.Show(this,
                        "Replace the existing file?\n" + paths[0], "Confirm save", MessageBoxButton.YesNo,
                        MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes) return;
                }
                else if (_options.CheckFileExists && paths.Any(x => !File.Exists(x))) throw new IOException("Choose existing files.");
                _options.FileNames = paths; _options.FileName = paths[0]; DialogResult = true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
            { Status.Text = "Selection was not accepted: " + ex.Message; }
        }
        private void NewFolderClicked(object sender, RoutedEventArgs e)
        {
            var input = new TextBox { Margin = new Thickness(0, 0, 0, 12) };
            var create = new Button { Content = "_Create", IsDefault = true, MinWidth = 90 };
            var panel = new StackPanel { Margin = new Thickness(20) };
            panel.Children.Add(new TextBlock { Text = "New folder name", Margin = new Thickness(0, 0, 0, 8) });
            panel.Children.Add(input);
            var actions = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right };
            actions.Children.Add(create);
            actions.Children.Add(new Button { Content = "_Cancel", IsCancel = true, MinWidth = 90, Margin = new Thickness(8, 0, 0, 0) });
            panel.Children.Add(actions);
            var dialog = new Window { Title = "New folder", Owner = this, Width = 380, SizeToContent = SizeToContent.Height,
                WindowStartupLocation = WindowStartupLocation.CenterOwner, Content = panel, ShowInTaskbar = false };
            create.Click += async (_, _) =>
            {
                try
                {
                    var name = input.Text.Trim();
                    if (name.Length == 0 || name is "." or ".." || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                        throw new IOException("Enter a single folder name.");
                    var folder = Path.Combine(_directory, name);
                    if (Directory.Exists(folder) || File.Exists(folder)) throw new IOException("That name already exists.");
                    Directory.CreateDirectory(folder); dialog.Close(); await NavigateAsync(folder);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
                { ThemeMessageBox.Show(dialog, ex.Message, "Folder was not created", MessageBoxButton.OK, MessageBoxImage.Warning); }
            };
            dialog.Loaded += (_, _) => input.Focus();
            dialog.ShowDialog();
        }
        #endregion END private helpers
    }
}
