using System.Windows;
using Dcs.Ipia.Dcs;

namespace DcsIpia.Presentation
{
    /// <summary>Displays a command merge proposal before staging it in the module editor.</summary>
    public partial class CommandImportReviewWindow : Window
    {
        /// <summary>Opens the reviewed commands and blocking diagnostics.</summary>
        /// <param name="moduleName">Destination aircraft name.</param>
        /// <param name="review">Parsed and validated command batch.</param>
        public CommandImportReviewWindow(string moduleName, ModuleCommandImportReview review)
        {
            InitializeComponent();
            ImportHeading.Text = "Import commands into " + moduleName;
            ImportSources.Text = string.Join(", ", review.Sources.Select(x => x.FileName)) + " · " + review.Commands.Count + " commands · " +
                review.Changes.Count(x => x.Action == "Add") + " additions · " + review.Changes.Count(x => x.Action == "Update") + " updates";
            ImportChanges.ItemsSource = review.Changes;
            ImportError.Text = review.Error ?? "Stage to update the draft. Save to database to keep the import, or Discard draft to undo it.";
            StageImportButton.IsEnabled = review.Error is null;
        }
        #region BEGIN Public
        #endregion END Public
        #region BEGIN private helpers
        private void StageClicked(object sender, RoutedEventArgs e) => DialogResult = true;
        #endregion END private helpers
    }
}
