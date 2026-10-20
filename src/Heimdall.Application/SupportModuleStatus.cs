namespace Heimdall.Application
{
    /// <summary>Saved module mapping counts without module names, binding content or identifiers.</summary>
    /// <param name="Number">One-based report-local module ordinal.</param>
    /// <param name="Profiles">Saved device profiles.</param>
    /// <param name="Assignments">Saved assignments.</param>
    /// <param name="BundledDrawings">Drawings without custom or generic pins.</param>
    /// <param name="GenericDrawings">Capability-derived drawings.</param>
    /// <param name="CustomDrawings">Pinned user drawings.</param>
    /// <param name="Pages">Saved kneeboard pages.</param>
    public sealed record SupportModuleStatus(int Number, int Profiles, int Assignments,
        int BundledDrawings, int GenericDrawings, int CustomDrawings, int Pages);
}

