using Dcs.Ipia.Domain;

namespace Dcs.Ipia.Application
{
    /// <summary>Represents module import request.</summary>
    /// <param name="Module">The module.</param>
    /// <param name="Profiles">The profiles.</param>
    /// <param name="ModifiersContent">The modifiers content.</param>
    /// <param name="Provenance">The provenance.</param>
    public sealed record ModuleImportRequest(DcsModule Module, IReadOnlyList<ProfileImportSource> Profiles,
                                             string? ModifiersContent, string Provenance);
}
