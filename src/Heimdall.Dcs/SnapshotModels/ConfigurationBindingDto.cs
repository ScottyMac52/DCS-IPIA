using Heimdall.Domain;

namespace Heimdall.Dcs.SnapshotModels
{
    /// <summary>Represents configuration binding dto.</summary>
    /// <param name="ProfileId">The profile identifier.</param>
    /// <param name="Section">The section.</param>
    /// <param name="CommandId">The command identifier.</param>
    /// <param name="CommandName">The command name.</param>
    /// <param name="Disposition">The disposition.</param>
    /// <param name="InputKey">The input key.</param>
    /// <param name="Reformers">The reformers.</param>
    /// <param name="Filter">The filter.</param>
    internal sealed record ConfigurationBindingDto(Guid ProfileId, BindingSection Section, string CommandId,
                                                   string CommandName, BindingDisposition Disposition, string InputKey,
                                                   IReadOnlyList<string> Reformers, AxisFilter? Filter);
}
