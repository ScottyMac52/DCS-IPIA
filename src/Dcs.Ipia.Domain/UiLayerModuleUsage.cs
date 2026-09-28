namespace Dcs.Ipia.Domain
{
    /// <summary>Represents UI Layer module usage.</summary>
    /// <param name="ModuleId">The module identifier.</param>
    /// <param name="CommandId">The command identifier.</param>
    /// <param name="Enabled">The enabled.</param>
    public sealed record UiLayerModuleUsage(Guid ModuleId, Guid CommandId, bool Enabled);
}
