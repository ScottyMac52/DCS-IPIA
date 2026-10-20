using Heimdall.Domain;

namespace Heimdall.Rendering
{
    /// <summary>Parsed output of one generated DCS profile or modifier file.</summary>
    /// <param name="Path">Exact deployment path.</param>
    /// <param name="ProfileId">Matching physical profile identity, if available.</param>
    /// <param name="Bindings">Actual added, changed and removed diff entries.</param>
    /// <param name="Modifiers">Actual native modifier definitions.</param>
    public sealed record ModuleDocumentationProfile(string Path, Guid? ProfileId,
        IReadOnlyList<DcsBinding> Bindings, IReadOnlyList<DcsModifier> Modifiers);
}
