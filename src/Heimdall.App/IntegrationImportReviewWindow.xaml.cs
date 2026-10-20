using System.Windows;
using Heimdall.Application;
using Heimdall.Dcs;

namespace Heimdall.App
{
    /// <summary>Displays original and proposed module instructions before changing the draft.</summary>
    public partial class IntegrationImportReviewWindow : Window
    {
        /// <summary>Shows full instruction changes and the explicit destination module.</summary>
        /// <param name="workspace">Destination module and previous instructions.</param>
        /// <param name="review">Validated source proposal.</param>
        public IntegrationImportReviewWindow(EditorWorkspaceSnapshot workspace, ModuleIntegrationImportReview review)
        {
            InitializeComponent();
            IntegrationImportHeading.Text = "Import instructions into " + workspace.Configuration.Module.DisplayName + " (" + workspace.Configuration.Module.DcsId + ")";
            IntegrationImportSource.Text = review.FileName + " · " + review.Instructions.Count + " instruction groups";
            var previous = ModuleIntegrationImport.Instructions(workspace);
            IntegrationImportChanges.Text = string.Join("\n\n", review.Instructions.Select(entry => {
                var old = previous.SingleOrDefault(x => string.Equals(x.Name, entry.Name, StringComparison.OrdinalIgnoreCase));
                return (old is null ? "ADD: " : "UPDATE: ") + entry.Name + "\n\nPrevious:\n" +
                    (old?.Instructions ?? "No previous instructions") + "\n\nProposed:\n" + entry.Instructions;
            }));
        }
        #region BEGIN Public
        #endregion END Public
        #region BEGIN private helpers
        private void StageClicked(object sender, RoutedEventArgs e) => DialogResult = true;
        #endregion END private helpers
    }
}
