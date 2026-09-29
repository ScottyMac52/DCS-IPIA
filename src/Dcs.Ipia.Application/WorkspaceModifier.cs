using Dcs.Ipia.Domain;

namespace Dcs.Ipia.Application
{
    /// <summary>An editable modifier owned by the module or its UI Layer.</summary>
    /// <param name="Layer">Modifier ownership scope.</param>
    /// <param name="ProfileId">Activator physical device.</param>
    /// <param name="Name">DCS modifier name.</param>
    /// <param name="Key">Shared digital control input.</param>
    /// <param name="SemanticLayer">Semantic modifier layer name.</param>
    /// <param name="Mode">Hold or toggle.</param>
    /// <param name="NativeDevice">Preserved native name for imported unresolved devices.</param>
    public sealed record WorkspaceModifier(AssignmentLayer Layer, Guid ProfileId, string Name, string Key,
                                           string SemanticLayer, ModifierMode Mode, string NativeDevice);
}
