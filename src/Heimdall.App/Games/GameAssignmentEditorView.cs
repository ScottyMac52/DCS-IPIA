using System.Windows;
using System.Windows.Controls;
using Heimdall.Domain.MultiSim;

namespace Heimdall.App.Games
{
    /// <summary>Shared selected-command and adjacent feedback frame around an adapter's native editor.</summary>
    public sealed class GameAssignmentEditorView : UserControl
    {
        internal TextBlock AssignmentCommand { get; } = new() { Name = "AssignmentCommand", Text = "No command selected.", TextWrapping = TextWrapping.Wrap };
        internal TextBlock AssignmentDraftStatus { get; } = new() { Name = "AssignmentDraftStatus", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 4) };
        private readonly ContentControl _nativeEditor = new();
        #region BEGIN Public
        /// <summary>Identifies the native editor content supplied by a simulator view.</summary>
        public static readonly DependencyProperty NativeEditorProperty = DependencyProperty.Register(
            nameof(NativeEditor), typeof(UIElement), typeof(GameAssignmentEditorView),
            new PropertyMetadata(null, (owner, args) => ((GameAssignmentEditorView)owner)._nativeEditor.Content = args.NewValue));
        /// <summary>Gets or sets native editor fields while retaining the common command/feedback frame.</summary>
        public UIElement? NativeEditor
        {
            get => (UIElement?)GetValue(NativeEditorProperty);
            set => SetValue(NativeEditorProperty, value);
        }
        /// <summary>Creates the shared command readout and adjacent feedback surface.</summary>
        public GameAssignmentEditorView()
        {
            var layout = new DockPanel();
            var header = new StackPanel();
            AssignmentCommand.SetResourceReference(StyleProperty, "AppLabel");
            AssignmentDraftStatus.SetResourceReference(StyleProperty, "Hint");
            System.Windows.Automation.AutomationProperties.SetLiveSetting(AssignmentCommand, System.Windows.Automation.AutomationLiveSetting.Polite);
            System.Windows.Automation.AutomationProperties.SetLiveSetting(AssignmentDraftStatus, System.Windows.Automation.AutomationLiveSetting.Polite);
            header.Children.Add(AssignmentCommand); header.Children.Add(AssignmentDraftStatus);
            DockPanel.SetDock(header, Dock.Top); layout.Children.Add(header); layout.Children.Add(_nativeEditor);
            Content = layout;
        }
        /// <summary>Hosts native fields without exposing simulator rules to the shared editor frame.</summary>
        public void SetEditor(UIElement editor) => NativeEditor = editor;
        /// <summary>Refreshes the command readout from the authoritative selection state.</summary>
        public void ShowCommand(SimCommand? command) => ShowCommandLabel(command?.DisplayName);
        /// <summary>Accepts an adapter-native command label without requiring conversion of its editing model.</summary>
        public void ShowCommandLabel(string? label) => AssignmentCommand.Text = label ?? "No command selected.";
        #endregion END Public
    }
}
