namespace Dcs.Ipia.Application;

/// <summary>One command assigned to one explicit module-owned input slot.</summary>
/// <param name="Slot">Device, layer, input, and modifier context.</param>
/// <param name="Command">Command in the same layer.</param>
public sealed record DraftAssignment(DraftAssignmentSlot Slot,DraftCommand Command);
