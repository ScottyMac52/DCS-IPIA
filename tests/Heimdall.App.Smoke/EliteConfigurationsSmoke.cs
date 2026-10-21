using System.IO;
using System.Windows;
using System.Windows.Controls;
using Heimdall.Application.Games;
using Heimdall.App.Games;
using Heimdall.Infrastructure;

namespace Heimdall.App.Smoke
{
    /// <summary>Exercises configuration commands, selection, cancellation and deletion through actual WPF controls.</summary>
    public static class EliteConfigurationsSmoke
    {
        #region BEGIN Public
        /// <summary>Uses a separate temporary store and never writes the user's game bindings.</summary>
        public static void Verify(string root)
        {
            var store = new FileGameWorkspaceStore(Path.Combine(root, "Configurations"));
            var services = new GameWorkspaceServices(store, new GameSmokeDiscovery());
            var pending = MessageBoxResult.Cancel;
            using var window = new EliteWorkspaceWindow(services, [], pendingChoice: () => pending);
            window.Show(); window.Activate(); ThemeControlSmoke.Pump(window);
            if (!Box<Button>(window, "NewConfigurationButton").IsEnabled || Box<Button>(window, "DeleteConfigurationButton").IsEnabled)
                throw new InvalidOperationException("New must be available without a saved configuration; Delete must be disabled.");
            window.ImportFile(Path.Combine(AppContext.BaseDirectory, "Fixtures", "EliteDangerous", "Synthetic.4.2.binds"));
            Box<TextBox>(window, "ProfileName").Text = "Combat";
            if (!window.SaveProfile()) throw new InvalidOperationException("Configuration source did not save.");
            var original = window.Session!.Snapshot.Id;
            window.ConfigurationNamePicker = (_, _) => null; Click(window, "DuplicateConfigurationButton");
            if (store.List().Count != 1) throw new InvalidOperationException("Cancelled duplicate wrote a configuration.");
            window.ConfigurationNamePicker = (_, _) => "Exploration"; Click(window, "DuplicateConfigurationButton");
            var duplicate = window.Session!.Snapshot.Id;
            if (original == duplicate || store.List().Count != 2) throw new InvalidOperationException("Duplicate did not create a distinct saved configuration.");
            window.ConfigurationNamePicker = (_, _) => "COMBAT"; Click(window, "RenameConfigurationButton");
            if (Box<TextBox>(window, "ProfileName").Text != "Exploration") throw new InvalidOperationException("A duplicate name was staged.");
            window.ConfigurationNamePicker = (_, _) => "Trading"; Click(window, "RenameConfigurationButton");
            if (store.Load(duplicate).Workspace.Name != "Exploration" || !window.SaveProfile() || store.Load(duplicate).Workspace.Name != "Trading")
                throw new InvalidOperationException("Rename did not honor explicit Save.");
            Box<TextBox>(window, "ProfileName").Text = "Pending rename";
            window.ConfigurationNamePicker = (_, _) => "Fresh"; Click(window, "NewConfigurationButton");
            if (store.List().Count != 2 || window.Session.Snapshot.Id != duplicate) throw new InvalidOperationException("Cancelled leave created a new configuration.");
            pending = MessageBoxResult.No; Click(window, "NewConfigurationButton");
            var fresh = window.Session!.Snapshot.Id;
            if (store.List().Count != 3 || window.Session.Snapshot.Profile.Devices.Any(x => x.Buttons.Count != 0 || x.Axes.Count != 0))
                throw new InvalidOperationException("New retained assignments or failed to save independently.");
            window.ConfirmConfigurationDeletion = _ => false; Click(window, "DeleteConfigurationButton");
            if (store.List().Count != 3) throw new InvalidOperationException("Cancelled delete removed a configuration.");
            var picker = Box<ComboBox>(window, "ProfilePicker");
            picker.SelectedItem = picker.Items.Cast<GameWorkspaceEntry>().Single(x => x.Id == original);
            if (window.Session!.Snapshot.Id != original || !window.Session.Snapshot.Profile.Devices.Any(x => x.Buttons.Count > 0))
                throw new InvalidOperationException("Switching configurations lost the original assignments.");
            window.ConfirmConfigurationDeletion = _ => true;
            Click(window, "DeleteConfigurationButton");
            if (store.List().Count != 2 || store.ReadSelection().WorkspaceId == original) throw new InvalidOperationException("Delete retained the removed selection.");
            Click(window, "DeleteConfigurationButton"); Click(window, "DeleteConfigurationButton");
            if (store.List().Count != 0 || window.Session is not null || store.ReadSelection().WorkspaceId is not null
                || Box<Button>(window, "DuplicateConfigurationButton").IsEnabled)
                throw new InvalidOperationException("Deleting the last configuration did not restore an empty workspace.");
            window.Close();
        }
        #endregion END Public
        #region BEGIN private helpers
        private static T Box<T>(FrameworkElement root, string name) where T : FrameworkElement => (T)root.FindName(name);
        private static void Click(FrameworkElement root, string name) => Box<Button>(root, name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        #endregion END private helpers
    }
}
