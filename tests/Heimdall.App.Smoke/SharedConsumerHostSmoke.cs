using System.Windows;
using System.Windows.Controls;
using Heimdall.App.Games;

namespace Heimdall.App.Smoke
{
    /// <summary>Checks native feature composition, real action routing and lifecycle transitions in the shared consumer host.</summary>
    internal static class SharedConsumerHostSmoke
    {
        #region BEGIN Public
        /// <summary>Exercises shared host behavior without opening an external game or touching saved data.</summary>
        internal static void Verify()
        {
            VerifySynchronousLifetime();
            var compact = new GameWorkspaceShell();
            var separate = new GameWorkspaceShell { SeparateFeatureActions = true };
            if (compact.RowDefinitions.Count != 4 || separate.RowDefinitions.Count != 5 ||
                !compact.RowDefinitions[2].Height.IsStar || !separate.RowDefinitions[3].Height.IsStar)
                throw new InvalidOperationException("The shared shell changed the DCS/Elite workspace row contracts.");
            var content = new ContentControl(); var actions = new FeatureActionBar(); var stopped = 0;
            var host = new GameWorkspaceFeatureHost(content, actions, () => stopped++);
            var native = new TextBox { Text = "Native tuning, layer and modifier fields" };
            var button = new Button { Name = "NativeApply", Content = "Native apply" }; var applied = 0;
            button.Click += (_, _) => applied++;
            var bar = new ActionBar { IsFeatureActions = true }; bar.Children.Add(button);
            var panel = new StackPanel(); panel.Children.Add(native); panel.Children.Add(bar);
            var first = new UserControl { Content = panel };
            host.Show(first);
            var command = actions.Actions.Single().Command;
            command.Execute(null);
            host.Show(first);
            if (stopped != 1 || applied != 1 || !ReferenceEquals(content.Content, first))
                throw new InvalidOperationException("Refreshing the cached feature interrupted native state or action routing.");
            var current = actions.Actions.Single().Command;
            host.Show(new TextBlock { Text = "Unsupported feature" });
            if (actions.Actions.Length != 0 || current.CanExecute(null) || stopped != 2)
                throw new InvalidOperationException("A non-interactive feature retained stale native commands.");
            host.Show(first);
            if (native.Text != "Native tuning, layer and modifier fields")
                throw new InvalidOperationException("Feature navigation replaced native field state.");
            var editor = new GameAssignmentEditorView(); editor.SetEditor(new TextBox { Text = "Native editor" });
            editor.ShowCommandLabel("DCS native command");
            if (editor.NativeEditor is not TextBox || editor.AssignmentCommand.Text != "DCS native command")
                throw new InvalidOperationException("The shared editor did not retain injected native controls and labels.");
            host.Clear(); host.Clear();
            if (stopped != 4 || content.Content is not null || actions.Actions.Length != 0)
                throw new InvalidOperationException("Shared feature cleanup was not idempotent.");
        }
        #endregion END Public
        #region BEGIN private helpers
        private static void VerifySynchronousLifetime()
        {
            var content = new ContentControl(); var actions = new FeatureActionBar();
            var host = new GameWorkspaceFeatureHost(content, actions);
            var first = new FeatureLifetimeProbe(); var second = new FeatureLifetimeProbe();
            host.Show(first, _ =>
            {
                if (!first.Active || first.IsLoaded) throw new InvalidOperationException("Feature preparation ran without synchronous ownership before WPF Loaded.");
            });
            host.Show(first);
            if (first.Activations != 1 || first.Releases != 0) throw new InvalidOperationException("Re-showing a cached feature changed its ownership.");
            host.Show(second, _ =>
            {
                if (first.Active || !second.Active) throw new InvalidOperationException("A new feature prepared while the previous feature still owned selection.");
            });
            host.Show(first);
            if (!first.Active || second.Active || first.Activations != 2 || second.Releases != 1)
                throw new InvalidOperationException("Cached navigation did not transfer selection ownership synchronously.");
            host.Clear(); host.Clear();
            if (first.Active || first.Releases != 2) throw new InvalidOperationException("Clearing the feature retained ownership or released it twice.");
        }
        #endregion END private helpers
    }
}

