using Heimdall.Application;
using Heimdall.Dcs;
using System.Windows;
using System.Windows.Controls;

namespace Heimdall.App.Features.Commands
{
    /// <summary>Shows every device assignment using a selected command.</summary>
    public partial class CommandUsageWindow : Window
    {
        #region BEGIN Public
        public CommandUsageWindow(
            string commandName,
            string category,
            IReadOnlyList<DraftAssignment> assignments,
            EditorWorkspaceSnapshot workspace,
            IReadOnlyDictionary<string, string> labels)
        {
            InitializeComponent();
            Header.Text = $"{commandName} — {assignments.Count} assignment(s)";

            var devices = WorkspaceStructureEditor.Describe(workspace).Devices;
            var groups = assignments
                .GroupBy(a => devices.FirstOrDefault(d => d.ProfileId == a.Slot.ProfileId && d.Layer == a.Slot.Layer)?.Title
                              ?? a.Slot.ProfileId.ToString())
                .OrderBy(g => g.Key, StringComparer.Ordinal);

            foreach (var group in groups)
            {
                var deviceNode = new TreeViewItem { Header = group.Key, IsExpanded = true };
                var categoryNode = new TreeViewItem { Header = category, IsExpanded = true };
                foreach (var a in group.OrderBy(x => x.Slot.InputKey, StringComparer.Ordinal))
                {
                    var diagram = workspace.Diagrams.FirstOrDefault(d => d.ProfileId == a.Slot.ProfileId);
                    var control = diagram?.Controls.FirstOrDefault(c => c.InputKey == a.Slot.InputKey);
                    var defaultLabel = control?.Label ?? a.Slot.InputKey;
                    var assigned = labels.GetValueOrDefault(a.Slot.Key);
                    var assignedText = string.IsNullOrWhiteSpace(assigned) ? "(none)" : assigned;
                    var chord = a.Slot.ModifierChord.Length == 0 ? "Base" : a.Slot.ModifierChord;
                    categoryNode.Items.Add(new TreeViewItem
                    {
                        Header = $"{a.Slot.Layer}  ·  {chord}  ·  {defaultLabel}  →  {assignedText}"
                    });
                }
                deviceNode.Items.Add(categoryNode);
                Usage.Items.Add(deviceNode);
            }

            if (Usage.Items.Count == 0)
                Usage.Items.Add(new TreeViewItem { Header = "Not used" });
        }
        #endregion END Public

        #region BEGIN private helpers
        private void CloseClicked(object sender, RoutedEventArgs e) => Close();
        #endregion END private helpers
    }
}
