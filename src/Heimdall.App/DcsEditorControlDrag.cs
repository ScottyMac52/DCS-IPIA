using Heimdall.Application;

namespace Heimdall.App
{
    /// <summary>Typed WPF drag payload for an assigned database control.</summary>
    /// <param name="Slot">Original occupied slot.</param>
    /// <param name="Scope">Workspace selection scope when dragging began.</param>
    /// <param name="Fingerprint">Native draft before entering the drag loop.</param>
    public sealed record DcsEditorControlDrag(DcsDraftAssignmentSlot Slot, string Scope, string Fingerprint);
}
