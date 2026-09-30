using System.Windows;
using System.Windows.Controls;
using Dcs.Ipia.Application;

namespace DcsIpia.Presentation
{
    /// <summary>Reopens saved shared defaults independently of aircraft module editors.</summary>
    public partial class SharedUiLayerWorkspaceWindow : Window
    {
        private readonly SharedUiLayerWorkspace _workspace;
        /// <summary>Shows a stored revision and its original catalog and Lua source texts.</summary>
        /// <param name="workspace">Saved shared defaults.</param>
        public SharedUiLayerWorkspaceWindow(SharedUiLayerWorkspace workspace)
        {
            _workspace = workspace;
            InitializeComponent();
            RevisionText.Text = "Revision " + workspace.Revision;
            SourcePaths.ItemsSource = workspace.Sources.Keys.Order(StringComparer.Ordinal).ToArray();
            SourcePaths.SelectedIndex = 0;
        }
        #region BEGIN Public
        #endregion END Public
        #region BEGIN private helpers
        private void SourceChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SourcePaths.SelectedItem is string path) SourceText.Text = _workspace.Sources[path];
        }
        #endregion END private helpers
    }
}
