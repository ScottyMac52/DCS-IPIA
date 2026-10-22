using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using Heimdall.Application;

namespace Heimdall.App
{
    public partial class ModuleWorkspaceWindow
    {
        private Guid? _selectedController;
        private bool _linkingController;

        #region BEGIN private helpers
        private void LinkControllerSelection(UserControl view)
        {
            _linkingController = true;
            try
            {
                BindingOperations.ClearBinding(ControllerPicker, Selector.SelectedValueProperty);
                BindingOperations.ClearBinding(ControllerPicker, ItemsControl.ItemsSourceProperty);
                var selector = ControllerSelector(view);
                if (selector is null)
                {
                    ControllerPicker.ItemsSource = _working is null ? null : WorkspaceStructureEditor.Describe(_working).Devices
                        .Where(x => x.Layer == AssignmentLayer.Module).ToArray();
                    ControllerPicker.SelectedValue = _selectedController;
                    SelectControllerPage(view);
                    return;
                }
                if (_selectedController is { } selected) selector.SelectedValue = selected;
                if (selector.SelectedIndex < 0 && selector.Items.Count > 0) selector.SelectedIndex = 0;
                ControllerPicker.SetBinding(ItemsControl.ItemsSourceProperty, new Binding("ItemsSource") { Source = selector });
                ControllerPicker.SetBinding(Selector.SelectedValueProperty, new Binding("SelectedValue")
                {
                    Source = selector, Mode = System.Windows.Data.BindingMode.TwoWay
                });
                if (ControllerPicker.SelectedValue is Guid id) _selectedController = id;
            }
            finally { _linkingController = false; }
        }

        private static Selector? ControllerSelector(UserControl view) =>
            view.FindName("ManagedDevices") as Selector ?? view.FindName("DeviceChoice") as Selector ?? view.FindName("FeedbackDevice") as Selector;

        private void ControllerSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_linkingController && ControllerPicker.SelectedValue is Guid id)
            {
                _selectedController = id;
                if (FeatureContent.Content is UserControl view) SelectControllerPage(view);
            }
        }

        private void SelectControllerPage(UserControl view)
        {
            if (_working is null || _selectedController is not { } selected || view.FindName("KneeboardList") is not ListBox pages) return;
            var key = KneeboardPageLocator.PageKeyForProfile(_working, selected);
            if (key is null) return;
            var index = Array.FindIndex(_working.Pages.OrderBy(x => x.Order).ToArray(), x => x.StableKey == key);
            if (index >= 0) pages.SelectedIndex = index;
        }

        private void AssignmentModeClicked(object sender, RoutedEventArgs e)
        {
            if (sender is not RadioButton { Tag: string key }) return;
            FeatureNav.SelectedItem = FeatureNav.Items.Cast<ListBoxItem>().Single(x => Equals(x.Tag, "Assignments"));
            _navigation.NavigateTo(key);
        }

        private void RefreshAssignmentMode(string key)
        {
            AssignmentModes.Visibility = key is "Assignments" or "InputTest" ? Visibility.Visible : Visibility.Collapsed;
            AssignMode.IsChecked = key == "Assignments";
            TestMode.IsChecked = key == "InputTest";
        }
        #endregion END private helpers
    }
}
