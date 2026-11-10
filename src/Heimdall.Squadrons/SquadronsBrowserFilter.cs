using Heimdall.Application;

namespace Heimdall.Squadrons
{
    /// <summary>Keeps command-catalog filters distinct from captured-record and drawing inspection filters.</summary>
    public sealed record SquadronsBrowserFilter(string? Source = null, string Category = "All", string InputType = "All types",
        string Status = "All", string RecordSection = "All", string RecordType = "All inputs", string RecordSearch = "",
        bool Review = true, string? FocusedConcept = null, HardwareGraphicControl? InspectedControl = null, string DrawingSection = "All");
}
