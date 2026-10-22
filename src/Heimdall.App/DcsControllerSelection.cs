using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using Heimdall.Application;
using Heimdall.Dcs;

namespace Heimdall.App
{
    /// <summary>Coordinates controller selection across existing DCS views without owning native profile edits.</summary>
    internal sealed class DcsControllerSelection
    {
        private Guid? _selectedController;
        private bool _linkingController;

        private readonly ListBox _picker;
        private readonly Func<EditorWorkspaceSnapshot?> _workspace;
        private UserControl? _view;

        #region BEGIN Public
        /// <summary>Connects the persistent rail to the current native workspace snapshot.</summary>
        internal DcsControllerSelection(ListBox picker, Func<EditorWorkspaceSnapshot?> workspace)
        {
            _picker = picker; _workspace = workspace;
            _picker.SelectionChanged += ControllerSelectionChanged;
        }

        /// <summary>Mirrors a feature selector while retaining the previous controller across navigation.</summary>
        internal void Link(UserControl view)
        {
            _view = view;
            _linkingController = true;
            try
            {
                BindingOperations.ClearBinding(_picker, Selector.SelectedValueProperty);
                BindingOperations.ClearBinding(_picker, ItemsControl.ItemsSourceProperty);
                var selector = ControllerSelector(view);
                if (selector is null)
                {
                    var snapshot = _workspace();
                    _picker.ItemsSource = snapshot is null ? null : WorkspaceStructureEditor.Describe(snapshot).Devices
                        .Where(x => x.Layer == AssignmentLayer.Module).ToArray();
                    _picker.SelectedValue = _selectedController;
                    SelectControllerPage(view);
                    return;
                }
                if (_selectedController is { } selected) selector.SelectedValue = selected;
                if (selector.SelectedIndex < 0 && selector.Items.Count > 0) selector.SelectedIndex = 0;
                _picker.SetBinding(ItemsControl.ItemsSourceProperty, new Binding("ItemsSource") { Source = selector });
                _picker.SetBinding(Selector.SelectedValueProperty, new Binding("SelectedValue")
                {
                    Source = selector, Mode = System.Windows.Data.BindingMode.TwoWay
                });
                if (_picker.SelectedValue is Guid id) _selectedController = id;
            }
            finally { _linkingController = false; }
        }

        #endregion END Public

        #region BEGIN private helpers
        private static Selector? ControllerSelector(UserControl view) =>
            view.FindName("ManagedDevices") as Selector ?? view.FindName("DeviceChoice") as Selector ?? view.FindName("FeedbackDevice") as Selector;

        private void ControllerSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_linkingController && _picker.SelectedValue is Guid id)
            {
                _selectedController = id;
                if (_view is { } view) SelectControllerPage(view);
            }
        }

        private void SelectControllerPage(UserControl view)
        {
            var snapshot = _workspace();
            if (snapshot is null || _selectedController is not { } selected || view.FindName("KneeboardList") is not ListBox pages) return;
            var key = KneeboardPageLocator.PageKeyForProfile(snapshot, selected);
            if (key is null) return;
            var index = Array.FindIndex(snapshot.Pages.OrderBy(x => x.Order).ToArray(), x => x.StableKey == key);
            if (index >= 0) pages.SelectedIndex = index;
        }

        #endregion END private helpers
    }
}
