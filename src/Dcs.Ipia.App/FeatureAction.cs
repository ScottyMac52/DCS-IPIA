using System.Windows.Input;

namespace DcsIpia.Presentation
{
    /// <summary>Presentation-only feature action with execution owned by its source view.</summary>
    public sealed record FeatureAction(string Id, string Label, ICommand Command, object? ToolTip);
}
