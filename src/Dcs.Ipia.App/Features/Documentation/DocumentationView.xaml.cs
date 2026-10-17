using Dcs.Ipia.Application;
using Dcs.Ipia.Application.Interfaces;
using Dcs.Ipia.Dcs;
using DcsIpia.Presentation;
using System.Windows;
using System.Windows.Controls;

namespace DcsIpia.Features.Documentation
{
    /// <summary>Edits module-owned integration instructions.</summary>
    public partial class DocumentationView : UserControl
    {
        private IModuleWorkspaceContext? _context;

        #region BEGIN Public
        public DocumentationView() => InitializeComponent();

        /// <summary>Connects this view to the shared workspace and refreshes its displayed state.</summary>
        public void Initialize(IModuleWorkspaceContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            _context = context;
            _context.StateChanged += (_, _) => Refresh();
            Refresh();
        }
        #endregion END Public

        #region BEGIN private helpers
        private void Refresh()
        {
            if (_context?.Working is null)
            {
                Instructions.ItemsSource = null;
                InstructionText.Text = "";
                StatusText.Text = "No working snapshot.";
                return;
            }
            try
            {
                var entries = ModuleIntegrationImport.Instructions(_context.Working);
                var selected = (Instructions.SelectedItem as ModuleIntegrationInstruction)?.Name;
                Instructions.ItemsSource = entries;
                Instructions.SelectedItem = entries.FirstOrDefault(x => x.Name == selected) ?? (entries.Count > 0 ? entries[0] : null);
                StatusText.Text = entries.Count == 0 ? "No integration instructions." : $"{entries.Count} instruction groups.";
            }
            catch (Exception ex)
            {
                StatusText.Text = "Unable to read instructions: " + ex.Message;
            }
        }

        private void InstructionSelected(object sender, SelectionChangedEventArgs e)
        {
            InstructionText.Text = (Instructions.SelectedItem as ModuleIntegrationInstruction)?.Instructions ?? "";
        }

        private void LoadClicked(object sender, RoutedEventArgs e)
        {
            if (_context?.Working is null) return;
            var picker = new ThemeBrowseDialog
            {
                Title = "Select integration instructions",
                Filter = "Integration instructions|*.json;*.md|JSON|*.json|Markdown|*.md"
            };
            var path = _context.IntegrationFilePicker is null
                ? (picker.ShowDialog(Window.GetWindow(this)) == true ? picker.FileName : null)
                : _context.IntegrationFilePicker();
            if (path is null) return;
            try
            {
                var review = ModuleIntegrationImport.ReadFile(_context.Working, path);
                var dialog = new IntegrationImportReviewWindow(_context.Working, review) { Owner = Window.GetWindow(this) };
                if (dialog.ShowDialog() != true) return;
                _context.RunStructureEdit(w => ModuleIntegrationImport.Apply(w, review));
                StatusText.Text = "Instructions staged.";
            }
            catch (Exception ex) { StatusText.Text = "Integration import failed: " + ex.Message; }
        }

        private void RemoveClicked(object sender, RoutedEventArgs e)
        {
            if (Instructions.SelectedItem is not ModuleIntegrationInstruction entry) return;
            _context?.RunStructureEdit(w => ModuleIntegrationImport.Remove(w, entry.Name));
            StatusText.Text = "Instruction removed and staged.";
        }

        private async void SaveClicked(object sender, RoutedEventArgs e)
        {
            if (_context is null) return;
            try
            {
                await _context.SaveAsync();
                StatusText.Text = "Saved to database.";
            }
            catch (Exception ex) { StatusText.Text = "Database save failed: " + ex.Message; }
        }

        private void DiscardClicked(object sender, RoutedEventArgs e)
        {
            _context?.Discard();
            Refresh();
            StatusText.Text = "Pending edits discarded.";
        }
        #endregion END private helpers
    }
}
