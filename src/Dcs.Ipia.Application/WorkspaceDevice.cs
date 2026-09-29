using Dcs.Ipia.Domain;

namespace Dcs.Ipia.Application
{
    /// <summary>A physical device's editable membership in one module-owned scope.</summary>
    /// <param name="ProfileId">Shared physical profile identity.</param>
    /// <param name="Layer">Module or module UI Layer.</param>
    /// <param name="FileName">Editable DCS diff.lua filename.</param>
    /// <param name="Instance">Optional physical instance.</param>
    /// <param name="Role">Optional semantic role.</param>
    public sealed record WorkspaceDevice(Guid ProfileId, AssignmentLayer Layer, string FileName,
                                         string? Instance, string? Role);
}
