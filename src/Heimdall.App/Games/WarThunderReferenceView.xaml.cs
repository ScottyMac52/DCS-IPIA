using System.Windows;
using System.Windows.Controls;
using Heimdall.WarThunder;

namespace Heimdall.App.Games
{
    /// <summary>Exports the supported table reference without implying unverified diagram assignments.</summary>
    public partial class WarThunderReferenceView : UserControl
    {
        private IReadOnlyList<WarThunderWorkspaceRow> _rows = [];
        public WarThunderReferenceView()
        {
            InitializeComponent(); Copy.Click += (_, _) => Clipboard.SetText(WarThunderWorkspaceProjection.ReferenceText(_rows));
        }
        #region BEGIN Public
        internal void Show(IReadOnlyList<WarThunderWorkspaceRow> rows) { _rows = rows; Rows.ItemsSource = rows; Copy.IsEnabled = rows.Count > 0; }
        #endregion END Public
    }
}
