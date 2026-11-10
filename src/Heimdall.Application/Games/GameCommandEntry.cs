using Heimdall.Domain.MultiSim;

namespace Heimdall.Application.Games
{
    /// <summary>Adapter-supplied display facts. Native records and format-specific metadata stay in the adapter.</summary>
    public sealed record GameCommandEntry(SimCommand Command, string Section, string Category,
        IReadOnlyCollection<CommandType> InputTypes, IReadOnlyCollection<string> AssignedControllers)
    {
        #region BEGIN Public
        /// <summary>Gets whether native assignments exist, including system inputs without a joystick owner.</summary>
        public bool HasAssignments { get; init; } = AssignedControllers.Count > 0;
        #endregion END Public
    }
}
