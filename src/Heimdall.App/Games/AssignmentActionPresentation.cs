using System.Windows;
using System.Windows.Controls;
using Heimdall.Application.Games;

namespace Heimdall.App.Games
{
    /// <summary>Renders the same capability state on source buttons and context-menu items.</summary>
    internal static class AssignmentActionPresentation
    {
        #region BEGIN Public
        /// <summary>Hides unsupported actions and explains supported actions that are currently unavailable.</summary>
        internal static void Show(Control control, WorkspaceAssignmentActionState state)
        {
            control.Visibility = state.Supported ? Visibility.Visible : Visibility.Collapsed;
            control.IsEnabled = state.Enabled;
            control.ToolTip = state.Enabled ? null : state.Reason;
            ToolTipService.SetShowOnDisabled(control, true);
        }
        #endregion END Public
    }
}
