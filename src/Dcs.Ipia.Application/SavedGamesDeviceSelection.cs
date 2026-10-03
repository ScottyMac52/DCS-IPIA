namespace Dcs.Ipia.Application
{
    /// <summary>A reviewed association between a captured profile and an authored hardware template.</summary>
    /// <param name="ProfileId">Source profile identity, including its physical instance.</param>
    /// <param name="Hardware">Hardware explicitly confirmed for this profile.</param>
    public sealed record SavedGamesDeviceSelection(Guid ProfileId, WorkspaceHardware Hardware);
}
