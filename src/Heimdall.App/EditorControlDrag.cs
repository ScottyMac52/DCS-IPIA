using Heimdall.Application;

namespace Heimdall.App
{
    /// <summary>Typed WPF drag payload for an assigned database control.</summary>
    /// <param name="Slot">Original occupied slot.</param>
    public sealed record EditorControlDrag(DraftAssignmentSlot Slot);
}
