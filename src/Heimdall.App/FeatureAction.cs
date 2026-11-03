using System.Windows.Controls;
using System.Windows.Input;

namespace Heimdall.App
{
    /// <summary>Presentation-only feature action with execution and live availability owned by its source view.</summary>
    public sealed record FeatureAction(string Id, string Label, ICommand Command, object? ToolTip)
    {
        #region BEGIN Public
        /// <summary>Gets the source button whose live visibility and unavailable explanation are mirrored by the shared toolbar.</summary>
        public Button? Source { get; init; }
        #endregion END Public
    }
}
