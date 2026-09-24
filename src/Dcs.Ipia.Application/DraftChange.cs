namespace Dcs.Ipia.Application;

/// <summary>Records one slot's original and proposed values for review and undo.</summary>
/// <param name="Slot">Affected input slot.</param>
/// <param name="Before">Assignment before the operation, if any.</param>
/// <param name="After">Assignment after the operation, if any.</param>
public sealed record DraftChange(DraftAssignmentSlot Slot,DraftAssignment? Before,DraftAssignment? After);
