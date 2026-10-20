namespace Heimdall.Application
{
    /// <summary>Immutable printable content for one controller and activity selection.</summary>
    /// <param name="Title">HEIMDALL controls-profile name.</param>
    /// <param name="Controller">Selected controller label.</param>
    /// <param name="Section">Activity selection.</param>
    /// <param name="Rows">Complete ordered command entries; unillustrated inputs remain representable.</param>
    public sealed record ReferenceSheetContent(string Title, string Controller, string Section, IReadOnlyList<ReferenceSheetRow> Rows);
}
