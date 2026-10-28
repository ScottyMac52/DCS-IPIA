using System.Windows.Controls;
using Heimdall.Application.Games;

namespace Heimdall.App.Games
{
    /// <summary>Owns the searchable native command catalog surface independently of workspace navigation.</summary>
    public partial class GameCommandBrowserView : UserControl
    {
        #region BEGIN Public
        /// <summary>Creates the themed feature component.</summary>
        public GameCommandBrowserView() => InitializeComponent();
        /// <summary>Installs only the adapter's supported native filter descriptors.</summary>
        public void Configure(IReadOnlyList<GameCommandTypeOption> inputTypes, IReadOnlyList<GameCommandStatusOption> statuses)
        {
            CommandTypePicker.ItemsSource = inputTypes;
            AssignmentStatusPicker.ItemsSource = statuses;
        }
        #endregion END Public
    }
}
