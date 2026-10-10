using System.Collections.ObjectModel;
using System.Windows.Input;
using DcsIpia.Presentation.Services;

namespace DcsIpia.Presentation.ViewModels
{
    public class ShellViewModel : BaseViewModel
    {
        private readonly INavigatorService _navigator;

        private object? _active;

        public ShellViewModel(INavigatorService navigator)
        {
            _navigator = navigator;
            _navigator.CurrentChanged += (_, __) => Active = _navigator.Current;
            NavigationItems = new ObservableCollection<string>
            {
                "Devices",
                "Modifiers",
                "Assignments",
                "Commands",
                "Pages",
                "Kneeboard",
                "Documentation",
                "Diagnostics",
                "Sources"
            };
        }

        public ObservableCollection<string> NavigationItems { get; }

        public object? Active
        {
            get => _active;
            set => SetProperty(ref _active, value);
        }
    }
}
