using Heimdall.Domain.MultiSim;

namespace Heimdall.Application.Games
{
    /// <summary>Adapter-supplied display facts. Native records and format-specific metadata stay in the adapter.</summary>
    public sealed record GameCommandEntry(SimCommand Command, string Section, string Category,
        IReadOnlyCollection<CommandType> InputTypes, IReadOnlyCollection<string> AssignedControllers);
}
